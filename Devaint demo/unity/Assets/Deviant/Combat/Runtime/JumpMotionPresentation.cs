using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Presentation only: reads the player's vertical speed and grounded flag, never writes movement or combat state.
    // Ordinary play keeps the approved native silhouette; ActorRenderInterpolation smooths fixed-step movement.
    // These optional effects remain available to the Editor A/B review, with both disabled by default:
    //   stretch:    tall while rising fast and a little tall while falling fast; landing keeps the native size;
    //   afterimage: faint copies of the drawing that was showing a moment ago, only while moving fast vertically.
    public sealed class JumpMotionPresentation : IDisposable
    {
        public const float RiseReference = 20f;          // first jump launch speed (PlayerLocomotion.json); only scales the look
        public const float RiseStretch = .16f, FallStretch = .08f, FallStart = 6f, FallRange = 20f;
        public const float EchoSpeed = 7f, EchoInterval = 1f / 30, EchoLife = .1f, EchoOpacity = .35f;

        struct Echo { public SpriteRenderer renderer; public float born; }
        readonly List<Echo> echoes = new List<Echo>();
        float nextEcho = float.NegativeInfinity, lastNow = -1;
        SpriteRenderer scaledSource;
        Vector3 nativeScale, appliedScale;
        public int ActiveAfterimages => echoes.Count;
        public bool StretchEnabled { get; set; }
        public bool AfterimagesEnabled { get; set; }

        /// <summary>Vertical scale for the body (x is 1/sqrt so the area stays about the same). 1 = untouched.</summary>
        public static float Stretch(bool grounded, float velocityY, float sinceLanding)
        {
            if (!grounded)
            {
                if (velocityY > 0) return 1 + RiseStretch * Mathf.Min(1, velocityY / RiseReference);
                if (velocityY < -FallStart) return 1 + FallStretch * Mathf.Min(1, (-velocityY - FallStart) / FallRange);
                return 1;
            }
            return 1;
        }

        /// <param name="suppressed">Dash, attacks, death and other owners of the body pose: no stretch, no new echoes.</param>
        public void Draw(in ActorReadSnapshot actor, SpriteRenderer source, bool suppressed)
        {
            RestoreScale();
            if (!source || !source.enabled || !source.gameObject.activeInHierarchy) { Reset(); return; }
            float now = actor.Now;
            if (now < lastNow) Reset();
            lastNow = now;
            var k = actor.Kinematics;
            FadeEchoes(now);
            if (k.Grounded || !AfterimagesEnabled) ClearEchoes();
            if (suppressed || actor.Vitals.Dead) return;

            float s = StretchEnabled ? Stretch(k.Grounded, k.VelocityY, 0) : 1;
            if (s != 1)
            {
                scaledSource = source;
                nativeScale = source.transform.localScale;
                appliedScale = Vector3.Scale(nativeScale, new Vector3(1 / Mathf.Sqrt(s), s, 1));
                source.transform.localScale = appliedScale;
            }

            if (!AfterimagesEnabled || k.Grounded || Mathf.Abs(k.VelocityY) <= EchoSpeed || !source.sprite) { nextEcho = now; return; }
            if (float.IsNegativeInfinity(nextEcho)) nextEcho = now;
            if (now < nextEcho) return;
            Spawn(source, now);
            // Never backfill missed echoes at one position after a slow frame.
            nextEcho += (Mathf.Floor((now - nextEcho) / EchoInterval) + 1) * EchoInterval;
        }

        void Spawn(SpriteRenderer source, float now)
        {
            var node = new GameObject("Jump afterimage", typeof(SpriteRenderer));
            node.hideFlags = HideFlags.DontSave;
            node.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            node.transform.localScale = source.transform.lossyScale;
            var drawing = node.GetComponent<SpriteRenderer>();
            drawing.sprite = source.sprite; drawing.sharedMaterial = source.sharedMaterial;
            drawing.flipX = source.flipX; drawing.flipY = source.flipY;
            drawing.sortingLayerID = source.sortingLayerID; drawing.sortingOrder = source.sortingOrder - 1;
            var color = source.color; color.a *= EchoOpacity; drawing.color = color;
            echoes.Add(new Echo { renderer = drawing, born = now });
        }

        void FadeEchoes(float now)
        {
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                var echo = echoes[i]; float age = now - echo.born;
                if (!echo.renderer || age >= EchoLife || age < 0) { Destroy(echo.renderer); echoes.RemoveAt(i); continue; }
                var color = echo.renderer.color; color.a = EchoOpacity * (1 - age / EchoLife); echo.renderer.color = color;
            }
        }

        static void Destroy(SpriteRenderer renderer)
        {
            if (!renderer) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(renderer.gameObject);
            else UnityEngine.Object.DestroyImmediate(renderer.gameObject);
        }

        void RestoreScale()
        {
            // Restore only our last write; a later pose owner may already have replaced it.
            if (scaledSource && scaledSource.transform.localScale == appliedScale)
                scaledSource.transform.localScale = nativeScale;
            scaledSource = null;
        }

        public void Reset()
        {
            RestoreScale();
            ClearEchoes();
            nextEcho = float.NegativeInfinity; lastNow = -1;
        }
        void ClearEchoes()
        {
            foreach (var echo in echoes) Destroy(echo.renderer);
            echoes.Clear();
        }
        public void Dispose() => Reset();
    }
}
