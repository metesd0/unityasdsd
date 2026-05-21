using System;
using System.Collections.Generic;
using MobilOfl.Case;
using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public class UguiNotebookHud : MonoBehaviour
    {
        private enum NotebookTab
        {
            Overview,
            Evidence,
            Interviews,
            Notes,
            Suspects
        }

        [SerializeField] private CaseNotebookHud logic;

        private Canvas _canvas;
        private RectTransform _overlayRoot;
        private RectTransform _panelRoot;
        private RectTransform _frameRoot;
        private RectTransform _sidebarRoot;
        private RectTransform _mobileHeaderRoot;
        private CanvasGroup _overlayGroup;
        private float _openBlend;
        private RectTransform _contentArea;
        private RectTransform _tabRootHost;
        private Button[] _tabButtons;
        private Text[] _tabButtonTexts;
        private Button[] _mobileTabButtons;
        private Text[] _mobileTabButtonTexts;
        private readonly string[] _tabBaseLabels = { "Genel Durum", "Deliller", "Sorgular", "Notlar", "Supheliler" };
        private Text _caseTitleText;
        private Text _statsText;
        private Text _reasoningText;
        private Text _dossierStatusText;
        private Text _dossierMetaText;
        private RectTransform _overviewContent;
        private RectTransform _evidenceTabRoot;
        private HorizontalLayoutGroup _evidenceHorizontalLayout;
        private RectTransform _evidenceListCard;
        private RectTransform _evidenceDetailCard;
        private RectTransform _evidenceListContent;
        private RectTransform _interviewContent;
        private RectTransform _notesListContent;
        private RectTransform _suspectsContent;
        private Text _evidenceDetailsText;
        private InputField _noteInput;
        private Text _noteCounterText;
        private string _selectedEvidenceId;
        private string _selectedMotive;
        private string _selectedTimeline;
        private NotebookTab _selectedTab;
        private float _nextRefreshAt;
        private bool _built;
        private bool _syncingNoteField;

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
            if (logic == null)
            {
                logic = GetComponent<CaseNotebookHud>();
                if (logic == null)
                {
                    logic = CaseNotebookHud.Instance;
                }

                if (logic != null)
                {
                    logic.RenderWithOnGui = false;
                }
            }

            if (!_built)
            {
                BuildIfNeeded();
            }

            if (logic == null)
            {
                return;
            }

            ApplyResponsiveLayout();
            var shouldShow = logic.IsOpen && !MainMenuHud.IsBlockingGameplay;
            _overlayRoot.gameObject.SetActive(true);
            AnimateNotebook(shouldShow);
            if (!shouldShow && _openBlend <= 0.01f)
            {
                return;
            }

            var desiredTab = (NotebookTab)Mathf.Clamp(logic.CurrentTabIndex, 0, 4);
            if (desiredTab != _selectedTab)
            {
                SetSelectedTab(desiredTab, false);
            }

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                RefreshImmediate();
                _nextRefreshAt = Time.unscaledTime + 0.2f;
            }
        }

        private void BuildIfNeeded()
        {
            if (_built)
            {
                return;
            }

            if (logic == null)
            {
                logic = GetComponent<CaseNotebookHud>();
            }

            if (logic != null)
            {
                logic.RenderWithOnGui = false;
            }

            var canvasTransform = transform.Find("UguiNotebookCanvas") as RectTransform;
            if (canvasTransform == null)
            {
                canvasTransform = RuntimeUiFactory.CreateUiRoot("UguiNotebookCanvas", transform);
            }

            _canvas = canvasTransform.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 86;

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

            BuildOverlay(canvasTransform);

            _built = true;
            _openBlend = logic != null && logic.IsOpen ? 1f : 0f;
            if (logic != null)
            {
                _selectedTab = (NotebookTab)Mathf.Clamp(logic.CurrentTabIndex, 0, 4);
            }
            SetSelectedTab(_selectedTab, false);
            AnimateNotebook(logic != null && logic.IsOpen);
        }

        private void BuildOverlay(RectTransform canvasTransform)
        {
            _overlayRoot = RuntimeUiFactory.CreateUiRoot("OverlayRoot", canvasTransform);
            RuntimeUiFactory.Stretch(_overlayRoot);
            RuntimeUiFactory.AddImage(_overlayRoot.gameObject, new Color(0.015f, 0.025f, 0.035f, 0.84f));
            _overlayGroup = _overlayRoot.gameObject.GetComponent<CanvasGroup>();
            if (_overlayGroup == null)
            {
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            }

            _panelRoot = RuntimeUiFactory.CreateCard("NotebookPanel", _overlayRoot, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentColor);
            _panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRoot.pivot = new Vector2(0.5f, 0.5f);
            _panelRoot.sizeDelta = new Vector2(1320f, 760f);
            _panelRoot.anchoredPosition = Vector2.zero;

            var frame = RuntimeUiFactory.CreateUiRoot("Frame", _panelRoot);
            _frameRoot = frame;
            RuntimeUiFactory.Stretch(frame);
            var bodyLayout = RuntimeUiFactory.AddHorizontalLayout(frame, 14f, new RectOffset(20, 20, 20, 20), true);
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;

            BuildSidebar(frame);
            BuildContent(frame);
        }

        private void BuildAtmosphereDecor()
        {
            var topWash = RuntimeUiFactory.CreateUiRoot("TopWash", _panelRoot);
            topWash.anchorMin = new Vector2(0f, 1f);
            topWash.anchorMax = new Vector2(1f, 1f);
            topWash.pivot = new Vector2(0.5f, 1f);
            topWash.sizeDelta = new Vector2(0f, 110f);
            RuntimeUiFactory.AddImage(topWash.gameObject, new Color(0.12f, 0.18f, 0.2f, 0.12f));
        }

        private void BuildSidebar(Transform parent)
        {
            var sidebar = RuntimeUiFactory.CreateCard("Sidebar", parent, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
            _sidebarRoot = sidebar;
            RuntimeUiFactory.EnsureLayoutElement(sidebar, preferredWidth: 280f, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(sidebar, 8f, new RectOffset(16, 16, 16, 16));

            RuntimeUiFactory.CreateText("SidebarTitle", sidebar, "VAKA DOSYASI", 24, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _caseTitleText = RuntimeUiFactory.CreateText("CaseTitle", sidebar, "-", 15, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var statsCard = RuntimeUiFactory.CreateCard("StatsCard", sidebar, new Color(0.08f, 0.1f, 0.13f, 0.95f), ModernGuiTheme.AccentColor);
            RuntimeUiFactory.EnsureLayoutElement(statsCard, preferredHeight: 112f);
            RuntimeUiFactory.AddVerticalLayout(statsCard, 5f, new RectOffset(12, 12, 14, 12));
            RuntimeUiFactory.CreateText("StatsLabel", statsCard, "OTURUM OZETI", 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _statsText = RuntimeUiFactory.CreateText("StatsText", statsCard, string.Empty, 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);

            var tabsHost = RuntimeUiFactory.CreateUiRoot("Tabs", sidebar);
            RuntimeUiFactory.EnsureLayoutElement(tabsHost, preferredHeight: 194f);
            RuntimeUiFactory.AddVerticalLayout(tabsHost, 6f, new RectOffset(0, 0, 0, 0));
            _tabButtons = new Button[5];
            _tabButtonTexts = new Text[5];
            CreateTabButton(tabsHost, NotebookTab.Overview, "Genel Durum");
            CreateTabButton(tabsHost, NotebookTab.Evidence, "Deliller");
            CreateTabButton(tabsHost, NotebookTab.Interviews, "Sorgular");
            CreateTabButton(tabsHost, NotebookTab.Notes, "Notlar");
            CreateTabButton(tabsHost, NotebookTab.Suspects, "Supheliler");

            var quickCard = RuntimeUiFactory.CreateCard("QuickCard", sidebar, new Color(0.08f, 0.1f, 0.13f, 0.95f), ModernGuiTheme.AccentWarmColor);
            RuntimeUiFactory.EnsureLayoutElement(quickCard, preferredHeight: 132f);
            RuntimeUiFactory.AddVerticalLayout(quickCard, 8f, new RectOffset(14, 14, 16, 14));
            RuntimeUiFactory.CreateText("QuickLabel", quickCard, "DOSYA ANALIZI", 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _reasoningText = RuntimeUiFactory.CreateText("ReasoningText", quickCard, string.Empty, 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);

            var closeButton = RuntimeUiFactory.CreateButton("CloseNotebookButton", sidebar, "Dosyayi Kapat", new Color(0.12f, 0.12f, 0.15f, 1f), 16);
            RuntimeUiFactory.EnsureLayoutElement(closeButton.transform, preferredHeight: 42f);
            closeButton.onClick.AddListener(() => logic?.CloseNotebook());
        }

        private void CreateTabButton(Transform parent, NotebookTab tab, string label)
        {
            var button = RuntimeUiFactory.CreateButton(tab + "TabButton", parent, label, new Color(0.11f, 0.13f, 0.16f, 1f), 14);
            RuntimeUiFactory.EnsureLayoutElement(button.transform, preferredHeight: 34f);
            button.onClick.AddListener(() => SetSelectedTab(tab, true));
            _tabButtons[(int)tab] = button;
            _tabButtonTexts[(int)tab] = button.GetComponentInChildren<Text>();
        }
        private void BuildContent(Transform parent)
        {
            _contentArea = RuntimeUiFactory.CreateUiRoot("ContentArea", parent);
            RuntimeUiFactory.EnsureLayoutElement(_contentArea, flexibleWidth: 1f, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(_contentArea, 12f, new RectOffset(0, 0, 0, 0));

            BuildMobileHeader();
            BuildDossierStrip();

            _tabRootHost = RuntimeUiFactory.CreateUiRoot("TabRootHost", _contentArea);
            RuntimeUiFactory.EnsureLayoutElement(_tabRootHost, flexibleWidth: 1f, flexibleHeight: 1f);
            RuntimeUiFactory.Stretch(_tabRootHost);

            BuildOverviewTab();
            BuildEvidenceTab();
            BuildInterviewsTab();
            BuildNotesTab();
            BuildSuspectsTab();
        }

        private void BuildMobileHeader()
        {
            _mobileHeaderRoot = RuntimeUiFactory.CreateCard("MobileHeader", _contentArea, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
            RuntimeUiFactory.EnsureLayoutElement(_mobileHeaderRoot, preferredHeight: 112f);
            RuntimeUiFactory.AddVerticalLayout(_mobileHeaderRoot, 8f, new RectOffset(14, 14, 13, 12));
            RuntimeUiFactory.CreateText("MobileTitle", _mobileHeaderRoot, "VAKA DOSYASI", 20, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var mobileTabsHost = RuntimeUiFactory.CreateUiRoot("MobileTabs", _mobileHeaderRoot);
            RuntimeUiFactory.EnsureLayoutElement(mobileTabsHost, preferredHeight: 42f);
            var tabsLayout = RuntimeUiFactory.AddHorizontalLayout(mobileTabsHost, 7f, new RectOffset(0, 0, 0, 0), true);
            tabsLayout.childForceExpandWidth = true;

            _mobileTabButtons = new Button[5];
            _mobileTabButtonTexts = new Text[5];
            CreateMobileTabButton(mobileTabsHost, NotebookTab.Overview, "Genel");
            CreateMobileTabButton(mobileTabsHost, NotebookTab.Evidence, "Delil");
            CreateMobileTabButton(mobileTabsHost, NotebookTab.Interviews, "Sorgu");
            CreateMobileTabButton(mobileTabsHost, NotebookTab.Notes, "Not");
            CreateMobileTabButton(mobileTabsHost, NotebookTab.Suspects, "Supheli");
            _mobileHeaderRoot.gameObject.SetActive(false);
        }

        private void CreateMobileTabButton(Transform parent, NotebookTab tab, string label)
        {
            var button = RuntimeUiFactory.CreateButton(tab + "MobileTabButton", parent, label, new Color(0.11f, 0.13f, 0.16f, 1f), 14);
            RuntimeUiFactory.EnsureLayoutElement(button.transform, flexibleWidth: 1f, preferredHeight: 42f);
            button.onClick.AddListener(() => SetSelectedTab(tab, true));
            _mobileTabButtons[(int)tab] = button;
            _mobileTabButtonTexts[(int)tab] = button.GetComponentInChildren<Text>();
        }

        private void BuildDossierStrip()
        {
            var strip = RuntimeUiFactory.CreateCard("DossierStrip", _contentArea, new Color(0.08f, 0.1f, 0.13f, 0.95f), ModernGuiTheme.AccentColor);
            RuntimeUiFactory.EnsureLayoutElement(strip, preferredHeight: 72f);
            var layout = RuntimeUiFactory.AddHorizontalLayout(strip, 12f, new RectOffset(16, 16, 12, 10), true);
            layout.childForceExpandWidth = false;

            var left = RuntimeUiFactory.CreateUiRoot("StatusBlock", strip);
            RuntimeUiFactory.EnsureLayoutElement(left, flexibleWidth: 1f);
            RuntimeUiFactory.AddVerticalLayout(left, 2f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.CreateText("StatusLabel", left, "DOSYA DURUMU", 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _dossierStatusText = RuntimeUiFactory.CreateText("StatusText", left, "Analiz bekleniyor.", 15, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var right = RuntimeUiFactory.CreateUiRoot("MetaBlock", strip);
            RuntimeUiFactory.EnsureLayoutElement(right, preferredWidth: 300f);
            RuntimeUiFactory.AddVerticalLayout(right, 2f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.CreateText("MetaLabel", right, "KISA OZET", 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _dossierMetaText = RuntimeUiFactory.CreateText("MetaText", right, "-", 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        private void BuildOverviewTab()
        {
            var tabRoot = CreateTabRoot("OverviewTab");
            var scroll = RuntimeUiFactory.CreateScrollView("OverviewScroll", tabRoot, out _overviewContent);
            RuntimeUiFactory.Stretch(scroll.GetComponent<RectTransform>());
            RuntimeUiFactory.AddVerticalLayout(_overviewContent, 12f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_overviewContent, ContentSizeFitter.FitMode.PreferredSize);
        }

        private void BuildEvidenceTab()
        {
            _evidenceTabRoot = CreateTabRoot("EvidenceTab");
            _evidenceHorizontalLayout = RuntimeUiFactory.AddHorizontalLayout(_evidenceTabRoot, 14f, new RectOffset(0, 0, 0, 0), true);
            _evidenceHorizontalLayout.childForceExpandWidth = true;
            _evidenceHorizontalLayout.childForceExpandHeight = true;

            var listCard = RuntimeUiFactory.CreateCard("EvidenceListCard", _evidenceTabRoot, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentColor);
            _evidenceListCard = listCard;
            RuntimeUiFactory.EnsureLayoutElement(listCard, preferredWidth: 410f, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(listCard, 10f, new RectOffset(16, 16, 16, 16));
            RuntimeUiFactory.CreateText("EvidenceListLabel", listCard, "BULUNAN DELILLER", 18, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            var listScroll = RuntimeUiFactory.CreateScrollView("EvidenceListScroll", listCard, out _evidenceListContent);
            RuntimeUiFactory.EnsureLayoutElement(listScroll.transform, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(_evidenceListContent, 8f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_evidenceListContent, ContentSizeFitter.FitMode.PreferredSize);

            var detailCard = RuntimeUiFactory.CreateCard("EvidenceDetailCard", _evidenceTabRoot, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
            _evidenceDetailCard = detailCard;
            RuntimeUiFactory.EnsureLayoutElement(detailCard, flexibleWidth: 1f, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(detailCard, 10f, new RectOffset(18, 18, 18, 18));
            RuntimeUiFactory.CreateText("EvidenceDetailLabel", detailCard, "DELIL DETAYI", 20, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _evidenceDetailsText = RuntimeUiFactory.CreateText("EvidenceDetailText", detailCard, "Bir delil sec.", 16, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        private void BuildInterviewsTab()
        {
            var tabRoot = CreateTabRoot("InterviewsTab");
            var scroll = RuntimeUiFactory.CreateScrollView("InterviewsScroll", tabRoot, out _interviewContent);
            RuntimeUiFactory.Stretch(scroll.GetComponent<RectTransform>());
            RuntimeUiFactory.AddVerticalLayout(_interviewContent, 10f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_interviewContent, ContentSizeFitter.FitMode.PreferredSize);
        }

        private void BuildNotesTab()
        {
            var tabRoot = CreateTabRoot("NotesTab");
            var row = RuntimeUiFactory.AddHorizontalLayout(tabRoot, 14f, new RectOffset(0, 0, 0, 0), true);
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            var notesCard = RuntimeUiFactory.CreateCard("NotesListCard", tabRoot, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentColor);
            RuntimeUiFactory.EnsureLayoutElement(notesCard, flexibleWidth: 1f, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(notesCard, 10f, new RectOffset(16, 16, 16, 16));
            RuntimeUiFactory.CreateText("NotesListLabel", notesCard, "TAKIM NOTLARI", 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            var notesScroll = RuntimeUiFactory.CreateScrollView("NotesListScroll", notesCard, out _notesListContent);
            RuntimeUiFactory.EnsureLayoutElement(notesScroll.transform, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(_notesListContent, 8f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_notesListContent, ContentSizeFitter.FitMode.PreferredSize);

            var composeCard = RuntimeUiFactory.CreateCard("ComposeCard", tabRoot, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
            RuntimeUiFactory.EnsureLayoutElement(composeCard, preferredWidth: 360f, flexibleHeight: 1f);
            RuntimeUiFactory.AddVerticalLayout(composeCard, 10f, new RectOffset(18, 18, 18, 18));
            RuntimeUiFactory.CreateText("ComposeLabel", composeCard, "YENI NOT", 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            RuntimeUiFactory.CreateText("ComposeHint", composeCard, "Kisa, net ve tek satir notlar ortak dosyada daha temiz gorunur.", 14, ModernGuiTheme.MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            _noteInput = RuntimeUiFactory.CreateInputField("NoteInput", composeCard, "Notunu yaz", 18);
            RuntimeUiFactory.EnsureLayoutElement(_noteInput.transform, preferredHeight: 54f);
            _noteInput.onValueChanged.AddListener(HandleNoteChanged);
            _noteCounterText = RuntimeUiFactory.CreateText("NoteCounter", composeCard, "0/140", 13, ModernGuiTheme.MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft);

            var actions = RuntimeUiFactory.CreateUiRoot("NoteActions", composeCard);
            RuntimeUiFactory.EnsureLayoutElement(actions, preferredHeight: 46f);
            var actionsLayout = RuntimeUiFactory.AddHorizontalLayout(actions, 10f, new RectOffset(0, 0, 0, 0), true);
            actionsLayout.childForceExpandWidth = true;

            var addButton = RuntimeUiFactory.CreateButton("AddNoteButton", actions, "Takima Ekle", new Color(0.13f, 0.2f, 0.16f, 1f), 16);
            RuntimeUiFactory.EnsureLayoutElement(addButton.transform, flexibleWidth: 1f, preferredHeight: 46f);
            addButton.onClick.AddListener(SubmitTeamNote);

            var clearButton = RuntimeUiFactory.CreateButton("ClearNoteButton", actions, "Temizle", new Color(0.12f, 0.12f, 0.15f, 1f), 16);
            RuntimeUiFactory.EnsureLayoutElement(clearButton.transform, flexibleWidth: 1f, preferredHeight: 46f);
            clearButton.onClick.AddListener(ClearNoteDraft);
        }

        private void BuildSuspectsTab()
        {
            var tabRoot = CreateTabRoot("SuspectsTab");
            var scroll = RuntimeUiFactory.CreateScrollView("SuspectsScroll", tabRoot, out _suspectsContent);
            RuntimeUiFactory.Stretch(scroll.GetComponent<RectTransform>());
            RuntimeUiFactory.AddVerticalLayout(_suspectsContent, 10f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_suspectsContent, ContentSizeFitter.FitMode.PreferredSize);
        }

        private RectTransform CreateTabRoot(string name)
        {
            var tabRoot = RuntimeUiFactory.CreateUiRoot(name, _tabRootHost);
            RuntimeUiFactory.EnsureLayoutElement(tabRoot, flexibleWidth: 1f, flexibleHeight: 1f);
            RuntimeUiFactory.Stretch(tabRoot);
            return tabRoot;
        }

        private void SetSelectedTab(NotebookTab tab, bool pushToLogic)
        {
            _selectedTab = tab;
            if (pushToLogic)
            {
                logic?.SetTabIndex((int)tab);
            }

            for (var i = 0; i < _tabRootHost.childCount; i++)
            {
                _tabRootHost.GetChild(i).gameObject.SetActive(i == (int)_selectedTab);
            }

            for (var i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null)
                {
                    continue;
                }

                var image = _tabButtons[i].GetComponent<Image>();
                image.color = i == (int)_selectedTab
                    ? new Color(0.26f, 0.21f, 0.1f, 1f)
                    : new Color(0.11f, 0.13f, 0.16f, 1f);
                _tabButtonTexts[i].color = i == (int)_selectedTab ? ModernGuiTheme.TextColor : ModernGuiTheme.MutedTextColor;
            }

            if (_mobileTabButtons != null)
            {
                for (var i = 0; i < _mobileTabButtons.Length; i++)
                {
                    if (_mobileTabButtons[i] == null)
                    {
                        continue;
                    }

                    var image = _mobileTabButtons[i].GetComponent<Image>();
                    image.color = i == (int)_selectedTab
                        ? new Color(0.26f, 0.21f, 0.1f, 1f)
                        : new Color(0.11f, 0.13f, 0.16f, 1f);
                    _mobileTabButtonTexts[i].color = i == (int)_selectedTab ? ModernGuiTheme.TextColor : ModernGuiTheme.MutedTextColor;
                }
            }

            RefreshImmediate();
        }

        private void ApplyResponsiveLayout()
        {
            if (!_built || _panelRoot == null)
            {
                return;
            }

            var mobileLayout = UseMobileNotebookLayout();
            _panelRoot.sizeDelta = mobileLayout ? new Vector2(1220f, 720f) : new Vector2(1320f, 760f);

            if (_sidebarRoot != null)
            {
                _sidebarRoot.gameObject.SetActive(!mobileLayout);
            }

            if (_mobileHeaderRoot != null)
            {
                _mobileHeaderRoot.gameObject.SetActive(mobileLayout);
                RuntimeUiFactory.EnsureLayoutElement(_mobileHeaderRoot, preferredHeight: mobileLayout ? 112f : 0f);
            }

            if (_evidenceHorizontalLayout != null)
            {
                _evidenceHorizontalLayout.enabled = true;
            }

            if (_evidenceListCard != null)
            {
                RuntimeUiFactory.EnsureLayoutElement(
                    _evidenceListCard,
                    preferredWidth: mobileLayout ? -1f : 410f,
                    preferredHeight: mobileLayout ? 230f : -1f,
                    flexibleWidth: mobileLayout ? 1f : 0f,
                    flexibleHeight: mobileLayout ? 0f : 1f);
            }

            if (_evidenceDetailCard != null)
            {
                RuntimeUiFactory.EnsureLayoutElement(
                    _evidenceDetailCard,
                    flexibleWidth: 1f,
                    flexibleHeight: 1f);
            }
        }

        private static bool UseMobileNotebookLayout()
        {
            return MobileInvestigationOverlay.IsMobileUiAllowed ||
                Application.isMobilePlatform ||
                Screen.width < 1500 ||
                Screen.height < 900;
        }

        private void RefreshImmediate()
        {
            if (!_built || logic == null)
            {
                return;
            }

            var session = CaseSessionManager.Instance;
            if (session == null || session.ActiveCase == null)
            {
                _caseTitleText.text = "Aktif vaka bekleniyor";
                _statsText.text = "Delil, sure ve not bilgisi daha sonra burada dolacak.";
                _reasoningText.text = "Ilk delili toplayinca dosya analizi otomatik guncellenecek.";
                return;
            }

            _caseTitleText.text = session.ActiveCase.CaseTitle;
            _statsText.text =
                $"Sure: {FormatTime(session.ElapsedCaseTimeSeconds)}\n" +
                $"Delil: {session.CollectedEvidenceIds.Count}/{session.ActiveCase.EvidenceItems.Count}\n" +
                $"Kritik: {session.CollectedCriticalEvidenceCount}/{session.TotalCriticalEvidenceCount}\n" +
                $"Cikarim: {session.InferenceHistory.Count}\n" +
                $"Takim notu: {session.TeamNotes.Count}";
            _reasoningText.text = session.GetReasoningSummary();
            _dossierStatusText.text = BuildDossierStatus(session);
            _dossierMetaText.text = BuildDossierMeta(session);
            RefreshTabLabels(session);

            if (_noteInput != null)
            {
                _syncingNoteField = true;
                if (_noteInput.text.Length > 140)
                {
                    _noteInput.text = _noteInput.text.Substring(0, 140);
                }
                _syncingNoteField = false;
                _noteCounterText.text = $"{_noteInput.text.Length}/140";
            }

            switch (_selectedTab)
            {
                case NotebookTab.Overview:
                    RebuildOverview(session);
                    break;
                case NotebookTab.Evidence:
                    RebuildEvidence(session);
                    break;
                case NotebookTab.Interviews:
                    RebuildInterviews(session);
                    break;
                case NotebookTab.Notes:
                    RebuildNotes(session);
                    break;
                case NotebookTab.Suspects:
                    RebuildSuspects(session);
                    break;
            }
        }
        private void RebuildOverview(CaseSessionManager session)
        {
            RuntimeUiFactory.ClearChildren(_overviewContent);
            AddSectionCard(_overviewContent, "Brifing", session.ActiveCase.OpeningBrief);
            AddSectionCard(_overviewContent, "Siradaki Mantikli Hamle", session.GetRecommendedNextStep());
            AddSectionCard(_overviewContent, "Kritik Cikarimlar", session.GetInferenceSummary());
            AddSectionCard(_overviewContent, "Dosya Analizi", session.GetReasoningSummary());

            var exploration = UnityEngine.Object.FindAnyObjectByType<SchoolExplorationTracker>();
            var explorationText = exploration == null || exploration.VisitedZoneCount == 0
                ? "Henuz bolge kaydi yok."
                : string.Join("  |  ", exploration.VisitedZones);
            AddSectionCard(_overviewContent, "Gezilen Bolgeler", explorationText);

            var bootstrap = UnityEngine.Object.FindAnyObjectByType<RelayNetworkBootstrap>();
            if (bootstrap != null)
            {
                var onlineText = bootstrap.CurrentStatus;
                if (!string.IsNullOrWhiteSpace(bootstrap.CurrentJoinCode))
                {
                    onlineText += "\nJoin code: " + bootstrap.CurrentJoinCode;
                }

                var networkCaseState = NetworkCaseState.Instance;
                if (networkCaseState != null && bootstrap.IsOnlineSessionActive)
                {
                    onlineText += $"\nHazir oyuncu: {networkCaseState.ReadyPlayerCount}/{networkCaseState.RegisteredPlayerCount}";
                }

                AddSectionCard(_overviewContent, "Co-op Durumu", onlineText);
            }

            var updates = RuntimeUiFactory.CreateCard("UpdatesCard", _overviewContent, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
            RuntimeUiFactory.EnsureLayoutElement(updates, flexibleWidth: 1f, preferredHeight: 132f);
            RuntimeUiFactory.AddVerticalLayout(updates, 8f, new RectOffset(16, 16, 16, 14));
            RuntimeUiFactory.CreateText("UpdatesLabel", updates, "Son Gelismeler", 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            if (session.MessageHistory.Count == 0)
            {
                RuntimeUiFactory.CreateText("UpdatesEmpty", updates, "Henuz oturum kaydi yok.", 14, ModernGuiTheme.MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
                return;
            }

            for (var i = session.MessageHistory.Count - 1; i >= 0; i--)
            {
                RuntimeUiFactory.CreateText("Update" + i, updates, "- " + session.MessageHistory[i], 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            }
        }

        private void RebuildEvidence(CaseSessionManager session)
        {
            RuntimeUiFactory.ClearChildren(_evidenceListContent);
            if (string.IsNullOrWhiteSpace(_selectedEvidenceId) || !session.HasEvidence(_selectedEvidenceId))
            {
                _selectedEvidenceId = null;
            }

            foreach (var evidence in session.ActiveCase.EvidenceItems)
            {
                if (evidence == null || !session.HasEvidence(evidence.Id))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(_selectedEvidenceId))
                {
                    _selectedEvidenceId = evidence.Id;
                }

                var color = evidence.Id == _selectedEvidenceId
                    ? new Color(0.26f, 0.21f, 0.1f, 1f)
                    : new Color(0.1f, 0.12f, 0.15f, 1f);
                var button = RuntimeUiFactory.CreateButton("EvidenceButton" + evidence.Id, _evidenceListContent, evidence.Title, color, 15);
                RuntimeUiFactory.EnsureLayoutElement(button.transform, preferredHeight: 46f);
                var capturedId = evidence.Id;
                button.onClick.AddListener(() =>
                {
                    _selectedEvidenceId = capturedId;
                    RefreshImmediate();
                });
            }

            if (string.IsNullOrWhiteSpace(_selectedEvidenceId))
            {
                _evidenceDetailsText.text = "Henuz kilidi acilmis delil yok. Sahneyi tara ve ilk fiziksel izi topla.";
                return;
            }

            var selectedEvidence = session.GetEvidence(_selectedEvidenceId);
            _evidenceDetailsText.text = selectedEvidence == null
                ? "Secili delil bilgisi yuklenemedi."
                : selectedEvidence.Title + "\n\n" + selectedEvidence.Description + "\n\nKategori: " + selectedEvidence.Category;
        }

        private void RebuildInterviews(CaseSessionManager session)
        {
            RuntimeUiFactory.ClearChildren(_interviewContent);
            if (session.ConversationHistory.Count == 0)
            {
                AddSectionCard(_interviewContent, "NPC Sorgulari", "Henuz sorgu kaydi yok. Delil topladikca yeni diyaloglar acilacak.");
                return;
            }

            for (var i = session.ConversationHistory.Count - 1; i >= 0; i--)
            {
                AddSectionCard(_interviewContent, "Kayit " + (session.ConversationHistory.Count - i), session.ConversationHistory[i]);
            }
        }

        private void RebuildNotes(CaseSessionManager session)
        {
            RuntimeUiFactory.ClearChildren(_notesListContent);
            if (session.TeamNotes.Count == 0)
            {
                AddSectionCard(_notesListContent, "Takim Notlari", "Henuz takim notu yok.");
                return;
            }

            for (var i = session.TeamNotes.Count - 1; i >= 0; i--)
            {
                AddSectionCard(_notesListContent, "Not " + (session.TeamNotes.Count - i), session.TeamNotes[i]);
            }
        }

        private void RebuildSuspects(CaseSessionManager session)
        {
            RuntimeUiFactory.ClearChildren(_suspectsContent);

            if (session.HasAnyAccusableSuspect())
            {
                CreateFinalDecisionCard(session);
            }

            foreach (var suspect in session.ActiveCase.Suspects)
            {
                if (suspect == null)
                {
                    continue;
                }

                var card = RuntimeUiFactory.CreateCard("SuspectCard" + suspect.Id, _suspectsContent, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
                RuntimeUiFactory.EnsureLayoutElement(card, flexibleWidth: 1f, preferredHeight: 292f);
                RuntimeUiFactory.AddVerticalLayout(card, 8f, new RectOffset(16, 16, 16, 14));
                RuntimeUiFactory.CreateText("Name", card, suspect.DisplayName, 18, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
                CreateStatusChip(card, session.CanAccuse(suspect.Id) ? "SUCLAMA HAZIR" : "EK DELIL GEREKIYOR", session.CanAccuse(suspect.Id) ? ModernGuiTheme.AccentWarmColor : ModernGuiTheme.BorderColor);
                RuntimeUiFactory.CreateText("Summary", card, suspect.Summary, 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);

                var matchCount = session.GetSuspectEvidenceMatchCount(suspect);
                var confidence = session.GetSuspectConfidencePercent(suspect.Id);
                RuntimeUiFactory.CreateText("EvidenceMatch", card, $"Eslesen delil: {matchCount}/{suspect.RequiredEvidenceIds.Count}", 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
                RuntimeUiFactory.CreateText("Confidence", card, $"Suphe yogunlugu: %{confidence}", 14, ModernGuiTheme.MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft);
                CreateProgressBar(card, confidence / 100f, $"Guven %{confidence}");
                CreateEvidenceRequirementList(card, suspect, session);
                RuntimeUiFactory.CreateText("Missing", card, session.GetMissingEvidenceSummary(suspect), 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);

                var accuseButton = RuntimeUiFactory.CreateButton(
                    "AccuseButton" + suspect.Id,
                    card,
                    session.CanAccuse(suspect.Id) && !session.IsCaseResolved ? "Secili zincirle sucla" : "Daha fazla delil gerekiyor",
                    session.CanAccuse(suspect.Id) && !session.IsCaseResolved && HasFinalDecisionSelections()
                        ? new Color(0.24f, 0.18f, 0.08f, 1f)
                        : new Color(0.11f, 0.13f, 0.15f, 1f),
                    15);
                RuntimeUiFactory.EnsureLayoutElement(accuseButton.transform, preferredHeight: 46f);
                accuseButton.interactable = session.CanAccuse(suspect.Id) && !session.IsCaseResolved && HasFinalDecisionSelections();
                var capturedSuspectId = suspect.Id;
                accuseButton.onClick.AddListener(() => TryAccuse(capturedSuspectId));
            }
        }

        private void CreateFinalDecisionCard(CaseSessionManager session)
        {
            var card = RuntimeUiFactory.CreateCard("FinalDecisionCard", _suspectsContent, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentColor);
            RuntimeUiFactory.EnsureLayoutElement(card, flexibleWidth: 1f, preferredHeight: UseMobileNotebookLayout() ? 720f : 640f);
            RuntimeUiFactory.AddVerticalLayout(card, 10f, new RectOffset(16, 16, 16, 14));
            RuntimeUiFactory.CreateText("FinalTitle", card, "FINAL KARAR ZINCIRI", 18, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            RuntimeUiFactory.CreateText(
                "FinalHint",
                card,
                "Suclama icin supheliyi, motivasyonu ve olay siralamasini birlikte dogrula.",
                14,
                ModernGuiTheme.MutedTextColor,
                FontStyle.Normal,
                TextAnchor.UpperLeft);

            CreateFinalChoiceGroup(
                card,
                "Motivasyon",
                session.ActiveCase.MotiveOptions,
                session.ActiveCase.CulpritMotive,
                _selectedMotive,
                value =>
                {
                    _selectedMotive = value;
                    RefreshImmediate();
                });

            CreateFinalChoiceGroup(
                card,
                "Olay Sirasi",
                session.ActiveCase.TimelineOptions,
                session.ActiveCase.CulpritTimeline,
                _selectedTimeline,
                value =>
                {
                    _selectedTimeline = value;
                    RefreshImmediate();
                });
        }

        private void CreateFinalChoiceGroup(
            Transform parent,
            string title,
            IReadOnlyList<string> configuredOptions,
            string fallbackOption,
            string selectedValue,
            Action<string> onSelected)
        {
            RuntimeUiFactory.CreateText(title + "Label", parent, title.ToUpperInvariant(), 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var options = BuildChoiceOptions(configuredOptions, fallbackOption);
            var host = RuntimeUiFactory.CreateUiRoot(title + "Options", parent);
            RuntimeUiFactory.EnsureLayoutElement(host, preferredHeight: Mathf.Max(72f, options.Count * 74f));
            RuntimeUiFactory.AddVerticalLayout(host, 6f, new RectOffset(0, 0, 0, 0));

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var selected = string.Equals(option, selectedValue, StringComparison.Ordinal);
                var button = RuntimeUiFactory.CreateButton(
                    title + "Option" + i,
                    host,
                    option,
                    selected ? new Color(0.24f, 0.18f, 0.08f, 1f) : new Color(0.1f, 0.12f, 0.15f, 1f),
                    13);
                RuntimeUiFactory.EnsureLayoutElement(button.transform, flexibleWidth: 1f, preferredHeight: 68f);
                var capturedOption = option;
                button.onClick.AddListener(() => onSelected(capturedOption));
            }
        }

        private static List<string> BuildChoiceOptions(IReadOnlyList<string> configuredOptions, string fallbackOption)
        {
            var options = new List<string>();
            if (configuredOptions != null)
            {
                for (var i = 0; i < configuredOptions.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(configuredOptions[i]))
                    {
                        options.Add(configuredOptions[i]);
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

        private void AddSectionCard(Transform parent, string title, string body)
        {
            var card = RuntimeUiFactory.CreateCard(title + "Card", parent, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentColor);
            var mobileLayout = UseMobileNotebookLayout();
            RuntimeUiFactory.EnsureLayoutElement(
                card,
                flexibleWidth: 1f,
                preferredHeight: Mathf.Clamp(
                    (mobileLayout ? 104f : 82f) + (string.IsNullOrWhiteSpace(body) ? 0f : body.Length * (mobileLayout ? 0.34f : 0.22f)),
                    mobileLayout ? 122f : 96f,
                    mobileLayout ? 240f : 150f));
            RuntimeUiFactory.AddVerticalLayout(card, mobileLayout ? 10f : 8f, new RectOffset(16, 16, 16, 14));
            RuntimeUiFactory.CreateText(title + "Title", card, title, mobileLayout ? 18 : 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            RuntimeUiFactory.CreateText(title + "Body", card, body, mobileLayout ? 16 : 14, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        private void CreateStatusChip(Transform parent, string label, Color accent)
        {
            var chip = RuntimeUiFactory.CreateCard("Chip" + label, parent, new Color(0.08f, 0.1f, 0.12f, 0.96f), accent);
            RuntimeUiFactory.EnsureLayoutElement(chip, preferredWidth: 206f, preferredHeight: 38f);
            RuntimeUiFactory.AddVerticalLayout(chip, 0f, new RectOffset(12, 12, 10, 8));
            RuntimeUiFactory.CreateText("ChipLabel", chip, label, 12, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        private void CreateEvidenceRequirementList(Transform parent, SuspectData suspect, CaseSessionManager session)
        {
            var host = RuntimeUiFactory.CreateUiRoot("RequirementHost", parent);
            RuntimeUiFactory.AddHorizontalLayout(host, 8f, new RectOffset(0, 0, 0, 0), true).childForceExpandWidth = false;

            for (var i = 0; i < suspect.RequiredEvidenceIds.Count; i++)
            {
                var evidenceId = suspect.RequiredEvidenceIds[i];
                var evidence = session.GetEvidence(evidenceId);
                var title = evidence != null ? evidence.Title : evidenceId;
                var hasEvidence = session.HasEvidence(evidenceId);

                var chip = RuntimeUiFactory.CreateCard(
                    "EvidenceChip" + i,
                    host,
                    hasEvidence ? new Color(0.11f, 0.18f, 0.16f, 0.96f) : new Color(0.11f, 0.12f, 0.15f, 0.96f),
                    hasEvidence ? ModernGuiTheme.AccentWarmColor : ModernGuiTheme.BorderColor);
                RuntimeUiFactory.EnsureLayoutElement(chip, preferredHeight: 34f, preferredWidth: Mathf.Clamp(title.Length * 8f, 120f, 220f));
                RuntimeUiFactory.AddVerticalLayout(chip, 0f, new RectOffset(10, 10, 8, 6));
                RuntimeUiFactory.CreateText("ChipTitle", chip, title, 11, hasEvidence ? ModernGuiTheme.TextColor : ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        private void CreateProgressBar(Transform parent, float normalized, string label)
        {
            var shell = RuntimeUiFactory.CreateUiRoot("ProgressShell", parent);
            RuntimeUiFactory.EnsureLayoutElement(shell, preferredHeight: 24f);
            var background = RuntimeUiFactory.AddImage(shell.gameObject, new Color(0.08f, 0.1f, 0.12f, 1f));
            background.raycastTarget = false;
            RuntimeUiFactory.AddOutline(shell.gameObject, new Color(0f, 0f, 0f, 0.4f), new Vector2(1f, -1f));

            var fill = RuntimeUiFactory.CreateUiRoot("Fill", shell);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            RuntimeUiFactory.AddImage(fill.gameObject, new Color(0.18f, 0.7f, 0.44f, 1f)).raycastTarget = false;

            var labelText = RuntimeUiFactory.CreateText("Label", shell, label, 13, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            labelText.raycastTarget = false;
        }
        private void HandleNoteChanged(string value)
        {
            if (_syncingNoteField)
            {
                return;
            }

            if (value.Length > 140)
            {
                _syncingNoteField = true;
                _noteInput.text = value.Substring(0, 140);
                _syncingNoteField = false;
            }

            _noteCounterText.text = $"{_noteInput.text.Length}/140";
        }

        private void SubmitTeamNote()
        {
            var trimmedNote = _noteInput.text.Trim();
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

            if (!added)
            {
                return;
            }

            _syncingNoteField = true;
            _noteInput.text = string.Empty;
            _syncingNoteField = false;
            _noteCounterText.text = "0/140";
            RefreshImmediate();
        }

        private void ClearNoteDraft()
        {
            _syncingNoteField = true;
            _noteInput.text = string.Empty;
            _syncingNoteField = false;
            _noteCounterText.text = "0/140";
        }

        private void TryAccuse(string suspectId)
        {
            var session = CaseSessionManager.Instance;
            if (session == null)
            {
                return;
            }

            if (!HasFinalDecisionSelections())
            {
                session.PublishMessage("Final suclama icin once motivasyon ve olay sirasi sec.");
                RefreshImmediate();
                return;
            }

            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState != null && networkCaseState.IsOnlineSessionActive)
            {
                networkCaseState.RequestResolveSuspect(suspectId, _selectedMotive, _selectedTimeline);
            }
            else
            {
                session.TryResolveCase(suspectId, _selectedMotive, _selectedTimeline, out _);
            }

            RefreshImmediate();
        }

        private void RefreshTabLabels(CaseSessionManager session)
        {
            if (_tabButtonTexts == null || _tabButtonTexts.Length < 5)
            {
                return;
            }

            _tabButtonTexts[0].text = _tabBaseLabels[0];
            _tabButtonTexts[1].text = $"{_tabBaseLabels[1]} ({session.CollectedEvidenceIds.Count})";
            _tabButtonTexts[2].text = $"{_tabBaseLabels[2]} ({session.ConversationHistory.Count})";
            _tabButtonTexts[3].text = $"{_tabBaseLabels[3]} ({session.TeamNotes.Count})";
            _tabButtonTexts[4].text = _tabBaseLabels[4];

            if (_mobileTabButtonTexts == null || _mobileTabButtonTexts.Length < 5)
            {
                return;
            }

            _mobileTabButtonTexts[0].text = "Genel";
            _mobileTabButtonTexts[1].text = $"Delil {session.CollectedEvidenceIds.Count}";
            _mobileTabButtonTexts[2].text = $"Sorgu {session.ConversationHistory.Count}";
            _mobileTabButtonTexts[3].text = $"Not {session.TeamNotes.Count}";
            _mobileTabButtonTexts[4].text = "Supheli";
        }

        private void AnimateNotebook(bool visible)
        {
            if (_overlayGroup == null || _panelRoot == null)
            {
                return;
            }

            var target = visible ? 1f : 0f;
            var deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 1f / 60f;
            _openBlend = Mathf.MoveTowards(_openBlend, target, deltaTime * 6f);
            _overlayGroup.alpha = _openBlend;
            _overlayGroup.interactable = _openBlend > 0.98f;
            _overlayGroup.blocksRaycasts = _openBlend > 0.02f;
            _panelRoot.localScale = Vector3.Lerp(new Vector3(0.95f, 0.98f, 1f), Vector3.one, _openBlend);
            _panelRoot.anchoredPosition = Vector2.Lerp(new Vector2(0f, 18f), Vector2.zero, _openBlend);
        }

        private void CreateAmbientNote(string name, Vector2 anchor, Vector2 position, Vector2 size, string title, string body, Color panelColor, Color accentColor, Vector2 motionAmplitude, float speed, float phase)
        {
            var card = RuntimeUiFactory.CreateCard(name, _panelRoot, panelColor, accentColor);
            card.anchorMin = anchor;
            card.anchorMax = anchor;
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = position;
            card.sizeDelta = size;
            RuntimeUiFactory.AddVerticalLayout(card, 3f, new RectOffset(14, 14, 16, 12));
            RuntimeUiFactory.CreateText("Title", card, title, 11, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            RuntimeUiFactory.CreateText("Body", card, body, 13, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            AddFloatMotion(card, card.GetComponent<Image>(), motionAmplitude, speed, 0.05f, 0.02f, phase);
        }

        private static void AddFloatMotion(RectTransform target, Graphic graphic, Vector2 amplitude, float speed, float alphaPulse, float scalePulse, float phase)
        {
            var motion = target.gameObject.GetComponent<UiFloatMotion>();
            if (motion == null)
            {
                motion = target.gameObject.AddComponent<UiFloatMotion>();
            }

            motion.Configure(target, graphic, amplitude, speed, alphaPulse, scalePulse, phase);
        }

        private static string BuildDossierStatus(CaseSessionManager session)
        {
            if (session.IsCaseResolved)
            {
                return "DOSYA KAPATILDI";
            }

            if (session.HasAnyAccusableSuspect())
            {
                return "SUCLAMA ICIN DOSYA YETERLI";
            }

            return "KANIT ZINCIRI TOPLANIYOR";
        }

        private static string BuildDossierMeta(CaseSessionManager session)
        {
            return $"Kritik {session.CollectedCriticalEvidenceCount}/{session.TotalCriticalEvidenceCount}  |  Kayit {session.ConversationHistory.Count}  |  Not {session.TeamNotes.Count}";
        }

        private static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            var minutes = totalSeconds / 60;
            var remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }
    }
}





