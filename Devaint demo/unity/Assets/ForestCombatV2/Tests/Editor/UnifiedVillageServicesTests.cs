using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Narrative.Opening;
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
    public sealed class UnifiedVillageServicesTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
        static readonly string[] Seen = { "opening", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "artisan-visit-3",
            "artisan-visit-4", "artisan-visit-5", "village-ink-reward-1", "village-ink-reward-2", "village-ink-reward-3",
            "village-ink-reward-4", "village-ink-reward-5", "village-executioner-note" };

        [Test] public void SharedFacilityScriptsKeepLogicalUnlocksAndUseOneDestination()
        {
            foreach (string name in new[] { "HubOpening", "HubVisits" })
            {
                var script = JsonUtility.FromJson<OpeningScript>(Resources.Load<TextAsset>(name).text);
                Assert.That(OpeningScriptRules.Validate(script), Is.Empty, name);
                foreach (var a in script.beats.SelectMany(b => b.actions).Where(a => a.type == "pan"))
                    Assert.That(a.id, Is.EqualTo(StartingVillageWorld.ServiceHub), "facility travel shares one destination");
                Assert.That(script.beats.SelectMany(b => b.actions).Any(a => a.type == "openStation" && a.id == "departure_table"), Is.False);
            }
            var visits = JsonUtility.FromJson<OpeningScript>(Resources.Load<TextAsset>("HubVisits").text);
            CollectionAssert.AreEquivalent(new[] { "palette_bench", "reclaim_bench" }, visits.beats.SelectMany(b => b.actions)
                .Where(a => a.type == "unlockStation").Select(a => a.id));
            Assert.That(visits.beats.Any(b => b.id == "visit-2-door"), Is.False);
        }

        [UnityTest] public IEnumerator FourPlacesShareOneBookAndPreservePurchaseGates()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData { profileId = Guid.NewGuid().ToString(),
                    pigment = 10000, seenStories = Seen }));
                StartingVillageWorld v = null;
                for (int i = 0; i < 600 && !(v && v.Ready); i++) { yield return null; v = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(v && v.Ready, Is.True);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                if (Field<bool>(gui, "openingActive")) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
                FieldReset(gui, "storyChecked", false);
                yield return null; yield return null;
                CollectionAssert.AreEquivalent(new[] { "record_desk", "return_well", "artisan_hub", "departure_sign" }, v.Stations.Select(s => s.Id));
                Assert.That(v.GroundWidth, Is.EqualTo(44));
                Assert.That(v.Find(StartingVillageWorld.DepartureSign).Position.x, Is.EqualTo(36));
                Assert.That(v.Sky.worldWidth, Is.EqualTo(84), "the optional sky course keeps its original width");
                var walker = v.World.arena.Player;
                var beforeWalk = walker.Position;
                walker.Position = new Vector2(43, v.WalkingLine); walker.velocity = Vector2.zero; Physics2D.SyncTransforms();
                using (var driver = v.World.arena.AcquireManualSimulation())
                    for (int tick = 0; tick < 240; tick++) driver.Step(1f / 120, new Deviant.Combat.Contracts.CombatInput(1));
                Assert.That(walker.Position.x, Is.LessThan(44), "the compact right boundary physically stops walking");
                v.World.RefreshCamera(0, true);
                Assert.That(v.World.CameraX, Is.LessThanOrEqualTo(44 * 32 - 960));
                // One real frame: the village's LateUpdate moves the view-sized sky picture to the snapped camera. Captured
                // straight after the snap, the sky stayed where the camera had been and the clear colour showed as a dark
                // brown panel over the right of the sky (QA 2026-11-13).
                yield return null;
                var view = v.World.View; var skyArt = v.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Hub sky");
                float halfW = view.orthographicSize * view.aspect, halfH = view.orthographicSize;
                Assert.That(skyArt.bounds.min.x, Is.LessThanOrEqualTo(view.transform.position.x - halfW + .01f), "the sky covers the view's left edge");
                Assert.That(skyArt.bounds.max.x, Is.GreaterThanOrEqualTo(view.transform.position.x + halfW - .01f), "the sky covers the view's right edge at the departure end");
                Assert.That(skyArt.bounds.max.y, Is.GreaterThanOrEqualTo(view.transform.position.y + halfH - .01f), "the sky covers the view's top");
                StartingVillageCaptureTests.Capture(gui, v, "compact-village-departure");
                walker.Position = beforeWalk; walker.velocity = Vector2.zero; Physics2D.SyncTransforms();
                var hub = v.Find(StartingVillageWorld.ServiceHub);
                foreach (string legacy in new[] { "palette_bench", "supply_counter", "reclaim_bench" })
                    Assert.That(v.Find(legacy).Position, Is.EqualTo(hub.Position));
                Assert.That(v.StationOpen(hub), Is.True, "locked services do not hide the shared counter");
                v.World.arena.Player.Position = hub.Position; Physics2D.SyncTransforms();
                Assert.That(v.TryInteract(), Is.True);
                yield return null;
                var book = Field<GameObject>(gui, "workbench");
                var items = Field<RectTransform>(gui, "benchItems");
                Assert.That(book.activeInHierarchy, Is.True);
                Assert.That(Field<Text>(gui, "benchTitle").text, Is.EqualTo("장인의 공방"));
                // Every service is a node of the same book, one branch tab at a time.
                void Tab(string branch) => book.transform.Find("Branch " + branch).GetComponent<Button>().onClick.Invoke();
                Button Node(string id) => items.Find("Node " + id).GetComponent<Button>();
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("combat"), "the book opens on the combat branch");
                foreach (var (branch, id) in new[] { ("ink_research", "ink_bundle_status"), ("economy", "starting_gold_50"), ("mobility", "extra_dash_1") })
                {
                    Tab(branch);
                    Assert.That(items.Find("Node " + id), Is.Not.Null, id + " belongs to the " + branch + " tab");
                    Assert.That(Chapter1ProfileHost.Service.StatusOf(id).CanBuy, Is.False);
                    Assert.That(Chapter1ProfileHost.Service.TryPurchase(id, out _), Is.False);
                    Node(id).onClick.Invoke();
                    Assert.That(items.Find("Node " + id).Find("Lock").GetComponent<Image>().enabled, Is.True);
                    StringAssert.StartsWith("첫 런을 마치고 돌아오면 열립니다", Field<Text>(gui, "benchStatus").text, id + " waits on the first run, not the village ink piece or a chapter clear");
                }
                Assert.That(items.Find("Node attack_bonus_1 (선행)"), Is.Not.Null, "the dash shows its combat prerequisite node in the mobility tab");
                Assert.That(Field<Text>(gui, "benchStatus").text, Does.Contain("공격력 I 완성 필요"));
                StartingVillageCaptureTests.Capture(gui, v, "workshop-unlock-requirements");
                foreach (string branch in new[] { "combat", "mobility", "economy", "choice", "ink_research" })
                {
                    Tab(branch);
                    var page = ((RectTransform)items.parent).rect;
                    foreach (Transform node in items)
                    {
                        if (!node.name.StartsWith("Node ")) continue;
                        var at = (RectTransform)node;
                        Assert.That(at.anchoredPosition.x, Is.GreaterThanOrEqualTo(0)); Assert.That(at.anchoredPosition.x + at.rect.width, Is.LessThanOrEqualTo(page.width + .1f), node.name + " fits the page width");
                        Assert.That(-at.anchoredPosition.y + at.rect.height, Is.LessThanOrEqualTo(page.height + .1f), node.name + " fits the page without scrolling");
                    }
                }
                var profile = Chapter1ProfileHost.Service;
                Assert.That(profile.SettleRun(profile.PlanUnarmedRun().RunId, RunOutcome.Died, false), Is.True);
                Tab("economy");
                Node("starting_gold_50").onClick.Invoke();
                yield return null;
                var buy = Field<Button>(gui, "benchBuy");
                Assert.That(buy.interactable, Is.True, "first settlement unlocks supply in the same book");
                long before = profile.Snapshot.Pigment;
                buy.onClick.Invoke(); yield return null;
                Assert.That(profile.Snapshot.Has("starting_gold_50"), Is.True);
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(before - profile.Catalog.Find("starting_gold_50").cost));
                Assert.That(profile.StationOpen("palette_bench"), Is.False);
                Assert.That(new[] { UnlockState.Available, UnlockState.NotEnoughPigment }, Does.Contain(profile.StatusOf("ink_bundle_status").State),
                    "the first ink bundle is on sale after the first run although the village station still waits for its piece");
                Assert.That(profile.StationOpen("reclaim_bench"), Is.False);
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("starting_gold_100"));
                Assert.That(items.Find("Node starting_gold_50").Find("Lock").GetComponent<Image>().enabled, Is.False, "purchase rebuild paints the node immediately");
                Assert.That(items.Find("Node starting_gold_50").Find("Count").GetComponent<Text>().text, Is.EqualTo("1/4"), "출발 지원금 I counts its bought tiers");
                StartingVillageCaptureTests.Capture(gui, v, "unified-workshop");
                // Pad/keyboard moves follow the tree: down to the nearest node of the next row, up to the tab.
                var first = Node("starting_gold_50");
                Assert.That(first.navigation.selectOnDown, Is.EqualTo(Node("kill_gold_1")));
                Assert.That(first.navigation.selectOnUp, Is.EqualTo(book.transform.Find("Branch economy").GetComponent<Button>()), "up from the top row reaches the tab");
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Node("kill_gold_1").gameObject);
                yield return null;
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("kill_gold_1"), "focus is the selection");
                gui.GetType().GetMethod("StepBenchBranch", Private).Invoke(gui, new object[] { 1 }); yield return null;
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("choice"), "E moves to the next branch");
                Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name, Does.StartWith("Node shared_rerolls_"), "the new tab focuses its next purchase");
                gui.GetType().GetMethod("StepBenchBranch", Private).Invoke(gui, new object[] { -2 }); yield return null;
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("mobility"));
                items.Find("Node attack_bonus_1 (선행)").GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("combat"), "a foreign prerequisite opens its own tab");
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("attack_bonus_1"), "the foreign node selects its node's next tier");
                StartingVillageCaptureTests.Capture(gui, v, "unified-workshop-combat");
                for (int chapter = 1; chapter <= 2; chapter++)
                    Assert.That(profile.SettleRun(profile.PlanUnarmedRun().RunId, RunOutcome.ChapterCleared, false, chapter), Is.True);
                yield return null;
                Tab("ink_research");
                var inkRow = items.Find("Node ink_bundle_status");
                Assert.That(profile.StatusOf("ink_bundle_status").State, Is.EqualTo(UnlockState.Available));
                inkRow.GetComponent<Button>().onClick.Invoke(); yield return null;
                StringAssert.Contains("3종", Field<Text>(gui, "benchEffect").text);
                Field<Button>(gui,"benchBuy").onClick.Invoke(); yield return null;
                Assert.That(profile.PlanUnarmedRun().LockedInkParts.Any(id=>id.Contains("_status_condition_")),Is.False,"the bundle opens its status-condition inks");
                StartingVillageCaptureTests.Capture(gui, v, "workshop-ink-bundle");
                gui.GetType().GetMethod("Back").Invoke(gui, null); yield return null;
                Assert.That(v.World.arena.Controls.Current.SimulationPaused, Is.False);
                Assert.That(v.StationOpen(v.Find(StartingVillageWorld.DepartureSign)), Is.True);
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }

        static void FieldReset(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);

        [UnityTest] public IEnumerator UnarmedPlanBypassesStartingOffersAndTheirExitLock()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var offers = world.GetComponent<Chapter1StartingBlueprints>();
            Assert.That(offers.ApplyUnarmedDeparture(), Is.True);
            yield return null;
            Assert.That(offers.Complete, Is.True);
            Assert.That(offers.ExitLocked, Is.False);
            Assert.That(offers.RemainingCount, Is.Zero);
            Assert.That(offers.TryChoose(0), Is.False);
            var arena = world.arena; var player = arena.Player;
            Assert.That(player.IsWeaponSlotReady(0), Is.True, "A is the unarmed basic attack");
            Assert.That(player.IsWeaponSlotReady(1), Is.False);
            Assert.That(player.Form.id, Is.EqualTo("unarmed"));
            Assert.That(player.AppearanceOverride, Is.Null, "the unarmed form owns its body, so later weapon equips can change it");
            var guide = world.GetComponent<Chapter1FirstRunInkGuide>();
            if (guide) { guide.FinishNotebook(); guide.enabled = false; }
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
            if (gui) gui.GetType().GetMethod("DismissDepartureCard")?.Invoke(gui, null);
            foreach (var e in arena.enemies) if (e) { e.aggressive = false; e.Position += Vector2.right * 1000; }
            using (var driver = arena.AcquireManualSimulation())
            {
                var at = player.Position;
                foreach (int profile in new[] { 0, 2 })
                {
                    player.Position = at; player.velocity = Vector2.zero; player.SyncFacing(1);
                    var enemy = arena.SpawnEnemy(profile, at + Vector2.right * 1.4f); enemy.aggressive = false;
                    float hp = enemy.health; Physics2D.SyncTransforms();
                    driver.Step(.01f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Weapon1Basic));
                    Assert.That(player.currentCast, Is.Not.Null);
                    Assert.That(player.currentCast.Definition.Id, Is.EqualTo("unarmed_basic_0"));
                    Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.Null.And.Not.Empty);
                    // A separate target starts a fresh combo after its .30s grace.
                    for (int tick = 0; tick < 80; tick++) driver.Step(.01f);
                    Assert.That(enemy.health, Is.LessThan(hp), profile == 2 ? "the punch hits bosses" : "the punch hits ordinary enemies");
                    Assert.That(enemy.ReceivedHitCount, Is.EqualTo(1), "one punch cannot hit repeatedly in its active window");
                    enemy.Position += Vector2.right * 1000;
                }
                driver.Step(.01f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Weapon1Special | Deviant.Combat.Contracts.CombatButtons.Weapon2Basic));
                Assert.That(player.currentCast, Is.Null, "unarmed has no special or hidden second weapon");
            }
            yield return null;
            if (gui)
                typeof(Chapter1ControlsSignTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { gui, arena.ViewCamera, "chapter1-unarmed-combat" });
            yield return new ExitPlayMode();
        }
    }
}
