using MobilOfl.Case;
using MobilOfl.Online;
using MobilOfl.Visuals;
using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class ToolPickupInteractable : InteractableBase
    {
        [SerializeField] private string toolId;
        [SerializeField] private string toolDisplayName = "Ekipman";
        [SerializeField] private string markerLabel = "Ekipman";
        [SerializeField] private Color markerColor = default;
        [SerializeField] private string pickupMessage = "Ekipman alindi.";
        [SerializeField] private string requiredEvidenceId;
        [SerializeField] private string missingRequirementMessage = "Bu ekipman icin once ilgili ipucunu bulman gerekiyor.";
        [SerializeField] private GameObject collectedVisual;
        [SerializeField] private bool disableObjectOnPickup = true;

        private bool _isSubscribed;
        private float _nextMissingRequirementMessageAt;
        private Renderer[] _cachedRenderers;
        private Collider[] _cachedColliders;
        private Behaviour[] _cachedBehaviours;

        public string ToolId => toolId;
        public string RequiredEvidenceId => requiredEvidenceId;
        public string MarkerLabel => string.IsNullOrWhiteSpace(markerLabel) ? toolDisplayName : markerLabel;
        public Color MarkerColor => markerColor.a <= 0f ? new Color(0.98f, 0.7f, 0.24f, 1f) : markerColor;
        public bool IsMarkerVisible =>
            isActiveAndEnabled &&
            !string.IsNullOrWhiteSpace(toolId) &&
            CaseSessionManager.Instance != null &&
            !CaseSessionManager.Instance.HasTool(toolId);

        public override bool CanShowInteractionPrompt(GameObject interactor)
        {
            return IsMarkerVisible;
        }

        public void Configure(
            string id,
            string displayName,
            string label,
            string message,
            Color color)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                toolId = id.Trim();
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                toolDisplayName = displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                markerLabel = label.Trim();
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                pickupMessage = message.Trim();
            }

            if (color.a > 0f)
            {
                markerColor = color;
            }

            RefreshCollectedState();
        }

        private void OnEnable()
        {
            CacheVisualTargets();
            TrySubscribe();
            RefreshCollectedState();
        }

        private void Update()
        {
            if (!_isSubscribed)
            {
                TrySubscribe();
            }
        }

        private void OnDisable()
        {
            if (CaseSessionManager.Instance != null)
            {
                CaseSessionManager.Instance.ToolUnlocked -= HandleToolUnlocked;
                CaseSessionManager.Instance.CaseStarted -= HandleCaseStarted;
            }

            _isSubscribed = false;
        }

        public override bool TryInteract(GameObject interactor)
        {
            if (CaseSessionManager.Instance == null || string.IsNullOrWhiteSpace(toolId))
            {
                return false;
            }

            if (CaseSessionManager.Instance.HasTool(toolId))
            {
                ApplyCollectedState();
                return false;
            }

            if (!HasRequiredEvidence())
            {
                TryPublishMissingRequirement(true);
                return false;
            }

            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState != null && networkCaseState.IsOnlineSessionActive)
            {
                return networkCaseState.RequestUnlockTool(toolId, toolDisplayName, pickupMessage);
            }

            if (!CaseSessionManager.Instance.TryUnlockTool(toolId, toolDisplayName, pickupMessage))
            {
                return false;
            }

            ApplyCollectedState();
            return true;
        }

        public void ConfigureEvidenceGate(string requiredEvidence, string message)
        {
            requiredEvidenceId = string.IsNullOrWhiteSpace(requiredEvidence) ? string.Empty : requiredEvidence.Trim();
            if (!string.IsNullOrWhiteSpace(message))
            {
                missingRequirementMessage = message.Trim();
            }
        }

        private bool HasRequiredEvidence()
        {
            return CaseSessionManager.Instance == null ||
                   string.IsNullOrWhiteSpace(requiredEvidenceId) ||
                   CaseSessionManager.Instance.HasEvidence(requiredEvidenceId);
        }

        private void TryPublishMissingRequirement(bool force = false)
        {
            if (CaseSessionManager.Instance == null)
            {
                return;
            }

            if (!force && Time.time < _nextMissingRequirementMessageAt)
            {
                return;
            }

            _nextMissingRequirementMessageAt = Time.time + 1.25f;
            CaseSessionManager.Instance.PublishMessage(ResolveMissingRequirementMessage());
        }

        private string ResolveMissingRequirementMessage()
        {
            if (!string.IsNullOrWhiteSpace(missingRequirementMessage))
            {
                return missingRequirementMessage;
            }

            if (CaseSessionManager.Instance != null)
            {
                var requiredEvidence = CaseSessionManager.Instance.GetEvidence(requiredEvidenceId);
                if (requiredEvidence != null)
                {
                    return "Once gerekli ipucunu bul: " + requiredEvidence.Title;
                }
            }

            return "Bu ekipmani almak icin once onceki ipucunu tamamlaman gerekiyor.";
        }

        private void HandleToolUnlocked(string unlockedToolId, string _)
        {
            if (unlockedToolId == toolId)
            {
                ApplyCollectedState();
            }
        }

        private void TrySubscribe()
        {
            if (CaseSessionManager.Instance == null)
            {
                return;
            }

            CaseSessionManager.Instance.ToolUnlocked -= HandleToolUnlocked;
            CaseSessionManager.Instance.CaseStarted -= HandleCaseStarted;
            CaseSessionManager.Instance.ToolUnlocked += HandleToolUnlocked;
            CaseSessionManager.Instance.CaseStarted += HandleCaseStarted;
            _isSubscribed = true;
        }

        private void HandleCaseStarted(CaseDefinition _)
        {
            RefreshCollectedState();
        }

        private void RefreshCollectedState()
        {
            if (CaseSessionManager.Instance != null && CaseSessionManager.Instance.HasTool(toolId))
            {
                ApplyCollectedState();
                return;
            }

            RestoreUncollectedState();
        }

        private void ApplyCollectedState()
        {
            if (collectedVisual != null)
            {
                collectedVisual.SetActive(true);
            }

            if (disableObjectOnPickup)
            {
                SetInteractableVisualState(false);
            }
        }

        private void RestoreUncollectedState()
        {
            if (collectedVisual != null)
            {
                collectedVisual.SetActive(false);
            }

            SetInteractableVisualState(true);
        }

        private void CacheVisualTargets()
        {
            if (_cachedRenderers == null)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_cachedColliders == null)
            {
                _cachedColliders = GetComponentsInChildren<Collider>(true);
            }

            if (_cachedBehaviours == null)
            {
                _cachedBehaviours = GetComponentsInChildren<Behaviour>(true);
            }
        }

        private void SetInteractableVisualState(bool isVisible)
        {
            CacheVisualTargets();

            if (_cachedRenderers != null)
            {
                for (var i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] != null)
                    {
                        _cachedRenderers[i].enabled = isVisible;
                    }
                }
            }

            if (_cachedColliders != null)
            {
                for (var i = 0; i < _cachedColliders.Length; i++)
                {
                    if (_cachedColliders[i] != null)
                    {
                        _cachedColliders[i].enabled = isVisible;
                    }
                }
            }

            if (_cachedBehaviours == null)
            {
                return;
            }

            for (var i = 0; i < _cachedBehaviours.Length; i++)
            {
                var behaviour = _cachedBehaviours[i];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                if (behaviour is EvidenceVisualPulse)
                {
                    behaviour.enabled = isVisible;
                }
            }
        }
    }
}
