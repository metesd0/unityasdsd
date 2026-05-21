using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MobilOfl.UI;
using MobilOfl.Online;

namespace MobilOfl.Gameplay
{
    public class InvestigationScanner : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInteractionController playerInteraction;
        [SerializeField] private Key scanKey = Key.Q;
        [SerializeField] private float scanRadius = 10f;
        [SerializeField] private float scanDuration = 2.35f;
        [SerializeField] private float scanCooldown = 6.5f;
        [SerializeField] private int maxReportedSignals = 3;
        [SerializeField] private MobileButton mobileScanButton;

        private static float _globalScanUntil;
        private static string _lastGlobalSummary = string.Empty;

        private readonly Collider[] _scanHits = new Collider[48];
        private readonly List<InteractableBase> _scanResults = new List<InteractableBase>(16);
        private float _nextScanAllowedAt;
        private Vector3 _lastScanOrigin;
        private PlayerStealthController _stealthController;

        public static bool IsScanActive => Time.time <= _globalScanUntil;
        public static string LastScanSummary => _lastGlobalSummary;

        public bool IsReady => Time.time >= _nextScanAllowedAt;
        public float CooldownRemaining => Mathf.Max(0f, _nextScanAllowedAt - Time.time);
        public float CooldownNormalized => scanCooldown <= 0.01f ? 0f : Mathf.Clamp01(CooldownRemaining / scanCooldown);

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();

            if (MainMenuHud.IsBlockingGameplay || CaseNotebookHud.IsAnyNotebookOpen)
            {
                return;
            }

            if (CaseSessionManager.Instance != null && CaseSessionManager.Instance.IsCaseResolved)
            {
                return;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                return;
            }

            var scanPressed =
                (Keyboard.current != null && Keyboard.current[scanKey].wasPressedThisFrame) ||
                (mobileScanButton != null && mobileScanButton.ConsumeWasPressedThisFrame());

            if (scanPressed)
            {
                TriggerScan();
            }
        }

        public bool TriggerScan()
        {
            var session = CaseSessionManager.Instance;
            if (session == null)
            {
                return false;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                return false;
            }

            if (!IsReady)
            {
                session.PublishMessage($"Tarama soguyor. {Mathf.CeilToInt(CooldownRemaining)} sn bekle.");
                return false;
            }

            _nextScanAllowedAt = Time.time + scanCooldown;
            _globalScanUntil = Time.time + scanDuration;
            if (_stealthController != null)
            {
                _stealthController.NotifyScannerPulse();
            }

            var summary = BuildScanSummary();
            _lastGlobalSummary = summary;
            session.PublishMessage(summary);
            MobilOfl.UI.ScreenEffectsController.TriggerScanPulse();
            return true;
        }

        public void SetMobileButton(MobileButton button)
        {
            mobileScanButton = button;
        }

        private string BuildScanSummary()
        {
            ResolveReferences();
            CollectSignals();

            if (playerInteraction != null && playerInteraction.CurrentInteractable != null)
            {
                var focus = playerInteraction.CurrentInteractable;
                if (!_scanResults.Contains(focus))
                {
                    _scanResults.Insert(0, focus);
                }
            }

            var evidenceCount = 0;
            var toolCount = 0;
            var npcCount = 0;
            var labels = new List<string>(maxReportedSignals);

            for (var i = 0; i < _scanResults.Count; i++)
            {
                var interactable = _scanResults[i];
                if (interactable == null)
                {
                    continue;
                }

                if (interactable is EvidenceInteractable evidence)
                {
                    if (!evidence.IsMarkerVisible)
                    {
                        continue;
                    }

                    evidenceCount++;
                    TryAddLabel(labels, FormatSignalLabel(evidence, evidence.MarkerLabel));
                    continue;
                }

                if (interactable is SearchSpotInteractable searchSpot)
                {
                    if (!searchSpot.IsMarkerVisible)
                    {
                        continue;
                    }

                    evidenceCount++;
                    TryAddLabel(labels, FormatSignalLabel(searchSpot, searchSpot.MarkerLabel));
                    continue;
                }

                if (interactable is ToolPickupInteractable toolPickup)
                {
                    if (!toolPickup.IsMarkerVisible)
                    {
                        continue;
                    }

                    toolCount++;
                    TryAddLabel(labels, FormatSignalLabel(toolPickup, toolPickup.MarkerLabel));
                    continue;
                }

                if (interactable is NpcInteractable npc)
                {
                    if (!npc.IsMarkerVisible)
                    {
                        continue;
                    }

                    npcCount++;
                    TryAddLabel(labels, FormatSignalLabel(npc, npc.MarkerLabel));
                }
            }

            if (evidenceCount == 0 && toolCount == 0 && npcCount == 0)
            {
                var nextStep = CaseSessionManager.Instance != null
                    ? CaseSessionManager.Instance.GetRecommendedNextStep()
                    : string.Empty;
                return string.IsNullOrWhiteSpace(nextStep)
                    ? "Tarama temiz. Yakinda yeni delil veya ekipman sinyali alinmadi."
                    : "Tarama temiz. Sonraki ipucu: " + nextStep;
            }

            var parts = new List<string>(3);
            if (evidenceCount > 0)
            {
                parts.Add($"{evidenceCount} delil izi");
            }

            if (toolCount > 0)
            {
                parts.Add($"{toolCount} ekipman noktasi");
            }

            if (npcCount > 0)
            {
                parts.Add($"{npcCount} sorgu noktasi");
            }

            var summary = "Tarama: " + string.Join(", ", parts) + " bulundu.";
            if (labels.Count > 0)
            {
                summary += " En yakinlar: " + string.Join(", ", labels) + ".";
            }

            return summary;
        }

        private void CollectSignals()
        {
            _scanResults.Clear();
            _lastScanOrigin = playerCamera != null
                ? playerCamera.transform.position + playerCamera.transform.forward * 1.6f
                : transform.position + transform.forward * 1.6f;

            var hitCount = Physics.OverlapSphereNonAlloc(_lastScanOrigin, scanRadius, _scanHits, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = _scanHits[i];
                if (hit == null)
                {
                    continue;
                }

                var interactable = hit.GetComponentInParent<InteractableBase>();
                if (interactable == null || !interactable.isActiveAndEnabled || _scanResults.Contains(interactable))
                {
                    continue;
                }

                _scanResults.Add(interactable);
            }

            _scanResults.Sort(CompareSignals);
        }

        private int CompareSignals(InteractableBase left, InteractableBase right)
        {
            var leftPriority = GetSignalPriority(left);
            var rightPriority = GetSignalPriority(right);
            if (leftPriority != rightPriority)
            {
                return leftPriority.CompareTo(rightPriority);
            }

            var leftDistance = left == null ? float.MaxValue : Vector3.SqrMagnitude(left.transform.position - _lastScanOrigin);
            var rightDistance = right == null ? float.MaxValue : Vector3.SqrMagnitude(right.transform.position - _lastScanOrigin);
            return leftDistance.CompareTo(rightDistance);
        }

        private static int GetSignalPriority(InteractableBase interactable)
        {
            if (interactable is EvidenceInteractable)
            {
                return 0;
            }

            if (interactable is SearchSpotInteractable)
            {
                return 0;
            }

            if (interactable is ToolPickupInteractable)
            {
                return 0;
            }

            if (interactable is NpcInteractable)
            {
                return 1;
            }

            return 2;
        }

        private void ResolveReferences()
        {
            if (playerInteraction == null || !playerInteraction.isActiveAndEnabled)
            {
                playerInteraction = GetComponent<PlayerInteractionController>();
                if (playerInteraction == null)
                {
                    playerInteraction = Object.FindAnyObjectByType<PlayerInteractionController>();
                }
            }

            if (playerCamera == null)
            {
                if (playerInteraction != null && playerInteraction.PlayerCamera != null)
                {
                    playerCamera = playerInteraction.PlayerCamera;
                }
                else
                {
                    playerCamera = GetComponentInChildren<Camera>();
                }
            }

            if (_stealthController == null || !_stealthController.isActiveAndEnabled)
            {
                _stealthController = GetComponent<PlayerStealthController>();
            }
        }

        private void TryAddLabel(List<string> labels, string candidate)
        {
            if (labels.Count >= maxReportedSignals || string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (!labels.Contains(candidate))
            {
                labels.Add(candidate);
            }
        }

        private string FormatSignalLabel(InteractableBase interactable, string label)
        {
            if (interactable == null || string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            var origin = playerCamera != null ? playerCamera.transform.position : transform.position;
            var distance = Vector3.Distance(origin, interactable.transform.position);
            return $"{label} ({distance:0}m)";
        }
    }
}
