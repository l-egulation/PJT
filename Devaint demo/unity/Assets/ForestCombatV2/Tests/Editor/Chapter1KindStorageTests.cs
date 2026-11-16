using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1KindStorageTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui,string name)=>(T)gui.GetType().GetField(name,Hidden).GetValue(gui);
        static void Refresh(MonoBehaviour gui)=>gui.GetType().GetMethod("Refresh").Invoke(gui,null);
        // Kept out of the test enumerator: a lambda capturing its locals would not survive the play mode domain reload.
        static bool Owns(Deviant.Run.WeaponGrowth growth,Guid ink)=>growth.Inks.Any(i=>i.Id==ink);
        static MonoBehaviour GuiFor(InventoryRuntimeHost host)=>Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(component=>component.GetType().Name=="TemporaryGameGui"&&component.GetType().GetField("host",Hidden).GetValue(component)==host);
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator WeaponOrderSwapKeepsRealRunInkStampsCooldownsAndHudTogether()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons=false;
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;yield return new EnterPlayMode();
            ForestCombatWorld world=null;InventoryRuntimeHost host=null;
            for(int i=0;i<900&&!(world&&world.Ready&&host);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();host=Object.FindAnyObjectByType<InventoryRuntimeHost>();}
            Assert.IsTrue(world&&world.Ready&&host);yield return null;
            var player=world.arena.Player;var offers=world.GetComponent<Chapter1StartingBlueprints>();
            for(int i=0;i<2;i++){player.Position=offers.OfferPosition(i);Assert.IsTrue(offers.TryChoose(i));}
            foreach(var enemy in world.arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
            player.MaintenanceSite=()=>true;
            host.Advance();
            var a=player.weapons[0];var b=player.weapons[1];
            var ink=host.Grant("stat.basic");Assert.IsNotNull(ink);Assert.IsTrue(host.Inventory.TryEquip(0,0,1,out var error),error);
            var run=world.GetComponent<Chapter1RunBinding>().Run;
            var stamp=Deviant.Run.RunStamp.SkillStamp(2);run.Stash.DebugConfigureStamps(0,null,stamp);
            var palette=host.Growth.Equipped(a.InstanceId);Assert.IsNotNull(palette);
            a.Cooldown.Spend(world.arena.Now);float recovery=a.Cooldown.NextReady;
            var gui=GuiFor(host);
            Refresh(gui);var originalIcons=Field<Image[]>(gui,"weaponIcons").Select(icon=>icon.sprite).ToArray();
            Assert.IsTrue(player.TrySwapWeaponOrder());Refresh(gui);
            Assert.That(player.weapons[0],Is.SameAs(b));Assert.That(player.weapons[1],Is.SameAs(a));
            Assert.That(host.Inventory.EquippedAt(1,1),Is.SameAs(ink));Assert.That(host.Inventory.EquippedAt(0,1),Is.Null);
            Assert.That(run.Stash.EquippedSkillStamp(1),Is.SameAs(stamp));Assert.That(run.Stash.EquippedSkillStamp(0),Is.Null);
            Assert.That(host.Growth.Equipped(player.weapons[1].InstanceId),Is.SameAs(palette));
            Assert.That(a.Cooldown.NextReady,Is.EqualTo(recovery));
            Assert.That(Field<Image[]>(gui,"weaponIcons").Select(icon=>icon.sprite).ToArray(),Is.EqualTo(originalIcons.Reverse().ToArray()));
            // A later inventory application must address the new order and retain the first mounted ink.
            Assert.IsNotNull(host.Grant("stat.basic"));Assert.IsTrue(host.Inventory.TryEquip(0,0,1,out error),error);
            Assert.That(host.Inventory.EquippedAt(1,1),Is.SameAs(ink));Assert.That(a.StatSlots[0],Is.EqualTo(1));Assert.That(b.StatSlots[0],Is.EqualTo(1));
            Assert.IsTrue(player.TrySwapWeaponOrder());Assert.That(player.weapons[0],Is.SameAs(a));
        }
        [UnityTest] public IEnumerator AllKindsShareOneFivePlaceBagAndTheBagReadsRealColoredPickups()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons=false;
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;yield return new EnterPlayMode();
            ForestCombatWorld world=null;InventoryRuntimeHost host=null;
            for(int i=0;i<900&&!(world&&world.Ready&&host);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();host=Object.FindAnyObjectByType<InventoryRuntimeHost>();}
            Assert.IsTrue(world&&world.Ready&&host);yield return null;
            var player=world.arena.Player;var offers=world.GetComponent<Chapter1StartingBlueprints>();
            for(int i=0;i<2;i++){player.Position=offers.OfferPosition(i);Assert.IsTrue(offers.TryChoose(i));}
            foreach(var e in world.arena.enemies)if(e){e.aggressive=false;e.CancelAttack();}
            var growth=host.Growth;Assert.IsNotNull(growth);
            growth.Bind(growth.GrantPalette(new[]{"red","red","red"}).Id,player.weapons[0].InstanceId);
            // Two forms, one special and two colour inks fill the single five-place bag; no kind has room of its own.
            Assert.IsNotNull(host.Grant("form.whip"));Assert.IsNotNull(host.Grant("form.whip"));Assert.IsNotNull(host.Grant("special.range"));
            for(int i=0;i<2;i++)growth.GrantInk("ink3_red_01");
            Assert.AreEqual(5,host.TotalStored);Assert.IsFalse(host.HasBagRoom);
            Assert.IsNull(host.Grant("form.whip"));Assert.IsNull(host.Grant("special.range"));Assert.IsNull(host.Grant("stat.basic"));
            var drop=new GameObject("Full colour pickup").AddComponent<Chapter1RewardPickup>();
            drop.Initialize(world,world.GetComponent<Chapter1RunBinding>().Run.Stash,Chapter1RewardKind.ColoredInk,null,player.Position+Vector2.right*20);drop.ConfigureGrowth(growth,"ink3_red_03");
            using(var sim=world.arena.AcquireManualSimulation()){sim.Step(1);yield return null;}
            Assert.IsTrue(drop.Ready);Assert.IsFalse(drop.TryCollect());Assert.IsTrue(drop);Assert.IsFalse(drop.Collected);
            growth.Sell(growth.Inks[0].Id);Assert.IsTrue(drop.TryCollect());Assert.AreEqual(2,growth.Inks.Count);
            var room=world.Data.rooms.First(r=>Chapter1MaintenanceFacility.IsFacility(r.kind));player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));Physics2D.SyncTransforms();
            var shop=world.GetComponent<Chapter1MaintenanceShop>();Assert.IsTrue(shop.TryOpen());growth.Economy.Earn(1000);
            int gold=growth.Economy.Gold,rolled=growth.ColorInksRolled,inks=growth.Inks.Count;Assert.Greater(shop.InkStock.Count,0);
            Assert.IsFalse(shop.TryBuyInk(0,out var full));StringAssert.Contains("가방이 가득",full);Assert.AreEqual(gold,growth.Economy.Gold);Assert.IsFalse(shop.InkStock[0].Sold);Assert.AreEqual(inks,growth.Inks.Count);Assert.AreEqual(rolled,growth.ColorInksRolled);shop.Close();
            host.Presenter.Open();
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui"&&b.GetType().GetField("host",Hidden).GetValue(b)!=null);
            Refresh(gui);CheckNativeSharedBag(gui,host,growth,player.weapons[0].InstanceId);
            host.Presenter.Close();Refresh(gui);
            var run=world.GetComponent<Chapter1RunBinding>().Run;run.MarkBossDefeated();var altar=world.GetComponent<Chapter1BossEncounter>().Altar;player.Position=altar.Position;Physics2D.SyncTransforms();yield return null;
            growth.GrantInk("ink3_red_01");Assert.IsTrue(altar.TryOpen());Assert.IsTrue(altar.TryChooseColor("blue"));
            Assert.IsFalse(altar.TryClaim(0));Assert.IsNull(altar.Claimed);Assert.IsFalse(altar.Finished);
            growth.Sell(growth.Inks[0].Id);Assert.IsTrue(altar.TryClaim(0));Assert.IsNotNull(altar.Claimed);Assert.AreEqual(2,growth.Inks.Count);
        }
        static void CheckNativeSharedBag(MonoBehaviour gui,InventoryRuntimeHost host,Deviant.Run.WeaponGrowth growth,Guid weapon)
        {
            var book=Field<GameObject>(gui,"bag");
            System.Func<string,Button> find=name=>book.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            System.Action<string> click=name=>{find(name).onClick.Invoke();Refresh(gui);};
            Assert.That(book.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("잉크 5 / 5")));
            var first=growth.Inks[0];click("Owned item "+first.Id);
            Assert.IsFalse(find("Book primary").interactable,"several matching empty slots require an explicit target");
            click("Palette slot 0");click("Book primary");Assert.AreEqual(1,growth.Inks.Count);
            int gold=growth.Economy.Gold;Guid replaced=growth.Equipped(weapon).Slots[0].InstanceId;
            Assert.IsTrue(host.TryEquipColor(growth.Inks[0].Id,0,0,out var error),error);Assert.AreEqual(gold,growth.Economy.Gold);Assert.IsTrue(Owns(growth,replaced));
            Refresh(gui);click("Palette slot 0");var mounted=growth.Equipped(weapon).Slots[0];var mountedId=mounted.InstanceId;int level=mounted.Level;
            click("Book unequip");Assert.IsNull(growth.Equipped(weapon).Slots[0].Part);Assert.AreEqual(level,growth.Equipped(weapon).Slots[0].Level);Assert.IsTrue(Owns(growth,mountedId));Assert.AreEqual(gold,growth.Economy.Gold);
            Assert.IsFalse(book.GetComponentsInChildren<Button>().Any(b=>b.name=="Book unequip"));
            Assert.IsTrue(host.TryEquipColor(mountedId,0,0,out error),error);Assert.AreEqual(1,growth.Inks.Count);
            Refresh(gui);click("Palette slot 0");Assert.That(book.GetComponentsInChildren<Text>().Any(t=>t.text==growth.Equipped(weapon).Slots[0].Part.name));
        }
    }
}
