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

        /// <summary>
        /// Skips the deepest sub-task that is active now (instructor shortcut). Reported as a mistake of this step.
        /// Returns false when no sub-task is active.
        /// </summary>
        public bool SkipActiveChild()
        {
            foreach (var c in children)
            {
                if (!c || c.State != StepState.Active) continue;
                if (c is CompositeStep inner && inner.SkipActiveChild()) return true;
                c.Abort();
                ForwardMistake(c, "Skipped: " + c.Title);
                BeginNextOrFinish();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Goes back one sub-task (instructor/tester shortcut): the active sub-task is closed and the one before it
        /// opens again. Returns false when the first sub-task is active, so the runner goes back a whole step.
        /// </summary>
        public bool BackOneChild()
        {
            if (order != Order.InSequence) return false;
            int i = children.FindIndex(c => c && c.State == StepState.Active);
            if (i < 0) return false;
            if (children[i] is CompositeStep inner && inner.BackOneChild()) return true;
            if (i == 0) return false;
            children[i].ResetStep();
            var prev = children[i - 1];
            prev.ResetStep();
            if (prev is CompositeStep pc) pc.PrepareResumeAtLast();
            prev.MarkReopened();
            BeginNextOrFinish();
            return true;
        }

        /// <summary>
        /// Call on a reset group before it begins: earlier sub-tasks count as done, so it opens at its last sub-task
        /// (used when going back into a finished group).
        /// </summary>
        public void PrepareResumeAtLast()
        {
            if (children.Count == 0) return;
            for (int i = 0; i < children.Count - 1; i++) if (children[i]) children[i].MarkCompletedWithoutRunning();
            var last = children[children.Count - 1];
            if (last is CompositeStep lc) lc.PrepareResumeAtLast();
            if (last) last.MarkReopened();
        }

        void OnChildCompleted(ScenarioStep child) => BeginNextOrFinish();

        void OnChildMistake(ScenarioStep child, string message) => ForwardMistake(child, message);

        void BeginNextOrFinish()
        {
            foreach (var c in children)
            {
                if (c.State == StepState.Completed || c.State == StepState.Skipped) continue;
                if (order == Order.InSequence && c.State == StepState.Locked) c.Begin(Context);
                return; // something is still open
            }
            Complete();
        }
    }
}
