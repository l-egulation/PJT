using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Equipment;
using ForestCombatV2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class InkPartCombatTests
    {
        [Test] public void PaletteCommandsAreAtomicIndependentAndSnapshotsSurviveReplacement()
        {
            var a = WeaponResources.CreateWeapon(); var b = WeaponResources.CreateWeapon();
            Assert.That(a.InkPalette.TrySet(0,"r_split",false,out _),Is.True);
            var before = a.InkPalette.Read();
            Assert.That(a.InkPalette.TrySet(1,"r_split",false,out _),Is.False);
            Assert.That(a.InkPalette.TrySet(6,"r_echo",true,out _),Is.False);
            Assert.That(a.InkPalette.Read().Slots[0],Is.SameAs(before.Slots[0]));
            Assert.That(a.InkPalette.TrySet(0,"r_echo",true,out _),Is.True);
            Assert.That(a.InkPalette.TrySet(1,"r_echo",true,out _),Is.True);
            Assert.That(a.InkPalette.Read().Slots[0].InstanceId,Is.Not.EqualTo(a.InkPalette.Read().Slots[1].InstanceId));
            Assert.That(before.Slots[0].PartId,Is.EqualTo("r_split"));
            Assert.That(b.InkPalette.Read().Slots.All(p=>p==null),Is.True);
            a.InkPalette.Clear(); Assert.That(a.InkPalette.Read().Slots.All(p=>p==null),Is.True);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(Application.isPlaying) yield return new ExitPlayMode();
            DummyTrainingTools.SelectNormalMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        static void Tick(CombatArena.ManualSimulation sim,float seconds)
        { for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++) sim.Step(.02f); }
        static List<CombatAcceptedHit> Read(CombatArena arena,ref CombatOutcomeCursor cursor)
        {
            var hits=new List<CombatAcceptedHit>(); var buffer=new CombatAcceptedHit[128];
            while(true)
            {
                Assert.That(arena.HitEvents.TryRead(cursor,buffer,out var page),Is.True);
                Assert.That(page.DroppedBefore,Is.Zero);cursor=page.Next;
                for(int i=0;i<page.Count;i++) hits.Add(buffer[i]);
                if(page.Count<buffer.Length)return hits;
            }
        }
        static CombatHitOrigin Origin(CombatArena arena,int slot=0,bool basic=true)
        {
            Assert.That(arena.TryBindActor(arena.Player,out var source),Is.True);
            var weapon=arena.Player.weapons[slot];
            return new CombatHitOrigin(source,default,Guid.NewGuid(),0,"fixture",Guid.Empty,CombatHitKind.Melee,
                stamp:new CombatAttackStamp(weapon.InstanceId,"base_sword","fixture","test",basic,-1,inkPalette:weapon.InkPalette.Read()),sourceFacing:1);
        }
        static void Equip(DummyTrainingRoom room,CombatArena arena,params string[] ids)
        {
            room.SetOpen(true);
            for(int i=0;i<6;i++)Assert.That(room.ApplyInkPart(0,i,i<ids.Length?ids[i]:null,out var error),Is.True,error);
            room.SetOpen(false);
        }
        [UnityTest] public IEnumerator SharedHitsSnapshotsResetAndEveryImplementedEffect()
        {
            DummyTrainingTools.Prepare();
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;
            var room=world.GetComponent<DummyTrainingRoom>(); var target=room.Targets[2];
            using(var sim=arena.AcquireManualSimulation())
            {
                Assert.That(arena.LastStartedInkWeaponId,Is.EqualTo(arena.Player.weapons[0].InstanceId));
                room.SetOpen(true);
                Assert.That(room.ApplyInkPart(0,0,"g_armor",out _),Is.False,"unimplemented candidates cannot become equipment");
                room.SetOpen(false);
                Equip(room,arena,"r_echo","r_echo");
                var echo=Origin(arena); float hp=target.health;
                Assert.That(target.Receive(10,arena.Player,1,true,null,origin:echo),Is.True);
                sim.Step(.02f);
                // Replacing the source after the hit cannot change already emitted effects.
                Equip(room,arena);
                arena.TryGetCursor(out var cursor);Tick(sim,.6f);
                var hits=Read(arena,ref cursor);
                Assert.That(hp-target.health,Is.EqualTo(26).Within(.001));
                Assert.That(hits.Count(h=>h.Origin.Kind==CombatHitKind.InkPart),Is.EqualTo(2));
                Assert.That(target.Meter.Total,Is.EqualTo(26).Within(.001));
                target.Restore();
                Equip(room,arena,"r_burn");var burn=Origin(arena);
                target.Receive(10,arena.Player,1,true,null,origin:burn);Tick(sim,3.2f);
                Assert.That(target.health,Is.EqualTo(981).Within(.001),"three independent 3-damage ticks, no recursive burn");
                target.Restore();
                target.Receive(10,arena.Player,1,true,null,origin:Origin(arena));sim.Step(.02f);
                target.Restore();float now=arena.Now;Tick(sim,3.2f);
                Assert.That(target.health,Is.EqualTo(1000));Assert.That(target.Meter.Total,Is.Zero);
                Assert.That(arena.Now,Is.GreaterThan(now));
                Equip(room,arena,"r_echo");
                var regular=arena.SpawnEnemy(0,new Vector2(30,100));regular.aggressive=false;
                regular.Receive(10,arena.Player,1,true,null,origin:Origin(arena));Tick(sim,.6f);
                Assert.That(regular.health,Is.EqualTo(82).Within(.001),"ordinary enemy uses the same effect executor");
                regular.ResetEnemy();
                regular.Receive(10,arena.Player,1,true,null,origin:Origin(arena));sim.Step(.02f);
                regular.ResetEnemy();Tick(sim,.6f);
                Assert.That(regular.health,Is.EqualTo(100),"old delayed hit cannot damage a respawned enemy");
                Equip(room,arena,"r_echo");room.SetOpen(true);
                Assert.That(room.ApplyInkPart(1,0,"r_echo",out var independentError),Is.True,independentError);room.SetOpen(false);
                var other=room.Targets[3];other.Restore();target.Restore();
                target.Receive(10,arena.Player,1,true,null,origin:Origin(arena));
                other.Receive(10,arena.Player,1,true,null,origin:Origin(arena,1));sim.Step(.02f);
                target.Restore();Tick(sim,.6f);
                Assert.That(target.health,Is.EqualTo(1000));Assert.That(other.health,Is.EqualTo(982));
                Assert.That(other.Meter.Total,Is.EqualTo(18),"resetting one life preserves the other weapon's pending damage and meter");
                Equip(room,arena,"r_split"); target.Restore();
                var split=Origin(arena);target.Receive(20,arena.Player,1,true,null,origin:split);
                target.Receive(20,arena.Player,1,true,null,origin:split);Tick(sim,.3f);
                Assert.That(target.health,Is.EqualTo(950).Within(.001),"one pair per action even for multiple hits");
                Equip(room,arena,"r_chase");target.Restore();var chase=Origin(arena);
                for(int i=0;i<3;i++)target.Receive(1,arena.Player,1,true,null,origin:chase);
                sim.Step(.02f);Assert.That(target.health,Is.EqualTo(990).Within(.001));
                Equip(room,arena,"g_leech");target.Restore();target.health=5;arena.Player.health=50;
                target.Receive(1000,arena.Player,1,true,null,origin:Origin(arena));sim.Step(.02f);
                Assert.That(arena.Player.health,Is.EqualTo(50.4f).Within(.001),"leech uses five lost HP, not overkill");
                target.Restore();target.shield=100;arena.Player.health=50;
                target.Receive(10,arena.Player,1,true,null,origin:Origin(arena));sim.Step(.02f);
                Assert.That(arena.Player.health,Is.EqualTo(50));Assert.That(target.Meter.Total,Is.Zero);
                target.Restore();
                // Pack spacing is preserved. Move test-only target positions to exercise area targeting.
                var group=room.Targets.Skip(3).ToArray();foreach(var enemy in group)enemy.Restore();
                target.Position=new Vector2(4,100);for(int i=0;i<group.Length;i++)group[i].Position=new Vector2(4.3f+i*.3f,100);
                Equip(room,arena,"r_shard");var shard=Origin(arena);
                target.Receive(1,arena.Player,1,true,null,origin:shard);sim.Step(.02f);
                Assert.That(target.health,Is.EqualTo(999));Assert.That(group[0].health,Is.EqualTo(993));
                Assert.That(group[1].health,Is.EqualTo(993));Assert.That(group[2].health,Is.EqualTo(1000));
                foreach(var enemy in group)enemy.Restore();target.Restore();
                target.Position=new Vector2(4,100);for(int i=0;i<group.Length;i++)group[i].Position=new Vector2(4.3f+i*.3f,100);
                Equip(room,arena,"r_blast");
                target.Receive(1,arena.Player,1,true,null,origin:Origin(arena));sim.Step(.02f);
                Assert.That(group[0].health,Is.EqualTo(1000),"blast is special-only");
                var blast=Origin(arena,basic:false);
                target.Receive(1,arena.Player,1,false,null,origin:blast);group[0].Receive(1,arena.Player,1,false,null,origin:blast);sim.Step(.02f);
                Assert.That(group[2].health,Is.EqualTo(993),"one explosion at the first accepted target");
            }
        }
        [UnityTest] public IEnumerator RealSwordStartsCapturePaletteAndRejectedInputCannotSwitchPassiveOwner()
        {
            DummyTrainingTools.Prepare();Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;
            var room=world.GetComponent<DummyTrainingRoom>();var player=arena.Player;var target=room.Targets[2];
            using(var sim=arena.AcquireManualSimulation())
            {
                room.SetOpen(true);
                int sword=Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword");
                Assert.That(room.ApplyEquipment(0,sword,0,new int[5],out var error),Is.True,error);
                Assert.That(room.ApplyEquipment(1,sword,0,new int[5],out error),Is.True,error);
                room.SetOpen(false);
                Equip(room,arena,"r_echo");
                player.Position=target.Position+Vector2.left*1.2f;player.SyncFacing(1);Physics2D.SyncTransforms();
                arena.TryGetCursor(out var cursor);
                sim.Step(.02f,new CombatInput(0,CombatButtons.Basic));Tick(sim,1.2f);
                var hits=Read(arena,ref cursor);
                Assert.That(hits.Any(h=>h.Origin.Kind==CombatHitKind.InkPart),Is.True,"actual shared sword origin carries its palette");
                Assert.That(arena.LastStartedInkWeaponId,Is.EqualTo(player.weapons[0].InstanceId));
                // A raw equipment selection is not an attack start.
                player.selected=1;
                Assert.That(arena.LastStartedInkWeaponId,Is.EqualTo(player.weapons[0].InstanceId));
                sim.Step(.02f,new CombatInput(0,CombatButtons.Basic));
                Assert.That(arena.LastStartedInkWeaponId,Is.EqualTo(player.weapons[1].InstanceId));
                // No charge means this request is rejected before switching from B to A.
                while(player.weapons[0].Cooldown.Spend(arena.Now)) { }
                sim.Step(.02f,new CombatInput(0,CombatButtons.Weapon1Special));
                Assert.That(arena.LastStartedInkWeaponId,Is.EqualTo(player.weapons[1].InstanceId));
                room.SetOpen(true);float paused=arena.Now;Assert.Throws<InvalidOperationException>(()=>sim.Step(.02f));
                Assert.That(arena.Now,Is.EqualTo(paused));room.SetOpen(false);
                sim.Reset();Assert.That(arena.LastStartedInkWeaponId,Is.EqualTo(player.weapons[0].InstanceId));
            }
        }
    }
}
