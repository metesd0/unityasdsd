using UnityEngine;
using UnityEngine.InputSystem;
using MobilOfl.UI;
using MobilOfl.Online;

namespace MobilOfl.Gameplay
{
    public class PlayerInteractionController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactDistance = 4.5f;
        [SerializeField] private float minimumInteractDistance = 5.25f;
        [SerializeField] private float aimAssistRadius = 0.56f;
        [SerializeField] private float targetGraceTime = 0.25f;
        [SerializeField] private float scanAssistDistanceBonus = 1.35f;
        [SerializeField] private float scanAssistRadiusBonus = 0.22f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private Key alternateInteractKey = Key.B;
        [SerializeField] private Key secondAlternateInteractKey = Key.None;
        [SerializeField] private MobileButton mobileInteractButton;

        private InteractableBase _currentInteractable;
        private InteractableBase _lastInteractable;
        private float _holdProgress;
        private float _lastTargetSeenAt = -999f;

        public InteractableBase CurrentInteractable => _currentInteractable;
        public Camera PlayerCamera => playerCamera;
        public float HoldProgress01 => _currentInteractable != null && _currentInteractable.RequiresHold ? Mathf.Clamp01(_holdProgress) : 0f;
        public bool IsHoldingInteract => HoldProgress01 > 0f;

        private void Update()
        {
            ResolveCamera();
            if (MainMenuHud.IsBlockingGameplay || (CaseSessionManager.Instance != null && CaseSessionManager.Instance.IsCaseResolved))
            {
                _currentInteractable = null;
                ResetHoldState();
                return;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                _currentInteractable = null;
                ResetHoldState();
                return;
            }

            if (CaseNotebookHud.IsAnyNotebookOpen)
            {
                _currentInteractable = null;
                ResetHoldState();
                return;
            }

            var interactPressed =
                IsKeyPressedThisFrame(interactKey) ||
                IsKeyPressedThisFrame(alternateInteractKey) ||
                IsKeyPressedThisFrame(secondAlternateInteractKey) ||
                (mobileInteractButton != null && mobileInteractButton.ConsumeWasPressedThisFrame());
            var interactHeld =
                IsKeyHeld(interactKey) ||
                IsKeyHeld(alternateInteractKey) ||
                IsKeyHeld(secondAlternateInteractKey) ||
                (mobileInteractButton != null && mobileInteractButton.IsPressed);

            UpdateCurrentInteractable(interactHeld, interactPressed);

            if (_currentInteractable == null)
            {
                ResetHoldState();
                return;
            }

            if (!_currentInteractable.RequiresHold)
            {
                if (interactPressed)
                {
                    _currentInteractable.TryInteract(gameObject);
                }

                ResetHoldState();
                return;
            }

            if (!interactHeld || !_currentInteractable.CanMaintainHold(gameObject))
            {
                if (!interactHeld)
                {
                    _holdProgress = 0f;
                }

                return;
            }

            var duration = Mathf.Max(0.05f, _currentInteractable.GetHoldDuration(gameObject));
            _holdProgress += Time.deltaTime / duration;
            if (_holdProgress < 1f)
            {
                return;
            }

            _currentInteractable.TryInteract(gameObject);
            ResetHoldState();
        }

        public bool TryInteractFromUi()
        {
            return _currentInteractable != null && _currentInteractable.TryInteract(gameObject);
        }

        public void SetMobileButton(MobileButton button)
        {
            mobileInteractButton = button;
        }

        private void UpdateCurrentInteractable(bool interactHeld, bool interactPressed)
        {
            _currentInteractable = null;

            if (playerCamera == null)
            {
                return;
            }

            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (TryFindAimedInteractable(ray, out var aimedInteractable))
            {
                SetCurrentInteractable(aimedInteractable);
                return;
            }

            if (_lastInteractable != null &&
                Time.time <= _lastTargetSeenAt + targetGraceTime &&
                _lastInteractable.isActiveAndEnabled)
            {
                _currentInteractable = _lastInteractable;
                return;
            }

            if (interactHeld &&
                _lastInteractable != null &&
                _lastInteractable.RequiresHold &&
                _lastInteractable.CanMaintainHold(gameObject))
            {
                _currentInteractable = _lastInteractable;
                return;
            }

            if (_lastInteractable != null)
            {
                ResetHoldState();
            }
        }

        private bool TryFindAimedInteractable(Ray ray, out InteractableBase interactable)
        {
            interactable = null;
            var distance = Mathf.Max(interactDistance, minimumInteractDistance);
            if (InvestigationScanner.IsScanActive)
            {
                distance += Mathf.Max(0f, scanAssistDistanceBonus);
            }

            InteractableBase exactInteractable = null;
            if (Physics.Raycast(ray, out var exactHit, distance, interactMask, QueryTriggerInteraction.Collide))
            {
                exactInteractable = exactHit.collider.GetComponentInParent<InteractableBase>();
                if (exactInteractable is NpcInteractable &&
                    exactInteractable.isActiveAndEnabled &&
                    exactInteractable.CanShowInteractionPrompt(gameObject))
                {
                    interactable = exactInteractable;
                    return true;
                }
            }

            var radius = Mathf.Max(0.05f, aimAssistRadius + (InvestigationScanner.IsScanActive ? scanAssistRadiusBonus : 0f));
            var hits = Physics.SphereCastAll(ray, radius, distance, interactMask, QueryTriggerInteraction.Collide);
            var bestScore = float.PositiveInfinity;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                var candidate = hit.collider.GetComponentInParent<InteractableBase>();
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    !candidate.CanShowInteractionPrompt(gameObject))
                {
                    continue;
                }

                var toHit = hit.point - ray.origin;
                var alignment = toHit.sqrMagnitude > 0.001f
                    ? Vector3.Dot(ray.direction, toHit.normalized)
                    : 1f;
                var score = hit.distance + (1f - Mathf.Clamp01(alignment)) * 1.35f;
                if (candidate is NpcInteractable)
                {
                    score -= 0.85f;
                }

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                interactable = candidate;
            }

            if (interactable != null)
            {
                return true;
            }

            if (exactInteractable != null &&
                exactInteractable.isActiveAndEnabled &&
                exactInteractable.CanShowInteractionPrompt(gameObject))
            {
                interactable = exactInteractable;
                return true;
            }

            return false;
        }

        private void SetCurrentInteractable(InteractableBase interactable)
        {
            _currentInteractable = interactable;
            _lastTargetSeenAt = Time.time;
            if (_currentInteractable != _lastInteractable)
            {
                ResetHoldState();
                _lastInteractable = _currentInteractable;
            }
        }

        private void ResetHoldState()
        {
            _holdProgress = 0f;
            _lastInteractable = _currentInteractable;
        }

        private static bool IsKeyPressedThisFrame(Key key)
        {
            return Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame;
        }

        private static bool IsKeyHeld(Key key)
        {
            return Keyboard.current != null && key != Key.None && Keyboard.current[key].isPressed;
        }

        private void ResolveCamera()
        {
            if (playerCamera != null && playerCamera.isActiveAndEnabled)
            {
                return;
            }

            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                return;
            }

            playerCamera = Camera.main;
            if (playerCamera != null)
            {
                return;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (var i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    playerCamera = cameras[i];
                    return;
                }
            }
        }
    }
}
