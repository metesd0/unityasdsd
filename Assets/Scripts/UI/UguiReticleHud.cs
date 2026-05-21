using MobilOfl.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public class UguiReticleHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractionController playerInteraction;
        [SerializeField] private float size = 7f;

        private RectTransform _root;
        private Image _centerDot;
        private Image[] _reticleTicks;
        private Text _promptText;
        private CanvasGroup _promptGroup;
        private Image _holdFill;
        private bool _built;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            DisableLegacy();
        }

        private void Update()
        {
            BuildIfNeeded();
            DisableLegacy();
            ResolveInteraction();
            Refresh();
        }

        private void BuildIfNeeded()
        {
            if (_built)
            {
                return;
            }

            var canvasTransform = transform.Find("UguiReticleCanvas") as RectTransform;
            if (canvasTransform == null)
            {
                canvasTransform = RuntimeUiFactory.CreateUiRoot("UguiReticleCanvas", transform);
            }

            var canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 72;

            var scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RuntimeUiFactory.Stretch(canvasTransform);
            RuntimeUiFactory.ClearChildren(canvasTransform);
            _root = canvasTransform;

            var center = RuntimeUiFactory.CreateUiRoot("ReticleCenter", _root);
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(1f, 1f);

            _centerDot = CreateLine(center, "Dot", Vector2.zero, new Vector2(5f, 5f));
            RuntimeUiFactory.ApplyOneUiRounding(_centerDot.gameObject, 2.5f);

            _reticleTicks = new Image[4];
            _reticleTicks[0] = CreateLine(center, "LeftTick", new Vector2(-12f, 0f), new Vector2(size, 2f));
            _reticleTicks[1] = CreateLine(center, "RightTick", new Vector2(12f, 0f), new Vector2(size, 2f));
            _reticleTicks[2] = CreateLine(center, "TopTick", new Vector2(0f, 12f), new Vector2(2f, size));
            _reticleTicks[3] = CreateLine(center, "BottomTick", new Vector2(0f, -12f), new Vector2(2f, size));

            var promptCard = RuntimeUiFactory.CreateCard("PromptCard", _root, new Color(0.07f, 0.09f, 0.12f, 0.96f), ModernGuiTheme.AccentColor);
            promptCard.anchorMin = new Vector2(0.5f, 0.5f);
            promptCard.anchorMax = new Vector2(0.5f, 0.5f);
            promptCard.pivot = new Vector2(0.5f, 0f);
            promptCard.anchoredPosition = new Vector2(0f, -90f);
            promptCard.sizeDelta = new Vector2(400f, 62f);
            RuntimeUiFactory.AddVerticalLayout(promptCard, 0f, new RectOffset(16, 16, 14, 12));
            _promptText = RuntimeUiFactory.CreateText("PromptText", promptCard, string.Empty, 19, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            _promptText.alignment = TextAnchor.MiddleCenter;
            var holdShell = RuntimeUiFactory.CreateUiRoot("HoldShell", promptCard);
            RuntimeUiFactory.EnsureLayoutElement(holdShell, preferredHeight: 12f);
            RuntimeUiFactory.AddImage(holdShell.gameObject, new Color(0.08f, 0.1f, 0.13f, 1f));
            RuntimeUiFactory.AddOutline(holdShell.gameObject, new Color(0f, 0f, 0f, 0.3f), new Vector2(1f, -1f));
            _holdFill = RuntimeUiFactory.CreateUiRoot("Fill", holdShell).gameObject.AddComponent<Image>();
            _holdFill.color = ModernGuiTheme.AccentWarmColor;
            var fillRect = _holdFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _promptGroup = promptCard.gameObject.GetComponent<CanvasGroup>();
            if (_promptGroup == null)
            {
                _promptGroup = promptCard.gameObject.AddComponent<CanvasGroup>();
            }

            _built = true;
        }

        private Image CreateLine(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rect = RuntimeUiFactory.CreateUiRoot(name, parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return RuntimeUiFactory.AddImage(rect.gameObject, new Color(1f, 1f, 1f, 0.55f));
        }

        private void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            var hidden = MainMenuHud.IsBlockingGameplay || CaseNotebookHud.IsAnyNotebookOpen || (CaseSessionManager.Instance != null && CaseSessionManager.Instance.IsCaseResolved);
            _root.gameObject.SetActive(!hidden);
            if (hidden)
            {
                return;
            }

            var hasTarget = playerInteraction != null && playerInteraction.CurrentInteractable != null;
            var scanPulse = InvestigationScanner.IsScanActive
                ? 0.45f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.35f)
                : 0f;
            var idleColor = Color.Lerp(new Color(1f, 1f, 1f, 0.42f), new Color(0.24f, 0.96f, 0.86f, 0.86f), scanPulse);
            var color = hasTarget ? new Color(0.2f, 0.95f, 0.65f, 0.95f) : idleColor;
            if (_centerDot != null)
            {
                _centerDot.color = hasTarget ? color : new Color(1f, 1f, 1f, 0.62f);
            }

            for (var i = 0; i < _reticleTicks.Length; i++)
            {
                _reticleTicks[i].color = hasTarget ? color : new Color(color.r, color.g, color.b, 0.24f);
            }

            var showPrompt = hasTarget;
            _promptGroup.alpha = showPrompt ? 1f : 0f;
            var holdTarget = hasTarget && playerInteraction.CurrentInteractable.RequiresHold;
            if (_holdFill != null)
            {
                _holdFill.transform.parent.gameObject.SetActive(holdTarget);
                var progress = holdTarget ? playerInteraction.HoldProgress01 : 0f;
                _holdFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                _holdFill.rectTransform.sizeDelta = Vector2.zero;
            }

            _promptText.text = hasTarget
                ? (holdTarget
                    ? $"Basili tut: {playerInteraction.CurrentInteractable.PromptText}"
                    : playerInteraction.CurrentInteractable.PromptText)
                : string.Empty;
        }

        private void ResolveInteraction()
        {
            if (playerInteraction != null && playerInteraction.isActiveAndEnabled)
            {
                return;
            }

            var candidates = Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Exclude);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && candidates[i].isActiveAndEnabled)
                {
                    playerInteraction = candidates[i];
                    return;
                }
            }
        }

        private void DisableLegacy()
        {
            var legacy = Object.FindAnyObjectByType<FocusReticleHud>();
            if (legacy != null)
            {
                legacy.enabled = false;
            }
        }
    }
}
