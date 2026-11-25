using UnityEngine;

namespace HSE.Scenario
{
    /// <summary>Where a step is in its life cycle.</summary>
    public enum StepState
    {
        Locked,
        Active,
        Completed,
        Skipped
    }

    /// <summary>Mandatory steps gate the scenario; optional ones run alongside it and only add bonus points.</summary>
    public enum StepRequirement
    {
        Mandatory,
        Optional
    }

    /// <summary>What a highlight is trying to say. The highlight service maps it to a concrete look.</summary>
    public enum HighlightStyle
    {
        Objective,
        Hint,
        Error,
        Success
    }

    /// <summary>
    /// Read-only description of a step. The report depends on this, not on MonoBehaviours,
    /// so scoring can be unit-tested without a scene (Dependency Inversion).
    /// </summary>
    public interface IStepInfo
    {
        string Id { get; }
        string Title { get; }
        StepRequirement Requirement { get; }
        int Points { get; }
    }

    /// <summary>Visual guidance. Steps only say "show this as an objective"; how it looks is the implementation's job.</summary>
    public interface IHighlightService
    {
        void Show(GameObject target, HighlightStyle style);
        void Hide(GameObject target);
        void Flash(GameObject target, HighlightStyle style);
    }

    /// <summary>A clock the runner and report share. Swappable for tests.</summary>
    public interface IScenarioClock
    {
        float Now { get; }
    }

    /// <summary>What a running step may use. Passed in when the step begins instead of looked up through singletons.</summary>
    public interface IScenarioContext
    {
        IHighlightService Highlighter { get; }
        IScenarioClock Clock { get; }
    }

    /// <summary>Highlighter that does nothing: the default when no visual service is assigned (Null Object).</summary>
    public sealed class NullHighlightService : IHighlightService
    {
        public static readonly NullHighlightService Instance = new NullHighlightService();
        NullHighlightService() { }
        public void Show(GameObject target, HighlightStyle style) { }
        public void Hide(GameObject target) { }
        public void Flash(GameObject target, HighlightStyle style) { }
    }

    public sealed class UnityClock : IScenarioClock
    {
        public static readonly UnityClock Instance = new UnityClock();
        UnityClock() { }
        public float Now => Time.time;
    }
}
