using System.Collections.Generic;
using System.Linq;
using System.Text;
using HSE.Localization;
using HSE.Scenario;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSE.Training.UI
{
    /// <summary>
    /// Wrist-mounted task list. Only listens to <see cref="ScenarioRunner"/> events and draws them;
    /// it never drives the scenario (Single Responsibility).
    /// </summary>
    public class ScenarioChecklistPanel : MonoBehaviour
    {
        [SerializeField] ScenarioRunner runner;
        [Tooltip("Usually the left controller. If empty, the panel looks for one by name and otherwise stays where it is.")]
        [SerializeField] Transform attachTo;
        [SerializeField] Vector3 attachOffset = new Vector3(0f, 0.07f, 0.03f);
        [SerializeField] Vector3 attachEuler = new Vector3(35f, 0f, 0f);
        [SerializeField] Vector2 sizeMeters = new Vector2(0.16f, 0.2f);
        [SerializeField, Min(0.5f)] float mistakeShowSeconds = 3.5f;

        static readonly string[] ControllerNames = { "Left Controller", "LeftHand Controller", "Left Hand Controller", "XR Controller Left", "Left Hand" };

        const float Width = 640f;
        TextMeshProUGUI _header, _instruction, _list, _toast;
        float _toastUntil;

        public void Bind(ScenarioRunner scenario) => runner = scenario;

        void Awake()
        {
            BuildCanvas();
        }

        void OnEnable()
        {
            if (!runner) return;
            runner.ScenarioStarted += OnStarted;
            runner.StepStarted += OnChanged;
            runner.StepCompleted += OnChanged;
            runner.MistakeMade += OnMistake;
            runner.ScenarioCompleted += OnFinished;
            Loc.Changed += Redraw;
        }

        void OnDisable()
        {
            if (!runner) return;
            runner.ScenarioStarted -= OnStarted;
            runner.StepStarted -= OnChanged;
            runner.StepCompleted -= OnChanged;
            runner.MistakeMade -= OnMistake;
            runner.ScenarioCompleted -= OnFinished;
            Loc.Changed -= Redraw;
        }

        float _nextSearch;
        bool _attached;

        void Start()
        {
            TryAttach();
            if (runner && runner.IsRunning) Redraw();
        }

        // Controllers can be switched on a little after the scene starts (input modality manager), so keep looking.
        void TryAttach()
        {
            if (_attached) return;
            if (!attachTo) attachTo = FindController();
            if (!attachTo) return;
            transform.SetParent(attachTo, false);
            transform.localPosition = attachOffset;
            transform.localRotation = Quaternion.Euler(attachEuler);
            _attached = true;
        }

        void Update()
        {
            if (!_attached && Time.time >= _nextSearch)
            {
                _nextSearch = Time.time + 1f;
                TryAttach();
            }
            if (_toast && !string.IsNullOrEmpty(_toast.text) && Time.time > _toastUntil) _toast.text = "";
        }

        // ------------------------------------------------------------ runner events

        void OnStarted(ScenarioRunner _) => Redraw();
        void OnChanged(ScenarioStep _) => Redraw();

        void OnMistake(ScenarioStep step, string message)
        {
            _toast.text = $"<b>{Loc.T("checklist.mistake")}</b> {message}";
            _toastUntil = Time.time + mistakeShowSeconds;
        }

        void OnFinished(ScenarioReport report)
        {
            var t = report.TotalTime;
            _finished = report;
            _header.text = $"<b>{ScenarioTitle}</b>  ·  {Loc.T("checklist.done")}";
            _instruction.text = Loc.T("checklist.results");
            _list.text =
                $"{Loc.T("checklist.score")}: <b>{report.Score} / {report.MaxScore}</b>\n" +
                $"{Loc.T("checklist.mistakes")}: <b>{report.MistakeCount}</b>\n" +
                $"{Loc.T("checklist.time")}: <b>{(int)(t / 60):00}:{(int)(t % 60):00}</b>\n" +
                $"{Loc.T("checklist.extrasDone")}: <b>{report.CompletedCount(StepRequirement.Optional)} / {report.Count(StepRequirement.Optional)}</b>";
        }

        // ------------------------------------------------------------ drawing

        ScenarioReport _finished;
        string ScenarioTitle => runner ? Loc.TOr("scenario.title", runner.Title) : "";

        void Redraw()
        {
            if (!runner || runner.Report == null) return;
            if (_finished != null && _finished == runner.Report) { OnFinished(_finished); return; }
            var mandatory = runner.MandatorySteps.ToList();
            int done = mandatory.Count(s => s.State == StepState.Completed || s.State == StepState.Skipped);
            _header.text = $"<b>{ScenarioTitle}</b>  ·  {done}/{mandatory.Count}";

            var current = runner.CurrentStep;
            _instruction.text = current ? $"<b>{StepText.Title(current)}</b>\n<size=80%>{StepText.Instruction(current)}</size>" : "";

            var sb = new StringBuilder();
            for (int i = 0; i < mandatory.Count; i++) AppendLine(sb, mandatory[i], (i + 1) + ".");
            var optional = runner.OptionalSteps.ToList();
            if (optional.Count > 0)
            {
                sb.Append($"\n<size=85%><color=#8A96A8>{Loc.T("checklist.extras")}</color></size>\n");
                foreach (var s in optional) AppendLine(sb, s, "+");
            }
            _list.text = sb.ToString();
        }

        static void AppendLine(StringBuilder sb, ScenarioStep s, string prefix)
        {
            switch (s.State)
            {
                case StepState.Completed:
                    sb.Append($"<color=#4CAF6A>{prefix} <s>{StepText.Title(s)}</s></color>\n"); break;
                case StepState.Skipped:
                    sb.Append($"<color=#B0B7C3>{prefix} <s>{StepText.Title(s)}</s> ({Loc.T("checklist.skipped")})</color>\n"); break;
                case StepState.Active when s.Requirement == StepRequirement.Mandatory:
                    sb.Append($"<color=#3D8BFF><b>{prefix} {StepText.Title(s)}</b></color>\n"); break;
                default:
                    sb.Append($"<color=#C9CFD8>{prefix} {StepText.Title(s)}</color>\n"); break;
            }
            if (s is CompositeStep c && s.State == StepState.Active)
                foreach (var child in c.Children)
                    sb.Append(child.State == StepState.Completed
                        ? $"<size=85%><color=#4CAF6A>    <s>{StepText.Title(child)}</s></color></size>\n"
                        : $"<size=85%><color=#DDE3EA>    {StepText.Title(child)}</color></size>\n");
        }

        void BuildCanvas()
        {
            float height = Width * sizeMeters.y / sizeMeters.x;
            var canvasGo = new GameObject("ChecklistCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(Width, height);
            rt.localScale = Vector3.one * (sizeMeters.x / Width);

            var bg = Panel(rt, new Color(0.07f, 0.1f, 0.15f, 0.88f));
            Stretch(bg, 0, 0, 0, 0);
            var head = Panel(rt, new Color(0.12f, 0.37f, 0.75f, 0.95f));
            Top(head, 0, 70);
            _header = Text(head, 30, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(_header.rectTransform, 22, 0, 22, 0);

            var instr = Panel(rt, new Color(1, 1, 1, 0.08f));
            Top(instr, 82, 150);
            Stretch(instr, 14, -1, 14, -1, keepVertical: true);
            _instruction = Text(instr, 28, Color.white, TextAlignmentOptions.TopLeft);
            Stretch(_instruction.rectTransform, 16, 12, 16, 12);

            _list = Text(rt, 26, Color.white, TextAlignmentOptions.TopLeft);
            Stretch(_list.rectTransform, 28, 120, 22, 250);

            _toast = Text(rt, 26, new Color(1f, 0.45f, 0.45f), TextAlignmentOptions.BottomLeft);
            Stretch(_toast.rectTransform, 24, 18, 22, height - 110);
        }

        static RectTransform Panel(RectTransform parent, Color c)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = c;
            go.GetComponent<Image>().raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI Text(RectTransform parent, float size, Color c, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void Stretch(RectTransform rt, float left, float bottom, float right, float top, bool keepVertical = false)
        {
            if (keepVertical)
            {
                rt.anchorMin = new Vector2(0, rt.anchorMin.y);
                rt.anchorMax = new Vector2(1, rt.anchorMax.y);
                rt.offsetMin = new Vector2(left, rt.offsetMin.y);
                rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
                return;
            }
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void Top(RectTransform rt, float offset, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -offset);
            rt.sizeDelta = new Vector2(0, height);
        }

        static Transform FindController()
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var n in ControllerNames)
            {
                var t = all.FirstOrDefault(x => x.name == n);
                if (t) return t;
            }
            return all.FirstOrDefault(x => x.name.Contains("Left") && x.name.Contains("Controller"));
        }
    }
}
