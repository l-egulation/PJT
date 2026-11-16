using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    [Category("RequiresArt")]
    public class ApprovedElevenWeaponTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation sim;
        CombatPlayer P=>arena.Player;
        readonly string[] forms={"launcher","launcher_laser","launcher_shotgun","launcher_grenade"};
        [UnitySetUp] public IEnumerator Setup()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>();P.UseTestInput=true;sim=arena.AcquireManualSimulation();
        }
        [UnityTearDown] public IEnumerator Teardown(){sim?.Dispose();if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        void Equip(int mode)
        {
            sim.Reset();P.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="launcher"),mode);
            sim.Step(.01f); // Resolve the ground probe after ResetPlayer before checking idle art.
            arena.Presentation.Draw(0);
        }
        void Advance(float seconds)
        {
            int steps=Mathf.CeilToInt(seconds/.01f);
            for(int i=0;i<steps;i++){sim.Step(.01f);arena.Presentation.Draw(.01f);arena.RangedPresentation?.Tick();}
        }
        void Fire(bool skill=false){sim.Step(.01f,new Deviant.Combat.Contracts.CombatInput(pressed:skill?Deviant.Combat.Contracts.CombatButtons.Special:Deviant.Combat.Contracts.CombatButtons.Basic));arena.Presentation.Draw(.01f);arena.RangedPresentation?.Tick();}
        // The ball is every ink's basic attack; laser, shotgun and grenade are that ink's D skill.
        void FireMode(int mode)=>Fire(mode!=0);
        CharacterAnimationSet Set(string form)=>Array.Find(arena.characterAnimations.sets,s=>s.form==form);

        [UnityTest] public IEnumerator AllElevenSetsAndRangedModesUseApprovedNativeArt()
        {
            Assert.That(arena.characterAnimations.sets.Length,Is.EqualTo(13));
            foreach(var set in arena.characterAnimations.sets)
                foreach(string state in new[]{"idle","run","jump_start","jump_rise","jump_apex","fall_start","fall_loop","land","dash","death","attack1"})
                {
                    var clip=set.Find(state);Assert.That(clip,Is.Not.Null,set.form+"/"+state);
                    foreach(var sprite in clip.sprites){Assert.That(sprite,Is.Not.Null);Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));}
                }
            for(int mode=0;mode<4;mode++)
            {
                Equip(mode);Assert.That(arena.Presentation.CurrentForm,Is.EqualTo(forms[mode]));
                Assert.That(P.visual.sprite,Is.EqualTo(Set(forms[mode]).Find("idle").sprites[0]));
                Assert.That(Vector2.Distance(P.visual.sprite.pivot,new Vector2(84,38)),Is.LessThan(.001f));
                Fire();Advance(.03f);Assert.That(arena.Presentation.CurrentBodyClipName,Is.EqualTo("attack_single"));
                CollectionAssert.Contains(Set(forms[mode]).Find("attack_single").sprites,P.visual.sprite);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MuzzleFollowsActualFireAndResetsWithoutDealingDamage()
        {
            foreach(int mode in new[]{0,2,3})
            {
                Equip(mode);FireMode(mode);Advance(.06f);Assert.That(arena.RangedPresentation.ActiveCount,Is.EqualTo(0));
                Advance(mode==3?.15f:.10f);
                var muzzle=GameObject.Find("ApprovedMuzzle_"+forms[mode]);Assert.That(muzzle,Is.Not.Null,forms[mode]);
                if(mode==2)
                {
                    // The shotgun recoil throws the body back: its flash stays where the shot left (approved 2026-11-06).
                    Vector3 left=muzzle.transform.position;Advance(.02f);Assert.That(muzzle.transform.position,Is.EqualTo(left));
                }
                else Assert.That(Vector2.Distance(muzzle.transform.position,arena.RangedPresentation.MuzzlePosition(P,forms[mode])),Is.LessThan(.001f));
                Assert.That(muzzle.GetComponent<Collider2D>(),Is.Null);Assert.That(muzzle.GetComponent<Rigidbody2D>(),Is.Null);
                Assert.That(arena.enemies[0].health,Is.EqualTo(100));
                sim.Reset();Assert.That(arena.RangedPresentation.ActiveCount,Is.Zero);
                yield return null;
            }
        }
        [UnityTest] public IEnumerator LaserCoreAndGlowStopAtWallAndDamageOnlyOnceWhilePoseIsHeld()
        {
            Equip(1);P.Position=new Vector2(6,0);arena.enemies[0].Position=new Vector2(9,0);Physics2D.SyncTransforms();
            Fire(true);Advance(.18f);Assert.That(arena.lasers.Count,Is.EqualTo(1));
            var beam=arena.lasers[0];var core=beam.transform.Find("WhiteBeamCore").GetComponent<SpriteRenderer>();
            Assert.That(core.color,Is.EqualTo(Color.white));Assert.That(core.sprite.rect.height,Is.EqualTo(2));
            Assert.That(beam.GetComponent<SpriteRenderer>().bounds.max.x,Is.LessThanOrEqualTo(12.801f));
            Assert.That(core.bounds.max.x,Is.LessThanOrEqualTo(12.801f));
            var variance=CombatDamageVariance.Settings;float hit=arena.enemies[0].health;
            Assert.That(hit,Is.InRange(100-28*variance.maximum-.001f,100-28*variance.minimum+.001f));
            Advance(.20f);Assert.That(arena.lasers.Count,Is.EqualTo(1));Assert.That(arena.enemies[0].health,Is.EqualTo(hit));
            Advance(.3f);Assert.That(arena.lasers.Count,Is.Zero);Assert.That(arena.enemies[0].health,Is.EqualTo(hit));
            yield return null;
        }
        [UnityTest] public IEnumerator LaserReleaseDurationRetainsCapturedMediaWhenLiveClipChangesBeforeImpact()
        {
            Equip(1);
            var clip=Set("launcher_laser").Find("attack_single");
            float fraction=(clip.sourceRelease-clip.sourceImpact)/Mathf.Max(.001f,clip.Duration-clip.sourceImpact);
            Fire(true);var cast=P.currentCast;Assert.That(cast,Is.Not.Null);
            float expectedDuration=fraction*(cast.MotionDuration-cast.ImpactTime);
            // A started execution keeps its original body and beam release marker.
            // This edit would shorten the live-catalog fraction by roughly tenfold.
            clip.seconds[2]*=10;
            for(int step=0;step<30 && arena.lasers.Count==0;step++)Advance(.01f);
            Assert.That(arena.lasers.Count,Is.EqualTo(1));
            Assert.That(arena.lasers[0].until-arena.Now,Is.InRange(expectedDuration-.0101f,expectedDuration+.0001f),"The fixed 10ms simulation step may finish after the fire callback.");
            Assert.That(arena.Presentation.LastAttackMotionError,Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator GrenadeUsesActualGroundContactAndClearsOnReset()
        {
            Equip(3);var data=arena.catalog.forms[P.Weapon.FormIndex].specials[3].bomb;
            var grenade=BombProjectile.Spawn(arena,P,new Vector2(0,4),Vector2.zero,data,12);
            Assert.That(grenade.transform.Find("ApprovedGrenadeProjectile"),Is.Not.Null);
            Advance(.2f);Assert.That(arena.BombExplosionCount,Is.Zero);
            grenade.SweepTo(new Vector2(0,-1));
            var blast=GameObject.Find("ApprovedGrenadeImpact");Assert.That(blast,Is.Not.Null);
            Assert.That(blast.transform.position.y,Is.EqualTo(0).Within(Physics2D.defaultContactOffset));
            Assert.That(blast.GetComponent<Collider2D>(),Is.Null);
            Assert.That(arena.bombBursts.Count,Is.Zero,"approved grenades do not use the orange placeholder");
            sim.Reset();Assert.That(arena.RangedPresentation.ActiveCount,Is.Zero);Assert.That(arena.bombs.Count,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator BallShotDrawsTheApprovedBodyInsteadOfTheCircle()
        {
            Equip(0);Fire();Advance(.12f);
            Assert.That(arena.playerShots.Count,Is.EqualTo(1));
            Assert.That(arena.playerShots[0].GetComponent<SpriteRenderer>().enabled,Is.False,"the circle stays only as the hit shape");
            Assert.That(GameObject.Find("LauncherSkillFx ball_bullet"),Is.Not.Null);
            Assert.That(GameObject.Find("LauncherSkillFx ball_pop"),Is.Not.Null,"the yellow pop stays at the muzzle");
            // Destroy lands at the end of the frame.
            sim.Reset();yield return null;Assert.That(GameObject.Find("LauncherSkillFx ball_pop"),Is.Null);
        }
        [UnityTest] public IEnumerator LaserStopsAtTheWallWithItsHitAndThinsOutAfterRelease()
        {
            Equip(1);P.Position=new Vector2(6,0);arena.enemies[0].Position=new Vector2(9,0);Physics2D.SyncTransforms();
            Fire(true);Advance(.22f);Assert.That(arena.lasers.Count,Is.EqualTo(1));
            Assert.That(GameObject.Find("LauncherSkillFx laser_wall_hit")??GameObject.Find("LauncherSkillFx laser_wall_hold"),Is.Not.Null,"the beam ends on the wall");
            Assert.That(GameObject.Find("LauncherSkillFx laser_hold"),Is.Not.Null);
            for(int step=0;step<80&&arena.lasers.Count>0;step++)Advance(.01f);
            Assert.That(arena.lasers.Count,Is.Zero);Assert.That(GameObject.Find("LaserVanish"),Is.Not.Null,"pieces scatter as the beam goes out");
            // The pieces run in LateUpdate on the arena clock; the manual simulation needs frames for them.
            Advance(.4f);yield return null;yield return null;Assert.That(GameObject.Find("LaserVanish"),Is.Null);
        }
        [UnityTest] public IEnumerator ConsecutiveShotsUseAcceptedFollowupPosesAndMirrorMuzzles()
        {
            foreach(int mode in new[]{0,2,3})
            {
                Equip(mode);P.SyncFacing(-1);
                for(int hit=1;hit<=3;hit++)
                {
                    Fire();Advance(.04f);
                    Assert.That(arena.Presentation.CurrentBodyClipName,Is.EqualTo(hit==1?"attack_single":"attack"+hit));
                    Assert.That(P.visual.flipX,Is.True);
                    float remaining=P.currentCast.EndTime-arena.Now;Advance(remaining+.01f);
                }
            }
            yield return null;
        }
    }
}
