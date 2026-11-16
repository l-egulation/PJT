using System.Collections.Generic;
using Deviant.Presentation.Unity;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Deviant.Chapter2.Tests")]

namespace Deviant.Chapter2.Editor
{
    // URP's scene scan cannot see the emitters Chapter2 creates from Resources.
    // SRP removes a variant only when every registered stripper permits it, so
    // this public extension keeps runtime requirements for every Player build.
    public sealed class RuntimeLightShaderVariants : IShaderVariantStripper, IShaderVariantStripperScope, IPreprocessShaders
    {
        const string LightShader = "Hidden/Light2D";
        static readonly string[] ShapeKeywords = {
            "USE_SHAPE_LIGHT_TYPE_0", "USE_SHAPE_LIGHT_TYPE_1",
            "USE_SHAPE_LIGHT_TYPE_2", "USE_SHAPE_LIGHT_TYPE_3"
        };
        Requirement[] requirements;
        public bool active => true;
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> variants)
        {
            if (IsLightShader(shader) && variants.Count == 0)
                throw new BuildFailedException("The runtime point-light shader has no Player programs after stripping. Check RuntimeLightShaderVariants and the current URP keyword contract.");
        }

        readonly struct Requirement
        {
            readonly bool normal, fast, additive, volume;
            public Requirement(Light2D template, bool volumetric)
            {
                normal = template.normalMapQuality != Light2D.NormalMapQuality.Disabled;
                fast = template.normalMapQuality == Light2D.NormalMapQuality.Fast;
                additive = !volumetric && template.overlapOperation == Light2D.OverlapOperation.Additive;
                volume = volumetric;
            }
            public bool Matches(System.Predicate<string> enabled)
            {
                if (enabled("USE_NORMAL_MAP") != normal ||
                    enabled("LIGHT_QUALITY_FAST") != fast ||
                    enabled("USE_ADDITIVE_BLENDING") != additive ||
                    enabled("USE_VOLUMETRIC") != volume) return false;
                int shapes = 0;
                foreach (var keyword in ShapeKeywords) if (enabled(keyword)) shapes++;
                // URP remaps the active blend styles to MRT slots per layer batch.
                // Non-volume draws use one slot; volume draws use the default output.
                // Bind can change shadow strength and SetTransmissionMask can add or
                // remove a point cookie, so retain both states of those two keywords.
                return shapes == (volume ? 0 : 1);
            }
        }

        public void BeforeShaderStripping(Shader shader)
        {
            requirements = null;
            if (IsLightShader(shader)) CaptureRequirements();
        }
        public void AfterShaderStripping(Shader shader) { requirements = null; }

        public bool CanRemoveVariant(Shader shader, ShaderSnippetData snippet, ShaderCompilerData variant)
        {
            if (!IsLightShader(shader)) return true;
            return CanRemoveKeywords(shader, name => Enabled(shader, variant.shaderKeywordSet, name));
        }
        internal bool CanRemoveKeywords(Shader shader, System.Predicate<string> enabled)
        {
            if (!IsLightShader(shader)) return true;
            if (requirements == null) CaptureRequirements();
            foreach (var requirement in requirements)
                if (requirement.Matches(enabled)) return false;
            return true;
        }
        void CaptureRequirements()
        {
            var bindings = Resources.Load<LightingBindings>("Lighting/Bindings");
            if (!bindings || !bindings.lanternTemplate || !bindings.windowTemplate)
                throw new BuildFailedException("Runtime lighting bindings are missing. Rebuild the Lighting Lab bindings before building a Player.");
            var captured = new List<Requirement>();
            foreach (var emitter in new[] { bindings.lanternTemplate, bindings.windowTemplate })
            {
                var light = emitter.Output;
                captured.Add(new Requirement(light, false));
                if (light.volumetricEnabled) captured.Add(new Requirement(light, true));
            }
            requirements = captured.ToArray();
        }
        static bool IsLightShader(Shader shader) => shader && shader.name == LightShader;
        static bool Enabled(Shader shader, ShaderKeywordSet keywords, string name)
        {
            var keyword = new ShaderKeyword(shader, name);
            return keyword.IsValid() && keywords.IsEnabled(keyword);
        }
    }
}
