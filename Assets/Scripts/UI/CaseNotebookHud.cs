using System.Collections.Generic;
using MobilOfl.Case;
using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobilOfl.UI
{
    public class CaseNotebookHud : MonoBehaviour
    {
        private enum NotebookTab
        {
            Overview,
            Evidence,
            Interviews,
            Notes,
            Suspects
        }

        [SerializeField] private MobileButton toggleButton;
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private bool startOpen;
        [SerializeField] private bool renderWithOnGui = true;

        public static CaseNotebookHud Instance { get; private set; }
        public static bool IsAnyNotebookOpen { get; private set; }

        public bool IsOpen => _isOpen;
        public int CurrentTabIndex => (int)_selectedTab;
        public bool RenderWithOnGui
        {
            get => renderWithOnGui;
            set => renderWithOnGui = value;
        }

        private readonly List<string> _collectedEvidenceCache = new List<string>();
        private bool _isOpen;
        private string _selectedEvidenceId;
        private string _selectedMotive;
        private string _selectedTimeline;
        private string _noteDraft = string.Empty;
        private NotebookTab _selectedTab;
        private Vector2 _overviewScroll;
        private Vector2 _evidenceScroll;
        private Vector2 _interviewScroll;
        private Vector2 _notesScroll;
        private Vector2 _suspectScroll;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _lockedStyle;
        private GUIStyle _chipStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _panelCardStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;

        private void Awake()
        {
            Instance = this;
            _isOpen = false;
            IsAnyNotebookOpen = false;
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
                IsAnyNotebookOpen = _isOpen;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            if (_isOpen)
            {
                _isOpen = false;
            }

            if (Instance == this)
            {
                IsAnyNotebookOpen = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsAnyNotebookOpen = false;
            }
        }

        private void Update()
        {
            if (_isOpen || IsAnyNotebookOpen)
            {
                CloseNotebook();
            }
        }

        public void OpenNotebook()
        {
            CloseNotebook();
        }

        public void OpenSuspectsNotebook()
        {
            CloseNotebook();
        }

        public void CloseNotebook()
        {
            _isOpen = false;
            IsAnyNotebookOpen = false;
        }

        public void SetTabIndex(int tabIndex)
        {
            _selectedTab = (NotebookTab)Mathf.Clamp(tabIndex, 0, 4);
        }

        public void SetToggleButton(MobileButton button)
        {
            toggleButton = button;
        }

        private void OnGUI()
        {
            if (!renderWithOnGui)
            {
                return;
            }
            if (MainMenuHud.IsBlockingGameplay || !_isOpen)
            {
                return;
            }

            var session = CaseSessionManager.Instance;
            if (session == null || session.ActiveCase == null)
            {
                return;
            }

            EnsureStyles();
            RefreshCollectedEvidenceCache(session);
            EnsureSelectedEvidence(session.ActiveCase);

            ModernGuiTheme.DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.04f, 0.72f));

            var width = Mathf.Min(1120f, Screen.width - 42f);
            var height = Mathf.Min(744f, Screen.height - 42f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(rect, _windowStyle);
            DrawHeader(session);
            GUILayout.Space(10f);
            DrawTabBar();
            GUILayout.Space(10f);

            switch (_selectedTab)
            {
                case NotebookTab.Overview:
                    DrawOverviewTab(session);
                    break;
                case NotebookTab.Evidence:
                    DrawEvidenceTab(session);
                    break;
                case NotebookTab.Interviews:
                    DrawInterviewsTab(session);
                    break;
                case NotebookTab.Notes:
                    DrawNotesTab(session);
                    break;
                case NotebookTab.Suspects:
                    DrawSuspectsTab(session);
                    break;
            }

            GUILayout.EndArea();
            ModernGuiTheme.DrawPanelChrome(rect, ModernGuiTheme.AccentColor);
        }

        private void DrawHeader(CaseSessionManager session)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("VAKA DOSYASI", _titleStyle);
            GUILayout.Label(session.ActiveCase.CaseTitle, _sectionStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label($"Sure: {FormatElapsedTime(session.ElapsedCaseTimeSeconds)}", _bodyStyle);
            GUILayout.Label($"Delil: {session.CollectedEvidenceIds.Count}/{session.ActiveCase.EvidenceItems.Count}", _bodyStyle);
            GUILayout.Label($"Kritik: {session.CollectedCriticalEvidenceCount}/{session.TotalCriticalEvidenceCount}", _bodyStyle);
            GUILayout.Label($"Takim notu: {session.TeamNotes.Count}", _bodyStyle);
            var exploration = Object.FindAnyObjectByType<SchoolExplorationTracker>();
            if (exploration != null)
            {
                GUILayout.Label($"Kesif: {exploration.VisitedZoneCount} bolge", _bodyStyle);
            }
            GUILayout.EndVertical();

            if (GUILayout.Button("Kapat", _buttonStyle, GUILayout.Width(96f), GUILayout.Height(34f)))
            {
                CloseNotebook();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            DrawTabButton(NotebookTab.Overview, "Genel Durum");
            DrawTabButton(NotebookTab.Evidence, "Deliller");
            DrawTabButton(NotebookTab.Interviews, "Sorgular");
            DrawTabButton(NotebookTab.Notes, "Notlar");
            DrawTabButton(NotebookTab.Suspects, "Supheliler");
            GUILayout.EndHorizontal();
        }

        private void DrawTabButton(NotebookTab tab, string label)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = _selectedTab == tab ? ModernGuiTheme.AccentColor : new Color(0.2f, 0.2f, 0.2f);

            if (GUILayout.Button(label, _tabStyle, GUILayout.Height(34f)))
            {
                _selectedTab = tab;
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawOverviewTab(CaseSessionManager session)
        {
            _overviewScroll = GUILayout.BeginScrollView(_overviewScroll);

            GUILayout.Label("Brifing", _sectionStyle);
            GUILayout.Label(session.ActiveCase.OpeningBrief, _bodyStyle);
            GUILayout.Space(8f);

            GUILayout.Label("Siradaki Mantikli Hamle", _sectionStyle);
            GUILayout.Label(session.GetRecommendedNextStep(), _bodyStyle);
            GUILayout.Space(8f);

            GUILayout.Label("Dosya Analizi", _sectionStyle);
            GUILayout.BeginVertical(_panelCardStyle);
            GUILayout.Label(session.GetReasoningSummary(), _bodyStyle);
            GUILayout.EndVertical();
            GUILayout.Space(8f);

            var onlineBootstrap = Object.FindAnyObjectByType<RelayNetworkBootstrap>();
            if (onlineBootstrap != null)
            {
                GUILayout.Label("Co-op Durumu", _sectionStyle);
                GUILayout.Label(onlineBootstrap.CurrentStatus, _bodyStyle);

                if (!string.IsNullOrWhiteSpace(onlineBootstrap.CurrentJoinCode))
                {
                    GUILayout.Label($"Join code: {onlineBootstrap.CurrentJoinCode}", _bodyStyle);
                }

                var networkCaseState = NetworkCaseState.Instance;
                if (networkCaseState != null && onlineBootstrap.IsOnlineSessionActive)
                {
                    GUILayout.Label($"Hazir oyuncu: {networkCaseState.ReadyPlayerCount}/{networkCaseState.RegisteredPlayerCount}", _bodyStyle);
                }

                GUILayout.Space(8f);
            }

            GUILayout.BeginHorizontal();
            DrawSummaryCard($"Toplanan Delil\n{session.CollectedEvidenceIds.Count}");
            DrawSummaryCard($"Kritik Delil\n{session.CollectedCriticalEvidenceCount}/{session.TotalCriticalEvidenceCount}");
            DrawSummaryCard($"Gorusulen NPC\n{session.InterviewedNpcCount}");
            var explorationSummary = Object.FindAnyObjectByType<SchoolExplorationTracker>();
            DrawSummaryCard(explorationSummary == null ? "Kesif\n-" : $"Kesif\n{explorationSummary.VisitedZoneCount}");
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            var explorationTracker = Object.FindAnyObjectByType<SchoolExplorationTracker>();
            if (explorationTracker != null && explorationTracker.VisitedZoneCount > 0)
            {
                GUILayout.Label("Gezilen Bolgeler", _sectionStyle);
                GUILayout.BeginVertical(_panelCardStyle);
                GUILayout.Label(string.Join("  |  ", explorationTracker.VisitedZones), _bodyStyle);
                GUILayout.EndVertical();
                GUILayout.Space(10f);
            }

            if (session.TeamNotes.Count > 0)
            {
                GUILayout.Label("Son Takim Notu", _sectionStyle);
                GUILayout.BeginVertical(_panelCardStyle);
                GUILayout.Label(session.TeamNotes[session.TeamNotes.Count - 1], _bodyStyle);
                GUILayout.EndVertical();
                GUILayout.Space(10f);
            }

            GUILayout.Label("Son Gelismeler", _sectionStyle);

            if (session.MessageHistory.Count == 0)
            {
                GUILayout.Label("Henuz oturum kaydi yok.", _lockedStyle);
            }
            else
            {
                for (var i = session.MessageHistory.Count - 1; i >= 0; i--)
                {
                    GUILayout.Label($"- {session.MessageHistory[i]}", _bodyStyle);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawSummaryCard(string text)
        {
            GUILayout.BeginVertical(_panelCardStyle, GUILayout.Height(84f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, _chipStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawEvidenceTab(CaseSessionManager session)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(460f));
            GUILayout.Label("Bulunan Deliller", _sectionStyle);

            _evidenceScroll = GUILayout.BeginScrollView(_evidenceScroll, GUILayout.Height(420f));

            foreach (var evidence in session.ActiveCase.EvidenceItems)
            {
                if (evidence == null)
                {
                    continue;
                }

                var collected = _collectedEvidenceCache.Contains(evidence.Id);
                GUI.enabled = collected;

                var label = collected ? evidence.Title : "Kilitli delil";
                if (GUILayout.Button(label, GUILayout.Height(34f)))
                {
                    _selectedEvidenceId = evidence.Id;
                }

                GUI.enabled = true;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12f);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("Delil Detayi", _sectionStyle);
            DrawEvidenceDetails(session.ActiveCase);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawEvidenceDetails(CaseDefinition activeCase)
        {
            if (string.IsNullOrWhiteSpace(_selectedEvidenceId) ||
                !_collectedEvidenceCache.Contains(_selectedEvidenceId))
            {
                GUILayout.Label("Bir delil sec. Kilitli deliller once sahnede bulunmali.", _lockedStyle);
                return;
            }

            EvidenceData selectedEvidence = null;

            foreach (var evidence in activeCase.EvidenceItems)
            {
                if (evidence != null && evidence.Id == _selectedEvidenceId)
                {
                    selectedEvidence = evidence;
                    break;
                }
            }

            if (selectedEvidence == null)
            {
                return;
            }

            GUILayout.BeginVertical(_panelCardStyle);
            GUILayout.Label(selectedEvidence.Title, _sectionStyle);
            GUILayout.Label($"Kategori: {selectedEvidence.Category}", _bodyStyle);
            GUILayout.Label(selectedEvidence.IsCritical ? "Oncelik: Kritik delil" : "Oncelik: Yardimci delil", _bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label(selectedEvidence.Description, _bodyStyle);
            GUILayout.EndVertical();
        }

        private void DrawInterviewsTab(CaseSessionManager session)
        {
            GUILayout.Label("NPC Sorgu Kayitlari", _sectionStyle);
            GUILayout.Label("Buldugun delillerle yeni diyaloglar acilir. Kayitlar burada tutulur.", _bodyStyle);
            GUILayout.Space(8f);

            _interviewScroll = GUILayout.BeginScrollView(_interviewScroll);

            if (session.ConversationHistory.Count == 0)
            {
                GUILayout.Label("Henuz sorgu kaydi yok.", _lockedStyle);
            }
            else
            {
                for (var i = session.ConversationHistory.Count - 1; i >= 0; i--)
                {
                    GUILayout.BeginVertical(_panelCardStyle);
                    GUILayout.Label(session.ConversationHistory[i], _bodyStyle);
                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawNotesTab(CaseSessionManager session)
        {
            GUILayout.Label("Takim Notlari", _sectionStyle);
            GUILayout.Label("Buldugunuz ipuclarini ortak dosyaya kisa notlar halinde dusun. Online oturumda herkesle senkronize olur.", _bodyStyle);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(520f));
            _notesScroll = GUILayout.BeginScrollView(_notesScroll, GUILayout.Height(430f));

            if (session.TeamNotes.Count == 0)
            {
                GUILayout.Label("Henuz takim notu yok.", _lockedStyle);
            }
            else
            {
                for (var i = session.TeamNotes.Count - 1; i >= 0; i--)
                {
                    GUILayout.BeginVertical(_panelCardStyle);
                    GUILayout.Label(session.TeamNotes[i], _bodyStyle);
                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12f);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(_panelCardStyle);
            GUILayout.Label("Yeni Not Ekle", _sectionStyle);
            GUILayout.Label("Kisa ve tek satir notlar en temiz akisi verir.", _bodyStyle);
            GUILayout.Space(8f);

            _noteDraft = GUILayout.TextField(_noteDraft, _textFieldStyle, GUILayout.Height(38f));
            if (_noteDraft.Length > 140)
            {
                _noteDraft = _noteDraft.Substring(0, 140);
            }

            GUILayout.Label($"{_noteDraft.Length}/140", _bodyStyle);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();

            GUI.enabled = !string.IsNullOrWhiteSpace(_noteDraft);
            if (GUILayout.Button("Takima Ekle", _buttonStyle, GUILayout.Height(36f)))
            {
                SubmitTeamNote();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Temizle", _buttonStyle, GUILayout.Height(36f)))
            {
                _noteDraft = string.Empty;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSuspectsTab(CaseSessionManager session)
        {
            GUILayout.Label("Supheli Dosyalari", _sectionStyle);
            GUILayout.Label("Eslesen deliller arttikca suclama icin guven artar.", _bodyStyle);
            GUILayout.Space(8f);

            _suspectScroll = GUILayout.BeginScrollView(_suspectScroll);

            if (session.HasAnyAccusableSuspect())
            {
                DrawFinalDecisionSelectors(session);
                GUILayout.Space(8f);
            }

            foreach (var suspect in session.ActiveCase.Suspects)
            {
                if (suspect == null)
                {
                    continue;
                }

                var matchCount = session.GetSuspectEvidenceMatchCount(suspect);
                var confidence = session.GetSuspectConfidencePercent(suspect.Id);
                var canAccuse = session.CanAccuse(suspect.Id);

                GUILayout.BeginVertical(_panelCardStyle);
                GUILayout.Label(suspect.DisplayName, _sectionStyle);
                GUILayout.Label(suspect.Summary, _bodyStyle);
                GUILayout.Label($"Eslesen delil: {matchCount}/{suspect.RequiredEvidenceIds.Count}", _bodyStyle);
                GUILayout.Label($"Suphe yogunlugu: %{confidence}", _bodyStyle);
                DrawProgressBar(confidence / 100f, new Color(0.18f, 0.74f, 0.42f), $"Guven %{confidence}");
                GUILayout.Label(session.GetMissingEvidenceSummary(suspect), _bodyStyle);
                GUILayout.Space(6f);

                GUI.enabled = canAccuse && !session.IsCaseResolved && HasFinalDecisionSelections();
                if (GUILayout.Button(canAccuse ? "Secili zincirle sucla" : "Daha fazla delil gerekiyor", _buttonStyle, GUILayout.Height(36f)))
                {
                    var networkCaseState = NetworkCaseState.Instance;
                    if (networkCaseState != null && networkCaseState.IsOnlineSessionActive)
                    {
                        networkCaseState.RequestResolveSuspect(suspect.Id, _selectedMotive, _selectedTimeline);
                    }
                    else
                    {
                        session.TryResolveCase(suspect.Id, _selectedMotive, _selectedTimeline, out _);
                    }
                }

                GUI.enabled = true;
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void DrawFinalDecisionSelectors(CaseSessionManager session)
        {
            GUILayout.BeginVertical(_panelCardStyle);
            GUILayout.Label("Final Karar Zinciri", _sectionStyle);
            GUILayout.Label("Suclama icin supheliyi, motivasyonu ve olay siralamasini birlikte dogrula.", _bodyStyle);
            GUILayout.Space(6f);

            DrawChoiceButtons("Motivasyon", session.ActiveCase.MotiveOptions, session.ActiveCase.CulpritMotive, ref _selectedMotive);
            GUILayout.Space(6f);
            DrawChoiceButtons("Olay Sirasi", session.ActiveCase.TimelineOptions, session.ActiveCase.CulpritTimeline, ref _selectedTimeline);

            GUILayout.EndVertical();
        }

        private void DrawChoiceButtons(string label, IReadOnlyList<string> configuredOptions, string fallbackOption, ref string selectedValue)
        {
            GUILayout.Label(label, _sectionStyle);
            var options = BuildChoiceOptions(configuredOptions, fallbackOption);
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var isSelected = option == selectedValue;
                var buttonLabel = isSelected ? "[Secili] " + option : option;
                if (GUILayout.Button(buttonLabel, _buttonStyle, GUILayout.Height(42f)))
                {
                    selectedValue = option;
                }
            }
        }

        private static List<string> BuildChoiceOptions(IReadOnlyList<string> configuredOptions, string fallbackOption)
        {
            var options = new List<string>();
            if (configuredOptions != null)
            {
                foreach (var option in configuredOptions)
                {
                    if (!string.IsNullOrWhiteSpace(option))
                    {
                        options.Add(option);
                    }
                }
            }

            if (options.Count == 0 && !string.IsNullOrWhiteSpace(fallbackOption))
            {
                options.Add(fallbackOption);
            }

            return options;
        }

        private bool HasFinalDecisionSelections()
        {
            return !string.IsNullOrWhiteSpace(_selectedMotive) && !string.IsNullOrWhiteSpace(_selectedTimeline);
        }

        private void SubmitTeamNote()
        {
            var trimmedNote = _noteDraft.Trim();
            if (string.IsNullOrWhiteSpace(trimmedNote))
            {
                return;
            }

            var networkCaseState = NetworkCaseState.Instance;
            var session = CaseSessionManager.Instance;
            var added = false;

            if (networkCaseState != null && networkCaseState.IsOnlineSessionActive)
            {
                added = networkCaseState.RequestAddTeamNote(networkCaseState.GetLocalPlayerLabel(), trimmedNote);
            }
            else if (session != null)
            {
                added = session.AddTeamNote("Sen", trimmedNote);
            }

            if (added)
            {
                _noteDraft = string.Empty;
            }
        }

        private void RefreshCollectedEvidenceCache(CaseSessionManager session)
        {
            _collectedEvidenceCache.Clear();

            foreach (var evidenceId in session.CollectedEvidenceIds)
            {
                _collectedEvidenceCache.Add(evidenceId);
            }
        }

        private void EnsureSelectedEvidence(CaseDefinition activeCase)
        {
            if (!string.IsNullOrWhiteSpace(_selectedEvidenceId))
            {
                return;
            }

            foreach (var evidence in activeCase.EvidenceItems)
            {
                if (evidence != null && _collectedEvidenceCache.Contains(evidence.Id))
                {
                    _selectedEvidenceId = evidence.Id;
                    return;
                }
            }
        }

        private static string FormatElapsedTime(float timeSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(timeSeconds));
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        private void DrawProgressBar(float normalized, Color fillColor, string label)
        {
            var rect = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
            ModernGuiTheme.DrawProgress(rect, normalized, fillColor, label, _chipStyle);
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _windowStyle = ModernGuiTheme.CreatePanelStyle(new RectOffset(22, 22, 18, 20));
            _panelCardStyle = ModernGuiTheme.CreateSoftPanelStyle(new RectOffset(14, 14, 12, 12));
            _titleStyle = ModernGuiTheme.CreateLabelStyle(34, true, false);
            _sectionStyle = ModernGuiTheme.CreateLabelStyle(17, true, false);
            _bodyStyle = ModernGuiTheme.CreateLabelStyle(14, false, false, ModernGuiTheme.TextColor);
            _lockedStyle = ModernGuiTheme.CreateLabelStyle(14, false, false, ModernGuiTheme.MutedTextColor);
            _chipStyle = ModernGuiTheme.CreateLabelStyle(18, true, true);
            _tabStyle = ModernGuiTheme.CreateButtonStyle(14);
            _buttonStyle = ModernGuiTheme.CreateButtonStyle(14);
            _textFieldStyle = ModernGuiTheme.CreateTextFieldStyle(15);
        }
    }
}










