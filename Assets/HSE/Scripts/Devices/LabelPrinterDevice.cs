using System;
using System.Collections.Generic;
using System.Linq;
using HSE.Clinical;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// The label printer on the trolley. Prints one specimen label per filled tube (patient name, date of birth,
    /// MRN, collection time, nurse ID, tube/test, barcode) and the A6 laboratory request slip for the specimen bag.
    /// Printed items are grabbable and are cleared when the next patient starts.
    /// </summary>
    public class LabelPrinterDevice : MonoBehaviour
    {
        [SerializeField] Material paperMaterial;
        [SerializeField] Material inkMaterial;
        [SerializeField] Transform outlet;
        [SerializeField] string nurseId = "VR1";

        static readonly Vector2 LabelSize = new Vector2(0.05f, 0.02f);
        static readonly Vector2 SlipSize = new Vector2(0.1f, 0.13f);

        readonly List<GameObject> _printed = new List<GameObject>();

        public string NurseId => nurseId;

        public void Configure(Material paper, Material ink, Transform paperOutlet)
        {
            paperMaterial = paper;
            inkMaterial = ink;
            outlet = paperOutlet;
        }

        public void ClearAll()
        {
            foreach (var go in _printed) if (go) Destroy(go);
            _printed.Clear();
        }

        /// <summary>One label per tube. Each label only sticks to the tube it was printed for.</summary>
        public void PrintLabels(PatientRecord p, IEnumerable<Transform> tubes, DateTime collected)
        {
            int i = 0;
            foreach (var tube in tubes)
            {
                string tests = string.Join(", ", TestsForTube(p, tube.name));
                string body = $"DOB {p.dateOfBirth}   MRN {p.medicalRecordNumber}\n{collected:dd.MM.yy HH:mm}   RN {nurseId}\n<b>{tests}</b>";
                var label = Paper("Label_" + tube.name, LabelSize, p.fullName.ToUpperInvariant(), body, p.medicalRecordNumber + tube.name,
                    Spawn(i++ * 0.06f, 0), headerShare: 0.3f, barcodeShare: 0.18f);
                label.AddComponent<LabelSticker>().Configure(tube, p);
            }
        }

        /// <summary>The laboratory request slip that goes in the outer pocket of the specimen bag.</summary>
        public GameObject PrintRequestSlip(LabOrder order)
        {
            var p = order.Patient;
            string tests = string.Join("\n", order.Tests.Select(t => $"  • {t.Name}"));
            string body =
                $"<b>Patient</b>  {p.fullName}\n" +
                $"<b>DOB</b>  {p.dateOfBirth}    <b>MRN</b>  {p.medicalRecordNumber}\n" +
                $"<b>Ward</b>  {p.Ward}, room {p.room} / bed {p.bed}\n" +
                $"<b>Requested by</b>  {p.attendingPhysician}\n" +
                $"<b>Collected by</b>  RN {nurseId}   {order.Time:dd.MM.yyyy HH:mm}\n" +
                $"<b>Priority</b>  {(order.Urgent ? "<color=#C62828><b>URGENT</b></color>" : "Routine")}\n" +
                $"<b>Tests</b>\n{tests}";
            var slip = Paper("RequestSlip", SlipSize, $"LAB REQUEST\n<size=70%>{order.Reference}</size>", body, order.Reference,
                Spawn(0, -0.08f), headerShare: 0.16f, barcodeShare: 0.1f);
            slip.AddComponent<RequestSlipMarker>().Order = order;
            return slip;
        }

        static IEnumerable<string> TestsForTube(PatientRecord p, string tubeName)
        {
            foreach (var id in p.physicianOrderedTestIds)
                if (BloodDrawSession.TubeForTest.TryGetValue(id, out var t) && t == tubeName)
                    yield return LabTestCatalog.Get(id)?.Name ?? id;
        }

        Pose Spawn(float right, float forward)
        {
            var anchor = outlet ? outlet : transform;
            var rot = Quaternion.Euler(0, anchor.eulerAngles.y, 0);
            return new Pose(anchor.position + rot * new Vector3(right, 0.01f, forward), rot);
        }

        /// <summary>
        /// A flat sheet (X = width, Z = height when read from above) with a bold header, a body and a barcode strip.
        /// Text sizes are fixed per sheet size so the print stays crisp and readable.
        /// </summary>
        GameObject Paper(string name, Vector2 size, string header, string body, string code, Pose pose, float headerShare, float barcodeShare)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(size.x, 0.006f, size.y);

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(sheet.GetComponent<Collider>());
            sheet.name = "Sheet";
            sheet.transform.SetParent(go.transform, false);
            sheet.transform.localScale = new Vector3(size.x, 0.0008f, size.y);
            if (paperMaterial) sheet.GetComponent<MeshRenderer>().sharedMaterial = paperMaterial;

            float margin = size.y * 0.06f;
            float inner = size.y - 2 * margin;
            float headerH = inner * headerShare, barcodeH = inner * barcodeShare, bodyH = inner - headerH - barcodeH;
            float top = size.y * 0.5f - margin;

            Text(go.transform, "Header", header, size.x - 2 * margin, headerH, top - headerH * 0.5f, true);
            // Thin rule under the header.
            Bar(go.transform, 0, top - headerH - margin * 0.25f, size.x - 2 * margin, Mathf.Max(0.0004f, size.y * 0.006f));
            Text(go.transform, "Body", body, size.x - 2 * margin, bodyH - margin * 0.5f, top - headerH - margin * 0.5f - (bodyH - margin * 0.5f) * 0.5f, false);
            Barcode(go.transform, code, size.x * 0.7f, barcodeH, -size.y * 0.5f + margin + barcodeH * 0.5f);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            _printed.Add(go);
            return go;
        }

        static void Text(Transform parent, string name, string text, float width, float height, float z, bool header)
        {
            var t = new GameObject(name).AddComponent<TextMeshPro>();
            t.transform.SetParent(parent, false);
            t.transform.localPosition = new Vector3(0, 0.0006f, z);
            t.transform.localRotation = Quaternion.Euler(90, 0, 0);
            t.rectTransform.sizeDelta = new Vector2(width, height);
            t.margin = Vector4.zero;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.004f;
            t.fontSizeMax = header ? 0.09f : 0.05f;
            t.lineSpacing = -10f;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.color = new Color(0.08f, 0.08f, 0.1f);
            t.fontStyle = header ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = header ? TextAlignmentOptions.Left : TextAlignmentOptions.TopLeft;
            t.text = text;
        }

        void Bar(Transform parent, float x, float z, float width, float depth)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(b.GetComponent<Collider>());
            b.name = "Ink";
            b.transform.SetParent(parent, false);
            b.transform.localPosition = new Vector3(x, 0.00045f, z);
            b.transform.localScale = new Vector3(width, 0.0001f, depth);
            var r = b.GetComponent<MeshRenderer>();
            if (inkMaterial) r.sharedMaterial = inkMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A Code-128-looking strip of black bars derived from the code text (decorative, deterministic).</summary>
        void Barcode(Transform parent, string code, float width, float height, float z)
        {
            var widths = new List<int> { 2, 1, 1, 2 };
            uint h = 2166136261;
            foreach (char c in code ?? "") { h = (h ^ c) * 16777619; widths.Add(1 + (int)(h % 3)); widths.Add(1 + (int)((h >> 8) % 3)); }
            widths.AddRange(new[] { 2, 1, 1, 2 });
            float unit = width / widths.Sum();
            float x = -width * 0.5f;
            for (int i = 0; i < widths.Count; i++)
            {
                float w = widths[i] * unit;
                if (i % 2 == 0) Bar(parent, x + w * 0.5f, z, w, height);
                x += w;
            }
        }
    }
}
