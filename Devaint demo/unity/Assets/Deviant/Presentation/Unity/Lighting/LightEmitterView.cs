using System;
using Deviant.Definitions;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deviant.Presentation.Unity
{
    [RequireComponent(typeof(Light2D))]
    public sealed class LightEmitterView : MonoBehaviour
    {
        [SerializeField, Tooltip("Point-light transmission cookie. Unpacked square full-texture Sprite, white RGB = open, black RGB = blocked. Not the housing's emission alpha.")]
        Sprite transmissionMask;
        LightEmitterDefinition definition;
        Light2D output;
        uint seed;
        bool nativeContribution = true;
        public float ComputedIntensity { get; private set; }
        public LightEmitterDefinition Definition => definition;
        public bool NativeContributionEnabled => nativeContribution;
        public Sprite TransmissionMask => transmissionMask;
        public Light2D Output => output ? output : (output = GetComponent<Light2D>());
        public void Bind(LightEmitterDefinition value, uint stableSeed)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            ValidateMask(transmissionMask);
            // URP exposes these authoring properties as read-only. They are baked into
            // the template with SerializedObject by the Editor, never patched at runtime.
            if (Output.normalMapQuality != Light2D.NormalMapQuality.Accurate || !Mathf.Approximately(Output.normalMapDistance, value.NormalDistance))
                throw new InvalidOperationException("Rebuild the lighting templates for the current normal-map settings.");
            definition = value; seed = stableSeed;
            Output.lightType = Light2D.LightType.Point; Output.color = new Color(value.Color.R, value.Color.G, value.Color.B);
            Output.pointLightOuterRadius = value.Radius; Output.pointLightInnerRadius = 0;
            Output.lightCookieSprite = transmissionMask;
            Output.shadowIntensity = value.ShadowStrength; Output.shadowsEnabled = nativeContribution && value.ShadowStrength > 0;
            Output.shadowSoftness = value.ShadowSoftness;
            Output.shadowSoftnessFalloffIntensity = value.ShadowFalloff;
            Output.falloffIntensity = value.LightFalloff;
            Sample(0);
        }
        public void SetTransmissionMask(Sprite mask)
        {
            if (definition == null || !nativeContribution || Output.lightType != Light2D.LightType.Point)
                throw new InvalidOperationException("Transmission cookies belong to bound native point emitters, not directional windows.");
            ValidateMask(mask); transmissionMask = mask; Output.lightCookieSprite = mask;
        }
        static void ValidateMask(Sprite mask)
        {
            if (!mask) return;
            var texture = mask.texture;
            // URP's point cookie samples the whole texture, not a Sprite atlas rect.
            if (!texture || mask.packed || texture.width != texture.height || mask.rect != new Rect(0, 0, texture.width, texture.height))
                throw new ArgumentException("Point transmission masks must be unpacked square full-texture sprites; atlas/cropped sprites are not supported.");
            if (texture.wrapMode != TextureWrapMode.Clamp || texture.mipmapCount != 1)
                throw new ArgumentException("Transmission data needs Clamp wrapping and no mipmaps.");
        }
        public void SetNativeContribution(bool enabled)
        {
            nativeContribution = enabled; Output.intensity = enabled ? ComputedIntensity : 0;
            Output.shadowsEnabled = enabled && definition != null && definition.ShadowStrength > 0;
        }
        public void Sample(double seconds)
        {
            if (definition == null) return;
            ComputedIntensity = definition.IntensityAt(seconds, seed); Output.intensity = nativeContribution ? ComputedIntensity : 0;
        }
    }
}
