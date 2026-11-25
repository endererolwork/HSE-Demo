using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HSE.Clinical;
using HSE.Devices;
using HSE.Localization;
using HSE.Scenario;
using HSE.Training.UI;
using PrismFX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Throw-away play-mode driver used only in the verification copy of the project.
public class VerifyDriver : MonoBehaviour
{
    public static bool Done;
    public static int Failures;
    public static string OutDir;
    static readonly StringBuilder Log = new StringBuilder();

    ScenarioRunner _runner;
    PdaApp _pda;

    void Check(bool ok, string what)
    {
        Log.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        if (!ok) Failures++;
    }

    void Note(string s) => Log.AppendLine("      " + s);

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1.0f);
        FindFirstObjectByType<HSE.Training.UI.LanguageMenu>()?.Choose(Language.English);
        yield return null;
        try { Setup(); } catch (System.Exception e) { Check(false, "setup threw " + e); Finish(); yield break; }

        // ---------- initial state
        Check(_runner && _runner.IsRunning, "scenario is running");
        Check(_runner.CurrentStep && _runner.CurrentStep.Id == "take_pda", "first step is take_pda (got " + _runner.CurrentStep?.Id + ")");
        Check(Lit(_pda.gameObject), "PDA highlighted for take_pda");
        Check(Camera.main != null, "main camera present");
        Check(FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()?.GetComponent("XRUIInputModule") != null, "event system uses XRUIInputModule");
        Shot("01_room_overview", new Vector3(-2.3f, 1.65f, -2.1f), new Vector3(0.6f, 0.8f, 1.3f), 70);
        Shot("02_room_from_window", new Vector3(2.6f, 1.7f, -2.1f), new Vector3(-0.5f, 0.7f, 1.2f), 70);
        Shot("03_bed_closeup", new Vector3(-1.2f, 1.5f, 0.2f), new Vector3(0.3f, 0.7f, 1.4f), 55);
        Shot("04_trolley_pda_highlight", _pda.transform.position + new Vector3(-0.35f, 0.35f, -0.35f), _pda.transform.position, 50);

        // ---------- 1 grab
        var grab = _pda.GetComponent<XRGrabInteractable>();
        grab.selectEntered.Invoke(new SelectEnterEventArgs());
        yield return null;
        Check(_runner.CurrentStep?.Id == "approach", "after grab -> approach");
        Check(!Lit(_pda.gameObject), "PDA highlight off after grab");

        // ---------- 2 approach (move the head to the bedside)
        var bedside = GameObject.Find("BedsidePoint").transform;
        var cam = Camera.main.transform;
        var origin = cam.root;
        origin.position += new Vector3(bedside.position.x - cam.position.x, 0, bedside.position.z - cam.position.z);
        yield return new WaitForSeconds(0.2f);
        Check(_runner.CurrentStep?.Id == "scan_wristband", "after reaching bedside -> scan_wristband (got " + _runner.CurrentStep?.Id + ")");
        var qr = FindObjectsByType<ScannableCode>(FindObjectsSortMode.None).FirstOrDefault(c => c.kind == CodeKind.PatientWristband);
        Check(qr && qr.patient, "wristband QR has a patient record");
        Check(Lit(GameObject.Find("Wristband")) || Lit(qr.gameObject), "wristband highlighted");
        Shot("05_wristband_highlight", qr.transform.position + new Vector3(-0.35f, 0.3f, -0.2f), qr.transform.position, 45);
        ShotPanel("06_checklist_scan_step");

        // ---------- mistake: scan something that is not a wristband
        int before = _runner.Report.MistakeCount;
        var fake = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var fakeCode = fake.AddComponent<ScannableCode>();
        fakeCode.kind = CodeKind.SpecimenBag;
        ClinicalEvents.RaiseCodeScanned(fakeCode);
        Check(_runner.Report.MistakeCount == before + 1, "scanning a non-wristband code counts as a mistake");
        Destroy(fake);

        // ---------- 3 real scan through the laser: hold the PDA 20 cm above the QR, tip pointing down
        var rb = _pda.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        _pda.transform.SetPositionAndRotation(qr.transform.position + Vector3.up * 0.2f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        Physics.SyncTransforms();
        var scanner = _pda.GetComponent<PdaScanner>();
        scanner.SetScanning(true);
        yield return new WaitForSeconds(0.3f);
        Shot("07_scan_beam", qr.transform.position + new Vector3(-0.3f, 0.25f, -0.25f), qr.transform.position + Vector3.up * 0.1f, 45);
        scanner.SetScanning(false);
        Check(_runner.CurrentStep?.Id == "confirm_identity", "laser scan of wristband -> confirm_identity (got " + _runner.CurrentStep?.Id + ")");
        Check(_pda.CurrentPatient == qr.patient, "PDA shows the scanned patient");

        // put the PDA in front of a capture camera for screen shots
        _pda.transform.SetPositionAndRotation(new Vector3(0, 1.3f, -1.0f), Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Euler(-90, 0, 0));
        yield return null;
        ShotPda("08_pda_patient");
        Check(Lit(_pda.gameObject), "PDA highlighted for confirm_identity");

        // ---------- 4 confirm identity
        Check(Press(Loc.T("pda.confirmIdentity")), "found confirm identity button");
        yield return null;
        Check(_runner.CurrentStep?.Id == "hand_hygiene", "after confirm -> hand_hygiene");
        ShotPda("09_pda_identity_confirmed");

        // ---------- 5 hand hygiene composite
        var rub = GameObject.Find("HandRubDispenser").GetComponent<XRBaseInteractable>();
        var gloves = GameObject.Find("GloveBox_M").GetComponent<XRBaseInteractable>();
        Check(rub && gloves, "dispenser and glove box are interactable");
        Check(Lit(rub.gameObject) && !Lit(gloves.gameObject), "dispenser lit first, gloves not yet");
        Shot("10_hygiene_highlight", rub.transform.position + new Vector3(1.0f, 0.2f, 0.4f), rub.transform.position + new Vector3(0, -0.05f, 0.2f), 50);
        ShotPanel("11_checklist_hygiene");
        rub.selectEntered.Invoke(new SelectEnterEventArgs());
        yield return null;
        Check(Lit(gloves.gameObject), "gloves lit after hand rub");
        gloves.selectEntered.Invoke(new SelectEnterEventArgs());
        yield return null;
        Check(_runner.CurrentStep?.Id == "lab_order", "after gloves -> lab_order");

        // ---------- optional: look at care board
        var board = GameObject.Find("CareBoard").GetComponent<XRBaseInteractable>();
        board.hoverEntered.Invoke(new HoverEnterEventArgs());
        yield return null;
        Check(_runner.Steps.First(s => s.Id == "read_care_board").State == StepState.Completed, "optional care board step completes on hover");

        // ---------- 6 lab order
        Check(Press(Loc.T("pda.labRequest")), "found lab request button");
        yield return null;
        ShotPda("12_pda_order_empty");
        int m0 = _runner.Report.MistakeCount;
        Press(Loc.T("pda.send"));
        yield return null;
        Check(_runner.Report.MistakeCount == m0 + 1, "sending an empty order is a mistake");
        foreach (var id in qr.patient.physicianOrderedTestIds) Check(PressRow(LabTestCatalog.Get(id).Name), "select test " + id);
        PressRow(LabTestCatalog.Get("COAG").Name);
        yield return null;
        Press(Loc.T("pda.send"));
        yield return null;
        Check(_runner.CurrentStep?.Id == "lab_order", "order with an extra test is rejected");
        ShotPda("13_pda_order_mismatch");
        PressRow(LabTestCatalog.Get("COAG").Name);
        yield return null;
        ShotPda("14_pda_order_selected");
        Press(Loc.T("pda.send"));
        yield return null;
        ShotPda("15_pda_confirmation");
        Check(!_runner.IsRunning && _runner.Report.IsFinished, "scenario finished after lab order");
        var r = _runner.Report;
        Note($"score {r.Score}/{r.MaxScore}, mistakes {r.MistakeCount}, optional {r.CompletedCount(StepRequirement.Optional)}/{r.Count(StepRequirement.Optional)}");
        Check(PrismHighlight.ActiveHighlights.Count(h => h && h.highlighted) == 0, "no highlights left on");
        ShotPanel("16_checklist_summary");

        Finish();
    }

    void Setup()
    {
        _runner = FindFirstObjectByType<ScenarioRunner>();
        _pda = FindFirstObjectByType<PdaApp>();
        Check(_runner, "ScenarioRunner in scene");
        Check(_pda, "PDA in scene");
        var panel = FindObjectsByType<ScenarioChecklistPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        Check(panel, "checklist panel in scene");
        Check(panel && panel.transform.parent && panel.transform.parent.name == "Left Controller", "checklist panel attached to Left Controller (parent " + (panel && panel.transform.parent ? panel.transform.parent.name : "none") + ")");
        if (panel && !panel.gameObject.activeInHierarchy)
        {
            // No headset in batch mode, so controllers are off; park the panel in the room for screenshots.
            panel.transform.SetParent(null);
            panel.transform.SetPositionAndRotation(new Vector3(-1.6f, 1.45f, -1.2f), Quaternion.Euler(0, 35, 0));
        }
        foreach (var s in _runner.Steps)
        {
            Check(s != null, "step reference not null");
            if (s && s.Requirement == StepRequirement.Mandatory && !(s is CompositeStep))
                Check(s.HighlightTargets.All(t => t), $"step {s.Id} highlight targets resolved ({s.HighlightTargets.Count})");
        }
    }

    static bool Lit(GameObject go)
    {
        if (!go) return false;
        var h = go.GetComponent<PrismHighlight>();
        return h && h.highlighted;
    }

    bool Press(string label)
    {
        var t = _pda.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.text == label);
        var b = t ? t.GetComponentInParent<Button>() : null;
        if (!b || !b.interactable) { Note("button not pressable: " + label); return false; }
        b.onClick.Invoke();
        return true;
    }

    bool PressRow(string startsWith)
    {
        var t = _pda.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.text.StartsWith(startsWith));
        var b = t ? t.GetComponentInParent<Button>() : null;
        if (!b) { Note("row not found: " + startsWith); return false; }
        b.onClick.Invoke();
        return true;
    }

    void ShotPda(string name)
    {
        var anchor = _pda.screenAnchor;
        Shot(name, anchor.position - anchor.forward * 0.17f, anchor.position, 40, 900, 1400, anchor.up);
    }

    void ShotPanel(string name)
    {
        var p = FindFirstObjectByType<ScenarioChecklistPanel>().transform;
        Shot(name, p.position - p.forward * 0.3f, p.position, 40, 1000, 1200, p.up);
    }

    void Shot(string name, Vector3 from, Vector3 at, float fov, int w = 1600, int h = 900, Vector3? up = null)
    {
        var go = new GameObject("VerifyCam");
        var c = go.AddComponent<Camera>();
        c.stereoTargetEye = StereoTargetEyeMask.None;
        c.fieldOfView = fov;
        c.nearClipPlane = 0.01f;
        go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, up ?? Vector3.up));
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        c.targetTexture = rt;
        c.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
        c.targetTexture = null;
        Destroy(rt); Destroy(tex); Destroy(go);
    }

    void Finish()
    {
        Log.AppendLine($"\n{Failures} failure(s)");
        File.WriteAllText(Path.Combine(OutDir, "report.txt"), Log.ToString());
        Done = true;
    }
}
