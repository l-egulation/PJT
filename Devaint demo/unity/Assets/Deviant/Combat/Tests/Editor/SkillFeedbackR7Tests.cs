using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class SkillFeedbackR7Tests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        [UnitySetUp] public IEnumerator Setup()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true; yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Teardown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float end=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<end) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        void Equip(string id,int special)
        { P.Reconfigure(0,System.Array.FindIndex(arena.catalog.forms,f=>f.id==id),special); }
        [UnityTest] public IEnumerator SwordWaveIsVisibleTravelsAlongFloorAndHitsEachEnemyOnce()
        {
            Equip("base_sword",1); arena.enemies[0].Position=new Vector2(0,0); arena.enemies[1].Position=new Vector2(2,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.24f);
            var wave=GameObject.Find("SwordWave"); Assert.That(wave,Is.Not.Null); Assert.That(wave.GetComponent<SpriteRenderer>().sprite,Is.Not.Null);
            float x=wave.transform.position.x; yield return Advance(.08f); Assert.That(wave.transform.position.x,Is.GreaterThan(x));
            yield return Advance(.5f);
            Assert.That(arena.enemies[0].health,Is.EqualTo(60)); Assert.That(arena.enemies[1].health,Is.EqualTo(20));
            Assert.That(GameObject.Find("SwordWave"),Is.Null);
        }
        [UnityTest] public IEnumerator SwordWaveWallBlocksDamageAndCancelAfterReleasePreservesFlight()
        {
            Equip("base_sword",1); P.Position=new Vector2(9,0); arena.enemies[0].Position=new Vector2(15,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.24f); P.CancelAction(); yield return Advance(.3f);
            Assert.That(arena.enemies[0].health,Is.EqualTo(100)); Assert.That(GameObject.Find("SwordWave"),Is.Null);
        }
        [UnityTest] public IEnumerator WhipDoesNotApproachAutomaticallyOrOnEarlyRepress()
        {
            Equip("whip",2); arena.enemies[0].Position=new Vector2(1,0); Physics2D.SyncTransforms(); float x=P.Position.x;
            P.QueueTestInput(special:true); yield return Advance(.06f); P.QueueTestInput(special:true); yield return Advance(.5f);
            Assert.That(P.Position.x,Is.EqualTo(x).Within(.001f)); Assert.That(arena.enemies[0].health,Is.EqualTo(85));
            Assert.That(P.ApproachRemaining,Is.GreaterThan(0));
        }
        [UnityTest, Category("RequiresArt")] public IEnumerator WhipRepressAfterHitUsesDashSpeedAnimationAndNoExtraDamageOrCharge()
        {
            Equip("whip",2); arena.enemies[0].Position=new Vector2(1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f); float x=P.Position.x,ready=P.Weapon.Cooldown.NextReady,at=arena.Now;
            P.QueueTestInput(special:true); yield return Advance(.04f); arena.Presentation.Draw(0);
            Assert.That(P.Position.x-x,Is.EqualTo((arena.Now-at)*15).Within(.025f)); Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("dash"));
            Assert.That(P.DashRemaining,Is.EqualTo(0)); Assert.That(P.Invulnerable,Is.False);
            yield return Advance(.4f);
            Assert.That(P.Position.x,Is.EqualTo(-1.05125f).Within(.04f)); Assert.That(arena.enemies[0].health,Is.EqualTo(85));
            Assert.That(P.Weapon.Cooldown.NextReady,Is.EqualTo(ready)); Assert.That(P.ApproachRemaining,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator WhipRepressWorksAfterRecoveryAndStopsAtSolidWall()
        {
            Equip("whip",2); arena.enemies[0].Position=new Vector2(1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.54f); Assert.That(P.currentCast,Is.Null);
            var wall=new GameObject("TemporaryWall"); wall.transform.position=new Vector2(-2,1.5f);
            wall.AddComponent<ArenaSurface>(); wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,3); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.4f);
            Assert.That(P.bodyCollider.bounds.max.x,Is.LessThanOrEqualTo(-2.099f)); Assert.That(P.ApproachRemaining,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator ExpiredOrDeadWhipMarkCannotBeRecast()
        {
            Equip("whip",2); arena.enemies[0].Position=new Vector2(1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(3.3f); float x=P.Position.x;
            P.QueueTestInput(special:true); yield return Advance(.1f); Assert.That(P.Position.x,Is.EqualTo(x));
            arena.ResetTest(); Equip("whip",2); arena.enemies[0].Position=new Vector2(1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f); arena.enemies[0].health=0;
            P.QueueTestInput(special:true); yield return Advance(.1f); Assert.That(P.Position.x,Is.EqualTo(x));
        }
        [UnityTest] public IEnumerator RapierMovementBlinksInOneFrameToNearestEnemyThenHitsOnlyIt()
        {
            Equip("rapier",2); P.Position=Vector2.zero; arena.enemies[0].Position=new Vector2(5,0); arena.enemies[0].SyncFacing(-1); arena.enemies[1].Position=new Vector2(7,0); Physics2D.SyncTransforms();
            float old=P.Position.x,maxStep=0; P.QueueTestInput(special:true); float until=arena.Now+.2f;
            while(arena.Now<until) { yield return new WaitForFixedUpdate(); maxStep=Mathf.Max(maxStep,Mathf.Abs(P.Position.x-old)); old=P.Position.x; }
            Assert.That(maxStep,Is.GreaterThan(2.5f),"must blink rather than move through several dash frames");
            Assert.That(P.Position.x,Is.EqualTo(2.58875f).Within(.04f)); Assert.That(P.currentCast.Moving,Is.False);
            Assert.That(arena.enemies[0].health,Is.EqualTo(65)); Assert.That(arena.enemies[1].health,Is.EqualTo(60));
        }
        [UnityTest] public IEnumerator RapierDoesNotBlinkWithoutTargetOrThroughWall()
        {
            Equip("rapier",2); P.Position=new Vector2(-15,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.4f); Assert.That(P.Position.x,Is.EqualTo(-15)); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            arena.ResetTest(); Equip("rapier",2); P.Position=new Vector2(10,0); arena.enemies[0].Position=new Vector2(16,0); arena.enemies[1].Position=new Vector2(-30,0); arena.enemies[2].Position=new Vector2(-40,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.4f); Assert.That(P.Position.x,Is.EqualTo(10)); Assert.That(arena.enemies[0].health,Is.EqualTo(100)); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator AirShotgunRecoilLaunchesLowHopWithoutSpendingNormalJump()
        {
            Equip("launcher",2); P.Position=new Vector2(0,8); P.grounded=false; P.velocity=Vector2.zero; Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); float until=arena.Now+.6f; bool launched=false; float at=0,top=0;
            int used=P.jumps.Used;
            while(arena.Now<until)
            {
                yield return new WaitForFixedUpdate();
                if(!launched && arena.PlayerShotCount>0) { launched=true; at=P.Position.y; top=at; Assert.That(P.velocity.y,Is.GreaterThan(0)); }
                if(launched) top=Mathf.Max(top,P.Position.y);
            }
            Assert.That(launched,Is.True); Assert.That(top-at,Is.GreaterThan(.5f)); Assert.That(top-at,Is.LessThanOrEqualTo(1.01f));
            Assert.That(P.Position.x,Is.EqualTo(-4).Within(.03f)); Assert.That(P.jumps.Used,Is.EqualTo(used));
        }
    }
}
