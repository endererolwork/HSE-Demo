using System.Linq;
using HSE.Clinical;
using HSE.Clinical.Lab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSE.Training.UI
{
    /// <summary>
    /// Floating laboratory report that appears in front of the trainee when results come back:
    /// every result with its reference range and a coloured flag, then the AI assistant's comment.
    /// </summary>
    public class BloodReportPanel : MonoBehaviour
    {
        [SerializeField] Vector2 sizeMeters = new Vector2(0.72f, 0.92f);
        [SerializeField] float distance = 0.95f;

        const float Width = 1200f;
        static readonly Color Bg = new Color(0.97f, 0.98f, 0.99f, 0.98f);
        static readonly Color Ink = new Color(0.11f, 0.14f, 0.19f);
        static readonly Color Muted = new Color(0.42f, 0.46f, 0.52f);
        static readonly Color Navy = new Color(0.09f, 0.2f, 0.36f);

        RectTransform _canvas, _rows;
        TextMeshProUGUI _title, _subtitle, _comment;
        LabReport _report;

        void Awake()
        {
            Build();
            _canvas.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            ClinicalEvents.LabReportReady += Show;
            ClinicalEvents.LabCommentReady += OnComment;
            ActivePatient.Changed += Hide;
        }

        void OnDisable()
        {
            ClinicalEvents.LabReportReady -= Show;
            ClinicalEvents.LabCommentReady -= OnComment;
            ActivePatient.Changed -= Hide;
        }

        void Hide(PatientRecord _) => _canvas.gameObject.SetActive(false);

        public void Show(LabReport report)
        {
            _report = report;
            PlaceInFrontOfUser();
            _canvas.gameObject.SetActive(true);
            _title.text = $"Laboratory Report  ·  {report.Patient.fullName}";
            _subtitle.text = $"MRN {report.Patient.medicalRecordNumber}   ·   {report.OrderReference}   ·   Collected {report.Collected:dd.MM.yyyy HH:mm}   ·   Reported {report.Reported:HH:mm}";

            for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);
            float rowH = report.Results.Count > 18 ? 38 : 46;
            float font = report.Results.Count > 18 ? 24 : 28;
            Row(_rows, "<b>Test</b>", "<b>Result</b>", "<b>Reference</b>", "<b>Flag</b>", new Color(0.88f, 0.91f, 0.95f), Muted, font, rowH);
            foreach (var group in report.Results.GroupBy(r => r.TestId))
            {
                var test = LabTestCatalog.Get(group.Key);
                var header = Row(_rows, $"<b>{test?.Name ?? group.Key}</b>", "", "", "", Color.white, Navy, font, rowH);
                foreach (var r in group)
                {
                    var (chip, label) = FlagStyle(r.Flag);
                    var unit = string.IsNullOrEmpty(r.Unit) ? "" : $" <size=75%>{r.Unit}</size>";
                    var valueColor = r.IsOutOfRange ? "#C62828" : r.Flag == ResultFlag.Borderline ? "#B26A00" : "#1C2430";
                    Row(_rows, "   " + r.Name, $"<color={valueColor}><b>{r.Value}</b></color>{unit}", r.Reference, label, Color.white, Ink, font, rowH, chip);
                }
            }

            int outCount = report.OutOfRange.Count(), borderCount = report.Borderline.Count();
            _comment.text = $"<b>Your AI assistant's comments</b>  <size=80%><color=#6B7685>{outCount} out of range · {borderCount} borderline</color></size>\n" +
                            "<color=#6B7685><i>Analysing the results...</i></color>";
        }

        void OnComment(LabReport report, string comment)
        {
            if (report != _report) return;
            int outCount = report.OutOfRange.Count(), borderCount = report.Borderline.Count();
            _comment.text = $"<b>Your AI assistant's comments</b>  <size=80%><color=#6B7685>{outCount} out of range · {borderCount} borderline</color></size>\n" +
                            comment +
                            "\n<size=70%><color=#8A96A8>Simulated results for training. AI comments can be wrong and are not medical advice.</color></size>";
        }

        static (Color, string) FlagStyle(ResultFlag f) => f switch
        {
            ResultFlag.Normal => (new Color(0.86f, 0.95f, 0.88f), "<color=#1E7E45>Normal</color>"),
            ResultFlag.Borderline => (new Color(1f, 0.95f, 0.82f), "<color=#B26A00>Borderline</color>"),
            ResultFlag.High => (new Color(1f, 0.88f, 0.86f), "<color=#C62828><b>High</b></color>"),
            ResultFlag.Low => (new Color(1f, 0.88f, 0.86f), "<color=#C62828><b>Low</b></color>"),
            ResultFlag.CriticalHigh => (new Color(0.95f, 0.7f, 0.7f), "<color=#8E0000><b>CRITICAL HIGH</b></color>"),
            ResultFlag.CriticalLow => (new Color(0.95f, 0.7f, 0.7f), "<color=#8E0000><b>CRITICAL LOW</b></color>"),
            _ => (new Color(1f, 0.88f, 0.86f), "<color=#C62828><b>Abnormal</b></color>"),
        };

        void PlaceInFrontOfUser()
        {
            var head = Camera.main ? Camera.main.transform : null;
            if (!head) return;
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            transform.position = head.position + fwd * distance + Vector3.down * 0.1f;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // ------------------------------------------------------------ UI

        void Build()
        {
            float height = Width * sizeMeters.y / sizeMeters.x;
            var go = new GameObject("ReportCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            var raycaster = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycaster != null) go.AddComponent(raycaster);
            go.AddComponent<GraphicRaycaster>();
            _canvas = (RectTransform)go.transform;
            _canvas.sizeDelta = new Vector2(Width, height);
            _canvas.localScale = Vector3.one * (sizeMeters.x / Width);

            var bg = Box(_canvas, Bg);
            Stretch(bg, 0, 0, 0, 0);
            var head = Box(_canvas, Navy);
            Top(head, 0, 130);
            _title = Text(head, 44, Color.white, TextAlignmentOptions.TopLeft, true);
            Stretch(_title.rectTransform, 30, 50, 130, 16);
            _subtitle = Text(head, 24, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.BottomLeft, false);
            Stretch(_subtitle.rectTransform, 30, 14, 130, 70);

            var close = Box(head, new Color(1, 1, 1, 0.18f));
            close.anchorMin = close.anchorMax = new Vector2(1, 0.5f);
            close.pivot = new Vector2(1, 0.5f);
            close.sizeDelta = new Vector2(96, 96);
            close.anchoredPosition = new Vector2(-18, 0);
            close.GetComponent<Image>().raycastTarget = true;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => _canvas.gameObject.SetActive(false));
            var x = Text(close, 56, Color.white, TextAlignmentOptions.Center, true);
            x.text = "×";
            Stretch(x.rectTransform, 0, 0, 0, 0);

            _rows = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            _rows.SetParent(_canvas, false);
            Stretch(_rows, 24, 430, 24, 146);
            var v = _rows.GetComponent<VerticalLayoutGroup>();
            v.spacing = 2;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var ai = Box(_canvas, new Color(0.91f, 0.94f, 1f));
            Stretch(ai, 24, 24, 24, height - 410);
            _comment = Text(ai, 28, Ink, TextAlignmentOptions.TopLeft, false);
            Stretch(_comment.rectTransform, 22, 16, 22, 16);
        }

        static RectTransform Row(RectTransform parent, string a, string b, string c, string d, Color bg, Color ink, float font, float height, Color? chip = null)
        {
            var row = Box(parent, bg);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            Cell(row, a, 0f, 0.42f, ink, font);
            Cell(row, b, 0.42f, 0.64f, ink, font);
            Cell(row, c, 0.64f, 0.84f, Muted, font * 0.9f);
            if (chip.HasValue)
            {
                var chipBox = Box(row, chip.Value);
                chipBox.anchorMin = new Vector2(0.845f, 0.12f);
                chipBox.anchorMax = new Vector2(0.995f, 0.88f);
                chipBox.offsetMin = chipBox.offsetMax = Vector2.zero;
            }
            var flag = Cell(row, d, 0.845f, 0.995f, ink, font * 0.85f);
            flag.alignment = TextAlignmentOptions.Center;
            return row;
        }

        static TextMeshProUGUI Cell(RectTransform row, string text, float x0, float x1, Color c, float size)
        {
            var t = Text(row, size, c, TextAlignmentOptions.MidlineLeft, false);
            t.text = text;
            t.rectTransform.anchorMin = new Vector2(x0, 0);
            t.rectTransform.anchorMax = new Vector2(x1, 1);
            t.rectTransform.offsetMin = new Vector2(10, 0);
            t.rectTransform.offsetMax = new Vector2(-6, 0);
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        static RectTransform Box(RectTransform parent, Color c)
        {
            var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI Text(RectTransform parent, float size, Color c, TextAlignmentOptions align, bool bold)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        static void Top(RectTransform rt, float offset, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -offset);
            rt.sizeDelta = new Vector2(0, h);
        }
    }
}
