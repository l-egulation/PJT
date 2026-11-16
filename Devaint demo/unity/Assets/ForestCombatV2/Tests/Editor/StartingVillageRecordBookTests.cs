using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace ForestCombatV2.Tests
{
    public sealed class StartingVillageRecordBookTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Read(MonoBehaviour gui,string name)=>gui.GetType().GetProperty(name).GetValue(gui);
        static T Field<T>(MonoBehaviour gui,string name)=>(T)gui.GetType().GetField(name,Hidden).GetValue(gui);
        static object Call(MonoBehaviour gui,string name,params object[] args)=>gui.GetType().GetMethods(Hidden).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(gui,args);
        static IEnumerator Ready(MonoBehaviour gui)
        {
            float deadline=Time.realtimeSinceStartup+5;while((bool)Read(gui,"RecordBusy")&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)Read(gui,"RecordBusy"),Is.False,"unscaled page fade completed while world simulation was paused");
        }
        static IEnumerator Press(Keyboard keyboard,Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        [UnityTest] public IEnumerator FacilityLabelsAndWorkshopSelectionStayReadable()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData{profileId=Guid.NewGuid().ToString(),runsStarted=1,seenStories=new[]{"opening","first-return","artisan-first-run"}}));
            Assert.That(Chapter1ProfileHost.Service.Snapshot.RunsStarted,Is.EqualTo(1),"fixture must be a valid first-return profile");
            StartingVillageWorld village=null;
            for(int f=0;f<600;f++){yield return null;village=Object.FindAnyObjectByType<StartingVillageWorld>();if(village&&village.Ready)break;}
            Assert.That(village&&village.Ready,Is.True);yield return null;
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui");
            if((bool)Read(gui,"OpeningActive"))Call(gui,"SkipOpening");Call(gui,"CloseNotebook");Call(gui,"HideBubble");
            var station=village.Find(StartingVillageWorld.SupplyCounter);
            var card=Call(gui,"MakeCard",station);
            var cardName=(Text)card.GetType().GetField("name").GetValue(card);
            Assert.That(cardName.color.r,Is.LessThan(.15f),"station name is black on the paper speech bubble");
            Assert.AreSame(Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk"),cardName.font);
            Assert.That(cardName.rectTransform.rect.height,Is.EqualTo(22));
            var bubble=cardName.transform.parent.GetComponents<MonoBehaviour>().First(b=>b.GetType().Name=="MaintenanceBubbleGraphic");
            Assert.That((bool)bubble.GetType().GetField("UseApprovedFrame").GetValue(bubble),Is.False,"nameplates must not use the dark decorative frame");
            var paper=cardName.transform.parent.Find("Pixel border").GetComponent<RawImage>();
            var texture=(Texture2D)paper.texture;
            Assert.That(texture.GetPixel(texture.width/2,texture.height/2).r,Is.GreaterThan(.9f));
            Assert.That(texture.GetPixel(texture.width/2,5).a,Is.Zero,"compact nameplate has no pointing tail");
            Object.Destroy(cardName.transform.parent.parent.gameObject);
            village.World.arena.Player.Position=village.Find(StartingVillageWorld.ReturnWell).Position;Physics2D.SyncTransforms();village.World.RefreshCamera(0,true);yield return null;yield return null;
            StartingVillageCaptureTests.Capture(gui,village,"ui-station-bubbles");
            village.World.arena.Player.Position=station.Position;Physics2D.SyncTransforms();village.World.RefreshCamera(0,true);
            Call(gui,"OpenStation",station,true);yield return null;yield return null;
            var nodes=Field<RectTransform>(gui,"benchItems").Cast<Transform>().Where(n=>n.name.StartsWith("Node ")).ToArray();Assert.That(nodes.Length,Is.GreaterThan(0));
            Assert.That(nodes.Count(n=>n.Find("Focus").GetComponent<Image>().enabled),Is.EqualTo(1),"one selected node carries the ring");
            Assert.That(nodes.All(n=>n.Find("Approved selection")==null),"tree nodes do not use the generic button wash");
            var tabs=Field<Button[]>(gui,"benchTabs");
            bool Kept(Button tab){var visual=tab.GetComponents<MonoBehaviour>().First(b=>b.GetType().Name=="ApprovedActionVisual");return (bool)visual.GetType().GetField("PersistentSelection").GetValue(visual);}
            Assert.That(tabs.Count(Kept),Is.EqualTo(1),"only the active branch tab keeps its wash");
            StartingVillageCaptureTests.Capture(gui,village,"ui-workshop-fixed");
            Call(gui,"CloseHubPanel");Chapter1ProfileHost.UseStore(null);yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ArchiveUsesLockedPreviewsAndKeyboardCutscenesAndReadsOnSelection()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);yield return new EnterPlayMode();
            yield return Exercise();yield return new ExitPlayMode();
        }
        static IEnumerator Exercise()
        {
            var store=new MemoryProfileStore(new ProfileData{profileId=Guid.NewGuid().ToString(),seenStories=new[]{"opening","first-return"}});
            Chapter1ProfileHost.UseStore(store);StartingVillageWorld village=null;
            for(int f=0;f<600;f++){yield return null;village=Object.FindAnyObjectByType<StartingVillageWorld>();if(village&&village.Ready)break;}
            Assert.That(village&&village.Ready,Is.True);yield return null;
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui");
            if((bool)Read(gui,"OpeningActive"))Call(gui,"SkipOpening");Call(gui,"CloseNotebook");Call(gui,"HideBubble");Chapter1ProfileHost.UseStore(store);
            var profile=Chapter1ProfileHost.Service;var player=village.World.arena.Player;player.Position=village.Find(StartingVillageWorld.RecordDesk).Position;Physics2D.SyncTransforms();village.World.RefreshCamera(0,true);
            var settings=InputSystem.settings;var oldBackground=settings.backgroundBehavior;var oldEditor=settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            try
            {
                Call(gui,"OpenStation",village.Find(StartingVillageWorld.RecordDesk),true);yield return null;
                var root=Field<GameObject>(gui,"record");
                Assert.That((int)Read(gui,"RecordStoryCount"),Is.EqualTo(3),"스토리 4 (침식자·마을 잉크 수첩) was removed on 2026-11-13");Assert.That((int)Read(gui,"RecordUnlockedCount"),Is.EqualTo(1));
                Assert.That(profile.Snapshot.HasSeen("record-journal-written-0"),Is.True,"the story selected when the record opens is read");Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.False,"no dots left, so the farmhouse ! goes");
                var veil=root.GetComponentsInChildren<Image>().First(i=>i.name=="Archive dark veil");Assert.That(veil.color.a,Is.EqualTo(244f/255f).Within(.001));
                Assert.That(veil.color.r,Is.EqualTo(36f/255f).Within(.001));
                var chosen=root.GetComponentsInChildren<Image>().First(i=>i.name=="스토리 1");
                Assert.That(chosen.color.r,Is.GreaterThan(chosen.color.b),"selection uses the character amber rather than blue");
                var archiveFont=Field<Font>(gui,"recordFont");Assert.That(archiveFont.dynamic,Is.True,"record archive uses the approved scalable menu face");
                Assert.That(Field<Text>(gui,"recordTitle").fontSize,Is.EqualTo(26));Assert.That(Field<Text>(gui,"recordPreviewTitle").fontSize,Is.EqualTo(17));
                var play=Field<Button>(gui,"recordPlay");var playRect=(RectTransform)play.transform;var playKey=(RectTransform)play.transform.Find("Key");
                Assert.That(playRect.rect.height,Is.EqualTo(46));Assert.That(playRect.rect.width,Is.InRange(116f,130f));Assert.That(playKey.rect.width,Is.EqualTo(26).Within(1));
                Assert.That(play.GetComponentsInChildren<Text>()[0].fontSize,Is.EqualTo(13));
                var close=Field<Button>(gui,"recordClose");Assert.That(close.GetComponent<Image>().color.a,Is.Zero,"close is an inline key hint, without a surrounding button box");
                Assert.That(close.GetComponentsInChildren<Image>().Length,Is.EqualTo(6),"only the key badge has a border");
                Assert.That(close.GetComponentsInChildren<Text>()[0].rectTransform.anchoredPosition.x,Is.GreaterThan(((RectTransform)close.transform.Find("Key")).anchoredPosition.x));
                Assert.That(Field<Button>(gui,"recordReturn").GetComponent<Image>().color.a,Is.Zero);Assert.That(Field<Button>(gui,"recordNext").GetComponent<Image>().color.a,Is.Zero);
                Assert.That(Field<Image>(gui,"recordFooterKey").rectTransform.rect.width,Is.EqualTo(19).Within(1),"footer F badge resizes after the input profile resolves");
                // World Interact is rebindable; archive confirmation and navigation remain fixed.
                var input=Deviant.Combat.PlayerControls.For(village.World.arena.Player);
                var interact=input.Source.GetAction(Deviant.Input.Contracts.GameAction.Interact);
                int binding=Enumerable.Range(0,interact.bindings.Count).First(i=>interact.bindings[i].effectivePath=="<Keyboard>/f");
                string previousOverride=interact.bindings[binding].overridePath;
                try
                {
                    interact.ApplyBindingOverride(binding,"<Keyboard>/k");yield return null;
                    yield return Press(keyboard,Key.F);Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.True,"archive playback remains on fixed F after rebinding world Interact");
                    yield return Press(keyboard,Key.Escape);Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False);
                    yield return Press(keyboard,Key.K);Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False,"world Interact does not confirm a menu item");
                    Assert.That(Field<Image>(gui,"recordFooterKey").GetComponentInChildren<Text>().text,Is.EqualTo("F"));
                    Assert.That(Field<Button>(gui,"recordReturn").transform.Find("Key").GetComponentInChildren<Text>().text,Is.EqualTo("Esc"));
                    yield return Press(keyboard,Key.Escape);Assert.That(Field<GameObject>(gui,"hubOpen"),Is.Null);
                    Assert.That(interact.bindings[binding].effectivePath,Is.EqualTo("<Keyboard>/k"),"closing the archive preserves the world binding");
                }
                finally
                {
                    if(previousOverride==null)interact.RemoveBindingOverride(binding);else interact.ApplyBindingOverride(binding,previousOverride);
                }
                Call(gui,"OpenStation",village.Find(StartingVillageWorld.RecordDesk),true);yield return null;
                StartingVillageCaptureTests.Capture(gui,village,"unity-record-list");
                yield return Press(keyboard,Key.DownArrow);Assert.That((bool)Read(gui,"RecordSelectionLocked"),Is.True);
                Assert.That(Field<RawImage>(gui,"recordPreviewArt").texture,Is.Null);Assert.That(Field<Button>(gui,"recordPlay").interactable,Is.False);
                Assert.That(Field<Text>(gui,"recordPreviewText").text,Is.EqualTo("게임을 진행하면 이야기가 열립니다."));
                yield return Press(keyboard,Key.F);Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False);
                StartingVillageCaptureTests.Capture(gui,village,"unity-record-locked");
                yield return Press(keyboard,Key.UpArrow);yield return Press(keyboard,Key.Enter);Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False,"F owns playback, not Submit/Enter");
                yield return Press(keyboard,Key.F);Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.True);Assert.That((bool)Read(gui,"RecordBusy"),Is.True);
                yield return new WaitForSecondsRealtime(.1f);float alpha=Field<CanvasGroup>(gui,"recordPageFade").alpha;Assert.That(alpha,Is.InRange(.001f,.999f));
                yield return Ready(gui);Assert.That((int)Read(gui,"RecordPageCount"),Is.EqualTo(8));
                var frames=new[]{"01","02","03","04","05","06","08","09"};
                for(int i=0;i<8;i++)
                {
                    var art=Field<RawImage>(gui,"recordIllustration");Assert.That(art.texture,Is.SameAs(Resources.Load<Texture2D>("RecordFarmStories/story1/"+frames[i])));
                    Assert.That(art.rectTransform.rect.size,Is.EqualTo(new Vector2(960,540)));Assert.That(Field<Text>(gui,"recordBody").text,Is.Not.Empty);Assert.That(Field<Text>(gui,"recordBody").preferredHeight*Field<Text>(gui,"recordBody").rectTransform.localScale.y,Is.LessThanOrEqualTo(49),"caption fits the approved two-line black bar");
                    Assert.That(Field<Text>(gui,"recordPageLabel").text,Is.EqualTo((i+1)+" / 8 페이지"));
                    if(i==0||i==5||i==6||i==7)StartingVillageCaptureTests.Capture(gui,village,"unity-story1-page-"+(i+1));
                    yield return Press(keyboard,i%2==0?Key.Space:Key.F);yield return Ready(gui);
                }
                Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False);Assert.That(profile.Snapshot.HasSeen("record-journal-written-0"),Is.True);Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.False);
                Assert.That(village.World.arena.Controls.Current.SimulationPaused,Is.True,"page completion returns to the paused list");
                profile.SettleRun(profile.PlanUnarmedRun().RunId,RunOutcome.Died,false);profile.MarkStorySeen("artisan-first-run");Call(gui,"RefreshRecordList");
                Assert.That((int)Read(gui,"RecordUnlockedCount"),Is.EqualTo(1),"chapter transition story waits for the chapter clear, not the first death");
                profile.RecordChapterOneClear(profile.PlanUnarmedRun().RunId,false);Call(gui,"RefreshRecordList");
                Assert.That((int)Read(gui,"RecordUnlockedCount"),Is.EqualTo(2));Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.True);
                Assert.That(profile.Snapshot.HasSeen("record-journal-written-1"),Is.False,"an unselected story keeps its dot");
                Call(gui,"SelectRecordStory",1);Assert.That(profile.Snapshot.HasSeen("record-journal-written-1"),Is.True);Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.False);
                StartingVillageCaptureTests.Capture(gui,village,"unity-record-unlocked-two");yield return Press(keyboard,Key.F);yield return Ready(gui);
                for(int i=0;i<8;i++)
                {
                    Assert.That(Field<RawImage>(gui,"recordIllustration").texture,Is.SameAs(Resources.Load<Texture2D>("RecordFarmStories/story2/"+(i+1).ToString("00"))));
                    if(i==4)Assert.That(Field<Text>(gui,"recordBody").text,Is.EqualTo("어떻게 하면 안으로 들어갈 수 있을지 곰곰이 생각해 봤다."));
                    if(i==5)Assert.That(Field<Text>(gui,"recordBody").text,Is.EqualTo("성벽 아래에서 수로 입구를 발견했다."));
                    if(i==0||i==5)StartingVillageCaptureTests.Capture(gui,village,"unity-story2-page-"+(i+1));
                    yield return Press(keyboard,Key.Space);yield return Ready(gui);
                }
                Assert.That(profile.Snapshot.HasSeen("record-journal-written-1"),Is.True);Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.False);
                Assert.That((int)Read(gui,"RecordUnlockedCount"),Is.EqualTo(2),"story 3 waits until the first village ink has been used");
                profile.MarkStorySeen("village-ink-return-1");Call(gui,"RefreshRecordList");
                Assert.That((int)Read(gui,"RecordUnlockedCount"),Is.EqualTo(3));Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.True);Call(gui,"SelectRecordStory",2);yield return null;
                Assert.That(profile.Snapshot.HasSeen("record-journal-written-2"),Is.True);Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.False);
                Assert.That(Field<RawImage>(gui,"recordPreviewArt").texture,Is.SameAs(Resources.Load<Texture2D>("RecordFarmStories/previews/story3")));
                Assert.That(Field<RectTransform>(gui,"stationCards").gameObject.activeSelf,Is.False,"the sign names do not show through the record's veil");
                Assert.That(Field<Text>(gui,"recordTitle").text,Is.EqualTo("기록"));
                StartingVillageCaptureTests.Capture(gui,village,"unity-record-story3-preview");
                yield return Press(keyboard,Key.F);yield return Ready(gui);
                var thirdFrames=new[]{"01","03","04","05","06","07","10"};
                for(int i=0;i<thirdFrames.Length;i++)
                {
                    var picture=Field<RawImage>(gui,"recordIllustration");var caption=Field<Text>(gui,"recordBody");
                    Assert.That(picture.texture,Is.SameAs(Resources.Load<Texture2D>("RecordFarmStories/story3/"+thirdFrames[i])));
                    Assert.That(picture.texture,Is.Not.Null);Assert.That(picture.rectTransform.rect.size,Is.EqualTo(new Vector2(960,540)));
                    Assert.That(Field<Text>(gui,"recordPageLabel").text,Is.EqualTo((i+1)+" / 7 페이지"));
                    if(i==0)Assert.That(caption.text,Is.EqualTo("시계탑 집행자가 사라진 자리에 붉은 잉크병이 남아 있었다.\n어디에 쓰는 거지?"));
                    if(i==4)Assert.That(caption.text,Is.EqualTo("남아 있던 잉크도 우물에 부었다."));
                    if(i==6)Assert.That(caption.text,Is.EqualTo("모두 돌아오지는 않는 것 같다.\n다른 잉크도 찾아야겠다."));
                    Assert.That(caption.text,Is.Not.Empty);Assert.That(caption.preferredHeight*caption.rectTransform.localScale.y,Is.LessThanOrEqualTo(49));
                    if(i==0||i==3||i==6)StartingVillageCaptureTests.Capture(gui,village,"unity-story3-page-"+(i+1));
                    yield return Press(keyboard,i%2==0?Key.F:Key.Space);yield return Ready(gui);
                }
                Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False);Assert.That(profile.Snapshot.HasSeen("record-journal-written-2"),Is.True);
                Assert.That((bool)Read(gui,"RecordHasUpdate"),Is.False);
                yield return Press(keyboard,Key.F);Assert.That((bool)Read(gui,"RecordBusy"),Is.True);yield return Press(keyboard,Key.Escape);yield return new WaitForSecondsRealtime(.5f);
                Assert.That((bool)Read(gui,"RecordDetailShowing"),Is.False,"Esc cancels the fade without a stale callback reopening the page");
                Assert.That(village.World.arena.Controls.Current.SimulationPaused,Is.True);
                yield return Press(keyboard,Key.Escape);Assert.That(Field<GameObject>(gui,"hubOpen"),Is.Null);Assert.That(village.World.arena.Controls.Current.SimulationPaused,Is.False);
                Chapter1ProfileHost.UseStore(store);Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("record-journal-written-1"),Is.True);
            }
            finally{InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=oldBackground;settings.editorInputBehaviorInPlayMode=oldEditor;Chapter1ProfileHost.UseStore(null);}
        }
    }
}
