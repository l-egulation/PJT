using System.Collections;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1ManualCheckpointTests
    {
        [UnityTest] public IEnumerator LifecycleSafetyAndCombatState()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            CheckFlow(Object.FindAnyObjectByType<CombatArena>());
            yield return new ExitPlayMode();
        }
        static void CheckFlow(CombatArena arena)
        {
            var p = arena.Player;
            foreach (var e in arena.enemies) if (e) e.aggressive = false;
            var data = JsonUtility.FromJson<ManualCheckpointData>(Resources.Load<TextAsset>("ManualCheckpoint").text);
            p.ConfigureCheckpoint(new ManualCheckpointProfile(data));
            GameObject Surface(string name, Vector2 center, Vector2 size, bool oneWay = false)
            {
                var go = new GameObject(name); go.transform.position = center;
                go.AddComponent<BoxCollider2D>().size = size; go.AddComponent<ArenaSurface>().oneWay = oneWay;
                return go;
            }
            Surface("checkpoint solid support", new Vector2(200,199), new Vector2(12,2));
            Surface("checkpoint one-way support", new Vector2(240,199.75f), new Vector2(12,.5f), true);
            Surface("solid separator crossed by teleport", new Vector2(220,204), new Vector2(2,20));
            using (var driver = arena.AcquireManualSimulation())
            {
                void Place(float x = 200, float y = 200)
                {
                    p.CancelAction(); p.ClearHitMotionLocks(); p.stunUntil = p.rootUntil = p.staggerUntil = 0;
                    p.Position = new Vector2(x,y); p.velocity = Vector2.zero; Physics2D.SyncTransforms(); p.grounded = p.ProbeGround();
                }
                void Advance(float seconds)
                { for(int i=0;i<Mathf.CeilToInt(seconds*120);i++) driver.Step(1f/120); }
                void Install(int slot)
                { Assert.That(p.TryInstallPortal(slot), Is.True, p.CheckpointMessage); Advance(data.installSeconds+.03f); Assert.That(p.HasPortal(slot), Is.True); }
                driver.Reset(); Place();
                Assert.That(p.TryTravelThroughPortal(), Is.False, "No default spawn fallback without installed portals");
                Place(200,205);
                Assert.That(p.TryInstallPortal(0), Is.False, "Airborne installation is rejected");
                Assert.That(p.TryTravelThroughPortal(), Is.False);
                Place(); p.stunUntil = arena.Now + 1;
                Assert.That(p.TryInstallPortal(0), Is.False, "Stun blocks requests");
                Place(); driver.Step(1f/120,new CombatInput(1, CombatButtons.Dash));
                Assert.That(p.Dashing, Is.True); Assert.That(p.TryInstallPortal(0), Is.False);
                Place(); driver.Step(1f/120,new CombatInput(0, CombatButtons.Basic));
                Assert.That(p.currentCast, Is.Not.Null); Assert.That(p.TryInstallPortal(0), Is.False);
                Place();
                using (var scope = arena.Controls.OpenScope("checkpoint menu test"))
                {
                    scope.BlockInput(InputChannels.Interaction);
                    Assert.That(p.TryInstallPortal(0), Is.False);
                }
                // C sets portal 1 here; one portal alone goes nowhere.
                Install(0); Vector2 first = p.PortalPosition(0);
                Assert.That(p.PortalUnderfoot(), Is.EqualTo(0));
                Assert.That(p.TryTravelThroughPortal(), Is.False, "the other portal is not set yet");
                Place(201); Assert.That(p.TryInstallPortal(1), Is.False, "the two portals keep apart");
                // V sets portal 2 across the solid separator.
                Place(240); Assert.That(p.TryTravelThroughPortal(), Is.False, "nothing stands at 240 yet");
                Install(1); Vector2 second = p.PortalPosition(1);
                Assert.That(p.PortalsLinked, Is.True); Assert.That(p.HasPortal(0) && p.PortalPosition(0) == first, Is.True, "V never moves portal 1");
                p.health = 73;
                int selected = p.selected; string form = p.Form.id; int hits = p.ReceivedHitCount;
                Assert.That(p.TryTravelThroughPortal(), Is.True, p.CheckpointMessage);
                Assert.That(p.InputBlocked, Is.True);
                Vector2 origin = p.Position;
                driver.Step(.1f,new CombatInput(1, CombatButtons.Jump | CombatButtons.Basic));
                Assert.That(p.Position, Is.EqualTo(origin)); Assert.That(p.currentCast, Is.Null);
                Assert.That(p.Invulnerable, Is.False, "Entry is interruptible");
                Advance(data.enterSeconds);
                Assert.That(p.PortalPhase, Is.EqualTo(CheckpointPhase.Appearing));
                Assert.That(Vector2.Distance(p.Position,first), Is.LessThan(.02f), "Travel crosses solid room separators");
                Assert.That(p.Invulnerable, Is.True);
                Assert.That(p.Receive(10, arena.enemies[0],1,false,null), Is.False, "Appearance rejects enemy damage");
                Assert.That(p.health, Is.EqualTo(73)); Assert.That(p.ReceivedHitCount, Is.EqualTo(hits));
                Assert.That(p.selected, Is.EqualTo(selected)); Assert.That(p.Form.id, Is.EqualTo(form));
                Advance(data.appearSeconds+.03f);
                Assert.That(p.CheckpointBusy, Is.False); Assert.That(p.InputBlocked, Is.False); Assert.That(p.Invulnerable, Is.False);
                Assert.That(p.PortalsLinked, Is.True, "both portals stay after a crossing");
                // And straight back again: no cooldown or consumable.
                Assert.That(p.TryTravelThroughPortal(), Is.True, "No cooldown or consumable");
                Advance(data.enterSeconds+data.appearSeconds+.06f);
                Assert.That(Vector2.Distance(p.Position,second), Is.LessThan(.02f), "back at portal 2");
                Place(244);
                Assert.That(p.TryInstallPortal(1), Is.True);
                Assert.That(p.Receive(1,arena.enemies[0],1,false,null), Is.True);
                Assert.That(p.CheckpointBusy, Is.False); Assert.That(p.HasPortal(1), Is.True);
                Assert.That(p.PortalPosition(1), Is.EqualTo(second), "Interrupted move keeps the old portal");
                Assert.That(p.PortalPosition(1).x, Is.EqualTo(240), "One-way floors support portals");
                Place(236); Assert.That(p.PortalUnderfoot(), Is.EqualTo(-1)); Assert.That(p.TryTravelThroughPortal(), Is.False, "only from inside a portal");
                Place(240); Assert.That(p.TryTravelThroughPortal(), Is.True);
                Assert.That(p.Receive(1,arena.enemies[0],1,false,null), Is.True);
                Assert.That(p.CheckpointBusy, Is.False); Advance(1); Assert.That(p.Position.x, Is.InRange(238,243), "Hit knockback is preserved without teleporting to 200");
                Place(240); Assert.That(p.TryTravelThroughPortal(), Is.True);
                var blocked = Surface("new destination obstruction", new Vector2(200,201), new Vector2(3,2)); Physics2D.SyncTransforms();
                Advance(data.enterSeconds+.03f);
                Assert.That(p.Position.x, Is.EqualTo(240), "Never arrive inside a new wall"); Assert.That(p.CheckpointBusy, Is.False);
                Assert.That(p.TryTravelThroughPortal(), Is.False, "a blocked far portal refuses the crossing");
                Object.DestroyImmediate(blocked); Physics2D.SyncTransforms();
                Assert.That(p.TryTravelThroughPortal(), Is.True);
                arena.ResetTest(); Assert.That(p.HasCheckpoint, Is.False); Assert.That(p.CheckpointBusy, Is.False); Assert.That(p.InputBlocked, Is.False);
                Place(); Install(0); p.health=1;
                Assert.That(p.Receive(10,arena.enemies[0],1,false,null), Is.True);
                Assert.That(p.Dead, Is.True); Assert.That(p.HasCheckpoint, Is.False);
                driver.Reset(); Place(); Install(0); Place(240); Install(1);
                p.ConfigureCheckpoint(new ManualCheckpointProfile(data));
                Assert.That(p.HasCheckpoint, Is.False, "New owner/run starts empty");
            }
        }

        [UnityTest] public IEnumerator ForestKeyboardPresentationAndCamera()
        {
            LegacySceneFixture.Open(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode(); yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            try { CheckKeyboard(world); } finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PortalPromptHidesWhileMenuOrInteractionOwnsControls()
        {
            LegacySceneFixture.Open(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            try { CheckKeyboard(world,true); } finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }
        static void CheckKeyboard(ForestCombatWorld world,bool markerOnly=false)
        {
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            var portal=world.GetComponent<Chapter1ManualCheckpoint>(); Assert.That(portal, Is.Not.Null);
            var p=world.arena.Player;
            // Install/return keys come from the bindings (install moved from F to C in 12e180b9).
            var keys=CombatKeyboardBindings.For(p);
            var data=JsonUtility.FromJson<ManualCheckpointData>(Resources.Load<TextAsset>("ManualCheckpoint").text);
            var settings=InputSystem.settings;
            var background=settings.backgroundBehavior;
            var editorInput=settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            try
            {
                using(var driver=world.arena.AcquireManualSimulation())
                {
                    driver.Reset(); for(int i=0;i<120&&!p.grounded;i++)driver.Step(1f/120);
                    void PressKeys(params Key[] keys)
                    { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();portal.HandleKeyboard(); }
                    void Advance(int ticks) {for(int i=0;i<ticks;i++)driver.Step(1f/120);}
                    // In a run C/V stay off until the workshop artisan has taught the portal (playtest 2026-11-05).
                    bool inRun=world.GetComponent<Chapter1RunBinding>()?.Run!=null;
                    Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
                    if(inRun){
                        PressKeys(); PressKeys(keys.CheckpointInstall); Assert.That(p.PortalPhase, Is.EqualTo(CheckpointPhase.None), "the portal is not taught yet");
                        PressKeys(); PressKeys(keys.CheckpointReturn); Assert.That(p.PortalPhase, Is.EqualTo(CheckpointPhase.None), "V is locked too");
                        Assert.That(portal.PortalLocked, Is.True);
                        // Locked presses stay unconsumed in this frame's buffer; clear them so C below is not read as C+V.
                        PlayerControls.Take(p, Deviant.Input.Contracts.GameAction.CheckpointInstall); PlayerControls.Take(p, Deviant.Input.Contracts.GameAction.CheckpointReturn);
                        Chapter1ProfileHost.Service.MarkStorySeen(Chapter1ManualCheckpoint.PortalTaughtStory);
                    }
                    Assert.That(portal.PortalLocked, Is.False);
                    PressKeys(); PressKeys(keys.CheckpointInstall); Assert.That(p.PortalPhase, Is.EqualTo(CheckpointPhase.Installing), $"message={p.CheckpointMessage}, grounded={p.grounded}, probe={p.ProbeGround()}, blocked={p.InputBlocked}, time={Time.timeScale}, install={keyboard[keys.CheckpointInstall].wasPressedThisFrame}, current={Keyboard.current==keyboard}, pos={p.Position}");
                    PressKeys(); Advance(Mathf.CeilToInt((data.installSeconds+.03f)*120)); Assert.That(p.HasPortal(0),Is.True);
                    var destination=p.PortalPosition(0);
                    portal.RefreshPresentation();
                    SpriteRenderer Named(string name) => System.Array.Find(portal.GetComponentsInChildren<SpriteRenderer>(true), r => r.name == name);
                    var installed=Named("Installed portal 1");
                    Assert.That(installed,Is.Not.Null); Assert.That(installed.enabled,Is.True);Assert.That(installed.sprite,Is.Not.Null);
                    Assert.That(portal.TravelOffered,Is.False,"one portal alone offers no crossing");
                    // V sets portal 2 further on; then F inside it crosses back to portal 1.
                    world.Warp(2); Advance(30); PressKeys(); PressKeys(keys.CheckpointReturn);
                    Assert.That(p.PortalPhase,Is.EqualTo(CheckpointPhase.Installing),p.CheckpointMessage);
                    PressKeys(); Advance(Mathf.CeilToInt((data.installSeconds+.03f)*120)); Assert.That(p.HasPortal(1),Is.True);
                    Assert.That(p.PortalPosition(0),Is.EqualTo(destination),"V leaves portal 1 alone");
                    portal.RefreshPresentation();
                    Assert.That(Named("Installed portal 2").enabled,Is.True);
                    Assert.That(portal.TravelOffered,Is.True);
                    Assert.That(portal.TravelKey.Visible,Is.True,"an F keycap shows over the portal the hero stands in");
                    if(markerOnly)
                    {
                        using(var scope=world.arena.Controls.OpenScope("portal marker menu test"))
                        {
                            scope.PauseSimulation();portal.RefreshPresentation();
                            Assert.That(portal.TravelKey.Visible,Is.False,"world interaction prompts hide while a menu pauses the run");
                        }
                        using(var scope=world.arena.Controls.OpenScope("portal marker interaction test"))
                        {
                            scope.BlockInput(InputChannels.Interaction);portal.RefreshPresentation();
                            Assert.That(portal.TravelKey.Visible,Is.False,"a blocked interaction must not be advertised as available");
                        }
                        portal.RefreshPresentation();Assert.That(portal.TravelKey.Visible,Is.True);
                        return;
                    }
                    PressKeys(); PressKeys(keys.Interact);
                    Assert.That(p.PortalPhase,Is.EqualTo(CheckpointPhase.Entering),p.CheckpointMessage); PressKeys(); Advance(Mathf.CeilToInt((data.enterSeconds+.03f)*120));
                    Assert.That(p.PortalPhase,Is.EqualTo(CheckpointPhase.Appearing));
                    Assert.That(Vector2.Distance(p.Position,destination),Is.LessThan(.03f));
                    Assert.That(Mathf.Abs(world.View.transform.position.x-p.Position.x),Is.LessThan(2),"Camera snaps with the crossing");
                    portal.RefreshPresentation(); Advance(Mathf.CeilToInt((data.appearSeconds+.03f)*120)); portal.RefreshPresentation();
                    Assert.That(p.visual.transform.localScale,Is.EqualTo(Vector3.one));
                    Assert.That(installed.enabled&&Named("Installed portal 2").enabled,Is.True,"both portals stay");
                    PressKeys(keys.CheckpointInstall); Assert.That(p.CheckpointBusy,Is.True);
                    PressKeys(); PressKeys(Key.Q); Assert.That(p.CheckpointBusy,Is.True);
                    PressKeys(); PressKeys(Key.W); Assert.That(p.CheckpointBusy,Is.True, "W is a weapon special and cannot reset portal traversal.");
                    // A dash during the install cancels it and the dash itself goes out (user request 2026-11-06).
                    PressKeys(); driver.Step(1f/120,new CombatInput(0,CombatButtons.Dash));
                    Assert.That(p.CheckpointBusy,Is.False,p.CheckpointMessage);
                    Assert.That(p.Dashing,Is.True,"the dash that cancelled the install is performed");
                    Assert.That(p.HasPortal(0)&&p.PortalPosition(0)==destination,Is.True,"the earlier portal stays");
                    // A jump during the install cancels it the same way and the jump goes out (user request 2026-11-16).
                    Advance(90); PressKeys(); PressKeys(keys.CheckpointInstall); Assert.That(p.PortalPhase,Is.EqualTo(CheckpointPhase.Installing),p.CheckpointMessage);
                    PressKeys(); driver.Step(1f/120,new CombatInput(0,CombatButtons.Jump));
                    Assert.That(p.CheckpointBusy,Is.False,p.CheckpointMessage);
                    Assert.That(p.grounded,Is.False,"the jump that cancelled the install is performed");
                    Assert.That(p.HasPortal(0)&&p.PortalPosition(0)==destination,Is.True,"the earlier portal stays");
                    Advance(120);
                    world.Warp(2); Advance(30); PressKeys(); PressKeys(keys.CheckpointReturn); Assert.That(p.CheckpointBusy,Is.True,p.CheckpointMessage);
                    PressKeys(); PressKeys(Key.F5);
                    Assert.That(p.CheckpointBusy,Is.False);Assert.That(p.HasCheckpoint,Is.False);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); settings.backgroundBehavior=background; settings.editorInputBehaviorInPlayMode=editorInput; }
        }
    }
}
