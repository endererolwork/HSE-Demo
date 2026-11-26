using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Clinical.Vitals
{
    /// <summary>
    /// The NIBP START button on the bedside monitor. With the cuff on the patient it runs an inflate/deflate cycle
    /// shown on the monitor, then takes a new blood pressure reading. Without the cuff it reports a mistake.
    /// </summary>
    public class NibpController : MonoBehaviour
    {
        [SerializeField] XRBaseInteractable startButton;
        [SerializeField] VitalsMonitorDisplay display;
        [SerializeField] float inflateSeconds = 2f;
        [SerializeField] float deflateSeconds = 4f;
        [SerializeField] AudioClip pumpSound;

        bool _busy;
        AudioSource _audio;

        public bool IsMeasuring => _busy;

        public void Bind(XRBaseInteractable button, VitalsMonitorDisplay monitor)
        {
            startButton = button;
            display = monitor;
        }

        void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.playOnAwake = false;
            if (!pumpSound) pumpSound = MakePumpHum();
        }

        void OnEnable()
        {
            if (!startButton) return;
            startButton.selectEntered.AddListener(OnPressed);
            startButton.activated.AddListener(OnActivated);
        }

        void OnDisable()
        {
            if (!startButton) return;
            startButton.selectEntered.RemoveListener(OnPressed);
            startButton.activated.RemoveListener(OnActivated);
        }

        void OnPressed(SelectEnterEventArgs _) => StartMeasurement();
        void OnActivated(ActivateEventArgs _) => StartMeasurement();

        public void StartMeasurement()
        {
            if (_busy) return;
            if (!ClinicalEvents.CuffOnPatient)
            {
                ClinicalEvents.RaiseBloodPressureStartedWithoutCuff();
                if (display) StartCoroutine(Flash("NO CUFF"));
                return;
            }
            StartCoroutine(Measure());
        }

        IEnumerator Measure()
        {
            _busy = true;
            _audio.clip = pumpSound;
            _audio.loop = true;
            _audio.Play();
            float target = Mathf.Max(160, VitalsFeed.Current.Systolic + 30);
            for (float t = 0; t < inflateSeconds; t += Time.deltaTime)
            {
                if (!ClinicalEvents.CuffOnPatient) { Abort(); yield break; }
                display?.OverrideNibp($"{Mathf.Lerp(0, target, t / inflateSeconds):0}", "INFLATING");
                yield return null;
            }
            _audio.Stop();
            for (float t = 0; t < deflateSeconds; t += Time.deltaTime)
            {
                if (!ClinicalEvents.CuffOnPatient) { Abort(); yield break; }
                display?.OverrideNibp($"{Mathf.Lerp(target, 40, t / deflateSeconds):0}", "MEASURING");
                yield return null;
            }
            var reading = VitalsFeed.MeasureBloodPressure();
            display?.OverrideNibp(null, null);
            _busy = false;
            ClinicalEvents.RaiseBloodPressureMeasured(reading);
        }

        void Abort()
        {
            _audio.Stop();
            display?.OverrideNibp(null, null);
            _busy = false;
        }

        IEnumerator Flash(string text)
        {
            display.OverrideNibp("---", text);
            yield return new WaitForSeconds(2f);
            if (!_busy) display.OverrideNibp(null, null);
        }

        static AudioClip MakePumpHum()
        {
            const int rate = 22050;
            int n = rate / 2;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                data[i] = (Mathf.Sin(2 * Mathf.PI * 95 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 190 * t) * 0.25f) * 0.25f;
            }
            var clip = AudioClip.Create("NibpPump", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
