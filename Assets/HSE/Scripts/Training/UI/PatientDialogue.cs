using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSE.Training.UI
{
    /// <summary>
    /// Talking to the patient: a floating panel of things the nurse can say (pointed at / poked), and a speech
    /// bubble for the patient's reply above the bed.
    /// </summary>
    public class PatientDialogue : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Vector2 sizeMeters = new Vector2(0.5f, 0.32f);

        const float Width = 1000f;
        RectTransform _options;
        GameObject _optionsRoot;
        TextMeshPro _bubble;
        GameObject _bubbleRoot;

        public void Bind(Transform patientHead) => head = patientHead;

        void Awake()
        {
            BuildOptions();
            BuildBubble();
            HideOptions();
            _bubbleRoot.SetActive(false);
        }

        public void ShowOptions(IList<string> lines, Action<int> chosen)
        {
            for (int i = _options.childCount - 1; i >= 0; i--) Destroy(_options.GetChild(i).gameObject);
            for (int i = 0; i < lines.Count; i++)
            {
                int index = i;
                var b = new GameObject("Option", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                b.transform.SetParent(_options, false);
                b.GetComponent<Image>().color = new Color(0.95f, 0.97f, 1f);
                b.GetComponent<LayoutElement>().preferredHeight = 110;
                b.GetComponent<Button>().onClick.AddListener(() => chosen(index));
                var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                t.transform.SetParent(b.transform, false);
                var rt = t.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(20, 6); rt.offsetMax = new Vector2(-20, -6);
                t.text = $"\u201C{lines[i]}\u201D";
                t.fontSize = 34;
                t.color = new Color(0.1f, 0.15f, 0.25f);
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.raycastTarget = false;
            }
            PlaceOptions();
            _optionsRoot.SetActive(true);
        }

        public void HideOptions() => _optionsRoot.SetActive(false);

        public void Say(string line, float seconds = 4f)
        {
            StopAllCoroutines();
            StartCoroutine(SayRoutine(line, seconds));
        }

        IEnumerator SayRoutine(string line, float seconds)
        {
            _bubble.text = line;
            _bubbleRoot.SetActive(true);
            yield return new WaitForSeconds(seconds);
            _bubbleRoot.SetActive(false);
        }

        void LateUpdate()
        {
            var cam = Camera.main ? Camera.main.transform : null;
            if (!cam) return;
            if (_bubbleRoot.activeSelf && head)
            {
                _bubbleRoot.transform.position = head.position + Vector3.up * 0.32f;
                _bubbleRoot.transform.rotation = Quaternion.LookRotation(_bubbleRoot.transform.position - cam.position);
            }
        }

        void PlaceOptions()
        {
            var cam = Camera.main ? Camera.main.transform : null;
            if (!cam) return;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            _optionsRoot.transform.position = cam.position + fwd * 0.75f - right * 0.25f + Vector3.down * 0.2f;
            _optionsRoot.transform.rotation = Quaternion.LookRotation(_optionsRoot.transform.position - cam.position);
        }

        void BuildOptions()
        {
            float height = Width * sizeMeters.y / sizeMeters.x;
            _optionsRoot = new GameObject("SayOptions", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _optionsRoot.transform.SetParent(transform, false);
            _optionsRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _optionsRoot.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            var raycaster = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycaster != null) _optionsRoot.AddComponent(raycaster);
            _optionsRoot.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)_optionsRoot.transform;
            rt.sizeDelta = new Vector2(Width, height);
            rt.localScale = Vector3.one * (sizeMeters.x / Width);

            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bg.SetParent(rt, false);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.15f, 0.9f);
            bg.GetComponent<Image>().raycastTarget = false;

            var title = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            title.transform.SetParent(rt, false);
            title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.sizeDelta = new Vector2(-40, 70);
            title.rectTransform.anchoredPosition = new Vector2(0, -10);
            title.text = "<b>Say to the patient</b>";
            title.fontSize = 34;
            title.color = Color.white;
            title.raycastTarget = false;

            _options = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            _options.SetParent(rt, false);
            _options.anchorMin = Vector2.zero; _options.anchorMax = Vector2.one;
            _options.offsetMin = new Vector2(20, 20); _options.offsetMax = new Vector2(-20, -90);
            var v = _options.GetComponent<VerticalLayoutGroup>();
            v.spacing = 12;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
        }

        void BuildBubble()
        {
            _bubbleRoot = new GameObject("SpeechBubble");
            _bubbleRoot.transform.SetParent(transform, false);
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(_bubbleRoot.transform, false);
            bg.transform.localPosition = new Vector3(0, 0, 0.002f);
            bg.transform.localScale = new Vector3(0.5f, 0.12f, 1);
            var mr = bg.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mr.sharedMaterial.SetColor("_BaseColor", Color.white);
            _bubble = new GameObject("Text").AddComponent<TextMeshPro>();
            _bubble.transform.SetParent(_bubbleRoot.transform, false);
            _bubble.rectTransform.sizeDelta = new Vector2(0.46f, 0.1f);
            _bubble.enableAutoSizing = true;
            _bubble.fontSizeMin = 0.05f;
            _bubble.fontSizeMax = 0.5f;
            _bubble.alignment = TextAlignmentOptions.Center;
            _bubble.color = new Color(0.1f, 0.12f, 0.2f);
        }
    }
}