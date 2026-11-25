using System.Collections;
using UnityEngine;

namespace HSE.Clinical.Lab
{
    /// <summary>
    /// The simulated laboratory: a submitted request comes back as a report after a short "processing" delay,
    /// followed by the comment written in advance for that patient.
    /// </summary>
    public class LabService : MonoBehaviour
    {
        [SerializeField, Min(0)] float processingSeconds = 6f;

        public LabReport Latest { get; private set; }
        public string LatestComment { get; private set; }

        readonly LabResultGenerator _generator = new LabResultGenerator();

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

            IReportCommentator commentator = new StoredCommentator();
            var report = Latest;
            yield return commentator.Comment(report, text => LatestComment = text);
            if (report == Latest) ClinicalEvents.RaiseLabCommentReady(report, LatestComment);
        }
    }
}
