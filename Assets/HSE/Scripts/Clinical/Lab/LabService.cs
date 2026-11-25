using System.Collections;
using UnityEngine;

namespace HSE.Clinical.Lab
{
    /// <summary>
    /// The simulated laboratory: a submitted request comes back as a report after a short "processing" delay,
    /// followed by an AI (or offline) comment on it.
    /// </summary>
    public class LabService : MonoBehaviour
    {
        [SerializeField, Min(0)] float processingSeconds = 6f;
        [SerializeField] AiCommentSettings aiSettings;

        public LabReport Latest { get; private set; }
        public string LatestComment { get; private set; }

        readonly LabResultGenerator _generator = new LabResultGenerator();

        public void Bind(AiCommentSettings settings) => aiSettings = settings;

        void OnEnable()
        {
            ClinicalEvents.LabOrderSubmitted += OnOrder;
            ActivePatient.Changed += OnPatientChanged;
        }

        void OnDisable()
        {
            ClinicalEvents.LabOrderSubmitted -= OnOrder;
            ActivePatient.Changed -= OnPatientChanged;
        }

        void OnPatientChanged(PatientRecord _)
        {
            StopAllCoroutines();
            Latest = null;
            LatestComment = null;
        }

        void OnOrder(LabOrder order) => StartCoroutine(Process(order));

        IEnumerator Process(LabOrder order)
        {
            yield return new WaitForSeconds(processingSeconds);
            Latest = _generator.Generate(order);
            LatestComment = null;
            ClinicalEvents.RaiseLabReportReady(Latest);

            IReportCommentator commentator = ClaudeReportCommentator.IsAvailable(aiSettings)
                ? new ClaudeReportCommentator(aiSettings)
                : new RuleBasedCommentator();
            var report = Latest;
            yield return commentator.Comment(report, text => LatestComment = text);
            if (report == Latest) ClinicalEvents.RaiseLabCommentReady(report, LatestComment);
        }
    }
}
