using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The held stage key beside the health bar (docs/design/world/stage-gimmicks.md), and ink rule lines that only claim the
    // run-long lock for legacy inks (weapons-and-ink.md 정비·재고·교환).
    public sealed class Chapter1HudKeyAndBagMessageTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetField(name, Hidden).GetValue(gui);
        static T Property<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetProperty(name).GetValue(gui);
        static string Constant(MonoBehaviour gui, string name) => (string)gui.GetType().GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        static void Click(MonoBehaviour gui, Button button) { Assert.IsTrue(button.interactable, button.name); button.onClick.Invoke(); Refresh(gui); }
        static Button Owned(MonoBehaviour gui, System.Guid id) => Field<RectTransform>(gui, "growthInks").GetComponentsInChildren<Button>().Single(b => b.name == "GrowthInk" + id);
        static Button OwnedStamp(MonoBehaviour gui,System.Guid id)
        {
            foreach(var button in Field<RectTransform>(gui,"stampPageOwned").GetComponentsInChildren<Button>())if(button.name=="Owned stamp "+id)return button;
            Assert.Fail("Missing owned stamp "+id);return null;
        }
        static void SettleReward(ForestCombatWorld world,Chapter1RewardPickup pickup)
        {
            using(var simulation=world.arena.AcquireManualSimulation())for(int i=0;i<30&&!pickup.Ready;i++)simulation.Step(.02f);
            Assert.IsTrue(pickup.Ready,"reward settles on the arena simulation clock");
        }

        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator AcceptedPlayerHitsFlashTheGaugeAndShakeTheWholeHealthGroupWithoutDrift()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            ForestCombatWorld world=null;
            for(int frame=0;frame<600&&(!world||!world.Ready);frame++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);yield return null;yield return null;
            Assert.IsTrue(world.GetComponent<Chapter1StartingBlueprints>().ApplyPreselected(new[]{"sword","blunt"}));
            var player=world.arena.Player;
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui"&&Field<InventoryRuntimeHost>(b,"host"));
            Refresh(gui);
            var root=Field<RectTransform>(gui,"combatHealthRoot");var resting=root.anchoredPosition;
            var fill=Field<Image>(gui,"healthFill");var flash=Field<Image>(gui,"healthHitFlash");
            Assert.That(Field<Text>(gui,"healthText").transform.parent,Is.SameAs(root));
            Assert.That(Field<Text>(gui,"healthMax").transform.parent,Is.SameAs(root));
            Assert.That(root.GetComponentsInChildren<Image>().Any(i=>i.sprite&&i.sprite.name=="hero-portrait"),Is.True);
            using(var simulation=world.arena.AcquireManualSimulation())
            {
                // Editor HP assignment and recovery do not count as received hits.
                player.health=90;Refresh(gui);Assert.IsFalse(flash.gameObject.activeSelf);Assert.That(root.anchoredPosition,Is.EqualTo(resting));
                player.health=100;Refresh(gui);Assert.IsFalse(flash.gameObject.activeSelf);
                int hits=player.ReceivedHitCount;
                Assert.IsTrue(player.Receive(10,null,1,false,null,bypass:true));Refresh(gui);
                Assert.That(player.ReceivedHitCount,Is.EqualTo(hits+1));Assert.IsTrue(flash.gameObject.activeSelf);
                Assert.That(flash.fillAmount,Is.EqualTo(fill.fillAmount).Within(.0001f));
                Assert.That(flash.material.shader.name,Is.EqualTo("Deviant/JournalText"),"the red sprite's alpha is rendered in white");
                Assert.That(flash.color,Is.EqualTo(Color.white));
                Assert.That(root.anchoredPosition,Is.Not.EqualTo(resting));
                Assert.That((root.anchoredPosition-resting).magnitude*960/1670,Is.LessThanOrEqualTo(3));
                Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"health-hit-flash-1920",1920,1080);
                Assert.IsTrue(player.Receive(5,null,-1,false,null,bypass:true));Refresh(gui);
                Assert.That((root.anchoredPosition-resting).magnitude*960/1670,Is.LessThanOrEqualTo(3),"repeated hits never add displacement");
                float began=Field<float>(gui,"healthHitAt");Refresh(gui);Assert.That(Field<float>(gui,"healthHitAt"),Is.EqualTo(began),"refreshing a hit does not replay it");
                yield return new WaitForSecondsRealtime(.3f);Refresh(gui);
                Assert.IsFalse(flash.gameObject.activeSelf);Assert.That(root.anchoredPosition,Is.EqualTo(resting));
                Assert.That(fill.sprite.name,Is.EqualTo("health-fill-red"));Assert.That(player.health,Is.EqualTo(85));
                Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"health-hit-restored-1920",1920,1080);
                Assert.IsTrue(player.Receive(1,null,1,false,null,bypass:true));Refresh(gui);Assert.IsTrue(flash.gameObject.activeSelf);
                gui.enabled=false;Assert.IsFalse(flash.gameObject.activeSelf);Assert.That(root.anchoredPosition,Is.EqualTo(resting));
            }
        }

        [UnityTest] public IEnumerator StampTabSelectsLiveEquipmentAndAppliesFreeAndPaidChangesInPlace()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            for(int frame=0;frame<900&&!Chapter1NativeEquipmentBookTests.WorldReady();frame++)yield return null;
            Assert.IsTrue(Chapter1NativeEquipmentBookTests.WorldReady());yield return null;yield return null;
            Chapter1NativeEquipmentBookTests.BindTestRig();
            Chapter1NativeEquipmentBookTests.CheckShop();
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator PickupsUseTypedTopTextAndMerchantsOnlyShowNearbyKey()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            ForestCombatWorld world=null;
            for(int frame=0;frame<600&&(!world||!world.Ready);frame++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);yield return null;yield return null;
            var player=world.arena.Player;
            var offers=world.GetComponent<Chapter1StartingBlueprints>();Assert.IsTrue(offers.ApplyPreselected(new[]{"sword","blunt"}));
            foreach(var enemy in world.arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui"&&Field<InventoryRuntimeHost>(b,"host"));
            var merchant=world.GetComponents<MonoBehaviour>().Single(b=>b.GetType().Name=="MaintenanceRoomPresentation");
            // Model a repeat visit without writing any profile progression from the fixture.
            var visits=merchant.GetType().GetField("visits",Hidden).GetValue(merchant);
            ((bool[])visits.GetType().GetField("seen").GetValue(visits))[0]=true;
            var anchor=(Vector2)merchant.GetType().GetProperty("ReviewAnchor").GetValue(merchant);
            player.Position=anchor+Vector2.left*7;player.velocity=Vector2.zero;Physics2D.SyncTransforms();Refresh(gui);
            Assert.That(Field<Image>(gui,"maintenanceKey").gameObject.activeSelf,Is.False,"inside the room but away from its merchant");
            Assert.That(Field<Text>(gui,"hint").text,Does.Not.Contain("정비소"));
            player.Position=anchor+Vector2.left*3;player.velocity=Vector2.zero;Physics2D.SyncTransforms();world.RefreshCamera(0,true);Refresh(gui);
            yield return new WaitForSecondsRealtime(3.6f);world.RefreshCamera(0,true);Refresh(gui);
            var key=Field<Image>(gui,"maintenanceKey");Assert.IsTrue(key.gameObject.activeSelf);
            Assert.That(key.GetComponentInChildren<Text>().text,Is.EqualTo("F"));
            var merchantRenderers=merchant.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Merchant ")).ToArray();
            Assert.That(merchantRenderers.Length,Is.EqualTo(2));
            foreach(var renderer in merchantRenderers)Assert.That(renderer.sharedMaterial.shader,Is.SameAs(player.visual.sharedMaterial.shader));
            Assert.That(merchantRenderers[0].sharedMaterial,Is.Not.SameAs(merchantRenderers[1].sharedMaterial));
            var bag=world.arena.GetComponent<InventoryRuntimeHost>();bag.Presenter.Reset();
            var ink=bag.Growth.GrantInk("ink3_red_01");bag.Presenter.Tick(Time.unscaledTimeAsDouble);Refresh(gui);
            Assert.That(Field<Text>(gui,"toast").text,Is.EqualTo(ink.Part.name+" 잉크 획득"));
            var note=Field<GameObject>(gui,"toastPanel");Assert.That(note.GetComponentsInChildren<Image>(),Is.Empty);
            Assert.That(((RectTransform)note.transform).anchoredPosition.y,Is.EqualTo(-18));
            Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"merchant-key-pickup-1920",1920,1080);
            bag.Presenter.Reset();
            var drop=new GameObject("Stamp pickup fixture").AddComponent<Chapter1RewardPickup>();
            drop.Initialize(world,world.GetComponent<Chapter1RunBinding>().Run.Stash,Chapter1RewardKind.WeaponStamp,"sword",anchor+Vector2.right*3);
            drop.ConfigureStamp(Deviant.Run.RunStamp.Weapon("sword","whip"));
            SettleReward(world,drop);Assert.IsTrue(drop.TryCollect());
            bag.Presenter.Tick(Time.unscaledTimeAsDouble);Refresh(gui);
            Assert.That(Field<Text>(gui,"toast").text,Is.EqualTo("채찍 무기 스탬프 획득"));
            var count=world.GetComponent<Chapter1RunBinding>().Run.Stash.Stamps.Count;
            Assert.IsFalse(drop.TryCollect());Assert.That(world.GetComponent<Chapter1RunBinding>().Run.Stash.Stamps.Count,Is.EqualTo(count));
            bag.Presenter.Reset();
            var skill=new GameObject("Skill pickup fixture").AddComponent<Chapter1RewardPickup>();
            skill.Initialize(world,world.GetComponent<Chapter1RunBinding>().Run.Stash,Chapter1RewardKind.SkillStamp,null,anchor+Vector2.right*3);
            skill.ConfigureStamp(Deviant.Run.RunStamp.SkillStamp(1));
            SettleReward(world,skill);Assert.IsTrue(skill.TryCollect());
            bag.Presenter.Tick(Time.unscaledTimeAsDouble);Refresh(gui);
            Assert.That(Field<Text>(gui,"toast").text,Is.EqualTo("범위 스킬 스탬프 획득"));
            Chapter1MaintenanceGuiCaptureTests.Capture(world,gui,"merchant-stamp-pickup-1920",1920,1080);
            var shop=world.GetComponent<Chapter1MaintenanceShop>();Assert.IsTrue(shop.TryOpen());Refresh(gui);
            Assert.IsFalse(key.gameObject.activeSelf,"no interaction key while the shop owns input");shop.Close();
        }

        [UnityTest] public IEnumerator HeldKeyShowsBesideHealthAndInkLinesNeverLockColourInks()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>()); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            var player = world.arena.Player; var offers = world.GetComponent<Chapter1StartingBlueprints>();
            for (int i = 0; i < 2; i++) { player.Position = offers.OfferPosition(i); Assert.IsTrue(offers.TryChoose(i)); }
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);

            // The key: hidden, shown beside the health bar while held, gone once the chest takes it.
            var keys = world.GetComponent<Chapter1KeyChest>(); keys.Prepare();
            Assert.That(keys.Chest, Is.Not.Null);
            Refresh(gui);
            Assert.That(Property<bool>(gui, "KeyHudShown"), Is.False);
            keys.GrantKeyForVerification(); Refresh(gui);
            Assert.That(Property<bool>(gui, "KeyHudShown"), Is.True);
            var icon = Field<Image>(gui, "keyHud");
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.rectTransform.anchoredPosition.x - icon.rectTransform.sizeDelta.x / 2, Is.GreaterThanOrEqualTo(188), "right of the 172 px health bar");
            player.Position = keys.Chest.Position + Vector2.left; player.velocity = Vector2.zero; Physics2D.SyncTransforms();
            Assert.That(keys.TryOpenChest(), Is.True);
            Assert.That(keys.Opened, Is.True);
            Refresh(gui);
            Assert.That(Property<bool>(gui, "KeyHudShown"), Is.False, "the opened chest used the key");
            // The chest's ink-or-blueprint choice is taken so the bag and shop checks below run unpaused.
            var sketch = world.GetComponent<Chapter1RunBinding>().Run.Sketch;
            if (sketch != null && sketch.PendingOffer != null) sketch.Choose(0);

            CheckNativeInkOptions(gui,world);
        }
        static void CheckNativeInkOptions(MonoBehaviour gui,ForestCombatWorld world)
        {
            var bag=world.arena.GetComponent<InventoryRuntimeHost>();bag.Presenter.Open();Refresh(gui);
            Assert.That(Field<GameObject>(gui,"bag").GetComponentsInChildren<Text>().All(t=>!t.text.Contains("런이 끝날 때까지")));
            bag.Presenter.Close();Refresh(gui);
            var room=world.Data.rooms.First(r=>Chapter1MaintenanceFacility.IsFacility(r.kind));world.arena.Player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));Physics2D.SyncTransforms();
            var shop=world.GetComponent<Chapter1MaintenanceShop>();Assert.IsTrue(shop.TryOpen());var g=shop.Growth;g.Economy.Earn(1000);
            g.Bind(g.GrantPalette(new[]{"red","red","red"}).Id,world.arena.Player.weapons[0].InstanceId);
            var red=g.GrantInk("ink3_red_03");var blue=g.GrantInk("ink3_blue_02");g.Insert(world.arena.Player.weapons[0].InstanceId,0,red.Id);Refresh(gui);
            var book=Field<GameObject>(gui,"shopPanel");System.Func<string,Button> find=name=>book.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            System.Action<string> click=name=>{find(name).onClick.Invoke();Refresh(gui);};
            click("Palette slot 0");click("Book recolor");click("Owned item "+blue.Id);
            Assert.IsFalse(book.GetComponentsInChildren<Button>().Any(b=>b.name=="Book sell"||b.name=="Book destroy"));
            int gold=g.Economy.Gold;click("Book primary");var slot=g.Equipped(world.arena.Player.weapons[0].InstanceId).Slots[0];
            Assert.AreEqual("blue",slot.Color);Assert.AreSame(blue.Part,slot.Part);Assert.AreEqual(gold-g.Data.colorChangeGold,g.Economy.Gold);Assert.IsTrue(g.Inks.Any(i=>i.Id==red.Id));
        }
    }
}
