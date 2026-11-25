using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HSE.Clinical.Lab
{
    /// <summary>
    /// Asks Claude (Anthropic Messages API) for a short comment on the report, over raw HTTP with UnityWebRequest
    /// (the official C# SDK targets modern .NET, not Unity's IL2CPP runtime).
    ///
    /// Keys: never put an Anthropic API key in a Quest build. In builds, point <see cref="AiCommentSettings.proxyUrl"/>
    /// at your own small server that adds the key and forwards to api.anthropic.com. In the Editor only, the key can
    /// come from the ANTHROPIC_API_KEY environment variable for quick tests.
    /// Falls back to <see cref="RuleBasedCommentator"/> on any failure, refusal or timeout.
    /// </summary>
    public class ClaudeReportCommentator : IReportCommentator
    {
        const string ApiUrl = "https://api.anthropic.com/v1/messages";
        const string Model = "claude-opus-5-5";

        const string SystemPrompt =
            "You are a clinical training assistant inside a nursing VR simulation. The patient and all results are fictional. " +
            "Write 3 to 5 short sentences in plain English for a nursing student: which results are outside or at the edge of the " +
            "reference range, what they may suggest given the stated diagnosis, and what a nurse would usually do next (for example " +
            "inform the doctor, repeat a test, monitor). Do not diagnose with certainty and do not give medication doses. No headings, no lists.";

        readonly AiCommentSettings _settings;

        public ClaudeReportCommentator(AiCommentSettings settings) => _settings = settings;

        /// <summary>True when there is somewhere to send the request (a proxy, or an Editor-only key).</summary>
        public static bool IsAvailable(AiCommentSettings s) =>
            s != null && s.enabled && (!string.IsNullOrEmpty(s.proxyUrl) || (Application.isEditor && !string.IsNullOrEmpty(EditorKey())));

        public IEnumerator Comment(LabReport report, Action<string> done)
        {
            string fallback = RuleBasedCommentator.Build(report);
            bool viaProxy = !string.IsNullOrEmpty(_settings.proxyUrl);
            string url = viaProxy ? _settings.proxyUrl : ApiUrl;

            var body = new RequestBody
            {
                model = Model,
                max_tokens = 1024,
                system = SystemPrompt,
                output_config = new OutputConfig { effort = "low" },
                fallbacks = "default",
                messages = new[] { new Message { role = "user", content = "Lab report:\n" + report.ToPlainText() } },
            };
            var json = JsonUtility.ToJson(body);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Max(5, _settings.timeoutSeconds),
            };
            req.SetRequestHeader("content-type", "application/json");
            req.SetRequestHeader("anthropic-version", "2026-10-21");
            req.SetRequestHeader("anthropic-beta", "server-side-fallback-2026-07-01");
            if (!viaProxy) req.SetRequestHeader("x-api-key", EditorKey());

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HSE] AI comment request failed ({req.responseCode}): {req.error}. Using the offline comment.");
                done(fallback);
                yield break;
            }

            var text = ExtractText(req.downloadHandler.text, out var stopReason);
            if (stopReason == "refusal" || string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning($"[HSE] AI comment empty or declined (stop_reason={stopReason}). Using the offline comment.");
                done(fallback);
                yield break;
            }
            done(text.Trim());
        }

        /// <summary>Joins every "text" content block; thinking blocks and others are skipped.</summary>
        static string ExtractText(string responseJson, out string stopReason)
        {
            stopReason = null;
            try
            {
                var resp = JsonUtility.FromJson<Response>(responseJson);
                stopReason = resp.stop_reason;
                if (resp.content == null) return null;
                var sb = new StringBuilder();
                foreach (var block in resp.content)
                    if (block.type == "text" && !string.IsNullOrEmpty(block.text)) sb.Append(block.text);
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HSE] Could not parse AI response: " + e.Message);
                return null;
            }
        }

        static string EditorKey()
        {
#if UNITY_EDITOR
            return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
#else
            return null; // never read a key on device
#endif
        }

        // ------------------------------------------------------------ wire types (JsonUtility needs fields)

        [Serializable] class RequestBody
        {
            public string model;
            public int max_tokens;
            public string system;
            public OutputConfig output_config;
            public string fallbacks;
            public Message[] messages;
        }

        [Serializable] class OutputConfig { public string effort; }
        [Serializable] class Message { public string role; public string content; }
        [Serializable] class Response { public string stop_reason; public ContentBlock[] content; }
        [Serializable] class ContentBlock { public string type; public string text; }
    }
}
