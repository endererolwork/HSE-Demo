using HSE.Localization;
using HSE.Scenario;

namespace HSE.Training.UI
{
    /// <summary>
    /// Localised text for a step. The scenario core stays language-agnostic: a step's own Title/Instruction
    /// are the English fallback, and translations live under "step.&lt;id&gt;.title|instr|cue".
    /// </summary>
    public static class StepText
    {
        public static string Title(ScenarioStep s) => Loc.TOr($"step.{s.Id}.title", s.Title);
        public static string Instruction(ScenarioStep s) => Loc.TOr($"step.{s.Id}.instr", s.Instruction);

        /// <summary>Two-to-four word prompt shown next to the guide arrow ("Take the PDA").</summary>
        public static string Cue(ScenarioStep s) => Loc.TOr($"step.{s.Id}.cue", Title(s));
    }
}
