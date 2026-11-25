using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSE.Clinical.Vitals
{
    public struct VitalSample
    {
        public DateTime Time;
        public float HeartRate, SpO2, RespRate, Systolic, Diastolic, Temperature;
    }

    /// <summary>
    /// Current vital signs of the patient in the bed plus a 12-hour chart history, generated from the patient's
    /// baseline. The bedside monitor and the PDA both read from here.
    /// </summary>
    public static class VitalsFeed
    {
        public static VitalSample Current { get; private set; }
        public static IReadOnlyList<VitalSample> History => _history;
        public static event Action Updated;

        static readonly List<VitalSample> _history = new List<VitalSample>();
        static PatientRecord _patient;
        static float _hrNoise, _spo2Noise, _rrNoise, _tempNoise;
        static System.Random _rng = new System.Random();

        /// <summary>Starts a new patient: fills 12 hours of chart readings (every 30 min) that trend towards today's values.</summary>
        public static void Reset(PatientRecord p)
        {
            _patient = p;
            _history.Clear();
            if (p == null) return;
            _rng = new System.Random(p.medicalRecordNumber.GetHashCode());
            var now = DateTime.Now;
            for (int i = 24; i >= 1; i--)
            {
                float hoursAgo = i * 0.5f;
                float trend = Mathf.Clamp01(hoursAgo / 12f); // earlier readings drift a little further from today's
                _history.Add(new VitalSample
                {
                    Time = now.AddHours(-hoursAgo),
                    HeartRate = p.heartRate + (p.heartRate > 90 ? 6 : 2) * trend + Jitter(4),
                    SpO2 = Mathf.Min(100, p.spo2 - (p.spo2 < 96 ? 1.5f : 0.3f) * trend + Jitter(1)),
                    RespRate = p.respRate + (p.respRate > 19 ? 2 : 0) * trend + Jitter(1.5f),
                    Systolic = p.systolic + Jitter(6),
                    Diastolic = p.diastolic + Jitter(4),
                    Temperature = p.temperature + (p.temperature > 37.5f ? 0.5f : 0.1f) * trend + Jitter(0.2f),
                });
            }
            Current = _history[_history.Count - 1];
            Updated?.Invoke();
        }

        /// <summary>Moves the live values a small random step, staying near the baseline.</summary>
        public static void Step()
        {
            if (_patient == null) return;
            var p = _patient;
            _hrNoise = Mathf.Lerp(_hrNoise, Jitter(p.irregularRhythm ? 12 : 4), 0.35f);
            _spo2Noise = Mathf.Lerp(_spo2Noise, Jitter(1.2f), 0.25f);
            _rrNoise = Mathf.Lerp(_rrNoise, Jitter(1.5f), 0.25f);
            _tempNoise = Mathf.Lerp(_tempNoise, Jitter(0.1f), 0.1f);
            Current = new VitalSample
            {
                Time = DateTime.Now,
                HeartRate = Mathf.Round(p.heartRate + _hrNoise),
                SpO2 = Mathf.Clamp(Mathf.Round(p.spo2 + _spo2Noise), 80, 100),
                RespRate = Mathf.Round(p.respRate + _rrNoise),
                Systolic = Current.Systolic == 0 ? p.systolic : Current.Systolic,
                Diastolic = Current.Diastolic == 0 ? p.diastolic : Current.Diastolic,
                Temperature = Mathf.Round((p.temperature + _tempNoise) * 10f) / 10f,
            };
            Updated?.Invoke();
        }

        /// <summary>A new non-invasive blood pressure reading.</summary>
        public static VitalSample MeasureBloodPressure()
        {
            if (_patient == null) return Current;
            var c = Current;
            c.Systolic = Mathf.Round(_patient.systolic + Jitter(6));
            c.Diastolic = Mathf.Round(_patient.diastolic + Jitter(4));
            c.Time = DateTime.Now;
            Current = c;
            Updated?.Invoke();
            return c;
        }

        static float Jitter(float amount) => (float)(_rng.NextDouble() * 2 - 1) * amount;
    }
}
