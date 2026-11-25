using System.Collections.Generic;
using UnityEngine;

namespace HSE.Clinical
{
    /// <summary>
    /// Electronic patient record shown on the PDA after a wristband scan. Fictional training data.
    /// Clinical data is always in English, whatever UI language is picked.
    /// </summary>
    [CreateAssetMenu(menuName = "HSE/Patient Record", fileName = "Patient")]
    public class PatientRecord : ScriptableObject
    {
        [Header("Identity (printed on the wristband)")]
        public string fullName = "Peter Glynne";
        public string dateOfBirth = "14.03.1958";
        public string medicalRecordNumber = "20460718";
        public string gender = "Male";

        [Header("Admission")]
        public string ward = "Internal Medicine 2B";
        public string room = "204";
        public string bed = "1";
        public string attendingPhysician = "Dr. Elif Demir";
        public string diagnosis = "Community-acquired pneumonia, follow-up";

        [Header("Clinical")]
        public string bloodType = "A Rh(+)";
        public string allergies = "Penicillin";
        [TextArea] public string carePlan = "Morning blood and urine tests. Vital signs every 4 hours.";

        [Tooltip("Lab tests the physician ordered; the nurse's PDA request is checked against this list.")]
        public List<string> physicianOrderedTestIds = new List<string> { "CBC", "BIOCHEM", "CRP", "UA" };

        [Header("Appearance (applied to the patient figure)")]
        public Color skinTone = new Color(0.86f, 0.68f, 0.56f);
        public Color hairColor = new Color(0.2f, 0.14f, 0.1f);
        public Color gownColor = new Color(0.62f, 0.76f, 0.88f);

        public string Gender => gender;
        public string Ward => ward;
        public string Diagnosis => diagnosis;
        public string Allergies => allergies;
        public string CarePlan => carePlan;

        /// <summary>Code printed on this patient's wristband QR.</summary>
        public string WristbandCode => "HSE-PT-" + medicalRecordNumber;

        public int Age
        {
            get
            {
                var parts = dateOfBirth.Split('.');
                if (parts.Length != 3 || !int.TryParse(parts[0], out var d) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var y)) return 0;
                var today = System.DateTime.Today;
                int age = today.Year - y;
                if (today.Month < m || (today.Month == m && today.Day < d)) age--;
                return age;
            }
        }
    }
}
