using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class RangedActionApplicationTests
    {
        static readonly MethodInfo Basic=typeof(CombatPlayer).GetMethod("BeginBasic",BindingFlags.NonPublic|BindingFlags.Instance);
        static readonly MethodInfo Special=typeof(CombatPlayer).GetMethod("BeginSpecial",BindingFlags.NonPublic|BindingFlags.Instance);
        [UnityTearDown] public IEnumerator TearDown(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator GameAllSixteenActionsAdoptNextCandidateWithoutRetargetingRunningOutput()=>Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabAllSixteenActionsAdoptNextCandidateWithoutRetargetingRunningOutput()=>Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Start(CombatArena arena,RangedActionDefinition action)
        {
            var p=arena.Player;var command=new CombatCommandKey(new CombatActorHandle(p.InputSession,p.ActorId),100,action.IsBasic?CombatCommandKind.Basic:CombatCommandKind.Special);
            try{if(action.IsBasic)Basic.Invoke(p,new object[]{command,action.FormId=="launcher"?0:action.Selection,-1f});else Special.Invoke(p,new object[]{command,-1f});}
            catch(TargetInvocationException error){throw error.InnerException;}
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene);Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;yield return new EnterPlayMode();yield return null;yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var floor=new GameObject("RangedCandidateFloor");floor.transform.position=new Vector3(0,999.5f,0);floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
            try
            {
                using(var driver=arena.AcquireManualSimulation())
                {
                    var originals=arena.RangedActions.ToArray();
                    foreach(var action in originals)
                    {
                        Assert.That(arena.TryApplyRangedActions(originals,true,out var error),Is.True,error);
                        foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,1000);}
                        var player=arena.Player;player.selected=0;foreach(var weapon in player.weapons)weapon.StatSlots.Clear();
                        Assert.That(player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,form=>form.id==action.FormId),action.FormId=="launcher"?action.Selection:action.IsBasic?0:action.Selection,out error),Is.True,error);
                        player.Position=new Vector2(0,1000);player.velocity=Vector2.zero;player.grounded=true;Physics2D.SyncTransforms();
                        Start(arena,action);var current=player.currentCast;Assert.That(current.RangedAction.Id,Is.EqualTo(action.Id));Assert.That(current.DefinitionVersion,Is.EqualTo(action.ContentHash));
                        var payload=current.Definition;var numeric=action.ToNumericData();for(int i=0;i<numeric.damages.Length;i++)numeric.damages[i]+=3;
                        if(numeric.ranged!=null&&numeric.ranged.type!="laser")numeric.ranged.speed+=1;
                        var snapshots=RangedActionCatalog.FromSnapshot(originals.Select(value=>value.ToSnapshot()).ToArray());var changed=snapshots.Edit(action.Id,numeric);
                        Assert.That(arena.TryApplyRangedActions(changed.Actions,false,out error),Is.True,error);
                        Assert.That(current.Definition,Is.SameAs(payload));Assert.That(current.DefinitionVersion,Is.EqualTo(action.ContentHash));
                        Assert.That(arena.TryApplyRangedActions(changed.Actions.Take(14).ToArray(),false,out _),Is.False);Assert.That(arena.GetRangedAction(action.Id).ContentHash,Is.EqualTo(changed.Read(action.Id).ContentHash));
                        driver.Step(.015625f);player.CancelAction();Start(arena,changed.Read(action.Id));Assert.That(player.currentCast.DefinitionVersion,Is.EqualTo(changed.Read(action.Id).ContentHash));
                        Assert.That(player.currentCast.Definition.Hits[0].Damage,Is.EqualTo(payload.Hits[0].Damage+3));
                    }
                    Assert.That(arena.TryApplyRangedActions(originals,true,out _),Is.True);
                }
            }
            finally{Object.Destroy(floor);}
        }
        [UnityTest] public IEnumerator ReleasedShotAndBombKeepOriginalLaunchValuesAfterPublicationAndSwap()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();
            using(var driver=arena.AcquireManualSimulation())
            {
                var originals=arena.RangedActions.ToArray();
                foreach(string id in new[]{"shuriken_basic_special","bomb_followup_special"})
                {
                    driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,1000);}var p=arena.Player;p.selected=0;
                    var source=arena.GetRangedAction(id);Assert.That(p.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id==source.FormId),source.Selection,out var error),Is.True,error);
                    p.Position=new Vector2(0,1005);p.velocity=Vector2.zero;Physics2D.SyncTransforms();Start(arena,source);
                    for(int tick=0;tick<16;tick++)driver.Step(.015625f);
                    var shot=arena.playerShots.FirstOrDefault();var bomb=arena.bombs.FirstOrDefault();var n=source.ToNumericData();
                    if(n.ranged!=null)n.ranged.speed+=4;else n.bomb.fuseSeconds+=1;
                    var catalog=RangedActionCatalog.FromSnapshot(originals.Select(a=>a.ToSnapshot()).ToArray()).Edit(id,n);Assert.That(arena.TryApplyRangedActions(catalog.Actions,false,out error),Is.True,error);
                    p.CancelAction();p.selected=1;
                    if(shot){float speed=shot.velocity.magnitude;driver.Step(.015625f);Assert.That(shot.velocity.magnitude,Is.EqualTo(speed));Assert.That(shot.Definition.Distance,Is.EqualTo(source.Execution.Ranged.Projectile.Distance));}
                    if(bomb)Assert.That(bomb.Definition.FuseSeconds,Is.EqualTo(source.Execution.Bomb.Projectile.FuseSeconds));
                    Assert.That(arena.TryApplyRangedActions(originals,true,out error),Is.True,error);
                }
            }
        }
    }
}
