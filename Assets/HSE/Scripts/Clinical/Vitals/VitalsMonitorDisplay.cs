using TMPro;
using UnityEngine;

namespace HSE.Clinical.Vitals
{
    /// <summary>
    /// Drives the bedside monitor: numbers wander around the patient's baseline, the ECG and pleth traces scroll,
    /// and the readings follow whichever patient is in the bed.
    /// </summary>
    public class VitalsMonitorDisplay : MonoBehaviour
    {
        [SerializeField] TextMeshPro heartRate, spo2, respRate, bloodPressure, bloodPressureTime;
        [SerializeField] Transform ecgParent;
        [SerializeField] Material traceMaterial;
        [SerializeField] float updateInterval = 1.5f;

        [Header("Trace area (monitor local space)")]
        [SerializeField] float traceLeft = -0.16f, traceRight = 0.04f;
        [SerializeField] float ecgY = 0.085f, plethY = 0.04f, traceZ = -0.039f;

        TextMeshPro _temp;
        LineRenderer _ecg, _pleth;
        float _next, _phase, _plethPhase;
        const int Points = 90;

        public void Bind(TextMeshPro hr, TextMeshPro sat, TextMeshPro rr, TextMeshPro bp, TextMeshPro bpTime, Transform ecg, Material trace)
        {
            heartRate = hr; spo2 = sat; respRate = rr; bloodPressure = bp; bloodPressureTime = bpTime; ecgParent = ecg; traceMaterial = trace;
        }

        void Awake()
        {
            if (ecgParent)
            {
                // The static trace from the scene builder is replaced by a live one.
                foreach (Transform child in ecgParent) child.gameObject.SetActive(false);
                _ecg = MakeTrace("ECG", new Color(0.35f, 1f, 0.45f));
                _pleth = MakeTrace("Pleth", new Color(0.4f, 0.9f, 1f));
            }
            if (bloodPressureTime)
            {
                _temp = Instantiate(bloodPressureTime, bloodPressureTime.transform.parent);
                _temp.name = "Monitor_Temp";
                _temp.transform.localPosition += new Vector3(0, -0.03f, 0);
                _temp.color = new Color(1f, 0.75f, 0.4f);
            }
        }

        void OnEnable()
        {
            ActivePatient.Changed += OnPatient;
            VitalsFeed.Updated += Refresh;
            if (ActivePatient.Current) OnPatient(ActivePatient.Current);
        }

        void OnDisable()
        {
            ActivePatient.Changed -= OnPatient;
            VitalsFeed.Updated -= Refresh;
        }

        void OnPatient(PatientRecord p)
        {
            VitalsFeed.Reset(p);
            VitalsFeed.MeasureBloodPressure();
        }

        void Update()
        {
            if (Time.time >= _next)
            {
                _next = Time.time + updateInterval;
                VitalsFeed.Step();
            }
            DrawTraces();
        }

        void Refresh()
        {
            var v = VitalsFeed.Current;
            if (heartRate) heartRate.text = v.HeartRate.ToString("0");
            if (spo2) spo2.text = v.SpO2.ToString("0");
            if (respRate) respRate.text = v.RespRate.ToString("0");
            if (bloodPressure) bloodPressure.text = $"{v.Systolic:0}/{v.Diastolic:0}";
            if (bloodPressureTime) bloodPressureTime.text = "NIBP " + v.Time.ToString("HH:mm");
            if (_temp) _temp.text = $"TEMP {v.Temperature:0.0} °C";
        }

        // ------------------------------------------------------------ traces

        void DrawTraces()
        {
            if (!_ecg) return;
            float hr = Mathf.Max(30, VitalsFeed.Current.HeartRate);
            float beatsPerSecond = hr / 60f;
            _phase += Time.deltaTime * beatsPerSecond;
            _plethPhase += Time.deltaTime * beatsPerSecond;
            const float window = 2.5f; // beats shown across the trace
            for (int i = 0; i < Points; i++)
            {
                float t = i / (float)(Points - 1);
                float x = Mathf.Lerp(traceLeft, traceRight, t);
                float beat = _phase - (1 - t) * window;
                _ecg.SetPosition(i, new Vector3(x, ecgY + Ecg(beat) * 0.035f, traceZ));
                _pleth.SetPosition(i, new Vector3(x, plethY + Pleth(_plethPhase - (1 - t) * window) * 0.02f, traceZ));
            }
        }

        static float Ecg(float beat)
        {
            float p = beat - Mathf.Floor(beat); // 0..1 within one beat
            if (p < 0.10f) return 0.15f * Mathf.Sin(p / 0.10f * Mathf.PI);              // P wave
            if (p < 0.16f) return 0;
            if (p < 0.18f) return -0.2f * ((p - 0.16f) / 0.02f);                        // Q
            if (p < 0.21f) return Mathf.Lerp(-0.2f, 1f, (p - 0.18f) / 0.03f);           // R up
            if (p < 0.24f) return Mathf.Lerp(1f, -0.35f, (p - 0.21f) / 0.03f);          // R down / S
            if (p < 0.27f) return Mathf.Lerp(-0.35f, 0, (p - 0.24f) / 0.03f);
            if (p < 0.38f) return 0;
            if (p < 0.55f) return 0.3f * Mathf.Sin((p - 0.38f) / 0.17f * Mathf.PI);      // T wave
            return 0;
        }

        static float Pleth(float beat)
        {
            float p = beat - Mathf.Floor(beat);
            return p < 0.3f ? Mathf.Sin(p / 0.3f * Mathf.PI * 0.5f) : Mathf.Exp(-(p - 0.3f) * 4f) + 0.15f * Mathf.Sin((p - 0.3f) * 18f) * Mathf.Exp(-(p - 0.3f) * 6f);
        }

        LineRenderer MakeTrace(string n, Color c)
        {
            var go = new GameObject(n);
            go.transform.SetParent(ecgParent.parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = Points;
            lr.startWidth = lr.endWidth = 0.0022f;
            lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = traceMaterial;
            return lr;
        }
    }
}
