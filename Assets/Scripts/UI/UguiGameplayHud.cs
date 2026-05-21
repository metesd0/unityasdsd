using MobilOfl.Case;
using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public class UguiGameplayHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractionController playerInteraction;
        [SerializeField] private CaseProgressTracker progressTracker;
        [SerializeField] private float messageDuration = 5f;
        [SerializeField] private float locationDuration = 2.4f;
        [SerializeField] private float dialogueDuration = 6.2f;
        [SerializeField] private float dialogueCharactersPerSecond = 52f;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _statusCard;
        private RectTransform _objectiveCard;
        private RectTransform _waypointCard;
        private Text _statusTitleText;
        private Text _statusBodyText;
        private Text _objectiveText;
        private RectTransform _messageCard;
        private Image _messageCardImage;
        private Image _messageAccentImage;
        private Text _messageText;
        private CanvasGroup _messageGroup;
        private Text _locationTitleText;
        private Text _locationSubtitleText;
        private CanvasGroup _locationGroup;
        private Text _waypointTitleText;
        private Text _waypointBodyText;
        private Text _waypointMetaText;
        private Image _progressFill;
        private Text _progressText;
        private Text _tacticText;
        private RectTransform _stepsContent;
        private RectTransform _checklistCard;
        private Image _noiseFill;
        private Image _alertFill;
        private Text _stealthStateText;
        private RectTransform _tensionCard;
        private RectTransform _dialogueRoot;
        private CanvasGroup _dialogueGroup;
        private Image _dialoguePanelImage;
        private Image _dialogueSpeakerImage;
        private Text _dialogueSpeakerText;
        private Text _dialogueBodyText;
        private Text _dialogueMetaText;
        private Image _dialogueSignalFill;
        private Image _staminaFill;
        private Text _staminaText;
        private Image _scanFill;
        private Text _scanText;
        private RectTransform _scanCard;
        private Image _flashlightFill;
        private Text _flashlightText;
        private RectTransform _flashlightCard;
        private string _dialogueSpeaker = string.Empty;
        private string _dialogueLine = string.Empty;
        private bool _dialogueRevealedLead;
        private float _dialogueStartedAt;
        private float _dialogueUntil;
        private float _messageUntil;
        private float _locationUntil;
        private string _currentMessage = string.Empty;
        private bool _currentMessageIsBlocker;
        private string _currentLocationTitle = string.Empty;
        private string _currentLocationSubtitle = string.Empty;
        private float _nextRefreshAt;
        private CaseSessionManager _subscribedSession;
        private bool _built;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            DisableLegacyHudScripts();
            TrySubscribe();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            if (_subscribedSession != null)
            {
                _subscribedSession.SessionMessagePublished -= HandleSessionMessage;
                _subscribedSession.EvidenceCollected -= HandleEvidenceCollected;
                _subscribedSession.NpcConversationRegistered -= HandleNpcConversation;
                _subscribedSession = null;
            }
        }

        private void Update()
        {
            BuildIfNeeded();
            DisableLegacyHudScripts();
            ResolveReferences();
            TrySubscribe();
            UpdateLocationBanner();

            if (_root != null)
            {
                var desktopHudVisible = !MainMenuHud.IsBlockingGameplay && !CaseNotebookHud.IsAnyNotebookOpen;
                _root.gameObject.SetActive(desktopHudVisible);
            }

            ApplyFocusedHudLayout();
            RefreshDialoguePanel();

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

            var canvasTransform = transform.Find("UguiGameplayCanvas") as RectTransform;
            if (canvasTransform == null)
            {
                canvasTransform = RuntimeUiFactory.CreateUiRoot("UguiGameplayCanvas", transform);
            }

            _canvas = canvasTransform.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 70;

            var scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = canvasTransform.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            RuntimeUiFactory.Stretch(canvasTransform);
            RuntimeUiFactory.ClearChildren(canvasTransform);

            _root = canvasTransform;
            BuildObjectiveCard();
            BuildMessageBanner();
            BuildLocationBanner();
            BuildWaypointCard();
            BuildDialoguePanel();
            BuildChecklistCard();
            BuildTensionCard();
            _built = true;
        }

        private void BuildStatusCard()
        {
            var card = RuntimeUiFactory.CreateCard("StatusCard", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentColor);
            _statusCard = card;
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(16f, -16f);
            card.sizeDelta = new Vector2(340f, 100f);
            RuntimeUiFactory.AddVerticalLayout(card, 3f, new RectOffset(16, 16, 14, 10));
            _statusTitleText = RuntimeUiFactory.CreateText("StatusTitle", card, "Vaka", 18, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _statusBodyText = RuntimeUiFactory.CreateText("StatusBody", card, string.Empty, 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
        }

        private void BuildObjectiveCard()
        {
            var card = RuntimeUiFactory.CreateCard("ObjectiveCard", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentWarmColor);
            _objectiveCard = card;
            card.anchorMin = new Vector2(0.5f, 1f);
            card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -16f);
            card.sizeDelta = new Vector2(680f, 72f);
            RuntimeUiFactory.AddVerticalLayout(card, 2f, new RectOffset(22, 22, 12, 10));
            RuntimeUiFactory.CreateText("ObjectiveLabel", card, "HEDEF", 13, ModernGuiTheme.AccentWarmColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _objectiveText = RuntimeUiFactory.CreateText("ObjectiveBody", card, string.Empty, 15, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _objectiveText.resizeTextForBestFit = true;
            _objectiveText.resizeTextMinSize = 12;
            _objectiveText.resizeTextMaxSize = 15;
        }

        private void BuildStaminaBar()
        {
            var track = RuntimeUiFactory.CreateUiRoot("StaminaTrack", _root);
            track.anchorMin = new Vector2(0.25f, 1f);
            track.anchorMax = new Vector2(0.75f, 1f);
            track.pivot = new Vector2(0.5f, 1f);
            track.anchoredPosition = new Vector2(0f, -8f);
            track.sizeDelta = new Vector2(0f, 10f);
            RuntimeUiFactory.AddImage(track.gameObject, ModernGuiTheme.StaminaTrackColor);
            RuntimeUiFactory.ApplyOneUiRounding(track.gameObject, 5f);

            _staminaFill = RuntimeUiFactory.CreateUiRoot("StaminaFill", track).gameObject.AddComponent<Image>();
            _staminaFill.color = ModernGuiTheme.StaminaColor;
            var fillRect = _staminaFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            RuntimeUiFactory.ApplyOneUiRounding(_staminaFill.gameObject, 5f);

            _staminaText = RuntimeUiFactory.CreateText("StaminaLabel", track, string.Empty, 11, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            RuntimeUiFactory.Stretch(_staminaText.rectTransform);
            _staminaText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
        }

        private void BuildScanCooldownCard()
        {
            _scanCard = RuntimeUiFactory.CreateCard("ScanCard", _root, new Color(0.06f, 0.08f, 0.12f, 0.85f), ModernGuiTheme.ScanReadyColor);
            _scanCard.anchorMin = new Vector2(0.5f, 1f);
            _scanCard.anchorMax = new Vector2(0.5f, 1f);
            _scanCard.pivot = new Vector2(0.5f, 1f);
            _scanCard.anchoredPosition = new Vector2(208f, -14f);
            _scanCard.sizeDelta = new Vector2(120f, 42f);

            RuntimeUiFactory.AddVerticalLayout(_scanCard, 2f, new RectOffset(10, 10, 6, 5));
            _scanText = RuntimeUiFactory.CreateText("ScanLabel", _scanCard, "TARA", 12, ModernGuiTheme.ScanReadyColor, FontStyle.Bold, TextAnchor.MiddleCenter);

            var scanTrack = RuntimeUiFactory.CreateUiRoot("ScanTrack", _scanCard);
            RuntimeUiFactory.EnsureLayoutElement(scanTrack, preferredHeight: 7f);
            RuntimeUiFactory.AddImage(scanTrack.gameObject, new Color(0.04f, 0.06f, 0.08f, 0.9f));
            RuntimeUiFactory.ApplyOneUiRounding(scanTrack.gameObject, 3f);

            _scanFill = RuntimeUiFactory.CreateUiRoot("ScanFill", scanTrack).gameObject.AddComponent<Image>();
            _scanFill.color = ModernGuiTheme.ScanReadyColor;
            var scanFillRect = _scanFill.rectTransform;
            scanFillRect.anchorMin = new Vector2(0f, 0f);
            scanFillRect.anchorMax = new Vector2(1f, 1f);
            scanFillRect.offsetMin = Vector2.zero;
            scanFillRect.offsetMax = Vector2.zero;
            RuntimeUiFactory.ApplyOneUiRounding(_scanFill.gameObject, 3f);
        }

        private void BuildFlashlightCard()
        {
            _flashlightCard = RuntimeUiFactory.CreateCard("FlashlightCard", _root, new Color(0.06f, 0.08f, 0.1f, 0.82f), ModernGuiTheme.FlashlightUIColor);
            _flashlightCard.anchorMin = new Vector2(0.5f, 1f);
            _flashlightCard.anchorMax = new Vector2(0.5f, 1f);
            _flashlightCard.pivot = new Vector2(0.5f, 1f);
            _flashlightCard.anchoredPosition = new Vector2(-208f, -14f);
            _flashlightCard.sizeDelta = new Vector2(120f, 42f);

            RuntimeUiFactory.AddVerticalLayout(_flashlightCard, 2f, new RectOffset(10, 10, 6, 5));
            _flashlightText = RuntimeUiFactory.CreateText("FlashlightLabel", _flashlightCard, "FENER", 12, ModernGuiTheme.FlashlightUIColor, FontStyle.Bold, TextAnchor.MiddleCenter);

            var flashTrack = RuntimeUiFactory.CreateUiRoot("FlashTrack", _flashlightCard);
            RuntimeUiFactory.EnsureLayoutElement(flashTrack, preferredHeight: 7f);
            RuntimeUiFactory.AddImage(flashTrack.gameObject, new Color(0.04f, 0.06f, 0.08f, 0.9f));
            RuntimeUiFactory.ApplyOneUiRounding(flashTrack.gameObject, 3f);

            _flashlightFill = RuntimeUiFactory.CreateUiRoot("FlashFill", flashTrack).gameObject.AddComponent<Image>();
            _flashlightFill.color = ModernGuiTheme.FlashlightUIColor;
            var flashFillRect = _flashlightFill.rectTransform;
            flashFillRect.anchorMin = new Vector2(0f, 0f);
            flashFillRect.anchorMax = new Vector2(1f, 1f);
            flashFillRect.offsetMin = Vector2.zero;
            flashFillRect.offsetMax = Vector2.zero;
            RuntimeUiFactory.ApplyOneUiRounding(_flashlightFill.gameObject, 3f);
        }

        private void BuildMessageBanner()
        {
            var card = RuntimeUiFactory.CreateCard("MessageBanner", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentColor);
            card.anchorMin = new Vector2(0.5f, 1f);
            card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -116f);
            card.sizeDelta = new Vector2(620f, 72f);
            RuntimeUiFactory.AddVerticalLayout(card, 0f, new RectOffset(22, 22, 14, 12));
            _messageCard = card;
            _messageCardImage = card.GetComponent<Image>();
            var accent = card.Find("Accent");
            _messageAccentImage = accent != null ? accent.GetComponent<Image>() : null;
            _messageText = RuntimeUiFactory.CreateText("MessageText", card, string.Empty, 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _messageText.resizeTextForBestFit = true;
            _messageText.resizeTextMinSize = 12;
            _messageText.resizeTextMaxSize = 18;
            _messageGroup = card.gameObject.GetComponent<CanvasGroup>();
            if (_messageGroup == null)
            {
                _messageGroup = card.gameObject.AddComponent<CanvasGroup>();
            }

            _messageGroup.alpha = 0f;
            _messageGroup.blocksRaycasts = false;
            _messageGroup.interactable = false;
            card.gameObject.SetActive(true);
        }

        private void BuildLocationBanner()
        {
            var card = RuntimeUiFactory.CreateCard("LocationBanner", _root, ModernGuiTheme.PanelSoftColor, ModernGuiTheme.AccentWarmColor);
            card.anchorMin = new Vector2(1f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(1f, 1f);
            card.anchoredPosition = new Vector2(-18f, -18f);
            card.sizeDelta = new Vector2(300f, 54f);
            RuntimeUiFactory.AddVerticalLayout(card, 1f, new RectOffset(14, 14, 9, 8));
            _locationTitleText = RuntimeUiFactory.CreateText("LocationTitle", card, string.Empty, 14, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            _locationSubtitleText = RuntimeUiFactory.CreateText("LocationSubtitle", card, string.Empty, 11, ModernGuiTheme.MutedTextColor, FontStyle.Normal, TextAnchor.MiddleLeft);
            _locationGroup = card.gameObject.GetComponent<CanvasGroup>();
            if (_locationGroup == null)
            {
                _locationGroup = card.gameObject.AddComponent<CanvasGroup>();
            }

            _locationGroup.alpha = 0f;
            _locationGroup.blocksRaycasts = false;
            _locationGroup.interactable = false;
        }

        private void BuildWaypointCard()
        {
            var card = RuntimeUiFactory.CreateCard("WaypointCard", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentWarmColor);
            _waypointCard = card;
            card.anchorMin = new Vector2(0.5f, 0f);
            card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2(0f, 22f);
            card.sizeDelta = new Vector2(560f, 78f);
            RuntimeUiFactory.AddVerticalLayout(card, 2f, new RectOffset(18, 18, 12, 10));
            _waypointTitleText = RuntimeUiFactory.CreateText("WaypointTitle", card, string.Empty, 14, ModernGuiTheme.AccentColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _waypointBodyText = RuntimeUiFactory.CreateText("WaypointBody", card, string.Empty, 13, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _waypointMetaText = RuntimeUiFactory.CreateText("WaypointMeta", card, string.Empty, 11, ModernGuiTheme.MutedTextColor, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private void BuildDialoguePanel()
        {
            _dialogueRoot = RuntimeUiFactory.CreateCard("DialoguePanel", _root, new Color(0.045f, 0.06f, 0.09f, 0.96f), ModernGuiTheme.AccentColor);
            _dialoguePanelImage = _dialogueRoot.GetComponent<Image>();
            _dialogueRoot.anchorMin = new Vector2(0.5f, 0f);
            _dialogueRoot.anchorMax = new Vector2(0.5f, 0f);
            _dialogueRoot.pivot = new Vector2(0.5f, 0f);
            _dialogueRoot.anchoredPosition = new Vector2(0f, 104f);
            _dialogueRoot.sizeDelta = new Vector2(940f, 140f);

            var layout = RuntimeUiFactory.AddHorizontalLayout(_dialogueRoot, 18f, new RectOffset(20, 20, 18, 16), true);
            layout.childForceExpandWidth = false;

            var portrait = RuntimeUiFactory.CreateCard("SpeakerChip", _dialogueRoot, new Color(0.08f, 0.13f, 0.17f, 1f), ModernGuiTheme.AccentWarmColor);
            _dialogueSpeakerImage = portrait.GetComponent<Image>();
            RuntimeUiFactory.EnsureLayoutElement(portrait, preferredWidth: 84f, preferredHeight: 108f);
            RuntimeUiFactory.AddVerticalLayout(portrait, 6f, new RectOffset(14, 14, 14, 12));
            RuntimeUiFactory.CreateIcon("SpeakerIcon", portrait, "users", ModernGuiTheme.AccentColor, new Vector2(38f, 38f));
            RuntimeUiFactory.CreateText("SpeakerRole", portrait, "NPC", 13, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);

            var content = RuntimeUiFactory.CreateUiRoot("DialogueContent", _dialogueRoot);
            RuntimeUiFactory.EnsureLayoutElement(content, flexibleWidth: 1f, preferredHeight: 108f);
            RuntimeUiFactory.AddVerticalLayout(content, 5f, new RectOffset(0, 0, 0, 0));

            var header = RuntimeUiFactory.CreateUiRoot("DialogueHeader", content);
            RuntimeUiFactory.EnsureLayoutElement(header, preferredHeight: 24f);
            var headerLayout = RuntimeUiFactory.AddHorizontalLayout(header, 10f, new RectOffset(0, 0, 0, 0), true);
            headerLayout.childForceExpandWidth = false;

            _dialogueSpeakerText = RuntimeUiFactory.CreateText("DialogueSpeaker", header, string.Empty, 17, ModernGuiTheme.AccentWarmColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            RuntimeUiFactory.EnsureLayoutElement(_dialogueSpeakerText.transform, flexibleWidth: 1f);
            _dialogueMetaText = RuntimeUiFactory.CreateText("DialogueMeta", header, "SORGULAMA", 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.MiddleRight);
            RuntimeUiFactory.EnsureLayoutElement(_dialogueMetaText.transform, preferredWidth: 150f);

            _dialogueBodyText = RuntimeUiFactory.CreateText("DialogueBody", content, string.Empty, 19, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            _dialogueBodyText.resizeTextForBestFit = true;
            _dialogueBodyText.resizeTextMinSize = 15;
            _dialogueBodyText.resizeTextMaxSize = 19;
            RuntimeUiFactory.EnsureLayoutElement(_dialogueBodyText.transform, flexibleWidth: 1f, preferredHeight: 52f);

            var signalTrack = RuntimeUiFactory.CreateUiRoot("DialogueSignalTrack", content);
            RuntimeUiFactory.EnsureLayoutElement(signalTrack, preferredHeight: 8f);
            RuntimeUiFactory.AddImage(signalTrack.gameObject, new Color(0.08f, 0.11f, 0.15f, 1f));
            RuntimeUiFactory.ApplyOneUiRounding(signalTrack.gameObject, 4f);
            _dialogueSignalFill = RuntimeUiFactory.CreateUiRoot("DialogueSignalFill", signalTrack).gameObject.AddComponent<Image>();
            _dialogueSignalFill.color = ModernGuiTheme.AccentColor;
            var fillRect = _dialogueSignalFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            _dialogueGroup = _dialogueRoot.gameObject.GetComponent<CanvasGroup>();
            if (_dialogueGroup == null)
            {
                _dialogueGroup = _dialogueRoot.gameObject.AddComponent<CanvasGroup>();
            }

            _dialogueGroup.alpha = 0f;
            _dialogueGroup.blocksRaycasts = false;
            _dialogueGroup.interactable = false;
        }

        private void BuildChecklistCard()
        {
            var card = RuntimeUiFactory.CreateCard("ChecklistCard", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentWarmColor);
            _checklistCard = card;
            card.anchorMin = new Vector2(0f, 0f);
            card.anchorMax = new Vector2(0f, 0f);
            card.pivot = new Vector2(0f, 0f);
            card.anchoredPosition = new Vector2(18f, 18f);
            card.sizeDelta = new Vector2(320f, 140f);
            RuntimeUiFactory.AddVerticalLayout(card, 5f, new RectOffset(16, 16, 16, 12));
            RuntimeUiFactory.CreateText("ChecklistLabel", card, "ILERLEME", 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var progressShell = RuntimeUiFactory.CreateUiRoot("ProgressShell", card);
            var progressLayout = RuntimeUiFactory.EnsureLayoutElement(progressShell, flexibleWidth: 1f, preferredHeight: 38f);
            progressLayout.minHeight = 38f;

            var progressTrack = RuntimeUiFactory.CreateUiRoot("ProgressTrack", progressShell);
            progressTrack.anchorMin = new Vector2(0f, 0.5f);
            progressTrack.anchorMax = new Vector2(1f, 0.5f);
            progressTrack.pivot = new Vector2(0.5f, 0.5f);
            progressTrack.offsetMin = new Vector2(0f, -6f);
            progressTrack.offsetMax = new Vector2(0f, 6f);
            RuntimeUiFactory.AddImage(progressTrack.gameObject, new Color(0.08f, 0.11f, 0.15f, 0.95f));
            RuntimeUiFactory.ApplyOneUiRounding(progressTrack.gameObject, 6f);
            _progressFill = RuntimeUiFactory.CreateUiRoot("Fill", progressTrack).gameObject.AddComponent<Image>();
            var fillRect = _progressFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            _progressFill.color = ModernGuiTheme.AccentColor;
            RuntimeUiFactory.ApplyOneUiRounding(_progressFill.gameObject, 6f);
            _progressText = RuntimeUiFactory.CreateText("ProgressText", progressShell, string.Empty, 13, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            RuntimeUiFactory.Stretch(_progressText.rectTransform);

            _tacticText = RuntimeUiFactory.CreateText("TacticText", card, string.Empty, 13, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.UpperLeft);
            var scroll = RuntimeUiFactory.CreateScrollView("StepsScroll", card, out _stepsContent);
            RuntimeUiFactory.EnsureLayoutElement(scroll.transform, flexibleHeight: 1f, preferredHeight: 32f);
            RuntimeUiFactory.AddVerticalLayout(_stepsContent, 5f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.AddContentSizeFitter(_stepsContent, ContentSizeFitter.FitMode.PreferredSize);
        }

        private void BuildTensionCard()
        {
            var card = RuntimeUiFactory.CreateCard("TensionCard", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentColor);
            _tensionCard = card;
            card.anchorMin = new Vector2(1f, 0f);
            card.anchorMax = new Vector2(1f, 0f);
            card.pivot = new Vector2(1f, 0f);
            card.anchoredPosition = new Vector2(-18f, 18f);
            card.sizeDelta = new Vector2(320f, 128f);
            RuntimeUiFactory.AddVerticalLayout(card, 5f, new RectOffset(16, 16, 16, 12));
            RuntimeUiFactory.CreateText("TensionLabel", card, "GIZLILIK", 16, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _stealthStateText = RuntimeUiFactory.CreateText("TensionState", card, string.Empty, 13, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _noiseFill = CreateMeter(card, "Gurultu");
            _alertFill = CreateMeter(card, "Dikkat");
        }

        private void ApplyFocusedHudLayout()
        {
            if (_statusCard != null)
            {
                _statusCard.gameObject.SetActive(false);
            }

            if (_staminaFill != null)
            {
                _staminaFill.transform.parent.gameObject.SetActive(false);
            }

            if (_scanCard != null)
            {
                _scanCard.gameObject.SetActive(false);
            }

            if (_flashlightCard != null)
            {
                _flashlightCard.gameObject.SetActive(false);
            }

            if (_waypointCard != null)
            {
                _waypointCard.gameObject.SetActive(true);
            }

            if (_checklistCard != null)
            {
                _checklistCard.gameObject.SetActive(false);
            }

            if (_tensionCard != null)
            {
                _tensionCard.gameObject.SetActive(false);
            }
        }

        private void RefreshImmediate()
        {
            RefreshMessageBanner();
            RefreshLocationVisuals();

            var session = CaseSessionManager.Instance;
            if (session == null || session.ActiveCase == null)
            {
                if (_statusTitleText != null)
                {
                    _statusTitleText.text = "Vaka";
                }

                if (_statusBodyText != null)
                {
                    _statusBodyText.text = "Vaka bilgisi yukleniyor.";
                }

                if (_objectiveText != null)
                {
                    _objectiveText.text = "Ilk delili bulmak icin koridoru tara.";
                }

                return;
            }

            RefreshStatus(session);
            RefreshObjective(session);
            RefreshWaypoint(session);
            RefreshChecklist(session);
            RefreshTension();
        }

        private void RefreshStatus(CaseSessionManager session)
        {
            if (_statusTitleText == null || _statusBodyText == null)
            {
                return;
            }

            _statusTitleText.text = session.ActiveCase.CaseTitle;
            var zone = playerInteraction != null
                ? SchoolLocationUtility.GetZoneTitle(playerInteraction.transform.position)
                : "Bolge yok";
            var networkLine = string.Empty;
            var bootstrap = Object.FindAnyObjectByType<RelayNetworkBootstrap>();
            if (bootstrap != null && !string.IsNullOrWhiteSpace(bootstrap.CurrentStatus))
            {
                networkLine = string.IsNullOrWhiteSpace(bootstrap.CurrentJoinCode)
                    ? $" | {bootstrap.CurrentStatus}"
                    : $" | {bootstrap.CurrentStatus} [{bootstrap.CurrentJoinCode}]";
            }

            _statusBodyText.text =
                $"{zone.ToUpperInvariant()} | {FormatElapsedTime(session.ElapsedCaseTimeSeconds)}\n" +
                $"Delil {session.CollectedEvidenceIds.Count}/{session.ActiveCase.EvidenceItems.Count}  Kritik {session.CollectedCriticalEvidenceCount}/{session.TotalCriticalEvidenceCount}{networkLine}";
        }

        private void RefreshObjective(CaseSessionManager session)
        {
            var stealth = Object.FindAnyObjectByType<PlayerStealthController>();
            if (stealth != null && stealth.IsHighAlert)
            {
                _objectiveText.text = "Dikkat yuksek. Sakinlesme noktasina cekil.";
                return;
            }

            _objectiveText.text = TrimHudLine(session.GetRecommendedNextStep(), 118);
        }

        private void RefreshWaypoint(CaseSessionManager session)
        {
            if (playerInteraction == null || session.IsCaseResolved)
            {
                _waypointTitleText.text = "HEDEF YOK";
                _waypointBodyText.text = "Vaka tamamlandi ya da oyuncu referansi eksik.";
                _waypointMetaText.text = string.Empty;
                return;
            }

            if (!TryGetNextTarget(session, out var title, out var worldPosition, out var subtitle))
            {
                _waypointTitleText.text = "HEDEF YOK";
                _waypointBodyText.text = string.Empty;
                _waypointMetaText.text = string.Empty;
                return;
            }

            worldPosition = SchoolLocationUtility.PrototypeToWorldPosition(worldPosition, playerInteraction.transform.position);
            var directionHint = GetDirectionHint(worldPosition);
            var distance = Vector3.Distance(playerInteraction.transform.position, worldPosition);
            _waypointTitleText.text = "YONERGE";
            _waypointBodyText.text = title + " - " + TrimHudLine(subtitle, 86);
            _waypointMetaText.text = $"{directionHint}  |  {distance:0}m";
        }

        private void RefreshChecklist(CaseSessionManager session)
        {
            if (progressTracker == null || _progressFill == null || _progressText == null || _stepsContent == null)
            {
                return;
            }

            var ratio = progressTracker.GetCompletionRatio();
            var fillRect = _progressFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            _progressText.text = $"{Mathf.RoundToInt(ratio * 100f)}% tamamlandi";
            _tacticText.text = "Taktik: " + session.GetRecommendedNextStep();

            RuntimeUiFactory.ClearChildren(_stepsContent);
            var steps = progressTracker.Steps;
            var shown = 0;
            for (var i = 0; i < steps.Count && shown < 1; i++)
            {
                if (steps[i].IsCompleted)
                {
                    continue;
                }

                DrawChecklistStep(i, steps[i].Label, false);
                shown++;
            }

            if (shown > 0)
            {
                return;
            }

            for (var i = Mathf.Max(0, steps.Count - 1); i < steps.Count && shown < 1; i++)
            {
                DrawChecklistStep(i, steps[i].Label, steps[i].IsCompleted);
                shown++;
            }
        }

        private void DrawChecklistStep(int index, string label, bool isCompleted)
        {
            var prefix = isCompleted ? "[x]" : "[ ]";
            var color = isCompleted ? ModernGuiTheme.AccentColor : ModernGuiTheme.TextColor;
            var card = RuntimeUiFactory.CreateCard("Step" + index, _stepsContent, new Color(0.09f, 0.11f, 0.14f, 0.96f), isCompleted ? ModernGuiTheme.AccentWarmColor : ModernGuiTheme.BorderColor);
            RuntimeUiFactory.EnsureLayoutElement(card, preferredHeight: 36f);
            RuntimeUiFactory.AddVerticalLayout(card, 0f, new RectOffset(12, 12, 9, 7));
            RuntimeUiFactory.CreateText("StepText", card, prefix + " " + label, 13, color, isCompleted ? FontStyle.Bold : FontStyle.Normal, TextAnchor.UpperLeft);
        }

        private void RefreshTension()
        {
            var stealth = Object.FindAnyObjectByType<PlayerStealthController>();
            if (stealth == null)
            {
                if (_stealthStateText != null)
                {
                    _stealthStateText.text = "Gizlilik verisi bekleniyor.";
                }

                SetMeterFill(_noiseFill, 0f, new Color(0.28f, 0.78f, 0.82f, 1f));
                SetMeterFill(_alertFill, 0f, new Color(0.88f, 0.44f, 0.24f, 1f));
                return;
            }

            _stealthStateText.text = $"Profil: {stealth.MovementProfile}";
            SetMeterFill(_noiseFill, stealth.NoiseLevel01, new Color(0.24f, 0.86f, 0.9f, 1f));
            SetMeterFill(_alertFill, stealth.AlertLevel01, stealth.IsHighAlert ? new Color(0.96f, 0.3f, 0.22f, 1f) : new Color(0.9f, 0.58f, 0.24f, 1f));
        }

        private void RefreshMessageBanner()
        {
            if (_messageGroup == null)
            {
                return;
            }

            var timeLeft = _messageUntil - Time.time;
            if (timeLeft <= 0f || string.IsNullOrWhiteSpace(_currentMessage))
            {
                _messageGroup.alpha = 0f;
                return;
            }

            var alpha = Mathf.Clamp01(Mathf.Min(1f, timeLeft / 0.35f));
            var punch = _currentMessageIsBlocker ? Mathf.PingPong(Time.unscaledTime * 2.6f, 1f) : 0f;
            _messageGroup.alpha = alpha;
            if (_messageCard != null)
            {
                _messageCard.sizeDelta = Vector2.Lerp(
                    _messageCard.sizeDelta,
                    _currentMessageIsBlocker ? new Vector2(720f, 82f) : new Vector2(620f, 68f),
                    Time.unscaledDeltaTime * 10f);
            }

            if (_messageCardImage != null)
            {
                var targetColor = _currentMessageIsBlocker
                    ? new Color(0.12f, 0.085f, 0.035f, 0.96f)
                    : ModernGuiTheme.PanelColor;
                _messageCardImage.color = Color.Lerp(_messageCardImage.color, targetColor, Time.unscaledDeltaTime * 12f);
            }

            if (_messageAccentImage != null)
            {
                var targetAccent = _currentMessageIsBlocker
                    ? Color.Lerp(ModernGuiTheme.WarningColor, ModernGuiTheme.AccentWarmColor, punch)
                    : ModernGuiTheme.AccentColor;
                _messageAccentImage.color = Color.Lerp(_messageAccentImage.color, targetAccent, Time.unscaledDeltaTime * 14f);
            }

            _messageText.color = _currentMessageIsBlocker ? ModernGuiTheme.TextColor : ModernGuiTheme.MutedTextColor;
            _messageText.text = _currentMessage;
        }

        private void RefreshDialoguePanel()
        {
            if (_dialogueGroup == null || _dialogueRoot == null)
            {
                return;
            }

            var hasDialogue = !string.IsNullOrWhiteSpace(_dialogueLine) && Time.time < _dialogueUntil;
            var mobileHud = MobileInvestigationOverlay.IsMobileHudVisible;
            var hiddenPosition = new Vector2(0f, mobileHud ? 146f : 78f);
            var visiblePosition = new Vector2(0f, mobileHud ? 190f : 112f);
            var targetSize = new Vector2(mobileHud ? 760f : 900f, mobileHud ? 118f : 126f);
            _dialogueRoot.sizeDelta = Vector2.Lerp(_dialogueRoot.sizeDelta, targetSize, Time.unscaledDeltaTime * 9f);

            if (!hasDialogue)
            {
                _dialogueGroup.alpha = Mathf.MoveTowards(_dialogueGroup.alpha, 0f, Time.unscaledDeltaTime * 7f);
                _dialogueRoot.anchoredPosition = Vector2.Lerp(_dialogueRoot.anchoredPosition, hiddenPosition, Time.unscaledDeltaTime * 8f);
                return;
            }

            var elapsed = Mathf.Max(0f, Time.time - _dialogueStartedAt);
            var timeLeft = _dialogueUntil - Time.time;
            var intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.28f));
            var outro = Mathf.Clamp01(timeLeft / 0.32f);
            var alpha = Mathf.Min(intro, outro);
            _dialogueGroup.alpha = alpha;
            _dialogueRoot.anchoredPosition = Vector2.Lerp(hiddenPosition, visiblePosition, intro);
            _dialogueRoot.localScale = Vector3.Lerp(new Vector3(0.97f, 0.97f, 1f), Vector3.one, intro);

            var characterRate = Mathf.Max(1f, dialogueCharactersPerSecond);
            var typedCharacters = Mathf.Clamp(Mathf.FloorToInt(elapsed * characterRate), 0, _dialogueLine.Length);
            var visibleLine = _dialogueLine.Substring(0, typedCharacters);
            if (typedCharacters < _dialogueLine.Length && Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0)
            {
                visibleLine += "_";
            }

            _dialogueSpeakerText.text = _dialogueSpeaker;
            _dialogueBodyText.text = visibleLine;
            _dialogueMetaText.text = _dialogueRevealedLead ? "YENI IPUCU" : "SORGULAMA";

            var accent = _dialogueRevealedLead ? ModernGuiTheme.AccentWarmColor : ModernGuiTheme.AccentColor;
            _dialogueSpeakerText.color = accent;
            _dialogueMetaText.color = _dialogueRevealedLead ? ModernGuiTheme.AccentWarmColor : ModernGuiTheme.MutedTextColor;
            if (_dialoguePanelImage != null)
            {
                _dialoguePanelImage.color = new Color(0.045f, 0.06f, 0.09f, Mathf.Lerp(0.9f, 0.98f, alpha));
            }

            if (_dialogueSpeakerImage != null)
            {
                _dialogueSpeakerImage.color = _dialogueRevealedLead
                    ? new Color(0.18f, 0.12f, 0.07f, 1f)
                    : new Color(0.08f, 0.13f, 0.17f, 1f);
            }

            if (_dialogueSignalFill != null)
            {
                _dialogueSignalFill.color = accent;
                var fillRect = _dialogueSignalFill.rectTransform;
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(typedCharacters / (float)Mathf.Max(1, _dialogueLine.Length)), 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }
        }

        private void RefreshLocationVisuals()
        {
            if (_locationGroup == null)
            {
                return;
            }

            var timeLeft = _locationUntil - Time.time;
            if (timeLeft <= 0f || string.IsNullOrWhiteSpace(_currentLocationTitle))
            {
                _locationGroup.alpha = 0f;
                return;
            }

            _locationGroup.alpha = Mathf.Clamp01(Mathf.Min(1f, timeLeft / 0.35f));
            _locationTitleText.text = _currentLocationTitle;
            _locationSubtitleText.text = _currentLocationSubtitle;
        }

        private void RefreshStaminaBar()
        {
            if (_staminaFill == null)
            {
                return;
            }

            var controller = Object.FindAnyObjectByType<PrototypeFirstPersonController>();
            var stamina = controller != null ? controller.SprintStamina01 : 1f;
            var fillRect = _staminaFill.rectTransform;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(stamina), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            _staminaFill.color = Color.Lerp(ModernGuiTheme.StaminaLowColor, ModernGuiTheme.StaminaColor, stamina);

            if (_staminaText != null)
            {
                if (stamina < 0.99f)
                {
                    _staminaText.text = $"ENERJI %{Mathf.RoundToInt(stamina * 100f)}";
                    _staminaText.color = stamina < 0.25f ? ModernGuiTheme.StaminaLowColor : ModernGuiTheme.TextColor;
                }
                else
                {
                    _staminaText.text = string.Empty;
                }
            }
        }

        private void RefreshScanCooldown()
        {
            if (_scanFill == null || _scanCard == null)
            {
                return;
            }

            var scanner = Object.FindAnyObjectByType<InvestigationScanner>();
            if (scanner == null)
            {
                _scanCard.gameObject.SetActive(false);
                return;
            }

            _scanCard.gameObject.SetActive(true);
            var ready = scanner.IsReady;
            var cooldownNorm = scanner.CooldownNormalized;
            var isActive = InvestigationScanner.IsScanActive;

            var fillRect = _scanFill.rectTransform;
            fillRect.anchorMax = new Vector2(ready ? 1f : (1f - cooldownNorm), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            if (isActive)
            {
                var pulse = Mathf.PingPong(Time.unscaledTime * 4f, 1f);
                _scanFill.color = Color.Lerp(ModernGuiTheme.ScanActiveColor, ModernGuiTheme.AccentColor, pulse);
                _scanText.text = "TARAMA AKTIF";
                _scanText.color = ModernGuiTheme.ScanActiveColor;
            }
            else if (ready)
            {
                _scanFill.color = ModernGuiTheme.ScanReadyColor;
                _scanText.text = "TARA [Q]";
                _scanText.color = ModernGuiTheme.ScanReadyColor;
            }
            else
            {
                _scanFill.color = ModernGuiTheme.ScanCooldownColor;
                _scanText.text = $"SOGUMA {Mathf.CeilToInt(scanner.CooldownRemaining)}s";
                _scanText.color = ModernGuiTheme.MutedTextColor;
            }
        }

        private void RefreshFlashlight()
        {
            if (_flashlightFill == null || _flashlightCard == null)
            {
                return;
            }

            var flashlight = Object.FindAnyObjectByType<FlashlightController>();
            if (flashlight == null)
            {
                _flashlightCard.gameObject.SetActive(false);
                return;
            }

            _flashlightCard.gameObject.SetActive(true);
            var battery = flashlight.Battery01;

            var fillRect = _flashlightFill.rectTransform;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(battery), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            if (flashlight.IsOn)
            {
                var flickerPulse = flashlight.IsFlickering
                    ? Mathf.PingPong(Time.unscaledTime * 8f, 1f)
                    : 1f;
                _flashlightFill.color = Color.Lerp(ModernGuiTheme.WarningColor, ModernGuiTheme.FlashlightUIColor, battery) * flickerPulse;
                _flashlightText.text = $"FENER %{Mathf.RoundToInt(battery * 100f)}";
                _flashlightText.color = battery < 0.2f ? ModernGuiTheme.DangerColor : ModernGuiTheme.FlashlightUIColor;
            }
            else
            {
                _flashlightFill.color = new Color(0.4f, 0.38f, 0.32f, 0.5f);
                _flashlightText.text = battery < 1f ? $"FENER [F] %{Mathf.RoundToInt(battery * 100f)}" : "FENER [F]";
                _flashlightText.color = ModernGuiTheme.MutedTextColor;
            }
        }

        private void UpdateLocationBanner()
        {
            if (playerInteraction == null)
            {
                return;
            }

            var title = SchoolLocationUtility.GetZoneTitle(playerInteraction.transform.position);
            if (title == _currentLocationTitle)
            {
                return;
            }

            _currentLocationTitle = title;
            _currentLocationSubtitle = SchoolLocationUtility.GetZoneSubtitle(playerInteraction.transform.position);
            _locationUntil = Time.time + locationDuration;
        }

        private void HandleSessionMessage(string message)
        {
            if (IsCurrentDialogueMessage(message))
            {
                return;
            }

            _currentMessage = message;
            _currentMessageIsBlocker = IsBlockerMessage(message);
            _messageUntil = Time.time + (_currentMessageIsBlocker ? Mathf.Max(messageDuration, 6.2f) : messageDuration);
        }

        private static bool IsBlockerMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.StartsWith("Once ", System.StringComparison.OrdinalIgnoreCase) ||
                ContainsOrdinalIgnoreCase(message, " icin once ") ||
                ContainsOrdinalIgnoreCase(message, "once ilgili") ||
                ContainsOrdinalIgnoreCase(message, "once uygun") ||
                ContainsOrdinalIgnoreCase(message, "onceki ipucunu");
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HandleNpcConversation(string npcId, string npcDisplayName, string line, bool revealedLead)
        {
            _dialogueSpeaker = string.IsNullOrWhiteSpace(npcDisplayName) ? "NPC" : npcDisplayName;
            _dialogueLine = CompactDialogueLine(line);
            _dialogueRevealedLead = revealedLead;
            _dialogueStartedAt = Time.time;
            var characterRate = Mathf.Max(1f, dialogueCharactersPerSecond);
            _dialogueUntil = Time.time + Mathf.Max(dialogueDuration, _dialogueLine.Length / characterRate + 1.4f);

            if (_dialogueGroup != null)
            {
                _dialogueGroup.alpha = 0f;
            }
        }

        private void HandleEvidenceCollected(EvidenceData evidence)
        {
            if (evidence != null && evidence.IsCritical)
            {
                HandleSessionMessage("Kritik delil: " + evidence.Title);
            }
        }

        private void TrySubscribe()
        {
            if (CaseSessionManager.Instance == null || _subscribedSession == CaseSessionManager.Instance)
            {
                return;
            }

            if (_subscribedSession != null)
            {
                _subscribedSession.SessionMessagePublished -= HandleSessionMessage;
                _subscribedSession.EvidenceCollected -= HandleEvidenceCollected;
                _subscribedSession.NpcConversationRegistered -= HandleNpcConversation;
            }

            _subscribedSession = CaseSessionManager.Instance;
            _subscribedSession.SessionMessagePublished += HandleSessionMessage;
            _subscribedSession.EvidenceCollected += HandleEvidenceCollected;
            _subscribedSession.NpcConversationRegistered += HandleNpcConversation;

            if (_subscribedSession.ActiveCase != null)
            {
                HandleSessionMessage(_subscribedSession.ActiveCase.OpeningBrief);
            }
        }

        private void ResolveReferences()
        {
            if (playerInteraction == null || !playerInteraction.isActiveAndEnabled)
            {
                var candidates = Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Exclude);
                for (var i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i] != null && candidates[i].isActiveAndEnabled)
                    {
                        playerInteraction = candidates[i];
                        break;
                    }
                }
            }

            if (progressTracker == null)
            {
                progressTracker = Object.FindAnyObjectByType<CaseProgressTracker>();
            }
        }

        private void DisableLegacyHudScripts()
        {
            var status = Object.FindAnyObjectByType<CaseStatusHud>();
            if (status != null)
            {
                status.enabled = false;
            }

            var checklist = Object.FindAnyObjectByType<CaseChecklistHud>();
            if (checklist != null)
            {
                checklist.enabled = false;
            }

            var waypoint = Object.FindAnyObjectByType<InvestigationWaypointHud>();
            if (waypoint != null)
            {
                waypoint.enabled = false;
            }

            var banner = Object.FindAnyObjectByType<LocationBannerHud>();
            if (banner != null)
            {
                banner.enabled = false;
            }

            var minimap = Object.FindAnyObjectByType<SchoolMinimapHud>();
            if (minimap != null)
            {
                minimap.enabled = false;
                minimap.gameObject.SetActive(false);
            }

            var uguiMinimap = Object.FindAnyObjectByType<UguiMinimapHud>();
            if (uguiMinimap != null)
            {
                uguiMinimap.enabled = false;
                uguiMinimap.gameObject.SetActive(false);
            }
        }

        private bool TryGetNextTarget(CaseSessionManager session, out string title, out Vector3 worldPosition, out string subtitle)
        {
            title = string.Empty;
            worldPosition = Vector3.zero;
            subtitle = string.Empty;

            var stealth = Object.FindAnyObjectByType<PlayerStealthController>();
            if (stealth != null && stealth.IsHighAlert && playerInteraction != null)
            {
                GetNearestRecoverySpot(playerInteraction.transform.position, out worldPosition, out title, out subtitle);
                return true;
            }

            if (!session.HasEvidence("evidence.security-log"))
            {
                title = "Guvenlik Odasi";
                worldPosition = new Vector3(-7f, 1f, 10f);
                subtitle = "Gece hareketlerini teyit eden ilk dijital kayit burada.";
                return true;
            }

            if (!session.HasEvidence("evidence.answer-key-note"))
            {
                title = "Kutuphane Masasi";
                worldPosition = new Vector3(7f, 1f, -2f);
                subtitle = "Yazili not ve fiziksel kagit izi kutuphane tarafinda.";
                return true;
            }

            if (!session.HasEvidence("evidence.library-alibi"))
            {
                title = "Bilisim Oturum Kaydi";
                worldPosition = new Vector3(7.8f, 1f, 9.5f);
                subtitle = "Okul ag kaydi, kutuphane ogrencisinin mazeretini dogrular.";
                return true;
            }

            if (!session.HasEvidence("evidence.student-testimony"))
            {
                title = "Kutuphane Ogrencisi";
                worldPosition = new Vector3(5f, 1f, -2f);
                subtitle = "Oturum kaydi bulunduktan sonra ogrenci yanlis hedef olmadigini anlatir.";
                return true;
            }

            if (!session.HasEvidence("evidence.canteen-receipt"))
            {
                title = "Kantin Fisi";
                worldPosition = new Vector3(13.2f, 1f, 8.8f);
                subtitle = "Kasa ustundeki fis, kantin ifadesindeki saat tutarsizligini acar.";
                return true;
            }

            if (!session.HasEvidence("evidence.canteen-testimony"))
            {
                title = "Kantin Calisani";
                worldPosition = new Vector3(13.2f, 1f, 8.8f);
                subtitle = "Fisle birlikte konusursan ilk yalan ifade kirilir.";
                return true;
            }

            if (!session.HasTool("tool.archive-pass"))
            {
                title = "Arsiv Gecis Karti";
                worldPosition = new Vector3(7.8f, 1f, 9.5f);
                subtitle = "Kantin ifadesinden sonra karti al. Arsiv raflari bu olmadan acilmayacak.";
                return true;
            }

            if (!session.HasEvidence("evidence.archive-ledger"))
            {
                title = "Arsiv Kanadi";
                worldPosition = new Vector3(-15f, 1f, 9f);
                subtitle = "Gecis karti sende. Raf kutusunu arayip giris defterini ortaya cikar.";
                return true;
            }

            if (!session.HasTool("tool.lockpick"))
            {
                title = "Maymuncuk Seti";
                worldPosition = new Vector3(-8.1f, 1f, 9.8f);
                subtitle = "Guvenlik ekipman dolabindaki seti al. Kilitli cekmeceyi bununla acacaksin.";
                return true;
            }

            if (!session.HasEvidence("evidence.locker-key"))
            {
                title = "Ogretmenler Odasi";
                worldPosition = new Vector3(7f, 1f, 10f);
                subtitle = "Maymuncukla masadaki cekmeceyi ara; yedek anahtar burada sakli.";
                return true;
            }

            if (!session.HasEvidence("evidence.security-drawer-note"))
            {
                title = "Guvenlik Cekmecesi";
                worldPosition = new Vector3(-8.1f, 1f, 9.8f);
                subtitle = "Maymuncukla kilitli cekmeceyi ac; nobet notu kamera boslugunu tamamlar.";
                return true;
            }

            if (!session.HasEvidence("evidence.guard-testimony"))
            {
                title = "Guvenlik Gorevlisi";
                worldPosition = new Vector3(-2f, 1f, 9f);
                subtitle = "Nobet notundan sonra guvenlik gorevlisi kamera kaydini net ifade eder.";
                return true;
            }

            if (session.HasAnyAccusableSuspect())
            {
                title = "Guvenlik Gorevlisi";
                worldPosition = new Vector3(0f, 1f, 4f);
                subtitle = "Son ifade vakayi kapatacak. Guvenlik gorevlisine geri don.";
                return true;
            }

            title = "Koridor Tarama";
            worldPosition = new Vector3(0f, 1f, 4f);
            subtitle = "Takim notlarini kontrol et ve eksik ipucunu yeniden tara.";
            return true;
        }

        private string GetDirectionHint(Vector3 worldPosition)
        {
            var localTarget = playerInteraction.transform.InverseTransformPoint(worldPosition);
            if (localTarget.z < -1f)
            {
                return localTarget.x < 0f ? "ARKA SOL" : "ARKA SAG";
            }

            if (localTarget.x < -1.25f)
            {
                return "SOLA DON";
            }

            if (localTarget.x > 1.25f)
            {
                return "SAGA DON";
            }

            return "DUZ ILERI";
        }

        private bool IsCurrentDialogueMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) ||
                string.IsNullOrWhiteSpace(_dialogueSpeaker) ||
                string.IsNullOrWhiteSpace(_dialogueLine) ||
                Time.time >= _dialogueUntil)
            {
                return false;
            }

            return message.StartsWith(_dialogueSpeaker + ":", System.StringComparison.Ordinal) &&
                message.Contains(_dialogueLine);
        }

        private static string CompactDialogueLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var compact = line.Replace('\n', ' ').Replace('\r', ' ').Trim();
            while (compact.Contains("  "))
            {
                compact = compact.Replace("  ", " ");
            }

            return compact.Length <= 180 ? compact : compact.Substring(0, 177) + "...";
        }

        private static string TrimHudLine(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, Mathf.Max(0, maxLength - 3)).TrimEnd() + "...";
        }

        private static string FormatElapsedTime(float timeSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(timeSeconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static void GetNearestRecoverySpot(Vector3 origin, out Vector3 worldPosition, out string title, out string subtitle)
        {
            var positions = new[]
            {
                new Vector3(1.8f, 1f, 6.5f),
                new Vector3(8.45f, 1f, -4.8f),
                new Vector3(-4f, 1f, 22f)
            };
            var titles = new[]
            {
                "Koridor Banki",
                "Kutuphane Rafi",
                "Avlu Banki"
            };
            var subtitles = new[]
            {
                "Bankta sakinlesip dikkat seviyeni dusur.",
                "Raf arkasinda bekleyip baskiyi azalt.",
                "Avluya cekilip nefesini toparla."
            };

            var bestIndex = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < positions.Length; i++)
            {
                var distance = Vector3.SqrMagnitude(origin - positions[i]);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestIndex = i;
            }

            worldPosition = positions[bestIndex];
            title = titles[bestIndex];
            subtitle = subtitles[bestIndex];
        }

        private Image CreateMeter(Transform parent, string label)
        {
            var root = RuntimeUiFactory.CreateUiRoot(label + "Meter", parent);
            RuntimeUiFactory.AddVerticalLayout(root, 3f, new RectOffset(0, 0, 0, 0));
            RuntimeUiFactory.CreateText(label + "Label", root, label.ToUpperInvariant(), 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            var shell = RuntimeUiFactory.CreateUiRoot(label + "Shell", root);
            RuntimeUiFactory.EnsureLayoutElement(shell, preferredHeight: 18f);
            RuntimeUiFactory.AddImage(shell.gameObject, new Color(0.07f, 0.09f, 0.11f, 1f));
            RuntimeUiFactory.AddOutline(shell.gameObject, new Color(0f, 0f, 0f, 0.32f), new Vector2(1f, -1f));

            var fill = RuntimeUiFactory.CreateUiRoot(label + "Fill", shell).gameObject.AddComponent<Image>();
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(0f, -2f);
            fill.color = ModernGuiTheme.AccentColor;

            var valueText = RuntimeUiFactory.CreateText(label + "Value", shell, string.Empty, 11, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            RuntimeUiFactory.Stretch(valueText.rectTransform);
            valueText.gameObject.name = "Value";
            return fill;
        }

        private void SetMeterFill(Image fill, float value, Color color)
        {
            if (fill == null)
            {
                return;
            }

            var clamped = Mathf.Clamp01(value);
            fill.color = color;
            fill.rectTransform.sizeDelta = new Vector2(320f * clamped, 0f);
            var valueText = fill.transform.parent != null ? fill.transform.parent.Find("Value") : null;
            if (valueText != null)
            {
                var text = valueText.GetComponent<Text>();
                if (text != null)
                {
                    text.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
                }
            }
        }
    }
}

