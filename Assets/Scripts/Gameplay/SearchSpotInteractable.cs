using MobilOfl.Case;
using MobilOfl.Online;
using MobilOfl.Visuals;
using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class SearchSpotInteractable : InteractableBase
    {
        [SerializeField] private CaseDefinition caseDefinition;
        [SerializeField] private string hiddenEvidenceId;
        [SerializeField] private string markerLabel = "Aranacak Alan";
        [SerializeField] private Color markerColor = default;
        [SerializeField] private float searchDuration = 1.7f;
        [SerializeField] private float useDistance = 5.25f;
        [SerializeField] private string requiredToolId;
        [SerializeField] private string missingToolMessage = "Bu alan icin uygun ekipman gerekiyor.";
        [SerializeField] private string requiredEvidenceId;
        [SerializeField] private string missingRequirementMessage = "Bu arama icin once ilgili ipucunu bulman gerekiyor.";
        [SerializeField] private string searchCompleteMessage = "Arama tamamlandi.";
        [SerializeField] private GameObject searchedVisual;
        [SerializeField] private bool disableObjectOnSearch = true;

        private bool _isSubscribed;
        private float _nextMissingToolMessageAt;
        private float _nextMissingRequirementMessageAt;
        private Renderer[] _cachedRenderers;
        private Collider[] _cachedColliders;
        private Behaviour[] _cachedBehaviours;

        public string HiddenEvidenceId => hiddenEvidenceId;
        public string RequiredToolId => ResolveRequiredToolId();
        public string RequiredEvidenceId => requiredEvidenceId;
        public string MarkerLabel => string.IsNullOrWhiteSpace(markerLabel) ? "Aranacak Alan" : markerLabel;
        public Color MarkerColor => markerColor.a <= 0f ? new Color(0.92f, 0.74f, 0.3f, 1f) : markerColor;
        public bool IsMarkerVisible =>
            isActiveAndEnabled &&
            !string.IsNullOrWhiteSpace(hiddenEvidenceId) &&
            CaseSessionManager.Instance != null &&
            !CaseSessionManager.Instance.HasEvidence(hiddenEvidenceId);

        public override bool RequiresHold => true;
        public override float HoldDuration => Mathf.Clamp(searchDuration, 0.35f, 1.1f);

        public override bool CanShowInteractionPrompt(GameObject interactor)
        {
            return IsMarkerVisible;
        }

        public void ConfigureToolGate(string toolId, string message)
        {
            if (!string.IsNullOrWhiteSpace(toolId))
            {
                requiredToolId = toolId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                missingToolMessage = message.Trim();
            }
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
                CaseSessionManager.Instance.EvidenceCollected -= HandleEvidenceCollected;
                CaseSessionManager.Instance.CaseStarted -= HandleCaseStarted;
            }

            _isSubscribed = false;
        }

        public override bool CanMaintainHold(GameObject interactor)
        {
            if (interactor == null)
            {
                return false;
            }

            if (GetDistanceToInteractor(interactor) > Mathf.Max(1f, useDistance))
            {
                return false;
            }

            if (HasRequiredTool())
            {
                if (HasRequiredEvidence())
                {
                    return true;
                }

                TryPublishMissingRequirement();
                return false;
            }

            TryPublishMissingToolMessage();
            return false;
        }

        public override bool TryInteract(GameObject interactor)
        {
            var sourceCase = caseDefinition != null
                ? caseDefinition
                : (CaseSessionManager.Instance != null ? CaseSessionManager.Instance.ActiveCase : null);
            if (sourceCase == null || string.IsNullOrWhiteSpace(hiddenEvidenceId) || CaseSessionManager.Instance == null)
            {
                return false;
            }

            if (CaseSessionManager.Instance.HasEvidence(hiddenEvidenceId))
            {
                ApplySearchedState();
                return false;
            }

            if (!HasRequiredTool())
            {
                TryPublishMissingToolMessage(true);
                return false;
            }

            if (!HasRequiredEvidence())
            {
                TryPublishMissingRequirement(true);
                return false;
            }

            var networkCaseState = NetworkCaseState.Instance;
            var collected = networkCaseState != null && networkCaseState.IsOnlineSessionActive
                ? networkCaseState.RequestCollectEvidence(hiddenEvidenceId)
                : CaseSessionManager.Instance.TryCollectEvidence(sourceCase, hiddenEvidenceId);

            if (!collected)
            {
                return false;
            }

            CaseSessionManager.Instance.PublishMessage(searchCompleteMessage);
            ApplySearchedState();
            return true;
        }

        private bool HasRequiredTool()
        {
            var resolvedRequiredToolId = ResolveRequiredToolId();
            return CaseSessionManager.Instance == null ||
                   string.IsNullOrWhiteSpace(resolvedRequiredToolId) ||
                   CaseSessionManager.Instance.HasTool(resolvedRequiredToolId);
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

            return "Bu aramayi yapmak icin once onceki ipucunu tamamlaman gerekiyor.";
        }

        private void TryPublishMissingToolMessage(bool force = false)
        {
            if (CaseSessionManager.Instance == null)
            {
                return;
            }

            if (!force && Time.time < _nextMissingToolMessageAt)
            {
                return;
            }

            _nextMissingToolMessageAt = Time.time + 1.25f;
            var resolvedMessage = ResolveMissingToolMessage();
            CaseSessionManager.Instance.PublishMessage(string.IsNullOrWhiteSpace(resolvedMessage)
                ? "Bu alan icin once uygun ekipman bulman gerekiyor."
                : resolvedMessage);
        }

        private string ResolveRequiredToolId()
        {
            if (!string.IsNullOrWhiteSpace(requiredToolId))
            {
                return requiredToolId;
            }

            return hiddenEvidenceId switch
            {
                "evidence.locker-key" => "tool.lockpick",
                "evidence.archive-ledger" => "tool.archive-pass",
                _ => string.Empty
            };
        }

        private float GetDistanceToInteractor(GameObject interactor)
        {
            var interactorPosition = interactor.transform.position + Vector3.up * 1.05f;
            var colliders = GetComponentsInChildren<Collider>(false);
            var closestDistance = float.PositiveInfinity;

            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                var closestPoint = collider.ClosestPoint(interactorPosition);
                closestDistance = Mathf.Min(closestDistance, Vector3.Distance(interactorPosition, closestPoint));
            }

            return float.IsPositiveInfinity(closestDistance)
                ? Vector3.Distance(interactor.transform.position, transform.position)
                : closestDistance;
        }

        private string ResolveMissingToolMessage()
        {
            if (!string.IsNullOrWhiteSpace(missingToolMessage))
            {
                return missingToolMessage;
            }

            return ResolveRequiredToolId() switch
            {
                "tool.lockpick" => "Bu cekmece icin once maymuncuk seti bulman gerekiyor.",
                "tool.archive-pass" => "Arsiv raf kutusu icin once gecis karti bulman gerekiyor.",
                _ => string.Empty
            };
        }

        private void HandleEvidenceCollected(EvidenceData evidence)
        {
            if (evidence != null && evidence.Id == hiddenEvidenceId)
            {
                ApplySearchedState();
            }
        }

        private void HandleCaseStarted(CaseDefinition _)
        {
            RefreshCollectedState();
        }

        private void TrySubscribe()
        {
            if (CaseSessionManager.Instance == null)
            {
                return;
            }

            CaseSessionManager.Instance.EvidenceCollected -= HandleEvidenceCollected;
            CaseSessionManager.Instance.CaseStarted -= HandleCaseStarted;
            CaseSessionManager.Instance.EvidenceCollected += HandleEvidenceCollected;
            CaseSessionManager.Instance.CaseStarted += HandleCaseStarted;
            _isSubscribed = true;
        }

        private void RefreshCollectedState()
        {
            if (CaseSessionManager.Instance != null && CaseSessionManager.Instance.HasEvidence(hiddenEvidenceId))
            {
                ApplySearchedState();
                return;
            }

            RestoreUnsearchedState();
        }

        private void ApplySearchedState()
        {
            if (searchedVisual != null)
            {
                searchedVisual.SetActive(true);
            }

            if (disableObjectOnSearch)
            {
                SetInteractableVisualState(false);
            }
        }

        private void RestoreUnsearchedState()
        {
            if (searchedVisual != null)
            {
                searchedVisual.SetActive(false);
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
