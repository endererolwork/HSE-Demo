using System.Collections.Generic;
using HSE.Clinical;
using UnityEditor;
using UnityEngine;

namespace HSE.EditorTools
{
    /// <summary>Creates/updates the training patients (fictional, English data) under Assets/HSE/Data/Patients.</summary>
    public static class PatientLibrary
    {
        const string Dir = "Assets/HSE/Data/Patients";

        struct Spec
        {
            public string File, Name, Dob, Mrn, Gender, Physician, Diagnosis, Blood, Allergies, Plan;
            public string[] Orders;
            public Color Skin, Hair, Gown;
        }

        static readonly Spec[] Specs =
        {
            new Spec
            {
                File = "Patient_PeterGlynne", Name = "Peter Glynne", Dob = "14.03.1958", Mrn = "20460718", Gender = "Male",
                Physician = "Dr. Sarah Collins", Diagnosis = "Community-acquired pneumonia, day 3 of antibiotics",
                Blood = "A Rh(+)", Allergies = "Penicillin",
                Plan = "Morning bloods to review infection markers. Urinalysis. Vital signs every 4 hours.",
                Orders = new[] { "CBC", "BIOCHEM", "CRP", "UA" },
                Skin = new Color(0.9f, 0.74f, 0.64f), Hair = new Color(0.62f, 0.6f, 0.58f), Gown = new Color(0.62f, 0.76f, 0.88f)
            },
            new Spec
            {
                File = "Patient_EnderErol", Name = "Ender Erol", Dob = "02.07.1994", Mrn = "20511342", Gender = "Male",
                Physician = "Dr. James Patel", Diagnosis = "Type 1 diabetes, poorly controlled blood glucose",
                Blood = "0 Rh(+)", Allergies = "No known allergies",
                Plan = "Fasting glucose and renal function before breakfast. Urinalysis for ketones.",
                Orders = new[] { "GLU", "BIOCHEM", "CBC", "UA" },
                Skin = new Color(0.82f, 0.64f, 0.5f), Hair = new Color(0.16f, 0.11f, 0.08f), Gown = new Color(0.6f, 0.8f, 0.78f)
            },
            new Spec
            {
                File = "Patient_AliErol", Name = "Ali Erol", Dob = "21.11.1961", Mrn = "20398805", Gender = "Male",
                Physician = "Dr. Sarah Collins", Diagnosis = "Atrial fibrillation on warfarin, INR monitoring",
                Blood = "B Rh(+)", Allergies = "Aspirin",
                Plan = "Daily INR. Check full blood count and electrolytes. Watch for bleeding.",
                Orders = new[] { "COAG", "CBC", "BIOCHEM" },
                Skin = new Color(0.8f, 0.62f, 0.48f), Hair = new Color(0.35f, 0.33f, 0.32f), Gown = new Color(0.7f, 0.74f, 0.86f)
            },
            new Spec
            {
                File = "Patient_MargaretHughes", Name = "Margaret Hughes", Dob = "09.05.1947", Mrn = "20277610", Gender = "Female",
                Physician = "Dr. Laura Bennett", Diagnosis = "Urinary tract infection with fever",
                Blood = "AB Rh(-)", Allergies = "Sulfonamides",
                Plan = "Urine sample before the next antibiotic dose. Blood cultures if temperature above 38.5 C.",
                Orders = new[] { "CBC", "CRP", "UA", "UCULT", "BCULT" },
                Skin = new Color(0.93f, 0.78f, 0.7f), Hair = new Color(0.9f, 0.9f, 0.88f), Gown = new Color(0.86f, 0.72f, 0.82f)
            },
            new Spec
            {
                File = "Patient_SamuelOkafor", Name = "Samuel Okafor", Dob = "30.01.1979", Mrn = "20633157", Gender = "Male",
                Physician = "Dr. James Patel", Diagnosis = "Jaundice, suspected liver disease",
                Blood = "0 Rh(-)", Allergies = "Latex (use nitrile gloves)",
                Plan = "Liver function and clotting screen this morning. Monitor for confusion.",
                Orders = new[] { "LFT", "COAG", "CBC" },
                Skin = new Color(0.42f, 0.29f, 0.21f), Hair = new Color(0.06f, 0.05f, 0.05f), Gown = new Color(0.62f, 0.76f, 0.88f)
            },
        };

        [MenuItem("HSE/Create or Update Patients")]
        public static List<PatientRecord> CreateOrUpdate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/HSE/Data")) AssetDatabase.CreateFolder("Assets/HSE", "Data");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/HSE/Data", "Patients");

            var list = new List<PatientRecord>();
            foreach (var s in Specs)
            {
                string path = $"{Dir}/{s.File}.asset";
                var p = AssetDatabase.LoadAssetAtPath<PatientRecord>(path);
                if (!p)
                {
                    p = ScriptableObject.CreateInstance<PatientRecord>();
                    AssetDatabase.CreateAsset(p, path);
                }
                p.fullName = s.Name;
                p.dateOfBirth = s.Dob;
                p.medicalRecordNumber = s.Mrn;
                p.gender = s.Gender;
                p.ward = "Internal Medicine 2B";
                p.room = "204";
                p.bed = "1";
                p.attendingPhysician = s.Physician;
                p.diagnosis = s.Diagnosis;
                p.bloodType = s.Blood;
                p.allergies = s.Allergies;
                p.carePlan = s.Plan;
                p.physicianOrderedTestIds = new List<string>(s.Orders);
                p.skinTone = s.Skin;
                p.hairColor = s.Hair;
                p.gownColor = s.Gown;
                EditorUtility.SetDirty(p);
                list.Add(p);
            }
            AssetDatabase.SaveAssets();
            return list;
        }
    }
}
