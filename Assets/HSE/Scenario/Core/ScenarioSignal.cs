using System;
using UnityEngine;

namespace HSE.Scenario
{
    /// <summary>
    /// A named game event as an asset. Gameplay code raises it; steps listen to it.
    /// Neither side references the other, so tasks can be added in the editor without touching gameplay code.
    /// </summary>
    [CreateAssetMenu(menuName = "HSE/Scenario/Signal", fileName = "Signal")]
    public class ScenarioSignal : ScriptableObject
    {
        public event Action Raised;

        public void Raise() => Raised?.Invoke();
    }
}
