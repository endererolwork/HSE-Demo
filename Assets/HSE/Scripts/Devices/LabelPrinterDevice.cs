using System;
using System.Collections.Generic;
using HSE.Clinical;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// The label printer on the trolley. Prints one specimen label per filled tube (patient name, date of birth,
    /// MRN, collection time, nurse ID, tube/test) and the paper request slip for the specimen bag.
    /// Printed items are grabbable and are cleared when the next patient starts.
    /// </summary>
    public class LabelPrinterDevice : MonoBehaviour
    {
        [SerializeField] Material paperMaterial;
        [SerializeField] Transform outlet;
        [SerializeField] string nurseId = "VR1";

        readonly List<GameObject> _printed = new List<GameObject>();

        public string NurseId => nurseId;

        public void Configure(Material paper, Transform paperOutlet)
        {
            paperMaterial = paper;
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
                string text =
                    $"<b>{p.fullName}</b>\nDOB {p.dateOfBirth}  MRN {p.medicalRecordNumber}\n" +
                    $"{collected:dd.MM.yyyy HH:mm}  RN {nurseId}\n<b>{tests}</b>";
                var label = Paper("Label_" + tube.name, new Vector3(0.05f, 0.0015f, 0.03f), text, i++);
                label.AddComponent<LabelSticker>().Configure(tube, p);
            }
        }

        /// <summary>The laboratory request slip that goes in the outer pocket of the specimen bag.</summary>
        public GameObject PrintRequestSlip(LabOrder order)
        {
            string text = $"<b>LAB REQUEST {order.Reference}</b>\n{order.Patient.fullName}  MRN {order.Patient.medicalRecordNumber}\n" +
                          $"{string.Join(", ", order.Tests.ConvertAll(t => t.Id))}{(order.Urgent ? "  <color=#C62828>URGENT</color>" : "")}";
            var slip = Paper("RequestSlip", new Vector3(0.07f, 0.0015f, 0.05f), text, 0);
            slip.AddComponent<RequestSlipMarker>().Order = order;
            return slip;
        }

        static IEnumerable<string> TestsForTube(PatientRecord p, string tubeName)
        {
            foreach (var id in p.physicianOrderedTestIds)
                if (BloodDrawSession.TubeForTest.TryGetValue(id, out var t) && t == tubeName)
                    yield return LabTestCatalog.Get(id)?.Name ?? id;
        }

        GameObject Paper(string name, Vector3 size, string text, int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = Vector3.one;
            var anchor = outlet ? outlet : transform;
            go.transform.position = anchor.position + anchor.right * (index * 0.055f) + Vector3.up * 0.01f;
            go.transform.rotation = Quaternion.Euler(0, anchor.eulerAngles.y, 0);
            Destroy(go.GetComponent<MeshFilter>());
            Destroy(go.GetComponent<MeshRenderer>());
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(size.x, 0.006f, size.z);

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(sheet.GetComponent<Collider>());
            sheet.name = "Sheet";
            sheet.transform.SetParent(go.transform, false);
            sheet.transform.localScale = size;
            if (paperMaterial) sheet.GetComponent<MeshRenderer>().sharedMaterial = paperMaterial;

            var t = new GameObject("Text").AddComponent<TextMeshPro>();
            t.transform.SetParent(go.transform, false);
            t.transform.localPosition = new Vector3(0, size.y * 0.5f + 0.0004f, 0);
            t.transform.localRotation = Quaternion.Euler(90, 0, 0);
            t.rectTransform.sizeDelta = new Vector2(size.x * 0.92f, size.z * 0.9f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.01f;
            t.fontSizeMax = 0.2f;
            t.color = Color.black;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.text = text;

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
    }
}
