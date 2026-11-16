using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.CombatLab
{
    public sealed class SwordDashVersion
    {
        public BasicMeleeDefinition Sword { get; }
        public LocomotionProfile Movement { get; }
        public MeleeFrameDefinition FrameTiming { get; }
        public BasicMeleeDefinition FixedSword => FrameTiming?.SourceAction ?? Sword;
        public SwordDashVersion(BasicMeleeDefinition sword, LocomotionProfile movement, MeleeFrameDefinition frameTiming = null)
        {
            Sword = sword ?? throw new ArgumentNullException(nameof(sword));
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));
            if (sword.Id != "base_sword_basic_0") throw new ArgumentException("Expected sword first hit.");
            if (frameTiming != null && frameTiming.Action.ContentHash != sword.ContentHash) throw new ArgumentException("Frame timing must own the captured effective sword definition.");
            FrameTiming = frameTiming;
        }
        public static SwordDashVersion Capture(CombatArena arena)
        {
            return new SwordDashVersion(arena.BasicSwordFirst, arena.Movement, arena.FrameSwordTiming);
        }
    }

    public sealed class SwordDashMetrics
    {
        public SwordDashVersion Version { get; }
        public float HealthLost { get; }
        public int FirstHitTick { get; }
        public float Travel { get; }
        public float WallTravel { get; }
        public CombatInputRecording AttackInputs { get; }
        public CombatInputRecording WallInputs { get; }
        internal SwordDashMetrics(SwordDashVersion version, float healthLost, int firstHitTick, float travel, float wallTravel,
            CombatInputRecording attackInputs, CombatInputRecording wallInputs)
        { Version = version; HealthLost = healthLost; FirstHitTick = firstHitTick; Travel = travel; WallTravel = wallTravel; AttackInputs = attackInputs; WallInputs = wallInputs; }
    }

    // Isolated fixture, not product content or an alternative combat implementation.
    // Each pair uses the same built-in lab geometry, empty stat slots and input ticks.
    public static class SwordDashComparison
    {
        public const string ScenarioId = "lab-sword-dash-wall-v1";
        public const float Step = .02f;
        public static (SwordDashMetrics A, SwordDashMetrics B) Run(CombatArena arena, SwordDashVersion a, SwordDashVersion b, SwordDashInputSet inputs = null)
        {
            if (!Application.isPlaying || !arena || !arena.Player || arena.CompositionProfile != CombatCompositionProfile.Lab ||
                arena.gameObject.scene.path != "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity" || arena.enemies.Count != 3 ||
                UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Run this fixture in an initialized CombatV2Arena scene with exactly one arena.");
            if (arena.ManualSimulationActive) throw new InvalidOperationException("Release the existing manual simulation owner before comparing.");
            if (a == null || b == null) throw new ArgumentNullException("Capture both A and B first.");
            if (arena.Player.InputBlocked) throw new InvalidOperationException("Release the existing input block before comparing.");
            if (arena.Controls.Current.SimulationPaused) throw new InvalidOperationException("Release the existing simulation pause before comparing.");
            int swordIndex = Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword");
            if (swordIndex < 0) throw new InvalidOperationException("Sword content missing.");
            var prior = SwordDashVersion.Capture(arena);
            ValidateFrameBinding(arena, a); ValidateFrameBinding(arena, b); ValidateFrameBinding(arena, prior);
            var player = arena.Player;
            int selected = player.selected;
            var forms = player.weapons.Select(w => w.FormIndex).ToArray();
            var specials = player.weapons.Select(w => w.SpecialIndex).ToArray();
            var slots = player.weapons.Select(w => (int[])w.StatSlots.Clone()).ToArray();
            var aggressive = arena.enemies.Select(e => e.aggressive).ToArray();
            using (var driver = arena.AcquireManualSimulation())
            {
                try
                {
                    player.selected = 0;
                    foreach (var weapon in player.weapons)
                    { weapon.Select(swordIndex, 0); weapon.StatSlots.Clear(); }
                    foreach (var enemy in arena.enemies) enemy.aggressive = false;
                    var first = inputs == null ? RunVersion(arena, driver, a) : RunVersion(arena, driver, a, inputs.Attack, inputs.Wall);
                    return (first, RunVersion(arena, driver, b, first.AttackInputs, first.WallInputs));
                }
                finally
                {
                    player.selected = selected;
                    for (int i = 0; i < player.weapons.Length; i++)
                    {
                        var weapon = player.weapons[i]; weapon.Select(forms[i], specials[i]);
                        weapon.StatSlots.CopyFrom(slots[i]);
                    }
                    for (int i = 0; i < arena.enemies.Count; i++) arena.enemies[i].aggressive = aggressive[i];
                    Apply(arena, prior);
                    driver.Reset(); // Reset, not restoration of in-flight actions or prior HP.
                }
            }
        }
        static void ValidateFrameBinding(CombatArena arena, SwordDashVersion version)
        {
            if (version.FrameTiming == null) return;
            if (!arena.TryGetBasicSwordMotion(out var motion, out var error)) throw new InvalidOperationException(error);
            if (motion.ContentHash != version.FrameTiming.SourceMotion.ContentHash) throw new InvalidOperationException("Captured frame timing requires its original fixed motion binding.");
        }
        static void Apply(CombatArena arena, SwordDashVersion version)
        {
            if (!arena.TryRestoreSwordFixedTiming(false, out var error) ||
                !arena.TryApplyBasicSwordFirst(version.FixedSword.ToData(), false, out error) ||
                version.FrameTiming != null && !arena.TryApplySwordFrameTiming(version.FrameTiming, false, out error) ||
                !arena.TryApplyMovementAndReset(version.Movement.ToData(), out error)) throw new InvalidOperationException(error);
        }
        static SwordDashMetrics RunVersion(CombatArena arena, CombatArena.ManualSimulation driver, SwordDashVersion version,
            CombatInputRecording attackInputs = null, CombatInputRecording wallInputs = null)
        {
            if ((attackInputs == null) != (wallInputs == null)) throw new ArgumentException("Both scenario segment recordings are required for replay.");
            ValidateRecording(attackInputs, "attack", SwordDashInputSet.AttackTicks); ValidateRecording(wallInputs, "wall", SwordDashInputSet.WallTicks);
            var attackRecorder = attackInputs == null ? new CombatInputRecorder(ScenarioId + "/attack", Step, SwordDashInputSet.AttackTicks) : null;
            var wallRecorder = wallInputs == null ? new CombatInputRecorder(ScenarioId + "/wall", Step, SwordDashInputSet.WallTicks) : null;
            Apply(arena, version); driver.Reset(); Place(arena, 0);
            var target = arena.enemies[0]; target.Position = new Vector2(3, 0); Physics2D.SyncTransforms();
            var reads = arena.ReadModel;
            if (!reads.TryGetSession(out var session) || !arena.TryBindActor(target, out var targetHandle) ||
                !reads.TryReadActor(targetHandle, out var targetRead)) throw new InvalidOperationException("Comparison actors could not bind to the reset combat session.");
            float initialHealth = targetRead.Vitals.Health; int hit = -1;
            for (int tick = 0; tick < SwordDashInputSet.AttackTicks; tick++)
            {
                var input = attackInputs == null ? new CombatInput(0, tick == 0 ? CombatButtons.Basic : tick == 30 ? CombatButtons.Dash : CombatButtons.None) : attackInputs.Inputs[tick];
                driver.Step(Step, input);
                attackRecorder?.Record(input); // A rejected/faulted step is never published as a recorded tick.
                if (!reads.TryReadActor(targetHandle, out targetRead)) throw new InvalidOperationException("Comparison target left its combat session.");
                if (hit < 0 && targetRead.Vitals.ReceivedHits > 0) hit = tick;
            }
            if (!reads.TryReadPlayer(session, out var playerRead)) throw new InvalidOperationException("Comparison player left its combat session.");
            float lost = initialHealth - targetRead.Vitals.Health, travel = playerRead.Kinematics.X;
            driver.Reset(); Place(arena, 11.8f);
            if (!reads.TryGetSession(out session)) throw new InvalidOperationException("Comparison wall segment could not bind to its reset combat session.");
            for (int tick = 0; tick < SwordDashInputSet.WallTicks; tick++)
            {
                var input = wallInputs == null ? new CombatInput(0, tick == 0 ? CombatButtons.Dash : CombatButtons.None) : wallInputs.Inputs[tick];
                driver.Step(Step, input); wallRecorder?.Record(input);
            }
            if (!reads.TryReadPlayer(session, out playerRead)) throw new InvalidOperationException("Comparison player left its wall-segment combat session.");
            return new SwordDashMetrics(version, lost, hit, travel, playerRead.Kinematics.X - 11.8f,
                attackInputs ?? attackRecorder.Complete(), wallInputs ?? wallRecorder.Complete());
        }
        static void ValidateRecording(CombatInputRecording recording, string segment, int count)
        {
            if (recording == null) return;
            SwordDashInputSet.Validate(recording, segment, count);
        }
        static void Place(CombatArena arena, float x)
        {
            arena.Player.Position = new Vector2(x, 0); arena.Player.SyncFacing(1);
            foreach (var enemy in arena.enemies) enemy.Position = new Vector2(35, 0);
            Physics2D.SyncTransforms();
        }
    }
}
