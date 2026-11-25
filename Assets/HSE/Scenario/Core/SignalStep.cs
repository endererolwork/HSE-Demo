using System;
using UnityEngine;

namespace HSE.Scenario
{
    /// <summary>
    /// Completes when <see cref="completeOn"/> is raised. Any of <see cref="mistakeOn"/> counts as a mistake.
    /// Covers most simple "do X" tasks without writing a new class.
    /// </summary>
    public class SignalStep : ScenarioStep
    {
        [SerializeField] ScenarioSignal completeOn;
        [SerializeField] ScenarioSignal[] mistakeOn = Array.Empty<ScenarioSignal>();
        [SerializeField] string mistakeMessage = "Wrong action.";

        public void SetSignals(ScenarioSignal complete, string mistakeText = null, params ScenarioSignal[] mistakes)
        {
            completeOn = complete;
            mistakeOn = mistakes ?? Array.Empty<ScenarioSignal>();
            if (mistakeText != null) mistakeMessage = mistakeText;
        }

        protected override void OnBegin()
        {
            if (completeOn) completeOn.Raised += Complete;
            foreach (var s in mistakeOn) if (s) s.Raised += OnMistake;
        }

        protected override void OnEnd()
        {
            if (completeOn) completeOn.Raised -= Complete;
            foreach (var s in mistakeOn) if (s) s.Raised -= OnMistake;
        }

        void OnMistake() => ReportMistake(mistakeMessage);
    }
}
