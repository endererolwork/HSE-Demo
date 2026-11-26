using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using HSE.Training.UI;

namespace HSE.Training.Steps
{
    /// <summary>
    /// GDD 1: introduce yourself and ask the patient to state their name and date of birth.
    /// The patient's answer is what the wristband is checked against.
    /// </summary>
    public class GreetingStep : ScenarioStep
    {
        PatientDialogue _dialogue;

        protected override void OnBegin()
        {
            _dialogue = FindFirstObjectByType<PatientDialogue>();
            if (!_dialogue) { Complete(); return; }
            _dialogue.ShowOptions(new[] { Loc.T("say.greet.good"), Loc.T("say.greet.rushed"), Loc.T("say.greet.noIntro") }, OnChosen);
        }

        protected override void OnEnd()
        {
            if (_dialogue) _dialogue.HideOptions();
        }

        void OnChosen(int i)
        {
            if (i != 0)
            {
                ReportMistake(Loc.T("mistake.introduce"));
                return;
            }
            var p = ActivePatient.Current;
            _dialogue.Say(p ? Loc.F("reply.greet", p.fullName, p.dateOfBirth) : Loc.T("reply.hello"), 6f);
            Complete();
        }
    }
}