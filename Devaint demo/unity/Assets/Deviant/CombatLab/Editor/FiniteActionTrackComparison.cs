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
    public sealed class FiniteActionTrackVersion
    {
        public IReadOnlyList<ActionTrackDefinition> Actions { get; }
        public LocomotionProfile Movement { get; }
        public FiniteActionTrackVersion(IEnumerable<ActionTrackDefinition> actions, LocomotionProfile movement)
        {
            var values = actions?.ToArray();
            if (values == null || values.Length != 2 || values.Any(value => value == null) ||
                values.Count(value => value.Id == ActionTrackDefinition.MovementId) != 1 || values.Count(value => value.Id == ActionTrackDefinition.FollowUpId) != 1)
                throw new ArgumentException("Capture the complete two-action finite version.");
            Actions = Array.AsReadOnly(values); Movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }
        public static FiniteActionTrackVersion Capture(CombatArena arena) => new FiniteActionTrackVersion(new[] {
            arena.GetActionTracks(ActionTrackDefinition.MovementId), arena.GetActionTracks(ActionTrackDefinition.FollowUpId)
        }, arena.Movement);
    }
    public sealed class FiniteActionTrackMetric
    {
        public string Segment { get; }
        public string DefinitionVersion { get; }
        public float HealthLost { get; }
        public int ReceivedHits { get; }
        public int FirstHitTick { get; }
        public float Travel { get; }
        public CombatInputRecording Inputs { get; }
        internal FiniteActionTrackMetric(string segment, string definition, float lost, int hits, int first, float travel, CombatInputRecording inputs)
        { Segment = segment; DefinitionVersion = definition; HealthLost = lost; ReceivedHits = hits; FirstHitTick = first; Travel = travel; Inputs = inputs; }
    }
    // Five explicit uses of the existing flat/wall lab, not another scenario VM.
    // Native simulation owns all hits, movement, resources and cancellation.
    public static class FiniteActionTrackComparison
    {
        public const string ScenarioId = "lab-finite-sword-v1";
        public const float Step = .02f;
        public const int Ticks = 60;
        static readonly string[] Segments = { "movement-flat", "movement-wall", "follow-up", "follow-up-dash-cancel", "follow-up-switch" };
        public static (IReadOnlyList<FiniteActionTrackMetric> A, IReadOnlyList<FiniteActionTrackMetric> B) Run(CombatArena arena, FiniteActionTrackVersion a, FiniteActionTrackVersion b)
        {
            if (!Application.isPlaying || !arena || !arena.Player || arena.CompositionProfile != CombatCompositionProfile.Lab ||
                arena.gameObject.scene.path != "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity" || arena.enemies.Count != 3 ||
                UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Run this bounded preset in the single initialized CombatV2Arena scene.");
            if (arena.ManualSimulationActive || arena.Player.InputBlocked || arena.Controls.Current.SimulationPaused)
                throw new InvalidOperationException("Release the existing manual driver, input block and pause before comparing.");
            if (a == null || b == null) throw new ArgumentException("Capture both finite A and B versions first.");
            var prior = FiniteActionTrackVersion.Capture(arena);
            foreach (var version in new[] { a, b }) foreach (var definition in version.Actions)
                if (prior.Actions.Single(value => value.Id == definition.Id).SourceVersion != definition.SourceVersion)
                    throw new ArgumentException("Captured finite metadata no longer matches this combat runtime.");
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
        static void Apply(CombatArena arena, FiniteActionTrackVersion version)
        {
            if (!arena.TryApplyActionTracks(version.Actions, false, out var error) || !arena.TryApplyMovementAndReset(version.Movement.ToData(), out error))
                throw new InvalidOperationException(error);
        }
        static IReadOnlyList<FiniteActionTrackMetric> RunVersion(CombatArena arena, CombatArena.ManualSimulation driver,
            FiniteActionTrackVersion version, IReadOnlyList<FiniteActionTrackMetric> replay)
        {
            Apply(arena, version); var result = new FiniteActionTrackMetric[Segments.Length];
            int sword = Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword");
            for (int segment = 0; segment < Segments.Length; segment++)
            {
                bool movement = segment < 2; string id = movement ? ActionTrackDefinition.MovementId : ActionTrackDefinition.FollowUpId;
                int special = Array.FindIndex(arena.catalog.forms[sword].specials, skill => skill.id == id);
                driver.Reset(); arena.Player.selected = 0;
                foreach (var weapon in arena.Player.weapons) weapon.Select(sword, special);
                float start = segment == 1 ? 11.8f : 0;
                arena.Player.Position = new Vector2(start, 0); arena.Player.SyncFacing(1);
                foreach (var enemy in arena.enemies) enemy.Position = new Vector2(35, 0);
                var target = arena.enemies[0]; target.Position = new Vector2(segment == 1 ? 12.4f : movement ? 3 : 2, 0);
                Physics2D.SyncTransforms();
                var read = arena.ReadModel;
                if (!read.TryGetSession(out var session) || !arena.TryBindActor(target, out var targetHandle) ||
                    !read.TryReadPlayer(session, out var playerStart) || !read.TryReadActor(targetHandle, out var targetStart))
                    throw new InvalidOperationException("The reset preset could not bind its read-only actors.");
                int first = -1; ActorReadSnapshot playerEnd = playerStart, targetEnd = targetStart;
                using (var recorder = replay == null ? new CombatInputRecorder(ScenarioId + "/" + Segments[segment], Step, Ticks) : null)
                {
                    for (int tick = 0; tick < Ticks; tick++)
                    {
                        var input = replay == null ? new CombatInput(0, tick == 0 ? CombatButtons.Special : segment == 3 && tick == 13 ? CombatButtons.Dash :
                            segment == 4 && tick == 15 ? CombatButtons.Swap : CombatButtons.None) : replay[segment].Inputs.Inputs[tick];
                        driver.Step(Step, input); recorder?.Record(input);
                        if (!read.TryReadPlayer(session, out playerEnd) || !read.TryReadActor(targetHandle, out targetEnd))
                            throw new InvalidOperationException("Preset read handles became stale while simulating.");
                        if (first < 0 && targetEnd.Vitals.ReceivedHits > targetStart.Vitals.ReceivedHits) first = tick;
                    }
                    result[segment] = new FiniteActionTrackMetric(Segments[segment], version.Actions.Single(value => value.Id == id).ContentHash,
                        targetStart.Vitals.Health - targetEnd.Vitals.Health, targetEnd.Vitals.ReceivedHits - targetStart.Vitals.ReceivedHits,
                        first, playerEnd.Kinematics.X - playerStart.Kinematics.X, replay == null ? recorder.Complete() : replay[segment].Inputs);
                }
            }
            return Array.AsReadOnly(result);
        }
    }
}
