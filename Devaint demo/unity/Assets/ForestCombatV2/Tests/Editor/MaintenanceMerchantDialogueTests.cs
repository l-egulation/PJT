using System;
using System.Collections;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ForestCombatV2.Tests
{
    public sealed class MaintenanceMerchantDialogueTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(EditorApplication.isPlaying)yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ReturningMerchantTalkDoesNotOpenAStoreOrTeachThePortalAgain()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                Chapter1ProfileHost.Service.MarkStorySeen("maintenance-intro-chapter1");
                Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
                Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
                SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);
                var talkType=Type.GetType("Deviant.Game.MaintenanceRoomPresentation, Assembly-CSharp",true);
                var shopType=Type.GetType("Deviant.Game.CommonMaintenanceShop, Assembly-CSharp",true);
                ForestCombatWorld world=null;Component talk=null;
                for(int i=0;i<900;i++)
                {
                    yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();
                    talk=world?world.GetComponent(talkType):null;
                    if(world&&world.Ready&&talk&&(bool)talkType.GetField("built",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(talk))break;
                }
                Assert.That(world&&world.Ready&&talk,Is.True);
                using(var driver=world.arena.AcquireManualSimulation())
                {
                    var position=(Vector2)talkType.GetProperty("ReviewAnchor").GetValue(talk);
                    world.arena.Player.Position=position+Vector2.right*1.5f;world.arena.Player.grounded=true;
                    foreach(var enemy in world.arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
                    Physics2D.SyncTransforms();
                    bool portalBefore=Chapter1ProfileHost.Service.Snapshot.HasSeen(Chapter1ManualCheckpoint.PortalTaughtStory);
                    int gold=world.arena.Sketch.Economy.Gold;
                    var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(x=>x.GetType().Name=="TemporaryGameGui");
                    var marker=gui.GetComponentsInChildren<Image>(true).First(x=>x.name=="Merchant interact key");
                    Assert.That(marker.type,Is.EqualTo(Image.Type.Sliced));
                    Assert.That(marker.GetComponentInChildren<Text>().text,Is.EqualTo("F"));
                    Assert.That(marker.rectTransform.sizeDelta.y,Is.EqualTo(30));
                    var shop=world.GetComponent(shopType);
                    Assert.That((bool)shopType.GetMethod("TryOpen").Invoke(shop,null),Is.True,"the real interaction adapter starts a conversation");
                    Assert.That((bool)shopType.GetProperty("IsOpen").GetValue(shop),Is.False,"no legacy store tabs are opened");
                    Assert.That(talkType.GetProperty("Phase").GetValue(talk).ToString(),Is.EqualTo("Dialogue"));
                    StringAssert.Contains("물건",(string)talkType.GetProperty("CurrentText").GetValue(talk));
                    Assert.That((bool)shopType.GetMethod("TryOpen").Invoke(shop,null),Is.False,"repeated interaction cannot nest conversations");
                    talkType.GetMethod("Advance").Invoke(talk,null);talkType.GetMethod("Advance").Invoke(talk,null);
                    Assert.That(talkType.GetProperty("Phase").GetValue(talk).ToString(),Is.EqualTo("Free"));
                    Assert.That((bool)talkType.GetProperty("Capturing").GetValue(talk),Is.False);
                    Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen(Chapter1ManualCheckpoint.PortalTaughtStory),Is.EqualTo(portalBefore));
                    Assert.That(world.arena.Sketch.Economy.Gold,Is.EqualTo(gold));
                }
            }
            finally { }
            yield return new ExitPlayMode();
        }
    }
}
