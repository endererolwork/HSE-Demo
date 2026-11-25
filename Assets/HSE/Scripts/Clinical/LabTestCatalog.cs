using System.Collections.Generic;
using UnityEngine;

namespace HSE.Clinical
{
    public enum SampleType { Blood, Urine, Other }

    public class LabTest
    {
        public string Id;
        public string Name;
        public string Detail;
        public SampleType Sample;
        public string Container;
        public Color ContainerColor;

        public LabTest(string id, string name, string detail, SampleType sample, string container, Color color)
        {
            Id = id; Name = name; Detail = detail; Sample = sample; Container = container; ContainerColor = color;
        }
    }

    /// <summary>Tests the nurse can request from the PDA, with the sample container each one needs.</summary>
    public static class LabTestCatalog
    {
        static readonly Color Lavender = new Color(0.62f, 0.5f, 0.86f);
        static readonly Color Gold = new Color(0.92f, 0.72f, 0.2f);
        static readonly Color LightBlue = new Color(0.5f, 0.76f, 0.96f);
        static readonly Color Green = new Color(0.2f, 0.65f, 0.3f);
        static readonly Color Gray = new Color(0.6f, 0.62f, 0.64f);
        static readonly Color UrineYellow = new Color(0.98f, 0.85f, 0.25f);
        static readonly Color CultureBlack = new Color(0.15f, 0.15f, 0.18f);

        public static readonly List<LabTest> All = new List<LabTest>
        {
            new LabTest("CBC", "Hemogram (Tam Kan Sayımı)", "WBC, HGB, PLT", SampleType.Blood, "Mor kapak (EDTA)", Lavender),
            new LabTest("BIOCHEM", "Biyokimya", "Üre, kreatinin, elektrolitler", SampleType.Blood, "Sarı kapak (Jel)", Gold),
            new LabTest("LFT", "Karaciğer Fonksiyon Testleri", "ALT, AST, ALP, bilirubin", SampleType.Blood, "Sarı kapak (Jel)", Gold),
            new LabTest("CRP", "CRP", "C-reaktif protein", SampleType.Blood, "Sarı kapak (Jel)", Gold),
            new LabTest("COAG", "Koagülasyon", "PT, aPTT, INR", SampleType.Blood, "Mavi kapak (Sitrat)", LightBlue),
            new LabTest("GLU", "Açlık Kan Şekeri", "Glukoz", SampleType.Blood, "Gri kapak (Florür)", Gray),
            new LabTest("BCULT", "Kan Kültürü", "Aerob / anaerob şişe", SampleType.Blood, "Kültür şişesi", CultureBlack),
            new LabTest("UA", "Tam İdrar Tahlili", "Dipstick + mikroskopi", SampleType.Urine, "İdrar kabı", UrineYellow),
            new LabTest("UCULT", "İdrar Kültürü", "Orta akım idrar", SampleType.Urine, "Steril idrar kabı", UrineYellow),
            new LabTest("SPUTUM", "Balgam Kültürü", "Gram boyama + kültür", SampleType.Other, "Steril kap", Gray),
        };

        public static LabTest Get(string id) => All.Find(t => t.Id == id);

        public static string SampleLabel(SampleType s) => s switch
        {
            SampleType.Blood => "KAN",
            SampleType.Urine => "İDRAR",
            _ => "DİĞER"
        };
    }
}
