using Deviant.Combat;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    // Wind-up cue (user decision 2026-10-30): from the spec's windupCueFrame until the damage window the body
    // steps one native pixel every shakeStepSeconds (one side only, then both sides from shakeWidenFrom), and for
    // the last flashSeconds an overlay covers the body in one flat colour. Presentation only: the offset is on the
    // sprite transform and the flash is a separate renderer, so the actor, its boxes and the approved pixels stay.
    // Melee kinds (cue tintKinds) also redden: from the start of the attack to the flash the same overlay covers the
    // body in tintColor, its alpha rising to tintMax (playtest 2026-11-06: the wind-up should turn red as it builds).
    public sealed class Stage1VillageWindupCue
    {
        const float PixelsPerUnit = 32;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        readonly FieldMouseEnemy actor;
        readonly Stage1VillageMonsterAttack attack;
        readonly SpriteRenderer flash;
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public int OffsetPixels { get; private set; }
        public bool Flashing => flash && flash.enabled && !Reddening;
        // Alpha of the red wind-up overlay this frame (0 when none).
        public float Redness { get; private set; }
        bool Reddening => Redness > 0;

        public Stage1VillageWindupCue(FieldMouseEnemy actor, Stage1VillageMonsterAttack attack)
        {
            this.actor = actor; this.attack = attack;
            var overlay = new GameObject("Wind-up flash"); overlay.transform.SetParent(actor.visual.transform, false);
            flash = overlay.AddComponent<SpriteRenderer>();
            // Same flat unlit material as the white hit flash.
            flash.sharedMaterial = Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            flash.sortingLayerID = actor.visual.sortingLayerID; flash.sortingOrder = actor.visual.sortingOrder + 1; flash.enabled = false;
        }

        // Combat-clock window of the cue for the current attack; false when this species has none.
        public bool TryGetWindow(out float start, out float strike)
        {
            start = strike = 0;
            var spec = attack ? attack.Spec : null;
            if (spec == null || spec.cue == null || spec.windupCueFrame < 0) return false;
            var frames = spec.definition.attackFrameDurations;
            for (int i = 0; i < spec.windupCueFrame; i++) start += frames[i];
            // A cue that starts after the held pose is pushed back by the same hold as the rest of the attack.
            if (spec.prepareHoldFrame >= 0 && spec.windupCueFrame > spec.prepareHoldFrame) start += attack.HoldExtra;
            strike = spec.damageStart + attack.HoldExtra;
            return start < strike;
        }

        bool Melee(VillageWindupCue cue) => cue.tintKinds != null && cue.tintColor != null && cue.tintColor.Length == 3 &&
            System.Array.IndexOf(cue.tintKinds, attack.Spec.attackKind) >= 0;

        // Call after the presentation resets the sprite transform each frame.
        public void Draw(bool attacking)
        {
            OffsetPixels = 0; flash.enabled = false; Redness = 0;
            if (!attacking || !actor.visual.enabled || !TryGetWindow(out float start, out float strike)) return;
            float age = actor.SpeciesAttackAge;
            var cue = attack.Spec.cue;
            if (age >= 0 && age < strike - cue.flashSeconds && Melee(cue))
            {
                float t = age / (strike - cue.flashSeconds);
                Redness = cue.tintMax * t * t;
                flash.sprite = actor.visual.sprite; flash.flipX = actor.visual.flipX; flash.enabled = flash.sprite;
                var r = cue.tintColor;
                properties.SetColor(ColorId, new Color32((byte)r[0], (byte)r[1], (byte)r[2], 255)); flash.SetPropertyBlock(properties);
                flash.color = new Color(1, 1, 1, Redness);
            }
            if (age < start || age >= strike) return;
            bool odd = Mathf.FloorToInt(age / cue.shakeStepSeconds) % 2 == 1;
            OffsetPixels = (age - start) / (strike - start) >= cue.shakeWidenFrom ? (odd ? 1 : -1) : (odd ? 1 : 0);
            actor.visual.transform.localPosition = new Vector3(OffsetPixels / PixelsPerUnit, 0, 0);
            if (age < strike - cue.flashSeconds) return;
            Redness = 0;
            flash.sprite = actor.visual.sprite; flash.flipX = actor.visual.flipX; flash.enabled = flash.sprite; flash.color = Color.white;
            var c = cue.flashColor;
            properties.SetColor(ColorId, new Color32((byte)c[0], (byte)c[1], (byte)c[2], 255)); flash.SetPropertyBlock(properties);
        }
    }
}
