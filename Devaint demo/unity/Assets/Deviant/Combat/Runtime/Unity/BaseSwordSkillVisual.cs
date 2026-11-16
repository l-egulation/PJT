using System;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    [DefaultExecutionOrder(110)]
    internal sealed class BaseSwordSkillVisual : MonoBehaviour
    {
        const string Root = "CharacterBaseSwordReviewed/";
        static readonly float[] CleaveTimes = { -.040f, -.027f, -.014f, 0, .028f, .040f, .070f, .102f, .135f, .170f, .210f, .250f };
        static readonly float[] CutTimes = { -.017f, 0, .017f, .033f, .050f, .067f, .083f };
        static readonly float[] BurstTimes = { 0, .017f, .033f, .050f, .067f, .083f, .100f, .133f, .150f, .167f };
        static Sprite[] cleave, cut, thrust, wave, burst;
        static Sprite scar;
        CombatArena arena;
        CombatCast cast;
        CombatActor actor;
        int life, facing;
        float started;
        string action;
        Guid execution;
        SpriteRenderer view;
        Vector2 lastPoint;
        bool bursting, released;
        float sweepClock, sweepAge;

        static Sprite[] Load(string stem, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < frames.Length; ++i)
                frames[i] = Resources.Load<Sprite>(Root + stem + "-" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            return frames;
        }

        static int FrameAt(float age, float[] times)
        {
            int frame = 0;
            while (frame + 1 < times.Length && age >= times[frame + 1]) ++frame;
            return frame;
        }

        internal static Sprite WaveAt(float age, float flight)
        {
            wave ??= Load("wave", 15);
            return wave[SwordWaveFrames.At(age, flight)];
        }

        internal static BaseSwordSkillVisual Attach(CombatCast cast)
        {
            string id = cast.Definition.Id;
            Sprite[] frames;
            switch (id)
            {
                case "base_sword_default": frames = cleave ??= Load("cleave", CleaveTimes.Length); break;
                case "base_sword_follow_up": frames = cut ??= Load("echo-cut", CutTimes.Length); break;
                case "base_sword_movement": frames = thrust ??= Load("thrust", 8); break;
                default: return null;
            }
            if (!frames[0]) return null;
            var effect = Create(cast.arena, cast.owner, id, cast.FeelFacing);
            effect.cast = cast;
            return effect;
        }

        internal static BaseSwordSkillVisual AttachScar(CombatArena arena, CombatActor target, CombatHitOrigin origin, bool burstNow)
        {
            scar ??= Resources.Load<Sprite>(Root + "scar-00");
            burst ??= Load("scar-burst", BurstTimes.Length);
            if (!scar || !burst[0]) return null;
            var effect = Create(arena, target, "scar", origin.SourceFacing);
            effect.execution = origin.ExecutionId;
            effect.lastPoint = target.Center;
            if (burstNow) effect.Burst();
            else effect.Draw(scar, effect.lastPoint, 1);
            return effect;
        }

        internal bool Matches(CombatActor target, Guid id) =>
            action == "scar" && actor == target && life == target.LifeVersion && execution == id;

        internal void Burst()
        {
            started = arena.Now;
            bursting = true;
            if (actor) lastPoint = actor.Center;
            Draw(burst[0], lastPoint, 1);
        }

        static BaseSwordSkillVisual Create(CombatArena arena, CombatActor actor, string id, int facing)
        {
            var node = new GameObject("Basic sword visual " + id);
            node.transform.SetParent(arena.transform, false);
            var effect = node.AddComponent<BaseSwordSkillVisual>();
            effect.arena = arena;
            effect.actor = actor;
            effect.life = actor.LifeVersion;
            effect.action = id;
            effect.facing = facing < 0 ? -1 : 1;
            effect.started = arena.Now;
            var view = effect.view = node.AddComponent<SpriteRenderer>();
            view.sharedMaterial = arena.spriteMaterial;
            view.sortingLayerID = actor.visual.sortingLayerID;
            view.sortingOrder = actor.visual.sortingOrder + (id == "scar" ? 6 : -1);
            view.enabled = false;
            return effect;
        }

        void LateUpdate()
        {
            if (!arena || !arena.FeelFeedback || !arena.FeelFeedback.EffectsEnabled)
            { Destroy(gameObject); return; }
            if (action == "scar")
            {
                float time = arena.Now - started;
                if (time >= (bursting ? .18f : .34f) || (!bursting && (!actor || actor.Dead || actor.LifeVersion != life)))
                { Destroy(gameObject); return; }
                if (actor && actor.LifeVersion == life) lastPoint = actor.Center;
                Draw(bursting ? burst[FrameAt(time, BurstTimes)] : scar, lastPoint, 1);
                return;
            }
            if (!actor || actor.Dead || actor.LifeVersion != life)
            { Destroy(gameObject); return; }
            bool casting = cast && !cast.PresentationCancelled;
            if (action == "base_sword_movement")
            {
                if (casting) DrawDash();
                else Destroy(gameObject);
                return;
            }
            if (casting)
            {
                sweepAge = cast.Age - cast.ImpactTime;
                sweepClock = arena.Now;
                lastPoint = actor.Position;
                released |= cast.PresentationActivated && sweepAge >= 0;
            }
            else if (!released)
            { Destroy(gameObject); return; }
            float age = sweepAge + Mathf.Max(0, arena.Now - sweepClock);
            if (action == "base_sword_default") DrawSweep(cleave, CleaveTimes, age, .27f);
            else DrawSweep(cut, CutTimes, age, .095f);
        }

        void DrawSweep(Sprite[] frames, float[] times, float age, float end)
        {
            if (age >= end) { Destroy(gameObject); return; }
            if (age < times[0]) return;
            Draw(frames[FrameAt(age, times)], lastPoint, 1);
        }

        void DrawDash()
        {
            var motion = cast.ActionTracks?.Motion;
            if (motion == null) { Destroy(gameObject); return; }
            float age = cast.Age;
            if (age < motion.Start) return;
            float end = Mathf.Min(motion.End, cast.PresentationMotionDuration - cast.Definition.Movement.WallRecovery);
            if (age >= end + .075f) { Destroy(gameObject); return; }
            float sample = Mathf.Clamp01((age - motion.Start) / motion.Duration);
            Vector2 point = actor.Position + new Vector2(facing * (147f / 64f), 63f / 64f);
            Draw(thrust[Mathf.Min(thrust.Length - 1, (int)(sample * thrust.Length))], point,
                1 - Mathf.Clamp01((age - end) / .075f));
        }

        void Draw(Sprite sprite, Vector2 position, float opacity)
        {
            view.enabled = sprite != null;
            view.sprite = sprite;
            view.flipX = facing < 0;
            view.color = new Color(1, 1, 1, opacity);
            transform.position = position;
        }
    }
}
