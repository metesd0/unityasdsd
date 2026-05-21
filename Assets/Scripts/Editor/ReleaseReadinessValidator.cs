using System.Collections.Generic;
using System.IO;
using System.Linq;
using MobilOfl.Case;
using MobilOfl.Gameplay;
using MobilOfl.Online;
using MobilOfl.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MobilOfl.EditorTools
{
    public static class ReleaseReadinessValidator
    {
        private const string CaseAssetPath = "Assets/Data/Cases/ExamTheftCase.asset";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [MenuItem("Mobil OFL/Validation/Run Release Readiness Check")]
        public static void RunReleaseReadinessCheck()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            ValidateScene(errors, warnings);
            ValidateCaseData(errors, warnings);
            ValidateCoreComponents(errors, warnings);
            ValidateInvestigationFlow(errors, warnings);
            ValidateOnlinePrototype(errors, warnings);
            ValidateMobileReleaseSettings(errors, warnings);

            var report = BuildReport(errors, warnings);
            if (errors.Count > 0)
            {
                Debug.LogError(report);
            }
            else if (warnings.Count > 0)
            {
                Debug.LogWarning(report);
            }
            else
            {
                Debug.Log(report);
            }

            EditorUtility.DisplayDialog(
                "Mobil OFL Release Readiness",
                errors.Count == 0
                    ? $"Kontrol tamamlandi. Hata yok, uyari sayisi: {warnings.Count}."
                    : $"Kontrol tamamlandi. Hata: {errors.Count}, uyari: {warnings.Count}. Detaylar Console'da.",
                "Tamam");
        }

        [MenuItem("Mobil OFL/Validation/Repair Active Scene Release Blockers")]
        public static void RepairActiveSceneReleaseBlockers()
        {
            CaseSceneAutoSetupTool.AutoSetupInvestigationScene();
            RunReleaseReadinessCheck();
        }

        private static void ValidateScene(List<string> errors, List<string> warnings)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                errors.Add("Aktif sahne kaydedilmis bir Unity sahnesi degil.");
                return;
            }

            var buildScene = EditorBuildSettings.scenes.FirstOrDefault(item => item.path == activeScene.path);
            if (buildScene == null)
            {
                errors.Add($"Aktif sahne Build Settings listesinde yok: {activeScene.path}");
            }
            else if (!buildScene.enabled)
            {
                errors.Add($"Aktif sahne Build Settings icinde pasif: {activeScene.path}");
            }

            if (QualitySettings.renderPipeline == null)
            {
                warnings.Add("Aktif Quality Settings render pipeline atamasi bos gorunuyor. URP asset atamasini kontrol et.");
            }
        }

        private static void ValidateCaseData(List<string> errors, List<string> warnings)
        {
            var caseDefinition = AssetDatabase.LoadAssetAtPath<CaseDefinition>(CaseAssetPath);
            if (caseDefinition == null)
            {
                errors.Add($"Vaka asset'i bulunamadi: {CaseAssetPath}");
                return;
            }

            if (caseDefinition.EvidenceItems.Count < 6)
            {
                warnings.Add("MVP vaka icin delil sayisi dusuk. Hedef en az 6-8 delil.");
            }

            if (caseDefinition.Suspects.Count < 3)
            {
                warnings.Add("MVP vaka icin supheli sayisi dusuk. Hedef en az 3 supheli.");
            }

            var evidenceIds = new HashSet<string>();
            foreach (var evidence in caseDefinition.EvidenceItems)
            {
                if (evidence == null || string.IsNullOrWhiteSpace(evidence.Id))
                {
                    errors.Add("Vaka asset'inde bos delil kaydi var.");
                    continue;
                }

                if (!evidenceIds.Add(evidence.Id))
                {
                    errors.Add($"Tekrarlanan delil id'si: {evidence.Id}");
                }
            }

            foreach (var suspect in caseDefinition.Suspects)
            {
                if (suspect == null || string.IsNullOrWhiteSpace(suspect.Id))
                {
                    errors.Add("Vaka asset'inde bos supheli kaydi var.");
                    continue;
                }

                foreach (var requiredEvidenceId in suspect.RequiredEvidenceIds)
                {
                    if (!evidenceIds.Contains(requiredEvidenceId))
                    {
                        errors.Add($"{suspect.DisplayName} icin tanimsiz gerekli delil: {requiredEvidenceId}");
                    }
                }
            }

            if (caseDefinition.Suspects.All(item => item == null || item.Id != caseDefinition.CulpritSuspectId))
            {
                errors.Add($"Culprit suspect id supheli listesinde yok: {caseDefinition.CulpritSuspectId}");
            }

            ValidateFinalAccusationOptions(caseDefinition, errors, warnings);

            ValidateNarrativeEvidenceChain(caseDefinition, evidenceIds, errors, warnings);
        }

        private static void ValidateFinalAccusationOptions(
            CaseDefinition caseDefinition,
            List<string> errors,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(caseDefinition.CulpritMotive))
            {
                errors.Add("Dogru motivasyon metni bos.");
            }

            if (string.IsNullOrWhiteSpace(caseDefinition.CulpritTimeline))
            {
                errors.Add("Dogru olay sirasi metni bos.");
            }

            if (caseDefinition.MotiveOptions.Count < 2)
            {
                warnings.Add("Final karar icin motivasyon secenegi az. Dogru cevapla birlikte en az 2-3 secenek hedefle.");
            }

            if (caseDefinition.TimelineOptions.Count < 2)
            {
                warnings.Add("Final karar icin olay sirasi secenegi az. Dogru cevapla birlikte en az 2-3 secenek hedefle.");
            }

            if (!ContainsOption(caseDefinition.MotiveOptions, caseDefinition.CulpritMotive))
            {
                errors.Add("Dogru motivasyon final secenekleri icinde yok.");
            }

            if (!ContainsOption(caseDefinition.TimelineOptions, caseDefinition.CulpritTimeline))
            {
                errors.Add("Dogru olay sirasi final secenekleri icinde yok.");
            }

            AddDuplicateOptionWarnings("motivasyon", caseDefinition.MotiveOptions, warnings);
            AddDuplicateOptionWarnings("olay sirasi", caseDefinition.TimelineOptions, warnings);
        }

        private static bool ContainsOption(IReadOnlyList<string> options, string expected)
        {
            if (options == null || string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            return options.Any(option => string.Equals(
                option?.Trim(),
                expected.Trim(),
                System.StringComparison.OrdinalIgnoreCase));
        }

        private static void AddDuplicateOptionWarnings(
            string label,
            IReadOnlyList<string> options,
            List<string> warnings)
        {
            if (options == null)
            {
                return;
            }

            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    warnings.Add($"Final {label} seceneklerinde bos satir var.");
                    continue;
                }

                if (!seen.Add(option.Trim()))
                {
                    warnings.Add($"Final {label} seceneklerinde tekrarlanan cevap var: {option.Trim()}");
                }
            }
        }

        private static void ValidateNarrativeEvidenceChain(
            CaseDefinition caseDefinition,
            HashSet<string> evidenceIds,
            List<string> errors,
            List<string> warnings)
        {
            var expectedEvidenceIds = new[]
            {
                "evidence.security-log",
                "evidence.answer-key-note",
                "evidence.guard-testimony",
                "evidence.student-testimony",
                "evidence.archive-ledger",
                "evidence.canteen-testimony",
                "evidence.locker-key"
            };

            foreach (var expectedEvidenceId in expectedEvidenceIds)
            {
                if (!evidenceIds.Contains(expectedEvidenceId))
                {
                    errors.Add($"Vaka cikarim zinciri icin eksik delil id'si: {expectedEvidenceId}");
                }
            }

            var criticalEvidenceCount = caseDefinition.EvidenceItems.Count(item => item != null && item.IsCritical);
            if (criticalEvidenceCount < 4)
            {
                warnings.Add("Vaka akisi icin kritik delil sayisi dusuk. Final kararini desteklemek icin en az 4 kritik delil hedefle.");
            }

            var culprit = caseDefinition.Suspects.FirstOrDefault(item => item != null && item.Id == caseDefinition.CulpritSuspectId);
            if (culprit == null)
            {
                return;
            }

            if (culprit.RequiredEvidenceIds.Count < 4)
            {
                warnings.Add("Dogru supheli icin gerekli delil sayisi dusuk. Yanlis pozitifleri azaltmak icin en az 4 delil bagla.");
            }

            var expectedCulpritEvidence = new[]
            {
                "evidence.security-log",
                "evidence.answer-key-note",
                "evidence.archive-ledger",
                "evidence.locker-key"
            };

            foreach (var evidenceId in expectedCulpritEvidence)
            {
                if (!culprit.RequiredEvidenceIds.Contains(evidenceId))
                {
                    warnings.Add($"Dogru suphelinin kanit zincirinde beklenen delil yok: {evidenceId}");
                }
            }
        }

        private static void ValidateCoreComponents(List<string> errors, List<string> warnings)
        {
            RequireComponent<CaseSessionManager>("CaseSessionManager", errors);
            RequireComponent<PrototypeFirstPersonController>("PrototypeFirstPersonController", errors);
            RequireComponent<PlayerInteractionController>("PlayerInteractionController", errors);
            RequireComponent<CaseProgressTracker>("CaseProgressTracker", warnings);
            RequireComponent<MainMenuHud>("MainMenuHud", errors);
            RequireComponent<UguiMainMenuHud>("UguiMainMenuHud", warnings);
            RequireComponent<UguiGameplayHud>("UguiGameplayHud", warnings);
            RequireComponent<CaseNotebookHud>("CaseNotebookHud", errors);
            RequireComponent<UguiNotebookHud>("UguiNotebookHud", warnings);
        }

        private static void ValidateInvestigationFlow(List<string> errors, List<string> warnings)
        {
            var evidence = Object.FindObjectsByType<EvidenceInteractable>(FindObjectsInactive.Include);
            var searchSpots = Object.FindObjectsByType<SearchSpotInteractable>(FindObjectsInactive.Include);
            var npcs = Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Include);
            var tools = Object.FindObjectsByType<ToolPickupInteractable>(FindObjectsInactive.Include);

            RequireTool("tool.archive-pass", tools, errors);
            RequireTool("tool.lockpick", tools, errors);
            RequireSearchGate("evidence.archive-ledger", "tool.archive-pass", searchSpots, errors);
            RequireSearchGate("evidence.locker-key", "tool.lockpick", searchSpots, errors);

            if (evidence.Length < 2)
            {
                errors.Add("Sahnede temel EvidenceInteractable sayisi dusuk. Guvenlik kaydi ve cevap notu sahnede olmali.");
            }

            if (searchSpots.Length < 2)
            {
                errors.Add("Sahnede arama noktasi sayisi dusuk. Arsiv defteri ve yedek anahtar arama noktasi olmali.");
            }

            if (npcs.Length < 5)
            {
                warnings.Add("MVP akisi icin sahnede 5 NPC bekleniyor: guvenlik, kutuphane, ogretmen yardimcisi, arsiv, kantin.");
            }
        }

        private static void ValidateOnlinePrototype(List<string> errors, List<string> warnings)
        {
            RequireComponent<NetworkManager>("NetworkManager", warnings);
            RequireComponent<NetworkCaseState>("NetworkCaseState", warnings);
            RequireComponent<RelayNetworkBootstrap>("RelayNetworkBootstrap", warnings);
        }

        private static void ValidateMobileReleaseSettings(List<string> errors, List<string> warnings)
        {
            if (!File.Exists(ProjectSettingsPath))
            {
                warnings.Add("ProjectSettings.asset okunamadi. Android release ayarlari kontrol edilemedi.");
                return;
            }

            var projectSettings = File.ReadAllText(ProjectSettingsPath);

            if (projectSettings.Contains("Android: com.mobilofl.prototype"))
            {
                warnings.Add("Android application id hala prototype paket adini kullaniyor: com.mobilofl.prototype");
            }

            if (projectSettings.Contains("AndroidKeystoreName: \n") ||
                projectSettings.Contains("AndroidKeystoreName: \r\n"))
            {
                warnings.Add("Android release icin keystore atanmamis.");
            }

            if (projectSettings.Contains("m_BuildTargetIcons: []"))
            {
                warnings.Add("Build target ikonlari bos. Store/release icin uygulama ikonlari hazirlanmali.");
            }

            if (projectSettings.Contains("AndroidTargetSdkVersion: 0"))
            {
                warnings.Add("Android target SDK otomatik ayarda. Store gonderimi oncesi hedef SDK bilincli secilmeli.");
            }

            if (projectSettings.Contains("bundleVersion: 0.0.0") ||
                projectSettings.Contains("bundleVersion: 0.1.0"))
            {
                warnings.Add("Bundle version erken prototip degerinde gorunuyor. Release surumleme planini kontrol et.");
            }
        }

        private static void RequireComponent<T>(string label, List<string> target) where T : Object
        {
            if (Object.FindAnyObjectByType<T>(FindObjectsInactive.Include) == null)
            {
                target.Add($"Eksik sahne komponenti: {label}");
            }
        }

        private static void RequireTool(string toolId, IReadOnlyList<ToolPickupInteractable> tools, List<string> errors)
        {
            if (tools.Any(tool => tool != null && tool.ToolId == toolId))
            {
                return;
            }

            errors.Add($"Eksik ekipman pickup'i: {toolId}");
        }

        private static void RequireSearchGate(
            string evidenceId,
            string requiredToolId,
            IReadOnlyList<SearchSpotInteractable> searchSpots,
            List<string> errors)
        {
            var searchSpot = searchSpots.FirstOrDefault(item => item != null && item.HiddenEvidenceId == evidenceId);
            if (searchSpot == null)
            {
                errors.Add($"Eksik arama noktasi: {evidenceId}");
                return;
            }

            if (searchSpot.RequiredToolId != requiredToolId)
            {
                errors.Add($"{evidenceId} arama noktasi {requiredToolId} ile gate edilmiyor.");
            }
        }

        private static string BuildReport(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            var lines = new List<string>
            {
                "MOBIL OFL Release Readiness",
                $"Errors: {errors.Count}",
                $"Warnings: {warnings.Count}"
            };

            if (errors.Count > 0)
            {
                lines.Add("");
                lines.Add("Errors:");
                for (var i = 0; i < errors.Count; i++)
                {
                    lines.Add($"- {errors[i]}");
                }
            }

            if (warnings.Count > 0)
            {
                lines.Add("");
                lines.Add("Warnings:");
                for (var i = 0; i < warnings.Count; i++)
                {
                    lines.Add($"- {warnings[i]}");
                }
            }

            if (errors.Count == 0)
            {
                lines.Add("");
                lines.Add("No blocking release issues found by the static scene validator.");
            }

            return string.Join("\n", lines);
        }
    }
}
