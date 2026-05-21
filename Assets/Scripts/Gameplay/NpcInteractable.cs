using MobilOfl.Case;
using MobilOfl.Online;
using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class NpcInteractable : InteractableBase
    {
        [SerializeField] private CaseDefinition caseDefinition;
        [SerializeField] private string npcId = "npc.default";
        [SerializeField] private string npcDisplayName = "NPC";
        [SerializeField] private Color markerColor = default;
        [SerializeField] [TextArea] private string defaultLine = "Simdi konusamam.";
        [SerializeField] private string requiredEvidenceId;
        [SerializeField] [TextArea] private string evidenceLine = "Bunu soylemem gerekiyordu.";
        [SerializeField] private string witnessEvidenceId;
        [SerializeField] private bool collectWitnessEvidenceOnce = true;
        [SerializeField] private float interactionFocusDuration = 5.5f;

        private Transform _focusedInteractor;
        private float _focusUntil;

        public string NpcDisplayName => npcDisplayName;
        public string MarkerLabel => npcDisplayName;
        public Color MarkerColor => markerColor.a <= 0f ? new Color(1f, 0.78f, 0.3f, 1f) : markerColor;
        public bool IsMarkerVisible => isActiveAndEnabled && CaseSessionManager.Instance != null && !CaseSessionManager.Instance.IsCaseResolved;
        public bool IsInteractionFocused => _focusedInteractor != null && Time.time < _focusUntil;
        public Transform FocusedInteractor => IsInteractionFocused ? _focusedInteractor : null;

        public string NpcId => npcId;
        public string RequiredEvidenceId => requiredEvidenceId;
        public string WitnessEvidenceId => witnessEvidenceId;

        private void OnEnable()
        {
            RepairPlaceholderDialogueIfNeeded();
        }

        public void ConfigureDialogue(
            CaseDefinition sourceCase,
            string id,
            string displayName,
            string unavailableLine,
            string unlockedLine,
            string requiredEvidence,
            string witnessEvidence,
            Color color)
        {
            if (sourceCase != null)
            {
                caseDefinition = sourceCase;
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                npcId = id.Trim();
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                npcDisplayName = displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(unavailableLine))
            {
                defaultLine = unavailableLine.Trim();
            }

            if (!string.IsNullOrWhiteSpace(unlockedLine))
            {
                evidenceLine = unlockedLine.Trim();
            }

            requiredEvidenceId = string.IsNullOrWhiteSpace(requiredEvidence) ? string.Empty : requiredEvidence.Trim();
            witnessEvidenceId = string.IsNullOrWhiteSpace(witnessEvidence) ? string.Empty : witnessEvidence.Trim();
            if (color.a > 0f)
            {
                markerColor = color;
            }

            ConfigurePrompt("Konus: " + npcDisplayName);
        }

        private void Update()
        {
            if (!IsInteractionFocused)
            {
                return;
            }

            FaceInteractor(_focusedInteractor.gameObject);
        }

        public override bool TryInteract(GameObject interactor)
        {
            RepairPlaceholderDialogueIfNeeded();

            if (CaseSessionManager.Instance == null)
            {
                return false;
            }

            if (CaseSessionManager.Instance.IsCaseResolved)
            {
                CaseSessionManager.Instance.PublishMessage("Vaka tamamlandi. Artik yeni sorgu yapilamaz.");
                return false;
            }

            var stealth = interactor != null ? interactor.GetComponent<PlayerStealthController>() : null;
            if (stealth != null && !stealth.CanStartCalmConversation(out var stealthReason))
            {
                CaseSessionManager.Instance.PublishMessage(stealthReason);
                return false;
            }

            var hasRequiredEvidence =
                string.IsNullOrWhiteSpace(requiredEvidenceId) ||
                CaseSessionManager.Instance.HasEvidence(requiredEvidenceId);
            var witnessAlreadyCollected =
                !string.IsNullOrWhiteSpace(witnessEvidenceId) &&
                CaseSessionManager.Instance.HasEvidence(witnessEvidenceId);

            var line = hasRequiredEvidence
                ? evidenceLine
                : defaultLine;
            var revealsNewLead =
                hasRequiredEvidence &&
                !string.IsNullOrWhiteSpace(witnessEvidenceId) &&
                (!collectWitnessEvidenceOnce || !witnessAlreadyCollected);

            BeginInteractionFocus(interactor);

            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState != null && networkCaseState.IsOnlineSessionActive)
            {
                return networkCaseState.RequestNpcInteraction(
                    npcId,
                    npcDisplayName,
                    defaultLine,
                    evidenceLine,
                    requiredEvidenceId,
                    witnessEvidenceId,
                    collectWitnessEvidenceOnce);
            }

            CaseSessionManager.Instance.RegisterNpcConversation(npcId, npcDisplayName, line, revealsNewLead);

            if (!hasRequiredEvidence || string.IsNullOrWhiteSpace(witnessEvidenceId))
            {
                return true;
            }

            if (collectWitnessEvidenceOnce && witnessAlreadyCollected)
            {
                return true;
            }

            if (caseDefinition != null)
            {
                CaseSessionManager.Instance.TryCollectEvidence(caseDefinition, witnessEvidenceId);
            }

            return true;
        }

        private void FaceInteractor(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            var lookDirection = interactor.transform.position - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private void BeginInteractionFocus(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            _focusedInteractor = interactor.transform;
            _focusUntil = Time.time + Mathf.Max(1.5f, interactionFocusDuration);
            FaceInteractor(interactor);
        }

        private void RepairPlaceholderDialogueIfNeeded()
        {
            if (!LooksLikePlaceholderDialogue())
            {
                return;
            }

            var profile = ResolveFallbackProfile();
            ConfigureDialogue(
                CaseSessionManager.Instance != null ? CaseSessionManager.Instance.ActiveCase : caseDefinition,
                profile.Id,
                profile.DisplayName,
                profile.DefaultLine,
                profile.EvidenceLine,
                profile.RequiredEvidenceId,
                profile.WitnessEvidenceId,
                profile.MarkerColor);
        }

        private bool LooksLikePlaceholderDialogue()
        {
            return string.IsNullOrWhiteSpace(npcId) ||
                   npcId == "npc.default" ||
                   npcDisplayName == "NPC" ||
                   string.Equals(defaultLine, "Simdi konusamam.", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(evidenceLine, "Bunu soylemem gerekiyordu.", System.StringComparison.OrdinalIgnoreCase);
        }

        private NpcDialogueProfile ResolveFallbackProfile()
        {
            var key = (name + " " + npcDisplayName + " " + npcId).ToLowerInvariant();
            if (key.Contains("guvenlik") || key.Contains("gorevlisi") || key.Contains("guard") || key.Contains("npc_01"))
            {
                return FallbackProfiles[0];
            }

            if (key.Contains("arsiv") || key.Contains("archive") || key.Contains("npc_05"))
            {
                return FallbackProfiles[1];
            }

            if (key.Contains("kutuphane") || key.Contains("library") || key.Contains("npc_02"))
            {
                return FallbackProfiles[2];
            }

            if (key.Contains("ogretmen") || key.Contains("teacher") || key.Contains("npc_04"))
            {
                return FallbackProfiles[3];
            }

            if (key.Contains("kantin") || key.Contains("canteen") || key.Contains("npc_03"))
            {
                return FallbackProfiles[4];
            }

            return FallbackProfiles[0];
        }

        private static readonly NpcDialogueProfile[] FallbackProfiles =
        {
            new NpcDialogueProfile(
                "npc.guard",
                "Guvenlik Gorevlisi",
                "Nobet notunu gormeden kamera boslugunu net anlatamam.",
                "Nobet notu kamera boslugunu dogruluyor. Gece 22:15'te bilisim kulubu ogrencisi laboratuvar koridorundaydi.",
                "evidence.security-drawer-note",
                "evidence.guard-testimony",
                new Color(0.96f, 0.68f, 0.24f, 1f)),
            new NpcDialogueProfile(
                "npc.archive-clerk",
                "Arsiv Sorumlusu",
                "Defter olmadan arsiv odasi hakkinda resmi bir sey soyleyemem.",
                "Giris defterine gore bilisim kulubu ogrencisi sinavdan hemen once arsiv anahtarini sormustu.",
                "evidence.archive-ledger",
                string.Empty,
                new Color(0.78f, 0.64f, 1f, 1f)),
            new NpcDialogueProfile(
                "npc.library-student",
                "Kutuphane Ogrencisi",
                "Ben de supheli gorundugumu biliyorum ama o saatte bilgisayar rezervasyonum vardi.",
                "Oturum kaydina baktiniz mi? Ben 21:50-22:30 arasi kutuphanedeydim. Not bilisim kulubu ogrencisinin defterinden dustu.",
                "evidence.library-alibi",
                "evidence.student-testimony",
                new Color(0.25f, 0.82f, 1f, 1f)),
            new NpcDialogueProfile(
                "npc.teacher-assistant",
                "Ogretmen Yardimcisi",
                "Dolap anahtari kayboldu ama bunu herkes biliyor olabilir.",
                "Yedek anahtar bende degildi. Dolabin yanina en son bilisim kulubu ogrencisi geldi.",
                "evidence.locker-key",
                string.Empty,
                new Color(1f, 0.78f, 0.3f, 1f)),
            new NpcDialogueProfile(
                "npc.canteen-worker",
                "Kantin Calisani",
                "Gece gelen tek kisi ogretmen yardimcisiydi, baska birini gormedim.",
                "Tamam, fis saatini yanlis soyledim. Bilisim kulubu ogrencisi enerji icecegi alip laboratuvar tarafina kostu.",
                "evidence.canteen-receipt",
                "evidence.canteen-testimony",
                new Color(0.32f, 0.9f, 0.58f, 1f))
        };

        private struct NpcDialogueProfile
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string DefaultLine;
            public readonly string EvidenceLine;
            public readonly string RequiredEvidenceId;
            public readonly string WitnessEvidenceId;
            public readonly Color MarkerColor;

            public NpcDialogueProfile(
                string id,
                string displayName,
                string defaultLine,
                string evidenceLine,
                string requiredEvidenceId,
                string witnessEvidenceId,
                Color markerColor)
            {
                Id = id;
                DisplayName = displayName;
                DefaultLine = defaultLine;
                EvidenceLine = evidenceLine;
                RequiredEvidenceId = requiredEvidenceId;
                WitnessEvidenceId = witnessEvidenceId;
                MarkerColor = markerColor;
            }
        }
    }
}
