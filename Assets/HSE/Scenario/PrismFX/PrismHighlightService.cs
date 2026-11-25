using System.Collections.Generic;
using PrismFX;
using UnityEngine;

namespace HSE.Scenario.PrismFX
{
    /// <summary>
    /// <see cref="IHighlightService"/> backed by PrismFX Highlight (github.com/endererolwork/PrismVFX-Highlight).
    /// This adapter is the only scenario code that knows PrismFX exists; swapping the VFX package means
    /// writing another adapter, not touching steps or the runner (Dependency Inversion).
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismHighlightService : MonoBehaviour, IHighlightService
    {
        [Header("Look per highlight style")]
        [SerializeField] PrismPreset objective = PrismPreset.Objective;
        [SerializeField] PrismPreset hint = PrismPreset.SubtleHover;
        [SerializeField] PrismPreset error = PrismPreset.Danger;
        [SerializeField] PrismPreset success = PrismPreset.SciFiSelect;

        [Tooltip("If a target already has a PrismHighlight set up by hand, keep its look instead of applying a preset.")]
        [SerializeField] bool respectExistingSetup = true;

        [Tooltip("Show outline/glow through walls and other objects for objective targets.")]
        [SerializeField] bool objectivesVisibleThroughWalls = false;

        [Tooltip("For targets with a Canvas (e.g. the PDA), skip overlay/inner glow/reticle so the screen stays readable.")]
        [SerializeField] bool keepScreensReadable = true;

        // Components this service added, and which targets were configured by hand.
        readonly Dictionary<GameObject, PrismHighlight> _highlights = new Dictionary<GameObject, PrismHighlight>();
        readonly HashSet<GameObject> _handConfigured = new HashSet<GameObject>();

        public void Show(GameObject target, HighlightStyle style)
        {
            var h = Resolve(target);
            if (!h) return;
            if (!_handConfigured.Contains(target))
            {
                h.ApplyPreset(PresetFor(style));
                if (style == HighlightStyle.Objective && objectivesVisibleThroughWalls)
                    h.settings.outlineVisibility = PrismVisibility.AlwaysOnTop;
                else
                    // See-through also fires on an object's own hidden parts (a device's body behind its
                    // screen glass), which tints the whole object. Keep it for explicit "through walls" only.
                    h.settings.seeThroughMode = PrismSeeThroughMode.Never;
                if (keepScreensReadable && target.GetComponentInChildren<Canvas>(true))
                {
                    // Devices with a UI (PDA, terminals): only outline + glow, nothing drawn over the screen.
                    h.settings.overlay = 0f;
                    h.settings.innerGlow = 0f;
                    h.settings.target = false;
                    h.settings.touchGlow = false;
                    h.settings.hologram = false;
                }
                h.Refresh();
            }
            h.highlighted = true;
        }

        public void Hide(GameObject target)
        {
            if (target && _highlights.TryGetValue(target, out var h) && h) h.highlighted = false;
        }

        public void Flash(GameObject target, HighlightStyle style)
        {
            var h = Resolve(target);
            if (!h) return;
            switch (style)
            {
                case HighlightStyle.Error: h.Error(); break;
                case HighlightStyle.Success: h.Select(); break;
                default: h.HitFX(); break;
            }
        }

        PrismHighlight Resolve(GameObject target)
        {
            if (!target) return null;
            if (_highlights.TryGetValue(target, out var cached) && cached) return cached;

            var h = target.GetComponent<PrismHighlight>();
            if (h)
            {
                if (respectExistingSetup) _handConfigured.Add(target);
            }
            else
            {
                h = target.AddComponent<PrismHighlight>();
                h.trigger = PrismTrigger.Manual;
                h.selectOnClick = false;
            }
            _highlights[target] = h;
            return h;
        }

        PrismPreset PresetFor(HighlightStyle style) => style switch
        {
            HighlightStyle.Hint => hint,
            HighlightStyle.Error => error,
            HighlightStyle.Success => success,
            _ => objective
        };
    }
}
