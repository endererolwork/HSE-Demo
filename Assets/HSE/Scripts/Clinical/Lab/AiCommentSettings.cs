using UnityEngine;

namespace HSE.Clinical.Lab
{
    /// <summary>
    /// Where AI commentary requests go. Holds no secrets: in builds use a proxy URL that adds the API key on the
    /// server side; in the Editor, ANTHROPIC_API_KEY from the environment is used when no proxy is set.
    /// </summary>
    [CreateAssetMenu(menuName = "HSE/AI Comment Settings", fileName = "AiCommentSettings")]
    public class AiCommentSettings : ScriptableObject
    {
        public bool enabled = true;
        [Tooltip("Your own HTTPS endpoint that forwards the request body to https://api.anthropic.com/v1/messages with the x-api-key header. Leave empty to use ANTHROPIC_API_KEY in the Editor only.")]
        public string proxyUrl = "";
        [Min(5)] public int timeoutSeconds = 30;
    }
}
