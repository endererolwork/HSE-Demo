using UnityEngine;

namespace HSE.Environment
{
    /// <summary>Records which version of the scene builder produced this scene, so an outdated scene can be rebuilt.</summary>
    public class SceneBuildStamp : MonoBehaviour
    {
        /// <summary>Bump whenever the builders change what ends up in the scene.</summary>
        public const int CurrentVersion = 14;

        public int version;
    }
}
