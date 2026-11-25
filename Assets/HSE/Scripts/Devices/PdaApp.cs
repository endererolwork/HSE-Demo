using System;
using System.Collections.Generic;
using System.Linq;
using HSE.Clinical;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HSE.Devices
{
    /// <summary>
    /// The nurse's PDA application: scan screen, patient record, lab request and confirmation.
    /// The whole UI is generated at runtime on a world-space canvas placed over the PDA's screen glass.
    /// </summary>
    public class PdaApp : MonoBehaviour
    {
        [Tooltip("Centre of the screen glass. The canvas's forward points into the glass, its up towards the scanner.")]
        public Transform screenAnchor;
        public Vector2 screenSizeMeters = new Vector2(0.068f, 0.128f);

        const float Width = 720f;
        static readonly Color Navy = Hex("16325C");
        static readonly Color Blue = Hex("1F5FBF");
        static readonly Color BlueLight = Hex("E8F0FC");
        static readonly Color Bg = Hex("F4F6F9");
        static readonly Color Card = Color.white;
        static readonly Color Ink = Hex("1C2430");
        static readonly Color Muted = Hex("6B7685");
        static readonly Color Line = Hex("DDE2E8");
        static readonly Color Green = Hex("1E9E5A");
        static readonly Color Red = Hex("D23B3B");
        static readonly Color RedLight = Hex("FDECEC");
        static readonly Color Amber = Hex("E8920C");

        PatientRecord _patient;
        bool _identityConfirmed;
        bool _urgent;
        readonly HashSet<string> _selected = new HashSet<string>();
        LabOrder _lastOrder;

        RectTransform _canvas, _body;
        TextMeshProUGUI _title, _clock;
        RectTransform _home, _patientScreen, _orderScreen, _confirmScreen;
        TextMeshProUGUI _homeStatus;

        public PatientRecord CurrentPatient => _patient;
        public bool IdentityConfirmed => _identityConfirmed;

        void Start()
        {
            BuildCanvas();
            ShowHome(null);
        }

        void Update()
        {
            if (_clock) _clock.text = DateTime.Now.ToString("HH:mm");
        }

        // ------------------------------------------------------------ scanner input

        public void OnCodeScanned(ScannableCode code)
        {
            if (code.kind != CodeKind.PatientWristband || code.patient == null)
            {
                ShowHome("<color=#" + ColorUtility.ToHtmlStringRGB(Amber) + ">Okunan kod bir hasta bilekliği değil.</color>");
                return;
            }
            if (_patient != code.patient)
            {
                _patient = code.patient;
                _identityConfirmed = false;
                _selected.Clear();
                _urgent = false;
            }
            ShowPatient();
        }

        // ------------------------------------------------------------ screens

        void ShowHome(string status)
        {
            SetScreen(_home, "Hasta Okut");
            _homeStatus.text = status ?? (_patient != null ? "Son okunan hasta: <b>" + _patient.fullName + "</b>" : "");
        }

        void ShowPatient()
        {
            Clear(_patientScreen);
            SetScreen(_patientScreen, "Hasta Bilgileri");
            var p = _patient;
            var col = Column(_patientScreen, 24, 18);

            var card = CardBox(col, 0);
            var cardCol = Column(card, 24, 6);
            Label(cardCol, p.fullName, 52, Ink, TextAlignmentOptions.Left, true, 64);
            Label(cardCol, $"{p.Age} yaş  ·  {p.gender}  ·  Kan grubu {p.bloodType}", 30, Muted, TextAlignmentOptions.Left, false, 40);

            var info = CardBox(col, 0);
            var infoCol = Column(info, 20, 4);
            Row(infoCol, "Protokol No", p.medicalRecordNumber);
            Row(infoCol, "Doğum Tarihi", p.dateOfBirth);
            Row(infoCol, "Servis / Oda", $"{p.ward} · Oda {p.room} · Yatak {p.bed}");
            Row(infoCol, "Tanı", p.diagnosis);
            Row(infoCol, "Sorumlu Hekim", p.attendingPhysician);

            var allergy = Box(col, RedLight, 64);
            Label(allergy, "ALERJİ:  " + p.allergies, 32, Red, TextAlignmentOptions.Center, true);

            var plan = CardBox(col, 0);
            var planCol = Column(plan, 20, 6);
            Label(planCol, "BAKIM PLANI", 24, Muted, TextAlignmentOptions.Left, true, 30);
            Label(planCol, p.carePlan, 28, Ink, TextAlignmentOptions.Left, false, 76);
            Label(planCol, "HEKİM İSTEMİ", 24, Muted, TextAlignmentOptions.Left, true, 30);
            var ordered = string.Join(", ", p.physicianOrderedTestIds.Select(id => LabTestCatalog.Get(id)?.Name ?? id));
            Label(planCol, ordered, 28, Ink, TextAlignmentOptions.Left, false, 76);

            Spacer(col);
            if (_identityConfirmed)
            {
                var done = Box(col, BlueLight, 70);
                Label(done, "Kimlik doğrulandı", 32, Green, TextAlignmentOptions.Center, true);
            }
            else
            {
                MakeButton(col, "Kimliği Doğrula", Green, Color.white, 96, () =>
                {
                    _identityConfirmed = true;
                    ClinicalEvents.RaiseIdentityConfirmed(_patient);
                    ShowPatient();
                });
            }
            var nav = HRow(col, 96, 16);
            MakeButton(nav, "Ana Ekran", Line, Ink, 96, () => ShowHome(null));
            var order = MakeButton(nav, "Tahlil İstemi", Blue, Color.white, 96, ShowOrder);
            order.interactable = _identityConfirmed;
        }

        void ShowOrder()
        {
            Clear(_orderScreen);
            SetScreen(_orderScreen, "Laboratuvar İstemi");
            var col = Column(_orderScreen, 20, 8);
            Label(col, $"{_patient.fullName}  ·  {_patient.medicalRecordNumber}", 28, Muted, TextAlignmentOptions.Left, false, 40);

            TextMeshProUGUI warning = null;
            foreach (SampleType sample in Enum.GetValues(typeof(SampleType)))
            {
                var tests = LabTestCatalog.All.Where(t => t.Sample == sample).ToList();
                if (tests.Count == 0) continue;
                Label(col, LabTestCatalog.SampleLabel(sample), 26, Blue, TextAlignmentOptions.Left, true, 40);
                foreach (var t in tests) TestRow(col, t, () => { if (warning) warning.text = ""; });
            }

            var seg = HRow(col, 78, 12);
            Label(seg, "Öncelik", 30, Ink, TextAlignmentOptions.Left, true).GetComponent<LayoutElement>().preferredWidth = 150;
            Button routine = null, urgent = null;
            void Paint()
            {
                PaintButton(routine, !_urgent ? Blue : Line, !_urgent ? Color.white : Ink);
                PaintButton(urgent, _urgent ? Red : Line, _urgent ? Color.white : Ink);
            }
            routine = MakeButton(seg, "Rutin", Line, Ink, 70, () => { _urgent = false; Paint(); });
            urgent = MakeButton(seg, "ACİL", Line, Ink, 70, () => { _urgent = true; Paint(); });
            Paint();

            warning = Label(col, "", 26, Red, TextAlignmentOptions.Center, true, 70);

            var nav = HRow(col, 96, 16);
            MakeButton(nav, "Geri", Line, Ink, 96, ShowPatient);
            MakeButton(nav, "İstemi Gönder", Blue, Color.white, 96, () =>
            {
                var problem = Validate();
                if (problem != null) { warning.text = problem; return; }
                Submit();
            });
        }

        void ShowConfirm()
        {
            Clear(_confirmScreen);
            SetScreen(_confirmScreen, "İstem Gönderildi");
            var col = Column(_confirmScreen, 28, 14);

            var badgeRow = HRow(col, 150, 0);
            var badgeHolder = new GameObject("Badge", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            badgeHolder.SetParent(badgeRow, false);
            badgeHolder.GetComponent<LayoutElement>().preferredHeight = 150;
            var circle = Box(badgeHolder, Green, 0);
            circle.anchorMin = circle.anchorMax = new Vector2(0.5f, 0.5f);
            circle.sizeDelta = new Vector2(140, 140);
            Check(circle);

            Label(col, "İstem laboratuvara iletildi", 40, Ink, TextAlignmentOptions.Center, true, 56);
            var refBox = Box(col, BlueLight, 110);
            var refCol = Column(refBox, 10, 0);
            Label(refCol, "Referans No", 24, Muted, TextAlignmentOptions.Center, false, 32);
            Label(refCol, _lastOrder.Reference, 44, Navy, TextAlignmentOptions.Center, true, 56);
            Label(col, $"{_lastOrder.Time:dd.MM.yyyy HH:mm}  ·  {(_lastOrder.Urgent ? "<color=#D23B3B><b>ACİL</b></color>" : "Rutin")}", 28, Muted, TextAlignmentOptions.Center, false, 40);

            var list = CardBox(col, 0);
            var listCol = Column(list, 18, 4);
            foreach (var t in _lastOrder.Tests)
            {
                var r = HRow(listCol, 48, 12);
                Dot(r, t.ContainerColor, 26);
                var name = Label(r, $"{t.Name}  <color=#6B7685><size=80%>{t.Container}</size></color>", 26, Ink, TextAlignmentOptions.Left, false);
                name.GetComponent<LayoutElement>().flexibleWidth = 1;
            }
            Label(col, "Sonraki adım: numune tüplerini hazırlayın ve etiketleyin.", 26, Muted, TextAlignmentOptions.Center, false, 70);
            Spacer(col);
            MakeButton(col, "Ana Ekran", Blue, Color.white, 96, () => ShowHome(null));
        }

        // ------------------------------------------------------------ order logic

        string Validate()
        {
            if (_selected.Count == 0) return "En az bir tahlil seçin.";
            var ordered = new HashSet<string>(_patient.physicianOrderedTestIds);
            var missing = ordered.Where(id => !_selected.Contains(id)).Select(id => LabTestCatalog.Get(id)?.Name ?? id).ToList();
            var extra = _selected.Where(id => !ordered.Contains(id)).Select(id => LabTestCatalog.Get(id)?.Name ?? id).ToList();
            if (missing.Count == 0 && extra.Count == 0) return null;
            var msg = "Hekim istemiyle uyuşmuyor.";
            if (missing.Count > 0) msg += "\nEksik: " + string.Join(", ", missing);
            if (extra.Count > 0) msg += "\nFazla: " + string.Join(", ", extra);
            return msg;
        }

        void Submit()
        {
            _lastOrder = new LabOrder
            {
                Patient = _patient,
                Urgent = _urgent,
                Time = DateTime.Now,
                Tests = LabTestCatalog.All.Where(t => _selected.Contains(t.Id)).ToList(),
            };
            _lastOrder.Reference = $"LAB-{_lastOrder.Time:yyMMdd}-{UnityEngine.Random.Range(1000, 9999)}";
            ClinicalEvents.RaiseLabOrderSubmitted(_lastOrder);
            ShowConfirm();
        }

        // ------------------------------------------------------------ canvas

        void BuildCanvas()
        {
            var anchor = screenAnchor ? screenAnchor : transform;
            float height = Width * screenSizeMeters.y / screenSizeMeters.x;

            var go = new GameObject("PdaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(anchor, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            var raycasterType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycasterType != null) go.AddComponent(raycasterType);
            go.AddComponent<GraphicRaycaster>();

            _canvas = go.GetComponent<RectTransform>();
            _canvas.sizeDelta = new Vector2(Width, height);
            _canvas.localScale = Vector3.one * (screenSizeMeters.x / Width);
            _canvas.localPosition = Vector3.zero;
            _canvas.localRotation = Quaternion.identity;

            Fill(Box(_canvas, Bg, 0));

            var status = Box(_canvas, Navy, 0);
            Dock(status, true, 0, 56);
            _clock = Label(status, "", 26, Color.white, TextAlignmentOptions.Left, true);
            Pad(_clock.rectTransform, 24, 0);
            var brand = Label(status, "HSE Klinik  ·  Wi-Fi  ·  %86", 22, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.Right, false);
            Pad(brand.rectTransform, 24, 0);

            var header = Box(_canvas, Blue, 0);
            Dock(header, true, 56, 104);
            _title = Label(header, "", 42, Color.white, TextAlignmentOptions.Left, true);
            Pad(_title.rectTransform, 28, 0);

            _body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            _body.SetParent(_canvas, false);
            Fill(_body);
            _body.offsetMax = new Vector2(0, -160);

            _home = MakeScreen("Home");
            _patientScreen = MakeScreen("Patient");
            _orderScreen = MakeScreen("Order");
            _confirmScreen = MakeScreen("Confirm");
            BuildHome();
        }

        void BuildHome()
        {
            var col = Column(_home, 40, 24);
            Spacer(col);
            var frameRow = HRow(col, 300, 0);
            var holder = new GameObject("ScanFrame", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(frameRow, false);
            holder.GetComponent<LayoutElement>().preferredHeight = 300;
            ScanFrame(holder);
            Label(col, "Hasta bilekliğini okutun", 46, Ink, TextAlignmentOptions.Center, true, 70);
            Label(col, "Tetiği basılı tutun ve kırmızı ışını bileklikteki QR koda tutun.", 30, Muted, TextAlignmentOptions.Center, false, 90);
            _homeStatus = Label(col, "", 28, Ink, TextAlignmentOptions.Center, false, 80);
            Spacer(col);
        }

        RectTransform MakeScreen(string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(_body, false);
            Fill(rt);
            rt.gameObject.SetActive(false);
            return rt;
        }

        void SetScreen(RectTransform screen, string title)
        {
            foreach (var s in new[] { _home, _patientScreen, _orderScreen, _confirmScreen }) s.gameObject.SetActive(s == screen);
            _title.text = title;
        }

        static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------ widgets

        void TestRow(RectTransform parent, LabTest t, Action changed)
        {
            var row = Box(parent, Card, 0);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 8, 8);
            h.spacing = 14;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var box = Box(row, Color.white, 0);
            var boxLe = box.gameObject.AddComponent<LayoutElement>();
            boxLe.preferredWidth = 44; boxLe.preferredHeight = 44; boxLe.flexibleHeight = 0;
            var border = box.gameObject.AddComponent<Outline>();
            border.effectColor = Muted; border.effectDistance = new Vector2(2, -2);
            var tick = Box(box, Blue, 0);
            Fill(tick); tick.offsetMin = new Vector2(7, 7); tick.offsetMax = new Vector2(-7, -7);

            var label = Label(row, $"{t.Name}\n<size=70%><color=#6B7685>{t.Detail}</color></size>", 27, Ink, TextAlignmentOptions.Left, false);
            label.GetComponent<LayoutElement>().flexibleWidth = 1;
            Dot(row, t.ContainerColor, 26);

            void Refresh()
            {
                bool on = _selected.Contains(t.Id);
                tick.gameObject.SetActive(on);
                row.GetComponent<Image>().color = on ? BlueLight : Card;
            }
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (!_selected.Remove(t.Id)) _selected.Add(t.Id);
                Refresh();
                changed?.Invoke();
            });
            Refresh();
        }

        void Row(RectTransform parent, string label, string value)
        {
            var r = HRow(parent, 50, 12);
            Label(r, label, 26, Muted, TextAlignmentOptions.Left, false).GetComponent<LayoutElement>().preferredWidth = 210;
            Label(r, value, 28, Ink, TextAlignmentOptions.Left, true).GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        static RectTransform Column(RectTransform parent, int pad, float spacing)
        {
            var v = parent.gameObject.GetComponent<VerticalLayoutGroup>() ?? parent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return parent;
        }

        static RectTransform HRow(RectTransform parent, float height, float spacing)
        {
            var rt = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.GetComponent<LayoutElement>().preferredHeight = height;
            var h = rt.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return rt;
        }

        static RectTransform CardBox(RectTransform parent, float height)
        {
            var rt = Box(parent, Card, height);
            return rt;
        }

        static RectTransform Box(RectTransform parent, Color color, float height)
        {
            var rt = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.GetComponent<Image>().color = color;
            if (height > 0) rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return rt;
        }

        static void Spacer(RectTransform parent)
        {
            var rt = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.GetComponent<LayoutElement>().flexibleHeight = 1;
        }

        static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, bool bold, float height = 0)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (height > 0) go.GetComponent<LayoutElement>().preferredHeight = height;
            if (parent.GetComponent<LayoutGroup>() == null) Fill(t.rectTransform);
            return t;
        }

        static Button MakeButton(RectTransform parent, string label, Color bg, Color fg, float height, UnityAction onClick)
        {
            var rt = Box(parent, bg, height);
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.disabledColor = new Color(1, 1, 1, 0.35f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            Label(rt, label, 34, fg, TextAlignmentOptions.Center, true);
            return b;
        }

        static void PaintButton(Button b, Color bg, Color fg)
        {
            b.GetComponent<Image>().color = bg;
            b.GetComponentInChildren<TextMeshProUGUI>().color = fg;
        }

        static void Dot(RectTransform parent, Color c, float size)
        {
            var rt = Box(parent, c, 0);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size; le.preferredHeight = size; le.flexibleWidth = 0;
        }

        static void Check(RectTransform circle)
        {
            var a = Box(circle, Color.white, 0);
            a.anchorMin = a.anchorMax = new Vector2(0.5f, 0.5f);
            a.sizeDelta = new Vector2(16, 44);
            a.anchoredPosition = new Vector2(-18, -6);
            a.localRotation = Quaternion.Euler(0, 0, 45);
            var b = Box(circle, Color.white, 0);
            b.anchorMin = b.anchorMax = new Vector2(0.5f, 0.5f);
            b.sizeDelta = new Vector2(16, 80);
            b.anchoredPosition = new Vector2(12, 4);
            b.localRotation = Quaternion.Euler(0, 0, -40);
        }

        static void ScanFrame(RectTransform holder)
        {
            const float s = 240, len = 70, th = 10;
            foreach (var (x, y) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) })
            {
                var h = Box(holder, Blue, 0);
                h.anchorMin = h.anchorMax = new Vector2(0.5f, 0.5f);
                h.sizeDelta = new Vector2(len, th);
                h.anchoredPosition = new Vector2(x * (s / 2 - len / 2), y * (s / 2 - th / 2));
                var v = Box(holder, Blue, 0);
                v.anchorMin = v.anchorMax = new Vector2(0.5f, 0.5f);
                v.sizeDelta = new Vector2(th, len);
                v.anchoredPosition = new Vector2(x * (s / 2 - th / 2), y * (s / 2 - len / 2));
            }
            var beam = Box(holder, Red, 0);
            beam.anchorMin = beam.anchorMax = new Vector2(0.5f, 0.5f);
            beam.sizeDelta = new Vector2(s - 40, 6);
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Dock(RectTransform rt, bool top, float offset, float height)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -offset);
            rt.sizeDelta = new Vector2(0, height);
        }

        static void Pad(RectTransform rt, float x, float y)
        {
            rt.offsetMin = new Vector2(x, y); rt.offsetMax = new Vector2(-x, -y);
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
