using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class PlayerActionStartExecutionTests
    {
        sealed class State
        {
            public readonly List<string> Calls = new List<string>();
            public FormData Form;
            public int Axis = -1, SpecialIndex;
            public float Rate = 1.3f;
            public bool Ground = true, ReturnPending;
            public BasicMeleeDefinition Basic;
            public ActionTrackDefinition Tracks;
            public DirectMeleeSpecialDefinition Direct;
            public ChargedMeleeDefinition Charged;
            public SkillData Payload;
            public string Version;
            public MeleeActionDefinition Timeline;
            public MeleeFrameDefinition Frame;
            public CombatCommandKey Command;
            public float StartAt;
            public Guid Execution = Guid.NewGuid();
            public Action<string> Callback;
            public void Call(string name) { Calls.Add(name); Callback?.Invoke(name); }
        }
        struct Host : IPlayerActionStartHost
        {
            public State State;
            public int Axis => State.Axis;
            public void SyncFacing(int direction) => State.Call("face:" + direction);
            public void BeginBasicCombo(int index) => State.Call("combo:" + index);
            public FormData Form => State.Form;
            public int SpecialIndex => State.SpecialIndex;
            public float WeaponSpeed => State.Rate;
            public BasicMeleeDefinition BasicMelee(string id, int index) { State.Call("basic:" + id + ":" + index); return State.Basic; }
            public RangedActionDefinition RangedBasic(string form, int index) => null;
            public RangedActionDefinition RangedSpecial(string id) => null;
            public SkillData Special => State.Form.specials[State.SpecialIndex];
            public ActionTrackDefinition ActionTracks(string id) { State.Call("tracks:" + id); return State.Tracks; }
            public DirectMeleeSpecialDefinition DirectSpecial(string id) { State.Call("direct:" + id); return State.Direct; }
            public ChargedMeleeDefinition ChargedMelee(string id) { State.Call("charged:" + id); return State.Charged; }
            public AuthoredSkillDefinition AuthoredSpecial(string id) => null;
            public bool ShurikenReturnPending() { State.Call("return"); return State.ReturnPending; }
            public bool ProbeGround() { State.Call("ground"); return State.Ground; }
            public void RecordUnavailableSpecial() => State.Call("unavailable");
            public void ClearCombo() => State.Call("clear-combo");
            public void ClearJumpBuffer() => State.Call("clear-jump");
            public void ClearBufferedJump() => State.Call("clear-buffered");
            public Guid CreateBasic(SkillData data, CombatCommandKey command, float startAt, int index, BasicMeleeDefinition definition, MeleeActionDefinition timeline, RangedActionDefinition ranged)
            { State.Call("create-basic"); Capture(data, command, startAt, definition?.ContentHash, timeline); return State.Execution; }
            public Guid CreateSpecial(SkillData data, CombatCommandKey command, float startAt, string version, MeleeActionDefinition timeline, DirectMeleeSpecialDefinition direct, ChargedMeleeDefinition charged, ActionTrackDefinition tracks, AuthoredSkillDefinition authored, RangedActionDefinition ranged)
            { State.Call("create-special"); Capture(data, command, startAt, version, timeline); return State.Execution; }
            void Capture(SkillData data, CombatCommandKey command, float startAt, string version, MeleeActionDefinition timeline)
            { State.Payload = data; State.Command = command; State.StartAt = startAt; State.Version = version; State.Timeline = timeline; }
        }
        static State Fixture(string kind = null) => new State {
            Form = new FormData { id = "fixture", name = "Fixture", inputSpeed = 5, basics = new[] { new BasicData { kind = kind, impact = .21f, duration = .65f, active = .07f, damage = 12,
                box = new BoxData { width = 3, height = 2, x = 1, y = 1 }, bomb = kind == "bomb" ? new BombData { speed = 3 } : null, ranged = kind == "ranged" ? new RangedData { type = "shot" } : null } },
                specials = new[] { new SkillData { id = "fixture_special", vertical = "normal", facing = "windup_input_else_facing", hits = new[] { new HitData { targets = "nearest", box = new BoxData() } } } } }
        };
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [TestCase(null)] [TestCase("ranged")] [TestCase("bomb")]
        public void BasicPayloadRetainsLegacyScalingReferencesAndNativeCreateReceipt(string kind)
        {
            var state = Fixture(kind); var host = new Host { State = state }; var basic = state.Form.basics[0];
            var command = new CombatCommandKey(default, 7, CombatCommandKind.Basic);
            var result = PlayerActionStartExecution.BeginBasic(command, 0, .125f, ref host);
            Assert.That(state.Calls, Is.EqualTo(new[] { "face:-1", "combo:0", "basic:fixture:0", "create-basic" }));
            Assert.That(result.Started && result.OutcomeRecorded, Is.True); Assert.That(result.ExecutionId, Is.EqualTo(state.Execution));
            Assert.That(state.Command, Is.EqualTo(command)); Assert.That(state.StartAt, Is.EqualTo(.125f));
            var data = state.Payload; Assert.That(data.id, Is.EqualTo("fixture_basic_0")); Assert.That(data.hits[0].box, Is.SameAs(basic.box)); Assert.That(data.ranged, Is.SameAs(basic.ranged)); Assert.That(data.bomb, Is.SameAs(basic.bomb));
            Assert.That(Bits(data.activation), Is.EqualTo(Bits(basic.impact / state.Rate))); Assert.That(Bits(data.end), Is.EqualTo(Bits(basic.duration / state.Rate)));
            Assert.That(Bits(data.basicLungeStart), Is.EqualTo(Bits(Mathf.Max(0, basic.impact - .04f) / state.Rate))); Assert.That(Bits(data.basicLungeDuration), Is.EqualTo(Bits(.08f / state.Rate)));
            Assert.That(data.basicLungeDistance, Is.EqualTo(kind == "ranged" ? 0 : .65f)); Assert.That(data.hits[0].anchor, Is.EqualTo(kind == "ranged" || kind == "bomb" ? "ranged_projectile" : "actor"));
        }
        [Test] public void ValidatedMeleeSelectionPreservesItsSourceAndTimeline()
        {
            var state = Fixture(); Assert.That(BasicMeleeDefinition.TryCreate("fixture_basic_0", state.Form.basics[0], out state.Basic, out var error), Is.True, error);
            state.Form.basics[0].damage = 999; var host = new Host { State = state }; PlayerActionStartExecution.BeginBasic(default, 0, -1, ref host);
            Assert.That(state.Payload.hits[0].damage, Is.EqualTo(12)); Assert.That(state.Version, Is.EqualTo(state.Basic.ContentHash)); Assert.That(state.Timeline, Is.Not.Null);
            Assert.That(Bits(state.Payload.basicLungeStart), Is.EqualTo(Bits(state.Timeline.AdvanceStart)));
        }
        [TestCase(1f)] [TestCase(1.5f)]
        public void AuthoredUnarmedStepAndHitUseTheSameAttackRate(float rate)
        {
            var state = Fixture("melee"); state.Rate = rate;
            state.Form.id = "unarmed";
            state.Form.basics[0] = new BasicData { kind = "melee", damage = 8, duration = .36f, impact = .12f, active = .06f,
                advance = new BasicAdvanceData { distance = .2f, start = .08f, duration = .06f },
                box = new BoxData { width = 1.40625f, height = 1.5f, x = .603125f, y = 1.25f } };
            var host = new Host { State = state };
            PlayerActionStartExecution.BeginBasic(default, 0, 0, ref host);
            Assert.That(state.Payload.hits[0].damage, Is.EqualTo(8));
            Assert.That(state.Payload.hits[0].anchor, Is.EqualTo("actor"));
            Assert.That(state.Payload.activation, Is.EqualTo(.12f / rate));
            Assert.That(state.Payload.end, Is.EqualTo(.36f / rate));
            Assert.That(state.Payload.hits[0].active, Is.EqualTo(.06f / rate));
            Assert.That(state.Payload.basicLungeDistance, Is.EqualTo(.2f));
            Assert.That(state.Payload.basicLungeStart, Is.EqualTo(.08f / rate));
            Assert.That(state.Payload.basicLungeDuration, Is.EqualTo(.06f / rate));
        }
        [TestCase("bomb")] [TestCase("ranged")]
        public void MissingOrEmptyDeserializedAdvanceRetainsExistingWeaponMovement(string kind)
        {
            foreach (string field in new[] { "", ",\"advance\":{}" })
            {
                var state = Fixture(kind);
                state.Form.basics[0] = JsonUtility.FromJson<BasicData>("{\"kind\":\""+kind+"\",\"duration\":0.65,\"impact\":0.21"+field+"}");
                var host = new Host { State = state };
                PlayerActionStartExecution.BeginBasic(default, 0, 0, ref host);
                Assert.That(state.Payload.basicLungeDistance, Is.EqualTo(kind == "ranged" ? 0 : .65f));
                Assert.That(state.Payload.basicLungeStart, Is.EqualTo(Mathf.Max(0, .21f-.04f)/state.Rate));
                Assert.That(state.Payload.basicLungeDuration, Is.EqualTo(.08f/state.Rate));
            }
        }
        [Test] public void BasicReadsLiveSelectionAfterFacingAndLeavesPartialComboOnCreateException()
        {
            var state = Fixture(); state.Callback = name => { if (name == "face:-1") state.Form.id = "changed"; if (name == "create-basic") throw new InvalidOperationException("create failed"); };
            var host = new Host { State = state }; Assert.Throws<InvalidOperationException>(() => PlayerActionStartExecution.BeginBasic(default, 0, -1, ref host));
            Assert.That(state.Calls, Does.Contain("combo:0")); Assert.That(state.Calls, Does.Contain("basic:changed:0")); Assert.That(state.Payload, Is.Null);
        }
        [TestCase("return", CombatCommandReason.ReturnPending)] [TestCase("unavailable", CombatCommandReason.Unavailable)]
        [TestCase("ground", CombatCommandReason.GroundRequired)]
        public void SpecialAdmissionRetainsOrderedRejectionBeforeClearingOrCreating(string failure, CombatCommandReason reason)
        {
            var state = Fixture(); var source = state.Form.specials[0];
            if (failure == "return") { source.ranged = new RangedData { returnDelay = 1 }; state.ReturnPending = true; }
            if (failure == "unavailable") source.unavailable = true;
            if (failure == "ground") { source.vertical = "grounded_required"; state.Ground = false; }
            var host = new Host { State = state }; var result = PlayerActionStartExecution.BeginSpecial(default, -1, ref host);
            Assert.That(result.Started, Is.False); Assert.That(result.Reason, Is.EqualTo(reason)); Assert.That(state.Calls, Does.Not.Contain("clear-combo").And.Not.Contain("create-special"));
            Assert.That(state.Calls.Contains("face:-1"), Is.False);
        }
        // A nearest-target special starts with no enemy in reach and whiffs; admission never asks for a target.
        [Test] public void NearestTargetSpecialStartsWithoutATarget()
        {
            var state = Fixture(); var host = new Host { State = state }; var result = PlayerActionStartExecution.BeginSpecial(default, -1, ref host);
            Assert.That(result.Started, Is.True); Assert.That(state.Calls, Does.Contain("create-special")); Assert.That(state.Calls, Does.Not.Contain("target"));
        }
        [Test] public void FiniteDefinitionOwnsEligibilityFacingAndVersionDespiteLegacyPayloadEdits()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var source = catalog.forms.SelectMany(form => form.specials).First(skill => !ActionTrackDocument.IsAbsent(skill.actionTracks));
            var state = Fixture(); state.Form.specials[0] = source;
            state.Tracks = new ActionTrackDefinition(source); source.unavailable = true; source.vertical = "grounded_required"; source.facing = "changed";
            var host = new Host { State = state }; var result = PlayerActionStartExecution.BeginSpecial(default, .125f, ref host);
            Assert.That(result.Started, Is.True); Assert.That(state.Version, Is.EqualTo(state.Tracks.ContentHash)); Assert.That(state.Calls, Does.Not.Contain("unavailable").And.Not.Contain("ground").And.Not.Contain("target"));
            Assert.That(state.Calls.SkipWhile(call => call != "clear-combo").Take(3), Is.EqualTo(new[] { "clear-combo", "clear-jump", "clear-buffered" }));
        }
        [Test] public void NewExecutionTypesRemainUnityIndependent()
        {
            Assert.That(typeof(PlayerActionStartExecution).Assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("UnityEngine") || reference.Name.StartsWith("UnityEditor") || reference.Name == "Deviant.Combat"), Is.False);
        }
    }
}
