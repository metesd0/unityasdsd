using System;
using System.Collections.Generic;
using System.Linq;
using MobilOfl.Case;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MobilOfl.Gameplay
{
    public class CaseSessionManager : MonoBehaviour
    {
        [Serializable]
        public class ToolSnapshot
        {
            public string ToolId;
            public string DisplayName;
        }

        [Serializable]
        public class ConversationSnapshot
        {
            public string NpcId;
            public string NpcDisplayName;
            public string Line;
        }

        [Serializable]
        public class TeamNoteSnapshot
        {
            public string AuthorName;
            public string NoteText;
        }

        [Serializable]
        public class CaseSessionSnapshot
        {
            public string CaseId;
            public float ElapsedSeconds;
            public bool IsCaseResolved;
            public string ResultMessage;
            public List<string> EvidenceIds = new List<string>();
            public List<ToolSnapshot> Tools = new List<ToolSnapshot>();
            public List<ConversationSnapshot> Conversations = new List<ConversationSnapshot>();
            public List<TeamNoteSnapshot> TeamNotes = new List<TeamNoteSnapshot>();
        }

        public static CaseSessionManager Instance { get; private set; }

        [SerializeField] private CaseDefinition activeCase;

        private readonly HashSet<string> _collectedEvidenceIds = new HashSet<string>();
        private readonly HashSet<string> _unlockedToolIds = new HashSet<string>();
        private readonly Dictionary<string, EvidenceData> _evidenceById = new Dictionary<string, EvidenceData>();
        private readonly Dictionary<string, string> _toolDisplayNames = new Dictionary<string, string>();
        private readonly List<string> _messageHistory = new List<string>();
        private readonly List<string> _conversationHistory = new List<string>();
        private readonly List<string> _teamNotes = new List<string>();
        private readonly List<string> _inferenceHistory = new List<string>();
        private readonly List<ConversationSnapshot> _conversationSnapshots = new List<ConversationSnapshot>();
        private readonly List<TeamNoteSnapshot> _teamNoteSnapshots = new List<TeamNoteSnapshot>();
        private readonly HashSet<string> _interviewedNpcIds = new HashSet<string>();
        private readonly HashSet<string> _unlockedInferenceIds = new HashSet<string>();
        private float _caseStartedAt;
        private float _lastProgressAt;
        private string _lastResultMessage = string.Empty;

        public event Action<EvidenceData> EvidenceCollected;
        public event Action<CaseDefinition> CaseStarted;
        public event Action<string> SessionMessagePublished;
        public event Action<bool, string> CaseResolved;
        public event Action<string, string, string, bool> NpcConversationRegistered;
        public event Action<string, string> TeamNoteAdded;
        public event Action<string, string> ToolUnlocked;
        public event Action<string, string> InferenceUnlocked;
        public event Action SessionRestored;

        public CaseDefinition ActiveCase => activeCase;
        public IReadOnlyCollection<string> CollectedEvidenceIds => _collectedEvidenceIds;
        public IReadOnlyCollection<string> UnlockedToolIds => _unlockedToolIds;
        public IReadOnlyList<string> MessageHistory => _messageHistory;
        public IReadOnlyList<string> ConversationHistory => _conversationHistory;
        public IReadOnlyList<string> TeamNotes => _teamNotes;
        public IReadOnlyList<string> InferenceHistory => _inferenceHistory;
        public bool IsCaseResolved { get; private set; }
        public int InterviewedNpcCount => _interviewedNpcIds.Count;
        public float ElapsedCaseTimeSeconds => Mathf.Max(0f, Time.time - _caseStartedAt);
        public float LastProgressTime => _lastProgressAt;
        public int CollectedCriticalEvidenceCount => _collectedEvidenceIds.Count(IsCriticalEvidence);
        public int TotalCriticalEvidenceCount => _evidenceById.Values.Count(item => item != null && item.IsCritical);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (activeCase == null)
            {
                activeCase = ResolveDefaultCaseDefinition();
            }

            SetActiveCase(activeCase);
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetActiveCase(CaseDefinition caseDefinition)
        {
            if (caseDefinition == null)
            {
                caseDefinition = ResolveDefaultCaseDefinition();
            }

            activeCase = caseDefinition;
            _collectedEvidenceIds.Clear();
            _unlockedToolIds.Clear();
            _evidenceById.Clear();
            _toolDisplayNames.Clear();
            _messageHistory.Clear();
            _conversationHistory.Clear();
            _teamNotes.Clear();
            _inferenceHistory.Clear();
            _conversationSnapshots.Clear();
            _teamNoteSnapshots.Clear();
            _interviewedNpcIds.Clear();
            _unlockedInferenceIds.Clear();
            _lastResultMessage = string.Empty;
            IsCaseResolved = false;
            _caseStartedAt = Time.time;
            _lastProgressAt = Time.time;

            if (activeCase == null)
            {
                return;
            }

            foreach (var evidence in activeCase.EvidenceItems)
            {
                if (evidence == null || string.IsNullOrWhiteSpace(evidence.Id))
                {
                    continue;
                }

                _evidenceById[evidence.Id] = evidence;
            }

            CaseStarted?.Invoke(activeCase);
            PublishMessage(activeCase.OpeningBrief);
        }

        public void RestartCurrentCase()
        {
            SetActiveCase(activeCase);
        }

        public bool TryCollectEvidence(CaseDefinition sourceCase, string evidenceId)
        {
            if (IsCaseResolved)
            {
                PublishMessage("Vaka zaten tamamlandi.");
                return false;
            }

            if (activeCase == null || sourceCase != activeCase)
            {
                return false;
            }

            if (!_evidenceById.TryGetValue(evidenceId, out var evidence))
            {
                return false;
            }

            if (!_collectedEvidenceIds.Add(evidenceId))
            {
                return false;
            }

            EvidenceCollected?.Invoke(evidence);
            RegisterProgress();
            PublishMessage($"Delil toplandi: {evidence.Title}");
            EvaluateInferences();
            TryResolveCaseAutomatically(evidenceId);
            return true;
        }

        public bool TryApplyNetworkEvidence(string evidenceId, bool publishSyncMessage)
        {
            if (activeCase == null)
            {
                return false;
            }

            if (!_evidenceById.TryGetValue(evidenceId, out var evidence))
            {
                return false;
            }

            if (!_collectedEvidenceIds.Add(evidenceId))
            {
                return false;
            }

            EvidenceCollected?.Invoke(evidence);
            RegisterProgress();

            if (publishSyncMessage)
            {
                PublishMessage($"Takim delili senkronize edildi: {evidence.Title}");
            }

            EvaluateInferences();
            TryResolveCaseAutomatically(evidenceId);
            return true;
        }

        public bool TryUnlockTool(string toolId, string displayName, string pickupMessage)
        {
            if (IsCaseResolved)
            {
                PublishMessage("Vaka tamamlandi. Yeni ekipman toplanamaz.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(toolId))
            {
                return false;
            }

            if (!_unlockedToolIds.Add(toolId))
            {
                return false;
            }

            var resolvedName = ResolveToolDisplayName(toolId, displayName);
            _toolDisplayNames[toolId] = resolvedName;
            RegisterProgress();
            ToolUnlocked?.Invoke(toolId, resolvedName);
            PublishMessage(string.IsNullOrWhiteSpace(pickupMessage) ? $"Ekipman alindi: {resolvedName}" : pickupMessage);
            EvaluateInferences();
            return true;
        }

        public bool TryApplyNetworkTool(string toolId, string displayName, bool publishSyncMessage)
        {
            if (string.IsNullOrWhiteSpace(toolId))
            {
                return false;
            }

            if (!_unlockedToolIds.Add(toolId))
            {
                return false;
            }

            var resolvedName = ResolveToolDisplayName(toolId, displayName);
            _toolDisplayNames[toolId] = resolvedName;
            RegisterProgress();
            ToolUnlocked?.Invoke(toolId, resolvedName);

            if (publishSyncMessage)
            {
                PublishMessage($"Takim ekipmani senkronize edildi: {resolvedName}");
            }

            EvaluateInferences();
            return true;
        }

        public void PublishMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _messageHistory.Add(message);
            TrimHistory(_messageHistory, 12);
            SessionMessagePublished?.Invoke(message);
        }

        public void RegisterNpcConversation(string npcId, string npcDisplayName, string line, bool revealedLead)
        {
            if (string.IsNullOrWhiteSpace(npcDisplayName) || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var resolvedNpcId = string.IsNullOrWhiteSpace(npcId) ? npcDisplayName : npcId;
            _interviewedNpcIds.Add(resolvedNpcId);

            var conversationEntry = $"{npcDisplayName}: {line}";
            _conversationHistory.Add(conversationEntry);
            _conversationSnapshots.Add(new ConversationSnapshot
            {
                NpcId = resolvedNpcId,
                NpcDisplayName = npcDisplayName,
                Line = line
            });
            TrimHistory(_conversationHistory, 18);
            TrimHistory(_conversationSnapshots, 18);
            RegisterProgress();

            NpcConversationRegistered?.Invoke(resolvedNpcId, npcDisplayName, line, revealedLead);
            PublishMessage(revealedLead ? $"{conversationEntry} [Yeni ipucu]" : conversationEntry);
            EvaluateInferences();
        }

        public void ApplyNetworkConversation(string npcId, string npcDisplayName, string line)
        {
            if (string.IsNullOrWhiteSpace(npcDisplayName) || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var resolvedNpcId = string.IsNullOrWhiteSpace(npcId) ? npcDisplayName : npcId;
            if (!_interviewedNpcIds.Add(resolvedNpcId))
            {
                return;
            }

            var conversationEntry = $"{npcDisplayName}: {line}";
            _conversationHistory.Add(conversationEntry);
            _conversationSnapshots.Add(new ConversationSnapshot
            {
                NpcId = resolvedNpcId,
                NpcDisplayName = npcDisplayName,
                Line = line
            });
            TrimHistory(_conversationHistory, 18);
            TrimHistory(_conversationSnapshots, 18);
            RegisterProgress();
            PublishMessage($"{conversationEntry} [Takim senkronize]");
            EvaluateInferences();
        }

        public bool AddTeamNote(string authorName, string noteText)
        {
            if (string.IsNullOrWhiteSpace(noteText))
            {
                return false;
            }

            var trimmedNote = noteText.Trim();
            var resolvedAuthor = string.IsNullOrWhiteSpace(authorName) ? "Takim" : authorName.Trim();
            var noteEntry = $"{resolvedAuthor}: {trimmedNote}";

            _teamNotes.Add(noteEntry);
            _teamNoteSnapshots.Add(new TeamNoteSnapshot { AuthorName = resolvedAuthor, NoteText = trimmedNote });
            TrimHistory(_teamNotes, 18);
            TrimHistory(_teamNoteSnapshots, 18);
            RegisterProgress();
            TeamNoteAdded?.Invoke(resolvedAuthor, trimmedNote);
            PublishMessage($"{resolvedAuthor} yeni not ekledi.");
            EvaluateInferences();
            return true;
        }

        public bool ApplyNetworkTeamNote(string authorName, string noteText)
        {
            if (string.IsNullOrWhiteSpace(noteText))
            {
                return false;
            }

            var trimmedNote = noteText.Trim();
            var resolvedAuthor = string.IsNullOrWhiteSpace(authorName) ? "Takim" : authorName.Trim();
            var noteEntry = $"{resolvedAuthor}: {trimmedNote}";

            _teamNotes.Add(noteEntry);
            _teamNoteSnapshots.Add(new TeamNoteSnapshot { AuthorName = resolvedAuthor, NoteText = trimmedNote });
            TrimHistory(_teamNotes, 18);
            TrimHistory(_teamNoteSnapshots, 18);
            RegisterProgress();
            PublishMessage($"{resolvedAuthor} notu takimla senkronize edildi.");
            EvaluateInferences();
            return true;
        }

        public bool HasEvidence(string evidenceId)
        {
            return _collectedEvidenceIds.Contains(evidenceId);
        }

        public bool HasTool(string toolId)
        {
            return !string.IsNullOrWhiteSpace(toolId) && _unlockedToolIds.Contains(toolId);
        }

        public string GetToolDisplayName(string toolId)
        {
            return ResolveToolDisplayName(toolId, string.Empty);
        }

        public EvidenceData GetEvidence(string evidenceId)
        {
            _evidenceById.TryGetValue(evidenceId, out var evidence);
            return evidence;
        }

        public int GetSuspectEvidenceMatchCount(SuspectData suspect)
        {
            if (suspect == null)
            {
                return 0;
            }

            var collected = 0;
            foreach (var evidenceId in suspect.RequiredEvidenceIds)
            {
                if (HasEvidence(evidenceId))
                {
                    collected++;
                }
            }

            return collected;
        }

        public int GetSuspectConfidencePercent(string suspectId)
        {
            if (activeCase == null)
            {
                return 0;
            }

            var suspect = activeCase.Suspects.FirstOrDefault(item => item != null && item.Id == suspectId);
            if (suspect == null || suspect.RequiredEvidenceIds.Count == 0)
            {
                return 0;
            }

            return Mathf.RoundToInt((float)GetSuspectEvidenceMatchCount(suspect) / suspect.RequiredEvidenceIds.Count * 100f);
        }

        public string GetMissingEvidenceSummary(SuspectData suspect)
        {
            if (suspect == null || suspect.RequiredEvidenceIds.Count == 0)
            {
                return "Ek delil gerekmiyor.";
            }

            var missingEvidenceTitles = new List<string>();

            foreach (var evidenceId in suspect.RequiredEvidenceIds)
            {
                if (HasEvidence(evidenceId))
                {
                    continue;
                }

                var evidence = GetEvidence(evidenceId);
                missingEvidenceTitles.Add(evidence != null ? evidence.Title : evidenceId);
            }

            if (missingEvidenceTitles.Count == 0)
            {
                return "Tum gerekli deliller toplandi.";
            }

            return "Eksik delil: " + string.Join(", ", missingEvidenceTitles);
        }

        public bool HasInference(string inferenceId)
        {
            return !string.IsNullOrWhiteSpace(inferenceId) && _unlockedInferenceIds.Contains(inferenceId);
        }

        public string GetInferenceSummary()
        {
            if (_inferenceHistory.Count == 0)
            {
                return "Henuz zincir cikarimi yok. Birden fazla delil ayni noktayi isaret ettiginde analiz burada acilacak.";
            }

            return string.Join("\n", _inferenceHistory.Select(item => "- " + item));
        }

        public CaseSessionSnapshot CreateSnapshot()
        {
            var snapshot = new CaseSessionSnapshot
            {
                CaseId = activeCase != null ? activeCase.CaseId : string.Empty,
                ElapsedSeconds = ElapsedCaseTimeSeconds,
                IsCaseResolved = IsCaseResolved,
                ResultMessage = _lastResultMessage
            };

            snapshot.EvidenceIds.AddRange(_collectedEvidenceIds);

            foreach (var toolId in _unlockedToolIds)
            {
                snapshot.Tools.Add(new ToolSnapshot
                {
                    ToolId = toolId,
                    DisplayName = GetToolDisplayName(toolId)
                });
            }

            foreach (var conversation in _conversationSnapshots)
            {
                if (conversation == null)
                {
                    continue;
                }

                snapshot.Conversations.Add(new ConversationSnapshot
                {
                    NpcId = conversation.NpcId,
                    NpcDisplayName = conversation.NpcDisplayName,
                    Line = conversation.Line
                });
            }

            foreach (var note in _teamNoteSnapshots)
            {
                if (note == null)
                {
                    continue;
                }

                snapshot.TeamNotes.Add(new TeamNoteSnapshot
                {
                    AuthorName = note.AuthorName,
                    NoteText = note.NoteText
                });
            }

            return snapshot;
        }

        public bool RestoreSnapshot(CaseSessionSnapshot snapshot, bool publishMessage)
        {
            if (snapshot == null || activeCase == null || snapshot.CaseId != activeCase.CaseId)
            {
                return false;
            }

            _collectedEvidenceIds.Clear();
            _unlockedToolIds.Clear();
            _toolDisplayNames.Clear();
            _messageHistory.Clear();
            _conversationHistory.Clear();
            _teamNotes.Clear();
            _inferenceHistory.Clear();
            _conversationSnapshots.Clear();
            _teamNoteSnapshots.Clear();
            _interviewedNpcIds.Clear();
            _unlockedInferenceIds.Clear();

            foreach (var evidenceId in snapshot.EvidenceIds)
            {
                if (!string.IsNullOrWhiteSpace(evidenceId) && _evidenceById.ContainsKey(evidenceId))
                {
                    _collectedEvidenceIds.Add(evidenceId);
                }
            }

            foreach (var tool in snapshot.Tools)
            {
                if (tool == null || string.IsNullOrWhiteSpace(tool.ToolId))
                {
                    continue;
                }

                _unlockedToolIds.Add(tool.ToolId);
                _toolDisplayNames[tool.ToolId] = ResolveToolDisplayName(tool.ToolId, tool.DisplayName);
            }

            foreach (var conversation in snapshot.Conversations)
            {
                if (conversation == null || string.IsNullOrWhiteSpace(conversation.NpcDisplayName) || string.IsNullOrWhiteSpace(conversation.Line))
                {
                    continue;
                }

                var resolvedNpcId = string.IsNullOrWhiteSpace(conversation.NpcId) ? conversation.NpcDisplayName : conversation.NpcId;
                _interviewedNpcIds.Add(resolvedNpcId);
                _conversationHistory.Add($"{conversation.NpcDisplayName}: {conversation.Line}");
                _conversationSnapshots.Add(new ConversationSnapshot
                {
                    NpcId = resolvedNpcId,
                    NpcDisplayName = conversation.NpcDisplayName,
                    Line = conversation.Line
                });
            }

            foreach (var note in snapshot.TeamNotes)
            {
                if (note == null || string.IsNullOrWhiteSpace(note.NoteText))
                {
                    continue;
                }

                var resolvedAuthor = string.IsNullOrWhiteSpace(note.AuthorName) ? "Takim" : note.AuthorName.Trim();
                var trimmedNote = note.NoteText.Trim();
                _teamNotes.Add($"{resolvedAuthor}: {trimmedNote}");
                _teamNoteSnapshots.Add(new TeamNoteSnapshot { AuthorName = resolvedAuthor, NoteText = trimmedNote });
            }

            TrimHistory(_conversationHistory, 18);
            TrimHistory(_conversationSnapshots, 18);
            TrimHistory(_teamNotes, 18);
            TrimHistory(_teamNoteSnapshots, 18);

            _caseStartedAt = Time.time - Mathf.Max(0f, snapshot.ElapsedSeconds);
            _lastResultMessage = snapshot.ResultMessage ?? string.Empty;
            IsCaseResolved = snapshot.IsCaseResolved;
            RegisterProgress();
            EvaluateInferences(false);
            SessionRestored?.Invoke();

            if (publishMessage)
            {
                PublishMessage("Kayit yuklendi. Dosya kaldigin yerden devam ediyor.");
            }

            return true;
        }

        public string GetRecommendedNextStep()
        {
            if (activeCase == null)
            {
                return "Aktif vaka bulunamadi.";
            }

            if (IsCaseResolved)
            {
                return "Vaka tamamlandi. Sonuc ekranini incele.";
            }

            if (!HasEvidence("evidence.security-log"))
            {
                return "Guvenlik odasina git ve gece hareket kaydini topla.";
            }

            if (!HasEvidence("evidence.answer-key-note"))
            {
                return "Kutuphane masasini tara; not kagidi ilk fiziksel izi verecek.";
            }

            if (!HasEvidence("evidence.library-alibi"))
            {
                return "Bilisim kulubundeki okul ag oturum kaydini bul. Supheli duran ogrencinin mazereti orada netlesecek.";
            }

            if (!HasEvidence("evidence.student-testimony"))
            {
                return "Kutuphane ogrencisiyle tekrar konus; notun kaynagi netlesecek.";
            }

            if (!HasEvidence("evidence.canteen-receipt"))
            {
                return "Kantin kasasindaki fislere bak. Ilk ifade bilerek yanlis yonlendirme olabilir.";
            }

            if (!HasEvidence("evidence.canteen-testimony"))
            {
                return "Kantin calisaniyla fis uzerinden tekrar konus. Yalan ifade burada kirilacak.";
            }

            if (!HasTool("tool.archive-pass"))
            {
                return "Kantin ifadesinden sonra arsiv gecis kartini al. Arsiv raflari bu kart olmadan acilmaz.";
            }

            if (!HasEvidence("evidence.archive-ledger"))
            {
                return "Arsiv gecis kartini kullanip arsiv kanadina gir. Gizli kutuyu arayip giris defterini ortaya cikar.";
            }

            if (!HasTool("tool.lockpick"))
            {
                return "Guvenlik odasina geri don ve ekipman dolabindan maymuncuk setini al. Zor cekmeceyi bununla acacaksin.";
            }

            if (!HasEvidence("evidence.locker-key"))
            {
                return "Maymuncuk setiyle ogretmenler odasindaki cekmeceyi ara; yedek anahtar erisim zincirini tamamlayacak.";
            }

            if (!HasEvidence("evidence.security-drawer-note"))
            {
                return "Guvenlik odasindaki kilitli cekmeceyi maymuncukla ac. Nobet notu son zaman boslugunu kapatacak.";
            }

            if (!HasEvidence("evidence.guard-testimony"))
            {
                return "Guvenlik gorevlisine nobet notuyla geri don. Kamera kaydi artik net ifade acacak.";
            }

            var readySuspect = activeCase.Suspects.FirstOrDefault(item => item != null && CanAccuse(item.Id));
            if (readySuspect != null)
            {
                return $"{readySuspect.DisplayName} icin yeterli delil var. Guvenlik gorevlisinin son ifadesiyle vaka kapanacak.";
            }

            return "Suphelilerin eksik delillerini dosyada karsilastir ve son ipuclarini topla.";
        }

        public bool CanAccuse(string suspectId)
        {
            if (activeCase == null || IsCaseResolved)
            {
                return false;
            }

            var suspect = activeCase.Suspects.FirstOrDefault(item => item != null && item.Id == suspectId);
            if (suspect == null)
            {
                return false;
            }

            return suspect.RequiredEvidenceIds.All(HasEvidence);
        }

        public bool HasAnyAccusableSuspect()
        {
            if (activeCase == null || IsCaseResolved)
            {
                return false;
            }

            foreach (var suspect in activeCase.Suspects)
            {
                if (suspect != null && CanAccuse(suspect.Id))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveCase(string suspectId, out string resultMessage)
        {
            resultMessage = "Final karar icin supheliyle birlikte motivasyon ve olay sirasi da secilmeli.";
            PublishMessage(resultMessage);
            CaseResolved?.Invoke(false, resultMessage);
            return false;
        }

        public bool TryResolveCase(string suspectId, string motive, string timeline, out string resultMessage)
        {
            resultMessage = string.Empty;

            if (activeCase == null)
            {
                resultMessage = "Aktif vaka bulunamadi.";
                PublishMessage(resultMessage);
                CaseResolved?.Invoke(false, resultMessage);
                return false;
            }

            if (IsCaseResolved)
            {
                resultMessage = "Vaka zaten tamamlandi.";
                PublishMessage(resultMessage);
                return false;
            }

            if (!CanAccuse(suspectId))
            {
                resultMessage = "Bu supheliyi suclamak icin yeterli delil toplanmadi.";
                PublishMessage(resultMessage);
                CaseResolved?.Invoke(false, resultMessage);
                return false;
            }

            var correctSuspect = suspectId == activeCase.CulpritSuspectId;
            var correctMotive = IsFinalAnswerMatch(motive, activeCase.CulpritMotive);
            var correctTimeline = IsFinalAnswerMatch(timeline, activeCase.CulpritTimeline);

            if (correctSuspect && correctMotive && correctTimeline)
            {
                resultMessage =
                    $"Dogru karar. Motivasyon: {activeCase.CulpritMotive}\nZaman cizelgesi: {activeCase.CulpritTimeline}";
                IsCaseResolved = true;
                _lastResultMessage = resultMessage;
                RegisterProgress();
                PublishMessage(resultMessage);
                CaseResolved?.Invoke(true, resultMessage);
                return true;
            }

            if (!correctSuspect)
            {
                resultMessage = "Yanlis supheli secildi.";
            }
            else if (!correctMotive && !correctTimeline)
            {
                resultMessage = "Supheli dogru olabilir, ama motivasyon ve olay sirasi kanitlarla uyusmuyor.";
            }
            else if (!correctMotive)
            {
                resultMessage = "Supheli dogru olabilir, ama motivasyon kanitlarla uyusmuyor.";
            }
            else
            {
                resultMessage = "Supheli dogru olabilir, ama olay sirasi kanitlarla uyusmuyor.";
            }

            PublishMessage(resultMessage);
            CaseResolved?.Invoke(false, resultMessage);
            return false;
        }

        public void ApplyNetworkResolution(bool success, string resultMessage)
        {
            if (success)
            {
                IsCaseResolved = true;
                _lastResultMessage = resultMessage;
            }

            RegisterProgress();
            PublishMessage(resultMessage);
            CaseResolved?.Invoke(success, resultMessage);
        }

        public string GetReasoningSummary()
        {
            if (activeCase == null)
            {
                return "Aktif vaka yok. Analiz olusturulamiyor.";
            }

            var segments = new List<string>();

            if (HasEvidence("evidence.security-log"))
            {
                segments.Add("Guvenlik kaydi, gece laboratuvar koridorunda planli bir hareket oldugunu dogruluyor.");
            }

            if (HasEvidence("evidence.answer-key-note"))
            {
                segments.Add("Kutuphanedeki not, soru sizintisinin fiziksel olarak elde dolastigini gosteriyor.");
            }

            if (HasEvidence("evidence.library-alibi"))
            {
                segments.Add("Kutuphane oturum kaydi, kutuphanedeki supheli ogrencinin olay aninda yanlis hedef oldugunu gosteriyor.");
            }

            if (HasEvidence("evidence.student-testimony"))
            {
                segments.Add("Kutuphane tanigi notu dogrudan bilisim kulubu ogrencisine bagliyor.");
            }

            if (HasEvidence("evidence.guard-testimony"))
            {
                segments.Add("Guvenlik ifadesi, suphelinin gece koridorda bulundugunu insan tanikla destekliyor.");
            }

            if (HasEvidence("evidence.archive-ledger"))
            {
                segments.Add("Arsiv defteri, suphelinin onceki gunlerde de hassas erisim yollarini aradigini gosteriyor.");
            }

            if (HasEvidence("evidence.canteen-testimony"))
            {
                segments.Add("Kantin ifadesi, olay saatine yakin hizli ve amacli hareket zincirini tamamliyor.");
            }

            if (HasEvidence("evidence.canteen-receipt"))
            {
                segments.Add("Kantin fisi, ilk anlatilan hikayede saat ve urun tutarsizligi oldugunu aciga cikariyor.");
            }

            if (HasEvidence("evidence.locker-key"))
            {
                segments.Add("Yedek anahtar, soru dolabina fiziksel erisimin nasil saglandigini acikliyor.");
            }

            if (HasEvidence("evidence.security-drawer-note"))
            {
                segments.Add("Guvenlik cekmecesi notu, kamera kaydindaki boslugu ve nobet degisimini dogruluyor.");
            }

            if (HasTool("tool.archive-pass"))
            {
                segments.Add("Arsiv gecis karti, kisitli okul alanlarina planli erisim saglandigini gosteriyor.");
            }

            if (HasTool("tool.lockpick"))
            {
                segments.Add("Maymuncuk seti, kilitli cekmece ve dolaplarin zorlanarak acilabildigini kanitliyor.");
            }

            if (segments.Count == 0)
            {
                return "Henuz yeterli veri yok. Ilk delili toplayip olay zincirini kurmaya basla.";
            }

            if (_inferenceHistory.Count > 0)
            {
                segments.Insert(0, "Acilan cikarimlar: " + string.Join(" ", _inferenceHistory));
            }

            if (HasAnyAccusableSuspect())
            {
                segments.Add("Toplanan deliller artik net bir zaman, mekan ve erisim zinciri kuruyor.");
            }

            return string.Join(" ", segments);
        }

        private void TryResolveCaseAutomatically(string latestEvidenceId)
        {
            if (activeCase == null || IsCaseResolved || latestEvidenceId != "evidence.guard-testimony")
            {
                return;
            }

            if (!CanAccuse(activeCase.CulpritSuspectId))
            {
                return;
            }

            var suspect = activeCase.Suspects.FirstOrDefault(item => item != null && item.Id == activeCase.CulpritSuspectId);
            var suspectName = suspect != null ? suspect.DisplayName : "Bilisim Kulubu Ogrencisi";
            var resultMessage =
                "Vaka cozuldu.\n" +
                $"Suclu: {suspectName}\n" +
                $"Motivasyon: {activeCase.CulpritMotive}\n" +
                $"Olay sirasi: {activeCase.CulpritTimeline}";

            IsCaseResolved = true;
            _lastResultMessage = resultMessage;
            RegisterProgress();
            PublishMessage(resultMessage);
            CaseResolved?.Invoke(true, resultMessage);
        }

        private void RegisterProgress()
        {
            _lastProgressAt = Time.time;
        }

        private void EvaluateInferences(bool publishMessages = true)
        {
            AddInferenceIf(
                "inference.camera-corridor",
                HasEvidence("evidence.security-log") && HasEvidence("evidence.guard-testimony"),
                "Kamera kaydi ve guvenlik ifadesi ayni koridor zaman cizgisini dogruluyor.",
                publishMessages);

            AddInferenceIf(
                "inference.note-owner",
                HasEvidence("evidence.answer-key-note") &&
                HasEvidence("evidence.student-testimony") &&
                HasEvidence("evidence.library-alibi"),
                "Kutuphanedeki not bilisim kulubu ogrencisinin defteriyle baglaniyor; kutuphane ogrencisi yanlis hedef olmaktan cikiyor.",
                publishMessages);

            AddInferenceIf(
                "inference.access-chain",
                HasTool("tool.archive-pass") && HasEvidence("evidence.archive-ledger"),
                "Arsiv karti ve giris defteri kisitli alanlara planli erisim zinciri kuruyor.",
                publishMessages);

            AddInferenceIf(
                "inference.physical-access",
                HasTool("tool.lockpick") && HasEvidence("evidence.locker-key"),
                "Maymuncuk seti ve yedek anahtar dolap erisiminin nasil saglandigini acikliyor.",
                publishMessages);

            AddInferenceIf(
                "inference.motive-window",
                HasEvidence("evidence.canteen-testimony") &&
                HasEvidence("evidence.canteen-receipt") &&
                HasEvidence("evidence.archive-ledger") &&
                HasEvidence("evidence.security-drawer-note") &&
                HasEvidence("evidence.security-log"),
                "Kantin fisi, ifade, arsiv defteri, nobet notu ve kamera kaydi olay saatine yakin hareket zincirini tamamliyor.",
                publishMessages);
        }

        private void AddInferenceIf(string inferenceId, bool condition, string summary, bool publishMessages)
        {
            if (!condition || !_unlockedInferenceIds.Add(inferenceId))
            {
                return;
            }

            _inferenceHistory.Add(summary);
            TrimHistory(_inferenceHistory, 10);
            RegisterProgress();
            if (publishMessages)
            {
                InferenceUnlocked?.Invoke(inferenceId, summary);
                PublishMessage("Yeni cikarim: " + summary);
            }
        }

        private bool IsCriticalEvidence(string evidenceId)
        {
            return _evidenceById.TryGetValue(evidenceId, out var evidence) && evidence != null && evidence.IsCritical;
        }

        private static bool IsFinalAnswerMatch(string selected, string expected)
        {
            return string.Equals(
                NormalizeFinalAnswer(selected),
                NormalizeFinalAnswer(expected),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFinalAnswer(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private string ResolveToolDisplayName(string toolId, string preferredDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(preferredDisplayName))
            {
                return preferredDisplayName.Trim();
            }

            if (_toolDisplayNames.TryGetValue(toolId, out var cachedDisplayName) && !string.IsNullOrWhiteSpace(cachedDisplayName))
            {
                return cachedDisplayName;
            }

            return toolId switch
            {
                "tool.archive-pass" => "Arsiv Gecis Karti",
                "tool.lockpick" => "Maymuncuk Seti",
                _ => string.IsNullOrWhiteSpace(toolId) ? "Ekipman" : toolId
            };
        }

        private static void TrimHistory<T>(List<T> history, int maxEntries)
        {
            while (history.Count > maxEntries)
            {
                history.RemoveAt(0);
            }
        }

        private static CaseDefinition ResolveDefaultCaseDefinition()
        {
            var resourceCase = Resources.Load<CaseDefinition>("Cases/ExamTheftCase");
            if (resourceCase != null)
            {
                return resourceCase;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CaseDefinition>("Assets/Data/Cases/ExamTheftCase.asset");
#else
            return null;
#endif
        }
    }
}
