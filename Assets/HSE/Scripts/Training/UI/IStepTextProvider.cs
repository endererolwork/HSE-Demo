namespace HSE.Training.UI
{
    /// <summary>
    /// A step whose prompt changes while it runs (e.g. "Tube 1/2: light blue cap").
    /// Return null to fall back to the step's normal localised text.
    /// </summary>
    public interface IStepTextProvider
    {
        string CueText { get; }
        string InstructionText { get; }
    }
}
