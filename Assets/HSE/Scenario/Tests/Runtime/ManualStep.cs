namespace HSE.Scenario.Tests
{
    /// <summary>Step that the test finishes by hand.</summary>
    public class ManualStep : ScenarioStep
    {
        public int BeginCount, EndCount;
        protected override void OnBegin() => BeginCount++;
        protected override void OnEnd() => EndCount++;
        public void Finish() => Complete();
        public void Fail(string why) => ReportMistake(why);
    }
}
