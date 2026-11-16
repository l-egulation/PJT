using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Ordinary jumps hold framing; stairs and actual height transitions still ease to their new height.
    public sealed class Chapter1CameraFollowTests
    {
        const string SettingsKey="Chapter1CameraFollowTests.PlayOptions";
        [SetUp] public void PreservePlayOptions()
        {
            SessionState.SetBool(SettingsKey,EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(SettingsKey+".Flags",(int)EditorSettings.enterPlayModeOptions);
        }
        [TearDown] public void RestorePlayOptions()
        {
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(SettingsKey+".Flags",0);
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(SettingsKey,false);
        }
        [UnityTest] public IEnumerator StairsEaseToNewHeightButOrdinaryJumpHoldsTheView()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null;
            var player = world.arena.Player;
            foreach (var enemy in world.arena.enemies) if (enemy) enemy.aggressive = false;
            world.CameraPresentationPosition = null;
            player.velocity = Vector2.zero; player.grounded = true; world.RefreshCamera(0, true);

            // Walking up four 32px steps: the view never jumps more than a few pixels a frame and always moves up, never dips.
            float start = world.CameraY, previous = start, worst = 0;
            bool dipped = false;
            for (int step = 1; step <= 4; step++)
            {
                player.Position += Vector2.up;
                for (int i = 0; i < 15; i++)
                {
                    world.RefreshCamera(1f / 60);
                    worst = Mathf.Max(worst, Mathf.Abs(world.CameraY - previous));
                    if (world.CameraY > previous + .01f) dipped = true;
                    previous = world.CameraY;
                }
            }
            Assert.That(worst, Is.LessThan(6), "no jerk per frame while climbing");
            Assert.That(dipped, Is.False, "climbing never pulls the view downward");
            for (int i = 0; i < 180; i++) world.RefreshCamera(1f / 60);
            Assert.That(start - world.CameraY, Is.EqualTo(128).Within(1f), "settles on the new floor");

            // A complete 96px jump and its descent leave the camera exactly on the grounded framing.
            world.RefreshCamera(0, true);
            float ground = world.CameraY, peakLag = 0;
            var origin = player.Position; player.grounded = false;
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                player.Position = origin + Vector2.up * 3 * 4 * t * (1 - t);
                player.velocity=new Vector2(0,12*(1-2*t));
                world.RefreshCamera(1f / 60);
                peakLag = Mathf.Max(peakLag, Mathf.Abs(ground - world.CameraY));
            }
            Assert.That(peakLag, Is.LessThan(.01f), "ordinary jump must not move the view vertically");
            player.Position=origin;player.velocity=Vector2.zero;player.grounded=true;world.RefreshCamera(1f/60);
            Assert.That(world.CameraY,Is.EqualTo(ground).Within(.01f),"same-height landing must not tug the view");
            player.grounded=false;player.Position=origin+Vector2.up*8;world.RefreshCamera(1f/60);
            Assert.That(world.CameraY,Is.LessThan(ground),"climbing beyond the safe zone still follows");
            // Measure long-fall following around a neutral center, independently of the scene's 150px offset.
            float authoredOffset=world.cameraVerticalOffsetPixels;world.cameraVerticalOffsetPixels=0;
            player.Position=new Vector2(player.Position.x,-57);world.RefreshCamera(0,true);
            float fallStart=world.CameraY;player.velocity=new Vector2(0,-20);
            for(int i=0;i<60;i++){player.Position+=Vector2.down*(20f/60);world.RefreshCamera(1f/60);}
            Assert.That(world.CameraY,Is.GreaterThan(fallStart+300),"long falls must not leave the player offscreen");
            Assert.That(-player.Position.y*32-world.CameraY,Is.InRange(180,410));
            world.cameraVerticalOffsetPixels=authoredOffset;
            world.FixedCameraCenter=new Vector2(player.Position.x,player.Position.y+4);world.RefreshCamera(0,true);
            float bossFrame=world.CameraY;player.Position+=Vector2.up*3;
            for(int i=0;i<60;i++)world.RefreshCamera(1f/60);
            Assert.That(world.CameraY,Is.EqualTo(bossFrame).Within(.01f),"boss framing remains fixed");
            world.FixedCameraCenter=null;
            Debug.Log("CHAPTER1_CAMERA_CHECK ordinaryJumpDriftPx="+peakLag+" stairMaxStepPx="+worst+"; landing, long fall and boss hold passed");
            yield return new ExitPlayMode();
        }
    }
}
