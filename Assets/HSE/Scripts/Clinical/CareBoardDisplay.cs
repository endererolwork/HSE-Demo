using TMPro;
using UnityEngine;

namespace HSE.Clinical
{
    /// <summary>Fills the room's care board with the current patient's details (always English).</summary>
    public class CareBoardDisplay : MonoBehaviour
    {
        [SerializeField] TextMeshPro header;
        [SerializeField] TextMeshPro body;
        [SerializeField] string nurseName = "VR User 1";

        public void Bind(TextMeshPro headerText, TextMeshPro bodyText)
        {
            header = headerText;
            body = bodyText;
        }

        void OnEnable()
        {
            ActivePatient.Changed += Show;
            if (ActivePatient.Current) Show(ActivePatient.Current);
        }

        void OnDisable() => ActivePatient.Changed -= Show;

        void Show(PatientRecord p)
        {
            if (!p) return;
            if (header) header.text = $"<b>ROOM {p.room} · BED {p.bed}</b>   {p.fullName}";
            if (body)
                body.text =
                    $"<b>Your nurse today:</b> {nurseName}\n" +
                    $"<b>Doctor:</b> {p.attendingPhysician}\n" +
                    $"<b>Problems:</b> {p.problems}\n" +
                    $"<b>Allergies:</b> <color=#C62828>{p.allergies}</color>\n" +
                    $"<b>Diet:</b> {p.diet}\n" +
                    $"<b>Mobility:</b> {p.mobility}    <b>Falls risk:</b> {p.fallsRisk}\n" +
                    $"<b>Today:</b> {p.carePlan}";
        }
    }
}
