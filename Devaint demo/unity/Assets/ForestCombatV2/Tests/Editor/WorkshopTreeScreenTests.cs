using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The artisan workshop's branch tabs and node tree (docs/development/presentation/ui/temporary-gui.md, 장인의 공방),
    // opened straight at the shared counter so the screen is checked apart from the departure and run flow.
    public sealed class WorkshopTreeScreenTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethods(Private).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(o, args);
        static readonly string[] Seen = { "opening", "first-return", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "artisan-visit-3",
            "artisan-visit-4", "artisan-visit-5", "village-ink-reward-1", "village-ink-reward-2", "village-ink-reward-3",
            "village-ink-reward-4", "village-ink-reward-5", "village-executioner-note" };

        [UnityTest] public IEnumerator BranchTabsDrawNodesThatOpenTheirLinksWhenComplete()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData { profileId = Guid.NewGuid().ToString(), runsStarted = 1, pigment = 10000, seenStories = Seen }));
                StartingVillageWorld v = null;
                for (int i = 0; i < 600 && !(v && v.Ready); i++) { yield return null; v = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(v && v.Ready, Is.True);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                if (Field<bool>(gui, "openingActive")) Call(gui, "SkipOpening");
                yield return null; yield return null;
                var railFixture=new GameObject("Scroll rebuild regression",typeof(RectTransform));
                railFixture.transform.SetParent(gui.transform,false);
                var shortContent=(RectTransform)Call(gui,"JournalScroll","Short description",railFixture.transform,0f,0f,200f,80f);
                Assert.That(shortContent.parent.GetComponent<ScrollRect>().verticalScrollbar.gameObject.activeSelf,Is.False,"newly rebuilt short lists start without a one-frame scrollbar flash");
                Canvas.ForceUpdateCanvases();yield return null;
                Assert.That(shortContent.parent.GetComponent<ScrollRect>().verticalScrollbar.gameObject.activeSelf,Is.False);
                shortContent.sizeDelta=new Vector2(200,180);Canvas.ForceUpdateCanvases();yield return null;yield return null;
                Assert.That(shortContent.parent.GetComponent<ScrollRect>().verticalScrollbar.gameObject.activeSelf,Is.True,"overflow still shows a working scrollbar");
                Object.Destroy(railFixture);
                // Settings follow the same category/content focus contract as the workshop.
                Call(gui,"HubPause");yield return null;
                Call(gui,"OpenApprovedSettings");yield return null;yield return null;
                var settingsTabs=Field<Button[]>(gui,"settingsTabs");
                var settingsBody=Field<RectTransform>(gui,"settingsBody");
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(settingsTabs[0].gameObject));
                Assert.That(settingsBody.GetComponent<CanvasGroup>().alpha,Is.EqualTo(.3f));
                Assert.That(((RectTransform)settingsTabs[0].transform).anchoredPosition,Is.EqualTo(new Vector2(80,-120)),"left sidebar keeps its original position");
                Assert.That(((RectTransform)settingsTabs[1].transform).anchoredPosition,Is.EqualTo(new Vector2(80,-167)));
                Assert.That(settingsBody.anchoredPosition,Is.EqualTo(new Vector2(275,-120)),"body keeps its original layout");
                ExecuteEvents.Execute<IMoveHandler>(settingsTabs[0].gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down},ExecuteEvents.moveHandler);yield return null;yield return null;
                Assert.That(Field<int>(gui,"settingsTab"),Is.EqualTo(1),"arrow focus activates sound without F");
                ExecuteEvents.Execute<IMoveHandler>(settingsTabs[1].gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Right},ExecuteEvents.moveHandler);
                yield return null;yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(settingsBody),Is.True);
                Assert.That(settingsBody.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
                Text RowTitle(string caption)=>settingsBody.GetComponentsInChildren<Text>().Single(t=>t.text==caption);
                var point=new Color32(217,203,132,255);
                Assert.That(RowTitle("전체 음량").color,Is.EqualTo((Color)point));
                var master=EventSystem.current.currentSelectedGameObject.GetComponent<Slider>();
                Assert.That(master.navigation.selectOnLeft,Is.Null,"left/right keep adjusting the native slider");
                ExecuteEvents.Execute<IMoveHandler>(master.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down},ExecuteEvents.moveHandler);
                yield return null;yield return null;
                Assert.That(RowTitle("배경 음악").color,Is.EqualTo((Color)point));
                Assert.That(RowTitle("전체 음량").color,Is.Not.EqualTo((Color)point));
                StartingVillageCaptureTests.Capture(gui,v,"settings-original-layout-selected-row");
                ExecuteEvents.Execute<IMoveHandler>(EventSystem.current.currentSelectedGameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Up},ExecuteEvents.moveHandler);
                yield return null;yield return null;
                ExecuteEvents.Execute<IMoveHandler>(EventSystem.current.currentSelectedGameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Up},ExecuteEvents.moveHandler);
                yield return null;yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(settingsTabs[1].gameObject));
                Assert.That(settingsBody.GetComponent<CanvasGroup>().alpha,Is.EqualTo(.3f));
                Call(gui,"CloseApprovedSettings");Call(gui,"CloseHubPanel");yield return null;
                var hub = v.Find(StartingVillageWorld.ServiceHub);
                v.World.arena.Player.Position = hub.Position; Physics2D.SyncTransforms(); v.World.RefreshCamera(0, true);
                Call(gui, "OpenStation", hub, true); yield return null; yield return null;

                var book = Field<GameObject>(gui, "workbench");
                var items = Field<RectTransform>(gui, "benchItems");
                var profile = Chapter1ProfileHost.Service;
                Assert.That(book.activeInHierarchy, Is.True);
                Assert.That(Field<Button[]>(gui, "benchTabs").Select(t => t.GetComponentInChildren<Text>().text), Is.EqualTo(new[] { "전투", "기동", "경제", "선택", "잉크 연구" }));
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("combat"), "the book opens on the combat branch");
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("attack_bonus_1"));
                var branchTabs = Field<Button[]>(gui, "benchTabs");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(branchTabs[0].gameObject));
                Assert.That(items.parent.GetComponent<CanvasGroup>(), Is.Not.Null, "opening creates the initially missing fade component before assigning alpha");
                Assert.That(items.parent.GetComponents<CanvasGroup>().Length, Is.EqualTo(1), "refresh reuses the same fade component");
                Assert.That(items.parent.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.3f));
                // Arrow focus itself switches the page; F is not needed to activate the tab.
                EventSystem.current.SetSelectedGameObject(branchTabs[4].gameObject);
                yield return null; yield return null;
                Assert.That(Field<string>(gui,"benchBranch"), Is.EqualTo("ink_research"));
                Assert.That(branchTabs.Count(t=>{var visual=t.GetComponents<MonoBehaviour>().Single(c=>c.GetType().Name=="ApprovedActionVisual");return (bool)visual.GetType().GetField("PersistentSelection").GetValue(visual);}), Is.EqualTo(1));
                Assert.That(items.Find("Node ink_bundle_status/Focus").GetComponent<Image>().enabled, Is.False);
                ExecuteEvents.Execute<IMoveHandler>(branchTabs[4].gameObject, new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down}, ExecuteEvents.moveHandler);
                yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.parent, Is.EqualTo(items));
                Assert.That(items.parent.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                ExecuteEvents.Execute<IMoveHandler>(EventSystem.current.currentSelectedGameObject, new AxisEventData(EventSystem.current){moveDir=MoveDirection.Up}, ExecuteEvents.moveHandler);
                yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(branchTabs[4].gameObject));
                Assert.That(items.parent.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.3f));
                branchTabs[0].onClick.Invoke(); yield return null; yield return null;
                void Tab(string branch) => book.transform.Find("Branch " + branch).GetComponent<Button>().onClick.Invoke();
                Button Node(string id) => items.Find("Node " + id).GetComponent<Button>();
                Image Lock(string id) => items.Find("Node " + id).Find("Lock").GetComponent<Image>();
                string Count(string id) => items.Find("Node " + id).Find("Count").GetComponent<Text>().text;
                string Caption(string id) => items.Find("Node " + id).Find("Caption").GetComponent<Text>().text;
                Color Line(string from, string to) => items.Find("Tree lines/Line " + from + ">" + to).GetComponentInChildren<Image>().color;
                Assert.That(items.Find("Node shared_rerolls_1"), Is.Null, "one branch tab at a time");
                // Tiers 1-4 and 5-8 of one effect are the nodes I and II; a tier is no node of its own.
                Assert.That(Caption("attack_bonus_1"), Is.EqualTo("공격력 I"));
                Assert.That(Caption("attack_bonus_5"), Is.EqualTo("공격력 II"));
                Assert.That(items.Find("Node attack_bonus_2"), Is.Null);
                Assert.That(Count("attack_bonus_1"), Is.EqualTo("0/4"));
                Assert.That(Count("revive_1"), Is.Empty, "a single unlock shows no count");
                Assert.That(Field<Text>(gui, "benchName").text, Is.EqualTo("공격력 I"));

                // Before the first run is settled every node is locked and the right page says why.
                foreach (var (branch, id) in new[] { ("ink_research", "ink_bundle_status"), ("economy", "starting_gold_50"), ("mobility", "extra_dash_1") })
                {
                    Tab(branch);
                    Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo(branch));
                    Assert.That(profile.StatusOf(id).CanBuy, Is.False);
                    Node(id).onClick.Invoke();
                    Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo(id));
                    Assert.That(Lock(id).enabled, Is.True);
                    StringAssert.StartsWith("첫 런을 마치고 돌아오면 열립니다", Field<Text>(gui, "benchStatus").text, id + " waits on the first run");
                }
                Assert.That(items.Find("Node attack_bonus_1 (선행)"), Is.Not.Null, "the dash shows its combat prerequisite node in the mobility tab");
                Assert.That(items.Find("Row 2").GetComponent<Text>().text, Is.EqualTo("전투 선행"));
                Assert.That(Field<Text>(gui, "benchStatus").text, Does.Contain("공격력 I 완성 필요"), "a link names the whole node it waits on");
                StartingVillageCaptureTests.Capture(gui, v, "workshop-tree-locked");
                foreach (string branch in new[] { "combat", "mobility", "economy", "choice", "ink_research" })
                {
                    Tab(branch);
                    var page = ((RectTransform)items.parent).rect;
                    int nodes = 0;
                    foreach (Transform node in items)
                    {
                        if (!node.name.StartsWith("Node ")) continue; nodes++;
                        var at = (RectTransform)node; var caption = (RectTransform)node.Find("Caption");
                        var ringCorners=new Vector3[4];
                        ((RectTransform)node.Find("Focus")).GetWorldCorners(ringCorners);
                        float ringTop=items.parent.InverseTransformPoint(ringCorners[1]).y;
                        Assert.That(ringTop,Is.LessThanOrEqualTo(page.yMax+.01f),node.name+" selection ring stays below the viewport's top clipping edge");
                        var captionCorners=new Vector3[4];caption.GetWorldCorners(captionCorners);
                        float nameGap=items.parent.InverseTransformPoint(ringCorners[0]).y-items.parent.InverseTransformPoint(captionCorners[1]).y;
                        Assert.That(nameGap,Is.GreaterThanOrEqualTo(5.9f),node.name+" name leaves space below the outer selection ring");
                        Assert.That(at.anchoredPosition.x, Is.GreaterThanOrEqualTo(0));
                        Assert.That(at.anchoredPosition.x + at.rect.width, Is.LessThanOrEqualTo(page.width + .1f), node.name + " fits the page width");
                        Assert.That(-at.anchoredPosition.y - caption.anchoredPosition.y + caption.rect.height, Is.LessThanOrEqualTo(page.height + .1f), node.name + " and its name fit the page without scrolling");
                    }
                    Assert.That(nodes, Is.EqualTo(WorkshopTreeLayout.For(profile.Catalog, branch).Nodes.Count));
                    StartingVillageCaptureTests.Capture(gui, v, "workshop-tree-" + branch);
                }

                // After the first settled run the roots are on sale. A node is bought one tier at a time and opens its
                // links only once complete.
                Assert.That(profile.SettleRun(profile.PlanUnarmedRun().RunId, RunOutcome.Died, false), Is.True);
                Tab("economy");
                Node("starting_gold_50").onClick.Invoke(); yield return null;
                var buy = Field<Button>(gui, "benchBuy");
                Assert.That(buy.interactable, Is.True, "the first settlement opens the economy root");
                long before = profile.Snapshot.Pigment;
                Assert.That(buy.transform.Find("Price/Keycap").gameObject.activeSelf, Is.True, "focused purchasable node advertises F with its price");
                ExecuteEvents.Execute<ISubmitHandler>(Node("starting_gold_50").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Node("starting_gold_50").gameObject), "F purchases once and keeps node focus after rebuilding");
                Assert.That(profile.Snapshot.Has("starting_gold_50"), Is.True);
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(before - profile.Catalog.Find("starting_gold_50").cost));
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("starting_gold_100"), "a purchase hands the selection to the node's next tier");
                Assert.That(Count("starting_gold_50"), Is.EqualTo("1/4"));
                Assert.That(Field<Text>(gui, "benchName").text, Is.EqualTo("출발 지원금 I"));
                Assert.That(items.Find("Node starting_gold_50").Find("Focus").GetComponent<Image>().enabled, Is.True, "the node keeps the ring");
                Assert.That(Lock("kill_gold_1").enabled, Is.True, "a partly bought node opens nothing yet");
                var dimLine = Line("starting_gold_50", "kill_gold_1");
                Node("kill_gold_1").onClick.Invoke(); yield return null;
                Assert.That(Field<Text>(gui, "benchStatus").text, Does.Contain("출발 지원금 I 완성 필요"));
                Node("starting_gold_50").onClick.Invoke(); yield return null;
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("starting_gold_100"), "a node click selects its next tier");
                for (int i = 0; i < 3; i++) { buy.onClick.Invoke(); yield return null; yield return null; }
                Assert.That(new[] { "starting_gold_100", "starting_gold_150", "starting_gold_200" }.All(profile.Snapshot.Has), Is.True);
                Assert.That(Count("starting_gold_50"), Is.EqualTo("4/4"));
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("starting_gold_200"), "a complete node keeps its last tier");
                Assert.That(buy.interactable, Is.False);
                Assert.That(Lock("kill_gold_1").enabled, Is.False, "the complete node opens kill gold");
                Assert.That(Lock("shop_stock_1").enabled, Is.False, "and the shop stock");
                Assert.That(Line("starting_gold_50", "kill_gold_1").a, Is.GreaterThan(dimLine.a), "a line brightens once its node is complete");
                StartingVillageCaptureTests.Capture(gui, v, "workshop-tree-economy-bought");

                // Pad/keyboard moves follow the tree: down to the nearest node of the next row, up to the tab, right from
                // a row's end to the buy button while the selection can be bought.
                var first = Node("starting_gold_50");
                Assert.That(first.navigation.selectOnDown, Is.EqualTo(Node("kill_gold_1")));
                Assert.That(first.navigation.selectOnUp, Is.EqualTo(book.transform.Find("Branch economy").GetComponent<Button>()), "up from the top row reaches the tab");
                EventSystem.current.SetSelectedGameObject(Node("kill_gold_1").gameObject);
                yield return null;
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("kill_gold_1"), "focus is the selection");
                Assert.That(first.navigation.selectOnRight,Is.EqualTo(Node("kill_gold_1")),"right follows the diagonal economic branch");
                Assert.That(Node("kill_gold_1").navigation.selectOnRight, Is.EqualTo(Node("weapon_break_gold_1")));
                Call(gui, "StepBenchBranch", 1); yield return null;
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("choice"), "E moves to the next branch");
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("shared_rerolls_1"), "the new tab selects its next purchase");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(book.transform.Find("Branch choice").gameObject), "branch shortcut stays on its tab until Down");
                Call(gui, "StepBenchBranch", -2); yield return null;
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("mobility"), "Q moves back and wraps through the tabs");
                var dash=Node("extra_dash_1");
                var foreign=items.Find("Node attack_bonus_1 (선행)").GetComponent<Button>();
                Assert.That(dash.navigation.selectOnDown,Is.EqualTo(foreign),"a drawn prerequisite is keyboard reachable");
                EventSystem.current.SetSelectedGameObject(dash.gameObject);yield return null;
                ExecuteEvents.Execute<IMoveHandler>(dash.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down},ExecuteEvents.moveHandler);
                yield return null;yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(foreign.gameObject));
                ExecuteEvents.Execute<ISubmitHandler>(foreign.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return null;
                Assert.That(Field<string>(gui, "benchBranch"), Is.EqualTo("combat"), "a foreign prerequisite opens its own tab");
                Assert.That(Field<string>(gui, "hubSelected"), Is.EqualTo("attack_bonus_1"));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Node("attack_bonus_1").gameObject));
                StartingVillageCaptureTests.Capture(gui, v, "workshop-tree-combat-foreign");

                gui.GetType().GetMethod("Back").Invoke(gui, null); yield return null;
                Assert.That(book.activeInHierarchy, Is.False);
                Assert.That(v.World.arena.Controls.Current.SimulationPaused, Is.False, "closing returns the lobby's pause");
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }
    }
}
