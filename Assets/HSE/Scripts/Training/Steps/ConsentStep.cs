using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using HSE.Training.UI;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>
    /// GDD 3: explain the blood test and ask for verbal consent. In the "consent refused" branch the patient says no;
    /// the right response is to stop, document the refusal and tell the doctor, which ends the scenario early.
    /// </summary>
    public class ConsentStep : ScenarioStep
    {
        PatientDialogue _dialogue;

        protected override void OnBegin()
        {
            _dialogue = FindFirstObjectByType<PatientDialogue>();
            if (!_dialogue) { Complete(); return; }
            Ask();
        }

        protected override void OnEnd()
        {
            if (_dialogue) _dialogue.HideOptions();
        }

        void Ask() => _dialogue.ShowOptions(new[] { Loc.T("say.consent.good"), Loc.T("say.consent.noExplain") }, OnAsked);

        void OnAsked(int i)
        {
            if (i != 0)
            {
                ReportMistake(Loc.T("mistake.consent"));
                return;
            }
            if (TrainingBranch.Current == Branch.ConsentRefused)
            {
                _dialogue.Say(Loc.T("reply.refuse"), 6f);
                _dialogue.ShowOptions(new[] { Loc.T("say.refuse.respect"), Loc.T("say.refuse.push") }, OnRefusal);
                return;
            }
            _dialogue.Say(Loc.T("reply.consent"), 4f);
            Complete();
        }

        void OnRefusal(int i)
        {
            if (i != 0)
            {
                ReportMistake(Loc.T("mistake.forceProcedure"));
                return;
            }
            _dialogue.HideOptions();
            _dialogue.Say(Loc.T("reply.thanks"), 4f);
            Complete();
            // Valid stop: document the refusal and notify the doctor; the remaining steps no longer apply.
            var runner = FindFirstObjectByType<ScenarioRunner>();
            if (runner) runner.EndEarly(Loc.T("end.consentRefused"));
        }
    }
}