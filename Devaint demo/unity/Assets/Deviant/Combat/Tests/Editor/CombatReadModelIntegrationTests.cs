using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CombatReadModelIntegrationTests
    {
        [Test] public void UninitializedAndDestroyedModelsRejectRatherThanCreatingInputOrFindingAnotherArena()
        {
            var root = new GameObject("UninitializedReadModel"); var arena = root.AddComponent<CombatArena>();
            var reads = arena.ReadModel;
            try
            {
                Assert.That(reads.TryGetSession(out _), Is.False);
                Assert.That(reads.TryReadPlayer(default, out _), Is.False);
                Assert.That(reads.TryReadActor(default, out _), Is.False);
                Assert.That(arena.Player, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
            Assert.That(reads.TryGetSession(out _), Is.False);
            Assert.That(reads.TryReadActor(default, out _), Is.False);
        }
        [UnityTest] public IEnumerator InitializationPublishesTheCompleteReadSessionAfterConstruction()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("ExplicitReadSession").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            CheckInitialization(Object.FindAnyObjectByType<CombatArena>());
        }
        // Keep captured callbacks out of the coroutine state serialized across
        // EnterPlayMode's domain reload.
        static void CheckInitialization(CombatArena arena)
        {
            var reads = arena.ReadModel;
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Profile = CombatCompositionProfile.Game; content.Camera = new GameObject("ReadCamera").AddComponent<Camera>();
            content.PlayerSpawn = new Vector2(100, 1000);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] { new CombatEnemySpawn("ReadEnemy", 1, new Vector2(110, 1000), Vector2.one, Vector2.up, 60, false, false, 1) };
            int published = 0;
            content.PrepareEnvironment = owner => Assert.That(reads.TryGetSession(out _), Is.False);
            content.BindPlayerInput = player => Assert.That(reads.TryGetSession(out _), Is.False);
            arena.Initialized += () =>
            {
                published++;
                Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game));
                Assert.That(reads.TryGetSession(out var session), Is.True);
                Assert.That(reads.TryReadPlayer(session, out var player), Is.True);
                Assert.That(player.Kinematics.X, Is.EqualTo(100)); Assert.That(player.Kinematics.Y, Is.EqualTo(1000));
                Assert.That(player.Vitals.Health, Is.EqualTo(100)); Assert.That(player.TickCount, Is.Zero);
                Assert.That(arena.TryBindActor(arena.enemies[0], out var enemy), Is.True);
                Assert.That(reads.TryReadActor(enemy, out var target), Is.True); Assert.That(target.Vitals.Health, Is.EqualTo(60));
            };
            Assert.That(arena.Initialize(content), Is.True); Assert.That(published, Is.EqualTo(1));
        }


        [UnityTest] public IEnumerator GameHudAndActorReadsUseDetachedOwnersAndExplicitResetBindings()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabActorReadsUseDetachedOwnersAndExplicitResetBindings()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameWeaponResourceReadsProjectExistingOwnersWithoutAllocating() => ReadResources("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabWeaponResourceReadsProjectExistingOwnersWithoutAllocating() => ReadResources("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator ReadResources(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); var player = arena.Player; foreach (var enemy in arena.enemies) enemy.aggressive = false;
                player.weapons[0].Cooldown.Reset(1, 5); player.weapons[1].Cooldown.Reset(3, 4);
                Assert.That(player.weapons[0].Cooldown.Spend(arena.Now), Is.True); Assert.That(player.weapons[1].Cooldown.Spend(arena.Now), Is.True);
                player.selected = 1; Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True);
                Assert.That(arena.ReadModel.TryReadPlayer(session, out var first), Is.True);
                Assert.That(first.Resources.HasLoadout, Is.True); Assert.That(first.Resources.ActiveSlot, Is.EqualTo(1));
                Assert.That(first.Resources.Slot0.InstanceId, Is.EqualTo(player.weapons[0].InstanceId)); Assert.That(first.Resources.Slot1.InstanceId, Is.EqualTo(player.weapons[1].InstanceId));
                Assert.That(first.Resources.Slot0.Available, Is.Zero); Assert.That(first.Resources.Slot0.Period, Is.EqualTo(5)); Assert.That(first.Resources.Slot0.NextReady, Is.EqualTo(5));
                Assert.That(first.Resources.Slot1.Capacity, Is.EqualTo(3)); Assert.That(first.Resources.Slot1.Available, Is.EqualTo(2)); Assert.That(first.Resources.Slot1.NextReady, Is.EqualTo(4));
                Assert.That(arena.TryBindActor(arena.enemies[0], out var target), Is.True); Assert.That(arena.ReadModel.TryReadActor(target, out var enemyRead), Is.True); Assert.That(enemyRead.Resources.HasLoadout, Is.False);
                for (int i = 0; i < 32; i++) arena.ReadModel.TryReadPlayer(session, out _);
                long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 2048; i++) arena.ReadModel.TryReadPlayer(session, out _);
                Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero, "Read projection must not allocate another loadout snapshot or collections.");
                player.weapons[0].Cooldown.Tick(5); player.weapons[1].Cooldown.Spend(0); player.selected = 0;
                Assert.That(arena.ReadModel.TryReadPlayer(session, out var changed), Is.True); Assert.That(changed.Resources.Slot0.Available, Is.EqualTo(1)); Assert.That(changed.Resources.Slot1.Available, Is.EqualTo(1));
                Assert.That(first.Resources.Slot0.Available, Is.Zero); Assert.That(first.Resources.Slot1.Available, Is.EqualTo(2)); Assert.That(first.Resources.ActiveSlot, Is.EqualTo(1));
                driver.Reset(); Assert.That(arena.ReadModel.TryReadPlayer(session, out _), Is.False); Assert.That(arena.ReadModel.TryGetSession(out session), Is.True);
                Assert.That(arena.ReadModel.TryReadPlayer(session, out var reset), Is.True);
                Assert.That(reset.Resources.Slot0.Available, Is.EqualTo(player.weapons[0].Cooldown.Available)); Assert.That(reset.Resources.Slot1.Available, Is.EqualTo(player.weapons[1].Cooldown.Available));
                Assert.That(reset.Resources.Slot0.Period, Is.EqualTo(player.weapons[0].Cooldown.Period)); Assert.That(reset.Resources.Slot1.NextReady, Is.EqualTo(player.weapons[1].Cooldown.NextReady));
            }
        }

        static IEnumerator Exercise(string scene, bool game)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var reads = arena.ReadModel;
            var player = arena.Player; var target = arena.enemies[0];
            var strayRoot = new GameObject("UnregisteredReadActor"); var stray = strayRoot.AddComponent<CombatActor>();
            var hudRoot = new GameObject("ReadBoundHud");
            var hudType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Deviant.UI.BattleHudDataSource")).First(t => t != null);
            var liveHud = (MonoBehaviour)hudRoot.AddComponent(hudType);
            var liveField = hudType.GetField("liveHealth"); var bindHud = hudType.GetMethod("BindLiveHealth"); var readHud = hudType.GetMethod("Read");
            var composition = game ? arena.GetComponent("GameComposition") : null;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (var enemy in arena.enemies) enemy.aggressive = false;
                    driver.Reset();
                    player.Position = new Vector2(1.25f, 1000); player.velocity = new Vector2(3.5f, -4.5f); player.facing = -1;
                    player.maxHealth = 150; player.health = 73.25f; player.shield = 7;
                    player.stunUntil = 3; player.rootUntil = 4;
                    target.health = 60.25f; target.shield = 5;
                    Assert.That(reads.TryGetSession(out var session), Is.True);
                    Assert.That(session.Equals(player.InputSession), Is.True);
                    Assert.That(arena.TryBindActor(player, out var playerHandle), Is.True);
                    Assert.That(arena.TryBindActor(target, out var targetHandle), Is.True);
                    Assert.That(reads.TryReadPlayer(session, out var initial), Is.True);
                    Assert.That(reads.TryReadActor(playerHandle, out var byHandle), Is.True);
                    Assert.That(byHandle.Handle.Equals(initial.Handle), Is.True);
                    Assert.That(initial.Vitals.Health, Is.EqualTo(73.25f)); Assert.That(initial.Vitals.Maximum, Is.EqualTo(150)); Assert.That(initial.Vitals.Shield, Is.EqualTo(7));
                    Assert.That(initial.Statuses.StunUntil, Is.EqualTo(3)); Assert.That(initial.Statuses.RootUntil, Is.EqualTo(4));
                    Assert.That(initial.ActiveWeaponId, Is.EqualTo(player.Weapon.InstanceId)); Assert.That(initial.ActiveWeaponContentId, Is.EqualTo(player.Form.id)); Assert.That(initial.ActiveWeaponName, Is.EqualTo(player.Form.name));
                    Assert.That(initial.Action.Phase, Is.EqualTo(CombatActionReadPhase.None));
                    player.transform.position = new Vector3(900, 901, 902);
                    long nextInput = player.NextInputSequence;
                    for (int i = 0; i < 32; i++) reads.TryReadPlayer(session, out _);
                    GC.GetAllocatedBytesForCurrentThread(); long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 2048; i++) { reads.TryReadPlayer(session, out _); reads.TryReadActor(targetHandle, out _); }
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.That(allocated, Is.Zero, "A warmed HUD/metric query must not copy collections, hash content or allocate.");
                    Assert.That(reads.TryReadPlayer(session, out var repeated), Is.True);
                    Assert.That(repeated.Kinematics.X, Is.EqualTo(1.25f)); Assert.That(repeated.Kinematics.Y, Is.EqualTo(1000)); Assert.That(repeated.Kinematics.VelocityY, Is.EqualTo(-4.5f)); Assert.That(repeated.Kinematics.Facing, Is.EqualTo(-1));
                    Assert.That(player.transform.position, Is.EqualTo(new Vector3(900, 901, 902)), "A read must not pull from or silently repair the Native view.");
                    Assert.That(repeated.Now, Is.EqualTo(initial.Now)); Assert.That(repeated.TickCount, Is.EqualTo(initial.TickCount)); Assert.That(player.NextInputSequence, Is.EqualTo(nextInput));
                    liveField.SetValue(liveHud, player); bindHud.Invoke(liveHud, null);
                    AssertHud(readHud.Invoke(liveHud, null), 74, 150, player.Form.name);
                    if (game) AssertHud(composition.GetType().GetMethod("Read").Invoke(composition, null), 74, 150, player.Form.name);
                    liveField.SetValue(liveHud, target);
                    AssertHud(readHud.Invoke(liveHud, null), 0, 0, null);
                    liveField.SetValue(liveHud, player); bindHud.Invoke(liveHud, null);
                    player.health = 12; player.Position = new Vector2(11, 1001);
                    Assert.That(initial.Vitals.Health, Is.EqualTo(73.25f)); Assert.That(initial.Kinematics.X, Is.EqualTo(1.25f));
                    Assert.That(target.Receive(0, player, 1, false, null), Is.True);
                    Assert.That(target.Receive(8, player, 1, false, null), Is.True);
                    Assert.That(reads.TryReadActor(targetHandle, out var hitRead), Is.True);
                    Assert.That(hitRead.Vitals.ReceivedHits, Is.EqualTo(2)); Assert.That(hitRead.Vitals.Health, Is.EqualTo(57.25f)); Assert.That(hitRead.Vitals.Shield, Is.Zero);
                    target.health = 0; Assert.That(reads.TryReadActor(targetHandle, out var deadRead), Is.True); Assert.That(deadRead.Vitals.Dead, Is.True);
                    stray.Configure(arena, Vector2.zero, Vector2.one, Vector2.up, false);
                    Assert.That(arena.TryBindActor(stray, out _), Is.False, "A matching arena pointer alone is not registration.");
                    Assert.That(reads.TryReadActor(new CombatActorHandle(session, stray.ActorId), out _), Is.False);
                    Assert.That(reads.TryReadActor(new CombatActorHandle(new CombatInputSession(session.ActorId + 1, session.Generation, session.Epoch), target.ActorId), out _), Is.False);
                    arena.enemies.RemoveAt(0);
                    try
                    {
                        Assert.That(arena.TryBindActor(target, out _), Is.False);
                        Assert.That(reads.TryReadActor(targetHandle, out _), Is.False);
                    }
                    finally { arena.enemies.Insert(0, target); }
                    arena.SpawnDecoy(player, new Vector2(2, 1000), new EffectData { hp = 10, duration = 3 });
                    var firstDecoy = arena.decoys[0]; Assert.That(arena.TryBindActor(firstDecoy, out var decoyHandle), Is.True);
                    arena.SpawnDecoy(player, new Vector2(3, 1000), new EffectData { hp = 20, duration = 3 });
                    Assert.That(reads.TryReadActor(decoyHandle, out _), Is.False, "A replaced decoy must never resolve to its replacement.");
                    Assert.That(arena.TryBindActor(arena.decoys[0], out var replacement), Is.True); Assert.That(replacement.ActorId, Is.Not.EqualTo(decoyHandle.ActorId));

                    float now = arena.Now; long tick = arena.SimulationTickCount;
                    arena.ResetTest();
                    Assert.That(reads.TryReadPlayer(session, out var rejected), Is.False); Assert.That(rejected.Handle.ActorId, Is.Zero);
                    Assert.That(reads.TryReadActor(targetHandle, out _), Is.False); Assert.That(reads.TryReadActor(replacement, out _), Is.False);
                    Assert.That(reads.TryGetSession(out var ordinary), Is.True); Assert.That(ordinary.Generation, Is.EqualTo(session.Generation + 1)); Assert.That(ordinary.Epoch, Is.EqualTo(session.Epoch));
                    Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
                    Assert.That(reads.TryReadPlayer(ordinary, out var resetRead), Is.True); Assert.That(resetRead.Vitals.Health, Is.EqualTo(150)); Assert.That(resetRead.Vitals.ReceivedHits, Is.Zero);
                    AssertHud(readHud.Invoke(liveHud, null), 150, 150, player.Form.name);
                    if (game) AssertHud(composition.GetType().GetMethod("Read").Invoke(composition, null), 150, 150, player.Form.name);
                    driver.Step(.02f); float keptNow = arena.Now; long keptTick = arena.SimulationTickCount;
                    arena.ResetTest(); Assert.That(arena.Now, Is.EqualTo(keptNow)); Assert.That(arena.SimulationTickCount, Is.EqualTo(keptTick));
                    Assert.That(reads.TryGetSession(out ordinary), Is.True);
                    driver.Reset();
                    Assert.That(reads.TryReadPlayer(ordinary, out _), Is.False); Assert.That(reads.TryGetSession(out var epoch), Is.True);
                    Assert.That(epoch.Generation, Is.EqualTo(ordinary.Generation + 1)); Assert.That(epoch.Epoch, Is.EqualTo(ordinary.Epoch + 1));
                    Assert.That(reads.TryReadPlayer(epoch, out resetRead), Is.True); Assert.That(resetRead.Now, Is.Zero); Assert.That(resetRead.TickCount, Is.Zero);
                    Assert.That(arena.TryBindActor(target, out targetHandle), Is.True); Assert.That(targetHandle.Session.Equals(epoch), Is.True);

                    player.selected = 0; player.weapons[0].Select(Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
                    foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                    player.Position = new Vector2(0, 1000); player.grounded = true;
                    foreach (var enemy in arena.enemies) enemy.Position = new Vector2(35, 1000);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                    Assert.That(reads.TryReadPlayer(epoch, out var preparing), Is.True);
                    Assert.That(preparing.Action.ExecutionId, Is.EqualTo(player.currentCast.ExecutionId)); Assert.That(preparing.Action.ContentId, Is.EqualTo("base_sword_basic_0"));
                    Assert.That(preparing.Action.DefinitionVersion, Is.EqualTo(player.currentCast.DefinitionVersion)); Assert.That(preparing.Action.TimelineVersion, Is.EqualTo(player.currentCast.ActionTimelineVersion));
                    Assert.That(preparing.Action.Phase, Is.EqualTo(CombatActionReadPhase.Preparing));
                    ActorReadSnapshot active = preparing;
                    for (int i = 0; i < 20 && active.Action.Phase != CombatActionReadPhase.Active; i++)
                    { driver.Step(.02f); Assert.That(reads.TryReadPlayer(epoch, out active), Is.True); }
                    Assert.That(active.Action.Phase, Is.EqualTo(CombatActionReadPhase.Active)); Assert.That(active.Action.Elapsed, Is.GreaterThan(preparing.Action.Elapsed));
                    Assert.That(preparing.Action.Phase, Is.EqualTo(CombatActionReadPhase.Preparing));
                    driver.Reset(); Assert.That(reads.TryGetSession(out epoch), Is.True);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump)); Assert.That(reads.TryReadPlayer(epoch, out var jumped), Is.True); Assert.That(jumped.Jump.Used, Is.EqualTo(1));
                    driver.Reset(); Assert.That(reads.TryGetSession(out epoch), Is.True);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Assert.That(reads.TryReadPlayer(epoch, out var dashed), Is.True); Assert.That(dashed.Dash.EndsAt, Is.GreaterThan(dashed.Now));
                    int rebound = 0; Action fail = () => throw new InvalidOperationException("read reset boundary"); Action bind = () => rebound++;
                    arena.ResetCompleted += fail; arena.ResetCompleted += bind;
                    try { Assert.Throws<InvalidOperationException>(() => driver.Reset()); }
                    finally { arena.ResetCompleted -= fail; arena.ResetCompleted -= bind; }
                    Assert.That(rebound, Is.Zero); Assert.That(reads.TryReadPlayer(epoch, out _), Is.False, "Even a failed reset invalidates the previous token at entry.");
                    driver.Reset(); AssertHud(readHud.Invoke(liveHud, null), 150, 150, player.Form.name);
                }
                Object.DestroyImmediate(arena);
                Assert.That(reads.TryGetSession(out _), Is.False); Assert.That(reads.TryReadPlayer(default, out _), Is.False);
                AssertHud(readHud.Invoke(liveHud, null), 0, 0, null);
                if (game) AssertHud(composition.GetType().GetMethod("Read").Invoke(composition, null), 0, 0, null);
            }
            finally { Object.Destroy(strayRoot); Object.Destroy(hudRoot); }
        }

        static void AssertHud(object state, int hp, int maximum, string weapon)
        {
            var type = state.GetType();
            Assert.That((int)type.GetField("hp").GetValue(state), Is.EqualTo(hp));
            Assert.That((int)type.GetField("maxHP").GetValue(state), Is.EqualTo(maximum));
            Assert.That((string)type.GetField("weaponName").GetValue(state), Is.EqualTo(weapon));
        }
    }
}
