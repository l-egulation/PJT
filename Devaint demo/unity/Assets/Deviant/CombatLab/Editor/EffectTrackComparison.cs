using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.CombatLab
{
    public sealed class EffectTrackVersion
    {
        public IReadOnlyList<ActionTrackDefinition> Actions { get; }
        public LocomotionProfile Movement { get; }
        public EffectTrackVersion(IEnumerable<ActionTrackDefinition> actions, LocomotionProfile movement)
        {
            var values = actions?.ToArray();
            if (values == null || values.Length != 2 || values.Any(value => value == null) ||
                values.Count(value => value.Id == ActionTrackDefinition.WaveId) != 1 || values.Count(value => value.Id == ActionTrackDefinition.GuardId) != 1)
                throw new ArgumentException("Capture the complete wave/guard comparison pair.");
            Actions = Array.AsReadOnly(values); Movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }
        public static EffectTrackVersion Capture(CombatArena arena) => new EffectTrackVersion(new[] {
            arena.GetActionTracks(ActionTrackDefinition.WaveId), arena.GetActionTracks(ActionTrackDefinition.GuardId)
        }, arena.Movement);
    }
    public sealed class EffectTrackMetric
    {
        public string Segment { get; }
        public string DefinitionVersion { get; }
        public float TargetHealthLost { get; }
        public int TargetHits { get; }
        public float PlayerHealthLost { get; }
        public float Shield { get; }
        public float GuardUntil { get; }
        public CombatInputRecording Inputs { get; }
        internal EffectTrackMetric(string segment, string definition, float targetLost, int targetHits, float playerLost, float shield, float guardUntil, CombatInputRecording inputs)
        { Segment = segment; DefinitionVersion = definition; TargetHealthLost = targetLost; TargetHits = targetHits; PlayerHealthLost = playerLost; Shield = shield; GuardUntil = guardUntil; Inputs = inputs; }
    }
    // Bounded uses of the existing Lab and Native damage path. No alternate
    // defense math, wave simulation, scenario interpreter or product content.
    public static class EffectTrackComparison
    {
        public const string ScenarioId = "lab-wave-guard-v1";
        public const float Step = .02f;
        public const int Ticks = 100;
        static readonly string[] Segments = { "wave-independent", "guard-normal", "guard-fall-bypass" };
        public static (IReadOnlyList<EffectTrackMetric> A, IReadOnlyList<EffectTrackMetric> B) Run(CombatArena arena, EffectTrackVersion a, EffectTrackVersion b)
        {
            if (!Application.isPlaying || !arena || !arena.Player || arena.CompositionProfile != CombatCompositionProfile.Lab ||
                arena.gameObject.scene.path != "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity" || arena.enemies.Count != 3 ||
                UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Run this bounded effect preset in the single initialized CombatV2Arena scene.");
            if (arena.ManualSimulationActive || arena.Player.InputBlocked || arena.Controls.Current.SimulationPaused)
                throw new InvalidOperationException("Release the existing manual driver, input block and pause before comparing.");
            if (a == null || b == null) throw new ArgumentException("Capture both wave/guard versions first.");
            var prior = EffectTrackVersion.Capture(arena);
            foreach (var version in new[] { a, b }) foreach (var definition in version.Actions)
                if (prior.Actions.Single(value => value.Id == definition.Id).SourceVersion != definition.SourceVersion)
                    throw new ArgumentException("Captured effect metadata no longer matches this combat runtime.");
            var player = arena.Player; int selected = player.selected;
            int[] forms = player.weapons.Select(weapon => weapon.FormIndex).ToArray(), specials = player.weapons.Select(weapon => weapon.SpecialIndex).ToArray();
            int[][] slots = player.weapons.Select(weapon => (int[])weapon.StatSlots.Clone()).ToArray();
            bool[] aggressive = arena.enemies.Select(enemy => enemy.aggressive).ToArray();
            using (var driver = arena.AcquireManualSimulation())
            {
                try
                {
                    foreach (var enemy in arena.enemies) enemy.aggressive = false;
                    foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                    var first = RunVersion(arena, driver, a, null);
                    return (first, RunVersion(arena, driver, b, first));
                }
                finally
                {
                    player.selected = selected;
                    for (int index = 0; index < player.weapons.Length; index++) {
                        player.weapons[index].Select(forms[index], specials[index]); player.weapons[index].StatSlots.CopyFrom(slots[index]);
                    }
                    for (int index = 0; index < arena.enemies.Count; index++) arena.enemies[index].aggressive = aggressive[index];
                    Apply(arena, prior); driver.Reset();
                }
            }
        }
        static void Apply(CombatArena arena, EffectTrackVersion version)
        {
            if (!arena.TryApplyActionTracks(version.Actions, false, out var error) || !arena.TryApplyMovementAndReset(version.Movement.ToData(), out error))
                throw new InvalidOperationException(error);
        }
        static IReadOnlyList<EffectTrackMetric> RunVersion(CombatArena arena, CombatArena.ManualSimulation driver,
            EffectTrackVersion version, IReadOnlyList<EffectTrackMetric> replay)
        {
            Apply(arena, version); var result = new EffectTrackMetric[Segments.Length];
            for (int segment = 0; segment < Segments.Length; segment++)
            {
                bool wave = segment == 0; string id = wave ? ActionTrackDefinition.WaveId : ActionTrackDefinition.GuardId;
                int form = Array.FindIndex(arena.catalog.forms, value => value.id == (wave ? "base_sword" : "shield"));
                int special = Array.FindIndex(arena.catalog.forms[form].specials, skill => skill.id == id);
                driver.Reset(); arena.Player.selected = 0;
                foreach (var weapon in arena.Player.weapons) weapon.Select(form, special);
                arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1);
                foreach (var enemy in arena.enemies) enemy.Position = new Vector2(35, 0);
                var target = arena.enemies[0]; target.Position = new Vector2(wave ? 5 : 30, 0); Physics2D.SyncTransforms();
                var read = arena.ReadModel;
                if (!read.TryGetSession(out var session) || !arena.TryBindActor(target, out var targetHandle) ||
                    !read.TryReadPlayer(session, out var playerStart) || !read.TryReadActor(targetHandle, out var targetStart))
                    throw new InvalidOperationException("The reset effect preset could not bind read-only actors.");
                ActorReadSnapshot playerEnd = playerStart, targetEnd = targetStart;
                using (var recorder = replay == null ? new CombatInputRecorder(ScenarioId + "/" + Segments[segment], Step, Ticks) : null)
                {
                    for (int tick = 0; tick < Ticks; tick++)
                    {
                        var input = replay == null ? new CombatInput(0, tick == 0 ? CombatButtons.Special : tick == 13 ? CombatButtons.Dash : tick == 15 ? CombatButtons.Swap : CombatButtons.None) : replay[segment].Inputs.Inputs[tick];
                        driver.Step(Step, input); recorder?.Record(input);
                        if (!wave && tick == 40) {
                            if (segment == 1) arena.Player.Receive(100, target, -1, false, null);
                            else arena.Player.Receive(10, target, -1, false, null, true);
                        }
                        if (!read.TryReadPlayer(session, out playerEnd) || !read.TryReadActor(targetHandle, out targetEnd))
                            throw new InvalidOperationException("Effect preset read handles became stale.");
                    }
                    result[segment] = new EffectTrackMetric(Segments[segment], version.Actions.Single(value => value.Id == id).ContentHash,
                        targetStart.Vitals.Health - targetEnd.Vitals.Health, targetEnd.Vitals.ReceivedHits - targetStart.Vitals.ReceivedHits,
                        playerStart.Vitals.Health - playerEnd.Vitals.Health, playerEnd.Vitals.Shield, playerEnd.Vitals.GuardUntil,
                        replay == null ? recorder.Complete() : replay[segment].Inputs);
                }
            }
            return Array.AsReadOnly(result);
        }
    }
}
