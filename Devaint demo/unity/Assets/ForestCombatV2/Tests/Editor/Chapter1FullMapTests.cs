using System.Collections;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public class Chapter1MapViewportTests
    {
        [Test] public void FitIncludesWholeMapAndCoordinatesRoundTripAtEveryZoom()
        {
            var view=new Chapter1MapViewport(new Vector2(18432,4864));
            foreach(var screen in new[]{new Vector2(928,394),new Vector2(1888,934)})
            {
                view.Fit();var a=view.ToScreen(Vector2.zero,screen);var b=view.ToScreen(view.Size,screen);
                Assert.That(a.x,Is.GreaterThanOrEqualTo(-.01f));Assert.That(a.y,Is.GreaterThanOrEqualTo(-.01f));
                Assert.That(b.x,Is.LessThanOrEqualTo(screen.x+.01f));Assert.That(b.y,Is.LessThanOrEqualTo(screen.y+.01f));
                view.ZoomAt(4,screen/2,screen);var point=new Vector2(12500,2100);
                Assert.That(Vector2.Distance(view.ToMap(view.ToScreen(point,screen),screen),point),Is.LessThan(.01));
                view.Pan(new Vector2(100000,-100000),screen);
                Assert.That(view.Center.x,Is.InRange(0,view.Size.x));Assert.That(view.Center.y,Is.InRange(0,view.Size.y));
                view.ZoomAt(100,screen/2,screen);Assert.That(view.Zoom,Is.EqualTo(16));
                view.ZoomAt(.0001f,screen/2,screen);Assert.That(view.Zoom,Is.EqualTo(1));
            }
        }
        [Test] public void CursorAnchoredZoomAndFocusKeepTheTargetInView()
        {
            var view=new Chapter1MapViewport(new Vector2(2000,2000));var screen=new Vector2(800,600);
            view.ZoomAt(2,screen/2,screen);var cursor=new Vector2(440,320);var before=view.ToMap(cursor,screen);
            view.ZoomAt(1.5f,cursor,screen);
            Assert.That(Vector2.Distance(before,view.ToMap(cursor,screen)),Is.LessThan(.01));
            view.Focus(new Vector2(1900,50),screen);var pin=view.ToScreen(new Vector2(1900,50),screen);
            Assert.That(pin.x,Is.InRange(0,screen.x));Assert.That(pin.y,Is.InRange(0,screen.y));
            view.Fit();Assert.That(view.Center,Is.EqualTo(view.Size/2));
        }
    }
    public class Chapter1FullMapTests
    {
        ForestCombatWorld world;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open(Chapter1MapSelector.Ver2Scene);
            yield return new EnterPlayMode();yield return null;yield return null;
            world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if(world&&world.FullMap)world.FullMap.SetOpen(false);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }
        IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.Update();keyboard.MakeCurrent();
            world.FullMap.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
        }
        [UnityTest] public IEnumerator MAndEscapeToggleMapAndPauseWithoutReleasingAnotherOwnersPause()
        {
            var map=world.FullMap;Assert.That(map.MapTexture.width,Is.EqualTo(world.Data.cols));
            bool debugUI=world.diagnostics.showDebugUI;
            using(var other=world.arena.Controls.OpenScope("map test other owner"))
            {
                other.PauseSimulation();yield return KeyPress(Key.M);
                Assert.That(map.IsOpen,Is.True);Assert.That(world.arena.Controls.Current.Blocks(InputChannels.Combat),Is.True);
                Assert.That(world.diagnostics.showDebugUI,Is.False);
                Assert.That(Time.timeScale,Is.Zero);
                var texture=map.MapTexture;yield return KeyPress(Key.Equals);
                Assert.That(map.Viewport.Zoom,Is.GreaterThan(1));Assert.That(map.MapTexture,Is.SameAs(texture));
                yield return KeyPress(Key.Escape);Assert.That(map.IsOpen,Is.False);
                Assert.That(world.diagnostics.showDebugUI,Is.EqualTo(debugUI));
                Assert.That(world.arena.Controls.Current.SimulationPaused,Is.True);
            }
            Assert.That(world.arena.Controls.Current.SimulationPaused,Is.False);
            Assert.That(world.arena.Controls.Current.Blocks(InputChannels.Combat),Is.False);
            yield return KeyPress(Key.M);Assert.That(world.FullMap.IsOpen,Is.True);
            world.FullMap.enabled=false;
            Assert.That(world.arena.Controls.Current.SimulationPaused,Is.False);
            Assert.That(world.arena.Controls.Current.Blocks(InputChannels.Combat),Is.False);
        }
        [UnityTest] public IEnumerator SimulationAndPlayerStayStillWhileMapIsOpenAndResumeWhenClosed()
        {
            var map=world.FullMap;map.SetOpen(true);float now=world.arena.Now;var position=world.arena.Player.Position;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(world.arena.Now,Is.EqualTo(now));Assert.That(world.arena.Player.Position,Is.EqualTo(position));
            map.SetOpen(false);yield return new WaitForSecondsRealtime(.12f);
            Assert.That(world.arena.Now,Is.GreaterThan(now));
        }
        [UnityTest] public IEnumerator EnemyMarkersTrackLivingEnemiesAndRespectExploration()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Chapter1MapSelector.ProceduralScene);
            world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int frame=0;frame<600&&(!world||!world.Ready);frame++)
            {yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var map=world.FullMap;
            var enemy=world.arena.enemies[0];
            using(var driver=world.arena.AcquireManualSimulation())
            {
                enemy.Position=world.arena.Player.Position;
                Assert.That(map.TryGetEnemyMapPoint(enemy,out var first),Is.True);
                enemy.Position+=Vector2.right;
                Assert.That(map.TryGetEnemyMapPoint(enemy,out var moved),Is.True);
                Assert.That(moved.x-first.x,Is.EqualTo(32).Within(.001f));
                enemy.gameObject.SetActive(false);
                Assert.That(map.TryGetEnemyMapPoint(enemy,out _),Is.False);
                enemy.gameObject.SetActive(true);
                ForestRoom hidden=null;
                foreach(var room in world.Data.rooms)if(!map.IsRoomDiscovered(room.id)){hidden=room;break;}
                Assert.That(hidden,Is.Not.Null);
                enemy.Position=new Vector2(hidden.x+hidden.w/2f,-hidden.y-hidden.h/2f);
                Assert.That(map.TryGetEnemyMapPoint(enemy,out _),Is.False);
                world.arena.Player.Position=enemy.Position;
                map.SendMessage("Update");
                Assert.That(map.TryGetEnemyMapPoint(enemy,out _),Is.True);
                enemy.health=0;
                Assert.That(map.TryGetEnemyMapPoint(enemy,out _),Is.False);
                Assert.That(map.TryGetEnemyMapPoint(null,out _),Is.False);
            }
        }

        [UnityTest] public IEnumerator MinimapTracksPlayerWithoutChangingFullMapZoomOrPausingCombat()
        {
            var map=world.FullMap;var texture=map.MapTexture;var fullCenter=map.Viewport.Center;
            var start=map.MinimapViewport.Center;float now=world.arena.Now;
            world.arena.Player.Position=new Vector2(world.Data.width/64f,-world.Data.height/64f);
            yield return new WaitForSecondsRealtime(.12f);map.SendMessage("LateUpdate");
            Assert.That(Vector2.Distance(start,map.MinimapViewport.Center),Is.GreaterThan(100));
            var viewport=new Vector2(map.MinimapBounds.width-12,118);
            var point=new Vector2(world.arena.Player.Position.x*32,-world.arena.Player.Position.y*32);
            Assert.That(Vector2.Distance(map.MinimapViewport.ToScreen(point,viewport),viewport/2),Is.LessThan(1));
            Assert.That(map.Viewport.Center,Is.EqualTo(fullCenter));Assert.That(map.Viewport.Zoom,Is.EqualTo(1));
            Assert.That(map.MapTexture,Is.SameAs(texture));Assert.That(map.IsOpen,Is.False);
            Assert.That(world.arena.Controls.Current.SimulationPaused,Is.False);
            Assert.That(world.arena.Now,Is.GreaterThan(now));
        }
    }
}
