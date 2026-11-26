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
        [Tooltip("The list opens at the start (and on mistakes), then folds into a small \"Tasks\" tab after this many seconds. It opens again with the results.")]
        [SerializeField, Min(0f)] float autoHideSeconds = 10f;

        static readonly string[] ControllerNames = { "Left Controller", "Left Hand", "LeftHand Controller", "Left Hand Controller", "XR Controller Left" };

        const float Width = 640f;
        TextMeshProUGUI _header, _instruction, _list, _toast, _tabText;
        RectTransform _bodyRoot, _tabRoot;
        float _toastUntil;
        float _hideAt = float.MaxValue;
        bool _expanded = true;

        public bool IsExpanded => _expanded;

        /// <summary>Raised when the trainee presses "Next patient" on the results screen.</summary>
        public event System.Action NextRequested;
        RectTransform _nextButton;

        /// <summary>Opens the full list. With autoHide it folds back into the tab after a while.</summary>
        public void Expand(bool autoHide = true)
        {
            _expanded = true;
            _bodyRoot.gameObject.SetActive(true);
            _tabRoot.gameObject.SetActive(false);
            _hideAt = autoHide && autoHideSeconds > 0 ? Time.time + autoHideSeconds : float.MaxValue;
        }

        public void Collapse()
        {
            _expanded = false;
            _bodyRoot.gameObject.SetActive(false);
            _tabRoot.gameObject.SetActive(true);
            _hideAt = float.MaxValue;
        }

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

        void Start()
        {
            TryAttach();
            if (runner && runner.IsRunning) Redraw();
            else
            {
                Collapse();
                _tabText.text = Loc.F("checklist.tab", 0, runner ? runner.MandatorySteps.Count() : 0);
            }
        }

        // Follows whichever left hand is live: the controller, or the tracked hand when controllers are put down.
        void TryAttach()
        {
            var parent = transform.parent;
            bool onHand = parent && (parent == attachTo || System.Array.IndexOf(ControllerNames, parent.name) >= 0);
            if (onHand && parent.gameObject.activeInHierarchy) return;
            var target = attachTo && attachTo.gameObject.activeInHierarchy ? attachTo : FindController();
            if (!target || target == parent) return;
            transform.SetParent(target, false);
            transform.localPosition = attachOffset;
            transform.localRotation = Quaternion.Euler(attachEuler);
        }

        void Update()
        {
            if (Time.time >= _nextSearch)
            {
                _nextSearch = Time.time + 1f;
                TryAttach();
            }
            if (_expanded && Time.time >= _hideAt) Collapse();
            if (_toast && !string.IsNullOrEmpty(_toast.text) && Time.time > _toastUntil) _toast.text = "";
        }

        // ------------------------------------------------------------ runner events

        void OnStarted(ScenarioRunner _)
        {
            _finished = null;
            _nextButton.gameObject.SetActive(false);
            Redraw();
            Expand(autoHide: true);
        }
        void OnChanged(ScenarioStep _) => Redraw();

        void OnMistake(ScenarioStep step, string message)
        {
            _toast.text = $"<b>{Loc.T("checklist.mistake")}</b> {message}";
            _toastUntil = Time.time + mistakeShowSeconds;
            if (!_expanded) Expand(autoHide: true);
        }

        void OnFinished(ScenarioReport report)
        {
            var t = report.TotalTime;
            _finished = report;
            Expand(autoHide: false);
            _tabText.text = Loc.T("checklist.done");
            _nextButton.gameObject.SetActive(true);
            _nextButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.T("session.next");
            _header.text = $"<b>{ScenarioTitle}</b>  ·  {Loc.T("checklist.done")}";
            _instruction.text = Loc.T("checklist.results");
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(report.EndedEarlyReason)) sb.Append($"<color=#F2B84B>{report.EndedEarlyReason}</color>\n");
            sb.Append($"{Loc.T("checklist.score")}: <b>{report.Score} / {report.MaxScore}</b>   {Loc.T("checklist.mistakes")}: <b>{report.MistakeCount}</b>   {Loc.T("checklist.time")}: <b>{(int)(t / 60):00}:{(int)(t % 60):00}</b>\n");
            foreach (var line in ScoreCard.Build(report))
            {
                string value = line.NotApplicable && line.Max == 0 ? "n/a" : $"{line.Score}/{line.Max}";
                string color = line.Max > 0 && line.Score == line.Max ? "#4CAF6A" : line.Mistakes > 0 || line.Score < line.Max ? "#F2B84B" : "#C9CFD8";
                sb.Append($"<size=85%><color={color}>{line.Name}: <b>{value}</b>{(line.Mistakes > 0 ? $"  ({line.Mistakes}x)" : "")}</color></size>\n");
            }
            sb.Append($"<size=85%>{Loc.T("checklist.extrasDone")}: <b>{report.CompletedCount(StepRequirement.Optional)} / {report.Count(StepRequirement.Optional)}</b></size>");
            _list.text = sb.ToString();
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
            _tabText.text = Loc.F("checklist.tab", done, mandatory.Count);

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
            var raycaster = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycaster != null) canvasGo.AddComponent(raycaster);
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.sizeDelta = new Vector2(Width, height);
            canvasRt.localScale = Vector3.one * (sizeMeters.x / Width);

            // Folded state: a small tab at the bottom that opens the list.
            _tabRoot = Panel(canvasRt, new Color(0.12f, 0.37f, 0.75f, 0.95f));
            _tabRoot.anchorMin = _tabRoot.anchorMax = new Vector2(0.5f, 0f);
            _tabRoot.pivot = new Vector2(0.5f, 0f);
            _tabRoot.sizeDelta = new Vector2(380, 96);
            _tabRoot.anchoredPosition = Vector2.zero;
            _tabRoot.GetComponent<Image>().raycastTarget = true;
            _tabRoot.gameObject.AddComponent<Button>().onClick.AddListener(() => Expand(autoHide: false));
            _tabText = Text(_tabRoot, 34, Color.white, TextAlignmentOptions.Center);
            _tabText.fontStyle = FontStyles.Bold;
            Stretch(_tabText.rectTransform, 8, 0, 8, 0);
            _tabRoot.gameObject.SetActive(false);

            _bodyRoot = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            _bodyRoot.SetParent(canvasRt, false);
            Stretch(_bodyRoot, 0, 0, 0, 0);
            var rt = _bodyRoot;

            var bg = Panel(rt, new Color(0.07f, 0.1f, 0.15f, 0.88f));
            Stretch(bg, 0, 0, 0, 0);
            var head = Panel(rt, new Color(0.12f, 0.37f, 0.75f, 0.95f));
            Top(head, 0, 70);
            _header = Text(head, 30, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(_header.rectTransform, 22, 0, 90, 0);

            var close = Panel(head, new Color(1, 1, 1, 0.15f));
            close.anchorMin = close.anchorMax = new Vector2(1f, 0.5f);
            close.pivot = new Vector2(1f, 0.5f);
            close.sizeDelta = new Vector2(70, 58);
            close.anchoredPosition = new Vector2(-8, 0);
            close.GetComponent<Image>().raycastTarget = true;
            close.gameObject.AddComponent<Button>().onClick.AddListener(Collapse);
            var x = Text(close, 40, Color.white, TextAlignmentOptions.Center);
            x.text = "×";
            Stretch(x.rectTransform, 0, 0, 0, 0);

            var instr = Panel(rt, new Color(1, 1, 1, 0.08f));
            Top(instr, 82, 150);
            Stretch(instr, 14, -1, 14, -1, keepVertical: true);
            _instruction = Text(instr, 28, Color.white, TextAlignmentOptions.TopLeft);
            Stretch(_instruction.rectTransform, 16, 12, 16, 12);

            _list = Text(rt, 26, Color.white, TextAlignmentOptions.TopLeft);
            Stretch(_list.rectTransform, 28, 120, 22, 250);

            _nextButton = Panel(rt, new Color(0.12f, 0.62f, 0.35f, 1f));
            _nextButton.anchorMin = _nextButton.anchorMax = new Vector2(0.5f, 0f);
            _nextButton.pivot = new Vector2(0.5f, 0f);
            _nextButton.sizeDelta = new Vector2(Width - 60, 110);
            _nextButton.anchoredPosition = new Vector2(0, 24);
            _nextButton.GetComponent<Image>().raycastTarget = true;
            _nextButton.gameObject.AddComponent<Button>().onClick.AddListener(() => NextRequested?.Invoke());
            var nextText = Text(_nextButton, 40, Color.white, TextAlignmentOptions.Center);
            nextText.fontStyle = FontStyles.Bold;
            Stretch(nextText.rectTransform, 0, 0, 0, 0);
            _nextButton.gameObject.SetActive(false);

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
                var t = all.FirstOrDefault(x => x.name == n && x.gameObject.activeInHierarchy);
                if (t) return t;
            }
            foreach (var n in ControllerNames)
            {
                var t = all.FirstOrDefault(x => x.name == n);
                if (t) return t;
            }
            return all.FirstOrDefault(x => x.name.Contains("Left") && x.name.Contains("Controller"));
        }
    }
}
