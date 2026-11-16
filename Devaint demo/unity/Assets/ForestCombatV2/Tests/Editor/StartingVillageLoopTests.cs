using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // Title -> village -> departure table -> chapter 1 -> clear -> village -> unlock -> next departure, driven through
    // the real screens' buttons. The profile uses a memory store so the player's saved progress is never touched.
    public sealed class StartingVillageLoopTests
    {
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        static void Click(Button button) { Assert.That(button.interactable, Is.True, button.name + " is enabled"); button.onClick.Invoke(); }
        static StartingVillageWorld Village() { var v = Object.FindAnyObjectByType<StartingVillageWorld>(); return v && v.Ready ? v : null; }
        static ForestCombatWorld Chapter(RunSession run)
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
            return world && world.Ready && binding && binding.Run == run ? world : null;
        }

        [UnityTest] public IEnumerator TheVillageStartsRunsAndEveryRunReturnsToIt()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var profile = Chapter1ProfileHost.Service;
            for (int frame = 0; frame < 120 && !Gui(); frame++) yield return null;

            // Title "시작하기" goes to the village, not straight into a run.
            Click(Field<Button>(Gui(), "menuPrimary"));
            StartingVillageWorld village = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(village = Village());) yield return null;
            Assert.That(village, Is.Not.Null, "the title opens the starting village");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Chapter1RunHost.VillageScene));
            yield return null; yield return null;
            var gui = Gui();

            // First visit: the playable opening comes first and no station is offered until it ends; skipping it starts
            // the lobby at the well with the opening recorded as seen.
            Assert.That(Field<bool>(gui, "openingActive"), Is.True, "the first visit walks into the village");
            Assert.That(village.TryInteract(), Is.False, "no station before the village is erased");
            gui.GetType().GetMethod("SkipOpening", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gui, null);
            Assert.That(Field<bool>(gui, "openingActive"), Is.False);
            Assert.That(Field<bool>(gui, "storyActive"), Is.False, "the still scenes are not shown");
            Assert.That(village.World.arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(profile.Snapshot.HasSeen("opening"), Is.True);
            Assert.That(Mathf.Abs(village.World.arena.Player.Position.x - village.Find(StartingVillageWorld.ReturnWell).Position.x), Is.LessThan(.01f), "control starts at the well");
            yield return null;
            Assert.That(village.WellResting, Is.False, "after the opening the well's shortcut is back");

            // The sign departs immediately with no blueprint picker; repeated F cannot create a second run.
            village.World.arena.Player.Position = village.Find(StartingVillageWorld.DepartureSign).Position;
            Physics2D.SyncTransforms();
            Assert.That(village.TryInteract(), Is.True);
            Assert.That(village.Departing, Is.True);
            var run = Chapter1RunHost.Instance.Flow.Current;
            Assert.That(village.TryInteract(), Is.False);
            Assert.That(Chapter1RunHost.Instance.Flow.Current, Is.SameAs(run));
            Assert.That(run.Plan.Unarmed, Is.True);
            Assert.That(run.Plan.Blueprints, Is.Empty);
            ForestCombatWorld world = null;
            float departureDeadline = Time.realtimeSinceStartup + 30; while (!(world = Chapter(run)) && Time.realtimeSinceStartup < departureDeadline) yield return null;
            Assert.That(world, Is.Not.Null, "departure loads chapter 1 for the planned run");
            Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(Chapter1RunHost.ChapterScene));
            yield return null;
            Assert.That(world.GetComponent<Chapter1StartingBlueprints>().ExitLocked, Is.False);
            Assert.That(world.GetComponent<Chapter1StartingBlueprints>().RemainingCount, Is.Zero);
            Assert.That(world.arena.Player.IsWeaponSlotReady(0), Is.True, "the unarmed primary can attack");
            Assert.That(world.arena.Player.IsWeaponSlotReady(1), Is.False);
            Assert.That(world.arena.Player.Form.id, Is.EqualTo("unarmed"));

            // Elite kill pays pigment; the chapter clear records the first boss clear and returns to the village.
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            world.arena.enemies.First(e => e && !e.Dead && Chapter1EnemyRole.Of(e) == Chapter1EnemyRole.Elite).health = 0;
            for (int frame = 0; frame < 60 && profile.Snapshot.Pigment < 5; frame++) yield return null;
            run.MarkBossDefeated();
            Assert.That(world.GetComponent<Chapter1RunBinding>().TryClearChapter(), Is.True);
            yield return null; yield return null;
            Assert.That(profile.Snapshot.BossFirstClear, Is.True);
            var resultGui = Gui();
            Assert.That(Field<Button>(resultGui, "menuPrimary").GetComponentInChildren<Text>().text, Is.EqualTo("마을로"));
            StringAssert.Contains("마법안료   +10", Field<Text>(resultGui, "menuStats").text);
            // The first village palette piece: the run ends here and goes home with it (no chapter 2 yet).
            StringAssert.Contains("마을 잉크   1 / 5", Field<Text>(resultGui, "menuStats").text);
            Assert.That(Field<Button>(resultGui, "menuSecondary").gameObject.activeSelf, Is.False, "no way on to chapter 2 before the palette is full");
            Click(Field<Button>(resultGui, "menuPrimary"));
            village = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(village = Village());) yield return null;
            Assert.That(village, Is.Not.Null, "the result returns to the village");
            yield return null; yield return null;
            gui = Gui();
            Assert.That(Field<bool>(gui, "storyActive"), Is.False, "a returning visit does not force the opening again");
            // Home with the first piece, the artisan's first visit plays; skip it like a player holding Esc.
            // The first run's artisan comes first (pigment and the supply office); skip it as well.
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && (int)gui.GetType().GetProperty("VisitPlaying").GetValue(gui) != 1;)
            {
                if ((int)gui.GetType().GetProperty("VisitPlaying").GetValue(gui) == 100) gui.GetType().GetMethod("SkipOpening", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(gui, null);
                yield return null;
            }
            Assert.That((int)gui.GetType().GetProperty("VisitPlaying").GetValue(gui), Is.EqualTo(1), "the first piece brings the artisan");
            gui.GetType().GetMethod("SkipOpening", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(gui, null);
            yield return null;
            Assert.That(profile.Snapshot.HasSeen("artisan-visit-1"), Is.True);

            // Dye workshop: 4 slots costs 30; collect enough through real kill awards, then buy through the screen.
            var bonus = Guid.NewGuid();
            profile.AwardPigment(bonus, "a", "boss", false); profile.AwardPigment(bonus, "b", "boss", false); profile.AwardPigment(bonus, "c", "boss", false);
            Assert.That(profile.FlushPigment(), Is.True);
            var bench = village.Find(StartingVillageWorld.PaletteBench);
            village.World.arena.Player.Position = bench.Position; Physics2D.SyncTransforms();
            Assert.That(village.TryInteract(), Is.True);
            yield return null;
            Assert.That(Field<GameObject>(gui, "workbench").activeInHierarchy, Is.True);
            // The workshop opens on the combat branch; the reroll line lives in the choice tab.
            var items = Field<RectTransform>(gui, "benchItems");
            Assert.That(items.Find("Node shared_rerolls_1"), Is.Null, "one branch tab at a time");
            Field<GameObject>(gui, "workbench").transform.Find("Branch choice").GetComponent<Button>().onClick.Invoke();
            yield return null;
            // 공용 리롤 I holds tiers 1-4 in one node; 공용 리롤 II and the bundles wait on the whole node.
            items.Find("Node shared_rerolls_1").GetComponent<Button>().onClick.Invoke();
            Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("shared_rerolls_1"));
            yield return null;
            Image Lock(string id) => items.Find("Node " + id).Find("Lock").GetComponent<Image>();
            Text Count(string id) => items.Find("Node " + id).Find("Count").GetComponent<Text>();
            Assert.That(Lock("shared_rerolls_1").enabled, Is.False, "a buyable node shows no lock");
            Assert.That(Lock("shared_rerolls_5").enabled, Is.True, "the next node waits on the whole node before it");
            Assert.That(Count("shared_rerolls_1").text, Is.EqualTo("0/4"));
            long before = profile.Snapshot.Pigment;
            Click(Field<Button>(gui, "benchBuy"));
            Assert.That(profile.Snapshot.Has("shared_rerolls_1"), Is.True);
            Assert.That(profile.Snapshot.Pigment, Is.EqualTo(before - 60));
            yield return null;
            Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("shared_rerolls_2"), "a purchase hands the selection to the node's next tier");
            Assert.That(Field<Button>(gui, "benchBuy").interactable, Is.EqualTo(profile.StatusOf("shared_rerolls_2").CanBuy));
            yield return null; // the tree is rebuilt after a purchase; the old nodes are destroyed at the end of that frame
            Assert.That(Field<Text>(gui, "benchName").text, Is.EqualTo("공용 리롤 I"));
            Assert.That(Count("shared_rerolls_1").text, Is.EqualTo("1/4"));
            Assert.That(items.Find("Node shared_rerolls_1").Find("Focus").GetComponent<Image>().enabled, Is.True, "the selected node keeps the ring");
            gui.GetType().GetMethod("Back").Invoke(gui, null);
            yield return null;
            Assert.That(village.World.arena.Controls.Current.SimulationPaused, Is.False, "closing returns the lobby's pause");

            // The next unarmed departure snapshots the same purchased permanent upgrades.
            village.World.arena.Player.Position = village.Find(StartingVillageWorld.DepartureSign).Position;
            Physics2D.SyncTransforms(); yield return null;
            Assert.That(village.TryInteract(), Is.True);
            Assert.That(village.Departing, Is.True);
            var next = Chapter1RunHost.Instance.Flow.Current;
            Assert.That(next, Is.Not.SameAs(run)); Assert.That(next.Plan.Unarmed, Is.True);
            Assert.That(next.Plan.PaletteSlots, Is.EqualTo(3)); Assert.That(next.Perks.RerollsRemaining,Is.EqualTo(1));
            world = null;
            departureDeadline=Time.realtimeSinceStartup+30;while(!(world=Chapter(next))&&Time.realtimeSinceStartup<departureDeadline)yield return null;
            Assert.That(world,Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(Chapter1RunHost.ChapterScene));
            Assert.That(next.Plan.PaletteSlots, Is.EqualTo(3), "palette slot unlocks are retired");

            // A death also goes back to the village.
            world.arena.Player.health = 0;
            // The result follows the death motion after real seconds (Chapter1RunBinding.GameOverDelaySeconds).
            float diedAt = Time.realtimeSinceStartup;
            while (!next.Ended && Time.realtimeSinceStartup - diedAt < 8) yield return null;
            Assert.That(next.Ended, Is.True);
            yield return null;
            Click(Field<Button>(Gui(), "menuPrimary"));
            village = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(village = Village());) yield return null;
            Assert.That(village, Is.Not.Null, "a death returns to the village");
            Assert.That(profile.Snapshot.RunsDied, Is.EqualTo(1));
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }
    }
}
