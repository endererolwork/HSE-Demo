using System.Collections.Generic;
using HSE.Localization;
using UnityEngine;

namespace HSE.Clinical
{
    public enum SampleType { Blood, Urine, Other }

    public class LabTest
    {
        public readonly string Id;
        public readonly SampleType Sample;
        public readonly Color ContainerColor;
        readonly string _name, _nameTr, _detail, _detailTr, _container, _containerTr;

        public LabTest(string id, SampleType sample, Color color, string name, string nameTr, string detail, string detailTr, string container, string containerTr)
        {
            Id = id; Sample = sample; ContainerColor = color;
            _name = name; _nameTr = nameTr; _detail = detail; _detailTr = detailTr; _container = container; _containerTr = containerTr;
        }

        // Clinical data stays in English in every UI language; the Turkish names are kept for reference only.
        public string Name => _name;
        public string Detail => _detail;
        public string Container => _container;
        public string NameTr => _nameTr;
    }

    /// <summary>Tests the nurse can request from the PDA, with the sample container each one needs.</summary>
    public static class LabTestCatalog
    {
        static readonly Color Lavender = new Color(0.62f, 0.5f, 0.86f);
        static readonly Color Gold = new Color(0.92f, 0.72f, 0.2f);
        static readonly Color LightBlue = new Color(0.5f, 0.76f, 0.96f);
        static readonly Color Gray = new Color(0.6f, 0.62f, 0.64f);
        static readonly Color UrineYellow = new Color(0.98f, 0.85f, 0.25f);
        static readonly Color CultureBlack = new Color(0.15f, 0.15f, 0.18f);

        public static readonly List<LabTest> All = new List<LabTest>
        {
            new LabTest("CBC", SampleType.Blood, Lavender, "Full Blood Count", "Hemogram (Tam Kan Sayımı)", "WBC, HGB, PLT", "WBC, HGB, PLT", "Lavender top (EDTA)", "Mor kapak (EDTA)"),
            new LabTest("BIOCHEM", SampleType.Blood, Gold, "Urea and Electrolytes", "Biyokimya", "Urea, creatinine, Na, K", "Üre, kreatinin, elektrolitler", "Gold top (SST)", "Sarı kapak (Jel)"),
            new LabTest("LFT", SampleType.Blood, Gold, "Liver Function Tests", "Karaciğer Fonksiyon Testleri", "ALT, AST, ALP, bilirubin", "ALT, AST, ALP, bilirubin", "Gold top (SST)", "Sarı kapak (Jel)"),
            new LabTest("CRP", SampleType.Blood, Gold, "CRP", "CRP", "C-reactive protein", "C-reaktif protein", "Gold top (SST)", "Sarı kapak (Jel)"),
            new LabTest("COAG", SampleType.Blood, LightBlue, "Coagulation Screen", "Koagülasyon", "PT, aPTT, INR", "PT, aPTT, INR", "Light blue top (Citrate)", "Mavi kapak (Sitrat)"),
            new LabTest("GLU", SampleType.Blood, Gray, "Fasting Glucose", "Açlık Kan Şekeri", "Glucose", "Glukoz", "Grey top (Fluoride)", "Gri kapak (Florür)"),
            new LabTest("BCULT", SampleType.Blood, CultureBlack, "Blood Culture", "Kan Kültürü", "Aerobic / anaerobic bottles", "Aerob / anaerob şişe", "Culture bottle", "Kültür şişesi"),
            new LabTest("UA", SampleType.Urine, UrineYellow, "Urinalysis", "Tam İdrar Tahlili", "Dipstick + microscopy", "Dipstick + mikroskopi", "Urine container", "İdrar kabı"),
            new LabTest("UCULT", SampleType.Urine, UrineYellow, "Urine Culture", "İdrar Kültürü", "Midstream urine", "Orta akım idrar", "Sterile urine container", "Steril idrar kabı"),
            new LabTest("SPUTUM", SampleType.Other, Gray, "Sputum Culture", "Balgam Kültürü", "Gram stain + culture", "Gram boyama + kültür", "Sterile container", "Steril kap"),
        };

        public static LabTest Get(string id) => All.Find(t => t.Id == id);

        public static string SampleLabel(SampleType s) => s switch
        {
            SampleType.Blood => Loc.T("sample.blood"),
            SampleType.Urine => Loc.T("sample.urine"),
            _ => Loc.T("sample.other")
        };
    }
}
