using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorFacingExecutionTests
    {
        sealed class State
        {
            public float X = .125f, Y = 1000, Width = .9375f, Height = 2, OffsetX = .1875f, OffsetY = 1.25f;
            public int Facing = 1;
            public bool Body = true, Hurt = true, Visual = true, Player, Flip;
            public float BodyX = .1875f, BodyY = 1.25f, HurtX = .1875f, HurtY = 1.25f, VisualX = 8, VisualY = 9;
            public IActorPlacementQuery Query;
            public Action Moved, Faced, Bodied, Hurted, Flipped, Positioned;
            public readonly List<string> Events = new List<string>();
            public readonly List<ActorBodyFrame> Frames = new List<ActorBodyFrame>();
        }
        sealed class Query : IActorPlacementQuery
        {
            readonly State state;
            public readonly string Name;
            public readonly List<ActorOverlapBox> Requests = new List<ActorOverlapBox>();
            public readonly List<ActorSurfaceBounds> Bounds = new List<ActorSurfaceBounds>();
            public Func<ActorOverlapBox, IReadOnlyList<ActorSurfaceBounds>> Overlap;
            public Query(State state, string name = "world") { this.state = state; Name = name; }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request)
            {
                state.Events.Add(Name); Requests.Add(request); return Overlap == null ? Bounds : Overlap(request);
            }
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) => throw new InvalidOperationException("Facing is a placement test, not movement.");
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => throw new InvalidOperationException("Facing is not a floor query.");
        }
        struct Host : IActorFacingHost
        {
            public readonly State State;
            public int Writes;
            public Host(State state) { State = state; Writes = 0; }
            public int Facing
            {
                get { State.Events.Add("facing"); return State.Facing; }
                set { State.Events.Add("face"); Writes++; State.Facing = value; State.Faced?.Invoke(); }
            }
            public float OffsetX { get { State.Events.Add("offset-x"); return State.OffsetX; } }
            public float OffsetY { get { State.Events.Add("offset-y"); return State.OffsetY; } }
            public void ReadPosition(out float x, out float y) { State.Events.Add("position"); x = State.X; y = State.Y; }
            public ActorBodyFrame BodyFrame
            {
                get
                {
                    State.Events.Add("body-frame"); var frame = new ActorBodyFrame(State.X, State.Y, State.Width, State.Height, State.OffsetX, State.OffsetY, State.Facing);
                    State.Frames.Add(frame); return frame;
                }
            }
            public IActorPlacementQuery SurfaceQuery { get { State.Events.Add("query"); return State.Query; } }
            public void SetPosition(float x, float y) { State.Events.Add("move"); Writes++; State.X = x; State.Y = y; State.Moved?.Invoke(); }
            public bool HasBody { get { State.Events.Add("has-body"); return State.Body; } }
            public void SetBodyOffset(float x, float y) { State.Events.Add("body"); Writes++; State.BodyX = x; State.BodyY = y; State.Bodied?.Invoke(); }
            public bool HasHurt { get { State.Events.Add("has-hurt"); return State.Hurt; } }
            public void SetHurtOffset(float x, float y) { State.Events.Add("hurt"); Writes++; State.HurtX = x; State.HurtY = y; State.Hurted?.Invoke(); }
            public bool HasVisual { get { State.Events.Add("has-visual"); return State.Visual; } }
            public void SetVisualFlip(bool flip) { State.Events.Add("flip"); Writes++; State.Flip = flip; State.Flipped?.Invoke(); }
            public bool IsPlayer { get { State.Events.Add("player"); return State.Player; } }
            public void SetVisualPosition(float x, float y) { State.Events.Add("visual-position"); Writes++; State.VisualX = x; State.VisualY = y; State.Positioned?.Invoke(); }
        }
        static State Create()
        {
            var state = new State(); state.Query = new Query(state); return state;
        }
        static Host Sync(State state, int direction)
        {
            var host = new Host(state); Assert.That(ActorFacingExecution.Sync(direction, ref host), Is.True); return host;
        }
        static ActorSurfaceBounds Wall(int direction) => direction < 0
            ? new ActorSurfaceBounds(-3.2f, 995, -1, -2.8f, 1010, 1, false)
            : new ActorSurfaceBounds(2.8f, 995, -1, 3.2f, 1010, 1, false);
        static void AssertOffsets(State state, int facing, float offsetX, float offsetY)
        {
            Assert.That(state.Facing, Is.EqualTo(facing)); Assert.That(state.BodyX, Is.EqualTo(offsetX)); Assert.That(state.BodyY, Is.EqualTo(offsetY));
            Assert.That(state.HurtX, Is.EqualTo(offsetX)); Assert.That(state.HurtY, Is.EqualTo(offsetY)); Assert.That(state.Flip, Is.EqualTo(facing < 0));
        }
        [Test] public void ExecutionAndHostStayEngineSceneUiAndDefinitionsFree()
        {
            foreach (var type in new[] { typeof(ActorFacingExecution), typeof(IActorFacingHost) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
            Assert.That(typeof(IActorFacingHost).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Deviant.Definitions")), Is.False);
        }
        [Test] public void ZeroIsASuccessfulNoopWithoutReadingAnyHostFact()
        {
            var state = Create(); state.Facing = 0; state.Width = float.NaN; state.OffsetX = float.PositiveInfinity; state.Query = null;
            var host = Sync(state, 0);
            Assert.That(state.Events, Is.Empty); Assert.That(host.Writes, Is.Zero); Assert.That(state.Facing, Is.Zero);
            Assert.That(state.BodyX, Is.EqualTo(.1875f)); Assert.That(state.VisualX, Is.EqualTo(8));
        }
        [TestCase(-1)] [TestCase(-17)] [TestCase(int.MinValue)] [TestCase(1)] [TestCase(23)] [TestCase(int.MaxValue)]
        public void NonzeroInputNormalizesAndSameFacingResynchronizesWithoutAPlacementRead(int direction)
        {
            var state = Create(); int facing = direction < 0 ? -1 : 1; state.Facing = facing;
            state.Query = null; state.Width = float.NaN; state.BodyX = state.HurtX = 99; state.Flip = facing > 0;
            var host = Sync(state, direction);
            AssertOffsets(state, facing, .1875f * facing, 1.25f); Assert.That(state.X, Is.EqualTo(.125f)); Assert.That(state.Y, Is.EqualTo(1000));
            Assert.That(state.VisualX, Is.EqualTo(.1875f * facing)); Assert.That(state.VisualY, Is.EqualTo(1.25f)); Assert.That(host.Writes, Is.EqualTo(5));
            Assert.That(state.Events, Does.Not.Contain("position")); Assert.That(state.Events, Does.Not.Contain("body-frame")); Assert.That(state.Events, Does.Not.Contain("query"));
        }
        [TestCase(-1, .1875f)] [TestCase(1, .1875f)] [TestCase(-1, .4375f)] [TestCase(1, .4375f)]
        public void WallTurnKeepsTheBodyCentreAndAppliesCorrectionBeforeFacingAndNativeUpdates(int direction, float offsetX)
        {
            var state = Create(); var query = (Query)state.Query; state.Facing = -direction; state.OffsetX = offsetX;
            state.X = direction * (2.8f - state.Width * .5f) - offsetX * state.Facing; float oldX = state.X, center = state.X + offsetX * state.Facing;
            query.Bounds.Add(Wall(direction));
            Assert.That(ActorPlacementResolver.Blocked(new ActorBodyFrame(state.X, state.Y, state.Width, state.Height, offsetX, state.OffsetY, state.Facing), state.X, state.Y, state.Facing, query), Is.False);
            query.Requests.Clear(); state.Events.Clear();
            state.Moved = () => { Assert.That(state.Facing, Is.EqualTo(-direction)); Assert.That(state.X, Is.EqualTo(oldX + offsetX * (-direction - direction))); };
            state.Faced = () => Assert.That(state.X + offsetX * direction, Is.EqualTo(center).Within(.000001f));
            var host = Sync(state, direction);
            AssertOffsets(state, direction, offsetX * direction, 1.25f); Assert.That(host.Writes, Is.EqualTo(6));
            Assert.That(query.Requests.Count, Is.EqualTo(1)); Assert.That(query.Requests[0].X, Is.EqualTo(oldX + offsetX * direction));
            Assert.That(state.Frames.Single().Facing, Is.EqualTo(-direction)); Assert.That(state.Frames[0].X, Is.EqualTo(oldX));
            Assert.That(state.Events, Is.EqualTo(new[] { "facing", "position", "body-frame", "query", "world", "position", "offset-x", "facing", "move", "face", "offset-x", "facing", "offset-y", "has-body", "body", "has-hurt", "hurt", "has-visual", "facing", "flip", "has-visual", "player", "offset-x", "facing", "offset-y", "visual-position" }));
            Assert.That(ActorPlacementResolver.Blocked(new ActorBodyFrame(state.X, state.Y, state.Width, state.Height, offsetX, state.OffsetY, direction), state.X, state.Y, direction, query), Is.False);
            query.Requests.Clear(); state.Events.Clear(); Sync(state, direction * 19); Assert.That(query.Requests, Is.Empty); Assert.That(state.X, Is.EqualTo(oldX + offsetX * (-direction - direction)));
        }
        [TestCase(-1)] [TestCase(1)]
        public void UnblockedTurnDoesNotMoveAndFloorOneWayAndDifferentZBoundsDoNotBecomeWalls(int direction)
        {
            var state = Create(); var query = (Query)state.Query; state.Facing = -direction;
            query.Bounds.Add(new ActorSurfaceBounds(-100, 999, -1, 100, 1000.02f, 1, false));
            query.Bounds.Add(new ActorSurfaceBounds(-100, 999, -1, 100, 1010, 1, true));
            query.Bounds.Add(new ActorSurfaceBounds(-100, 999, 1, 100, 1010, 2, false));
            query.Bounds.Add(Wall(direction));
            var host = Sync(state, direction);
            Assert.That(host.Writes, Is.EqualTo(5)); Assert.That(state.X, Is.EqualTo(.125f)); Assert.That(state.Y, Is.EqualTo(1000));
            AssertOffsets(state, direction, .1875f * direction, 1.25f); Assert.That(state.Events, Does.Not.Contain("move"));
            Assert.That(state.Events.IndexOf("world"), Is.LessThan(state.Events.IndexOf("face")));
        }
        [Test] public void QueryCallbacksUseTheOldBodyButCorrectionReadsCurrentPoseOffsetAndFacing()
        {
            var state = Create(); var query = (Query)state.Query;
            query.Overlap = request =>
            {
                Assert.That(request.X, Is.EqualTo(-.0625f)); Assert.That(request.Y, Is.EqualTo(1001.25f));
                Assert.That(request.Width, Is.EqualTo(.9225f)); Assert.That(request.Height, Is.EqualTo(1.985f)); Assert.That(state.Facing, Is.EqualTo(1));
                state.X = 5; state.Y = 1003; state.OffsetX = .5f; state.OffsetY = 2; state.Width = 3; state.Query = new Query(state, "replacement");
                return new[] { new ActorSurfaceBounds(-1, 999, -1, 1, 1003, 1, false) };
            };
            Sync(state, -23);
            Assert.That(state.X, Is.EqualTo(6)); Assert.That(state.Y, Is.EqualTo(1003)); AssertOffsets(state, -1, -.5f, 2);
            Assert.That(state.Frames.Single().Width, Is.EqualTo(.9375f)); Assert.That(((Query)state.Query).Requests, Is.Empty);
        }
        [Test] public void QueryReentryCanAlreadyTurnTheActorAndOuterCorrectionUsesThatCurrentFacing()
        {
            var state = Create(); var outer = (Query)state.Query; var nested = new Query(state, "nested");
            outer.Overlap = request =>
            {
                state.Query = nested; Sync(state, -1); state.OffsetX = .5f; state.OffsetY = 2;
                return new[] { new ActorSurfaceBounds(-1, 999, -1, 1, 1003, 1, false) };
            };
            Sync(state, -1);
            Assert.That(state.X, Is.EqualTo(.125f)); AssertOffsets(state, -1, -.5f, 2);
            Assert.That(outer.Requests.Count, Is.EqualTo(1)); Assert.That(nested.Requests.Count, Is.EqualTo(1)); Assert.That(state.Events.Count(e => e == "face"), Is.EqualTo(2));
            Assert.That(state.Events.LastIndexOf("move"), Is.GreaterThan(state.Events.IndexOf("visual-position")));
        }
        [Test] public void PositionAppliesBeforeFacingAndFollowingOffsetsReadChangesMadeByItsCallback()
        {
            var state = Create(); var query = (Query)state.Query; query.Bounds.Add(new ActorSurfaceBounds(-1, 999, -1, 1, 1003, 1, false));
            state.Moved = () => { Assert.That(state.Facing, Is.EqualTo(1)); Assert.That(state.X, Is.EqualTo(.5f)); state.OffsetX = .75f; state.OffsetY = 3; state.Facing = 1; };
            Sync(state, -1);
            Assert.That(state.X, Is.EqualTo(.5f)); AssertOffsets(state, -1, -.75f, 3); Assert.That(state.VisualX, Is.EqualTo(-.75f));
        }
        [Test] public void BodyAndHurtShareOneOffsetButVisualRechecksCurrentPresencePlayerAndGeometryAfterFlip()
        {
            var state = Create(); state.Facing = -1;
            state.Bodied = () => { state.OffsetX = .5f; state.OffsetY = 2; state.Facing = 1; };
            state.Flipped = () => { state.OffsetX = .75f; state.OffsetY = 3; state.Facing = -1; };
            Sync(state, -1);
            Assert.That(state.BodyX, Is.EqualTo(-.1875f)); Assert.That(state.BodyY, Is.EqualTo(1.25f)); Assert.That(state.HurtX, Is.EqualTo(-.1875f)); Assert.That(state.HurtY, Is.EqualTo(1.25f));
            Assert.That(state.Flip, Is.False); Assert.That(state.VisualX, Is.EqualTo(-.75f)); Assert.That(state.VisualY, Is.EqualTo(3));
            state.Events.Clear(); state.Bodied = null; state.Flipped = () => state.Visual = false; Sync(state, -1);
            Assert.That(state.Events.Count(e => e == "has-visual"), Is.EqualTo(2)); Assert.That(state.Events, Does.Not.Contain("player")); Assert.That(state.Events, Does.Not.Contain("visual-position"));
            state.Events.Clear(); state.Visual = true; state.Flipped = () => state.Player = true; Sync(state, -1);
            Assert.That(state.Events, Does.Contain("player")); Assert.That(state.Events, Does.Not.Contain("visual-position")); Assert.That(state.VisualX, Is.EqualTo(-.75f));
        }
        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)]
        [TestCase(4, false)] [TestCase(5, false)] [TestCase(6, false)] [TestCase(7, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)]
        [TestCase(4, true)] [TestCase(5, true)] [TestCase(6, true)] [TestCase(7, true)]
        public void MissingNativeObjectsAreSkippedAndOnlyEnemyVisualPositionIsAdjusted(int mask, bool player)
        {
            var state = Create(); state.Body = (mask & 1) != 0; state.Hurt = (mask & 2) != 0; state.Visual = (mask & 4) != 0; state.Player = player;
            Sync(state, -1);
            Assert.That(state.Facing, Is.EqualTo(-1)); Assert.That(state.BodyX, Is.EqualTo(state.Body ? -.1875f : .1875f)); Assert.That(state.HurtX, Is.EqualTo(state.Hurt ? -.1875f : .1875f));
            Assert.That(state.Flip, Is.EqualTo(state.Visual)); Assert.That(state.VisualX, Is.EqualTo(state.Visual && !player ? -.1875f : 8));
            Assert.That(state.Events.Contains("visual-position"), Is.EqualTo(state.Visual && !player)); Assert.That(state.Events.Contains("player"), Is.EqualTo(state.Visual));
        }
        [Test] public void QueryFailureLeavesCallbackChangesButDoesNotStartCorrectionOrSynchronization()
        {
            var state = Create(); var query = (Query)state.Query; var failure = new InvalidOperationException("query");
            query.Overlap = request => { state.X = 7; state.Y = 1004; state.OffsetX = .75f; state.OffsetY = 3; throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => Sync(state, -1)), Is.SameAs(failure));
            Assert.That(state.X, Is.EqualTo(7)); Assert.That(state.Y, Is.EqualTo(1004)); Assert.That(state.Facing, Is.EqualTo(1)); Assert.That(state.BodyX, Is.EqualTo(.1875f));
            Assert.That(state.VisualX, Is.EqualTo(8)); Assert.That(state.Events.Last(), Is.EqualTo("world"));
        }
        [TestCase("move")] [TestCase("face")] [TestCase("body")] [TestCase("hurt")] [TestCase("flip")] [TestCase("visual-position")]
        public void CallbackFailureKeepsExactlyTheAlreadyAppliedPrefixWithoutRollbackOrLaterWork(string phase)
        {
            var state = Create(); var query = (Query)state.Query; query.Bounds.Add(new ActorSurfaceBounds(-1, 999, -1, 1, 1003, 1, false));
            var failure = new InvalidOperationException(phase); Action fail = () => throw failure;
            if (phase == "move") state.Moved = fail;
            else if (phase == "face") state.Faced = fail;
            else if (phase == "body") state.Bodied = fail;
            else if (phase == "hurt") state.Hurted = fail;
            else if (phase == "flip") state.Flipped = fail;
            else state.Positioned = fail;
            var ordered = new[] { "move", "face", "body", "hurt", "flip", "visual-position" }; int stopped = Array.IndexOf(ordered, phase);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Sync(state, -1)), Is.SameAs(failure));
            Assert.That(state.Events.Where(e => ordered.Contains(e)), Is.EqualTo(ordered.Take(stopped + 1))); Assert.That(state.Events.Last(), Is.EqualTo(phase));
            Assert.That(state.X, Is.EqualTo(.5f)); Assert.That(state.Facing, Is.EqualTo(stopped >= 1 ? -1 : 1));
            Assert.That(state.BodyX, Is.EqualTo(stopped >= 2 ? -.1875f : .1875f)); Assert.That(state.HurtX, Is.EqualTo(stopped >= 3 ? -.1875f : .1875f));
            Assert.That(state.Flip, Is.EqualTo(stopped >= 4)); Assert.That(state.VisualX, Is.EqualTo(stopped >= 5 ? -.1875f : 8));
        }
        [Test] public void ExistingDirectFacingAssignmentRemainsAQueryFreeResynchronization()
        {
            var state = Create(); state.X = 6; state.Y = 1002; state.Facing = -1; state.Query = null;
            Sync(state, -1);
            Assert.That(state.X, Is.EqualTo(6)); Assert.That(state.Y, Is.EqualTo(1002)); AssertOffsets(state, -1, -.1875f, 1.25f);
            Assert.That(state.Events, Does.Not.Contain("body-frame")); Assert.That(state.Events, Does.Not.Contain("move"));
        }
    }
}
