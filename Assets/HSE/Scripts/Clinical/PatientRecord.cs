using System.Collections.Generic;
using HSE.Localization;
using UnityEngine;

namespace HSE.Clinical
{
    /// <summary>Electronic patient record shown on the PDA after a wristband scan. Fictional training data only.</summary>
    [CreateAssetMenu(menuName = "HSE/Patient Record", fileName = "Patient")]
    public class PatientRecord : ScriptableObject
    {
        [Header("Identity (printed on the wristband)")]
        public string fullName = "Mehmet Kaya";
        public string dateOfBirth = "14.03.1958";
        public string medicalRecordNumber = "20460718";
        public string gender = "Male";
        public string genderTr = "Erkek";

        [Header("Admission")]
        public string ward = "Internal Medicine 2B";
        public string wardTr = "Dahiliye 2B";
        public string room = "204";
        public string bed = "1";
        public string attendingPhysician = "Dr. Elif Demir";
        public string diagnosis = "Community-acquired pneumonia, follow-up";
        public string diagnosisTr = "Toplum kökenli pnömoni, takip";

        [Header("Clinical")]
        public string bloodType = "A Rh(+)";
        public string allergies = "Penicillin";
        public string allergiesTr = "Penisilin";
        [TextArea] public string carePlan = "Morning blood and urine tests. Vital signs every 4 hours.";
        [TextArea] public string carePlanTr = "Sabah kan ve idrar tahlilleri. Vital bulgular 4 saatte bir.";

        [Tooltip("Lab tests the physician ordered; the nurse's PDA request is checked against this list.")]
        public List<string> physicianOrderedTestIds = new List<string> { "CBC", "BIOCHEM", "CRP", "UA" };

        // Text in the language picked at the room entrance.
        public string Gender => Loc.Pick(gender, genderTr);
        public string Ward => Loc.Pick(ward, wardTr);
        public string Diagnosis => Loc.Pick(diagnosis, diagnosisTr);
        public string Allergies => Loc.Pick(allergies, allergiesTr);
        public string CarePlan => Loc.Pick(carePlan, carePlanTr);

        /// <summary>Resets the asset to the default training patient (used by the scene builder).</summary>
        public void ResetToDefaults()
        {
            var fresh = CreateInstance<PatientRecord>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
            DestroyImmediate(fresh);
        }

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
