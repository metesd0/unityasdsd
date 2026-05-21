using System.Collections.Generic;
using UnityEngine;

namespace MobilOfl.Case
{
    [CreateAssetMenu(menuName = "Mobil OFL/Case Definition", fileName = "CaseDefinition")]
    public class CaseDefinition : ScriptableObject
    {
        [Header("Case")]
        [SerializeField] private string caseId = "case.exam-theft";
        [SerializeField] private string caseTitle = "Sinav Sorulari Calindi";
        [SerializeField] [TextArea] private string openingBrief =
            "Okulda sinav sorulari kayboldu. Takim gercegi ortaya cikarmali.";

        [Header("Evidence")]
        [SerializeField] private List<EvidenceData> evidenceItems = new List<EvidenceData>();

        [Header("Suspects")]
        [SerializeField] private List<SuspectData> suspects = new List<SuspectData>();
        [SerializeField] private string culpritSuspectId;
        [SerializeField] [TextArea] private string culpritMotive = "Motivasyon";
        [SerializeField] [TextArea] private string culpritTimeline = "Olay sirasi";
        [SerializeField] private List<string> motiveOptions = new List<string>();
        [SerializeField] private List<string> timelineOptions = new List<string>();

        public string CaseId => caseId;
        public string CaseTitle => caseTitle;
        public string OpeningBrief => openingBrief;
        public IReadOnlyList<EvidenceData> EvidenceItems => evidenceItems;
        public IReadOnlyList<SuspectData> Suspects => suspects;
        public string CulpritSuspectId => culpritSuspectId;
        public string CulpritMotive => culpritMotive;
        public string CulpritTimeline => culpritTimeline;
        public IReadOnlyList<string> MotiveOptions => motiveOptions;
        public IReadOnlyList<string> TimelineOptions => timelineOptions;
    }
}
