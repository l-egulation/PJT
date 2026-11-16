using System;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable]
    public sealed class CombatFeelSettings
    {
        // Per action id (CombatHitOrigin.ActionId). Unlisted actions use the global values.
        [Serializable] public sealed class ActionFeel { public string action; public float hitStop, shakeDistance, shakeSeconds, flashSeconds, lift; public bool heavy; }
        [Serializable] public sealed class KillFeel { public float hitStop, shakeDistance, shakeSeconds, fly, lift; }
        [Serializable] public sealed class RecoilFeel { public float basic, big, basicSeconds, bigSeconds; }
        [Serializable] public sealed class CameraFeel { public float zoom; }
        public int schema;
        public bool enabled;
        public float hitStopSeconds, heavyHitStopSeconds, heavyDamageThreshold;
        public float shakeSeconds, shakeDistance, flashSeconds, burstSeconds;
        public int inkParticles;
        public int[] impactRingVariants={1,4};
        public bool centeredImpact = true, inkSwordTrail = true, hitStopEnabled;
        public bool continuousSwordTrail = true;
        public bool compactHud=true,whipTrail=true;
        public float playerHurtOpacity=.5f,playerHurtBorder=.16f,playerHurtSeconds=.9f,playerHurtRiseSeconds=.15f;
        public bool killPop = true, damageNumbers = true, impactRing = true, heavyCleave = true, underfootHealthBars = true;
        public int flashDirections = 3;
        public float impactSeconds = .11f, directionDelay = .014f, impactScale = 1.1f;
        public float particleSpread = 1.9f, particleSize = .075f;
        // Pixel swing trails (blade tip, hammer, 강베기) drawn by CombatFeelPixelFx instead of the blade mesh.
        // pixelHits also replaces the impact variants with the pixel burst; it stays off while the
        // team's approved hit-effect asset is pending, so the existing impact path remains in place.
        public bool pixelEffects, pixelHits;
        // Off while the team's hit feedback (HitFeedbackBridge) owns the contact flash, impact VFX and debris.
        public bool contactFlash = true;
        public string effectColor = "#ffffff", effectCore = "#ffffff";
        public float liftGravity = 28.125f;
        public ActionFeel[] actions = new ActionFeel[0];
        public KillFeel kill = new KillFeel();
        public RecoilFeel recoil = new RecoilFeel();
        public CameraFeel combatCamera = new CameraFeel();

        public ActionFeel Find(string action)
        {
            if (string.IsNullOrEmpty(action) || actions == null) return null;
            foreach (var feel in actions) if (feel.action == action) return feel;
            return null;
        }
        public Color32 EffectColor => Parse(effectColor);
        public Color32 EffectCore => Parse(effectCore);
        static Color32 Parse(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? (Color32)c : new Color32(255, 255, 255, 255);

        public void Validate()
        {
            if (schema != 1 || !Range(hitStopSeconds, 0, .15f) ||
                !Range(heavyHitStopSeconds, 0, .15f) || !Range(heavyDamageThreshold, .001f, 1000000) ||
                !Range(shakeSeconds, .001f, .5f) || !Range(shakeDistance, 0, .3f) ||
                !Range(flashSeconds, .001f, .2f) || !Range(burstSeconds, .01f, 1) ||
                inkParticles < 0 || inkParticles > 40 || flashDirections < 1 || flashDirections > 3 ||
                !Range(impactSeconds, .025f, .4f) || !Range(directionDelay, 0, .05f) ||
                !Range(impactScale, .25f, 3) || !Range(particleSpread, .1f, 4) || !Range(particleSize, .01f, .3f) ||
                !Range(playerHurtOpacity,0,.5f)||!Range(playerHurtBorder,.01f,.2f)||!Range(playerHurtSeconds,.05f,1) ||
                !Range(playerHurtRiseSeconds,0,.3f)||playerHurtRiseSeconds>=playerHurtSeconds ||
                !Range(liftGravity, 1, 200) || !ColorUtility.TryParseHtmlString(effectColor, out _) || !ColorUtility.TryParseHtmlString(effectCore, out _))
                throw new ArgumentException("Invalid experimental hit feedback settings.");
            if(impactRingVariants==null||Array.Exists(impactRingVariants,v=>v<0||v>5))throw new ArgumentException("Invalid ring variants.");
            if (actions == null || kill == null || recoil == null) throw new ArgumentException("Hit feel actions, kill and recoil are required.");
            foreach (var feel in actions)
                if (feel == null || string.IsNullOrEmpty(feel.action) || !Range(feel.hitStop, 0, .15f) || !Range(feel.shakeDistance, 0, .3f) ||
                    !Range(feel.shakeSeconds, .001f, .5f) || !Range(feel.flashSeconds, .001f, .2f) || !Range(feel.lift, 0, 3))
                    throw new ArgumentException("Invalid hit feel action: " + feel?.action);
            if (!Range(kill.hitStop, 0, .15f) || !Range(kill.shakeDistance, 0, .3f) || !Range(kill.shakeSeconds, 0, .5f) || !Range(kill.fly, 0, 4) || !Range(kill.lift, 0, 3) ||
                !Range(recoil.basic, 0, .8f) || !Range(recoil.big, 0, .8f) || !Range(recoil.basicSeconds, 0, .6f) || !Range(recoil.bigSeconds, 0, .6f))
                throw new ArgumentException("Invalid hit feel kill or recoil values.");
            if (combatCamera == null || !Range(combatCamera.zoom, 0, .3f))
                throw new ArgumentException("Invalid combat camera values.");
        }
        static bool Range(float value, float min, float max)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        public static CombatFeelSettings Load()
        {
            var source = Resources.Load<TextAsset>("CombatFeelFeedback");
            if (!source) throw new InvalidOperationException("Export data/combat/presentation/hit_feedback.json first.");
            var settings = JsonUtility.FromJson<CombatFeelSettings>(source.text);
            if (settings == null) throw new InvalidOperationException("Missing hit feedback settings.");
            settings.Validate(); return settings;
        }
    }

    // Gameplay side of the hit-feel tuning: per-action enemy push and player basic step.
    // Source: data/combat/attack_feel_motion.json (tools/combat/export_hit_feedback.py).
    [Serializable]
    public sealed class CombatFeelMotion
    {
        [Serializable] public sealed class ActionMotion { public string action; public float push, pushSeconds, step; }
        public int schema;
        public float stepGap;
        public ActionMotion[] actions = new ActionMotion[0];

        public bool TryGet(string action, out ActionMotion motion)
        {
            motion = null;
            if (string.IsNullOrEmpty(action) || actions == null) return false;
            foreach (var m in actions) if (m.action == action) { motion = m; return true; }
            return false;
        }
        public void Validate()
        {
            if (schema != 1 || !Range(stepGap, 0, 1) || actions == null) throw new ArgumentException("Invalid attack feel motion.");
            foreach (var m in actions)
                if (m == null || string.IsNullOrEmpty(m.action) || !Range(m.push, 0, 4) || !Range(m.step, 0, 2) ||
                    !Range(m.pushSeconds, 0, 1) || m.push > 0 && m.pushSeconds < .01f)
                    throw new ArgumentException("Invalid attack feel motion: " + m?.action);
        }
        static bool Range(float value, float min, float max)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        public static CombatFeelMotion Load()
        {
            var source = Resources.Load<TextAsset>("CombatFeelMotion");
            if (!source) throw new InvalidOperationException("Export data/combat/attack_feel_motion.json first.");
            var motion = JsonUtility.FromJson<CombatFeelMotion>(source.text);
            if (motion == null) throw new InvalidOperationException("Missing attack feel motion.");
            motion.Validate(); return motion;
        }
    }
}
