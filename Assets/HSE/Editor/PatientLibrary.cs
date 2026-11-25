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
            public string Problems, Diet, Mobility, Falls;
            public float Hr, Spo2, Rr, Sys, Dia, Temp;
            public bool Irregular;
            public (string, float)[] Labs;
            public string LabComment;
            public string[] Orders;
            public Color Skin, Hair, Gown;
        }

        static readonly Spec[] Specs =
        {
            new Spec
            {
                File = "Patient_PeterGlynne", Name = "Peter Glynne", Dob = "14.03.1958", Mrn = "20460718", Gender = "Male",
                Physician = "VR Doctor 1", Diagnosis = "Community-acquired pneumonia, day 3 of antibiotics",
                Problems = "Right lower lobe pneumonia, IV antibiotics day 3. Needs 2 L/min oxygen. Productive cough.",
                Diet = "Normal diet, encourage fluids", Mobility = "Independent", Falls = "Low",
                Hr = 96, Spo2 = 94, Rr = 20, Sys = 118, Dia = 72, Temp = 38.1f,
                Labs = new[] { ("WBC", 14.2f), ("HGB", 138f), ("PLT", 312f), ("NA", 136f), ("K", 4.1f), ("UREA", 7.7f), ("CREA", 92f), ("CRP", 86f),
                                ("UA_LEU", 0f), ("UA_NIT", 0f), ("UA_GLU", 0f), ("UA_KET", 0f), ("UA_PRO", 0f) },
                LabComment = "White cells (14.2) and CRP (86) are raised, which fits the chest infection on day 3 of antibiotics. " +
                             "Urea is at the top of the normal range, so keep encouraging fluids. Sodium, potassium, creatinine, " +
                             "haemoglobin, platelets and the urine test are normal. Report the raised infection markers to VR Doctor 1 " +
                             "and keep monitoring temperature and oxygen saturation.",
                Blood = "A Rh(+)", Allergies = "Penicillin",
                Plan = "Morning bloods to review infection markers. Urinalysis. Vital signs every 4 hours.",
                Orders = new[] { "CBC", "BIOCHEM", "CRP", "UA" },
                Skin = new Color(0.9f, 0.74f, 0.64f), Hair = new Color(0.62f, 0.6f, 0.58f), Gown = new Color(0.62f, 0.76f, 0.88f)
            },
            new Spec
            {
                File = "Patient_EnderErol", Name = "Ender Erol", Dob = "02.07.1994", Mrn = "20511342", Gender = "Male",
                Physician = "VR Doctor 2", Diagnosis = "Type 1 diabetes, poorly controlled blood glucose",
                Problems = "High blood glucose for 3 days, insulin being adjusted. Check ketones. Hypo awareness.",
                Diet = "Diabetic diet, carbohydrate counted", Mobility = "Independent", Falls = "Low",
                Hr = 88, Spo2 = 98, Rr = 18, Sys = 128, Dia = 80, Temp = 36.9f,
                Labs = new[] { ("GLU", 13.8f), ("NA", 134f), ("K", 5.2f), ("UREA", 6.1f), ("CREA", 84f), ("WBC", 8.6f), ("HGB", 152f), ("PLT", 268f),
                                ("UA_LEU", 0f), ("UA_NIT", 0f), ("UA_GLU", 2f), ("UA_KET", 1f), ("UA_PRO", 0f) },
                LabComment = "Fasting glucose is high at 13.8 mmol/L and the urine shows glucose and ketones, so blood sugar control " +
                             "is poor and the body has started to burn fat for energy. Sodium is slightly low and potassium is at the " +
                             "upper limit, which can happen with high glucose. Blood count and kidney function are normal. Inform " +
                             "VR Doctor 2 today, check capillary glucose and ketones as in the diabetes plan, and encourage fluids.",
                Blood = "0 Rh(+)", Allergies = "No known allergies",
                Plan = "Fasting glucose and renal function before breakfast. Urinalysis for ketones.",
                Orders = new[] { "GLU", "BIOCHEM", "CBC", "UA" },
                Skin = new Color(0.82f, 0.64f, 0.5f), Hair = new Color(0.16f, 0.11f, 0.08f), Gown = new Color(0.6f, 0.8f, 0.78f)
            },
            new Spec
            {
                File = "Patient_AliErol", Name = "Ali Erol", Dob = "21.11.1961", Mrn = "20398805", Gender = "Male",
                Physician = "VR Doctor 1", Diagnosis = "Atrial fibrillation on warfarin, INR monitoring",
                Problems = "Atrial fibrillation, fast rate at times. Warfarin: INR target 2-3. Bruising on arms - watch for bleeding.",
                Diet = "Normal diet, consistent vitamin K", Mobility = "Walks with stick", Falls = "Medium",
                Hr = 102, Spo2 = 96, Rr = 17, Sys = 136, Dia = 84, Temp = 36.7f, Irregular = true,
                Labs = new[] { ("PT", 31f), ("APTT", 36.2f), ("INR", 3.6f), ("WBC", 7.2f), ("HGB", 118f), ("PLT", 224f),
                                ("NA", 139f), ("K", 4.4f), ("UREA", 6.0f), ("CREA", 98f) },
                LabComment = "INR is 3.6, above the warfarin target of 2 to 3, and the prothrombin time is prolonged, so the blood " +
                             "is clotting too slowly and the bleeding risk is higher. Haemoglobin is low at 118 g/L, which together " +
                             "with the bruising needs attention. Electrolytes and kidney function are normal. Tell VR Doctor 1 before " +
                             "the next warfarin dose, check for any signs of bleeding and avoid injections into muscle.",
                Blood = "B Rh(+)", Allergies = "Aspirin",
                Plan = "Daily INR. Check full blood count and electrolytes. Watch for bleeding.",
                Orders = new[] { "COAG", "CBC", "BIOCHEM" },
                Skin = new Color(0.8f, 0.62f, 0.48f), Hair = new Color(0.35f, 0.33f, 0.32f), Gown = new Color(0.7f, 0.74f, 0.86f)
            },
            new Spec
            {
                File = "Patient_MargaretHughes", Name = "Margaret Hughes", Dob = "09.05.1947", Mrn = "20277610", Gender = "Female",
                Physician = "VR Doctor 3", Diagnosis = "Urinary tract infection with fever",
                Problems = "Urinary infection with fever and new confusion at night. Reduced fluid intake. Kidney function to watch.",
                Diet = "Soft diet, fluid chart 2 L/day", Mobility = "Assist x2, frame", Falls = "High",
                Hr = 104, Spo2 = 95, Rr = 22, Sys = 104, Dia = 62, Temp = 38.6f,
                Labs = new[] { ("WBC", 15.8f), ("HGB", 124f), ("PLT", 356f), ("CRP", 112f),
                                ("UA_LEU", 3f), ("UA_NIT", 1f), ("UA_GLU", 0f), ("UA_KET", 0f), ("UA_PRO", 1f), ("UCULT", 1f), ("BCULT", 0f) },
                LabComment = "White cells (15.8) and CRP (112) are clearly raised, and the urine shows leukocytes, nitrite and a " +
                             "trace of protein, with E. coli growing in the urine culture. This fits a urinary tract infection, " +
                             "which can also explain the new confusion at night. Blood cultures show no growth so far. Inform " +
                             "VR Doctor 3, keep the fluid chart, and watch temperature, blood pressure and confusion closely.",
                Blood = "AB Rh(-)", Allergies = "Sulfonamides",
                Plan = "Urine sample before the next antibiotic dose. Blood cultures if temperature above 38.5 C.",
                Orders = new[] { "CBC", "CRP", "UA", "UCULT", "BCULT" },
                Skin = new Color(0.93f, 0.78f, 0.7f), Hair = new Color(0.9f, 0.9f, 0.88f), Gown = new Color(0.86f, 0.72f, 0.82f)
            },
            new Spec
            {
                File = "Patient_SamuelOkafor", Name = "Samuel Okafor", Dob = "30.01.1979", Mrn = "20633157", Gender = "Male",
                Physician = "VR Doctor 2", Diagnosis = "Jaundice, suspected liver disease",
                Problems = "Yellow skin and eyes, tired, itching. Liver scan booked. Watch for confusion and bleeding.",
                Diet = "Low salt diet", Mobility = "Independent", Falls = "Low",
                Hr = 84, Spo2 = 97, Rr = 16, Sys = 112, Dia = 70, Temp = 37.2f,
                Labs = new[] { ("ALT", 186f), ("AST", 154f), ("ALP", 210f), ("BILI", 64f), ("PT", 14.6f), ("APTT", 33.8f), ("INR", 1.6f),
                                ("WBC", 6.4f), ("HGB", 141f), ("PLT", 118f) },
                LabComment = "All four liver tests are raised, and bilirubin at 64 umol/L explains the yellow skin and eyes. Clotting " +
                             "is slower than normal (INR 1.6) and platelets are low at 118, so the liver is not making enough clotting " +
                             "factors and the bleeding risk is higher. Inform VR Doctor 2, watch for bleeding and confusion, and " +
                             "remember the latex allergy: use nitrile gloves when taking blood.",
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
                p.problems = s.Problems;
                p.diet = s.Diet;
                p.mobility = s.Mobility;
                p.fallsRisk = s.Falls;
                p.heartRate = s.Hr; p.spo2 = s.Spo2; p.respRate = s.Rr;
                p.systolic = s.Sys; p.diastolic = s.Dia; p.temperature = s.Temp;
                p.irregularRhythm = s.Irregular;
                p.labResults = new List<PatientRecord.LabBias>();
                if (s.Labs != null) foreach (var (a, v) in s.Labs) p.labResults.Add(new PatientRecord.LabBias(a, v));
                p.labComment = s.LabComment;
                EditorUtility.SetDirty(p);
                list.Add(p);
            }
            AssetDatabase.SaveAssets();
            return list;
        }
    }
}
