using System;
using System.Collections.Generic;
using System.Linq;
using HSE.Clinical;
using HSE.Localization;
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

        Action _redraw;
        PatientRecord _patient;
        bool _identityConfirmed;
        bool _urgent;
        readonly HashSet<string> _selected = new HashSet<string>();
        LabOrder _lastOrder;

        RectTransform _canvas, _body;
        TextMeshProUGUI _title, _clock;
        RectTransform _home, _patientScreen, _orderScreen, _confirmScreen, _documentScreen, _specimenScreen;
        TextMeshProUGUI _homeStatus;

        public PatientRecord CurrentPatient => _patient;

        /// <summary>Back to the scan screen with no patient (used when the next patient starts).</summary>
        public void ResetApp()
        {
            _patient = null;
            _identityConfirmed = false;
            _urgent = false;
            _selected.Clear();
            _lastOrder = null;
            _patientTab = PatientTab.Details;
            _bpMeasured = _vitalsRecorded = _escalated = false;
            ShowHome(null);
        }
        public bool IdentityConfirmed => _identityConfirmed;

        void Awake()
        {
            // Built in Awake so highlight services see the screen canvas before any Start runs.
            BuildCanvas();
            ShowHome(null);
        }

        void OnEnable()
        {
            Loc.Changed += OnLanguageChanged;
            ClinicalEvents.BloodPressureMeasured += OnBpMeasured;
            ClinicalEvents.ProcedureEvent += OnProcedure;
        }

        void OnDisable()
        {
            Loc.Changed -= OnLanguageChanged;
            ClinicalEvents.BloodPressureMeasured -= OnBpMeasured;
            ClinicalEvents.ProcedureEvent -= OnProcedure;
        }

        /// <summary>Rebuilds the UI in the new language and shows the same screen again.</summary>
        void OnLanguageChanged()
        {
            if (_canvas) Destroy(_canvas.gameObject);
            BuildCanvas();
            (_redraw ?? (() => ShowHome(null)))();
        }

        void Update()
        {
            if (_clock) _clock.text = DateTime.Now.ToString("HH:mm");
        }

        // ------------------------------------------------------------ scanner input

        public void OnCodeScanned(ScannableCode code)
        {
            if (code.kind == CodeKind.SpecimenBag)
            {
                ShowSpecimen(code);
                return;
            }
            if (code.kind != CodeKind.PatientWristband || code.patient == null)
            {
                ShowHome("<color=#" + ColorUtility.ToHtmlStringRGB(Amber) + ">" + Loc.T("pda.scan.notWristband") + "</color>");
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
        // Font sizes are in canvas units (720 wide = 68 mm), so 36 is about 3.4 mm tall on the device.

        enum PatientTab { Details, Orders, Vitals, Results }
        PatientTab _patientTab = PatientTab.Details;
        SampleType _orderTab = SampleType.Blood;

        void ShowHome(string status)
        {
            _redraw = () => ShowHome(status);
            SetScreen(_home, Loc.T("pda.title.scan"));
            _homeStatus.text = status ?? (_patient != null ? Loc.F("pda.scan.last", _patient.fullName) : "");
        }

        void ShowPatient()
        {
            _redraw = ShowPatient;
            Clear(_patientScreen);
            SetScreen(_patientScreen, Loc.T("pda.title.patient"));
            var p = _patient;
            var col = Column(_patientScreen, 22, 14);

            var tabs = HRow(col, 92, 10);
            Tab(tabs, Loc.T("pda.tab.patient"), _patientTab == PatientTab.Details, () => { _patientTab = PatientTab.Details; ShowPatient(); });
            Tab(tabs, Loc.T("pda.tab.orders"), _patientTab == PatientTab.Orders, () => { _patientTab = PatientTab.Orders; ShowPatient(); });
            Tab(tabs, Loc.T("pda.tab.vitals"), _patientTab == PatientTab.Vitals, () => { _patientTab = PatientTab.Vitals; ShowPatient(); });
            Tab(tabs, Loc.T("pda.tab.results"), _patientTab == PatientTab.Results, () => { _patientTab = PatientTab.Results; ShowPatient(); });
            foreach (var t in tabs.GetComponentsInChildren<TextMeshProUGUI>()) t.fontSize = 26;

            var card = Box(col, Card, 0);
            var cardCol = Column(card, 20, 4);
            Label(cardCol, p.fullName, 58, Ink, TextAlignmentOptions.Left, true, 72);
            Label(cardCol, Loc.F("pda.age", p.Age, p.Gender, p.bloodType), 32, Muted, TextAlignmentOptions.Left, false, 44);

            if (_patientTab == PatientTab.Details)
            {
                var info = Box(col, Card, 0);
                var infoCol = Column(info, 18, 2);
                Row(infoCol, Loc.T("pda.mrn"), p.medicalRecordNumber);
                Row(infoCol, Loc.T("pda.dob"), p.dateOfBirth);
                Row(infoCol, Loc.T("pda.ward"), p.Ward, 96);
                Row(infoCol, Loc.T("pda.roomBed"), $"{p.room} / {p.bed}");
                Row(infoCol, Loc.T("pda.diagnosis"), p.Diagnosis, 120);
                Row(infoCol, Loc.T("pda.physician"), p.attendingPhysician);

                var allergy = Box(col, RedLight, 84);
                Label(allergy, Loc.T("pda.allergy") + "  " + p.Allergies, 36, Red, TextAlignmentOptions.Center, true);
            }
            else if (_patientTab == PatientTab.Vitals) VitalsPage(col);
            else if (_patientTab == PatientTab.Results) ResultsPage(col);
            else
            {
                var plan = Box(col, Card, 0);
                var planCol = Column(plan, 20, 6);
                Label(planCol, Loc.T("pda.carePlan"), 28, Muted, TextAlignmentOptions.Left, true, 38);
                Label(planCol, p.CarePlan, 32, Ink, TextAlignmentOptions.Left, false, 100);

                var orders = Box(col, Card, 0);
                var ordersCol = Column(orders, 20, 6);
                Label(ordersCol, Loc.T("pda.physicianOrder"), 28, Muted, TextAlignmentOptions.Left, true, 38);
                foreach (var id in p.physicianOrderedTestIds)
                {
                    var t = LabTestCatalog.Get(id);
                    if (t == null) continue;
                    var r = HRow(ordersCol, 58, 14);
                    Dot(r, t.ContainerColor, 30);
                    Label(r, t.Name, 34, Ink, TextAlignmentOptions.Left, false).GetComponent<LayoutElement>().flexibleWidth = 1;
                }
                SpecimenActions(col);
            }

            Spacer(col);
            if (_identityConfirmed)
            {
                var done = Box(col, BlueLight, 80);
                Label(done, Loc.T("pda.identityConfirmed"), 36, Green, TextAlignmentOptions.Center, true);
            }
            else
            {
                var idRow = HRow(col, 112, 14);
                MakeButton(idRow, Loc.T("pda.confirmIdentity"), Green, Color.white, 112, () =>
                {
                    ClinicalEvents.RaiseIdentityConfirmed(_patient);
                    // Confirming the wrong person does not unlock the record.
                    if (_patient == ActivePatient.Current) _identityConfirmed = true;
                    ShowPatient();
                });
                // GDD branch: the band does not match the patient -> stop and report.
                MakeButton(idRow, Loc.T("pda.reportMismatch"), RedLight, Red, 112, () => ClinicalEvents.RaiseProcedure("id_mismatch_reported"))
                    .GetComponentInChildren<TextMeshProUGUI>().fontSize = 28;
            }
            var nav = HRow(col, 112, 14);
            MakeButton(nav, Loc.T("pda.home"), Line, Ink, 112, () => ShowHome(null));
            var order = MakeButton(nav, Loc.T("pda.labRequest"), Blue, Color.white, 112, () => { _orderTab = SampleType.Blood; ShowOrder(); });
            order.interactable = _identityConfirmed;
        }

        // ------------------------------------------------------------ vitals and results pages

        void VitalsPage(RectTransform col)
        {
            var v = HSE.Clinical.Vitals.VitalsFeed.Current;
            var grid = Box(col, Card, 0);
            var gcol = Column(grid, 16, 2);
            var r1 = HRow(gcol, 52, 10);
            Label(r1, $"<color=#1E9E5A>HR</color> <b>{v.HeartRate:0}</b>", 34, Ink, TextAlignmentOptions.Left, false);
            Label(r1, $"<color=#1F5FBF>SpO2</color> <b>{v.SpO2:0}%</b>", 34, Ink, TextAlignmentOptions.Left, false);
            var r2 = HRow(gcol, 52, 10);
            Label(r2, $"<color=#D23B3B>BP</color> <b>{v.Systolic:0}/{v.Diastolic:0}</b>", 34, Ink, TextAlignmentOptions.Left, false);
            Label(r2, $"<color=#E8920C>Temp</color> <b>{v.Temperature:0.0}</b>", 34, Ink, TextAlignmentOptions.Left, false);
            Label(col, Loc.T("pda.vitals.trend"), 26, Muted, TextAlignmentOptions.Left, true, 34);
            var h = HSE.Clinical.Vitals.VitalsFeed.History;
            Chart(col, "HR", h.Select(x => x.HeartRate).ToList(), 40, 140, 60, 100, Green);
            Chart(col, "SpO2 %", h.Select(x => x.SpO2).ToList(), 85, 100, 94, 100, Blue);

            // Record the bedside measurement into the patient record.
            if (_bpMeasured)
            {
                bool critical = v.Systolic >= 180 || v.Diastolic >= 110 || v.Systolic < 90;
                bool abnormal = critical || v.Systolic >= 140 || v.Systolic < 100 || v.Diastolic >= 90 || v.Diastolic < 60 || v.HeartRate > 100 || v.HeartRate < 50;
                if (critical && !_escalated)
                {
                    // GDD branch: critical blood pressure must be escalated before carrying on.
                    Label(col, Loc.T("pda.vitals.critical"), 26, Red, TextAlignmentOptions.Center, true, 70);
                    MakeButton(col, Loc.T("pda.vitals.escalate"), Red, Color.white, 96, () =>
                    {
                        _escalated = true;
                        ClinicalEvents.RaiseProcedure("bp_escalated");
                        ShowPatient();
                    });
                    return;
                }
                if (abnormal) Label(col, Loc.T(critical ? "pda.vitals.escalated" : "pda.vitals.abnormal"), 26, Red, TextAlignmentOptions.Center, true, 70);
                if (_vitalsRecorded)
                {
                    var done = Box(col, BlueLight, 70);
                    Label(done, Loc.T("pda.vitals.recorded"), 32, Green, TextAlignmentOptions.Center, true);
                }
                else
                    MakeButton(col, Loc.F("pda.vitals.record", $"{v.Systolic:0}/{v.Diastolic:0}", $"{v.HeartRate:0}"), Green, Color.white, 96, () =>
                    {
                        _vitalsRecorded = true;
                        ClinicalEvents.RaiseVitalsRecorded(_patient, v);
                        ShowPatient();
                    });
            }
            else Label(col, Loc.T("pda.vitals.measureFirst"), 26, Muted, TextAlignmentOptions.Center, false, 60);
        }

        bool _bpMeasured, _vitalsRecorded, _escalated;

        void OnBpMeasured(HSE.Clinical.Vitals.VitalSample _)
        {
            _bpMeasured = true;
            _vitalsRecorded = false;
            if (_patient != null)
            {
                // Jump straight to the Vitals page so the reading can be recorded.
                _patientTab = PatientTab.Vitals;
                if (_patientScreen.gameObject.activeSelf) ShowPatient();
            }
        }

        void ResultsPage(RectTransform col)
        {
            var lab = FindFirstObjectByType<HSE.Clinical.Lab.LabService>();
            var report = lab ? lab.Latest : null;
            if (report == null || report.Patient != _patient)
            {
                var box = Box(col, Card, 160);
                Label(box, Loc.T("pda.results.none"), 32, Muted, TextAlignmentOptions.Center, false);
                return;
            }
            var list = Box(col, Card, 0);
            var lcol = Column(list, 16, 2);
            var flagged = report.Results.Where(r => r.IsOutOfRange || r.Flag == HSE.Clinical.Lab.ResultFlag.Borderline).ToList();
            Label(lcol, Loc.F("pda.results.summary", report.OutOfRange.Count(), report.Borderline.Count()), 28, Muted, TextAlignmentOptions.Left, true, 40);
            foreach (var r in flagged.Take(8))
            {
                var row = HRow(lcol, 54, 10);
                Label(row, r.Name, 30, Ink, TextAlignmentOptions.Left, false).GetComponent<LayoutElement>().flexibleWidth = 1;
                var color = r.IsOutOfRange ? "#C62828" : "#B26A00";
                Label(row, $"<color={color}><b>{r.Value}</b></color> <size=70%>{r.Unit}</size>", 30, Ink, TextAlignmentOptions.Right, false).GetComponent<LayoutElement>().preferredWidth = 260;
            }
            if (flagged.Count == 0) Label(lcol, Loc.T("pda.results.allNormal"), 30, Green, TextAlignmentOptions.Left, true, 50);
            MakeButton(col, Loc.T("pda.results.open"), Blue, Color.white, 100, () =>
            {
                var panel = FindFirstObjectByType<HSE.Training.UI.BloodReportPanel>(FindObjectsInactive.Include);
                if (panel) panel.Show(report);
            });
        }

        /// <summary>Small line chart drawn with UI segments; the shaded band is the normal range.</summary>
        void Chart(RectTransform col, string title, List<float> values, float min, float max, float normLo, float normHi, Color color)
        {
            const float w = 640f, hgt = 86f;
            var box = Box(col, Card, hgt + 44);
            var t = Label(box, $"<b>{title}</b>", 24, Muted, TextAlignmentOptions.TopLeft, false);
            t.rectTransform.offsetMin = new Vector2(12, 0); t.rectTransform.offsetMax = new Vector2(-12, -4);
            var area = new GameObject("Plot", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(box, false);
            area.anchorMin = new Vector2(0, 0); area.anchorMax = new Vector2(1, 0);
            area.pivot = new Vector2(0.5f, 0);
            area.sizeDelta = new Vector2(-24, hgt);
            area.anchoredPosition = new Vector2(0, 8);

            var band = Box(area, new Color(color.r, color.g, color.b, 0.12f), 0);
            band.anchorMin = new Vector2(0, Mathf.InverseLerp(min, max, normLo));
            band.anchorMax = new Vector2(1, Mathf.InverseLerp(min, max, normHi));
            band.offsetMin = band.offsetMax = Vector2.zero;

            if (values.Count < 2) return;
            Vector2 P(int i) => new Vector2(i / (float)(values.Count - 1) * w, Mathf.InverseLerp(min, max, values[i]) * hgt);
            for (int i = 0; i < values.Count - 1; i++)
            {
                Vector2 a = P(i), b = P(i + 1), d = b - a;
                var seg = Box(area, color, 0);
                seg.anchorMin = seg.anchorMax = Vector2.zero;
                seg.pivot = new Vector2(0, 0.5f);
                seg.sizeDelta = new Vector2(d.magnitude, 4);
                seg.anchoredPosition = a;
                seg.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            var last = Box(area, color, 0);
            last.anchorMin = last.anchorMax = Vector2.zero;
            last.sizeDelta = new Vector2(12, 12);
            last.anchoredPosition = P(values.Count - 1);
        }

        // ------------------------------------------------------------ labels, documentation, specimen dispatch

        BloodDrawSession Draw => FindFirstObjectByType<BloodDrawSession>();

        /// <summary>Print labels after the blood draw, then document the collection.</summary>
        void SpecimenActions(RectTransform col)
        {
            var draw = Draw;
            if (!draw || !draw.IsDone("tubes_complete")) return;
            if (!draw.IsDone("labels_printed"))
            {
                MakeButton(col, Loc.F("pda.labels.print", draw.RequiredTubes.Count), Blue, Color.white, 96, PrintLabels);
            }
            else if (!draw.IsDone("collection_documented"))
            {
                MakeButton(col, Loc.T("pda.doc.open"), Blue, Color.white, 96, () => { _docSite = null; _docTolerance = null; ShowDocument(); });
            }
        }

        void PrintLabels()
        {
            var draw = Draw;
            if (!draw.IsDone("wristband_rescanned"))
            {
                // GDD 5: identity must be verified again before labelling.
                ClinicalEvents.RaiseProcedureMistake("mistake.verifyBeforeLabel");
                ShowHome("<color=#" + ColorUtility.ToHtmlStringRGB(Red) + ">" + Loc.T("mistake.verifyBeforeLabel") + "</color>");
                return;
            }
            var printer = FindFirstObjectByType<LabelPrinterDevice>();
            var fill = FindFirstObjectByType<TubeFilling>();
            if (!printer || !fill) return;
            var tubes = fill.Tubes.Where(t => t && draw.RequiredTubes.Contains(t.name)).Select(t => t.transform);
            printer.PrintLabels(_patient, tubes, draw.CollectedAt);
            ClinicalEvents.RaiseProcedure("labels_printed");
            ShowPatient();
        }

        string _docSite, _docTolerance;

        void ShowDocument()
        {
            _redraw = ShowDocument;
            Clear(_documentScreen);
            SetScreen(_documentScreen, Loc.T("pda.doc.title"));
            var draw = Draw;
            var col = Column(_documentScreen, 20, 10);
            Label(col, $"{_patient.fullName}  ·  {_patient.medicalRecordNumber}", 30, Muted, TextAlignmentOptions.Left, false, 42);

            var info = Box(col, Card, 0);
            var icol = Column(info, 16, 2);
            Row(icol, Loc.T("pda.doc.time"), draw ? draw.CollectedAt.ToString("dd.MM.yyyy HH:mm") : "-");
            Row(icol, Loc.T("pda.doc.tubes"), draw ? $"{draw.RequiredTubes.Count}: " + string.Join(", ", draw.RequiredTubes.Select(t => t.Replace("Tube_", "").Replace("_", " "))) : "-", 96);
            var v = HSE.Clinical.Vitals.VitalsFeed.Current;
            Row(icol, Loc.T("pda.doc.vitals"), _vitalsRecorded ? $"BP {v.Systolic:0}/{v.Diastolic:0}, HR {v.HeartRate:0}" : "-");

            Label(col, Loc.T("pda.doc.site"), 28, Muted, TextAlignmentOptions.Left, true, 38);
            var sites = HRow(col, 90, 8);
            foreach (var site in new[] { "R antecubital", "L antecubital", "R hand" })
            {
                var s = site;
                Tab(sites, s, _docSite == s, () => { _docSite = s; ShowDocument(); });
            }
            Label(col, Loc.T("pda.doc.tolerance"), 28, Muted, TextAlignmentOptions.Left, true, 38);
            var tol = HRow(col, 90, 8);
            foreach (var option in new[] { "Tolerated well", "Bruising", "Felt faint" })
            {
                var o = option;
                Tab(tol, o, _docTolerance == o, () => { _docTolerance = o; ShowDocument(); });
            }
            foreach (var t in sites.GetComponentsInChildren<TextMeshProUGUI>()) t.fontSize = 24;
            foreach (var t in tol.GetComponentsInChildren<TextMeshProUGUI>()) t.fontSize = 24;

            var warning = Label(col, "", 28, Red, TextAlignmentOptions.Center, true, 70);
            Spacer(col);
            var nav = HRow(col, 112, 14);
            MakeButton(nav, Loc.T("pda.back"), Line, Ink, 112, ShowPatient);
            MakeButton(nav, Loc.T("pda.doc.save"), Green, Color.white, 112, () =>
            {
                if (_docSite == null || _docTolerance == null) { warning.text = Loc.T("pda.doc.incomplete"); return; }
                if (_docSite != "R antecubital")
                {
                    ClinicalEvents.RaiseProcedureMistake("mistake.wrongSiteDocumented");
                    warning.text = Loc.T("mistake.wrongSiteDocumented");
                    return;
                }
                ClinicalEvents.RaiseProcedure("collection_documented");
                ShowMessage(Loc.T("pda.doc.done"), Green);
            });
        }

        ScannableCode _bagCode;

        /// <summary>GDD 8: scanning the sealed bag links the specimen to patient, request, time, nurse and lab.</summary>
        void ShowSpecimen(ScannableCode code)
        {
            _bagCode = code;
            _redraw = () => ShowSpecimen(code);
            Clear(_specimenScreen);
            SetScreen(_specimenScreen, Loc.T("pda.spec.title"));
            var draw = Draw;
            var col = Column(_specimenScreen, 22, 12);
            var info = Box(col, Card, 0);
            var icol = Column(info, 16, 2);
            var p = ActivePatient.Current;
            Row(icol, Loc.T("pda.spec.code"), code.code);
            Row(icol, Loc.T("pda.spec.patient"), p ? $"{p.fullName} · {p.medicalRecordNumber}" : "-", 96);
            Row(icol, Loc.T("pda.spec.request"), _lastOrder != null ? _lastOrder.Reference : "-");
            Row(icol, Loc.T("pda.doc.time"), draw ? draw.CollectedAt.ToString("dd.MM.yyyy HH:mm") : "-");
            var printer = FindFirstObjectByType<LabelPrinterDevice>();
            Row(icol, Loc.T("pda.spec.nurse"), printer ? printer.NurseId : "-");
            Row(icol, Loc.T("pda.spec.lab"), "Central Laboratory");
            Spacer(col);
            if (_lastOrder == null)
            {
                Label(col, Loc.T("pda.spec.noRequest"), 30, Red, TextAlignmentOptions.Center, true, 90);
                return;
            }
            if (draw && draw.IsDone("dispatch_requested"))
            {
                var box = Box(col, BlueLight, 90);
                Label(box, Loc.T("pda.spec.placeInTube"), 30, Navy, TextAlignmentOptions.Center, true);
                return;
            }
            MakeButton(col, Loc.T("pda.spec.send"), Green, Color.white, 112, () =>
            {
                ClinicalEvents.RaiseProcedure("dispatch_requested");
                ShowSpecimen(code);
            });
        }

        void ShowMessage(string text, Color color)
        {
            _redraw = () => ShowMessage(text, color);
            Clear(_specimenScreen);
            SetScreen(_specimenScreen, "");
            var col = Column(_specimenScreen, 30, 20);
            Spacer(col);
            Label(col, text, 44, color, TextAlignmentOptions.Center, true, 160);
            Spacer(col);
            MakeButton(col, Loc.T("pda.home"), Blue, Color.white, 112, () => { if (_patient) ShowPatient(); else ShowHome(null); });
        }

        void OnProcedure(string id)
        {
            if (id == "specimen_dispatched") ShowMessage(Loc.T("pda.spec.dispatched"), Green);
            if (id == "wristband_replaced")
            {
                _patient = null;
                _identityConfirmed = false;
                ShowHome("<color=#" + ColorUtility.ToHtmlStringRGB(Amber) + ">" + Loc.T("pda.newWristband") + "</color>");
            }
        }

        void ShowOrder()
        {
            _redraw = ShowOrder;
            Clear(_orderScreen);
            SetScreen(_orderScreen, Loc.T("pda.title.order"));
            var col = Column(_orderScreen, 20, 10);
            Label(col, $"{_patient.fullName}  ·  {_patient.medicalRecordNumber}", 30, Muted, TextAlignmentOptions.Left, false, 42);

            // One page per sample type keeps the rows big enough to hit in VR.
            var tabs = HRow(col, 92, 10);
            foreach (SampleType sample in Enum.GetValues(typeof(SampleType)))
            {
                int count = LabTestCatalog.All.Count(t => t.Sample == sample && _selected.Contains(t.Id));
                var label = LabTestCatalog.SampleLabel(sample) + (count > 0 ? $" ({count})" : "");
                var captured = sample;
                Tab(tabs, label, _orderTab == sample, () => { _orderTab = captured; ShowOrder(); });
            }

            TextMeshProUGUI warning = null;
            TextMeshProUGUI selectedCount = null;
            foreach (var t in LabTestCatalog.All.Where(t => t.Sample == _orderTab))
                TestRow(col, t, () =>
                {
                    if (warning) warning.text = "";
                    if (selectedCount) selectedCount.text = Loc.F("pda.selected", _selected.Count);
                });

            Spacer(col);
            selectedCount = Label(col, Loc.F("pda.selected", _selected.Count), 30, Muted, TextAlignmentOptions.Left, true, 44);

            var seg = HRow(col, 86, 12);
            Label(seg, Loc.T("pda.priority"), 32, Ink, TextAlignmentOptions.Left, true).GetComponent<LayoutElement>().preferredWidth = 170;
            Button routine = null, urgent = null;
            void Paint()
            {
                PaintButton(routine, !_urgent ? Blue : Line, !_urgent ? Color.white : Ink);
                PaintButton(urgent, _urgent ? Red : Line, _urgent ? Color.white : Ink);
            }
            routine = MakeButton(seg, Loc.T("pda.routine"), Line, Ink, 86, () => { _urgent = false; Paint(); });
            urgent = MakeButton(seg, Loc.T("pda.urgent"), Line, Ink, 86, () => { _urgent = true; Paint(); });
            Paint();

            warning = Label(col, "", 28, Red, TextAlignmentOptions.Center, true, 84);

            var nav = HRow(col, 112, 14);
            MakeButton(nav, Loc.T("pda.back"), Line, Ink, 112, ShowPatient);
            MakeButton(nav, Loc.T("pda.send"), Blue, Color.white, 112, () =>
            {
                var problem = Validate();
                if (problem != null)
                {
                    warning.text = problem;
                    ClinicalEvents.RaiseLabOrderRejected(problem.Replace("\n", " "));
                    return;
                }
                Submit();
            });
        }

        void ShowConfirm()
        {
            _redraw = ShowConfirm;
            Clear(_confirmScreen);
            SetScreen(_confirmScreen, Loc.T("pda.title.sent"));
            var col = Column(_confirmScreen, 26, 14);

            var badgeRow = HRow(col, 150, 0);
            var badgeHolder = new GameObject("Badge", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            badgeHolder.SetParent(badgeRow, false);
            badgeHolder.GetComponent<LayoutElement>().preferredHeight = 150;
            var circle = Box(badgeHolder, Green, 0);
            circle.anchorMin = circle.anchorMax = new Vector2(0.5f, 0.5f);
            circle.sizeDelta = new Vector2(140, 140);
            Check(circle);

            Label(col, Loc.T("pda.sent.headline"), 42, Ink, TextAlignmentOptions.Center, true, 110);
            var refBox = Box(col, BlueLight, 120);
            var refCol = Column(refBox, 10, 0);
            Label(refCol, Loc.T("pda.sent.ref"), 28, Muted, TextAlignmentOptions.Center, false, 36);
            Label(refCol, _lastOrder.Reference, 48, Navy, TextAlignmentOptions.Center, true, 60);
            Label(col, $"{_lastOrder.Time:dd.MM.yyyy HH:mm}  ·  {(_lastOrder.Urgent ? "<color=#D23B3B><b>" + Loc.T("pda.urgent") + "</b></color>" : Loc.T("pda.routine"))}", 32, Muted, TextAlignmentOptions.Center, false, 46);

            var list = Box(col, Card, 0);
            var listCol = Column(list, 18, 4);
            foreach (var t in _lastOrder.Tests)
            {
                var r = HRow(listCol, 56, 14);
                Dot(r, t.ContainerColor, 30);
                var name = Label(r, $"{t.Name}  <color=#6B7685><size=75%>{t.Container}</size></color>", 32, Ink, TextAlignmentOptions.Left, false);
                name.GetComponent<LayoutElement>().flexibleWidth = 1;
            }
            Label(col, Loc.T("pda.sent.next"), 30, Muted, TextAlignmentOptions.Center, false, 90);
            Spacer(col);
            MakeButton(col, Loc.T("pda.home"), Blue, Color.white, 112, () => ShowHome(null));
        }

        // ------------------------------------------------------------ order logic

        string Validate()
        {
            if (_selected.Count == 0) return Loc.T("pda.selectOne");
            var ordered = new HashSet<string>(_patient.physicianOrderedTestIds);
            var missing = ordered.Where(id => !_selected.Contains(id)).Select(id => LabTestCatalog.Get(id)?.Name ?? id).ToList();
            var extra = _selected.Where(id => !ordered.Contains(id)).Select(id => LabTestCatalog.Get(id)?.Name ?? id).ToList();
            if (missing.Count == 0 && extra.Count == 0) return null;
            var msg = Loc.T("pda.mismatch");
            if (missing.Count > 0) msg += "\n" + Loc.T("pda.missing") + ": " + string.Join(", ", missing);
            if (extra.Count > 0) msg += "\n" + Loc.T("pda.extra") + ": " + string.Join(", ", extra);
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
            var printer = FindFirstObjectByType<LabelPrinterDevice>();
            if (printer) printer.PrintRequestSlip(_lastOrder); // goes in the specimen bag's outer pocket
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
            var brand = Label(status, Loc.T("pda.status"), 22, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.Right, false);
            Pad(brand.rectTransform, 24, 0);

            var header = Box(_canvas, Blue, 0);
            Dock(header, true, 56, 104);
            _title = Label(header, "", 46, Color.white, TextAlignmentOptions.Left, true);
            Pad(_title.rectTransform, 28, 0);

            _body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            _body.SetParent(_canvas, false);
            Fill(_body);
            _body.offsetMax = new Vector2(0, -160);

            _home = MakeScreen("Home");
            _documentScreen = MakeScreen("Document");
            _specimenScreen = MakeScreen("Specimen");
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
            Label(col, Loc.T("pda.scan.headline"), 52, Ink, TextAlignmentOptions.Center, true, 140);
            Label(col, Loc.T("pda.scan.help"), 36, Muted, TextAlignmentOptions.Center, false, 150);
            _homeStatus = Label(col, "", 34, Ink, TextAlignmentOptions.Center, false, 100);
            Spacer(col);
            // Hand-tracking users have no trigger: this button switches the scanner on for a few seconds.
            MakeButton(col, Loc.T("pda.scanButton"), Red, Color.white, 130, () =>
            {
                var scanner = GetComponent<PdaScanner>();
                if (scanner) scanner.ScanBurst();
            }).GetComponentInChildren<TextMeshProUGUI>().fontSize = 48;
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
            foreach (var s in new[] { _home, _patientScreen, _orderScreen, _confirmScreen, _documentScreen, _specimenScreen }) s.gameObject.SetActive(s == screen);
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
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 94;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 8, 8);
            h.spacing = 14;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var box = Box(row, Color.white, 0);
            var boxLe = box.gameObject.AddComponent<LayoutElement>();
            boxLe.preferredWidth = 56; boxLe.preferredHeight = 56; boxLe.flexibleHeight = 0;
            var border = box.gameObject.AddComponent<Outline>();
            border.effectColor = Muted; border.effectDistance = new Vector2(2, -2);
            var tick = Box(box, Blue, 0);
            Fill(tick); tick.offsetMin = new Vector2(9, 9); tick.offsetMax = new Vector2(-9, -9);

            var label = Label(row, t.Name, 34, Ink, TextAlignmentOptions.Left, false);
            label.GetComponent<LayoutElement>().flexibleWidth = 1;
            Dot(row, t.ContainerColor, 34);

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

        void Row(RectTransform parent, string label, string value, float height = 62)
        {
            var r = HRow(parent, height, 12);
            Label(r, label, 30, Muted, TextAlignmentOptions.Left, false).GetComponent<LayoutElement>().preferredWidth = 230;
            Label(r, value, 33, Ink, TextAlignmentOptions.Left, true).GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        static void Tab(RectTransform parent, string label, bool active, UnityAction onClick)
        {
            var b = MakeButton(parent, label, active ? Blue : Line, active ? Color.white : Ink, 92, onClick);
            b.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
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
            Label(rt, label, 38, fg, TextAlignmentOptions.Center, true);
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
