using HSE.Localization;
using HSE.Scenario;

namespace HSE.Training.UI
{
    /// <summary>
    /// Localised text for a step. The scenario core stays language-agnostic: a step's own Title/Instruction
    /// are the English fallback, and translations live under "step.&lt;id&gt;.title|instr|cue".
    /// Steps implementing <see cref="IStepTextProvider"/> can replace the cue and instruction while they run.
    /// </summary>
    public static class StepText
    {
        public static string Title(ScenarioStep s) => Loc.TOr($"step.{s.Id}.title", s.Title);

        public static string Instruction(ScenarioStep s) =>
            (s as IStepTextProvider)?.InstructionText ?? Loc.TOr($"step.{s.Id}.instr", s.Instruction);

        /// <summary>Two-to-four word prompt shown next to the guide arrow ("Take the PDA").</summary>
        public static string Cue(ScenarioStep s) =>
            (s as IStepTextProvider)?.CueText ?? Loc.TOr($"step.{s.Id}.cue", Title(s));
    }
}
