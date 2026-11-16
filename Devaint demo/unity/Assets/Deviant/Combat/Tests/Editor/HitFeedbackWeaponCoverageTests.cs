using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    [Category("RequiresArt")]
    public sealed class HitFeedbackWeaponCoverageTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();Time.timeScale=1;}
        [UnityTest]public IEnumerator GameThirteenApprovedAttackAppearancesUseTheCapturedNativeHitContext()=>Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest]public IEnumerator LabThirteenApprovedAttackAppearancesUseTheSameBindings()=>Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene);Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=true;
            yield return new EnterPlayMode();yield return null;yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var bridge=arena.GetComponent<HitFeedbackBridge>();Assert.That(bridge.Ready,Is.True,bridge.LastError);
            var floor=new GameObject("Thirteen-form feedback fixture floor");floor.transform.position=new Vector3(0,999.5f,0);floor.AddComponent<ArenaSurface>();var collider=floor.AddComponent<BoxCollider2D>();collider.size=new Vector2(80,1);arena.RegisterFeedbackSurface(collider);
            var target=arena.enemies[0];var buffer=new CombatAcceptedHit[128];int exercised=0;
            using(var driver=arena.AcquireManualSimulation())
            for(int formIndex=0;formIndex<arena.catalog.forms.Length;formIndex++)
            {
                string form=arena.catalog.forms[formIndex].id;int modes=form=="launcher"?4:1;
                for(int mode=0;mode<modes;mode++)
                {
                    driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(35,1000);}
                    var player=arena.Player;player.UseTestInput=true;player.selected=0;player.Position=new Vector2(0,1000);player.SyncFacing(1);
                    Assert.That(player.TryReconfigure(0,formIndex,mode,out var error),Is.True,error);
                    target.maxHealth=target.health=1000;target.shield=0;target.guardUntil=0;target.Position=new Vector2(1.8f,1000);Physics2D.SyncTransforms();
                    arena.TryGetCursor(out var cursor);long started=bridge.StartedHits;
                    driver.Step(.01f,new CombatInput(0,CombatButtons.Basic));
                    CombatAcceptedHit accepted=default;bool found=false;
                    for(int i=0;i<300&&!found;i++)
                    {
                        driver.Step(.01f);arena.Presentation.Draw(0);bridge.Sample();
                        Assert.That(arena.TryRead(cursor,buffer,out var page),Is.True);cursor=page.Next;
                        for(int h=0;h<page.Count;h++)if(buffer[h].Target.ActorId==target.ActorId){accepted=buffer[h];found=true;break;}
                    }
                    string label=form+"/"+mode;
                    Assert.That(found,Is.True,label+" needs a real accepted hit, not a manually fabricated cue.");
                    Assert.That(accepted.Origin.Stamp.FormId,Is.EqualTo(form),label);Assert.That(accepted.Origin.Stamp.WeaponId,Is.EqualTo(player.weapons[0].InstanceId));Assert.That(accepted.Origin.Stamp.Basic,Is.True);
                    Assert.That(accepted.Contact.HasPoint,Is.True,label);Assert.That(bridge.StartedHits,Is.GreaterThan(started),label+": "+bridge.LastError);
                    Assert.That(bridge.LastError,Is.Null,label);Assert.That(player.visual.sprite,Is.Not.Null,label+" actual approved body media.");
                    Assert.That(bridge.Distortion,Is.Not.Null,label);Assert.That(bridge.Distortion.ActiveCount,Is.GreaterThan(0),label+" accepted native attack must request distortion.");
                    Assert.That(target.health,Is.LessThan(1000));Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
                    if(form=="launcher")Assert.That(accepted.Origin.Stamp.Conversion,Is.EqualTo(mode));
                    // Every ink keeps the ball as the launcher basic; the laser is the range-ink D skill.
                    if(form=="launcher")Assert.That(accepted.Contact.Kind,Is.Not.EqualTo(HitAnchorKind.BeamRepresentative),label);
                    long after=bridge.StartedHits;int waves=bridge.Distortion.ActiveCount;bridge.Sample();bridge.Sample();Assert.That(bridge.StartedHits,Is.EqualTo(after));Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(waves));
                    exercised++;
                }
            }
            Assert.That(exercised,Is.EqualTo(13));Object.Destroy(floor);
        }
    }
}
