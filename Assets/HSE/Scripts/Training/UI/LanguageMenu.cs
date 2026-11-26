using System;
using HSE.Localization;
using HSE.Scenario;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSE.Training.UI
{
    /// <summary>
    /// Panel at the room entrance: pick English or Turkish, then the scenario starts.
    /// English is the default if the scene is started some other way.
    /// </summary>
    public class LanguageMenu : MonoBehaviour
    {
        [SerializeField] ScenarioRunner runner;
        [SerializeField] Vector2 sizeMeters = new Vector2(0.62f, 0.5f);

        const float Width = 900f;

        public void Bind(ScenarioRunner scenario) => runner = scenario;

        readonly System.Collections.Generic.List<(Image, HSE.Clinical.Branch)> _variantButtons = new System.Collections.Generic.List<(Image, HSE.Clinical.Branch)>();

        void SelectVariant(HSE.Clinical.Branch branch)
        {
            HSE.Clinical.TrainingBranch.Set(branch);
            foreach (var (img, b) in _variantButtons)
                img.color = b == branch ? new Color(0.12f, 0.37f, 0.75f, 1f) : new Color(1, 1, 1, 0.12f);
        }

        void Awake() => Build();

        public void Choose(Language language)
        {
            Loc.Set(language);
            gameObject.SetActive(false);
            if (runner && !runner.IsRunning) runner.StartScenario();
        }

        void Build()
        {
            float height = Width * sizeMeters.y / sizeMeters.x;
            var go = new GameObject("LanguageCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            var raycaster = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycaster != null) go.AddComponent(raycaster);
            go.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(Width, height);
            rt.localScale = Vector3.one * (sizeMeters.x / Width);

            var bg = Box(rt, new Color(0.07f, 0.1f, 0.15f, 0.92f));
            Fill(bg, 0, 0, 0, 0);
            var head = Box(rt, new Color(0.12f, 0.37f, 0.75f, 1f));
            Anchor(head, 0, 1, 1, 1, new Vector2(0, -60), new Vector2(0, 120));
            Text(head, "Choose your language  /  Dilinizi seçin", 44, true);

            var sub = Text(rt, "Blood Collection and Lab Request\nKan Alma ve Laboratuvar İstemi", 32, false);
            Anchor(sub.rectTransform, 0, 1, 1, 1, new Vector2(0, -205), new Vector2(-60, 120));

            MakeButton(rt, "English", new Vector2(-200, 10), () => Choose(Language.English));
            MakeButton(rt, "Türkçe", new Vector2(200, 10), () => Choose(Language.Turkish));

            // Instructor: pick a GDD decision-point variant before starting.
            var label = Text(rt, "Scenario variant / Senaryo varyantı", 26, false);
            Anchor(label.rectTransform, 0, 0, 1, 0, new Vector2(0, 205), new Vector2(-40, 40));
            var variants = new (string, HSE.Clinical.Branch)[]
            {
                ("Standard", HSE.Clinical.Branch.Standard),
                ("Wrong wristband", HSE.Clinical.Branch.WrongWristband),
                ("Critical BP", HSE.Clinical.Branch.CriticalBloodPressure),
                ("Consent refused", HSE.Clinical.Branch.ConsentRefused),
            };
            for (int i = 0; i < variants.Length; i++)
            {
                var (name, branch) = variants[i];
                var b = Box(rt, new Color(1, 1, 1, 0.12f));
                Anchor(b, 0.5f, 0, 0.5f, 0, new Vector2(-330 + i * 220, 140), new Vector2(205, 70));
                var btn = b.gameObject.AddComponent<Button>();
                var t = Text(b, name, 24, true);
                _variantButtons.Add((b.GetComponent<Image>(), branch));
                btn.onClick.AddListener(() => SelectVariant(branch));
            }
            SelectVariant(HSE.Clinical.Branch.Standard);

            var hint = Text(rt, "Point and pinch or pull the trigger  ·  İşaret edip parmakları birleştirin veya tetiğe basın", 26, false);
            hint.color = new Color(1, 1, 1, 0.7f);
            Anchor(hint.rectTransform, 0, 0, 1, 0, new Vector2(0, 40), new Vector2(-40, 50));
        }

        static void MakeButton(RectTransform parent, string label, Vector2 pos, Action onClick)
        {
            var rt = Box(parent, new Color(0.95f, 0.96f, 0.98f, 1f));
            Anchor(rt, 0.5f, 0.5f, 0.5f, 0.5f, pos, new Vector2(340, 150));
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.75f, 0.85f, 1f);
            colors.pressedColor = new Color(0.55f, 0.7f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            var t = Text(rt, label, 52, true);
            t.color = new Color(0.1f, 0.2f, 0.4f);
        }

        static RectTransform Box(RectTransform parent, Color c)
        {
            var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = c;
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI Text(RectTransform parent, string text, float size, bool bold)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.color = Color.white;
            t.raycastTarget = false;
            Fill(t.rectTransform, 0, 0, 0, 0);
            return t;
        }

        static void Fill(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
