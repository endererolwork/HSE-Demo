using HSE.Clinical;
using HSE.Localization;
using HSE.Training.UI;
using UnityEngine;

namespace HSE.Training
{
    /// <summary>
    /// Makes the patient feel alive: slow breathing, blinking, turning the head towards the nurse when close,
    /// and short spoken reactions to what is being done (cuff tightening, tourniquet, needle).
    /// </summary>
    public class PatientBehaviour : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Transform torso;
        [SerializeField] Transform[] eyes = new Transform[0];
        [SerializeField] PatientDialogue dialogue;
        [SerializeField] float lookDistance = 1.8f;
        [SerializeField] float maxHeadYaw = 35f;

        Quaternion _headRest;
        Vector3 _torsoScale;
        Vector3[] _eyeScale;
        float _nextBlink, _blinkUntil, _wince;
        Transform _cam;

        public void Bind(Transform patientHead, Transform patientTorso, Transform[] patientEyes, PatientDialogue talk)
        {
            head = patientHead; torso = patientTorso; eyes = patientEyes; dialogue = talk;
        }

        void Awake()
        {
            if (head) _headRest = head.localRotation;
            if (torso) _torsoScale = torso.localScale;
            _eyeScale = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) if (eyes[i]) _eyeScale[i] = eyes[i].localScale;
            _nextBlink = Time.time + Random.Range(2f, 5f);
        }

        void OnEnable() => ClinicalEvents.ProcedureEvent += OnProcedure;
        void OnDisable() => ClinicalEvents.ProcedureEvent -= OnProcedure;

        void OnProcedure(string id)
        {
            switch (id)
            {
                case "tourniquet_on": Say("react.tourniquet"); break;
                case "site_cleaned": Say("react.cold"); break;
                case "needle_in": _wince = 1f; Say("react.needle"); break;
                case "site_covered": Say("react.thanks"); break;
            }
        }

        void Say(string key)
        {
            if (dialogue) dialogue.Say(Loc.T(key), 3f);
        }

        void Update()
        {
            if (!_cam && Camera.main) _cam = Camera.main.transform;

            // Breathing: about 15 breaths a minute.
            if (torso) torso.localScale = _torsoScale + new Vector3(0.006f, 0f, 0.012f) * (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / 4f));

            // Blinking.
            if (Time.time >= _nextBlink)
            {
                _blinkUntil = Time.time + 0.12f;
                _nextBlink = Time.time + Random.Range(2.5f, 6f);
            }
            bool closed = Time.time < _blinkUntil || _wince > 0.4f;
            for (int i = 0; i < eyes.Length; i++)
                if (eyes[i]) eyes[i].localScale = closed ? new Vector3(_eyeScale[i].x, _eyeScale[i].y * 0.15f, _eyeScale[i].z * 0.3f) : _eyeScale[i];

            // Head: turn towards the nurse when they are near, flinch briefly on the needle.
            if (!head) return;
            var target = _headRest;
            if (_cam && head.parent && (_cam.position - head.position).sqrMagnitude < lookDistance * lookDistance)
            {
                // The face points up (+Y of the head's parent); yaw around the body's long axis (+Z) towards the camera.
                Vector3 local = head.parent.InverseTransformPoint(_cam.position) - head.localPosition;
                float yaw = Mathf.Clamp(Mathf.Atan2(-local.x, local.y) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw);
                target = _headRest * Quaternion.Euler(0, 0, yaw);
            }
            if (_wince > 0)
            {
                target *= Quaternion.Euler(-12f * _wince, 0, 0);
                _wince = Mathf.Max(0, _wince - Time.deltaTime * 0.8f);
            }
            head.localRotation = Quaternion.Slerp(head.localRotation, target, Time.deltaTime * 3f);
        }
    }
}
