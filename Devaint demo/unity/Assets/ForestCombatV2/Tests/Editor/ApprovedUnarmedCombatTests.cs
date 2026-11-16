using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedUnarmedCombatTests
    {
        [Test] public void NativeAttackSpritesMatchIdleHeightAndFeetWithoutResampling()
        {
            var idle = Resources.Load<Sprite>("UnarmedBody/idle/F01");
            var attack = Resources.Load<Sprite>("UnarmedAttack/jab-combo/frame-09");
            var crescent = Resources.Load<Sprite>("UnarmedAttack/jab-fx/jab/01");
            Assert.That(idle && attack && crescent, Is.True);
            Assert.That(attack.rect.size, Is.EqualTo(new Vector2(640,360)));
            Assert.That(attack.pixelsPerUnit, Is.EqualTo(106.6667f).Within(.001f));
            Assert.That(attack.pivot.x, Is.EqualTo(220).Within(.01));
            Assert.That(attack.pivot.y, Is.EqualTo(33).Within(.01));
            // The jab cells were matched to the idle body drawn 10/3 larger, at the same feet.
            Assert.That(attack.pixelsPerUnit/idle.pixelsPerUnit, Is.EqualTo(10f/3f).Within(.001f));
            Assert.That(crescent.pixelsPerUnit, Is.EqualTo(32));
        }

        [UnityTest] public IEnumerator DeparturePlaysTwoSeparateContactsAndFinishesAtApprovedCycle()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i=0;i<600 && (!world || !world.Ready);i++)
            { yield return null; world=Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            Assert.That(world.GetComponent<Chapter1StartingBlueprints>().ApplyUnarmedDeparture(), Is.True);
            yield return null;
            var arena=world.arena; var player=arena.Player;
            var guide=world.GetComponent<Chapter1FirstRunInkGuide>();
            if(guide) { guide.FinishNotebook(); guide.enabled=false; }
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b=>b.GetType().Name=="TemporaryGameGui");
            if(gui) gui.GetType().GetMethod("DismissDepartureCard")?.Invoke(gui,null);
            foreach(var enemy in arena.enemies) if(enemy) { enemy.aggressive=false; enemy.Position+=Vector2.right*1000; }
            using(var driver=arena.AcquireManualSimulation())
            {
                for(int i=0;i<100 && !player.grounded;i++) driver.Step(.01f);
                Assert.That(player.Form.id,Is.EqualTo("unarmed"));
                Assert.That(player.Form.basics.Length,Is.EqualTo(2));
                Assert.That(player.BasicSpeed,Is.EqualTo(1));
                player.SyncFacing(1);
                var before=player.Position;
                var near=arena.SpawnEnemy(0,before+Vector2.right*1.2f);
                var far=arena.SpawnEnemy(0,before+Vector2.right*3);
                near.aggressive=far.aggressive=false;
                near.health=far.health=1000;
                Physics2D.SyncTransforms();
                driver.Step(.01f,new CombatInput(0,CombatButtons.Weapon1Basic));
                var first=player.currentCast;
                Assert.That(first,Is.Not.Null);
                Assert.That(first.Definition.Id,Is.EqualTo("unarmed_basic_0"));
                Assert.That(first.Definition.Hits[0].Damage,Is.EqualTo(8));
                Assert.That(first.EndTime-first.StartedAt,Is.EqualTo(.36f).Within(.00001));
                for(int i=0;i<25;i++) driver.Step(.01f);
                Assert.That(near.ReceivedHitCount,Is.EqualTo(1),"first contact hits once");
                Assert.That(far.ReceivedHitCount,Is.Zero,"out-of-range target stays untouched");
                // Manual simulation advances combat; the render pass has its own clock.
                arena.Presentation.Draw(0);
                near.Position=before+Vector2.right*1.2f;
                near.velocity=Vector2.zero;
                Physics2D.SyncTransforms();
                Assert.That(arena.Presentation.LastAttackMotionError,Is.Null);
                Assert.That(player.visual.sprite.pixelsPerUnit,Is.EqualTo(106.6667f).Within(.001f));
                Assert.That(player.visual.sprite.name,Does.StartWith("frame-"),"the straight-jab body art is drawn");
                driver.Step(.01f,new CombatInput(0,CombatButtons.Weapon1Basic));
                for(int i=0;i<15 && player.currentCast==first;i++) driver.Step(.01f);
                var second=player.currentCast;
                Assert.That(second,Is.Not.Null.And.Not.SameAs(first));
                Assert.That(second.Definition.Id,Is.EqualTo("unarmed_basic_1"));
                Assert.That(second.Definition.Hits[0].Damage,Is.EqualTo(10));
                Assert.That(second.StartedAt,Is.EqualTo(first.EndTime).Within(.00001));
                Assert.That(second.EndTime-first.StartedAt,Is.EqualTo(.68f).Within(.00001));
                Assert.That(arena.Presentation.LastAttackMotionError,Is.Null);
                for(int i=0;i<40;i++) driver.Step(.01f);
                Assert.That(near.ReceivedHitCount,Is.EqualTo(2),"second contact is a separate hit");
                Assert.That(far.ReceivedHitCount,Is.Zero);
                near.Position+=Vector2.right*1000;
                Assert.That(player.currentCast,Is.Null);
                Assert.That(player.Position.x,Is.EqualTo(before.x).Within(.002),"neutral input does not slide forward");
                driver.Step(.01f,new CombatInput(0,CombatButtons.Weapon1Special));
                Assert.That(player.currentCast,Is.Null,"bare hands have no special");
                // Outside the existing .30s grace, a new press restarts at hit 1.
                for(int i=0;i<32;i++) driver.Step(.01f);
                driver.Step(.01f,new CombatInput(1,CombatButtons.Weapon1Basic));
                Assert.That(player.currentCast.Definition.Id,Is.EqualTo("unarmed_basic_0"));
                float start=player.Position.x;
                for(int i=0;i<14;i++) driver.Step(.01f,new CombatInput(1,CombatButtons.None));
                // Measure the authored step before ordinary movement resumes.
                Assert.That(player.Position.x-start,Is.EqualTo(.2f).Within(.002));
            }
            yield return new ExitPlayMode();
        }
    }
}
