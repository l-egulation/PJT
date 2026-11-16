using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorContactTests
    {
        sealed class Query : IActorSurfaceQuery
        {
            public readonly List<ActorBoxCast> Requests = new List<ActorBoxCast>();
            public IReadOnlyList<ActorSurfaceContact> Hits = Array.AsReadOnly(Array.Empty<ActorSurfaceContact>());
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { Requests.Add(request); return Hits; }
        }
        static ActorBodyFrame Body(float y = 3) => new ActorBodyFrame(2, y, 1, 2, .25f, 1, -1);
        static Query Hits(params ActorSurfaceContact[] contacts) => new Query { Hits = Array.AsReadOnly(contacts) };
        static ActorSurfaceContact Hit(float distance = .4f, float nx = -1, float ny = 0, float top = 5, bool oneWay = false, bool ignored = false) => new ActorSurfaceContact(distance, nx, ny, top, oneWay, ignored);
        [Test] public void QueryContractsAndContactRulesDoNotReferenceEngineSceneOrUi()
        {
            foreach (var type in new[] { typeof(IActorSurfaceQuery), typeof(ActorBodyFrame), typeof(ActorContactResolver) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void AxisQueryUsesExistingBodyOffsetSkinAndContactMargin()
        {
            var query = new Query(); var move = ActorContactResolver.ResolveAxis(Body(), 1, true, query); var cast = query.Requests.Single();
            Assert.That(cast.X, Is.EqualTo(1.75f)); Assert.That(cast.Y, Is.EqualTo(4)); Assert.That(cast.Width, Is.EqualTo(.995f)); Assert.That(cast.Height, Is.EqualTo(1.995f));
            Assert.That(cast.DirectionX, Is.EqualTo(1)); Assert.That(cast.DirectionY, Is.Zero); Assert.That(cast.Distance, Is.EqualTo(1.003f)); Assert.That(move.DeltaX, Is.EqualTo(1)); Assert.That(move.Blocked, Is.False);
        }
        [Test] public void CurrentContactsChooseTheNearestAcceptedLimitAndRetainQueryOrder()
        {
            var query = Hits(Hit(.7f), Hit(.4f), Hit(.1f, ignored: true)); var before = query.Hits.ToArray(); var move = ActorContactResolver.ResolveAxis(Body(), 1, true, query);
            Assert.That(move.DeltaX, Is.EqualTo(.398f).Within(.0000001f)); Assert.That(move.Blocked, Is.True); Assert.That(move.DeltaY, Is.Zero); Assert.That(query.Hits, Is.EqualTo(before));
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, true, Hits(Hit(-.01f))).DeltaX, Is.Zero);
        }
        [Test] public void TinyRequestsSkipTheQueryAndNegativeAxisKeepsItsDirectionBits()
        {
            var query = new Query(); var skipped = ActorContactResolver.ResolveAxis(Body(), -.0000005f, false, query); Assert.That(skipped.Skipped, Is.True); Assert.That(query.Requests, Is.Empty);
            var move = ActorContactResolver.ResolveAxis(Body(), -1, false, query); Assert.That(move.DeltaY, Is.EqualTo(-1)); Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(move.DeltaX), 0), Is.EqualTo(int.MinValue), "Preserve the signed zero from axis-vector multiplication.");
        }
        [Test] public void OneWayContactsRespectAxisDirectionAndFootHeight()
        {
            var contact = Hit(.4f, 0, 1, top: 3, oneWay: true);
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, true, Hits(contact)).Blocked, Is.False);
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, false, Hits(contact)).Blocked, Is.False);
            Assert.That(ActorContactResolver.ResolveAxis(Body(2), -1, false, Hits(contact)).Blocked, Is.False);
            Assert.That(ActorContactResolver.ResolveAxis(Body(3), -1, false, Hits(contact)).Blocked, Is.True);
        }
        [Test] public void NormalFilteringRetainsFacingAndOverlapBoundarySemantics()
        {
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, true, Hits(Hit(nx: 1))).Blocked, Is.False);
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, true, Hits(Hit(nx: -.1f, ny: 1))).Blocked, Is.True);
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, true, Hits(Hit(nx: -.099f, ny: 1))).Blocked, Is.False);
            Assert.That(ActorContactResolver.ResolveAxis(Body(), 1, true, Hits(Hit(nx: 0, ny: 0))).Blocked, Is.True, "Initial overlaps with no useful normal still limit travel.");
        }
        [Test] public void GroundProbeUsesCurrentFootGeometryAndStrictVelocityNormalLimits()
        {
            var query = Hits(Hit(nx: 0, ny: 1, top: 3)); Assert.That(ActorContactResolver.ProbeGround(Body(), .050001f, query), Is.False); Assert.That(query.Requests, Is.Empty);
            Assert.That(ActorContactResolver.ProbeGround(Body(), .05f, query), Is.True); var cast = query.Requests.Single();
            Assert.That(cast.X, Is.EqualTo(1.75f)); Assert.That(cast.Y, Is.EqualTo(3.025f)); Assert.That(cast.Width, Is.EqualTo(.92f)); Assert.That(cast.Height, Is.EqualTo(.04f)); Assert.That(cast.DirectionY, Is.EqualTo(-1)); Assert.That(cast.Distance, Is.EqualTo(.055f));
            Assert.That(ActorContactResolver.ProbeGround(Body(), 0, Hits(Hit(nx: 0, ny: .8f))), Is.False);
            Assert.That(ActorContactResolver.ProbeGround(Body(), 0, Hits(Hit(nx: 0, ny: 1, ignored: true))), Is.False);
            Assert.That(ActorContactResolver.ProbeGround(Body(2), 0, Hits(Hit(nx: 0, ny: 1, top: 3))), Is.False);
        }
        [Test] public void FlushFloorSeamsDoNotBlockHorizontalMotionButRaisedWallsStillDo()
        {
            foreach (float direction in new[] { -1f, 1f })
            {
                var floor = Hits(Hit(0, nx: -direction, top: 3.002f));
                Assert.That(ActorContactResolver.ResolveAxis(Body(), direction, true, floor).Blocked, Is.False);
                Assert.That(ActorContactResolver.ResolveSolidAxis(Body(), direction, true, floor).Blocked, Is.False);
                var step = Hits(Hit(0, nx: -direction, top: 3.02f));
                Assert.That(ActorContactResolver.ResolveAxis(Body(), direction, true, step).Blocked, Is.True);
                Assert.That(ActorContactResolver.ResolveAxis(Body(), -1, false, Hits(Hit(0, nx: 0, ny: 1, top: 3))).Blocked, Is.True);
            }
        }
        [Test] public void MissingOrInvalidQueriesCannotProduceAMove()
        {
            Assert.Throws<ArgumentNullException>(() => ActorContactResolver.ResolveAxis(Body(), 1, true, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ActorContactResolver.ResolveAxis(Body(), float.NaN, true, new Query()));
            Assert.Throws<InvalidOperationException>(() => ActorContactResolver.ResolveAxis(Body(), 1, true, new Query { Hits = null }));
            Assert.Throws<ArgumentException>(() => ActorContactResolver.ResolveAxis(default, 1, true, new Query()));
            Assert.Throws<ArgumentException>(() => new ActorSurfaceContact(float.NaN, 0, 1, 0, false, false));
            Assert.Throws<ArgumentException>(() => new ActorBoxCast(0, 0, 1, 1, 0, 0, 1));
        }
    }
}
