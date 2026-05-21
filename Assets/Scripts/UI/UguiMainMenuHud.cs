using MobilOfl.Gameplay;
using MobilOfl.Online;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public class UguiMainMenuHud : MonoBehaviour
    {
        [SerializeField] private MainMenuHud logic;

        private Canvas _canvas;
        private RectTransform _overlayRoot;
        private RectTransform _collapsedRoot;
        private RectTransform _windowRoot;
        private RectTransform _headerRoot;
        private RectTransform _openingRoot;
        private RectTransform _lobbyRoot;
        private RectTransform _pauseRoot;
        private RectTransform _rosterContent;
        private CanvasGroup _overlayGroup;
        private InputField _playerNameField;
        private InputField _openingJoinField;
        private InputField _lobbyJoinField;
        private Text _titleText;
        private Text _subtitleText;
        private Text _modeBadgeText;
        private Text _statusText;
        private Text _casePreviewText;
        private Text _openingSettingsText;
        private Text _lobbyCodeText;
        private Text _lobbyHintText;
        private Text _readyButtonText;
        private Text _pauseSummaryText;
        private Text _pauseSettingsText;
        private Text _roleButtonText;
        private Button _soloButton;
        private Button _hostButton;
        private Button _openingJoinButton;
        private Button _lobbyJoinButton;
        private Button _reconnectButton;
        private Button _readyButton;
        private Button _closeButton;
        private Button _resetSaveButton;
        private Button _roleButton;
        private float _openBlend;
        private float _nextRefreshAt;
        private bool _built;
        private bool _syncingFields;

        // --- DETECTIVE THEME COLOR PALETTE ---
        private static readonly Color ThemeBgColor = new Color(0.025f, 0.035f, 0.065f, 0.97f); // Deeper midnight
        private static readonly Color ThemePanelColor = new Color(0.065f, 0.088f, 0.14f, 0.94f); // Darker slate panel
        private static readonly Color ThemeBorderColor = new Color(0.16f, 0.24f, 0.38f, 0.85f); // Sharper border
        private static readonly Color ThemeAccentColor = new Color(0.22f, 0.74f, 0.97f, 1f); // #38BDF8 Sky Cyan
        private static readonly Color ThemeTextColor = new Color(0.97f, 0.98f, 0.99f, 1f); // #F8FAFC Off-white
        private static readonly Color ThemeMutedColor = new Color(0.44f, 0.52f, 0.62f, 1f); // Slightly brighter muted
        private static readonly Color ThemeDangerColor = new Color(0.94f, 0.27f, 0.27f, 1f); // #EF4444 Crimson red
        private static readonly Color ThemeHighlightColor = new Color(0.96f, 0.62f, 0.04f, 1f); // #F59E0B Gold
        private static readonly Color ThemeSidebarColor = new Color(0.035f, 0.048f, 0.082f, 0.98f); // Deep sidebar
        private static readonly Color ThemeCardBgColor = new Color(0.045f, 0.062f, 0.105f, 0.92f); // Card background
        private static readonly Color ThemeSectionCardColor = new Color(0.055f, 0.075f, 0.125f, 0.9f); // Section cards

        public enum MenuTab
        {
            Play,
            Cases,
            Settings,
            Credits
        }

        private MenuTab _currentTab = MenuTab.Play;
        private RectTransform _sidebar;
        private RectTransform _mainOverviewPanel;
        private RectTransform _contentHost;

        private Text _overviewCaseTitleText;
        private Text _overviewCaseTargetText;
        private Text _overviewAgentIdText;
        private Text _overviewStatusText;

        private RectTransform _tabPlayRoot;
        private RectTransform _tabCasesRoot;
        private RectTransform _tabSettingsRoot;
        private RectTransform _tabCreditsRoot;

        private Button _tabPlayBtn;
        private Button _tabCasesBtn;
        private Button _tabSettingsBtn;
        private Button _tabCreditsBtn;

        private Text _settingsVolumeText;
        private Text _settingsSensitivityText;
        private Text _settingsQualityText;

        private Button _qualityLowBtn;
        private Button _qualityMedBtn;
        private Button _qualityHighBtn;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            RefreshImmediate();
        }

        private void Update()
        {
            ResolveLogic();
            BuildIfNeeded();
            SyncVisibility();
            ApplyResponsiveLayout();
            AnimateMenu(logic != null && logic.IsOpen);

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                RefreshImmediate();
                _nextRefreshAt = Time.unscaledTime + 0.15f;
            }
        }

        private void ResolveLogic()
        {
            if (logic != null)
            {
                return;
            }

            logic = GetComponent<MainMenuHud>();
            if (logic == null)
            {
                logic = MainMenuHud.Instance;
            }

            if (logic != null)
            {
                logic.RenderWithOnGui = false;
            }
        }

        private void BuildIfNeeded()
        {
            if (_built)
            {
                return;
            }

            ResolveLogic();
            if (logic != null)
            {
                logic.RenderWithOnGui = false;
            }

            EnsureEventSystem();

            var canvasTransform = transform.Find("UguiMainMenuCanvas") as RectTransform;
            if (canvasTransform == null)
            {
                var canvasObj = new GameObject("UguiMainMenuCanvas");
                canvasObj.transform.SetParent(transform, false);
                canvasTransform = canvasObj.AddComponent<RectTransform>();
            }

            _canvas = canvasTransform.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 85;

            var scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasTransform.GetComponent<GraphicRaycaster>() == null)
            {
                canvasTransform.gameObject.AddComponent<GraphicRaycaster>();
            }

            RuntimeUiFactory.Stretch(canvasTransform);
            RuntimeUiFactory.ClearChildren(canvasTransform);

            BuildCollapsedButton(canvasTransform);
            BuildOverlay(canvasTransform);

            _built = true;
            _openBlend = logic != null && logic.IsOpen ? 1f : 0f;
            RefreshImmediate();
            SyncVisibility();
            AnimateMenu(logic != null && logic.IsOpen);
        }

        private void BuildCollapsedButton(RectTransform canvasTransform)
        {
            _collapsedRoot = RuntimeUiFactory.CreateUiRoot("CollapsedButtonRoot", canvasTransform);
            _collapsedRoot.anchorMin = new Vector2(0f, 0f);
            _collapsedRoot.anchorMax = new Vector2(0f, 0f);
            _collapsedRoot.pivot = new Vector2(0f, 0f);
            _collapsedRoot.sizeDelta = new Vector2(100f, 44f);
            _collapsedRoot.anchoredPosition = new Vector2(24f, 218f);

            var openButton = RuntimeUiFactory.CreateButton("OpenMenuButton", _collapsedRoot, "MENU", new Color(0.1f, 0.13f, 0.16f, 0.82f), 16);
            RuntimeUiFactory.Stretch(openButton.GetComponent<RectTransform>());
            openButton.onClick.AddListener(() => logic?.OpenMenu());
        }

        private void BuildOverlay(RectTransform canvasTransform)
        {
            _overlayRoot = RuntimeUiFactory.CreateUiRoot("OverlayRoot", canvasTransform);
            RuntimeUiFactory.Stretch(_overlayRoot);
            RuntimeUiFactory.AddImage(_overlayRoot.gameObject, ThemeBgColor);

            _overlayGroup = _overlayRoot.gameObject.GetComponent<CanvasGroup>();
            if (_overlayGroup == null)
            {
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            }

            var bgObj = RuntimeUiFactory.CreateUiRoot("MenuBackground", _overlayRoot);
            RuntimeUiFactory.Stretch(bgObj);
            var bgImg = RuntimeUiFactory.AddImage(bgObj.gameObject, Color.black);
            var bgSprite = Resources.Load<Sprite>("UI/main_menu_bg");
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.color = new Color(1f, 1f, 1f, 0.35f);
            }
            else
            {
                bgImg.color = ThemeBgColor;
            }

            _windowRoot = RuntimeUiFactory.CreateUiRoot("MenuWindow", _overlayRoot);
            _windowRoot.anchorMin = Vector2.zero;
            _windowRoot.anchorMax = Vector2.one;
            _windowRoot.offsetMin = new Vector2(30f, 30f);
            _windowRoot.offsetMax = new Vector2(-30f, -30f);

            // --- COLUMN 1: SIDEBAR (Left) ---
            _sidebar = RuntimeUiFactory.CreateUiRoot("Sidebar", _windowRoot);
            RuntimeUiFactory.AddImage(_sidebar.gameObject, ThemeSidebarColor);
            RuntimeUiFactory.ApplyOneUiRounding(_sidebar.gameObject, 16f);
            RuntimeUiFactory.AddOutline(_sidebar.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            AnchorSidebarAndContent(280f, 16f, true);
            var sbVl = RuntimeUiFactory.AddVerticalLayout(_sidebar, 10f, new RectOffset(14, 14, 16, 14));
            sbVl.childForceExpandHeight = false;

            BuildSidebarHeader(_sidebar);
            CreateSleekDivider("SidebarDivider1", _sidebar, 1f, -1f, ThemeBorderColor);
            BuildSidebarProfile(_sidebar);
            CreateSleekDivider("SidebarDivider2", _sidebar, 1f, -1f, ThemeBorderColor);
            BuildSidebarNav(_sidebar);

            // --- COLUMN 2: CONTENT PANEL (Right) ---
            _contentHost = CreateFlatPanel("ContentHostPanel", _windowRoot, flexWidth: 1f, bgColor: ThemePanelColor);
            AnchorSidebarAndContent(280f, 16f, true);
            RuntimeUiFactory.AddOutline(_contentHost.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            var chLayout = RuntimeUiFactory.AddVerticalLayout(_contentHost, 0f, new RectOffset(20, 20, 18, 18));
            chLayout.childForceExpandHeight = true;

            BuildOpeningView(_contentHost);
            BuildLobbyView(_contentHost);
            BuildPauseView(_contentHost);
        }

        private void BuildSidebarProfile(Transform parent)
        {
            var profileCard = CreateFlatPanel("ProfileCard", parent, preferredHeight: 140f, bgColor: ThemeCardBgColor);
            RuntimeUiFactory.AddOutline(profileCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            var pVl = RuntimeUiFactory.AddVerticalLayout(profileCard, 6f, new RectOffset(14, 14, 12, 12));
            pVl.childForceExpandHeight = false;

            CreateSleekText("AgentLabel", profileCard, "Ajan Kimliği", 12, ThemeMutedColor, FontStyle.Bold, TextAnchor.MiddleLeft);

            RuntimeUiFactory.CreateSpacer("ProfileSpacer1", profileCard, 2f);

            var nameRow = RuntimeUiFactory.CreateUiRoot("NameRow", profileCard);
            RuntimeUiFactory.EnsureLayoutElement(nameRow, preferredHeight: 38f);
            var nrHl = RuntimeUiFactory.AddHorizontalLayout(nameRow, 8f, new RectOffset(0, 0, 0, 0), true);
            nrHl.childForceExpandWidth = false;

            _playerNameField = RuntimeUiFactory.CreateInputField("PlayerNameField", nameRow, "Ajan adı", 14);
            RuntimeUiFactory.EnsureLayoutElement(_playerNameField.transform, flexibleWidth: 1f, preferredHeight: 36f);
            var nameFieldImg = _playerNameField.GetComponent<Image>();
            if (nameFieldImg != null) nameFieldImg.color = new Color(0f, 0f, 0f, 0.5f);
            var nameFieldOutline = _playerNameField.GetComponent<Outline>();
            if (nameFieldOutline != null) nameFieldOutline.effectColor = ThemeBorderColor;
            _playerNameField.onValueChanged.AddListener(OnPlayerNameChanged);

            RuntimeUiFactory.CreateSpacer("ProfileSpacer2", profileCard, 4f);

            _overviewAgentIdText = CreateSleekText("AgentId", profileCard, "Ajan Kodu: OFL-", 11, ThemeMutedColor, FontStyle.Normal, TextAnchor.MiddleLeft);
        }

        private void BuildSidebarHeader(Transform parent)
        {
            var header = RuntimeUiFactory.CreateUiRoot("SidebarHeader", parent);
            RuntimeUiFactory.EnsureLayoutElement(header, preferredHeight: 82f);
            var vl = RuntimeUiFactory.AddVerticalLayout(header, 3f, new RectOffset(0, 0, 0, 0));
            vl.childForceExpandHeight = false;

            _titleText = CreateSleekText("Title", header, "MOBİL OFL", 26, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _subtitleText = CreateSleekText("Subtitle", header, "Okul soruşturma arayüzü", 11, ThemeMutedColor, FontStyle.Normal, TextAnchor.UpperLeft);

            var badge = CreateFlatPanel("SecurityBadge", header, preferredHeight: 24f, bgColor: new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.08f));
            RuntimeUiFactory.AddOutline(badge.gameObject, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.3f), new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(badge, 0f, new RectOffset(8, 8, 3, 3));
            _modeBadgeText = CreateSleekText("SecurityBadgeText", badge, "Sistem hazır", 10, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
        }

        private void BuildSidebarNav(Transform parent)
        {
            var nav = RuntimeUiFactory.CreateUiRoot("SidebarNav", parent);
            RuntimeUiFactory.EnsureLayoutElement(nav, flexibleHeight: 1f);
            var vl = RuntimeUiFactory.AddVerticalLayout(nav, 6f, new RectOffset(0, 0, 0, 0));
            vl.childForceExpandHeight = false;

            _tabPlayBtn = CreateMainActionButton("NavPlayBtn", nav, "Operasyon Merkezi", () => SwitchTab(MenuTab.Play));
            _tabCasesBtn = CreateMainActionButton("NavCasesBtn", nav, "Vaka Dosyaları", () => SwitchTab(MenuTab.Cases));
            _tabSettingsBtn = CreateMainActionButton("NavSettingsBtn", nav, "Sistem Ayarları", () => SwitchTab(MenuTab.Settings));
            _tabCreditsBtn = CreateMainActionButton("NavCreditsBtn", nav, "Proje Künyesi", () => SwitchTab(MenuTab.Credits));

            var spacer = RuntimeUiFactory.CreateSpacer("NavSpacer", nav, 0f);
            RuntimeUiFactory.EnsureLayoutElement(spacer.transform, preferredHeight: 0f, flexibleHeight: 1f);

            CreateSleekText("VersionLabel", nav, "Ajan Kodu: OFL Dedektif 0.2.0", 9, ThemeMutedColor, FontStyle.Normal, TextAnchor.MiddleCenter);
            RuntimeUiFactory.CreateSpacer("NavSpacer2", nav, 4f);

            var exitBtn = CreateMainActionButton("NavExitBtn", nav, "Sistemi Kapat", () => {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void SwitchTab(MenuTab targetTab)
        {
            _currentTab = targetTab;
            UpdateTabButtonsHighlight();

            if (_tabPlayRoot != null) _tabPlayRoot.gameObject.SetActive(_currentTab == MenuTab.Play && logic != null && logic.CurrentMenuMode == MainMenuHud.RuntimeMenuMode.Opening);
            if (_tabCasesRoot != null) _tabCasesRoot.gameObject.SetActive(_currentTab == MenuTab.Cases && logic != null && logic.CurrentMenuMode == MainMenuHud.RuntimeMenuMode.Opening);
            if (_tabSettingsRoot != null) _tabSettingsRoot.gameObject.SetActive(_currentTab == MenuTab.Settings && logic != null && logic.CurrentMenuMode == MainMenuHud.RuntimeMenuMode.Opening);
            if (_tabCreditsRoot != null) _tabCreditsRoot.gameObject.SetActive(_currentTab == MenuTab.Credits && logic != null && logic.CurrentMenuMode == MainMenuHud.RuntimeMenuMode.Opening);
        }

        private void UpdateTabButtonsHighlight()
        {
            SetButtonState(_tabPlayBtn, _currentTab == MenuTab.Play);
            SetButtonState(_tabCasesBtn, _currentTab == MenuTab.Cases);
            SetButtonState(_tabSettingsBtn, _currentTab == MenuTab.Settings);
            SetButtonState(_tabCreditsBtn, _currentTab == MenuTab.Credits);
        }

        private void SetButtonState(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            var txt = btn.GetComponentInChildren<Text>();
            var outline = btn.GetComponent<Outline>();

            if (img != null) img.color = active ? new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.15f) : new Color(1f, 1f, 1f, 0.03f);
            if (txt != null)
            {
                txt.color = active ? ThemeAccentColor : ThemeTextColor;
                txt.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }
            if (outline != null) outline.effectColor = active ? ThemeAccentColor : ThemeBorderColor;
        }

        private Button CreateMainActionButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var btn = RuntimeUiFactory.CreateButton(name, parent, text, new Color(1f, 1f, 1f, 0.03f), 14);
            RuntimeUiFactory.EnsureLayoutElement(btn.transform, preferredHeight: 54f);

            var rounded = btn.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (rounded != null) rounded.radius = 12f;
            else RuntimeUiFactory.ApplyOneUiRounding(btn.gameObject, 12f);

            var outline = btn.GetComponent<Outline>();
            if (outline != null) outline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(btn.gameObject, ThemeBorderColor, new Vector2(1f, 1f));

            var btnText = btn.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.color = ThemeTextColor;
                btnText.alignment = TextAnchor.MiddleCenter;
            }

            btn.onClick.AddListener(onClick);
            AddHoverEffects(btn);
            return btn;
        }

        private void AddHoverEffects(Button btn)
        {
            var trigger = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => {
                if (IsButtonActive(btn)) return;
                var img = btn.GetComponent<Image>();
                var txt = btn.GetComponentInChildren<Text>();
                var outline = btn.GetComponent<Outline>();
                var accent = btn.name == "NavExitBtn" ? ThemeDangerColor : ThemeAccentColor;
                if (img != null) img.color = new Color(accent.r, accent.g, accent.b, 0.12f);
                if (txt != null) txt.color = accent;
                if (outline != null) outline.effectColor = accent;
            });
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener((data) => {
                if (IsButtonActive(btn)) return;
                var img = btn.GetComponent<Image>();
                var txt = btn.GetComponentInChildren<Text>();
                var outline = btn.GetComponent<Outline>();
                if (img != null) img.color = new Color(1f, 1f, 1f, 0.03f);
                if (txt != null) txt.color = ThemeTextColor;
                if (outline != null) outline.effectColor = ThemeBorderColor;
            });
            trigger.triggers.Add(exit);
        }

        private bool IsButtonActive(Button btn)
        {
            if (btn == _tabPlayBtn && _currentTab == MenuTab.Play) return true;
            if (btn == _tabCasesBtn && _currentTab == MenuTab.Cases) return true;
            if (btn == _tabSettingsBtn && _currentTab == MenuTab.Settings) return true;
            if (btn == _tabCreditsBtn && _currentTab == MenuTab.Credits) return true;
            return false;
        }

        private Text CreateSleekText(string name, Transform parent, string text, int fontSize, Color color, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            return RuntimeUiFactory.CreateText(name, parent, text, fontSize, color, style, alignment);
        }

        private void CreateSleekDivider(string name, Transform parent, float height, float width, Color color)
        {
            var div = RuntimeUiFactory.CreateUiRoot(name, parent);
            if (width > 0f) RuntimeUiFactory.EnsureLayoutElement(div, preferredWidth: width, preferredHeight: height);
            else RuntimeUiFactory.EnsureLayoutElement(div, preferredHeight: height);
            RuntimeUiFactory.AddImage(div.gameObject, color);
        }

        private RectTransform CreateFlatPanel(string name, Transform parent, float preferredHeight = -1f, float flexWidth = -1f, float flexHeight = -1f, Color? bgColor = null)
        {
            var panel = RuntimeUiFactory.CreateUiRoot(name, parent);
            var bgCol = bgColor ?? ThemePanelColor;
            RuntimeUiFactory.AddImage(panel.gameObject, bgCol);
            RuntimeUiFactory.ApplyOneUiRounding(panel.gameObject, 14f);

            var layout = panel.GetComponent<LayoutElement>();
            if (layout == null) layout = panel.gameObject.AddComponent<LayoutElement>();

            if (preferredHeight > 0f) layout.preferredHeight = preferredHeight;
            if (flexWidth > 0f) layout.flexibleWidth = flexWidth;
            if (flexHeight > 0f) layout.flexibleHeight = flexHeight;

            return panel;
        }

        private void AnchorSidebarAndContent(float sidebarWidth, float gap, bool showSidebar)
        {
            if (_sidebar != null)
            {
                _sidebar.anchorMin = new Vector2(0f, 0f);
                _sidebar.anchorMax = new Vector2(0f, 1f);
                _sidebar.pivot = new Vector2(0f, 0.5f);
                _sidebar.anchoredPosition = Vector2.zero;
                _sidebar.sizeDelta = new Vector2(sidebarWidth, 0f);
                _sidebar.offsetMin = new Vector2(0f, 0f);
                _sidebar.offsetMax = new Vector2(sidebarWidth, 0f);
            }

            if (_contentHost != null)
            {
                _contentHost.anchorMin = new Vector2(0f, 0f);
                _contentHost.anchorMax = new Vector2(1f, 1f);
                _contentHost.pivot = new Vector2(0.5f, 0.5f);
                _contentHost.offsetMin = new Vector2(showSidebar ? sidebarWidth + gap : 0f, 0f);
                _contentHost.offsetMax = Vector2.zero;
            }
        }

        private void BuildOpeningView(Transform parent)
        {
            _openingRoot = CreateModeRoot("OpeningRoot", parent);

            _roleButton = RuntimeUiFactory.CreateButton("ProfileRoleBtn", _openingRoot, "", Color.clear, 1);
            _roleButton.gameObject.SetActive(false);
            _roleButtonText = _roleButton.GetComponentInChildren<Text>();

            // TAB 1: OPERASYON GİRİŞİ (PLAY TAB)
            _tabPlayRoot = RuntimeUiFactory.CreateUiRoot("TabPlayRoot", _openingRoot);
            RuntimeUiFactory.Stretch(_tabPlayRoot);
            
            var playVl = RuntimeUiFactory.AddVerticalLayout(_tabPlayRoot, 12f, new RectOffset(0, 0, 0, 0));
            playVl.childForceExpandHeight = false;

            CreateSleekText("PlayTabTitle", _tabPlayRoot, "Operasyon Merkezi", 28, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            CreateSleekDivider("PlayDivider", _tabPlayRoot, 2f, -1f, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.3f));

            var columnsContainer = RuntimeUiFactory.CreateUiRoot("PlayColumns", _tabPlayRoot);
            RuntimeUiFactory.EnsureLayoutElement(columnsContainer, flexibleHeight: 1f);
            var colsHl = RuntimeUiFactory.AddHorizontalLayout(columnsContainer, 14f, new RectOffset(0, 0, 8, 0), true);
            colsHl.childForceExpandWidth = false;

            // --- LEFT COLUMN: TELEMETRY & CASE STATUS ---
            var leftCol = RuntimeUiFactory.CreateUiRoot("LeftPlayColumn", columnsContainer);
            RuntimeUiFactory.EnsureLayoutElement(leftCol, flexibleWidth: 1f, flexibleHeight: 1f);
            var leftVl = RuntimeUiFactory.AddVerticalLayout(leftCol, 12f, new RectOffset(0, 0, 0, 0));
            leftVl.childForceExpandHeight = false;

            var caseCard = CreateFlatPanel("CaseCard", leftCol, preferredHeight: 160f, flexHeight: 1f, bgColor: ThemeCardBgColor);
            RuntimeUiFactory.AddOutline(caseCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(caseCard, 10f, new RectOffset(16, 16, 16, 16));
            CreateSleekText("ActiveCaseLabel", caseCard, "Aktif Soruşturma Dosyası", 13, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.CreateSpacer("CaseSpacer", caseCard, 4f);
            _overviewCaseTitleText = CreateSleekText("CaseTitle", caseCard, "Yükleniyor...", 18, ThemeTextColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            _overviewCaseTargetText = CreateSleekText("CaseTarget", caseCard, "Lütfen bir operasyon modu seçin.", 14, ThemeMutedColor, FontStyle.Normal, TextAnchor.MiddleLeft);

            var statusCard = CreateFlatPanel("StatusCard", leftCol, preferredHeight: 150f, flexHeight: 1f, bgColor: ThemeCardBgColor);
            RuntimeUiFactory.AddOutline(statusCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(statusCard, 10f, new RectOffset(16, 16, 16, 16));
            CreateSleekText("TelemetryLabel", statusCard, "Bağlantı Durumu", 13, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.CreateSpacer("StatusSpacer", statusCard, 4f);
            _overviewStatusText = CreateSleekText("OverviewStatus", statusCard, "Hazır", 14, ThemeTextColor, FontStyle.Normal, TextAnchor.UpperLeft);

            // --- RIGHT COLUMN: OPERATION CONTROLS ---
            var rightCol = RuntimeUiFactory.CreateUiRoot("RightPlayColumn", columnsContainer);
            RuntimeUiFactory.EnsureLayoutElement(rightCol, preferredWidth: 380f, flexibleHeight: 1f);
            var rightVl = RuntimeUiFactory.AddVerticalLayout(rightCol, 10f, new RectOffset(0, 0, 0, 0));
            rightVl.childForceExpandHeight = false;

            var soloSection = RuntimeUiFactory.CreateUiRoot("SoloSection", rightCol);
            var soloVl = RuntimeUiFactory.AddVerticalLayout(soloSection, 8f, new RectOffset(0, 0, 0, 0));
            soloVl.childForceExpandHeight = false;

            CreateSleekText("SoloTitleLabel", soloSection, "Solo Operasyon", 13, ThemeAccentColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _soloButton = RuntimeUiFactory.CreateButton("SoloButton", soloSection, "Operasyonu Başlat", ThemeAccentColor, 17);
            RuntimeUiFactory.EnsureLayoutElement(_soloButton.transform, preferredHeight: 64f);
            var soloBtnText = _soloButton.GetComponentInChildren<Text>();
            soloBtnText.color = Color.black;
            soloBtnText.fontStyle = FontStyle.Bold;
            var sRounded = _soloButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (sRounded != null) sRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_soloButton.gameObject, 10f);
            var sOutline = _soloButton.GetComponent<Outline>();
            if (sOutline != null) sOutline.effectColor = new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.4f);
            else RuntimeUiFactory.AddOutline(_soloButton.gameObject, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.4f), new Vector2(1f, 1f));
            _soloButton.onClick.AddListener(() => logic?.StartSoloFromUi());

            CreateSleekText("CoopTitle", rightCol, "Ekip Oyunu", 13, ThemeAccentColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var joinCard = CreateFlatPanel("JoinCard", rightCol, preferredHeight: 130f, bgColor: ThemeCardBgColor);
            RuntimeUiFactory.AddOutline(joinCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            var jcVl = RuntimeUiFactory.AddVerticalLayout(joinCard, 8f, new RectOffset(16, 16, 14, 14));
            jcVl.childForceExpandHeight = false;

            CreateSleekText("JoinDesc", joinCard, "6 haneli lobi kodu", 13, ThemeTextColor, FontStyle.Normal, TextAnchor.UpperLeft);

            var joinRow = RuntimeUiFactory.CreateUiRoot("JoinRow", joinCard);
            RuntimeUiFactory.EnsureLayoutElement(joinRow, preferredHeight: 48f);
            var jr = RuntimeUiFactory.AddHorizontalLayout(joinRow, 10f, new RectOffset(0, 0, 0, 0), false);
            jr.childForceExpandWidth = false;

            _openingJoinField = RuntimeUiFactory.CreateInputField("OpeningJoinCode", joinRow, "LOBİ KODU", 14);
            RuntimeUiFactory.EnsureLayoutElement(_openingJoinField.transform, flexibleWidth: 1f, preferredHeight: 46f);
            var joinImg = _openingJoinField.GetComponent<Image>();
            if (joinImg != null) joinImg.color = new Color(0f, 0f, 0f, 0.4f);
            var joinOutline = _openingJoinField.GetComponent<Outline>();
            if (joinOutline != null) joinOutline.effectColor = ThemeBorderColor;
            _openingJoinField.onValueChanged.AddListener(OnJoinCodeChanged);

            _openingJoinButton = RuntimeUiFactory.CreateButton("JoinCodeBtn", joinRow, "Katıl", ThemeAccentColor, 14);
            RuntimeUiFactory.EnsureLayoutElement(_openingJoinButton.transform, preferredWidth: 110f, preferredHeight: 46f);
            var joinBtnText = _openingJoinButton.GetComponentInChildren<Text>();
            joinBtnText.color = Color.black;
            joinBtnText.fontStyle = FontStyle.Bold;
            var jRounded = _openingJoinButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (jRounded != null) jRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_openingJoinButton.gameObject, 10f);
            var jOutline = _openingJoinButton.GetComponent<Outline>();
            if (jOutline != null) jOutline.effectColor = new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.4f);
            else RuntimeUiFactory.AddOutline(_openingJoinButton.gameObject, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.4f), new Vector2(1f, 1f));
            _openingJoinButton.onClick.AddListener(() => logic?.JoinCurrentCodeFromUi());

            CreateSleekText("HostTitleLabel", rightCol, "Yeni Ekip Kur", 13, ThemeAccentColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _hostButton = RuntimeUiFactory.CreateButton("HostButton", rightCol, "Lobi Oluştur", new Color(1f, 1f, 1f, 0.04f), 15);
            RuntimeUiFactory.EnsureLayoutElement(_hostButton.transform, preferredHeight: 56f);
            var hostBtnText = _hostButton.GetComponentInChildren<Text>();
            hostBtnText.color = ThemeTextColor;
            hostBtnText.fontStyle = FontStyle.Bold;
            var hRounded = _hostButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (hRounded != null) hRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_hostButton.gameObject, 10f);
            var hOutline = _hostButton.GetComponent<Outline>();
            if (hOutline != null) hOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(_hostButton.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            _hostButton.onClick.AddListener(() => logic?.StartHostFromUi());

            _reconnectButton = RuntimeUiFactory.CreateButton("ReconnectButton", rightCol, "Önceki Oturuma Dön", new Color(1f, 1f, 1f, 0.04f), 14);
            RuntimeUiFactory.EnsureLayoutElement(_reconnectButton.transform, preferredHeight: 52f);
            var recBtnText = _reconnectButton.GetComponentInChildren<Text>();
            recBtnText.color = ThemeAccentColor;
            recBtnText.fontStyle = FontStyle.Bold;
            var recRounded = _reconnectButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (recRounded != null) recRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_reconnectButton.gameObject, 10f);
            var recOutline = _reconnectButton.GetComponent<Outline>();
            if (recOutline != null) recOutline.effectColor = ThemeAccentColor;
            else RuntimeUiFactory.AddOutline(_reconnectButton.gameObject, ThemeAccentColor, new Vector2(1f, 1f));
            _reconnectButton.onClick.AddListener(() => logic?.ReconnectFromUi());

            // TAB 2: VAKA GÖRÜNÜMÜ (CASES TAB)
            _tabCasesRoot = RuntimeUiFactory.CreateUiRoot("TabCasesRoot", _openingRoot);
            RuntimeUiFactory.Stretch(_tabCasesRoot);
            var casesVl = RuntimeUiFactory.AddVerticalLayout(_tabCasesRoot, 14f, new RectOffset(0, 0, 10, 10));
            casesVl.childForceExpandHeight = false;

            CreateSleekText("DossierTitle", _tabCasesRoot, "Aktif Soruşturma Dosyası Raporu", 28, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            CreateSleekDivider("DossierDivider", _tabCasesRoot, 2f, -1f, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.3f));

            var previewFrame = CreateFlatPanel("PreviewFrame", _tabCasesRoot, flexHeight: 1f, bgColor: ThemeCardBgColor);
            RuntimeUiFactory.AddOutline(previewFrame.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(previewFrame, 12f, new RectOffset(18, 18, 18, 18));

            _casePreviewText = CreateSleekText("CasePreview", previewFrame, string.Empty, 15, ThemeTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            _casePreviewText.supportRichText = true;
            RuntimeUiFactory.EnsureLayoutElement(_casePreviewText.transform, flexibleHeight: 1f);

            // TAB 3: AYARLAR GÖRÜNÜMÜ (SETTINGS TAB)
            _tabSettingsRoot = RuntimeUiFactory.CreateUiRoot("TabSettingsRoot", _openingRoot);
            RuntimeUiFactory.Stretch(_tabSettingsRoot);
            var settingsVl = RuntimeUiFactory.AddVerticalLayout(_tabSettingsRoot, 12f, new RectOffset(0, 0, 8, 0));
            settingsVl.childForceExpandHeight = false;

            CreateSleekText("SettingsTitle", _tabSettingsRoot, "Sistem Ayarları ve Hassasiyet", 28, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            CreateSleekDivider("SettingsDivider", _tabSettingsRoot, 2f, -1f, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.3f));

            // --- SES AYARLARI SECTION CARD ---
            var soundCard = CreateFlatPanel("SoundCard", _tabSettingsRoot, preferredHeight: -1f, bgColor: ThemeSectionCardColor);
            RuntimeUiFactory.AddOutline(soundCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            var soundLayout = RuntimeUiFactory.AddVerticalLayout(soundCard, 10f, new RectOffset(18, 18, 14, 14));
            soundLayout.childForceExpandHeight = false;

            CreateSleekText("SecSoundTitle", soundCard, "Ses Ayarları", 14, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);

            var volWrapper = RuntimeUiFactory.CreateUiRoot("VolWrapper", soundCard);
            RuntimeUiFactory.EnsureLayoutElement(volWrapper, preferredHeight: 52f);
            var volRow = RuntimeUiFactory.AddHorizontalLayout(volWrapper, 16f, new RectOffset(0, 0, 0, 0), false);
            volRow.childForceExpandWidth = false;

            _settingsVolumeText = CreateSleekText("SettingsVolumeText", volWrapper, "Ses: 80%", 14, ThemeTextColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.EnsureLayoutElement(_settingsVolumeText.transform, flexibleWidth: 1f);

            var volBtns = RuntimeUiFactory.CreateUiRoot("VolBtns", volWrapper);
            RuntimeUiFactory.EnsureLayoutElement(volBtns, preferredWidth: 200f, preferredHeight: 48f);
            var vBr = RuntimeUiFactory.AddHorizontalLayout(volBtns, 8f, new RectOffset(0, 0, 0, 0), false);
            vBr.childForceExpandWidth = true;

            var volMinus = RuntimeUiFactory.CreateButton("VolMinus", volBtns, "Kıs (-)", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(volMinus.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var vmRounded = volMinus.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (vmRounded != null) vmRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(volMinus.gameObject, 10f);
            var vmOutline = volMinus.GetComponent<Outline>();
            if (vmOutline != null) vmOutline.effectColor = ThemeBorderColor;
            volMinus.onClick.AddListener(() => AdjustVolume(-0.1f));

            var volPlus = RuntimeUiFactory.CreateButton("VolPlus", volBtns, "Aç (+)", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(volPlus.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var vpRounded = volPlus.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (vpRounded != null) vpRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(volPlus.gameObject, 10f);
            var vpOutline = volPlus.GetComponent<Outline>();
            if (vpOutline != null) vpOutline.effectColor = ThemeBorderColor;
            volPlus.onClick.AddListener(() => AdjustVolume(0.1f));

            // --- KONTROL HASSASİYETİ SECTION CARD ---
            var controlCard = CreateFlatPanel("ControlCard", _tabSettingsRoot, preferredHeight: -1f, bgColor: ThemeSectionCardColor);
            RuntimeUiFactory.AddOutline(controlCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            var controlLayout = RuntimeUiFactory.AddVerticalLayout(controlCard, 10f, new RectOffset(18, 18, 14, 14));
            controlLayout.childForceExpandHeight = false;

            CreateSleekText("SecControlsTitle", controlCard, "Kontrol Hassasiyeti", 14, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);

            var sensWrapper = RuntimeUiFactory.CreateUiRoot("SensWrapper", controlCard);
            RuntimeUiFactory.EnsureLayoutElement(sensWrapper, preferredHeight: 52f);
            var sensRow = RuntimeUiFactory.AddHorizontalLayout(sensWrapper, 16f, new RectOffset(0, 0, 0, 0), false);
            sensRow.childForceExpandWidth = false;

            _settingsSensitivityText = CreateSleekText("SettingsSensText", sensWrapper, "Bakış hassasiyeti: 2.0", 14, ThemeTextColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.EnsureLayoutElement(_settingsSensitivityText.transform, flexibleWidth: 1f);

            var sensBtns = RuntimeUiFactory.CreateUiRoot("SensBtns", sensWrapper);
            RuntimeUiFactory.EnsureLayoutElement(sensBtns, preferredWidth: 200f, preferredHeight: 48f);
            var sBr = RuntimeUiFactory.AddHorizontalLayout(sensBtns, 8f, new RectOffset(0, 0, 0, 0), false);
            sBr.childForceExpandWidth = true;

            var sensMinus = RuntimeUiFactory.CreateButton("SensMinus", sensBtns, "Düşür (-)", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(sensMinus.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var smRounded = sensMinus.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (smRounded != null) smRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(sensMinus.gameObject, 10f);
            var smOutline = sensMinus.GetComponent<Outline>();
            if (smOutline != null) smOutline.effectColor = ThemeBorderColor;
            sensMinus.onClick.AddListener(() => AdjustLookSensitivity(-0.2f));

            var sensPlus = RuntimeUiFactory.CreateButton("SensPlus", sensBtns, "Artır (+)", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(sensPlus.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var spRounded = sensPlus.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (spRounded != null) spRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(sensPlus.gameObject, 10f);
            var spOutline = sensPlus.GetComponent<Outline>();
            if (spOutline != null) spOutline.effectColor = ThemeBorderColor;
            sensPlus.onClick.AddListener(() => AdjustLookSensitivity(0.2f));

            // --- GRAFİK KALİTESİ SECTION CARD ---
            var graphicsCard = CreateFlatPanel("GraphicsCard", _tabSettingsRoot, preferredHeight: -1f, bgColor: ThemeSectionCardColor);
            RuntimeUiFactory.AddOutline(graphicsCard.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            var graphicsLayout = RuntimeUiFactory.AddVerticalLayout(graphicsCard, 10f, new RectOffset(18, 18, 14, 14));
            graphicsLayout.childForceExpandHeight = false;

            CreateSleekText("SecGraphicsTitle", graphicsCard, "Grafik Kalitesi", 14, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);

            var qualWrapper = RuntimeUiFactory.CreateUiRoot("QualWrapper", graphicsCard);
            RuntimeUiFactory.EnsureLayoutElement(qualWrapper, preferredHeight: 52f);
            var qualRow = RuntimeUiFactory.AddHorizontalLayout(qualWrapper, 16f, new RectOffset(0, 0, 0, 0), false);
            qualRow.childForceExpandWidth = false;

            _settingsQualityText = CreateSleekText("SettingsQualityText", qualWrapper, "Grafik Kalitesi: Orta", 14, ThemeTextColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.EnsureLayoutElement(_settingsQualityText.transform, flexibleWidth: 1f);

            var qualBtns = RuntimeUiFactory.CreateUiRoot("QualBtns", qualWrapper);
            RuntimeUiFactory.EnsureLayoutElement(qualBtns, preferredWidth: 320f, preferredHeight: 48f);
            var qBr = RuntimeUiFactory.AddHorizontalLayout(qualBtns, 8f, new RectOffset(0, 0, 0, 0), false);
            qBr.childForceExpandWidth = true;

            _qualityLowBtn = RuntimeUiFactory.CreateButton("QualLow", qualBtns, "Düşük", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(_qualityLowBtn.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var qlRounded = _qualityLowBtn.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (qlRounded != null) qlRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_qualityLowBtn.gameObject, 10f);
            var qlOutline = _qualityLowBtn.GetComponent<Outline>();
            if (qlOutline != null) qlOutline.effectColor = ThemeBorderColor;
            _qualityLowBtn.onClick.AddListener(() => SetQuality(0));

            _qualityMedBtn = RuntimeUiFactory.CreateButton("QualMed", qualBtns, "Orta", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(_qualityMedBtn.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var qmRounded = _qualityMedBtn.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (qmRounded != null) qmRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_qualityMedBtn.gameObject, 10f);
            var qmOutline = _qualityMedBtn.GetComponent<Outline>();
            if (qmOutline != null) qmOutline.effectColor = ThemeBorderColor;
            _qualityMedBtn.onClick.AddListener(() => SetQuality(1));

            _qualityHighBtn = RuntimeUiFactory.CreateButton("QualHigh", qualBtns, "Yüksek", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(_qualityHighBtn.transform, flexibleWidth: 1f, preferredHeight: 48f);
            var qhRounded = _qualityHighBtn.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (qhRounded != null) qhRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_qualityHighBtn.gameObject, 10f);
            var qhOutline = _qualityHighBtn.GetComponent<Outline>();
            if (qhOutline != null) qhOutline.effectColor = ThemeBorderColor;
            _qualityHighBtn.onClick.AddListener(() => SetQuality(2));

            // TAB 4: KÜNYE GÖRÜNÜMÜ (CREDITS TAB)
            _tabCreditsRoot = RuntimeUiFactory.CreateUiRoot("TabCreditsRoot", _openingRoot);
            RuntimeUiFactory.Stretch(_tabCreditsRoot);
            var creditsVl = RuntimeUiFactory.AddVerticalLayout(_tabCreditsRoot, 14f, new RectOffset(0, 0, 8, 8));
            creditsVl.childForceExpandHeight = false;

            CreateSleekText("CreditsTitle", _tabCreditsRoot, "Proje Künyesi ve Geliştirici Ekip", 28, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            CreateSleekDivider("CreditsDivider", _tabCreditsRoot, 2f, -1f, new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.3f));

            var creditsFrame = CreateFlatPanel("CreditsFrame", _tabCreditsRoot, flexHeight: 1f, bgColor: ThemeCardBgColor);
            RuntimeUiFactory.AddOutline(creditsFrame.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(creditsFrame, 12f, new RectOffset(20, 20, 20, 20));

            var creditsInfoText = "<color=#D7FF00>Mobil OFL</color> - Çok Oyunculu Kooperatif Dedektiflik Soruşturması\n\n" +
                "Bu yazılım prototipi <b>Of Fen Lisesi</b> öğrencileri tarafından geliştirilmektedir.\n\n" +
                "Proje Geliştirme Ekibi:\n" +
                "  - <color=#D7FF00>Hüseyin Yeşilyurt</color> (Tasarım & Oynanış Mantığı)\n" +
                "  - <color=#D7FF00>Ali Küçük</color> (Ağ Altyapısı & Sistem Mekanikleri)\n\n" +
                "Altyapı Bileşenleri:\n" +
                "  - Unity 6 Oyun Motoru\n" +
                "  - Netcode for GameObjects (Çok Oyunculu Altyapı)\n" +
                "  - Antigravity AI Geliştirici Asistanı\n\n" +
                "Okuldaki kayıp sınav dosyalarının izini sürmek ve delilleri birleştirerek şüphelileri ortaya çıkarmak için ekibinle birlikte harekete geç!";
            var creditsTextObj = CreateSleekText("CreditsInfo", creditsFrame, creditsInfoText, 14, ThemeTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            creditsTextObj.supportRichText = true;

            // Default to Play Tab
            SwitchTab(MenuTab.Play);
        }

        private void BuildLobbyView(Transform parent)
        {
            _lobbyRoot = CreateModeRoot("LobbyRoot", parent);
            var row = RuntimeUiFactory.AddHorizontalLayout(_lobbyRoot, 24f, new RectOffset(0, 0, 0, 0), true);
            row.childForceExpandWidth = true;

            var invite = CreateFlatPanel("InviteCard", _lobbyRoot, flexWidth: 1f, bgColor: ThemePanelColor);
            RuntimeUiFactory.AddOutline(invite.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(invite, 14f, new RectOffset(20, 20, 20, 20));

            CreateSleekText("InviteTitle", invite, "LOBİ ERİŞİM DAVETİ //", 22, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            CreateSleekDivider("LobbyDiv", invite, 1f, -1f, ThemeBorderColor);

            CreateSleekText("CodeLabel", invite, "EKİP DAVET KODU", 11, ThemeMutedColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var codeRow = RuntimeUiFactory.CreateUiRoot("CodeRow", invite);
            RuntimeUiFactory.EnsureLayoutElement(codeRow, preferredHeight: 52f);
            var cr = RuntimeUiFactory.AddHorizontalLayout(codeRow, 8f, new RectOffset(0, 0, 0, 0), false);
            cr.childAlignment = TextAnchor.MiddleLeft;

            _lobbyCodeText = CreateSleekText("LobbyCode", codeRow, "------", 28, ThemeAccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.EnsureLayoutElement(_lobbyCodeText.transform, preferredWidth: 160f, preferredHeight: 46f);

            var copyBtn = RuntimeUiFactory.CreateButton("CopyBtn", codeRow, "KOPYALA", new Color(1f, 1f, 1f, 0.04f), 12);
            RuntimeUiFactory.EnsureLayoutElement(copyBtn.transform, preferredWidth: 90f, preferredHeight: 38f);
            var copyBtnText = copyBtn.GetComponentInChildren<Text>();
            copyBtnText.color = ThemeTextColor;
            copyBtnText.fontStyle = FontStyle.Bold;
            var cRounded = copyBtn.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (cRounded != null) cRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(copyBtn.gameObject, 10f);
            var cOutline = copyBtn.GetComponent<Outline>();
            if (cOutline != null) cOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(copyBtn.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            copyBtn.onClick.AddListener(CopyJoinCode);

            RuntimeUiFactory.CreateSpacer("InviteSpacer", invite, 10f);

            _lobbyHintText = CreateSleekText("LobbyHint", invite, "Kodu ekip arkadaşlarınıza iletin.", 13, ThemeMutedColor, FontStyle.Normal, TextAnchor.UpperLeft);
            RuntimeUiFactory.EnsureLayoutElement(_lobbyHintText.transform, flexibleHeight: 1f);

            _readyButton = RuntimeUiFactory.CreateButton("ReadyButton", invite, "HAZIR PROTOKOLÜNÜ BAŞLAT", ThemeAccentColor, 15);
            RuntimeUiFactory.EnsureLayoutElement(_readyButton.transform, preferredHeight: 52f);
            _readyButtonText = _readyButton.GetComponentInChildren<Text>();
            _readyButtonText.color = Color.black;
            _readyButtonText.fontStyle = FontStyle.Bold;
            var rRounded = _readyButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (rRounded != null) rRounded.radius = 12f;
            else RuntimeUiFactory.ApplyOneUiRounding(_readyButton.gameObject, 12f);
            _readyButton.onClick.AddListener(ToggleReady);

            var roster = CreateFlatPanel("RosterCard", _lobbyRoot, flexWidth: 1.2f, bgColor: ThemePanelColor);
            RuntimeUiFactory.AddOutline(roster.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(roster, 12f, new RectOffset(20, 20, 20, 20));
            CreateSleekText("RosterTitle", roster, "KAYITLI AJAN HAREKET RAPORU", 22, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var scrollView = RuntimeUiFactory.CreateScrollView("RosterScroll", roster, out _rosterContent);
            RuntimeUiFactory.EnsureLayoutElement(scrollView.transform, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(_rosterContent, 8f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_rosterContent, ContentSizeFitter.FitMode.PreferredSize);
        }

        private void BuildPauseView(Transform parent)
        {
            _pauseRoot = CreateModeRoot("PauseRoot", parent);
            var row = RuntimeUiFactory.AddHorizontalLayout(_pauseRoot, 18f, new RectOffset(0, 0, 0, 0), true);
            row.childForceExpandWidth = true;

            var actions = CreateFlatPanel("PauseActions", _pauseRoot, flexWidth: 1f, bgColor: ThemePanelColor);
            RuntimeUiFactory.AddOutline(actions.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(actions, 12f, new RectOffset(20, 20, 20, 20));

            CreateSleekText("PauseTitle", actions, "SORUŞTURMA ASKIDA //", 26, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            _closeButton = RuntimeUiFactory.CreateButton("ResumeButton", actions, "SAHAYA GERİ DÖN", ThemeAccentColor, 15);
            RuntimeUiFactory.EnsureLayoutElement(_closeButton.transform, preferredHeight: 52f);
            var closeText = _closeButton.GetComponentInChildren<Text>();
            closeText.color = Color.black;
            closeText.fontStyle = FontStyle.Bold;
            var clRounded = _closeButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (clRounded != null) clRounded.radius = 12f;
            else RuntimeUiFactory.ApplyOneUiRounding(_closeButton.gameObject, 12f);
            _closeButton.onClick.AddListener(() => logic?.CloseMenu());

            var newSoloButton = RuntimeUiFactory.CreateButton("NewSoloButton", actions, "YENİ YEREL DOSYA BAŞLAT", new Color(1f, 1f, 1f, 0.04f), 14);
            RuntimeUiFactory.EnsureLayoutElement(newSoloButton.transform, preferredHeight: 46f);
            var nsText = newSoloButton.GetComponentInChildren<Text>();
            nsText.color = ThemeTextColor;
            nsText.fontStyle = FontStyle.Bold;
            var nsRounded = newSoloButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (nsRounded != null) nsRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(newSoloButton.gameObject, 10f);
            var nsOutline = newSoloButton.GetComponent<Outline>();
            if (nsOutline != null) nsOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(newSoloButton.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            newSoloButton.onClick.AddListener(() => logic?.StartSoloFromUi());

            _resetSaveButton = RuntimeUiFactory.CreateButton("ResetSaveButton", actions, "DOSYA KAYDINI SIFIRLA", new Color(ThemeDangerColor.r, ThemeDangerColor.g, ThemeDangerColor.b, 0.08f), 13);
            RuntimeUiFactory.EnsureLayoutElement(_resetSaveButton.transform, preferredHeight: 40f);
            var rsText = _resetSaveButton.GetComponentInChildren<Text>();
            rsText.color = ThemeDangerColor;
            rsText.fontStyle = FontStyle.Bold;
            var rsRounded = _resetSaveButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (rsRounded != null) rsRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(_resetSaveButton.gameObject, 10f);
            var rsOutline = _resetSaveButton.GetComponent<Outline>();
            if (rsOutline != null) rsOutline.effectColor = ThemeDangerColor;
            else RuntimeUiFactory.AddOutline(_resetSaveButton.gameObject, ThemeDangerColor, new Vector2(1f, 1f));
            _resetSaveButton.onClick.AddListener(ClearSoloSave);

            var onlineCloseButton = RuntimeUiFactory.CreateButton("OnlineCloseButton", actions, "OTURUMU KAPAT VE AYRIL", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(onlineCloseButton.transform, preferredHeight: 40f);
            var ocText = onlineCloseButton.GetComponentInChildren<Text>();
            ocText.color = ThemeTextColor;
            ocText.fontStyle = FontStyle.Bold;
            var ocRounded = onlineCloseButton.GetComponent<DevsDaddy.Shared.UIFramework.Core.RoundedMasks.ImageRoundedMask>();
            if (ocRounded != null) ocRounded.radius = 10f;
            else RuntimeUiFactory.ApplyOneUiRounding(onlineCloseButton.gameObject, 10f);
            var ocOutline = onlineCloseButton.GetComponent<Outline>();
            if (ocOutline != null) ocOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(onlineCloseButton.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            onlineCloseButton.onClick.AddListener(ShutdownSession);

            var summary = CreateFlatPanel("PauseSummary", _pauseRoot, flexWidth: 1.2f, bgColor: ThemePanelColor);
            RuntimeUiFactory.AddOutline(summary.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(summary, 14f, new RectOffset(20, 20, 20, 20));

            CreateSleekText("SummaryTitle", summary, "SORUŞTURMA RAPOR ÖZETİ //", 22, ThemeTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _pauseSummaryText = CreateSleekText("SummaryText", summary, string.Empty, 14, ThemeTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            RuntimeUiFactory.EnsureLayoutElement(_pauseSummaryText.transform, preferredHeight: 150f);

            var settings = CreateFlatPanel("PauseSettings", summary, preferredHeight: 148f, bgColor: new Color(0f, 0f, 0f, 0.25f));
            RuntimeUiFactory.AddOutline(settings.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            RuntimeUiFactory.AddVerticalLayout(settings, 10f, new RectOffset(16, 16, 15, 15));
            CreateSleekText("PauseSettingsTitle", settings, "ANLIK PARAMETRELER", 14, ThemeAccentColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _pauseSettingsText = CreateSleekText("PauseSettingsText", settings, string.Empty, 13, ThemeTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            RuntimeUiFactory.EnsureLayoutElement(_pauseSettingsText.transform, preferredHeight: 26f);
            BuildSettingsButtons(settings);
        }

        private RectTransform CreateModeRoot(string name, Transform parent)
        {
            var root = RuntimeUiFactory.CreateUiRoot(name, parent);
            RuntimeUiFactory.EnsureLayoutElement(root, flexibleWidth: 1f, flexibleHeight: 1f);
            RuntimeUiFactory.Stretch(root);
            return root;
        }

        private void BuildSettingsButtons(Transform parent)
        {
            var buttons = RuntimeUiFactory.CreateUiRoot("SettingsButtons", parent);
            RuntimeUiFactory.EnsureLayoutElement(buttons, preferredHeight: 42f);
            var row = RuntimeUiFactory.AddHorizontalLayout(buttons, 8f, new RectOffset(0, 0, 0, 0), true);
            row.childForceExpandWidth = true;

            var volumeDown = RuntimeUiFactory.CreateButton("VolumeDown", buttons, "Ses -", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(volumeDown.transform, flexibleWidth: 1f, preferredHeight: 42f);
            var vdOutline = volumeDown.GetComponent<Outline>();
            if (vdOutline != null) vdOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(volumeDown.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            volumeDown.onClick.AddListener(() => AdjustVolume(-0.1f));

            var volumeUp = RuntimeUiFactory.CreateButton("VolumeUp", buttons, "Ses +", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(volumeUp.transform, flexibleWidth: 1f, preferredHeight: 42f);
            var vuOutline = volumeUp.GetComponent<Outline>();
            if (vuOutline != null) vuOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(volumeUp.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            volumeUp.onClick.AddListener(() => AdjustVolume(0.1f));

            var lookDown = RuntimeUiFactory.CreateButton("LookDown", buttons, "Bakış -", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(lookDown.transform, flexibleWidth: 1f, preferredHeight: 42f);
            var ldOutline = lookDown.GetComponent<Outline>();
            if (ldOutline != null) ldOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(lookDown.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            lookDown.onClick.AddListener(() => AdjustLookSensitivity(-0.2f));

            var lookUp = RuntimeUiFactory.CreateButton("LookUp", buttons, "Bakış +", new Color(1f, 1f, 1f, 0.04f), 13);
            RuntimeUiFactory.EnsureLayoutElement(lookUp.transform, flexibleWidth: 1f, preferredHeight: 42f);
            var luOutline = lookUp.GetComponent<Outline>();
            if (luOutline != null) luOutline.effectColor = ThemeBorderColor;
            else RuntimeUiFactory.AddOutline(lookUp.gameObject, ThemeBorderColor, new Vector2(1f, 1f));
            lookUp.onClick.AddListener(() => AdjustLookSensitivity(0.2f));
        }

        private void RefreshImmediate()
        {
            if (logic == null)
            {
                return;
            }

            var activeMode = logic.CurrentMenuMode;

            if (_mainOverviewPanel != null)
            {
                _mainOverviewPanel.gameObject.SetActive(activeMode == MainMenuHud.RuntimeMenuMode.Opening);
            }
            if (_sidebar != null)
            {
                _sidebar.gameObject.SetActive(activeMode == MainMenuHud.RuntimeMenuMode.Opening);
            }

            if (activeMode == MainMenuHud.RuntimeMenuMode.Opening)
            {
                if (_openingRoot != null) _openingRoot.gameObject.SetActive(true);
                if (_tabPlayRoot != null) _tabPlayRoot.gameObject.SetActive(_currentTab == MenuTab.Play);
                if (_tabCasesRoot != null) _tabCasesRoot.gameObject.SetActive(_currentTab == MenuTab.Cases);
                if (_tabSettingsRoot != null) _tabSettingsRoot.gameObject.SetActive(_currentTab == MenuTab.Settings);
                if (_tabCreditsRoot != null) _tabCreditsRoot.gameObject.SetActive(_currentTab == MenuTab.Credits);
            }
            else
            {
                if (_openingRoot != null) _openingRoot.gameObject.SetActive(false);
            }

            if (_lobbyRoot != null)
            {
                _lobbyRoot.gameObject.SetActive(activeMode == MainMenuHud.RuntimeMenuMode.Lobby);
            }

            if (_pauseRoot != null)
            {
                _pauseRoot.gameObject.SetActive(activeMode == MainMenuHud.RuntimeMenuMode.Pause);
            }

            _syncingFields = true;

            var profileName = logic.PlayerName;
            if (_playerNameField != null && _playerNameField.text != profileName)
            {
                _playerNameField.text = profileName;
            }

            var joinInput = logic.JoinCodeInput;
            if (_openingJoinField != null && _openingJoinField.text != joinInput)
            {
                _openingJoinField.text = joinInput;
            }

            if (_lobbyJoinField != null && _lobbyJoinField.text != joinInput)
            {
                _lobbyJoinField.text = joinInput;
            }

            _syncingFields = false;

            var networkBootstrap = logic.Bootstrap;
            var networkCaseState = networkBootstrap != null && networkBootstrap.IsOnlineSessionActive
                ? networkBootstrap.GetComponent<NetworkCaseState>()
                : null;

            if (_titleText != null)
            {
                _titleText.text = activeMode == MainMenuHud.RuntimeMenuMode.Pause ? "SORUŞTURMA DURAKLATILDI" : "MOBİL OFL";
            }

            if (_subtitleText != null)
            {
                _subtitleText.text = activeMode == MainMenuHud.RuntimeMenuMode.Pause
                    ? BuildPauseSummary(CaseSessionManager.Instance, networkBootstrap, networkCaseState)
                    : "Okul soruşturma arayüzü";
            }

            if (_modeBadgeText != null)
            {
                _modeBadgeText.text = BuildModeBadge(activeMode, networkBootstrap, networkCaseState);
            }

            if (_overviewCaseTitleText != null)
            {
                var session = CaseSessionManager.Instance;
                if (session != null && session.ActiveCase != null)
                {
                    _overviewCaseTitleText.text = session.ActiveCase.CaseTitle;
                    _overviewCaseTargetText.text = "Hedef: " + session.GetRecommendedNextStep();
                }
                else
                {
                    _overviewCaseTitleText.text = "Operasyon seçilmedi";
                    _overviewCaseTargetText.text = "Solo başlatın veya ekip lobisine katılın.";
                }
            }

            if (_overviewAgentIdText != null)
            {
                var cleanName = string.IsNullOrEmpty(profileName) ? "DEDEKTİF" : profileName;
                var hashVal = Mathf.Abs(cleanName.GetHashCode() % 9999);
                _overviewAgentIdText.text = $"Ajan Kodu: OFL-{cleanName}-{hashVal:0000}";
            }

            if (_overviewStatusText != null)
            {
                var telemetry = "Sistem Durumu\n";
                if (networkBootstrap != null && networkBootstrap.IsOnlineSessionActive)
                {
                    var isHost = networkBootstrap.IsHost;
                    telemetry += $"Mod: " + (isHost ? "Lobi sahibi" : "Ekip üyesi") + "\n";
                    telemetry += $"Kod: {BuildLobbyCode(networkBootstrap)}\n";
                    telemetry += "Bağlantı: Relay aktif";
                }
                else
                {
                    telemetry += "Mod: Solo\n";
                    telemetry += "Bağlantı: İnternet gerekmez\n";
                    telemetry += "Kayıt: Yerel";
                }
                _overviewStatusText.text = telemetry;
            }

            if (_statusText != null)
            {
                _statusText.text = logic.CurrentStatus;
            }

            if (_casePreviewText != null)
            {
                _casePreviewText.text = BuildCasePreview(CaseSessionManager.Instance);
            }

            var volPercent = Mathf.RoundToInt(logic.MasterVolume * 100f);
            var lookSens = logic.CameraSensitivity;
            var qual = logic.GraphicsQuality;
            var qualityLabel = qual == 0 ? "Düşük" : (qual == 1 ? "Orta" : "Yüksek");

            if (_settingsVolumeText != null)
            {
                _settingsVolumeText.text = $"Ses: {volPercent}%";
            }

            if (_settingsSensitivityText != null)
            {
                _settingsSensitivityText.text = $"Bakış hassasiyeti: {lookSens:F1}";
            }

            if (_settingsQualityText != null)
            {
                _settingsQualityText.text = $"Grafik Kalitesi: {qualityLabel}";
            }

            if (_lobbyCodeText != null)
            {
                _lobbyCodeText.text = BuildLobbyCode(networkBootstrap);
            }

            if (_lobbyHintText != null)
            {
                _lobbyHintText.text = BuildLobbyHint(networkBootstrap, networkCaseState);
            }

            var isClientReady = networkCaseState != null && IsLocalPlayerReady(networkCaseState);
            if (_readyButtonText != null)
            {
                _readyButtonText.text = isClientReady ? "HAZIR PROTOKOLÜ ETKİN" : "HAZIR DEĞİL";
            }

            if (_readyButton != null)
            {
                _readyButton.GetComponent<Image>().color = isClientReady
                    ? ThemeAccentColor
                    : new Color(1f, 1f, 1f, 0.05f);
                var rOutline = _readyButton.GetComponent<Outline>();
                if (rOutline != null) rOutline.effectColor = isClientReady ? ThemeAccentColor : ThemeBorderColor;
                var rText = _readyButton.GetComponentInChildren<Text>();
                if (rText != null) rText.color = isClientReady ? Color.black : ThemeTextColor;
            }

            if (_reconnectButton != null)
            {
                var canReconnect = networkBootstrap != null && networkBootstrap.CanReconnectLastSession;
                _reconnectButton.gameObject.SetActive(canReconnect && activeMode == MainMenuHud.RuntimeMenuMode.Opening);
            }

            RefreshQualityButtons(qual);
            RebuildRoster(networkCaseState, networkBootstrap);
        }

        private void RefreshQualityButtons(int quality)
        {
            var activeCol = new Color(ThemeAccentColor.r, ThemeAccentColor.g, ThemeAccentColor.b, 0.15f);
            var inactiveCol = new Color(1f, 1f, 1f, 0.03f);

            if (_qualityLowBtn != null)
            {
                _qualityLowBtn.GetComponent<Image>().color = quality == 0 ? activeCol : inactiveCol;
                var text = _qualityLowBtn.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = quality == 0 ? ThemeAccentColor : ThemeTextColor;
                    text.fontStyle = quality == 0 ? FontStyle.Bold : FontStyle.Normal;
                }
                var outline = _qualityLowBtn.GetComponent<Outline>();
                if (outline != null) outline.effectColor = quality == 0 ? ThemeAccentColor : ThemeBorderColor;
            }
            if (_qualityMedBtn != null)
            {
                _qualityMedBtn.GetComponent<Image>().color = quality == 1 ? activeCol : inactiveCol;
                var text = _qualityMedBtn.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = quality == 1 ? ThemeAccentColor : ThemeTextColor;
                    text.fontStyle = quality == 1 ? FontStyle.Bold : FontStyle.Normal;
                }
                var outline = _qualityMedBtn.GetComponent<Outline>();
                if (outline != null) outline.effectColor = quality == 1 ? ThemeAccentColor : ThemeBorderColor;
            }
            if (_qualityHighBtn != null)
            {
                _qualityHighBtn.GetComponent<Image>().color = quality == 2 ? activeCol : inactiveCol;
                var text = _qualityHighBtn.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = quality == 2 ? ThemeAccentColor : ThemeTextColor;
                    text.fontStyle = quality == 2 ? FontStyle.Bold : FontStyle.Normal;
                }
                var outline = _qualityHighBtn.GetComponent<Outline>();
                if (outline != null) outline.effectColor = quality == 2 ? ThemeAccentColor : ThemeBorderColor;
            }
        }

        private void SetQuality(int level)
        {
            if (logic == null) return;
            logic.SetGraphicsQuality(level);
            RefreshImmediate();
        }

        private void SyncFieldsFromLogic()
        {
            _syncingFields = true;

            if (_playerNameField != null && !_playerNameField.isFocused)
            {
                _playerNameField.text = logic.PlayerName;
            }

            if (_openingJoinField != null && !_openingJoinField.isFocused)
            {
                _openingJoinField.text = logic.JoinCodeInput;
            }

            if (_lobbyJoinField != null && !_lobbyJoinField.isFocused)
            {
                _lobbyJoinField.text = logic.JoinCodeInput;
            }

            _syncingFields = false;
        }

        private void ApplyMode(MainMenuHud.RuntimeMenuMode menuMode)
        {
            _openingRoot.gameObject.SetActive(menuMode == MainMenuHud.RuntimeMenuMode.Opening);
            _lobbyRoot.gameObject.SetActive(menuMode == MainMenuHud.RuntimeMenuMode.Lobby);
            _pauseRoot.gameObject.SetActive(menuMode == MainMenuHud.RuntimeMenuMode.Pause);

            _titleText.text = menuMode == MainMenuHud.RuntimeMenuMode.Pause ? "MOBIL OFL" : "MOBIL OFL";
            _subtitleText.text = menuMode == MainMenuHud.RuntimeMenuMode.Pause
                ? "Oyun duraklatildi."
                : "Okul dosyasi, gizlilik ve co-op arastirma.";
        }

        private void RebuildRoster(NetworkCaseState networkCaseState, RelayNetworkBootstrap bootstrap)
        {
            RuntimeUiFactory.ClearChildren(_rosterContent);

            if (bootstrap == null || !bootstrap.IsOnlineSessionActive || networkCaseState == null)
            {
                CreateRosterLine("Lobi acildiginda oyuncular burada listelenir.", ModernGuiTheme.MutedTextColor, false);
                _readyButton.interactable = false;
                _readyButtonText.text = "Hazirim";
                return;
            }

            var roster = networkCaseState.GetReadyRoster();
            if (roster.Count == 0)
            {
                CreateRosterLine("Oyuncu kaydi bekleniyor.", ModernGuiTheme.MutedTextColor, false);
            }
            else
            {
                foreach (var entry in roster)
                {
                    CreateRosterLine(
                        entry.DisplayName + (entry.IsReady ? "  |  Hazir" : "  |  Beklemede"),
                        entry.IsReady ? ModernGuiTheme.TextColor : ModernGuiTheme.MutedTextColor,
                        entry.IsReady);
                }
            }

            var canStart = bootstrap.IsOnlineSessionActive &&
                bootstrap.IsHost &&
                networkCaseState.CanHostStartInvestigation;

            _readyButton.interactable = networkCaseState.IsLobbyPhase;
            _readyButtonText.text = canStart
                ? "Operasyonu Baslat"
                : (IsLocalPlayerReady(networkCaseState) ? "Beklemeye Al" : "Hazirim");
        }

        private void CreateRosterLine(string text, Color color, bool ready)
        {
            var card = RuntimeUiFactory.CreateCard("RosterLine", _rosterContent, new Color(0.1f, 0.12f, 0.15f, 0.94f), ready ? ModernGuiTheme.AccentWarmColor : ModernGuiTheme.BorderColor);
            RuntimeUiFactory.EnsureLayoutElement(card, preferredHeight: 48f);
            RuntimeUiFactory.AddVerticalLayout(card, 0f, new RectOffset(14, 14, 12, 10));
            RuntimeUiFactory.CreateText("LineText", card, text, 15, color, FontStyle.Bold, TextAnchor.MiddleLeft);
        }

        private bool IsLocalPlayerReady(NetworkCaseState networkCaseState)
        {
            if (networkCaseState == null || NetworkManager.Singleton == null)
            {
                return false;
            }

            var localClientId = NetworkManager.Singleton.LocalClientId;
            var roster = networkCaseState.GetReadyRoster();
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster[i].ClientId == localClientId)
                {
                    return roster[i].IsReady;
                }
            }

            return false;
        }

        private void ToggleReady()
        {
            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState == null)
            {
                return;
            }

            var bootstrap = logic != null ? logic.Bootstrap : null;
            if (bootstrap != null &&
                bootstrap.IsOnlineSessionActive &&
                bootstrap.IsHost &&
                networkCaseState.CanHostStartInvestigation)
            {
                networkCaseState.RequestStartInvestigation();
                RefreshImmediate();
                return;
            }

            if (networkCaseState.IsLobbyPhase)
            {
                networkCaseState.RequestSetReady(!IsLocalPlayerReady(networkCaseState));
                RefreshImmediate();
            }
        }

        private void OpenNotebookFromPause()
        {
            CaseNotebookHud.Instance?.CloseNotebook();
        }

        private void ShutdownSession()
        {
            var bootstrap = logic != null ? logic.Bootstrap : null;
            if (bootstrap == null || !bootstrap.IsOnlineSessionActive)
            {
                return;
            }

            bootstrap.ShutdownSession();
            logic?.OpenMenu(bootstrap.CurrentStatus);
            RefreshImmediate();
        }

        private void ClearSoloSave()
        {
            var session = CaseSessionManager.Instance;
            if (session == null || session.ActiveCase == null)
            {
                return;
            }

            var saveManager = CaseSaveManager.Instance;
            if (saveManager != null)
            {
                saveManager.ClearSaveForCase(session.ActiveCase.CaseId);
            }

            session.RestartCurrentCase();
            session.PublishMessage("Solo kayit sifirlandi. Vaka temiz baslatildi.");
            RefreshImmediate();
        }

        private void AdjustVolume(float delta)
        {
            if (logic == null)
            {
                return;
            }

            logic.SetMasterVolume(logic.MasterVolume + delta);
            RefreshImmediate();
        }

        private void AdjustLookSensitivity(float delta)
        {
            if (logic == null)
            {
                return;
            }

            logic.SetCameraSensitivity(logic.CameraSensitivity + delta);
            RefreshImmediate();
        }

        private void CopyJoinCode()
        {
            if (logic == null)
            {
                return;
            }

            var code = logic.Bootstrap != null && !string.IsNullOrWhiteSpace(logic.Bootstrap.CurrentJoinCode)
                ? logic.Bootstrap.CurrentJoinCode
                : logic.JoinCodeInput;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = code;
        }

        private void OnPlayerNameChanged(string value)
        {
            if (_syncingFields || logic == null)
            {
                return;
            }

            logic.PlayerName = value;
        }

        private void OnJoinCodeChanged(string value)
        {
            if (_syncingFields || logic == null)
            {
                return;
            }

            logic.JoinCodeInput = value;
        }

        private void SyncVisibility()
        {
            if (!_built || logic == null)
            {
                return;
            }

            _overlayRoot.gameObject.SetActive(true);
            _collapsedRoot.gameObject.SetActive(!logic.IsOpen && _openBlend <= 0.02f);
            _collapsedRoot.anchoredPosition = MobileInvestigationOverlay.IsMobileHudVisible
                ? new Vector2(24f, 242f)
                : new Vector2(24f, 218f);
        }

        private void ApplyResponsiveLayout()
        {
            if (!_built || _windowRoot == null || logic == null)
            {
                return;
            }

            var mobileLayout = MobileInvestigationOverlay.IsMobileUiAllowed || Screen.width < 1500;

            _windowRoot.anchorMin = Vector2.zero;
            _windowRoot.anchorMax = Vector2.one;

            var margin = mobileLayout ? 18f : 30f;
            _windowRoot.offsetMin = new Vector2(margin, margin);
            _windowRoot.offsetMax = new Vector2(-margin, -margin);

            var showSidebar = logic.CurrentMenuMode == MainMenuHud.RuntimeMenuMode.Opening;
            var sidebarWidth = mobileLayout ? 240f : 280f;
            var gap = mobileLayout ? 12f : 16f;
            AnchorSidebarAndContent(sidebarWidth, gap, showSidebar);
        }

        private void AnimateMenu(bool visible)
        {
            if (_overlayGroup == null || _windowRoot == null)
            {
                return;
            }

            var target = visible ? 1f : 0f;
            var deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 1f / 60f;
            _openBlend = Mathf.MoveTowards(_openBlend, target, deltaTime * 4.0f);
            _overlayGroup.alpha = Mathf.SmoothStep(0f, 1f, _openBlend);
            _overlayGroup.interactable = _openBlend > 0.98f;
            _overlayGroup.blocksRaycasts = _openBlend > 0.02f;
            _windowRoot.localScale = Vector3.Lerp(new Vector3(0.96f, 0.98f, 1f), Vector3.one, _openBlend);
            _windowRoot.anchoredPosition = Vector2.Lerp(new Vector2(0f, 20f), Vector2.zero, _openBlend);
        }

        private static string BuildModeBadge(MainMenuHud.RuntimeMenuMode mode, RelayNetworkBootstrap bootstrap, NetworkCaseState networkCaseState)
        {
            if (mode == MainMenuHud.RuntimeMenuMode.Opening)
            {
                return "Sistem hazır";
            }

            if (mode == MainMenuHud.RuntimeMenuMode.Pause)
            {
                return "Duraklatıldı";
            }

            if (bootstrap == null || !bootstrap.IsOnlineSessionActive)
            {
                return "Yerel mod";
            }

            return networkCaseState == null
                ? "Çok oyunculu"
                : $"Lobi {networkCaseState.ReadyPlayerCount}/{networkCaseState.RegisteredPlayerCount} hazır";
        }

        private static string BuildCasePreview(CaseSessionManager session)
        {
            if (session == null || session.ActiveCase == null)
            {
                return "Soruşturulacak aktif bir vaka verisi yüklenemedi.\n\nLütfen operasyonu başlatmak için ana merkezden bir dosya veya bağlantı seçin.";
            }

            return "<size=20><color=#F59E0B>" + session.ActiveCase.CaseTitle.ToUpper() + "</color></size>\n\n" +
                "ÖN BRİFİNG RAPORU:\n" +
                session.ActiveCase.OpeningBrief + "\n\n" +
                "<color=#22D3EE>SIRADAKİ OPERASYON HEDEFİ:</color>\n" +
                session.GetRecommendedNextStep();
        }

        private static string BuildLobbyCode(RelayNetworkBootstrap bootstrap)
        {
            if (bootstrap == null || string.IsNullOrWhiteSpace(bootstrap.CurrentJoinCode))
            {
                return "AĞ KODU ALINMADI";
            }

            return bootstrap.CurrentJoinCode;
        }

        private static string BuildLobbyHint(RelayNetworkBootstrap bootstrap, NetworkCaseState networkCaseState)
        {
            if (bootstrap == null || !bootstrap.IsOnlineSessionActive)
            {
                return "Lobi kurun ya da bir takımdan aldığınız erişim kodunu girin.";
            }

            if (networkCaseState == null)
            {
                return "Relay sunucusuna bağlanıldı. Ajan kimlik kayıtları taranıyor...";
            }

            if (networkCaseState.AreAllRegisteredPlayersReady && bootstrap.IsHost)
            {
                return "Ekipteki tüm dedektifler hazır. Operasyonu başlatmak için yetkiniz var.";
            }

            return "Yukarıdaki davet kodunu ekip arkadaşlarınıza gönderin. Herkes hazırlandığında operasyon başlar.";
        }

        private static string BuildPauseSummary(CaseSessionManager session, RelayNetworkBootstrap bootstrap, NetworkCaseState networkCaseState)
        {
            var mode = bootstrap == null ? "ÇEVRİMDIŞI" : bootstrap.CurrentMode.ToUpper();
            var phase = networkCaseState == null ? "SERBEST KEŞİF" : networkCaseState.CurrentPhaseLabel.ToUpper();

            if (session == null || session.ActiveCase == null)
            {
                return $"Operasyon Modu: {mode}\nFaz Aşaması: {phase}\nSoruşturma vaka bilgisi bekleniyor.";
            }

            return $"AKTİF DOSYA: <color=#F59E0B>{session.ActiveCase.CaseTitle.ToUpper()}</color>\n" +
                $"BAĞLANTI MODU: {mode}  |  AŞAMA FAZI: {phase}\n" +
                $"TOPLANAN KANITLAR: {session.CollectedEvidenceIds.Count}/{session.ActiveCase.EvidenceItems.Count}  |  SORGULANAN SUPHELİLER: {session.InterviewedNpcCount}\n\n" +
                $"<color=#22D3EE>AKTİF HEDEF:</color> {session.GetRecommendedNextStep()}";
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = GameObject.Find("EventSystem");
            if (eventSystemObject == null)
            {
                var current = UnityEngine.EventSystems.EventSystem.current;
                if (current != null)
                {
                    eventSystemObject = current.gameObject;
                }
                else
                {
                    eventSystemObject = new GameObject("EventSystem");
                }
            }

            var eventSysComp = eventSystemObject.GetComponent<UnityEngine.EventSystems.EventSystem>();
            if (eventSysComp == null)
            {
                eventSysComp = eventSystemObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var newModule = eventSystemObject.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (newModule != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(newModule);
                else
                    Object.DestroyImmediate(newModule);
            }

            var legacyModule = eventSystemObject.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule == null)
            {
                eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }
}
