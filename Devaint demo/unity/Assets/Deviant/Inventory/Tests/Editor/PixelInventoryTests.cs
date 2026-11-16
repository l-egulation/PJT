using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Inventory.Tests
{
    public class PixelInventoryPresenterTests
    {
        [Test] public void PauseAndBagReleaseOnlyTheirOwnLeases()
        {
            using (var controls = new ControlSession())
            using (var other = controls.OpenScope("Other menu"))
            using (var p = new InventoryPresenter(new InkInventory(new[] { "sword", "ranged" }), controls))
            {
                p.TogglePause(); p.TogglePause(); p.Open(); p.TogglePause();
                Assert.That(controls.Current.SimulationPaused, Is.True);
                Assert.That(controls.Current.Blocks(InputChannels.Combat), Is.True);
                var foreign = other.PauseSimulation();
                p.Close();
                Assert.That(controls.Current.SimulationPaused, Is.True); Assert.That(controls.Current.Blocks(InputChannels.Combat), Is.False);
                foreign.Dispose(); Assert.That(controls.Current.SimulationPaused, Is.False);
                p.TogglePause(); p.Reset(); Assert.That(p.Paused, Is.False); Assert.That(controls.Current.SimulationPaused, Is.False);
            }
        }
        [Test] public void ToastWaitsWhileBagIsOpenAndEverySuccessRemainsIndividual()
        {
            var bag = new InkInventory(new[] { "sword" });
            using (var p = new InventoryPresenter(bag, null))
            {
                var a = new InkInstance(new InkDefinition("power", "Power", "", InkKind.Stat, 1));
                var b = new InkInstance(a.Definition);
                bag.TryAcquire(a, out _); p.Tick(0); p.Open(); p.Tick(50);
                bag.TryAcquire(b, out _); p.Tick(100); Assert.That(p.Toast, Is.SameAs(a));
                p.Close(); p.Tick(101); Assert.That(p.Toast, Is.SameAs(a)); p.Tick(103); Assert.That(p.Toast, Is.SameAs(b));
            }
        }
        [Test] public void IdenticalPickupFailuresCoalesceWithoutClaimingAcquisition()
        {
            using (var p = new InventoryPresenter(new InkInventory(new[] { "sword" }), null))
            {
                p.NotifyPickupFailure("full"); p.NotifyPickupFailure("full"); p.Tick(0);
                Assert.That(p.Toast, Is.Null); Assert.That(p.ToastMessage, Is.EqualTo("full"));
                p.NotifyPickupFailure("full"); p.Tick(3); Assert.That(p.ToastVisible, Is.False);
            }
        }
        [Test] public void PixelThemeUsesRestoredSpritesAndStaticKoreanBitmapFaces()
        {
            var theme = Resources.Load<PixelInventoryTheme>("PixelInventory/Theme");
            if (!theme || !theme.Ready) Assert.Ignore("Restore the pixel inventory UI asset ZIP before visual tests.");
            Assert.That(theme.regularFont.dynamic || theme.boldFont.dynamic, Is.False);
            foreach (var font in new[] { theme.regularFont, theme.boldFont })
                foreach (char c in "잉크정비가방장착해제획득검기본채찍힘의공격피해0123456789")
                    Assert.That(font.GetCharacterInfo(c, out _, 11), Is.True, font.name + " missing " + c);
            Assert.That(theme.slotFrame.border.x, Is.GreaterThan(0)); Assert.That(theme.weaponIcons.Length, Is.EqualTo(10));
            Assert.That(theme.itemIcons.Select(i => i.sprite).All(sprite => sprite && sprite.texture.filterMode == FilterMode.Point), Is.True);
        }
    }
    public class PixelInventoryIntegrationTests
    {
        CombatArena arena;
        InventoryRuntimeHost host;
        CombatArena.ManualSimulation simulation;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            arena = Object.FindAnyObjectByType<CombatArena>();
            float deadline = Time.realtimeSinceStartup + 10;
            while (!host || host.Inventory == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                host = arena.GetComponent<InventoryRuntimeHost>(); yield return null;
            }
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            simulation = arena.AcquireManualSimulation(); arena.Player.UseTestInput = true;
            var installedView = arena.GetComponentInChildren<TemporaryInventoryView>();
            if (installedView) Object.Destroy(installedView.gameObject);
            yield return null;
            if (!host.Theme) Assert.Ignore("Restore the pixel inventory UI asset ZIP before visual tests.");
        }
        [UnityTearDown] public IEnumerator Cleanup() { simulation?.Dispose(); Time.timeScale = 1; if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        void Advance(float time)
        { for (int i = 0; i < Mathf.CeilToInt(time / .02f); i++) { simulation.Step(.02f); host.Advance(); } }
        InkInstance Give(string id)
        { var item = new InkInstance(host.Content.Find(id).CreateDefinition()); Assert.That(host.Inventory.TryAcquire(item, out _), Is.True); return item; }
        [UnityTest] public IEnumerator DamagePreviewMatchesTheNextActualHitAndRangeInkReallyFires()
        {
            Give("stat.basic"); host.Presenter.SelectBag(0); Assert.That(host.Presenter.EquipSelected(), Is.True);
            var second = Give("stat.basic"); host.Presenter.SelectBag(0);
            Assert.That(host.SelectedPreview(), Is.EqualTo("12 → 14")); Assert.That(host.Presenter.EquipSelected(), Is.True);
            Assert.That(host.Inventory.EquippedAt(0, 2), Is.SameAs(second)); Assert.That(arena.Player.Weapon.BasicBonus, Is.EqualTo(.44f).Within(1e-5f));
            var enemy = arena.enemies[0]; enemy.health = enemy.maxHealth = 1000; enemy.Position = arena.Player.Position + Vector2.right * 1.5f;
            Physics2D.SyncTransforms(); simulation.Step(.02f, new CombatInput(0, CombatButtons.Basic)); host.Advance(); Advance(.25f);
            Assert.That(1000 - enemy.health, Is.EqualTo(14).Within(.001)); Advance(.5f);
            Give("special.range"); host.Presenter.SelectBag(0); Assert.That(host.Presenter.EquipSelected(), Is.True);
            enemy.health = 1000; enemy.Position = arena.Player.Position + Vector2.right * 6.5f; enemy.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); simulation.Step(.02f, new CombatInput(0, CombatButtons.Special)); host.Advance(); Advance(.8f);
            Assert.That(1000 - enemy.health, Is.EqualTo(40).Within(.001), "A range ink must use the real catalog projectile/skill, not just change its displayed name.");
            yield return null;
        }
        [UnityTest] public IEnumerator ActualPointerHitsTheBagAndEquipButtons()
        {
            var item = Give("stat.basic");
            var view = new GameObject("Pixel GUI pointer probe").AddComponent<TemporaryInventoryView>(); view.transform.SetParent(arena.transform); view.Bind(host);
            Assert.That(view.UsesPixelTheme, Is.True); host.Presenter.Open(); view.Refresh(); Canvas.ForceUpdateCanvases();
            yield return null; Canvas.ForceUpdateCanvases();
            Click(view.bagButtons[0]); Assert.That(host.Presenter.Selected, Is.SameAs(item));
            view.Refresh(); yield return null; Canvas.ForceUpdateCanvases(); Click(view.equipButton);
            Assert.That(host.Inventory.Used, Is.Zero); Assert.That(host.Inventory.EquippedAt(0, 1), Is.SameAs(item));
            Assert.That(arena.Player.Weapon.BasicBonus, Is.EqualTo(.2f)); yield return null;
        }
        static void Click(Button expected)
        {
            var rect = (RectTransform)expected.transform;
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(target, Is.SameAs(expected.gameObject), "A visible control must not be covered by another UI element.");
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        }
        [UnityTest] public IEnumerator DuplicateWorldPickupsUseTwoSlotsAndTheRealIcon()
        {
            var a = host.SpawnDrop("stat.basic", arena.Player.Position); var b = host.SpawnDrop("stat.basic", arena.Player.Position + Vector2.right * .05f);
            Assert.That(a.Visual.sprite == host.Theme.Item(a.Item.Definition), Is.True, "The pickup must use the same native sprite asset as its theme."); Assert.That(a.Visual.color, Is.EqualTo(Color.white));
            Advance(.75f); Assert.That(host.Inventory.Used, Is.EqualTo(2));
            Assert.That(host.Inventory.BagAt(0).InstanceId, Is.Not.EqualTo(host.Inventory.BagAt(1).InstanceId));
            Assert.That(host.Drops.Count, Is.Zero);
            var view = new GameObject("Live HP probe").AddComponent<TemporaryInventoryView>(); view.transform.SetParent(arena.transform); view.Bind(host);
            view.Refresh(); yield return null; Canvas.ForceUpdateCanvases();
            var hp = view.GetComponentsInChildren<Text>().First(text => text.text == "100 / 100");
            hp.cachedTextGenerator.Populate(hp.text, hp.GetGenerationSettings(hp.rectTransform.rect.size));
            Assert.That(hp.cachedTextGenerator.characterCountVisible, Is.GreaterThan(0), "Bitmap line height must not clip the entire HP label.");
            arena.Player.health = 73; view.Refresh(); Assert.That(hp.text, Is.EqualTo("73 / 100"));
            Assert.That(view.transform.Find("Pixel gameplay HUD/HP fill").GetComponent<RectTransform>().sizeDelta.x, Is.EqualTo(224 * .73f).Within(.01f));
        }
    }
}
