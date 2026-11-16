using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // First-run ink tutorial (docs/design/story/tutorials/first-run-guide.md, guide-ink · guide-bag).
    public sealed class Chapter1FirstRunInkGuideTests
    {
        static void CaptureCombatNotebook(Canvas canvas, Camera camera)
        {
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            var target = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                canvas.GetComponent<MonoBehaviour>().SendMessage("FitToWorld", SendMessageOptions.DontRequireReceiver);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/weapon-order-tutorial");
                File.WriteAllBytes("../outputs/weapon-order-tutorial/combat-notebook.png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; camera.targetTexture = target; camera.rect = rect;
                RenderTexture.active = active; Object.DestroyImmediate(image); RenderTexture.ReleaseTemporary(rt);
            }
        }
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static GrowthData Data() => JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text);
        static ProgressionService NewProfile() => new ProgressionService(
            new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json"))), new MemoryProfileStore());

        [Test] public void GuideInkIsTheLowestGradeInAColourOfTheEquippedPalettes()
        {
            var data = Data(); var random = new System.Random(3); var palette = new[] { "blue", "green" };
            for (int i = 0; i < 200; i++)
            {
                var part = Chapter1FirstRunInkGuide.ChooseGuidePart(data, palette, null, random);
                CollectionAssert.Contains(palette, part.color, "only a colour a palette slot takes");
                Assert.That(part.grade, Is.EqualTo(data.parts.Where(p => p.color == part.color).Min(p => p.grade)), "the lowest grade for this colour");
            }
            // Only colourless wildcard slots: every colour fits one.
            var colours = new HashSet<string>();
            for (int i = 0; i < 400; i++)
            {
                var part = Chapter1FirstRunInkGuide.ChooseGuidePart(data, new string[0], null, random);
                Assert.That(part.grade, Is.EqualTo(data.parts.Where(p => p.color == part.color).Min(p => p.grade))); colours.Add(part.color);
            }
            Assert.That(colours.Count, Is.EqualTo(5));
            // Village-locked grade-1 parts stay out; the colour's lowest grade still on offer takes their place.
            var locked = new HashSet<string>(data.parts.Where(p => p.color == "blue" && p.grade == 1).Select(p => p.id));
            for (int i = 0; i < 100; i++)
            {
                var part = Chapter1FirstRunInkGuide.ChooseGuidePart(data, new[] { "blue" }, p => !locked.Contains(p.id), random);
                Assert.That(part.color, Is.EqualTo("blue")); Assert.That(part.grade, Is.EqualTo(2)); CollectionAssert.DoesNotContain(locked, part.id);
            }
        }

        [Test] public void OnlyTheProfilesFirstRunBeforeTheGuideIsRecorded()
        {
            var profile = NewProfile();
            var first = profile.PlanRun(new[] { "sword", "blunt" });
            Assert.That(profile.Snapshot.RunsStarted, Is.EqualTo(1), "the departure counts the run");
            Assert.That(Chapter1FirstRunInkGuide.Eligible(true, first.ProfileId, profile.Snapshot), Is.True);
            Assert.That(Chapter1FirstRunInkGuide.Eligible(false, first.ProfileId, profile.Snapshot), Is.False, "training and plan-less runs");
            Assert.That(Chapter1FirstRunInkGuide.Eligible(true, "another profile", profile.Snapshot), Is.False, "a run this profile did not plan");
            var second = profile.PlanRun(new[] { "sword", "blunt" });
            Assert.That(Chapter1FirstRunInkGuide.Eligible(true, second.ProfileId, profile.Snapshot), Is.False, "never from the second run on, finished or not");
            var seen = NewProfile(); var plan = seen.PlanRun(new[] { "sword", "ranged" });
            Assert.That(seen.MarkStorySeen(Chapter1FirstRunInkGuide.GuideBagStory), Is.True);
            Assert.That(Chapter1FirstRunInkGuide.Eligible(true, plan.ProfileId, seen.Snapshot), Is.False, "a recorded guide does not repeat");
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static ForestCombatWorld Bound(RunSession run)
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
            return world && world.Ready && binding && binding.Run == run ? world : null;
        }
        static MonoBehaviour GuiOrNull() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        static T Field<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetField(name, Hidden).GetValue(gui);
        static T Property<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetProperty(name).GetValue(gui);

        static Button NativePaletteCell(MonoBehaviour gui,int index)=>Field<GameObject>(gui,"bag").GetComponentsInChildren<Button>().Single(b=>b.name=="Palette slot "+index);
        static void AssertNativeInkDetail(MonoBehaviour gui,string name)=>Assert.That(Field<GameObject>(gui,"bag").GetComponentsInChildren<Text>().Any(t=>t.text==name),Is.True,"the first equipped ink can be inspected");
        [UnityTest] public IEnumerator FirstRunMonsterDropsAFittingInkThatIsPromptedIntoThePalette()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !(Object.FindAnyObjectByType<ForestCombatWorld>()?.Ready ?? false); frame++) yield return null;
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var profile = Chapter1ProfileHost.Service;
            var host = Chapter1RunHost.Ensure();
            host.StartNewRun(profile.PlanRun(new[] { "sword", "blunt" }));
            var run = host.Flow.Current; ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world = Bound(run));) yield return null;
            Assert.That(world, Is.Not.Null, "the planned first run loads chapter 1");
            var guide = world.GetComponent<Chapter1FirstRunInkGuide>();
            for (int frame = 0; frame < 120 && guide.Current == Chapter1FirstRunInkGuide.Stage.Inactive; frame++) yield return null;
            Assert.That(guide.Current, Is.EqualTo(Chapter1FirstRunInkGuide.Stage.Waiting), "the profile's first run teaches the ink");
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            var player = world.arena.Player;

            // On this run the departure palette card waits to be closed and holds the hero still without pausing the world
            // (user decision 2026-11-09); closing it brings the combat notebook.
            MonoBehaviour cardGui = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 10 && !((cardGui = GuiOrNull()) && Property<bool>(cardGui, "DepartureCardLocked"));) yield return null;
            Assert.That(cardGui, Is.Not.Null, "the play GUI binds to the run");
            Assert.That(Property<bool>(cardGui, "DepartureCardLocked"), Is.True, "the first run holds the departure card");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "the world keeps running under the card");
            Assert.That((world.arena.Controls.Current.Blocked & Deviant.Control.Contracts.InputChannels.Navigation) != 0, Is.True, "the hero cannot move under the card");
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 5;) yield return null;
            Assert.That(Property<bool>(cardGui, "DepartureCardLocked") && !guide.NotebookOpen, Is.True, "the card does not close on a timer");
            cardGui.GetType().GetMethod("DismissDepartureCard").Invoke(cardGui, null);

            // The combat notebook (attack and skill keys) then opens, pauses the world and holds the monster
            // (user decision 2026-11-09). Finishing it hands the world back.
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 15 && !guide.NotebookOpen;) yield return null;
            Assert.That(guide.OpenNotebook, Is.EqualTo(Chapter1FirstRunInkGuide.Notebook.Combat), "the first run opens the combat notebook");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True, "the notebook pauses the world");
            Assert.That(guide.Current, Is.EqualTo(Chapter1FirstRunInkGuide.Stage.Waiting), "the monster waits for the notebook");
            yield return null; Refresh(cardGui);
            var combatCopy = cardGui.GetComponentsInChildren<Text>(true).Single(t => t.text.Contains("무기 순서를 변경할 수 있습니다."));
            Assert.That(combatCopy.text.Split('\n').Length, Is.EqualTo(3));
            Assert.That(combatCopy.gameObject.activeInHierarchy, Is.True);
            Assert.That(combatCopy.preferredHeight, Is.LessThanOrEqualTo(combatCopy.rectTransform.rect.height));
            CaptureCombatNotebook(combatCopy.GetComponentInParent<Canvas>(), world.View);

            guide.FinishNotebook();
            yield return null; yield return null;
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "closing the notebook gives the world back");

            // A moment later, one weak normal monster appears ahead and comes at the player.
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 15 && guide.Current == Chapter1FirstRunInkGuide.Stage.Waiting;) yield return null;
            Assert.That(guide.Current, Is.EqualTo(Chapter1FirstRunInkGuide.Stage.Approaching), guide.EndReason);
            var monster = guide.Monster;
            Assert.That(monster, Is.Not.Null);
            Assert.That(monster.aggressive, Is.True);
            Assert.That(Chapter1EnemyRole.Of(monster), Is.EqualTo(Chapter1EnemyRole.Normal));
            Assert.That(Vector2.Distance(monster.Position, player.Position), Is.LessThanOrEqualTo(Chapter1FirstRunInkGuide.ApproachDistance + .6f));
            Assert.That(monster.GetComponent<Chapter1NoInkReward>(), Is.Not.Null, "no random ink roll on top of the guide ink");

            // Its kill throws exactly one lowest-grade ink of a palette colour, outlined on the floor.
            monster.health = 0;
            for (int frame = 0; frame < 60 && guide.Current == Chapter1FirstRunInkGuide.Stage.Approaching; frame++) yield return null;
            Assert.That(guide.Current, Is.EqualTo(Chapter1FirstRunInkGuide.Stage.Dropped));
            Assert.That(guide.GuidePart.grade, Is.EqualTo(1));
            CollectionAssert.Contains(guide.PaletteColors(), guide.GuidePart.color);
            var inks = Object.FindObjectsByType<Chapter1RewardPickup>(FindObjectsSortMode.None).Where(p => p.Kind == Chapter1RewardKind.ColoredInk && !p.Collected).ToArray();
            Assert.That(inks.Length, Is.EqualTo(1), "exactly one colour ink");
            Assert.That(guide.OutlinePieces, Is.EqualTo(8));
            yield return null;
            Assert.That(guide.OutlineVisible, Is.True, "the ink blinks with an outline");
            var pickup = guide.Pickup;
            for (int frame = 0; frame < 3000 && !pickup.Ready; frame++) yield return null;
            yield return null;
            var gui = Gui(); Refresh(gui);
            // With the notebooks there is no 「어? 이건 뭐지?」 line at all (user decision 2026-11-09).
            Assert.That(guide.ReactionShowing, Is.False, "no line while the ink lies on the floor");

            // Picked up: the palette notebook opens right away, then in the bag the fitting slot is ringed and prompted.
            Assert.That(pickup.Collected || pickup.TryCollect(), Is.True);
            Assert.That(guide.Current, Is.EqualTo(Chapter1FirstRunInkGuide.Stage.InBag));
            yield return null; Refresh(gui);
            Assert.That(guide.ReactionShowing, Is.False, "no line after the pickup either");
            Assert.That(Property<bool>(gui, "InkGuideBubbleShown"), Is.False);
            Assert.That(profile.Snapshot.HasSeen(Chapter1FirstRunInkGuide.GuideInkStory), Is.True);
            // The prompt waits for no monster within 12 tiles (the bag does not pause): clear the start's neighbours.
            foreach (var e in world.arena.enemies)
                if (e && !e.Dead && Vector2.Distance(e.Position, player.Position) < Chapter1FirstRunInkGuide.BagPromptClearance) e.health = 0;
            yield return null;
            // The palette notebook opens on the pickup; the bag prompt waits for it (user decision 2026-11-09).
            for (int frame = 0; frame < 30 && !guide.NotebookOpen; frame++) yield return null;
            Assert.That(guide.OpenNotebook, Is.EqualTo(Chapter1FirstRunInkGuide.Notebook.Palette), "the palette notebook follows the pickup");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True, "the palette notebook pauses the world");
            Assert.That(guide.BagPromptReady, Is.False, "the bag prompt waits for the notebook");
            guide.FinishNotebook();
            yield return null; yield return null;
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(guide.BagPromptReady, Is.True);
            Refresh(gui);
            // The palette notebook already told the bag key, so the top prompt is not repeated (user decision 2026-11-09).
            Assert.That(Field<Text>(gui, "hint").text, Does.Not.Contain("인벤토리를 열어보자"));
            var bag = world.arena.GetComponent<InventoryRuntimeHost>();
            bag.Presenter.Open(); Refresh(gui);
            Assert.That(Property<bool>(gui, "InkGuidePromptShown"), Is.True);
            Assert.That(guide.TargetSlot(out int weapon, out int slot), Is.True);
            Assert.That(bag.Presenter.SelectedWeapon, Is.EqualTo(weapon), "the bag turns to the fitting palette");
            var book = Field<GameObject>(gui, "bag");
            var cell = NativePaletteCell(gui,slot);
            var ring = cell.transform.Find("First-run ink ring");
            Assert.That(ring && ring.gameObject.activeSelf, Is.True, "the fitting slot is ringed");
            Assert.That(profile.Snapshot.HasSeen(Chapter1FirstRunInkGuide.GuideBagStory), Is.False, "not until it is equipped");
            Assert.AreEqual(3, book.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Palette slot ")), "the first run uses the same opened slots as later runs");
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "palette-first-profile-run", 1920, 1080);
            cell.onClick.Invoke(); Refresh(gui);

            // Equipped: the guide ends and is recorded.
            Assert.That(bag.TryEquipColor(guide.GuideInk, weapon, slot, out var error), Is.True, error);
            Assert.That(guide.Completed, Is.True);
            Assert.That(profile.Snapshot.HasSeen(Chapter1FirstRunInkGuide.GuideBagStory), Is.True);
            Refresh(gui);
            Assert.That(Property<bool>(gui, "InkGuidePromptShown"), Is.False);
            Assert.IsFalse(book.GetComponentsInChildren<Transform>().Any(t => t.name == "First-run ink ring" && t.gameObject.activeInHierarchy));
            NativePaletteCell(gui,slot).onClick.Invoke(); Refresh(gui);
            AssertNativeInkDetail(gui,guide.GuidePart.name);
        }
    }
}
