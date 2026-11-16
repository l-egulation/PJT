using System;
using System.Text;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // Saved and generated JSON share one DTO and the pure semantic compiler.
    public static class CueSourceJson
    {
        public const int MaximumFileBytes = 1024 * 1024;
        public static CueSourceDocument ReadDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumFileBytes)
                throw new ArgumentException("Cue JSON is empty or exceeds the allowed file size.");
            return JsonUtility.FromJson<CueSourceDocument>(json);
        }
        public static CompiledCueSource Compile(string json) => CueSourceCompiler.Compile(ReadDocument(json));
        public static CompiledCueSource Compile(TextAsset asset)
        {
            if (!asset) throw new ArgumentException("An explicit Cue source TextAsset is required.", nameof(asset));
            return Compile(asset.text);
        }
    }
}
