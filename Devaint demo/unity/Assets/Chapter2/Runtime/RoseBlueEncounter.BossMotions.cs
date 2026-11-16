using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        // User-approved Rose/Blue body motions (resources boss-motions-v8-20261108) and idle breaths (boss-idle-breath-20261107).
        // Cells are 384x256 with the feet pivot at (192,232), drawn at original size (32 px per unit) on the actor's feet.
        // Presentation only: the frame comes from the cast clock (stretched by each motion's cue keys so the key pose lands on
        // the Patterns.cs cue), so combat cues, aim, hurtboxes and visibility rules for damage stay unchanged.
        [Serializable] sealed class BossMotion { public string id, sheet; public int columns, cells, totalMs; public int[] sequence, durationsMs; public bool loop; public float[] cueKeys; }
        [Serializable] sealed class BossMotionTable { public string format; public int cellWidthPx, cellHeightPx, pixelsPerUnit; public int[] pivotPx; public BossMotion[] motions; }

        BossMotionTable motionTable;
        readonly Dictionary<string, (BossMotion motion, Sprite[] cells)> bossMotions = new Dictionary<string, (BossMotion, Sprite[])>();
        float roseStunAt = float.NegativeInfinity, blueStunAt = float.NegativeInfinity, restStartedAt, completedAt = float.NaN;
        float lastRoseStunUntil, lastBlueStunUntil;
        bool wasRestingArt;

        static readonly Dictionary<Pattern, string> FusedMotionOf = new Dictionary<Pattern, string>
        { { Pattern.Homing, "p3-homing" }, { Pattern.Bounce, "p3-bounce" }, { Pattern.ArrowRain, "p3-rain" }, { Pattern.ExpandingFan, "p3-fan" } };

        void LoadBossMotions()
        {
            if (motionTable != null) return;
            var config = Resources.Load<TextAsset>("RoseBlueMotions");
            if (!config) throw new InvalidOperationException("Missing Rose/Blue motion binding");
            var table = JsonUtility.FromJson<BossMotionTable>(config.text);
            motionTable = table;
            foreach (var m in table.motions)
            {
                if (m.durationsMs == null || m.sequence == null || m.durationsMs.Length != m.sequence.Length) throw new InvalidOperationException("Motion timing mismatch: " + m.id);
                if (m.sequence.Any(i => i < 0 || i >= m.cells)) throw new InvalidOperationException("Motion cell out of range: " + m.id);
                if (m.cueKeys != null && m.cueKeys.Length % 2 != 0) throw new InvalidOperationException("Motion cue keys must be pairs: " + m.id);
                bossMotions[m.id] = (m, null); // cells are cut from the sheet on first use or by WarmArtNear
            }
        }

        Sprite[] MotionCells(BossMotion m)
        {
            var tex = Resources.Load<Texture2D>("RoseBlueMotions/" + m.sheet);
            if (!tex) throw new InvalidOperationException("Deploy approved Rose/Blue motion: " + m.sheet);
            int w = motionTable.cellWidthPx, h = motionTable.cellHeightPx, cols = m.columns;
            if (cols < 1 || cols * w > tex.width || (m.cells + cols - 1) / cols * h > tex.height) throw new InvalidOperationException("Motion sheet too small: " + m.sheet);
            var pivot = new Vector2(motionTable.pivotPx[0] / (float)w, 1 - motionTable.pivotPx[1] / (float)h);
            var cells = new Sprite[m.cells];
            for (int i = 0; i < m.cells; i++)
            {
                cells[i] = Sprite.Create(tex, new Rect(i % cols * w, tex.height - (i / cols + 1) * h, w, h), pivot, motionTable.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                ownedSprites.Add(cells[i]);
            }
            return cells;
        }

        /// <summary>Motion time for a game age: piecewise linear through [game, motion] key pairs, then 1:1 after the last key.</summary>
        public static double WarpAge(float[] keys, double gameSeconds)
        {
            if (keys == null || keys.Length < 4) return gameSeconds;
            double g = Math.Max(0, gameSeconds);
            if (g <= keys[0]) return keys[1];
            for (int i = 2; i < keys.Length; i += 2)
            {
                if (g <= keys[i])
                {
                    double g0 = keys[i - 2], c0 = keys[i - 1], g1 = keys[i], c1 = keys[i + 1];
                    return g1 > g0 ? c0 + (c1 - c0) * (g - g0) / (g1 - g0) : c1;
                }
            }
            return keys[keys.Length - 1] + (g - keys[keys.Length - 2]);
        }

        /// <summary>Step of the play order shown at a motion time; one-shots hold the last step, loops wrap.</summary>
        public static int MotionStepAt(int[] durationsMs, double seconds, bool loop)
        {
            double ms = Math.Max(0, seconds) * 1000;
            if (loop) { int total = durationsMs.Sum(); if (total > 0) ms %= total; }
            for (int i = 0; i < durationsMs.Length; i++)
            {
                if (ms < durationsMs[i]) return i;
                ms -= durationsMs[i];
            }
            return durationsMs.Length - 1;
        }

        /// <summary>Game seconds a motion takes with its cue keys (last key when present, else the clip length).</summary>
        float MotionGameSeconds(string id)
        {
            var m = bossMotions[id].motion;
            return m.cueKeys != null && m.cueKeys.Length >= 4 ? m.cueKeys[m.cueKeys.Length - 2] + Math.Max(0, m.totalMs / 1000f - m.cueKeys[m.cueKeys.Length - 1]) : m.totalMs / 1000f;
        }

        // The motion with its cells, cutting them from the sheet the first time (sheets stay unloaded until the boss is near).
        (BossMotion motion, Sprite[] cells) BossMotionEntry(string id)
        {
            var entry = bossMotions[id];
            if (entry.cells == null) bossMotions[id] = entry = (entry.motion, MotionCells(entry.motion));
            return entry;
        }

        Sprite MotionSprite(string id, double gameAge)
        {
            var (m, cells) = BossMotionEntry(id);
            return cells[m.sequence[MotionStepAt(m.durationsMs, WarpAge(m.cueKeys, gameAge), m.loop)]];
        }

        void ShowMotion(RoseBlueActor actor, string id, double gameAge)
        {
            actor.visual.sprite = MotionSprite(id, gameAge);
            actor.visual.color = Color.white;
            actor.visual.transform.localPosition = Vector3.zero;
            actor.visual.transform.localScale = Vector3.one;
        }

        Cast ActiveCast(Pattern pattern, Func<Cast, bool> owner) => casts.LastOrDefault(c => c.Pattern == pattern && !c.PresentationCancelled && owner(c));

        /// <summary>Names of the motions on screen this frame (for tests and debugging).</summary>
        public string RoseMotion { get; private set; }
        public string BlueMotion { get; private set; }

        // Called every presentation step outside phase1 layer shots.
        void ApplyBossMotions()
        {
            LoadBossMotions();
            float now = arena.Now;
            if (RoseMotionStunUntil > lastRoseStunUntil && now < RoseMotionStunUntil) roseStunAt = now;
            if (BlueMotionStunUntil > lastBlueStunUntil && now < BlueMotionStunUntil) blueStunAt = now;
            lastRoseStunUntil = RoseMotionStunUntil; lastBlueStunUntil = BlueMotionStunUntil;
            bool resting = Model.Phase == Phase.FusedBlue && wasResting;
            if (resting && !wasRestingArt) restStartedAt = now;
            wasRestingArt = resting;
            if (Model.Phase == Phase.Complete) { if (float.IsNaN(completedAt)) { completedAt = now; } } else completedAt = float.NaN;

            // Rose
            string rose = "rose-breath"; double roseAge = now;
            if (Model.Phase == Phase.Duo)
            {
                Cast c;
                if (now < RoseMotionStunUntil) { rose = "rose-stun"; roseAge = now - roseStunAt; }
                else if ((c = ActiveCast(Pattern.StunAndInk, x => !x.RoseCancelled)) != null) { rose = "rose-stun-shot"; roseAge = SketchMotionAge(c, true); }
                else if ((c = ActiveCast(Pattern.Swap, x => !x.RoseCancelled)) != null && SketchMotionAge(c, true) < MotionGameSeconds("rose-sky-volley")) { rose = "rose-sky-volley"; roseAge = SketchMotionAge(c, true); }
            }
            ShowMotion(Rose, rose, roseAge); RoseMotion = rose;

            // Blue
            string blue = "blue-breath"; double blueAge = now;
            if (Model.Phase == Phase.Machine)
            {
                var c = ActiveCast(Pattern.MachineAndPlatforms, _ => true);
                if (c != null) { blue = "blue-ink-burst"; blueAge = SketchMotionAge(c, false); }
            }
            else if (Model.Phase == Phase.Duo)
            {
                Cast c;
                if (now < BlueMotionStunUntil) { blue = "blue-stun"; blueAge = now - blueStunAt; }
                else if ((c = ActiveCast(Pattern.StunAndInk, x => !x.BlueCancelled)) != null) { blue = "blue-ink-throw"; blueAge = SketchMotionAge(c, false); }
                else if ((c = ActiveCast(Pattern.Swap, x => !x.BlueCancelled)) != null && SketchMotionAge(c, false) < MotionGameSeconds("blue-ink-whirl")) { blue = "blue-ink-whirl"; blueAge = SketchMotionAge(c, false); }
            }
            else if (Model.Phase == Phase.FusedBlue || Model.Phase == Phase.Complete)
            {
                blue = "fused-breath";
                var c = casts.LastOrDefault(x => !x.PresentationCancelled && FusedMotionOf.ContainsKey(x.Pattern));
                if (Model.Phase == Phase.Complete) { blue = "p3-defeat"; blueAge = now - completedAt; }
                else if (now < BlueMotionStunUntil) { blue = "p3-stun"; blueAge = now - blueStunAt; }
                else if (c != null && SketchMotionAge(c, false) < MotionGameSeconds(FusedMotionOf[c.Pattern])) { blue = FusedMotionOf[c.Pattern]; blueAge = SketchMotionAge(c, false); }
                // Resting: rise, drift away (RestMovement) and land, fitted into the rest time.
                else if (resting) { blue = "p3-hover"; blueAge = (now - restStartedAt) * MotionGameSeconds("p3-hover") / (float)Tuning.Rest; }
            }
            ShowMotion(Blue, blue, blueAge); BlueMotion = blue;
        }

        /// <summary>Phase1 layer shots: Rose's triple shot (stretched to the 0.7/1.7/2.7 s volley).</summary>
        void ShowRoseTripleShot(double age)
        {
            LoadBossMotions();
            ShowMotion(Rose, "rose-triple-shot", age); RoseMotion = "rose-triple-shot";
        }

        /// <summary>Phase1 machine burst: Blue shows from the cast start so the ink burst wind-up is seen (its slam lands on
        /// MachineBurstTell through the motion's cue keys); she still leaves MachineBurstVisibleSeconds after the burst (data).</summary>
        static double MachineBurstPoseSeconds => Tuning.MachineBurstTell + TemporaryPresentation.MachineBurstVisibleSeconds;

        /// <summary>After the fight ends the encounter stops stepping; only the fused Blue defeat pose keeps playing.</summary>
        void StepDefeatPose()
        {
            LoadBossMotions();
            if (float.IsNaN(completedAt)) { completedAt = arena.Now; }
            Rose.PresentationVisible = false;
            Blue.PresentationVisible = DefeatPoseVisible;
            if (Blue.PresentationVisible) { ShowMotion(Blue, "p3-defeat", arena.Now - completedAt); BlueMotion = "p3-defeat"; }
        }

        /// <summary>The defeat pose stays on screen for its own length plus a short hold after the fight ends.</summary>
        bool DefeatPoseVisible => !float.IsNaN(completedAt) && arena.Now - completedAt < (bossMotions.ContainsKey("p3-defeat") ? MotionGameSeconds("p3-defeat") : 0) + DefeatHoldSeconds;
        const float DefeatHoldSeconds = 1f;

        /// <summary>True from the fight's end until the defeat pose and its hold are over; the boss reward waits for it.</summary>
        public bool DefeatPresentationPlaying => Dead && (float.IsNaN(completedAt) || DefeatPoseVisible);
    }
}
