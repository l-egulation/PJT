using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorMotorExecutionTests
    {
        static readonly ActorSurfaceContact[] Empty = Array.Empty<ActorSurfaceContact>();
        sealed class State
        {
            public float Now = 1, X = 2, Y = 3, Width = 1, Height = 2, OffsetX = .25f, OffsetY = 1;
            public float VelocityX = 19, VelocityY = -2, KnockSpeed, KnockUntil, StunUntil;
            public int Facing = -1;
            public bool Grounded = true, WallHit = true;
            public IActorSurfaceQuery Query;
            public readonly List<string> Events = new List<string>();
            public readonly List<ActorBodyFrame> Bodies = new List<ActorBodyFrame>();
            public readonly List<ActorMotionInput> Inputs = new List<ActorMotionInput>();
            public readonly List<float> ExpiredAt = new List<float>(), RefreshedAt = new List<float>();
            public readonly List<float[]> Moves = new List<float[]>();
            public Action Expire, Refresh, Prune;
            public Action<float, float> Moved;
        }
        sealed class Query : IActorSurfaceQuery
        {
            public readonly State State;
            public readonly string Name;
            public readonly List<ActorBoxCast> Requests = new List<ActorBoxCast>();
            public Func<ActorBoxCast, IReadOnlyList<ActorSurfaceContact>> Cast;
            public Query(State state, string name = "world") { State = state; Name = name; }
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request)
            {
                State.Events.Add(Name + ":" + (IsProbe(request) ? "ground" : request.DirectionX != 0 ? "x" : "y"));
                Requests.Add(request); return Cast == null ? Empty : Cast(request);
            }
        }
        struct Host : IActorMotorHost
        {
            public readonly State State;
            public int Syncs;
            public Host(State state) { State = state; Syncs = 0; }
            public float Now { get { State.Events.Add("now"); return State.Now; } }
            public void ExpireShield(float now) { State.Events.Add("shield"); State.ExpiredAt.Add(now); State.Expire?.Invoke(); }
            public void RefreshPlatforms(float now) { State.Events.Add("platforms"); State.RefreshedAt.Add(now); State.Refresh?.Invoke(); }
            public void PruneUnusedPlatforms() { State.Events.Add("prune"); State.Prune?.Invoke(); }
            public ActorBodyFrame BodyFrame
            {
                get
                {
                    State.Events.Add("body");
                    var body = new ActorBodyFrame(State.X, State.Y, State.Width, State.Height, State.OffsetX, State.OffsetY, State.Facing);
                    State.Bodies.Add(body); return body;
                }
            }
            public IActorSurfaceQuery SurfaceQuery { get { State.Events.Add("query"); return State.Query; } }
            public float VelocityY
            {
                get { State.Events.Add("velocity-y"); return State.VelocityY; }
                set { State.Events.Add("stop-y"); State.VelocityY = value; }
            }
            public bool Grounded
            {
                get => State.Grounded;
                set { State.Events.Add(value ? "ground-on" : "ground-off"); State.Grounded = value; }
            }
            public bool WallHit { set { State.Events.Add(value ? "wall-on" : "wall-off"); State.WallHit = value; } }
            public ActorMotionInput ReadMotionInput(float now, float step, float desiredX, bool freezeVertical)
            {
                State.Events.Add("input");
                var input = new ActorMotionInput(now, step, desiredX, State.VelocityY, State.Grounded, freezeVertical,
                    false, false, false, State.KnockSpeed, State.KnockUntil, 0, State.StunUntil, 0, 0, 2, 4);
                State.Inputs.Add(input); return input;
            }
            public void SetVelocity(float x, float y) { State.Events.Add("velocity"); State.VelocityX = x; State.VelocityY = y; }
            public void MovePosition(float deltaX, float deltaY)
            {
                State.Events.Add("move"); State.Moves.Add(new[] { deltaX, deltaY }); State.X += deltaX; State.Y += deltaY;
                State.Moved?.Invoke(deltaX, deltaY);
            }
            public void SyncTransforms() { State.Events.Add("sync"); Syncs++; }
        }
        static bool IsProbe(ActorBoxCast request) => request.Height == .04f;
        static State Create()
        {
            var state = new State(); state.Query = new Query(state); return state;
        }
        static Host Step(State state, float desiredX = 3, bool freezeVertical = false, float step = .5f)
        {
            var host = new Host(state); ActorMotorExecution.Step(step, desiredX, freezeVertical, ref host); return host;
        }
        static ActorSurfaceContact Hit(float distance, float normalX, float normalY, float top = 3)
            => new ActorSurfaceContact(distance, normalX, normalY, top, false, false);

        [Test] public void ExecutionAndHostContractDoNotReferenceEngineSceneOrUi()
        {
            foreach (var type in new[] { typeof(ActorMotorExecution), typeof(IActorMotorHost) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void OneStepOrdersExpiryPlatformsGroundMotionXThenYAndFinalSync()
        {
            var state = Create(); var query = (Query)state.Query; var host = Step(state);
            Assert.That(state.Events, Is.EqualTo(new[]
            {
                "now", "shield", "platforms", "prune", "body", "velocity-y", "query", "world:ground", "ground-off", "input", "velocity", "wall-off",
                "body", "query", "world:x", "move", "body", "query", "world:y", "move", "sync"
            }));
            Assert.That(host.Syncs, Is.EqualTo(1), "The mutable struct host is passed by ref, not boxed or copied.");
            Assert.That(state.X, Is.EqualTo(3.5f)); Assert.That(state.Y, Is.EqualTo(1.5f));
            Assert.That(state.VelocityX, Is.EqualTo(3)); Assert.That(state.VelocityY, Is.EqualTo(-4)); Assert.That(state.Grounded || state.WallHit, Is.False);
            Assert.That(query.Requests.Count, Is.EqualTo(3)); Assert.That(query.Requests[2].X, Is.EqualTo(3.25f));
            Assert.That(state.Moves[0], Is.EqualTo(new[] { 1.5f, 0f })); Assert.That(state.Moves[1], Is.EqualTo(new[] { 0f, -1.5f }));
        }
        [Test] public void PlatformAndGroundCallbacksFeedCurrentBodyAndMotionButDoNotReplaceTheStepClock()
        {
            var state = Create(); var query = (Query)state.Query;
            state.Expire = () => state.Now = 9;
            state.Refresh = () => { state.X = 7; state.Y = 8; state.Width = 2; state.Height = 3; state.Facing = 1; };
            state.Prune = () => state.StunUntil = 1.25f;
            query.Cast = request =>
            {
                if (IsProbe(request))
                {
                    Assert.That(state.Events, Does.Contain("prune")); Assert.That(request.X, Is.EqualTo(7.25f)); Assert.That(request.Y, Is.EqualTo(8.025f));
                    state.VelocityY = -4; state.KnockSpeed = 7; state.KnockUntil = 2; state.Grounded = true;
                }
                return Empty;
            };
            Step(state);
            Assert.That(state.ExpiredAt, Is.EqualTo(new[] { 1f })); Assert.That(state.RefreshedAt, Is.EqualTo(new[] { 1f }));
            Assert.That(state.Events.Count(e => e == "now"), Is.EqualTo(1)); Assert.That(state.Now, Is.EqualTo(9));
            var input = state.Inputs.Single(); Assert.That(input.Now, Is.EqualTo(1)); Assert.That(input.Grounded, Is.False); Assert.That(input.VelocityY, Is.EqualTo(-4));
            Assert.That(state.VelocityX, Is.EqualTo(7)); Assert.That(state.VelocityY, Is.EqualTo(-6));
            Assert.That(state.X, Is.EqualTo(10.5f)); Assert.That(state.Y, Is.EqualTo(5.5f));
        }
        [Test] public void FinalPartialKnockStepStillUsesTheNormalOrderedAxisAndSyncPath()
        {
            var state = Create(); state.Now = 1; state.KnockUntil = 1.01f; state.KnockSpeed = -2.5f; state.StunUntil = 2;
            var host = Step(state, desiredX: 9, freezeVertical: true, step: .02f);
            Assert.That(state.VelocityX, Is.EqualTo(-1.25f).Within(.00001f));
            Assert.That(state.X - 2, Is.EqualTo(-.025f).Within(.000001f)); Assert.That(state.Y, Is.EqualTo(3));
            Assert.That(state.Moves.Count, Is.EqualTo(1)); Assert.That(host.Syncs, Is.EqualTo(1));
            Assert.That(state.Events.IndexOf("platforms"), Is.LessThan(state.Events.IndexOf("world:ground")));
            Assert.That(state.Events.IndexOf("world:ground"), Is.LessThan(state.Events.IndexOf("world:x")));
            state.Now = state.KnockUntil; Step(state, desiredX: 9, freezeVertical: true, step: .02f);
            Assert.That(state.VelocityX, Is.Zero); Assert.That(state.X - 2, Is.EqualTo(-.025f).Within(.000001f));
        }

        [TestCase(false)] [TestCase(true)]
        public void VerticalQueryFailureRetainsAppliedHorizontalMotionAndItsWallResult(bool blocked)
        {
            var state = Create(); var query = (Query)state.Query; var failure = new InvalidOperationException("vertical query");
            query.Cast = request =>
            {
                if (IsProbe(request)) return Empty;
                if (request.DirectionX != 0) return blocked ? new[] { Hit(.502f, -1, 0) } : Empty;
                Assert.That(state.X, Is.EqualTo(blocked ? 2.5f : 3.5f).Within(.000001f)); Assert.That(state.Y, Is.EqualTo(3));
                Assert.That(state.WallHit, Is.EqualTo(blocked)); Assert.That(state.Grounded, Is.False); throw failure;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(failure));
            Assert.That(state.VelocityX, Is.EqualTo(3)); Assert.That(state.VelocityY, Is.EqualTo(-4)); Assert.That(state.Moves.Count, Is.EqualTo(1));
            Assert.That(state.Events, Does.Not.Contain("sync")); Assert.That(state.Events, Does.Not.Contain("stop-y"));
        }
        [Test] public void EachAxisReadsTheCurrentBodyAndQueryAndAddsToThePoseAfterItsQueryCallback()
        {
            var state = Create(); var first = (Query)state.Query; var second = new Query(state, "replacement");
            first.Cast = request =>
            {
                if (IsProbe(request)) return Empty;
                state.X = 10; state.Y = 20; state.Width = 2; state.Height = 4; state.OffsetX = .5f; state.OffsetY = 2; state.Facing = -1;
                state.VelocityX = 17; state.VelocityY = -11; state.WallHit = true; state.Query = second; return Empty;
            };
            second.Cast = request =>
            {
                Assert.That(request.X, Is.EqualTo(11)); Assert.That(request.Y, Is.EqualTo(22));
                Assert.That(request.Width, Is.EqualTo(1.995f)); Assert.That(request.Height, Is.EqualTo(3.995f));
                Assert.That(request.DirectionY, Is.EqualTo(-1)); Assert.That(request.Distance, Is.EqualTo(1.503f));
                Assert.That(state.VelocityX, Is.EqualTo(17)); Assert.That(state.VelocityY, Is.EqualTo(-11));
                return new[] { Hit(.502f, 0, 1, 20) };
            };
            Step(state);
            Assert.That(first.Requests.Count, Is.EqualTo(2)); Assert.That(second.Requests.Count, Is.EqualTo(1));
            Assert.That(state.X, Is.EqualTo(11.5f)); Assert.That(state.Y, Is.EqualTo(19.5f).Within(.000001f));
            Assert.That(state.VelocityX, Is.EqualTo(17)); Assert.That(state.VelocityY, Is.Zero); Assert.That(state.Grounded && state.WallHit, Is.True);
            Assert.That(state.Events.IndexOf("move"), Is.LessThan(state.Events.IndexOf("replacement:y")));
        }
        [Test] public void SkippedVerticalAxisDoesNotApplyPoseOrOverwriteLiveContactFacts()
        {
            var state = Create(); var query = (Query)state.Query;
            query.Cast = request =>
            {
                if (!IsProbe(request)) { state.VelocityY = 9; state.Grounded = state.WallHit = true; }
                return Empty;
            };
            Step(state, freezeVertical: true);
            Assert.That(query.Requests.Count, Is.EqualTo(2)); Assert.That(state.Bodies.Count, Is.EqualTo(3), "Even a skipped axis reads the current body and query before the resolver skips it.");
            Assert.That(state.Moves.Count, Is.EqualTo(1)); Assert.That(state.X, Is.EqualTo(3.5f)); Assert.That(state.Y, Is.EqualTo(3));
            Assert.That(state.VelocityY, Is.EqualTo(9)); Assert.That(state.Grounded && state.WallHit, Is.True);
            Assert.That(state.Events, Does.Not.Contain("stop-y")); Assert.That(state.Events.Last(), Is.EqualTo("sync"));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void VerticalContactStopsOnlyYAndGroundsOnlyDescentWithoutClearingALiveGroundFact(bool rising, bool groundFromCallback)
        {
            var state = Create(); state.VelocityY = rising ? 2 : -2; var query = (Query)state.Query;
            query.Cast = request =>
            {
                if (IsProbe(request)) return Empty;
                Assert.That(state.Grounded || state.WallHit, Is.False); Assert.That(request.DirectionX, Is.Zero);
                state.Grounded = groundFromCallback; return new[] { Hit(.252f, 0, rising ? -1 : 1) };
            };
            Step(state, desiredX: 0);
            Assert.That(query.Requests.Count, Is.EqualTo(rising ? 1 : 2)); Assert.That(state.Moves.Count, Is.EqualTo(1));
            Assert.That(state.X, Is.EqualTo(2)); Assert.That(state.Y, Is.EqualTo(rising ? 3.25f : 2.75f));
            Assert.That(state.VelocityX, Is.Zero); Assert.That(state.VelocityY, Is.Zero); Assert.That(state.WallHit, Is.False);
            Assert.That(state.Grounded, Is.EqualTo(!rising || groundFromCallback));
            Assert.That(state.Events.IndexOf("stop-y"), Is.GreaterThan(state.Events.IndexOf("move")));
        }
        [Test] public void HorizontalBlockSetsWallWithoutStoppingHorizontalVelocityOrGrounding()
        {
            var state = Create(); var query = (Query)state.Query;
            query.Cast = request => request.DirectionX != 0 ? new[] { Hit(.502f, -1, 0) } : Empty;
            Step(state);
            Assert.That(state.X, Is.EqualTo(2.5f).Within(.000001f)); Assert.That(state.Y, Is.EqualTo(1.5f));
            Assert.That(state.VelocityX, Is.EqualTo(3)); Assert.That(state.VelocityY, Is.EqualTo(-4));
            Assert.That(state.WallHit, Is.True); Assert.That(state.Grounded, Is.False);
            Assert.That(state.Events.IndexOf("wall-on"), Is.GreaterThan(state.Events.IndexOf("move")));
        }
        [Test] public void GroundProbeFailureDoesNotWriteContactFactsOrStartMotion()
        {
            var state = Create(); var query = (Query)state.Query; var failure = new InvalidOperationException("ground query");
            query.Cast = _ => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(failure));
            Assert.That(state.Grounded && state.WallHit, Is.True); Assert.That(state.VelocityX, Is.EqualTo(19)); Assert.That(state.VelocityY, Is.EqualTo(-2));
            Assert.That(state.Inputs, Is.Empty); Assert.That(state.Moves, Is.Empty); Assert.That(state.Events.Last(), Is.EqualTo("world:ground"));
            Assert.That(state.ExpiredAt.Count, Is.EqualTo(1)); Assert.That(state.RefreshedAt.Count, Is.EqualTo(1));
        }
        [Test] public void InvalidMotionAfterTheGroundProbeKeepsItsGroundResultButDoesNotResetWallOrVelocity()
        {
            var state = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => Step(state, step: 0));
            Assert.That(state.Grounded, Is.False); Assert.That(state.WallHit, Is.True); Assert.That(state.VelocityX, Is.EqualTo(19)); Assert.That(state.VelocityY, Is.EqualTo(-2));
            Assert.That(state.Moves, Is.Empty); Assert.That(state.Events.Last(), Is.EqualTo("input"));
        }
        [Test] public void PositionApplicationFailureKeepsItsPartialPoseButDoesNotApplyTheBlockedFlagOrContinue()
        {
            var state = Create(); var query = (Query)state.Query; var failure = new InvalidOperationException("position application");
            query.Cast = request => request.DirectionX != 0 ? new[] { Hit(.502f, -1, 0) } : Empty;
            state.Moved = (_, __) => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(failure));
            Assert.That(state.X, Is.EqualTo(2.5f).Within(.000001f)); Assert.That(state.Y, Is.EqualTo(3)); Assert.That(state.WallHit || state.Grounded, Is.False);
            Assert.That(query.Requests.Count, Is.EqualTo(2)); Assert.That(state.Events.Last(), Is.EqualTo("move"));
        }
        [Test] public void ReentrantQueryStepRetainsNestedFactsWhileTheOuterStepFinishesItsOriginalDisplacements()
        {
            var state = Create(); var query = (Query)state.Query; bool nested = false; Host nestedHost = default;
            query.Cast = request =>
            {
                if (request.DirectionX != 0 && !nested)
                {
                    nested = true; nestedHost = Step(state, desiredX: 0, freezeVertical: true, step: .25f);
                    Assert.That(state.VelocityX, Is.Zero); Assert.That(state.VelocityY, Is.Zero);
                }
                return Empty;
            };
            var outerHost = Step(state);
            Assert.That(nested, Is.True); Assert.That(nestedHost.Syncs, Is.EqualTo(1)); Assert.That(outerHost.Syncs, Is.EqualTo(1));
            Assert.That(state.X, Is.EqualTo(3.5f)); Assert.That(state.Y, Is.EqualTo(1.5f)); Assert.That(state.VelocityX, Is.Zero); Assert.That(state.VelocityY, Is.Zero);
            Assert.That(state.Events.Count(e => e == "sync"), Is.EqualTo(2)); Assert.That(state.Inputs.Count, Is.EqualTo(2)); Assert.That(state.Moves.Count, Is.EqualTo(2));
        }
    }
}
