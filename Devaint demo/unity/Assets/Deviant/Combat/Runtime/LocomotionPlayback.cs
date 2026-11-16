using System;
using UnityEngine;

namespace Deviant.Combat
{
    // Per-form playback speed of the looping body clips (run/walk/idle). 1 plays the authored frame times; 2 plays them twice as fast.
    // Edit with Tools/deviant/Presentation/Locomotion Playback. Source: data/combat/presentation/locomotion_playback.json, mirrored
    // into Resources/CharacterV2/LocomotionPlayback.json by that tool. Forms without an entry play at 1.
    public static class LocomotionPlayback
    {
        public const string ResourcePath = "CharacterV2/LocomotionPlayback";
        public const float Minimum = .25f, Maximum = 3f;
        [Serializable] public sealed class Entry { public string form; public float run = 1, walk = 1, idle = 1; }
        [Serializable] public sealed class Document { public int schemaVersion = 1; public Entry[] forms = new Entry[0]; }

        static Document loaded;

        public static Document Current => loaded ??= Read();
        public static void Reload() => loaded = null;

        public static float Scale(string form, string state)
        {
            if (string.IsNullOrEmpty(form) || state != "run" && state != "walk" && state != "idle") return 1;
            foreach (var entry in Current.forms)
                if (entry != null && entry.form == form) return state == "run" ? entry.run : state == "walk" ? entry.walk : entry.idle;
            return 1;
        }

        public static bool TryParse(string json, out Document document, out string error)
        {
            document = null; error = null;
            try { document = JsonUtility.FromJson<Document>(json); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (document == null || document.schemaVersion != 1 || document.forms == null) { error = "Unsupported locomotion playback document."; document = null; return false; }
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in document.forms)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.form) || !seen.Add(entry.form)) error = "Every entry needs a unique form.";
                else foreach (float value in new[] { entry.run, entry.walk, entry.idle })
                    if (float.IsNaN(value) || value < Minimum || value > Maximum) error = entry.form + ": speed must be within " + Minimum + ".." + Maximum + ".";
                if (error != null) { document = null; return false; }
            }
            return true;
        }

        static Document Read()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (!asset) return new Document();
            if (TryParse(asset.text, out var document, out var error)) return document;
            Debug.LogError("LocomotionPlayback: " + error);
            return new Document();
        }
    }
}
