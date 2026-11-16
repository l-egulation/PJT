using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Inventory.Unity;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1NativeEquipmentBookTests
    {
        const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        static ForestCombatWorld world;static MonoBehaviour gui;static Chapter1MaintenanceShop shop;static InventoryRuntimeHost bag;
        static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden|BindingFlags.Public).GetValue(value);
        static void Refresh(MonoBehaviour value)=>value.GetType().GetMethod("Refresh").Invoke(value,null);
        static GameObject Root(MonoBehaviour value,bool maintenance)=>Field<GameObject>(value,maintenance?"shopPanel":"bag");
        static Button Find(GameObject root,string name)=>root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy);
        static void Click(MonoBehaviour value,GameObject root,string name){var button=Find(root,name);Assert.IsNotNull(button,name);Assert.IsTrue(button.interactable,name);button.onClick.Invoke();Refresh(value);}
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        static void OpenShop()
        {
            var room=world.Data.rooms.First(r=>Chapter1MaintenanceFacility.IsFacility(r.kind));world.arena.Player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));Physics2D.SyncTransforms();
            Assert.IsTrue(shop.TryOpen());shop.Service.Economy.DebugSetGold(2000);Refresh(gui);
        }
        static void AssertAction(Button button)
        {Assert.IsNotNull(button);Assert.AreEqual(new Vector2(167,39),((RectTransform)button.transform).sizeDelta);Assert.AreEqual(-361,((RectTransform)button.transform).anchoredPosition.y);}
        [UnityTest] public IEnumerator OpenedPaletteSlotsAndEquippedInkInspectionAreConsistent()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            yield return new EnterPlayMode();
            for(int i=0;i<900&&!(world&&world.Ready&&world.GetComponent<Chapter1RunBinding>());i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.IsTrue(world&&world.Ready);yield return null;yield return null;
            BindTestRig();
            CheckInventory();
        }
        [UnityTest] public IEnumerator ShopTransactionsKeepTheApprovedLayoutAndRecolorIntent()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            yield return new EnterPlayMode();
            for(int i=0;i<900&&!(world&&world.Ready&&world.GetComponent<Chapter1RunBinding>());i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.IsTrue(world&&world.Ready);yield return null;yield return null;
            BindTestRig();
            CheckShop();
        }
        internal static bool WorldReady()
        {
            world=Object.FindAnyObjectByType<ForestCombatWorld>();
            return world&&world.Ready&&world.GetComponent<Chapter1RunBinding>();
        }
        // Capturing lambdas live outside the iterator so their closure objects are created after domain reload.
        internal static void BindTestRig()
        {
            var offers=world.GetComponent<Chapter1StartingBlueprints>();
            for(int i=0;i<2;i++){world.arena.Player.Position=offers.OfferPosition(i);Assert.IsTrue(offers.TryChoose(i));}
            foreach(var enemy in world.arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
            gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(x=>x.GetType().Name=="TemporaryGameGui"&&x.GetType().GetField("host",Hidden).GetValue(x)!=null);
            shop=world.GetComponent<Chapter1MaintenanceShop>();bag=world.arena.GetComponent<InventoryRuntimeHost>();Refresh(gui);
        }
        internal static void CheckInventory()
        {
            bag.Presenter.Open();Refresh(gui);var root=Root(gui,false);
            Assert.IsTrue(root.activeInHierarchy);Assert.AreEqual(new Vector2(800,440),((RectTransform)root.transform).sizeDelta);
            Assert.AreEqual(2,root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Book tab ")));
            foreach(int count in new[]{3,4,5,3})
            {
                var palette=shop.Growth.GrantPalette(new[]{"blue","green","red","yellow","purple"}.Take(count).ToArray());shop.Growth.Bind(palette.Id,world.arena.Player.weapons[0].InstanceId);Refresh(gui);
                Assert.AreEqual(count,root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Palette slot ")));
                Click(gui,root,"Palette slot 1");Assert.That(root.GetComponentsInChildren<Text>().Any(t=>t.text=="초록 칸"));
                var empty=root.GetComponentsInChildren<Image>().First(i=>i.sprite&&i.sprite==Resources.Load<Sprite>("ApprovedItemIcons/backing-ink-green"));Assert.Less(empty.color.a,1);
            }
            shop.Growth.DebugSetSlotColor(world.arena.Player.weapons[0].InstanceId,0,"colorless");Refresh(gui);
            Assert.That(Find(root,"Palette slot 0").GetComponentsInChildren<Image>().Any(i=>i.sprite&&i.sprite.name.Contains("slot-empty")),"empty wildcard keeps its outline");
            Assert.IsNotNull(shop,"shop before grant"); Assert.IsNotNull(shop.Growth,"growth before grant"); var ink=shop.Growth.GrantInk("ink3_blue_01"); Assert.IsNotNull(ink,"granted blue ink"); Assert.IsNotNull(world.arena.Player,"player before insert"); shop.Growth.Insert(world.arena.Player.weapons[0].InstanceId,0,ink.Id); Refresh(gui); Click(gui,root,"Palette slot 0");
            Assert.That(root.GetComponentsInChildren<Text>().Any(t=>t.text==ink.Part.name));Assert.IsNotNull(Find(root,"Book unequip"));Assert.IsNull(Find(root,"Book upgrade"));
            var description=root.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Book effect description");
            Assert.AreEqual(ink.Part.description,description.content.GetComponentInChildren<Text>().text);
            Assert.That(root.GetComponentsInChildren<Text>().Select(t=>t.text),Does.Not.Contain(ink.Part.StatsAt(0)));
            Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"native-inventory-approved",1920,1080);
        }
        internal static void CheckShop()
        {
            OpenShop();var root=Root(gui,true);var g=shop.Growth;var weapon=world.arena.Player.weapons[0].InstanceId;
            var palette=g.GrantPalette(new[]{"blue","green","red"});g.Bind(palette.Id,weapon);
            var red=g.GrantInk("ink3_red_05"); Assert.IsNotNull(red,"granted red ink"); g.Insert(weapon,2,red.Id); var blue=g.GrantInk("ink3_blue_01"); Assert.IsNotNull(blue,"granted blue ink"); var green=g.GrantInk("ink3_green_10"); Assert.IsNotNull(green,"granted green ink"); Refresh(gui);
            Click(gui,root,"Palette slot 2");int gold=g.Economy.Gold;Click(gui,root,"Book upgrade");Assert.AreEqual(1,palette.Slots[2].Level);Assert.AreEqual(gold-50,g.Economy.Gold);Assert.IsNotNull(Find(root,"Book upgrade"));
            Click(gui,root,"Book recolor");Click(gui,root,"Owned item "+blue.Id);AssertAction(Find(root,"Book primary"));Assert.IsNull(Find(root,"Book sell"));Assert.IsNull(Find(root,"Book destroy"));
            Click(gui,root,"Owned item "+green.Id);var state=Field<object>(gui,"nativeShop");Assert.AreEqual(2,Field<int>(state,"Slot"));Assert.AreEqual("Recolor",Field<object>(state,"Action").ToString());
            Click(gui,root,"Book cancel");Assert.AreEqual(gold-50,g.Economy.Gold);Assert.AreSame(red.Part,palette.Slots[2].Part);
            Click(gui,root,"Book recolor");Click(gui,root,"Owned item "+blue.Id);Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"native-recolor-approved",1920,1080);
            Click(gui,root,"Book primary");Assert.AreEqual(gold-150,g.Economy.Gold);Assert.AreEqual("blue",palette.Slots[2].Color);Assert.AreSame(blue.Part,palette.Slots[2].Part);Assert.AreEqual(1,palette.Slots[2].Level);Assert.That(g.Inks.Any(i=>i.Id==red.Id));
            Click(gui,root,"Book recolor");Click(gui,root,"Owned item "+green.Id);Click(gui,root,"Book cancel");
            var stamp=shop.Service.Stash.AddWeaponStamp("sword","whip");Refresh(gui);Click(gui,root,"Book tab 2");Click(gui,root,"Owned item "+stamp.Id);AssertAction(Find(root,"Book primary"));AssertAction(Find(root,"Book sell"));AssertAction(Find(root,"Book cancel"));
            Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"native-stamps-approved",1920,1080);int before=g.Economy.Gold;Click(gui,root,"Book primary");Assert.AreSame(stamp,shop.Service.Stash.EquippedWeaponStamp(0));Assert.AreEqual(before,g.Economy.Gold);
            var replacement=shop.Service.Stash.AddWeaponStamp("sword","rapier");Refresh(gui);Click(gui,root,"Owned item "+replacement.Id);
            Assert.AreEqual("20",Find(root,"Book primary").transform.Find("Gold price/Value").GetComponent<Text>().text);
            Click(gui,root,"Book primary");Assert.AreSame(replacement,shop.Service.Stash.EquippedWeaponStamp(0));Assert.AreEqual(before-20,g.Economy.Gold);before=g.Economy.Gold;
            var skill=shop.Service.Stash.AddSkillStamp(1);Refresh(gui);Click(gui,root,"Owned item "+skill.Id);Click(gui,root,"Book sell");Click(gui,root,"Book primary");Assert.AreEqual(before+25,g.Economy.Gold);Assert.IsFalse(shop.Service.Stash.Stamps.Contains(skill));
            Click(gui,root,"Book tab 1");AssertAction(Find(root,"Book primary"));Assert.IsNull(Find(root,"Book cancel"));Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"native-growth-approved",1920,1080);
            Click(gui,root,"Growth slot service");Click(gui,root,"Palette slot 2");before=g.Economy.Gold;Click(gui,root,"Book upgrade");Assert.AreEqual(2,palette.Slots[2].Level);Assert.AreEqual(before-100,g.Economy.Gold);
            g.DebugSetPart(weapon,2,palette.Slots[2].Part.id,5);Refresh(gui);Assert.IsFalse(Find(root,"Book upgrade").interactable);Assert.AreEqual(before-100,g.Economy.Gold);
            g.DebugSetPart(weapon,2,palette.Slots[2].Part.id,2);g.Economy.DebugSetGold(49);Refresh(gui);Assert.IsFalse(Find(root,"Book upgrade").interactable);Assert.AreEqual(2,palette.Slots[2].Level);g.Economy.DebugSetGold(2000);Refresh(gui);
            Click(gui,root,"Book tab 3");Assert.IsFalse(root.GetComponentsInChildren<ScrollRect>().Any(s=>s.name=="Book owned items"));AssertAction(Find(root,"Book buy"));Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"native-stall-approved",1920,1080);
            var offer=shop.InkStock[0];before=g.Economy.Gold;UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Find(root,"Book buy").gameObject);Click(gui,root,"Book buy");Assert.IsTrue(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy,"purchase preserves a usable focus");Assert.AreEqual(before-offer.Price,g.Economy.Gold);Assert.IsTrue(offer.Sold);Assert.IsFalse(Find(root,"Book buy").interactable);
            Click(gui,root,"Ink offer 1");g.Economy.DebugSetGold(0);Refresh(gui);Assert.IsFalse(Find(root,"Book buy").interactable);
            shop.Close();Refresh(gui);Assert.IsNull(Field<object>(Field<object>(gui,"nativeShop"),"Key"),"closing invalidates the facility cache");
            foreach(var room in world.Data.rooms.Where(r=>Chapter1MaintenanceFacility.IsFacility(r.kind)).Take(2))
            {
                world.arena.Player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));Physics2D.SyncTransforms();Assert.IsTrue(shop.TryOpen());Refresh(gui);
                foreach(var entry in shop.InkStock)Assert.That(root.GetComponentsInChildren<Text>().Any(t=>t.text==entry.Part.name),"render current facility offers");
                shop.Close();Refresh(gui);
            }

        }
        [Test] public void StampSaleRejectsMissingOrAlreadySoldItemsWithoutPayingAgain()
        {
            var economy=new RunEconomy(new EconomyData{stampSaleGold=25});var stash=new RunStash();var stamp=stash.AddSkillStamp(1);
            Assert.IsFalse(stash.TrySell(Guid.NewGuid(),economy,out _));Assert.AreEqual(0,economy.Gold);
            Assert.IsTrue(stash.TrySell(stamp.Id,economy,out _));Assert.AreEqual(25,economy.Gold);Assert.AreEqual(0,stash.Stamps.Count);
            Assert.IsFalse(stash.TrySell(stamp.Id,economy,out _));Assert.AreEqual(25,economy.Gold);
            var retained=stash.AddSkillStamp(2);Assert.IsFalse(stash.TrySell(retained.Id,null,out _));Assert.That(stash.Stamps.Contains(retained));
            Assert.IsTrue(stash.TryApply(0,retained.Id,"sword",economy,_=>null,out _));Assert.IsFalse(stash.TrySell(retained.Id,economy,out _));Assert.AreSame(retained,stash.EquippedSkillStamp(0));Assert.AreEqual(25,economy.Gold);

        }
    }
}
