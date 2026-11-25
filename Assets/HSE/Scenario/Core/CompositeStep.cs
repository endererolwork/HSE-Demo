using System.Collections.Generic;
using UnityEngine;

namespace HSE.Scenario
{
    /// <summary>
    /// A step made of smaller mandatory sub-tasks (Composite pattern), e.g. "Prepare equipment" =
    /// gloves + tourniquet + swab + tubes. The runner treats it like any other step.
    /// </summary>
    public class CompositeStep : ScenarioStep
    {
        public enum Order
        {
            [Tooltip("All sub-tasks are active and highlighted at once; any order completes the parent.")]
            AnyOrder,
            [Tooltip("Sub-tasks run one after another.")]
            InSequence
        }

        [SerializeField] Order order = Order.AnyOrder;
        [SerializeField] List<ScenarioStep> children = new List<ScenarioStep>();

        public IReadOnlyList<ScenarioStep> Children => children;
        public Order Mode => order;

        public void SetChildren(IEnumerable<ScenarioStep> steps, Order mode)
        {
            children = new List<ScenarioStep>(steps);
            order = mode;
        }

        protected override void OnBegin()
        {
            foreach (var c in children)
            {
                c.Completed += OnChildCompleted;
                c.MistakeMade += OnChildMistake;
            }
            if (order == Order.AnyOrder)
                foreach (var c in children) c.Begin(Context);
            BeginNextOrFinish();
        }

        protected override void OnEnd()
        {
            foreach (var c in children)
            {
                c.Completed -= OnChildCompleted;
                c.MistakeMade -= OnChildMistake;
                c.Abort();
            }
        }

        public override void ResetStep()
        {
            base.ResetStep();
            foreach (var c in children) c.ResetStep();
        }

        void OnChildCompleted(ScenarioStep child) => BeginNextOrFinish();

        void OnChildMistake(ScenarioStep child, string message) => ForwardMistake(child, message);

        void BeginNextOrFinish()
        {
            foreach (var c in children)
            {
                if (c.State == StepState.Completed) continue;
                if (order == Order.InSequence && c.State == StepState.Locked) c.Begin(Context);
                return; // something is still open
            }
            Complete();
        }
    }
}
