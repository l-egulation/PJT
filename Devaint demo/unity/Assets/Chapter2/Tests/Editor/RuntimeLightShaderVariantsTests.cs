using System.Collections.Generic;
using System.Linq;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deviant.Chapter2.Tests
{
    public sealed class RuntimeLightShaderVariantsTests
    {
        Shader shader;
        RuntimeLightShaderVariants policy;
        [SetUp] public void Setup()
        {
            shader = Shader.Find("Hidden/Light2D");
            Assert.That(shader, Is.Not.Null);
            policy = new RuntimeLightShaderVariants();
            policy.BeforeShaderStripping(shader);
        }
        static IEnumerable<TestCaseData> RuntimeStates()
        {
            for (int slot = 0; slot < 4; slot++)
                foreach (bool shadow in new[] { false, true })
                    foreach (bool cookie in new[] { false, true })
                        yield return new TestCaseData(slot, shadow, cookie);
        }
        [TestCaseSource(nameof(RuntimeStates))]
        public void RuntimePointLightingSurvivesForEveryReachableMrtSlotAndOptionalMask(int slot, bool shadow, bool cookie)
        {
            var keywords = new List<string> { "USE_NORMAL_MAP", "USE_ADDITIVE_BLENDING", "USE_SHAPE_LIGHT_TYPE_" + slot };
            if (shadow) keywords.Add("USE_SHADOW_MAP");
            if (cookie) keywords.Add("USE_POINT_LIGHT_COOKIES");
            Assert.That(CanRemove(keywords.ToArray()), Is.False,
                "Resources-created lights and runtime shadow/cookie changes are invisible to URP's build-scene scan.");
        }
        [TestCase("USE_NORMAL_MAP", "USE_SHAPE_LIGHT_TYPE_0")]
        [TestCase("USE_ADDITIVE_BLENDING", "USE_SHAPE_LIGHT_TYPE_0")]
        [TestCase("USE_NORMAL_MAP", "USE_ADDITIVE_BLENDING")]
        [TestCase("USE_NORMAL_MAP", "USE_ADDITIVE_BLENDING", "USE_SHAPE_LIGHT_TYPE_0", "USE_SHAPE_LIGHT_TYPE_1")]
        [TestCase("USE_NORMAL_MAP", "LIGHT_QUALITY_FAST", "USE_ADDITIVE_BLENDING", "USE_SHAPE_LIGHT_TYPE_0")]
        [TestCase("USE_NORMAL_MAP", "USE_VOLUMETRIC")]
        public void UnusedLightFeaturesRemainEligibleForStripping(params string[] keywords)
            => Assert.That(CanRemove(keywords), Is.True);

        [Test] public void OtherShadersKeepTheirExistingStrippingPolicy()
        {
            var other = Shader.Find("Deviant/Lighting/LitInkOutline");
            Assert.That(other, Is.Not.Null);
            Assert.That(policy.CanRemoveVariant(other, default, default), Is.True);
        }
        [Test] public void BuildRejectsAnEmptyPointLightProgramList()
        {
            Assert.Throws<BuildFailedException>(() => policy.OnProcessShader(shader, default, new List<ShaderCompilerData>()));
            Assert.DoesNotThrow(() => policy.OnProcessShader(shader, default, new[] { default(ShaderCompilerData) }));
        }
        [Test] public void DefaultSrpBuildDiscoveryIncludesTheRuntimeLightingPolicy()
        {
            Assert.That(TypeCache.GetTypesDerivedFrom<IShaderVariantStripper>().Contains(typeof(RuntimeLightShaderVariants)), Is.True);
            Assert.That(typeof(RuntimeLightShaderVariants).GetConstructor(System.Type.EmptyTypes), Is.Not.Null);
            Assert.That(policy.active, Is.True, "Ordinary BuildPipeline builds must discover the policy without a custom option or entry point.");
        }
        bool CanRemove(params string[] names)
        {
            foreach (var name in names)
                Assert.That(shader.keywordSpace.FindKeyword(name).isValid, Is.True, "URP keyword contract changed: " + name);
            // Unity supplies the native shader-associated ShaderKeywordSet during a
            // real build; exercise the same policy here with explicit feature states.
            var enabled = new HashSet<string>(names);
            return policy.CanRemoveKeywords(shader, enabled.Contains);
        }
    }
}
