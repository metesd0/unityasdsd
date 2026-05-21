using System.Threading.Tasks;
using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobilOfl.UI
{
    public class MainMenuHud : MonoBehaviour
    {
        public enum RuntimeMenuMode
        {
            Opening,
            Lobby,
            Pause
        }

        private const string MasterVolumeKey = "MobilOfl.MasterVolume";
        private const string LookSensitivityKey = "MobilOfl.LookSensitivity";
        private const string GraphicsQualityKey = "MobilOfl.GraphicsQuality";

        [SerializeField] private RelayNetworkBootstrap bootstrap;
        [SerializeField] private bool startOpen = true;
        [SerializeField] private bool restartCaseOnStart = true;
        [SerializeField] private bool renderWithOnGui = true;

        public static MainMenuHud Instance { get; private set; }
        public static bool IsBlockingGameplay { get; private set; }

        public bool IsOpen => _isOpen;
        public RuntimeMenuMode CurrentMenuMode => _menuMode;
        public bool HasStartedGameplay => _hasStartedGameplay;
        public float MasterVolume => _masterVolume;
        public float CameraSensitivity => _cameraSensitivity;
        public int GraphicsQuality => _graphicsQuality;

        public string PlayerName
        {
            get => _playerName;
            set
            {
                _playerName = PlayerProfileSettings.Sanitize(value);
                PlayerProfileSettings.SavePlayerName(_playerName);
                SubmitLocalProfileToNetworkAvatar();
                RefreshLocalReadyRosterEntry();
            }
        }

        public string JoinCodeInput
        {
            get => _joinCodeInput;
            set => _joinCodeInput = value == null ? string.Empty : value.Trim().ToUpperInvariant();
        }

        public string CurrentStatus => _status;
        public RelayNetworkBootstrap Bootstrap => bootstrap;
        public bool RenderWithOnGui
        {
            get => renderWithOnGui;
            set => renderWithOnGui = value;
        }

        private bool _isOpen;
        private bool _hasStartedGameplay;
        private RuntimeMenuMode _menuMode = RuntimeMenuMode.Opening;
        private string _playerName;
        private string _joinCodeInput = string.Empty;
        private string _status = "Hazir.";
        private float _masterVolume = 0.82f;
        private float _cameraSensitivity = 2f;
        private int _graphicsQuality = 2;
        private float _nextRuntimeSettingsApplyAt;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _metricStyle;

        private bool _pendingSoloStart;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _playerName = PlayerProfileSettings.LoadPlayerName();
            _isOpen = startOpen;
            _menuMode = RuntimeMenuMode.Opening;
            LoadRuntimeSettings();
            ApplyRuntimeSettings();
            SyncBlockingState();
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureBootstrap();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            if (bootstrap != null)
            {
                bootstrap.StatusChanged += HandleStatusChanged;
                bootstrap.JoinCodeChanged += HandleJoinCodeChanged;
                _status = bootstrap.CurrentStatus;
            }
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (bootstrap != null)
            {
                bootstrap.StatusChanged -= HandleStatusChanged;
                bootstrap.JoinCodeChanged -= HandleJoinCodeChanged;
            }

            if (Instance == this)
            {
                IsBlockingGameplay = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsBlockingGameplay = false;
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "SampleScene")
            {
                DestroyInactiveDuplicateNetworkManagers();
                EnsureBootstrap();
                if (bootstrap != null)
                {
                    bootstrap.StatusChanged -= HandleStatusChanged;
                    bootstrap.StatusChanged += HandleStatusChanged;
                    bootstrap.JoinCodeChanged -= HandleJoinCodeChanged;
                    bootstrap.JoinCodeChanged += HandleJoinCodeChanged;
                }

                if (_pendingSoloStart)
                {
                    _pendingSoloStart = false;
                    ExecuteSoloStartOnLoaded();
                }
                else
                {
                    var networkCaseState = NetworkCaseState.Instance;
                    if (bootstrap != null && bootstrap.IsOnlineSessionActive && networkCaseState != null)
                    {
                        if (networkCaseState.IsGameplayPhase)
                        {
                            _hasStartedGameplay = true;
                            _menuMode = RuntimeMenuMode.Pause;
                            CloseMenu();
                        }
                        else
                        {
                            _hasStartedGameplay = false;
                            _menuMode = RuntimeMenuMode.Lobby;
                            OpenMenu();
                        }
                    }
                }
            }
        }

        private void Update()
        {
            MonitorOnlinePhaseTransition();
            if (Time.unscaledTime >= _nextRuntimeSettingsApplyAt)
            {
                ApplyRuntimeSettings();
                _nextRuntimeSettingsApplyAt = Time.unscaledTime + 0.5f;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    CloseMenu();
                }
                else
                {
                    OpenMenu();
                }
            }

            SyncBlockingState();
        }

        private void OnGUI()
        {
            if (!renderWithOnGui)
            {
                return;
            }
            if (!_isOpen)
            {
                EnsureStyles();
                DrawCollapsedButton();
                return;
            }

            EnsureStyles();

            var overlayRect = new Rect(0f, 0f, Screen.width, Screen.height);
            ModernGuiTheme.DrawRect(overlayRect, new Color(0.03f, 0.04f, 0.05f, 0.9f));
            ModernGuiTheme.DrawRect(new Rect(0f, 0f, Screen.width, 110f), new Color(0.1f, 0.08f, 0.05f, 0.4f));
            ModernGuiTheme.DrawRect(new Rect(0f, Screen.height - 170f, Screen.width, 170f), new Color(0.05f, 0.08f, 0.09f, 0.42f));
            ModernGuiTheme.DrawRect(new Rect(Screen.width * 0.62f, 0f, 10f, Screen.height), new Color(ModernGuiTheme.AccentWarmColor.r, ModernGuiTheme.AccentWarmColor.g, ModernGuiTheme.AccentWarmColor.b, 0.16f));

            var width = Mathf.Min(1140f, Screen.width - 48f);
            var height = Mathf.Min(720f, Screen.height - 48f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(rect, _windowStyle);
            DrawHeader();
            GUILayout.Space(12f);
            DrawTopMetrics();
            GUILayout.Space(12f);
            DrawMainContent();
            GUILayout.Space(12f);
            DrawFooter();
            GUILayout.EndArea();
            ModernGuiTheme.DrawPanelChrome(rect, ModernGuiTheme.AccentWarmColor);
        }

        public void OpenMenu(string statusOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(statusOverride))
            {
                _status = statusOverride;
            }

            CaseNotebookHud.Instance?.CloseNotebook();
            RefreshMenuMode();
            _isOpen = true;
            SyncBlockingState();
        }

        public void CloseMenu()
        {
            if (!_hasStartedGameplay && _menuMode != RuntimeMenuMode.Pause)
            {
                _isOpen = true;
                SyncBlockingState();
                return;
            }

            _isOpen = false;
            SyncBlockingState();
        }

        public void StartSoloFromUi()
        {
            StartSoloGame();
        }

        public async void StartHostFromUi()
        {
            await RunOnlineAction(true);
        }

        public async void JoinCurrentCodeFromUi()
        {
            await RunOnlineAction(false);
        }

        public async void ReconnectFromUi()
        {
            await ReconnectLastSessionAsync();
        }

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        public void SetCameraSensitivity(float value)
        {
            _cameraSensitivity = Mathf.Clamp(value, 0.6f, 4.5f);
            PlayerPrefs.SetFloat(LookSensitivityKey, _cameraSensitivity);
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        public void SetGraphicsQuality(int value)
        {
            _graphicsQuality = Mathf.Clamp(value, 0, 2);
            PlayerPrefs.SetInt(GraphicsQualityKey, _graphicsQuality);
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        private void DrawCollapsedButton()
        {
            var rect = new Rect(18f, Screen.height - 76f, 146f, 46f);
            if (GUI.Button(rect, "MENU", _buttonStyle))
            {
                OpenMenu();
            }
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("MOBIL OFL", _titleStyle);
            GUILayout.Label("Okulda gecen co-op suc arastirma prototipi", _subtitleStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(_cardStyle, GUILayout.Width(280f));
            GUILayout.Label("Profil", _mutedStyle);
            _playerName = GUILayout.TextField(_playerName, _textFieldStyle, GUILayout.Height(38f));
            if (_playerName.Length > 18)
            {
                _playerName = _playerName.Substring(0, 18);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawTopMetrics()
        {
            var session = CaseSessionManager.Instance;
            var networkCaseState = NetworkCaseState.Instance;

            GUILayout.BeginHorizontal();
            DrawMetricCard("VAKA", session != null && session.ActiveCase != null ? session.ActiveCase.CaseTitle : "Hazir");
            DrawMetricCard("OYUNCU", string.IsNullOrWhiteSpace(_playerName) ? "Dedektif" : _playerName);
            DrawMetricCard("MOD", bootstrap != null ? bootstrap.CurrentMode : "Offline");
            DrawMetricCard("HAZIR", networkCaseState == null ? "-" : $"{networkCaseState.ReadyPlayerCount}/{networkCaseState.RegisteredPlayerCount}");
            GUILayout.EndHorizontal();
        }

        private void DrawMetricCard(string title, string value)
        {
            GUILayout.BeginVertical(_cardStyle, GUILayout.Height(72f));
            GUILayout.Label(title, _mutedStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, _metricStyle);
            GUILayout.EndVertical();
        }

        private void DrawMainContent()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(390f));
            DrawSessionCard();
            GUILayout.Space(10f);
            DrawControlsCard();
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            GUILayout.BeginVertical();
            DrawMissionCard();
            GUILayout.Space(10f);
            DrawRosterCard();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSessionCard()
        {
            GUILayout.BeginVertical(_cardStyle);
            GUILayout.Label("Oturum", _bodyStyle);
            GUILayout.Label(_status, _mutedStyle);
            if (bootstrap != null)
            {
                GUILayout.Label($"Ag katmani: {bootstrap.BackendLabel}", _mutedStyle);
            }
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tek Basina Basla", _buttonStyle, GUILayout.Height(40f)))
            {
                StartSoloGame();
            }

            GUI.enabled = bootstrap == null || !bootstrap.IsBusy;
            if (GUILayout.Button("Co-op Host", _buttonStyle, GUILayout.Height(40f)))
            {
                _ = RunOnlineAction(isHost: true);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Join Code", _bodyStyle);
            _joinCodeInput = GUILayout.TextField(_joinCodeInput, _textFieldStyle, GUILayout.Height(36f));

            GUI.enabled = bootstrap == null || !bootstrap.IsBusy;
            if (GUILayout.Button("Oturuma Katil", _buttonStyle, GUILayout.Height(38f)))
            {
                _ = RunOnlineAction(isHost: false);
            }
            GUI.enabled = true;

            if (bootstrap != null && bootstrap.CanReconnectLastSession)
            {
                GUILayout.Space(6f);
                GUI.enabled = !bootstrap.IsBusy;
                if (GUILayout.Button("Son Oturuma Yeniden Baglan", _buttonStyle, GUILayout.Height(34f)))
                {
                    _ = ReconnectLastSessionAsync();
                }
                GUI.enabled = true;
            }

            if (bootstrap != null && !string.IsNullOrWhiteSpace(bootstrap.CurrentJoinCode))
            {
                GUILayout.Space(6f);
                GUILayout.Label($"Aktif kod: {bootstrap.CurrentJoinCode}", _bodyStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Kopyala", _buttonStyle, GUILayout.Height(30f)))
                {
                    GUIUtility.systemCopyBuffer = bootstrap.CurrentJoinCode;
                }

                if (GUILayout.Button("Yapistir", _buttonStyle, GUILayout.Height(30f)))
                {
                    _joinCodeInput = GUIUtility.systemCopyBuffer;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawControlsCard()
        {
            GUILayout.BeginVertical(_cardStyle);
            GUILayout.Label("Kontroller", _bodyStyle);
            GUILayout.Label("PC: WASD hareket, Mouse bakis, E etkilesim, Tab dosya, Esc menu.", _mutedStyle);
            GUILayout.Label("Mobil: Joystick hareket, sag ekran bakis, AL etkilesim.", _mutedStyle);
            GUILayout.Label("Ipucu: Vaka masasina donup notebook uzerinden suphelileri karsilastir.", _mutedStyle);
            if (bootstrap != null)
            {
                GUILayout.Label(bootstrap.BackendUpgradeHint, _mutedStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawMissionCard()
        {
            GUILayout.BeginVertical(_cardStyle, GUILayout.ExpandHeight(true));
            GUILayout.Label("Gorev Ozeti", _bodyStyle);

            var session = CaseSessionManager.Instance;
            if (session != null && session.ActiveCase != null)
            {
                GUILayout.Label(session.ActiveCase.OpeningBrief, _bodyStyle);
                GUILayout.Space(8f);
                GUILayout.Label("Siradaki mantikli hamle", _bodyStyle);
                GUILayout.Label(session.GetRecommendedNextStep(), _mutedStyle);
                GUILayout.Space(8f);
                GUILayout.Label($"Toplanan delil: {session.CollectedEvidenceIds.Count}/{session.ActiveCase.EvidenceItems.Count}", _mutedStyle);
                GUILayout.Label($"NPC sorgusu: {session.InterviewedNpcCount}", _mutedStyle);
                GUILayout.Label($"Takim notu: {session.TeamNotes.Count}", _mutedStyle);
            }
            else
            {
                GUILayout.Label("Aktif vaka bilgisi bekleniyor.", _mutedStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawRosterCard()
        {
            GUILayout.BeginVertical(_cardStyle, GUILayout.Height(210f));
            GUILayout.Label("Takim Durumu", _bodyStyle);

            var networkCaseState = NetworkCaseState.Instance;
            if (bootstrap == null || !bootstrap.IsOnlineSessionActive || networkCaseState == null)
            {
                GUILayout.Label("Co-op oturumu acildiginda oyuncular ve hazirlik durumlari burada listelenecek.", _mutedStyle);
            }
            else
            {
                var roster = networkCaseState.GetReadyRoster();
                if (roster.Count == 0)
                {
                    GUILayout.Label("Oyuncu kaydi bekleniyor.", _mutedStyle);
                }
                else
                {
                    for (var i = 0; i < roster.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(roster[i].DisplayName, _bodyStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(roster[i].IsReady ? "HAZIR" : "BEKLIYOR", roster[i].IsReady ? _bodyStyle : _mutedStyle);
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal();

            if (bootstrap != null && bootstrap.IsOnlineSessionActive)
            {
                if (GUILayout.Button("Oturumu Kapat", _buttonStyle, GUILayout.Height(38f), GUILayout.Width(170f)))
                {
                    bootstrap.ShutdownSession();
                    _status = bootstrap.CurrentStatus;
                }

                DrawLobbyActionButton();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Oyuna Don", _buttonStyle, GUILayout.Height(38f), GUILayout.Width(150f)))
            {
                CloseMenu();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLobbyActionButton()
        {
            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState == null || !networkCaseState.IsLobbyPhase)
            {
                return;
            }

            GUILayout.Space(8f);

            var isHost = bootstrap != null && bootstrap.IsHost;
            if (isHost && networkCaseState.CanHostStartInvestigation)
            {
                if (GUILayout.Button("Operasyonu Baslat", _buttonStyle, GUILayout.Height(38f), GUILayout.Width(180f)))
                {
                    networkCaseState.RequestStartInvestigation();
                    _status = "Operasyon baslatiliyor.";
                }

                return;
            }

            var isReady = networkCaseState.GetLocalReadyState();
            var label = isReady ? "Beklemeye Al" : "Hazirim";
            if (GUILayout.Button(label, _buttonStyle, GUILayout.Height(38f), GUILayout.Width(150f)))
            {
                networkCaseState.RequestSetReady(!isReady);
                _status = isReady ? "Hazirlik geri alindi." : "Hazirlik verildi.";
            }
        }

        private async Task RunOnlineAction(bool isHost)
        {
            SaveProfile();
            EnsureBootstrap();
            if (bootstrap == null)
            {
                _status = "Network bootstrap bulunamadi.";
                return;
            }

            var success = isHost
                ? await bootstrap.StartRelayHostAsync()
                : await bootstrap.JoinRelaySessionAsync(_joinCodeInput);

            _status = bootstrap.CurrentStatus;
            if (!success)
            {
                return;
            }

            if (isHost)
            {
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
            }

            _hasStartedGameplay = false;
            _menuMode = RuntimeMenuMode.Lobby;
            OpenMenu(bootstrap.IsHost
                ? "Online lobi hazir. Herkes hazir olunca host operasyonu baslatabilir."
                : "Oturuma katildin. Hazirlik verip hostun operasyonu baslatmasini bekle.");
        }

        private async Task ReconnectLastSessionAsync()
        {
            SaveProfile();
            EnsureBootstrap();
            if (bootstrap == null || !bootstrap.CanReconnectLastSession)
            {
                _status = "Yeniden baglanilabilecek bir onceki relay oturumu bulunamadi.";
                OpenMenu(_status);
                return;
            }

            var success = await bootstrap.AttemptReconnectToLastSessionAsync();
            _status = bootstrap.CurrentStatus;
            if (!success)
            {
                OpenMenu(_status);
                return;
            }

            _hasStartedGameplay = false;
            _menuMode = RuntimeMenuMode.Lobby;
            OpenMenu(_status);
        }

        private void StartSoloGame()
        {
            SaveProfile();
            EnsureBootstrap();

            if (bootstrap != null && bootstrap.IsOnlineSessionActive)
            {
                bootstrap.ShutdownSession();
                _status = bootstrap.CurrentStatus;
            }

            _pendingSoloStart = true;
            LoadGameplayScene();
        }

        private static void DestroyInactiveDuplicateNetworkManagers()
        {
            var managers = Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsInactive.Include);
            if (managers.Length <= 1)
            {
                return;
            }

            var singleton = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkManager canonicalManager = null;
            if (singleton != null && singleton.IsListening)
            {
                for (var i = 0; i < managers.Length; i++)
                {
                    if (managers[i] != null && managers[i] == singleton)
                    {
                        canonicalManager = managers[i];
                        break;
                    }
                }
            }

            if (canonicalManager == null)
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                for (var i = 0; i < managers.Length; i++)
                {
                    if (managers[i] != null && managers[i].gameObject.scene == activeScene)
                    {
                        canonicalManager = managers[i];
                        break;
                    }
                }
            }

            if (canonicalManager == null)
            {
                canonicalManager = managers[0];
            }

            for (var i = 0; i < managers.Length; i++)
            {
                var manager = managers[i];
                if (manager == null || manager == canonicalManager)
                {
                    continue;
                }

                Destroy(manager.gameObject);
            }
        }

        private void ExecuteSoloStartOnLoaded()
        {
            if (restartCaseOnStart && CaseSessionManager.Instance != null)
            {
                CaseSessionManager.Instance.RestartCurrentCase();
            }

            _hasStartedGameplay = true;
            _menuMode = RuntimeMenuMode.Pause;
            CloseMenu();
        }

        private void LoadGameplayScene()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            }
        }

        private void SaveProfile()
        {
            _playerName = PlayerProfileSettings.Sanitize(_playerName);
            PlayerProfileSettings.SavePlayerName(_playerName);
            SubmitLocalProfileToNetworkAvatar();
        }

        private void SubmitLocalProfileToNetworkAvatar()
        {
            var avatars = Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsInactive.Exclude);
            for (var i = 0; i < avatars.Length; i++)
            {
                if (avatars[i] != null && avatars[i].IsOwner)
                {
                    avatars[i].SubmitDisplayName(_playerName);
                }
            }
        }

        private static void RefreshLocalReadyRosterEntry()
        {
            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState != null && networkCaseState.IsOnlineSessionActive)
            {
                networkCaseState.RequestSetReady(networkCaseState.GetLocalReadyState());
            }
        }

        private void SyncBlockingState()
        {
            IsBlockingGameplay = _isOpen;
            Cursor.lockState = _isOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isOpen;
        }

        private void RefreshMenuMode()
        {
            var networkCaseState = NetworkCaseState.Instance;
            if (bootstrap != null &&
                bootstrap.IsOnlineSessionActive &&
                networkCaseState != null &&
                !networkCaseState.IsGameplayPhase)
            {
                _menuMode = RuntimeMenuMode.Lobby;
                return;
            }

            _menuMode = _hasStartedGameplay ? RuntimeMenuMode.Pause : RuntimeMenuMode.Opening;
        }

        private void MonitorOnlinePhaseTransition()
        {
            EnsureBootstrap();

            var networkCaseState = NetworkCaseState.Instance;
            if (bootstrap == null ||
                !bootstrap.IsOnlineSessionActive ||
                networkCaseState == null)
            {
                return;
            }

            if (networkCaseState.IsGameplayPhase)
            {
                if (!_hasStartedGameplay)
                {
                    _status = "Operasyon basladi. Takim sahada.";
                }

                _hasStartedGameplay = true;
                _menuMode = RuntimeMenuMode.Pause;
                if (_isOpen)
                {
                    CloseMenu();
                }
                return;
            }

            if (!_hasStartedGameplay)
            {
                _menuMode = RuntimeMenuMode.Lobby;
            }
        }

        private void LoadRuntimeSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, _masterVolume);
            _cameraSensitivity = PlayerPrefs.GetFloat(LookSensitivityKey, _cameraSensitivity);
            _graphicsQuality = PlayerPrefs.GetInt(GraphicsQualityKey, QualitySettings.GetQualityLevel());
        }

        private void ApplyRuntimeSettings()
        {
            AudioListener.volume = Mathf.Clamp01(_masterVolume);
            QualitySettings.SetQualityLevel(_graphicsQuality, true);

            var controllers = Object.FindObjectsByType<PrototypeFirstPersonController>(FindObjectsInactive.Exclude);
            for (var i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                {
                    controllers[i].LookSensitivity = _cameraSensitivity;
                }
            }
        }

        private void EnsureBootstrap()
        {
            if (bootstrap == null)
            {
                bootstrap = Object.FindAnyObjectByType<RelayNetworkBootstrap>();
            }
        }

        private void HandleStatusChanged(string status)
        {
            _status = status;
        }

        private void HandleJoinCodeChanged(string joinCode)
        {
            if (!string.IsNullOrWhiteSpace(joinCode))
            {
                _joinCodeInput = joinCode;
            }
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _windowStyle = ModernGuiTheme.CreatePanelStyle(new RectOffset(24, 24, 22, 22));
            _cardStyle = ModernGuiTheme.CreateSoftPanelStyle(new RectOffset(14, 14, 12, 12));
            _titleStyle = ModernGuiTheme.CreateLabelStyle(54, true, false);
            _subtitleStyle = ModernGuiTheme.CreateLabelStyle(16, false, false, ModernGuiTheme.MutedTextColor);
            _bodyStyle = ModernGuiTheme.CreateLabelStyle(17, true, false);
            _mutedStyle = ModernGuiTheme.CreateLabelStyle(13, false, false, ModernGuiTheme.MutedTextColor);
            _metricStyle = ModernGuiTheme.CreateLabelStyle(24, true, false);
            _buttonStyle = ModernGuiTheme.CreateButtonStyle(15);
            _textFieldStyle = ModernGuiTheme.CreateTextFieldStyle(16);
        }
    }
}







