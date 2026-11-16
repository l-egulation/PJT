using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class MonsterHealthBarTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {if(Application.isPlaying)yield return new ExitPlayMode();}

        [UnityTest] public IEnumerator RandomStageEnemyBarsContainNoHealthTextAndTrackHealth()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().FullName=="Deviant.Game.TemporaryGameGui");
            var target=world.arena.enemies.First(e=>e&&!e.Dead&&!e.isBoss);
            foreach(var enemy in world.arena.enemies)enemy.aggressive=false;
            world.arena.Player.Position=target.Position+Vector2.left;
            world.RefreshCamera(0,true);
            Physics2D.SyncTransforms();
            yield return null;
            var refresh=gui.GetType().GetMethod("RefreshEnemyHealth",BindingFlags.Instance|BindingFlags.NonPublic);
            var bars=(IEnumerable)gui.GetType().GetField("enemyBars",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gui);
            int Visible(){int n=0;foreach(var bar in bars)if(((Component)bar.GetType().GetField("Track").GetValue(bar)).gameObject.activeInHierarchy)n++;return n;}
            // Only an enemy hit in the last three seconds shows its bar (art review H-8, 2026-10-30).
            refresh.Invoke(gui,null);
            Assert.That(Visible(),Is.EqualTo(0),"No bar before any hit.");
            target.Receive(1,world.arena.Player,1,false,null,true);
            target.health=target.maxHealth*.5f;
            refresh.Invoke(gui,null);
            int visible=0;bool half=false;
            foreach(var bar in bars)
            {
                var track=(Component)bar.GetType().GetField("Track").GetValue(bar);
                if(!track.gameObject.activeInHierarchy)continue;
                visible++;
                Assert.That(track.GetComponentsInChildren<Component>(true).Any(c=>c.GetType().Name=="Text"),Is.False);
                var fill=(Component)bar.GetType().GetField("Fill").GetValue(bar);
                if(Mathf.Abs(((RectTransform)fill.transform).rect.width-22)<.1f)half=true;
            }
            Assert.That(visible,Is.EqualTo(1),"Only the monster just hit shows a bar.");
            Assert.That(half,Is.True,"The hit half-health monster retains a proportional bar.");
        }
    }
}
