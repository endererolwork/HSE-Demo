using UnityEngine;

namespace HSE.Clinical
{
    public enum CodeKind { PatientWristband, SpecimenBag, Medication }

    /// <summary>A barcode or QR code the PDA scanner can read. Needs a collider on the same object.</summary>
    [RequireComponent(typeof(Collider))]
    public class ScannableCode : MonoBehaviour
    {
        public CodeKind kind = CodeKind.PatientWristband;
        public string code = "HSE-PT-20460718";
        public PatientRecord patient;
    }
}
