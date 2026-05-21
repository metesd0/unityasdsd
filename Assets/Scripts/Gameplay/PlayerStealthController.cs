using UnityEngine;
using MobilOfl.UI;
using MobilOfl.Online;

namespace MobilOfl.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerStealthController : MonoBehaviour
    {
        [SerializeField] private PrototypeFirstPersonController movementController;
        [SerializeField] private InvestigationScanner scanner;
        [SerializeField] private float walkNoise = 0.34f;
        [SerializeField] private float sprintNoise = 1f;
        [SerializeField] private float crouchNoise = 0.14f;
        [SerializeField] private float idleNoiseDecay = 1.8f;
        [SerializeField] private float noiseLerpSpeed = 6.5f;
        [SerializeField] private float alertRiseSpeed = 1.8f;
        [SerializeField] private float alertDecaySpeed = 0.42f;
        [SerializeField] private float npcAwarenessRadius = 7.2f;
        [SerializeField] private float forcedCalmInteractionThreshold = 0.72f;
        [SerializeField] private float scanNoiseBoost = 0.2f;
        [SerializeField] private float quietSightNoiseThreshold = 0.18f;
        [SerializeField] private float sightMessageCooldown = 8f;

        private static readonly Collider[] NpcHits = new Collider[24];

        private CharacterController _characterController;
        private Vector3 _lastPosition;
        private float _noiseLevel;
        private float _alertLevel;
        private float _sightPressure;
        private float _lastSightPressureAt;
        private float _nextSpottedMessageAt;
        private bool _wasHighAlert;

        public float NoiseLevel01 => _noiseLevel;
        public float AlertLevel01 => _alertLevel;
        public bool IsHighAlert => _alertLevel >= CalmInteractionThreshold;
        private float CalmInteractionThreshold => Mathf.Max(0.9f, forcedCalmInteractionThreshold);
        public string MovementProfile => _characterController == null || !_characterController.isGrounded
            ? "Dengesiz"
            : movementController != null && movementController.IsCrouching
                ? "Sessiz"
                : movementController != null && movementController.IsSprinting
                    ? "Acik hedef"
                    : _noiseLevel > 0.2f ? "Dikkatli" : "Sakin";

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            ResolveReferences();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            ResolveReferences();

            if (MainMenuHud.IsBlockingGameplay || CaseNotebookHud.IsAnyNotebookOpen)
            {
                _lastPosition = transform.position;
                return;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                _lastPosition = transform.position;
                return;
            }

            UpdateNoise();
            UpdateAlert();
            _lastPosition = transform.position;
        }

        public bool CanStartCalmConversation(out string reason)
        {
            if (IsHighAlert)
            {
                reason = "Koridorda fazla dikkat cektin. Biraz sakinlesip tekrar dene.";
                return false;
            }

            if (_noiseLevel >= 0.84f)
            {
                reason = "Nefesin ve adimlarin hala cok belirgin. Sessizlesip tekrar konus.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void NotifyScannerPulse()
        {
            _noiseLevel = Mathf.Clamp01(_noiseLevel + scanNoiseBoost);
        }

        public void ApplyCalmRecovery(float targetAlertLevel, float targetNoiseLevel)
        {
            _alertLevel = Mathf.Min(_alertLevel, Mathf.Clamp01(targetAlertLevel));
            _noiseLevel = Mathf.Min(_noiseLevel, Mathf.Clamp01(targetNoiseLevel));
            _sightPressure = Mathf.Min(_sightPressure, Mathf.Clamp01(targetAlertLevel));
            if (_alertLevel <= CalmInteractionThreshold * 0.52f)
            {
                _wasHighAlert = false;
            }
        }

        public void RegisterNpcSightPressure(float amount, string watcherName)
        {
            var safeAmount = Mathf.Max(0f, amount);
            var playerIsQuiet = _noiseLevel < quietSightNoiseThreshold && _alertLevel < CalmInteractionThreshold * 0.45f;
            _sightPressure = Mathf.Clamp01(_sightPressure + (playerIsQuiet ? safeAmount * 0.12f : safeAmount));
            _lastSightPressureAt = Time.time;

            if (playerIsQuiet || CaseSessionManager.Instance == null || Time.time < _nextSpottedMessageAt)
            {
                return;
            }

            _nextSpottedMessageAt = Time.time + Mathf.Max(2.4f, sightMessageCooldown);
            var resolvedWatcher = string.IsNullOrWhiteSpace(watcherName) ? "Bir NPC" : watcherName;
            CaseSessionManager.Instance.PublishMessage($"{resolvedWatcher} seni gorus alanina aldi. Daha sessiz hareket et.");
        }

        private void UpdateNoise()
        {
            var displacement = transform.position - _lastPosition;
            var planarSpeed = new Vector2(displacement.x, displacement.z).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            var moveRatio = Mathf.Clamp01(planarSpeed / 6.5f);

            float targetNoise;
            if (!_characterController.isGrounded)
            {
                targetNoise = 0.18f;
            }
            else if (movementController != null && movementController.IsCrouching)
            {
                targetNoise = crouchNoise * moveRatio;
            }
            else if (movementController != null && movementController.IsSprinting)
            {
                targetNoise = Mathf.Lerp(walkNoise, sprintNoise, moveRatio);
            }
            else
            {
                targetNoise = walkNoise * moveRatio;
            }

            if (moveRatio <= 0.03f)
            {
                _noiseLevel = Mathf.MoveTowards(_noiseLevel, 0f, idleNoiseDecay * Time.deltaTime);
                return;
            }

            _noiseLevel = Mathf.Lerp(_noiseLevel, Mathf.Clamp01(targetNoise), 1f - Mathf.Exp(-noiseLerpSpeed * Time.deltaTime));
        }

        private void UpdateAlert()
        {
            var npcPressure = CalculateNpcPressure();
            var scanPressure = scanner != null && InvestigationScanner.IsScanActive ? 0.12f : 0f;
            if (Time.time > _lastSightPressureAt + 0.2f)
            {
                _sightPressure = Mathf.MoveTowards(_sightPressure, 0f, 1.4f * Time.deltaTime);
            }

            var targetAlert = Mathf.Clamp01((_noiseLevel * npcPressure) + scanPressure + _sightPressure);
            var speed = targetAlert > _alertLevel ? alertRiseSpeed : alertDecaySpeed;
            _alertLevel = Mathf.MoveTowards(_alertLevel, targetAlert, speed * Time.deltaTime);

            if (!_wasHighAlert && IsHighAlert)
            {
                _wasHighAlert = true;
                if (CaseSessionManager.Instance != null)
                {
                    CaseSessionManager.Instance.PublishMessage("Koridor dikkat cekiyor. Bir sure sessiz kalman gerekecek.");
                }
            }
            else if (_wasHighAlert && _alertLevel <= CalmInteractionThreshold * 0.52f)
            {
                _wasHighAlert = false;
                if (CaseSessionManager.Instance != null)
                {
                    CaseSessionManager.Instance.PublishMessage("Durum sakinlesti. NPC'lerle yeniden rahat konusabilirsin.");
                }
            }
        }

        private float CalculateNpcPressure()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, npcAwarenessRadius, NpcHits, ~0, QueryTriggerInteraction.Collide);
            var maxPressure = 0f;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = NpcHits[i];
                if (hit == null)
                {
                    continue;
                }

                var npc = hit.GetComponentInParent<NpcInteractable>();
                if (npc == null || !npc.isActiveAndEnabled)
                {
                    continue;
                }

                var distance = Vector3.Distance(transform.position, npc.transform.position);
                var normalized = 1f - Mathf.Clamp01(distance / npcAwarenessRadius);
                maxPressure = Mathf.Max(maxPressure, normalized);
            }

            return Mathf.Lerp(0.25f, 1f, maxPressure);
        }

        private void ResolveReferences()
        {
            if (movementController == null || !movementController.isActiveAndEnabled)
            {
                movementController = GetComponent<PrototypeFirstPersonController>();
            }

            if (scanner == null || !scanner.isActiveAndEnabled)
            {
                scanner = GetComponent<InvestigationScanner>();
            }
        }
    }
}
