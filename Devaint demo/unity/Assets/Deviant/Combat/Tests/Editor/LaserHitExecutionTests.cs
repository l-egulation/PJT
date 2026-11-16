using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class LaserHitExecutionTests
    {
        sealed class Actor
        {
            public readonly int Id;
            public readonly ActorVitalsState Vitals = new ActorVitalsState(100);
            public float X, Y, Width = .25f, Height = .5f;
            public bool Player, Decoy, Reject;
            public int Attempts, OcclusionQueries;
            public Actor(int id, float x = 0, float y = 0) { Id = id; X = x; Y = y; }
            public float Health => Vitals.Read().Health;
        }
        sealed class Obstacle
        {
            public readonly string Name;
            public float X, Y, Width = .2f, Height = 1;
            public bool Surface = true, OneWay;
            public Obstacle(string name, float x, float y = 0) { Name = name; X = x; Y = y; }
        }
        readonly struct Cast
        {
            public readonly Obstacle Obstacle;
            public readonly float Distance;
            public Cast(Obstacle obstacle, float distance) { Obstacle = obstacle; Distance = distance; }
        }
        sealed class Receipt
        {
            public Actor Target, Owner;
            public float Damage;
            public int Direction;
            public bool Basic;
        }
        sealed class Visual
        {
            public float X, Y, Width, Height, Until;
        }
        sealed class Beam
        {
            public readonly Actor Owner = new Actor(100) { Player = true };
            public readonly SkillRangedLaunchDefinition Definition;
            public readonly List<Actor> Actors = new List<Actor>();
            public readonly List<Obstacle> Obstacles = new List<Obstacle>();
            public readonly List<Receipt> Receipts = new List<Receipt>();
            public readonly List<Visual> Visuals = new List<Visual>();
            public readonly List<HashSet<int>> Claims = new List<HashSet<int>>();
            public readonly List<int> RecordedCounts = new List<int>();
            public readonly List<string> Events = new List<string>();
            public float OriginX, OriginY, MuzzleX = 1, MuzzleY, Damage = 12, Now = 2;
            public float QueryX, QueryY, QueryWidth, QueryHeight, CastLength, ContactX, ContactY;
            public int Direction = 1, CastQueries, TargetQueries;
            public Action<string> Callback;
            public Beam(float distance = 8, float thickness = .25f)
            { Definition = new SkillRangedLaunchDefinition(new RangedData { type = "laser", distance = distance, thickness = thickness }); }
            public void Mark(string name) { Events.Add(name); Callback?.Invoke(name); }
            public void Fire() { var host = new Host(this); LaserHitExecution.Fire(ref host); }
            public Actor Add(int id, float x, float y = 0)
            { var actor = new Actor(id, x, y); Actors.Add(actor); return actor; }
            public Obstacle Wall(string name, float face, bool oneWay = false)
            {
                var wall = new Obstacle(name, OriginX + Direction * (face + .1f), OriginY) { OneWay = oneWay };
                Obstacles.Add(wall); return wall;
            }
        }
        struct Host : ILaserHitHost
        {
            readonly Beam beam;
            readonly Actor owner;
            readonly SkillRangedLaunchDefinition definition;
            readonly float originX, originY, muzzleX, muzzleY, damage;
            readonly int direction;
            List<Cast> casts;
            Cast current;
            HashSet<int> seen;
            List<Actor>.Enumerator targets;
            Actor target;
            public Host(Beam beam)
            {
                this.beam = beam; owner = beam.Owner; definition = beam.Definition;
                originX = beam.OriginX; originY = beam.OriginY; muzzleX = beam.MuzzleX; muzzleY = beam.MuzzleY;
                damage = beam.Damage; direction = beam.Direction;
                casts = null; current = default; seen = null; targets = default; target = null;
            }
            public float MuzzleGap
            {
                get
                {
                    float x = muzzleX - originX, y = muzzleY - originY;
                    beam.Mark("gap"); return (float)Math.Sqrt(x * x + y * y);
                }
            }
            public float ProjectileDistance { get { beam.Mark("range"); return definition.Projectile.Distance; } }
            public bool IgnoresTerrain => definition.Projectile.IgnoresTerrain;
            public int QueryCast(float length)
            {
                casts = new List<Cast>(); beam.CastLength = length; beam.CastQueries++;
                foreach (var obstacle in beam.Obstacles)
                {
                    if (Math.Abs(obstacle.Y - originY) > (obstacle.Height + definition.Thickness) / 2) continue;
                    float near = direction * (obstacle.X - originX) - obstacle.Width / 2 - .001f / 2;
                    float far = direction * (obstacle.X - originX) + obstacle.Width / 2 + .001f / 2;
                    if (far < 0 || near > length) continue;
                    casts.Add(new Cast(obstacle, Math.Max(0, near)));
                }
                beam.Mark("cast"); return casts.Count;
            }
            public bool FindSurface(int index)
            { current = casts[index]; beam.Mark("surface/" + current.Obstacle.Name); return current.Obstacle.Surface; }
            public bool SurfaceIsOneWay
            { get { beam.Mark("one-way/" + current.Obstacle.Name); return current.Obstacle.OneWay; } }
            public float SurfaceDistance
            { get { beam.Mark("wall-distance/" + current.Obstacle.Name); return current.Distance; } }
            public void BeginTargets()
            { seen = new HashSet<int>(); beam.Claims.Add(seen); beam.Mark("begin"); }
            public void QueryTargets(float length, float width)
            {
                float x = originX + direction * length / 2;
                beam.QueryX = x; beam.QueryY = originY; beam.QueryWidth = width; beam.QueryHeight = definition.Thickness;
                beam.TargetQueries++; var query = new List<Actor>();
                foreach (var actor in beam.Actors)
                    if (Math.Abs(actor.X - x) <= (actor.Width + width) / 2 &&
                        Math.Abs(actor.Y - originY) <= (actor.Height + definition.Thickness) / 2) query.Add(actor);
                // Repeated rows deliberately exercise the pure claim boundary;
                // native TargetsInBox already coalesces duplicate hurt colliders.
                targets = query.GetEnumerator(); beam.Mark("query");
            }
            public bool NextTarget()
            {
                if (!targets.MoveNext()) return false;
                target = targets.Current; beam.Mark("target/" + target.Id); return true;
            }
            public bool TargetIsOwner { get { beam.Mark("owner/" + target.Id); return ReferenceEquals(target, owner); } }
            public bool TargetIsPlayer { get { beam.Mark("player/" + target.Id); return target.Player; } }
            public bool TargetIsDecoy { get { beam.Mark("decoy/" + target.Id); return target.Decoy; } }
            public bool TargetDead { get { beam.Mark("dead/" + target.Id); return target.Vitals.Read().Dead; } }
            public bool TryClaimTarget()
            { bool added = seen.Add(target.Id); beam.Mark("claim/" + target.Id); return added; }
            public bool TargetOccluded()
            {
                target.OcclusionQueries++;
                float x = Clamp(originX, target.X - target.Width / 2, target.X + target.Width / 2);
                float y = Clamp(originY, target.Y - target.Height / 2, target.Y + target.Height / 2);
                beam.ContactX = x; beam.ContactY = y; bool blocked = false;
                foreach (var obstacle in beam.Obstacles)
                {
                    if (!obstacle.Surface || obstacle.OneWay) continue;
                    float enter = 0, exit = 1;
                    if (Clip(originX, x - originX, obstacle.X - obstacle.Width / 2, obstacle.X + obstacle.Width / 2, ref enter, ref exit) &&
                        Clip(originY, y - originY, obstacle.Y - obstacle.Height / 2, obstacle.Y + obstacle.Height / 2, ref enter, ref exit))
                    { blocked = true; break; }
                }
                beam.Mark("occluded/" + target.Id); return blocked;
            }
            public void ReceiveBasic()
            {
                target.Attempts++;
                beam.Receipts.Add(new Receipt { Target = target, Owner = owner, Damage = damage, Direction = direction, Basic = true });
                if (!target.Reject) target.Vitals.ApplyDamage(damage, beam.Now, false);
                beam.Mark("receive/" + target.Id);
            }
            public void ShowVisual(float visible)
            {
                beam.Visuals.Add(new Visual { X = muzzleX + direction * visible / 2, Y = muzzleY,
                    Width = visible, Height = definition.Thickness, Until = beam.Now + .1f });
                beam.Mark("visual");
            }
            public int ClaimedTargetCount { get { beam.Mark("count"); return seen.Count; } }
            public void Record(int targetCount) { beam.RecordedCounts.Add(targetCount); beam.Mark("record"); }
            static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
            static bool Clip(float origin, float delta, float min, float max, ref float enter, ref float exit)
            {
                if (delta == 0) return origin >= min && origin <= max;
                float a = (min - origin) / delta, b = (max - origin) / delta;
                enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
                return enter <= exit;
            }
        }
        static int[] Received(Beam beam) => beam.Receipts.ConvertAll(receipt => receipt.Target.Id).ToArray();
        static List<string> Effects(Beam beam) => beam.Events.FindAll(name => name == "cast" || name == "begin" || name == "query" ||
            name.StartsWith("claim/", StringComparison.Ordinal) || name.StartsWith("occluded/", StringComparison.Ordinal) ||
            name.StartsWith("receive/", StringComparison.Ordinal) || name == "visual" || name == "record");

        [TestCase(-1)] [TestCase(1)]
        public void UnblockedBeamDamagesInCandidateOrderOnceAndVisualRunsOnlyFromMuzzleToEndpoint(int direction)
        {
            var beam = new Beam { OriginX = 2, OriginY = 3, MuzzleX = 2 + direction * .75f, MuzzleY = 3, Direction = direction };
            var far = beam.Add(2, 2 + direction * 6, 3); var near = beam.Add(1, 2 + direction * 2, 3);
            beam.Actors.Add(near); beam.Owner.X = 2; beam.Owner.Y = 3; beam.Actors.Add(beam.Owner);
            var player = beam.Add(3, 2 + direction, 3); player.Player = true;
            var decoy = beam.Add(4, 2 + direction, 3); decoy.Decoy = true;
            var dead = beam.Add(5, 2 + direction, 3); dead.Vitals.WriteHealth(0);
            var beyond = beam.Add(6, 2 + direction * 10, 3);
            beam.Fire();
            Assert.That(Received(beam), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(far.Health, Is.EqualTo(88)); Assert.That(near.Health, Is.EqualTo(88));
            Assert.That(near.Attempts, Is.EqualTo(1)); Assert.That(near.OcclusionQueries, Is.EqualTo(1));
            Assert.That(player.Health, Is.EqualTo(100)); Assert.That(decoy.Health, Is.EqualTo(100)); Assert.That(beyond.Health, Is.EqualTo(100));
            Assert.That(beam.Owner.Health, Is.EqualTo(100)); Assert.That(dead.Health, Is.Zero);
            Assert.That(beam.Events, Does.Not.Contain("claim/100")); Assert.That(beam.Events, Does.Not.Contain("decoy/3"));
            Assert.That(beam.Events, Does.Not.Contain("dead/4")); Assert.That(beam.Events, Does.Not.Contain("claim/5"));
            Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 2 })); Assert.That(beam.QueryWidth, Is.EqualTo(8.75f));
            Assert.That(beam.QueryX - direction * beam.QueryWidth / 2, Is.EqualTo(beam.OriginX));
            Assert.That(beam.QueryX + direction * beam.QueryWidth / 2, Is.EqualTo(2 + direction * 8.75f));
            Assert.That(beam.Visuals.Count, Is.EqualTo(1)); var visual = beam.Visuals[0];
            Assert.That(visual.X - direction * visual.Width / 2, Is.EqualTo(beam.MuzzleX));
            Assert.That(visual.X + direction * visual.Width / 2, Is.EqualTo(2 + direction * 8.75f));
            Assert.That(visual.Y, Is.EqualTo(3)); Assert.That(visual.Width, Is.EqualTo(8)); Assert.That(visual.Height, Is.EqualTo(.25f));
            Assert.That(visual.Until, Is.EqualTo(beam.Now + .1f));
            foreach (var receipt in beam.Receipts)
            { Assert.That(receipt.Owner, Is.SameAs(beam.Owner)); Assert.That(receipt.Damage, Is.EqualTo(12)); Assert.That(receipt.Direction, Is.EqualTo(direction)); Assert.That(receipt.Basic, Is.True); }
            Assert.That(beam.Events.GetRange(0, 5), Is.EqualTo(new[] { "gap", "range", "cast", "begin", "query" }));
        }
        [TestCase(-1)] [TestCase(1)]
        public void EveryNativeOrderedSolidContributesTheMinimumBeforeTargetsWhileOtherCollidersAndOneWayDoNot(int direction)
        {
            var beam = new Beam { Direction = direction, MuzzleX = direction };
            beam.Wall("far", 6); beam.Wall("one-way", .5f, true); beam.Wall("body", .25f).Surface = false; beam.Wall("near", 4);
            var before = beam.Add(1, direction * 3); var after = beam.Add(2, direction * 5);
            beam.Fire();
            Assert.That(before.Health, Is.EqualTo(88)); Assert.That(after.Health, Is.EqualTo(100));
            Assert.That(beam.Events.FindAll(name => name.StartsWith("surface/", StringComparison.Ordinal)),
                Is.EqualTo(new[] { "surface/far", "surface/one-way", "surface/body", "surface/near" }));
            Assert.That(beam.Events, Does.Not.Contain("wall-distance/one-way")); Assert.That(beam.Events, Does.Not.Contain("one-way/body"));
            Assert.That(beam.CastLength, Is.EqualTo(9)); Assert.That(beam.QueryWidth, Is.EqualTo(4 - .0015f).Within(.00001f));
            Assert.That(beam.Visuals[0].Width, Is.EqualTo(3 - .0015f).Within(.00001f));
            float endpoint = beam.Visuals[0].X + direction * beam.Visuals[0].Width / 2;
            Assert.That(direction * endpoint, Is.EqualTo(4 - .0015f).Within(.00001f));
            Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 1 }));
        }
        [Test] public void OneWayPassesBothLengthAndClosestPointOcclusionChecks()
        {
            var beam = new Beam(); beam.Wall("one-way", 2, true); var target = beam.Add(1, 6);
            beam.Fire();
            Assert.That(target.Health, Is.EqualTo(88)); Assert.That(target.OcclusionQueries, Is.EqualTo(1));
            Assert.That(beam.QueryWidth, Is.EqualTo(9)); Assert.That(beam.Visuals[0].Width, Is.EqualTo(8));
        }
        [Test] public void BodyToMuzzleWallHidesVisualButDoesNotSkipTheClippedDamageQueryOrRecord()
        {
            var beam = new Beam(); beam.Wall("muzzle", .4f);
            var before = beam.Add(1, .1f); before.Width = .05f; var after = beam.Add(2, 1.5f);
            beam.Fire();
            Assert.That(before.Health, Is.EqualTo(88)); Assert.That(after.Health, Is.EqualTo(100));
            Assert.That(beam.TargetQueries, Is.EqualTo(1)); Assert.That(beam.QueryWidth, Is.EqualTo(.4f - .0015f).Within(.00001f));
            Assert.That(beam.Visuals, Is.Empty); Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 1 }));
        }
        [Test] public void InitialSolidOverlapClampsToZeroYetQueriesTheMinimumWidthAndCountsAnOccludedClaim()
        {
            var beam = new Beam(); beam.Obstacles.Add(new Obstacle("overlap", 0)); var target = beam.Add(1, 0);
            beam.Fire();
            Assert.That(beam.QueryX, Is.Zero); Assert.That(beam.QueryWidth, Is.EqualTo(.001f));
            Assert.That(target.Health, Is.EqualTo(100)); Assert.That(target.OcclusionQueries, Is.EqualTo(1));
            Assert.That(beam.Claims[0].Contains(target.Id), Is.True); Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 1 }));
            Assert.That(beam.Visuals, Is.Empty);
        }
        [Test] public void EuclideanMuzzleGapAndZeroRangeKeepTheOriginalHorizontalQueryAndNoVisibleTail()
        {
            var beam = new Beam(distance: 0) { OriginX = 2, OriginY = -1, MuzzleX = 5, MuzzleY = 3 };
            var target = beam.Add(1, 6, -1); beam.Fire();
            Assert.That(beam.CastLength, Is.EqualTo(5)); Assert.That(beam.QueryX, Is.EqualTo(4.5f)); Assert.That(beam.QueryWidth, Is.EqualTo(5));
            Assert.That(beam.QueryY, Is.EqualTo(-1)); Assert.That(target.Health, Is.EqualTo(88)); Assert.That(beam.Visuals, Is.Empty);
            var point = new Beam(distance: 0) { MuzzleX = 0 }; var atOrigin = point.Add(2, 0); point.Fire();
            Assert.That(point.CastQueries, Is.EqualTo(1)); Assert.That(point.CastLength, Is.Zero); Assert.That(point.QueryWidth, Is.EqualTo(.001f));
            Assert.That(atOrigin.Health, Is.EqualTo(88)); Assert.That(point.Visuals, Is.Empty);
        }
        [Test] public void OccludedFirstCandidateKeepsItsClaimWhenTheWallDisappearsBeforeItsDuplicate()
        {
            var beam = new Beam(); var target = beam.Add(1, 3); beam.Actors.Add(target);
            beam.Callback = name =>
            {
                if (name == "query") beam.Wall("late-wall", 2);
                if (name == "occluded/1") beam.Obstacles.Clear();
            };
            beam.Fire();
            Assert.That(target.Health, Is.EqualTo(100)); Assert.That(target.Attempts, Is.Zero); Assert.That(target.OcclusionQueries, Is.EqualTo(1));
            Assert.That(beam.Claims[0].Count, Is.EqualTo(1)); Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 1 }));
            Assert.That(Effects(beam), Is.EqualTo(new[] { "cast", "begin", "query", "claim/1", "occluded/1", "claim/1", "visual", "record" }));
        }
        [Test] public void CapturedReferencesReadLaterEligibilityAndClosestPointsWithoutRequeryingOrRecheckingTheOwner()
        {
            var beam = new Beam(); var first = beam.Add(1, 1); var killed = beam.Add(2, 2); var player = beam.Add(3, 3);
            var decoy = beam.Add(4, 4); var revived = beam.Add(5, 5); revived.Vitals.WriteHealth(0);
            var movedOut = beam.Add(6, 6); var movedBehind = beam.Add(7, 7); var movedIn = beam.Add(8, 20);
            beam.Callback = name =>
            {
                if (name != "receive/1") return;
                killed.Vitals.WriteHealth(0); player.Player = true; decoy.Decoy = true; revived.Vitals.WriteHealth(100);
                movedOut.X = 40; movedBehind.X = -3; movedIn.X = 2;
                beam.Owner.Vitals.WriteHealth(0); beam.Owner.X = 90;
                beam.Obstacles.Add(new Obstacle("behind", -1)); beam.Now = 9;
            };
            beam.Fire();
            Assert.That(Received(beam), Is.EqualTo(new[] { 1, 5, 6 }));
            Assert.That(first.Health, Is.EqualTo(88)); Assert.That(revived.Health, Is.EqualTo(88)); Assert.That(movedOut.Health, Is.EqualTo(88));
            Assert.That(player.Health, Is.EqualTo(100)); Assert.That(decoy.Health, Is.EqualTo(100)); Assert.That(movedIn.Health, Is.EqualTo(100));
            Assert.That(movedBehind.Health, Is.EqualTo(100)); Assert.That(movedBehind.OcclusionQueries, Is.EqualTo(1));
            Assert.That(beam.ContactX, Is.LessThan(0)); Assert.That(beam.TargetQueries, Is.EqualTo(1));
            Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 4 })); Assert.That(beam.Visuals[0].Until, Is.EqualTo(9 + .1f));
            Assert.That(beam.Visuals[0].X, Is.EqualTo(5));
        }
        [Test] public void DeathDuringOcclusionDoesNotCancelTheAlreadyEnteredBasicReceiveOrRefundItsClaim()
        {
            var beam = new Beam(); var target = beam.Add(1, 2);
            beam.Callback = name => { if (name == "occluded/1") target.Vitals.WriteHealth(0); };
            beam.Fire();
            Assert.That(target.Health, Is.Zero); Assert.That(target.Attempts, Is.EqualTo(1));
            Assert.That(target.Vitals.Read().ReceivedHits, Is.Zero); Assert.That(Received(beam), Is.EqualTo(new[] { 1 }));
            Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 1 })); Assert.That(beam.Visuals.Count, Is.EqualTo(1));
        }
        [Test] public void RejectedReceiveStillConsumesTheClaimAndCountsTheActorWithoutRetryingItsDuplicate()
        {
            var beam = new Beam(); var target = beam.Add(1, 2); target.Reject = true; beam.Actors.Add(target);
            beam.Callback = name => { if (name == "receive/1") target.Reject = false; };
            beam.Fire();
            Assert.That(target.Health, Is.EqualTo(100)); Assert.That(target.Attempts, Is.EqualTo(1));
            Assert.That(target.OcclusionQueries, Is.EqualTo(1)); Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 1 }));
        }
        [Test] public void ReceiveReentryOwnsIndependentCandidateListsAndClaimsAndBothEnteredFiresFinishAfterOwnerDeath()
        {
            var beam = new Beam(); var first = beam.Add(1, 1); var second = beam.Add(2, 2); bool nested = false;
            beam.Callback = name =>
            {
                if (name != "receive/1" || nested) return;
                nested = true; beam.Owner.Vitals.WriteHealth(0); beam.Actors.Clear(); beam.Actors.Add(second); beam.Actors.Add(first); beam.Fire();
            };
            beam.Fire();
            Assert.That(Received(beam), Is.EqualTo(new[] { 1, 2, 1, 2 }));
            Assert.That(first.Health, Is.EqualTo(76)); Assert.That(second.Health, Is.EqualTo(76));
            Assert.That(beam.Claims.Count, Is.EqualTo(2)); Assert.That(beam.Claims[0], Is.Not.SameAs(beam.Claims[1]));
            Assert.That(beam.Claims[0].Count, Is.EqualTo(2)); Assert.That(beam.Claims[1].Count, Is.EqualTo(2));
            Assert.That(beam.CastQueries, Is.EqualTo(2)); Assert.That(beam.TargetQueries, Is.EqualTo(2));
            Assert.That(beam.Visuals.Count, Is.EqualTo(2)); Assert.That(beam.RecordedCounts, Is.EqualTo(new[] { 2, 2 }));
        }
        [TestCase("cast", 100f, 100f, 0, 0, 0)]
        [TestCase("begin", 100f, 100f, 0, 0, 0)]
        [TestCase("query", 100f, 100f, 0, 0, 0)]
        [TestCase("claim/2", 88f, 100f, 2, 0, 0)]
        [TestCase("occluded/2", 88f, 100f, 2, 0, 0)]
        [TestCase("receive/2", 88f, 88f, 2, 0, 0)]
        [TestCase("visual", 88f, 88f, 2, 1, 0)]
        [TestCase("record", 88f, 88f, 2, 1, 1)]
        public void CallbackErrorKeepsExactlyTheEnteredDamageClaimVisualAndRecordPrefix(string stage, float firstHealth,
            float secondHealth, int claims, int visuals, int records)
        {
            var beam = new Beam(); var first = beam.Add(1, 1); var second = beam.Add(2, 2);
            var error = new InvalidOperationException("entered " + stage);
            beam.Callback = name => { if (name == stage) throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => beam.Fire()), Is.SameAs(error));
            Assert.That(first.Health, Is.EqualTo(firstHealth)); Assert.That(second.Health, Is.EqualTo(secondHealth));
            Assert.That(beam.Claims.Count == 0 ? 0 : beam.Claims[0].Count, Is.EqualTo(claims));
            Assert.That(beam.Visuals.Count, Is.EqualTo(visuals)); Assert.That(beam.RecordedCounts.Count, Is.EqualTo(records));
            string[] completed = { "cast", "begin", "query", "claim/1", "occluded/1", "receive/1", "claim/2", "occluded/2", "receive/2", "visual", "record" };
            int count = Array.IndexOf(completed, stage) + 1;
            Assert.That(Effects(beam), Is.EqualTo(new ArraySegment<string>(completed, 0, count)));
            Assert.That(beam.Events[beam.Events.Count - 1], Is.EqualTo(stage));
        }
    }
}
