using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Chapter2;
using Deviant.Playtest.Editor;
using Deviant.Run;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Deviant.Playtest.Tests
{
    public sealed class GameSandboxModelTests
    {
        SketchEquipment Create()
        {
            var economy = new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text));
            var equipment = new SketchEquipment(CombatArena.LoadRunInkCatalog(), economy, 123,
                new Dictionary<string, int[]> { ["base_sword"] = new[] { 0 } });
            equipment.BeginStage(1); return equipment;
        }
        [Test] public void CurrentInkEditingPreservesIdentityCooldownAndGold()
        {
            var equipment = Create(); var weapon = equipment.DebugConfigureWeapon(0, "base_sword", 0);
            int gold = equipment.Economy.Gold; var ink = equipment.Catalog.inks[0];
            equipment.DebugSetInk(weapon.Id, 0, ink.id); var mount = weapon.Slots[0]; mount.Activate();
            equipment.DebugSetSlotColor(weapon.Id, 0, ink.color);
            equipment.DebugSetInk(weapon.Id, 0, ink.id);
            Assert.That(weapon.Slots[0], Is.SameAs(mount)); Assert.That(mount.Cooldown, Is.EqualTo(ink.cooldown));
            Assert.That(mount.Value, Is.EqualTo(ink.enhancedValue));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugSetInk(weapon.Id, 0, "legacy_missing"));
            Assert.That(mount.Ink, Is.SameAs(ink)); Assert.That(equipment.Economy.Gold, Is.EqualTo(gold));
            equipment.DebugSetInk(weapon.Id, 0, null); Assert.That(mount.Cooldown, Is.Zero);
        }
        [Test] public void EveryCurrentInkUsesItsBaseAndEnhancedValuesAndOneToSixSlots()
        {
            var equipment = Create(); var weapon = equipment.DebugConfigureWeapon(0, "base_sword", 0);
            Assert.That(equipment.Catalog.inks.Length, Is.EqualTo(39));
            Assert.That(equipment.Catalog.inks.Any(i => i.effect == "poison" || i.condition == "poison"), Is.False);
            foreach (var ink in equipment.Catalog.inks)
            {
                equipment.DebugSetInk(weapon.Id, 0, ink.id);
                equipment.DebugSetSlotColor(weapon.Id, 0, "neutral"); Assert.That(weapon.Slots[0].Value, Is.EqualTo(ink.baseValue));
                equipment.DebugSetSlotColor(weapon.Id, 0, ink.color); Assert.That(weapon.Slots[0].Value, Is.EqualTo(ink.enhancedValue));
            }
            for(int count=1;count<=6;count++) { equipment.DebugResizePalette(weapon.Id,count); Assert.That(weapon.Slots.Count,Is.EqualTo(count)); }
            for(int count=6;count>=1;count--)equipment.DebugResizePalette(weapon.Id,count);
            Assert.That(equipment.Equipped(0).Id,Is.EqualTo(weapon.Id));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugResizePalette(weapon.Id,0));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugResizePalette(weapon.Id,7));
        }
        [Test] public void TemporaryProfileRestoresTheOriginalOwnerWithoutSavingIt()
        {
            var original = new MemoryProfileStore();
            Chapter1ProfileHost.UseStore(original);
            try
            {
                var service = Chapter1ProfileHost.Service; int before = original.Saves;
                using (Chapter1ProfileHost.UseTemporaryStore(new MemoryProfileStore()))
                {
                    Assert.That(Chapter1ProfileHost.Service, Is.Not.SameAs(service));
                    Chapter1ProfileHost.Service.PlanRun(new[] { "sword", "blunt" });
                }
                Assert.That(Chapter1ProfileHost.Service, Is.SameAs(service));
                Assert.That(original.Saves, Is.EqualTo(before));
            }
            finally { Chapter1ProfileHost.UseStore(null); }
        }
    }

    public sealed class GameSandboxIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            GameSandboxTools.Prepare();
            yield return new EnterPlayMode();
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            ForestCombatV2.Editor.DummyTrainingTools.SelectNormalMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        static IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 50;
            while ((!GameSandbox.Instance || GameSandbox.Instance.Loading) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameSandbox.Instance, Is.Not.Null);
            Assert.That(GameSandbox.Instance.Loading, Is.False, GameSandbox.Instance.Message);
            Assert.That(GameSandbox.Instance.Arena, Is.Not.Null, GameSandbox.Instance.Message);
        }
        [UnityTest] public IEnumerator RealEquipmentPausesAndPersistsAcrossChaptersWithoutAProfileRun()
        {
            var sandbox = GameSandbox.Instance; var arena = sandbox.Arena;
            Assert.That(sandbox.IsOpen, Is.True); Assert.That(sandbox.Growth.IsSandbox, Is.True);
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F3)); InputSystem.Update(); keyboard.MakeCurrent(); sandbox.SendMessage("Update");
                Assert.That(sandbox.IsOpen, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F3)); InputSystem.Update(); sandbox.SendMessage("Update");
                Assert.That(sandbox.IsOpen, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
            Assert.That(Object.FindAnyObjectByType<Chapter1RunBinding>().ProfileRun, Is.False);
            float now = arena.Now; yield return null; yield return null; Assert.That(arena.Now, Is.EqualTo(now));
            Assert.That(Object.FindAnyObjectByType<Chapter1StartingBlueprints>().Complete, Is.True);
            foreach (var form in arena.catalog.forms)
                for (int skill = 0; skill < form.specials.Length; skill++)
                {
                    Assert.That(sandbox.ConfigureWeapon(0, form.id, skill), Is.True, sandbox.Message);
                    yield return null;
                    Assert.That(arena.catalog.forms[arena.Player.weapons[0].FormIndex].id,Is.EqualTo(form.id));
                }
            Assert.That(sandbox.ConfigureWeapon(0, "shield", 1), Is.True, sandbox.Message);
            var ink = sandbox.InkCatalog.inks[0]; var id = ink.id;
            Assert.That(sandbox.ConfigurePalette(0, new[] { id, null, null, null, null }, new[] { ink.color, "neutral", "red", "neutral", "blue" }), Is.True);
            Assert.That(sandbox.SetAttackLevel(2), Is.True);
            Assert.That(sandbox.ApplyTuning(0, "2", "3", "1.5", "0.25"), Is.True);
            Assert.That(sandbox.SetMaxHealth("275"), Is.True);
            Assert.That(sandbox.SetMaxHealth("NaN"), Is.False);
            Assert.That(sandbox.ApplyTuning(0, "bad", "3", "1.5", "0.25"), Is.False);
            sandbox.SetOpen(false); yield return null; yield return new WaitForFixedUpdate();
            Assert.That(arena.Now, Is.GreaterThan(now));
            Assert.That(sandbox.Travel(2, "maintenance"), Is.True); yield return Ready();
            Assert.That(sandbox.RoomId, Is.EqualTo("M"));
            Assert.That(sandbox.Equipment.Equipped(0).Slots[0].Ink.id, Is.EqualTo(id));
            Assert.That(sandbox.Equipment.Equipped(0).Slots[0].Color, Is.EqualTo(ink.color));
            Assert.That(sandbox.Equipment.Equipped(0).Slots[0].Value, Is.EqualTo(ink.enhancedValue));
            Assert.That(sandbox.Arena.catalog.forms[sandbox.Arena.Player.weapons[0].FormIndex].id, Is.EqualTo("shield"));
            Assert.That(sandbox.Arena.Player.weapons[0].SpecialIndex, Is.EqualTo(1));
            Assert.That(sandbox.Arena.Player.weapons[0].TestBasicDamageScale, Is.EqualTo(2));
            Assert.That(sandbox.Arena.Player.maxHealth, Is.EqualTo(275));
            Assert.That(sandbox.Growth.AttackLevel, Is.EqualTo(2));
            Assert.That(Object.FindAnyObjectByType<Chapter2World>().SourceRun.Plan, Is.Null);
        }
        [UnityTest] public IEnumerator CurrentInkPaletteUsesBothSlotsRejectsLegacyIdsAndSurvivesRestart()
        {
            var sandbox=GameSandbox.Instance;
            Assert.That(sandbox.InkCatalog,Is.SameAs(CombatArena.LoadRunInkCatalog()));
            Assert.That(sandbox.InkCatalog.inks.Length,Is.EqualTo(39));
            Assert.That(Object.FindAnyObjectByType<SketchRunBinding>(),Is.Null,"Test editing must not open progression rewards.");
            foreach(int slot in new[]{0,1})
            {
                var weapon=sandbox.Equipment.Equipped(slot);var id=weapon.Id;
                foreach(var ink in sandbox.InkCatalog.inks)
                {
                    Assert.That(sandbox.ConfigurePalette(slot,new[]{ink.id},new[]{"neutral"}),Is.True,sandbox.Message);
                    Assert.That(weapon.Slots[0].Ink,Is.SameAs(ink));Assert.That(weapon.Slots[0].Value,Is.EqualTo(ink.baseValue));
                    Assert.That(sandbox.ConfigurePalette(slot,new[]{ink.id},new[]{ink.color}),Is.True,sandbox.Message);
                    Assert.That(weapon.Slots[0].Value,Is.EqualTo(ink.enhancedValue));
                }
                Assert.That(sandbox.Equipment.Equipped(slot).Id,Is.EqualTo(id));
            }
            var latest=sandbox.InkCatalog.inks[0];
            var legacy=sandbox.Growth.Data.parts.First(p=>sandbox.InkCatalog.Find(p.id)==null).id;
            var before=sandbox.Equipment.Equipped(0).Slots[0];
            Assert.That(sandbox.ConfigurePalette(0,new[]{legacy},new[]{"neutral"}),Is.False);
            Assert.That(sandbox.ConfigurePalette(0,new[]{latest.id},new[]{"missing"}),Is.False);
            Assert.That(sandbox.Equipment.Equipped(0).Slots[0],Is.SameAs(before));
            Assert.That(sandbox.ConfigurePalette(0,new string[6],Enumerable.Repeat("neutral",6).ToArray()),Is.True);
            Assert.That(sandbox.ConfigurePalette(0,new[]{latest.id},new[]{latest.color}),Is.True);
            var growth=sandbox.Growth;
            sandbox.Arena.MvpGrowth=new WeaponGrowth(growth.Data,growth.Economy);
            try
            {
                Assert.That(sandbox.ConfigureWeapon(0,"hammer",0),Is.False);
                Assert.That(sandbox.ConfigurePalette(0,new[]{latest.id},new[]{latest.color}),Is.False);
            }
            finally { sandbox.Arena.MvpGrowth=growth; }
            sandbox.RestartRoom();yield return Ready();
            Assert.That(sandbox.Equipment.Equipped(0).Slots[0].Ink.id,Is.EqualTo(latest.id));
            Assert.That(sandbox.Equipment.Equipped(0).Slots[0].Color,Is.EqualTo(latest.color));
        }

        [UnityTest] public IEnumerator SelectedCurrentStunInkAppliesBaseAndEnhancedEffectsInCombat()
        {
            var sandbox=GameSandbox.Instance;var arena=sandbox.Arena;var player=arena.Player;
            var ink=sandbox.InkCatalog.inks.First(i=>i.trigger=="basic_attack"&&i.effect=="stun");
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var target=arena.enemies.First(e=>e&&!e.isBoss&&e.AllowsControlEffects);
            using(var driver=arena.AcquireManualSimulation())
            foreach(bool enhanced in new[]{false,true})
            {
                sandbox.SetOpen(true);Assert.That(sandbox.ConfigureWeapon(0,"base_sword",0),Is.True);
                Assert.That(sandbox.ConfigurePalette(0,new string[1],new[]{"neutral"}),Is.True);
                Assert.That(sandbox.ConfigurePalette(0,new[]{ink.id},new[]{enhanced?ink.color:"neutral"}),Is.True);
                player.CancelAction();player.ClearInputState();player.facing=1;
                target.maxHealth=10000;target.ResetActor(player.Position+Vector2.right);target.CancelAttack();target.rootUntil=arena.Now+100;
                Physics2D.SyncTransforms();int hits=target.ReceivedHitCount;
                sandbox.SetOpen(false);
                for(int tick=0;tick<180&&target.ReceivedHitCount==hits;tick++)
                    driver.Step(1f/120,new CombatInput(0,tick==0?CombatButtons.Weapon1Basic:CombatButtons.None));
                Assert.That(target.ReceivedHitCount,Is.GreaterThan(hits),"The actual configured weapon must hit.");
                Assert.That(target.stunUntil-arena.Now,Is.EqualTo(Mathf.Min(sandbox.InkCatalog.controlCap,enhanced?ink.enhancedValue:ink.baseValue)).Within(.03f));
                Assert.That(sandbox.Equipment.Equipped(0).Slots[0].Cooldown,Is.GreaterThan(0));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator BossPhaseButtonsPreserveEquipmentAndRestartSelectedPhase()
        {
            var sandbox = GameSandbox.Instance;
            Assert.That(sandbox.ConfigureWeapon(0, "shield", 1), Is.True);
            Assert.That(sandbox.ApplyTuning(0, "2", "3", "1.5", "0.25"), Is.True);
            Assert.That(sandbox.SetMaxHealth("275"), Is.True);
            foreach (int phase in new[] { 2, 3, 1 })
            {
                Assert.That(sandbox.TravelToBossPhase(phase), Is.True);
                yield return Ready();
                var boss = Object.FindAnyObjectByType<Chapter2World>().Boss;
                var expected = phase == 1 ? Deviant.Chapter2.RoseBlue.Phase.Machine :
                    phase == 2 ? Deviant.Chapter2.RoseBlue.Phase.Duo : Deviant.Chapter2.RoseBlue.Phase.FusedBlue;
                Assert.That(boss.Model.Phase, Is.EqualTo(expected));
                Assert.That(sandbox.IsOpen, Is.False);
                Assert.That(sandbox.Arena.Player.maxHealth, Is.EqualTo(275));
                Assert.That(sandbox.Arena.Player.weapons[0].TestBasicDamageScale, Is.EqualTo(2));
                Assert.That(sandbox.Arena.catalog.forms[sandbox.Arena.Player.weapons[0].FormIndex].id, Is.EqualTo("shield"));
                sandbox.RestartRoom();
                yield return Ready();
                Assert.That(Object.FindAnyObjectByType<Chapter2World>().Boss.Model.Phase, Is.EqualTo(expected));
            }
            sandbox.Travel(2, "start");
            yield return Ready();
            Assert.That(sandbox.BossPhase, Is.Zero);
        }

        [UnityTest] public IEnumerator EveryRoomAndBothBossesAreReachableAndCanBeLeft()
        {
            var sandbox = GameSandbox.Instance;
            foreach (int chapter in new[] { 1, 2 })
            {
                sandbox.Travel(chapter, "start"); yield return Ready();
                var rooms = sandbox.Rooms;
                foreach (var room in rooms)
                {
                    sandbox.Travel(chapter, room.Id); yield return Ready();
                    Assert.That(sandbox.RoomId, Is.EqualTo(room.Id), chapter + ": " + sandbox.Message);
                    var p = sandbox.Arena.Player;
                    Assert.That(sandbox.Arena.SolidOverlap(p.Center, p.size - Vector2.one * .08f), Is.False, chapter + ":" + room.Id);
                    if (chapter == 2) Assert.That(Object.FindAnyObjectByType<Chapter2World>().CurrentRoom, Is.EqualTo(room.Id));
                }
                foreach (var kind in new[] { "elite", "maintenance", "boss", "start" })
                { sandbox.Travel(chapter, kind); yield return Ready(); Assert.That(sandbox.Rooms.Any(r => r.Id == sandbox.RoomId && r.Kind == kind), Is.True, sandbox.Message); }
            }
        }
    }
}
