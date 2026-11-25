using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSE.Localization
{
    public enum Language
    {
        English,
        Turkish
    }

    /// <summary>
    /// Minimal runtime localisation. English is the default; Turkish can be picked at the room entrance.
    /// UI code asks for a key; anything listening to <see cref="Changed"/> redraws when the language switches.
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "HSE.Language";
        static Language? _current;

        public static event Action Changed;

        public static Language Current
        {
            get
            {
                if (_current == null) _current = (Language)PlayerPrefs.GetInt(PrefKey, (int)Language.English);
                return _current.Value;
            }
        }

        public static void Set(Language language)
        {
            if (_current == language) return;
            _current = language;
            PlayerPrefs.SetInt(PrefKey, (int)language);
            Changed?.Invoke();
        }

        /// <summary>Text for a key in the current language. Unknown keys come back unchanged so gaps are visible.</summary>
        public static string T(string key)
        {
            if (key != null && LocStrings.Table.TryGetValue(key, out var pair))
                return Current == Language.Turkish && !string.IsNullOrEmpty(pair.tr) ? pair.tr : pair.en;
            return key;
        }

        /// <summary>Formatted text: <c>Loc.F("pda.age", 68)</c>.</summary>
        public static string F(string key, params object[] args) => string.Format(T(key), args);

        /// <summary>Pick between inline English/Turkish values (used for data that lives in assets).</summary>
        public static string Pick(string en, string tr) => Current == Language.Turkish && !string.IsNullOrEmpty(tr) ? tr : en;

        /// <summary>Like <see cref="T"/>, but falls back to <paramref name="fallback"/> instead of the key.</summary>
        public static string TOr(string key, string fallback) =>
            key != null && LocStrings.Table.ContainsKey(key) ? T(key) : fallback;

        internal static IReadOnlyDictionary<string, (string en, string tr)> Table => LocStrings.Table;
    }
}
