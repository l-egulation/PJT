using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public sealed class RangedActionDefinitionTests
    {
        static Catalog Source()=>JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static RangedActionCatalog Compile(){Assert.That(RangedActionCatalog.TryCreate(Source(),out var value,out var error),Is.True,error);return value;}
        [Test] public void OnlyStraightSpecialsIgnoreTerrainAndThePolicySurvivesEverySnapshot()
        {
            var catalog = Compile(); var restored = RangedActionCatalog.FromSnapshot(catalog.ToSnapshot()); int count = 0;
            foreach (var action in catalog.Actions)
            {
                if (action.ToNumericData().ranged == null) continue;
                var ranged = action.Execution.Ranged;
                bool expected = !action.IsBasic;
                Assert.That(ranged.Projectile.IgnoresTerrain, Is.EqualTo(expected), action.Id);
                Assert.That(restored.Read(action.Id).Execution.Ranged.Projectile.IgnoresTerrain, Is.EqualTo(expected), action.Id);
                Assert.That(action.ToNumericData().ranged.ignoreTerrain, Is.EqualTo(expected), action.Id);
                Assert.That(ranged.Projectile.WithGeometry(1.5f, 2).IgnoresTerrain, Is.EqualTo(expected), action.Id);
                Assert.That(ranged.WithChargeAmount(1).Projectile.IgnoresTerrain, Is.EqualTo(expected), action.Id);
                var changed = action.ToNumericData(); changed.ranged.ignoreTerrain = !expected;
                Assert.Catch(() => action.Recompile(changed), "terrain collision is policy, not a numeric balance edit");
                if (expected) count++;
            }
            Assert.That(count, Is.EqualTo(7));
        }

        [Test] public void UnlimitedLaserAndChargeImpactSurviveSnapshotsAndRejectInvalidGeometry()
        {
            var catalog = Compile();
            var laser = catalog.Read("launcher_range_special");
            Assert.That(laser.Execution.Ranged.Projectile.UnlimitedRange, Is.True);
            Assert.That(laser.Execution.Ranged.Projectile.Distance, Is.Zero);
            var restored = RangedActionCatalog.FromSnapshot(catalog.ToSnapshot());
            Assert.That(restored.Read(laser.Id).ContentHash, Is.EqualTo(laser.ContentHash));
            Assert.That(restored.Read("launcher_basic_special").Execution.Ranged.WithChargeAmount(1).Projectile.ImpactBoxSize, Is.EqualTo(2));
            var invalid = laser.ToNumericData(); invalid.ranged.distance=8; Assert.Catch(()=>laser.Recompile(invalid));
            var charge = catalog.Read("launcher_basic_special");
            invalid=charge.ToNumericData(); invalid.ranged.impactBoxSize=float.PositiveInfinity; Assert.Catch(()=>charge.Recompile(invalid));
            invalid=charge.ToNumericData(); invalid.ranged.unlimitedRange=true; invalid.ranged.distance=0; Assert.Catch(()=>charge.Recompile(invalid));
        }
        [Test] public void BombBasicAndBaseSpecialUseFasterSharedBallistics()
        {
            var catalog=Compile();
            Assert.That(catalog.Read("bomb_basic_0").ToNumericData().bomb.speed,Is.EqualTo(15));
            Assert.That(catalog.Read("bomb_basic_special").ToNumericData().bomb.speed,Is.EqualTo(15));
            Assert.That(catalog.Read("bomb_range_special").ToNumericData().bomb.speed,Is.EqualTo(8));
        }
        [Test] public void AllSixteenClosedRecordsCompileAndSnapshotsPreserveDeepCopiesAndHashes()
        {
            var source=Source();Assert.That(RangedActionCatalog.TryCreate(source,out var catalog,out var error),Is.True,error);
            Assert.That(catalog.Actions.Select(a=>a.Id),Is.EqualTo(RangedActionCatalog.SupportedIds));Assert.That(catalog.Actions.Count,Is.EqualTo(16));
            Assert.That(catalog.Read("launcher_move_special").RuntimeId,Is.EqualTo("launcher_move_special"));Assert.That(catalog.Read("launcher_move_special").IsBasic,Is.False);
            var snapshots=catalog.ToSnapshot();var restored=RangedActionCatalog.FromSnapshot(snapshots);
            Assert.That(restored.Actions.Select(a=>a.ContentHash),Is.EqualTo(catalog.Actions.Select(a=>a.ContentHash)));
            snapshots[0].basic.ranged.speed=999;Assert.That(catalog.Read("launcher_basic_0").ToNumericData().ranged.speed,Is.EqualTo(20));
            source.forms.Single(f=>f.id=="launcher").specials[2].ranged.angles[0]=999;
            Assert.That(catalog.Read("launcher_move_special").ToNumericData().ranged.angles[0],Is.EqualTo(-20));
            var bad=catalog.ToSnapshot();bad[0].contentHash="changed";Assert.Catch(()=>RangedActionCatalog.FromSnapshot(bad));
        }
        [Test] public void NumericChangesUseWholeCandidateGroupsWithoutChangingClosedPolicyOrResourceVersion()
        {
            var original=Compile();var n=original.Read("shuriken_basic_0").ToNumericData();n.ranged.speed=13;n.damages[0]=9;
            var next=original.Edit("shuriken_basic_0",n);
            Assert.That(next.Read("shuriken_basic_1").ToNumericData().ranged.speed,Is.EqualTo(13));Assert.That(next.Read("shuriken_basic_1").ToNumericData().damages[0],Is.EqualTo(original.Read("shuriken_basic_1").ToNumericData().damages[0]));
            Assert.That(next.Read("shuriken_basic_0").ContentHash,Is.Not.EqualTo(original.Read("shuriken_basic_0").ContentHash));Assert.That(next.Read("shuriken_basic_0").SourceVersion,Is.EqualTo(original.Read("shuriken_basic_0").SourceVersion));
            n=original.Read("bomb_basic_0").ToNumericData();n.bomb.speed=7;n.bomb.spawnHeight=1.5f;next=original.Edit("bomb_basic_0",n);
            Assert.That(next.Read("bomb_basic_special").ToNumericData().bomb.speed,Is.EqualTo(7));Assert.That(next.Read("bomb_range_special").ToNumericData().bomb.spawnHeight,Is.EqualTo(1.5f));
            var partial=next.Actions.Take(15).ToArray();Assert.That(original.TryReplace(partial,out _,out _),Is.False);
            var incoherent=original.Actions.ToArray();int shuriken=Array.IndexOf(RangedActionCatalog.SupportedIds.ToArray(),"shuriken_basic_0");incoherent[shuriken]=original.Read("shuriken_basic_0").Recompile(n=original.Read("shuriken_basic_0").ToNumericData());
            n.ranged.speed=17;incoherent[shuriken]=incoherent[shuriken].Recompile(n);Assert.That(original.TryReplace(incoherent,out _,out _),Is.False);
        }
        [Test] public void AllActualConsumedNumericFamiliesAcceptEditsAndRejectPolicyOrNonfinitePayloads()
        {
            var catalog=Compile();
            foreach(var action in catalog.Actions)
            {
                var n=action.ToNumericData();n.damages[0]+=1;if(n.damages.Length>1)for(int i=1;i<n.damages.Length;i++)n.damages[i]=n.damages[0];
                var changed=action.Recompile(n);Assert.That(changed.ContentHash,Is.Not.EqualTo(action.ContentHash),action.Id);
                n=action.ToNumericData();n.activation=float.NaN;Assert.Catch(()=>action.Recompile(n),action.Id);
            }
            var pellet=catalog.Read("launcher_move_special");var p=pellet.ToNumericData();p.ranged.angles=new[]{-15f,0,15f};p.ranged.recoilHopHeight=1.25f;p.ranged.recoilDistance=3;
            Assert.That(pellet.Recompile(p).Execution.Ranged.Angles.Count,Is.EqualTo(3));
            p.ranged.type="laser";Assert.Catch(()=>pellet.Recompile(p));
            p=pellet.ToNumericData();p.bomb=new BombData{speed=3};Assert.Catch(()=>pellet.Recompile(p));
            p=pellet.ToNumericData();p.missile=new MissileData{speed=3};Assert.Catch(()=>pellet.Recompile(p));
            var fragment=catalog.Read("launcher_followup_special");var b=fragment.ToNumericData();b.bomb.fragmentAngles=new[]{45f,135f};b.bomb.fragmentDamage=8;
            Assert.That(fragment.Recompile(b).Execution.Bomb.Projectile.FragmentCount,Is.EqualTo(2));
            var timed=catalog.Read("bomb_followup_special");var t=timed.ToNumericData();t.bomb.fuseSeconds=.75f;Assert.That(timed.Recompile(t).Execution.Bomb.Projectile.FuseSeconds,Is.EqualTo(.75f));
            var visit=catalog.Read("shuriken_move_special");var v=visit.ToNumericData();v.ranged.returnDelay=2;v.ranged.behindGap=.5f;Assert.That(visit.Recompile(v).Execution.Ranged.Projectile.ReturnDelay,Is.EqualTo(2));
            var charged=catalog.Read("launcher_basic_special");var c=charged.ToNumericData();Assert.That(c.ranged.chargeRadius,Is.EqualTo(.3f));c.ranged.chargeRadius=.05f;Assert.Catch(()=>charged.Recompile(c),"the full-charge size cannot shrink below the minimum");
            c=charged.ToNumericData();c.damages[0]=charged.Execution.ChargeMaxDamage+1;Assert.Catch(()=>charged.Recompile(c),"the minimum charge damage cannot exceed the maximum");
            var periodic=catalog.Read("shuriken_followup_special");var f=periodic.ToNumericData();f.ranged.hitInterval=.125f;Assert.That(periodic.Recompile(f).Execution.Ranged.Projectile.HitInterval,Is.EqualTo(.125f));
        }
    }
}
