using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class VillageInkReturnTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Read(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(done(), Is.True, "condition timed out after " + seconds + " seconds");
        }
        [UnityTest] public IEnumerator RecoveredRewardRestoresUsedColourBeforeItIsConfirmed()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            try
            {
                var saved = new ProfileData { profileId=System.Guid.NewGuid().ToString(), chaptersCleared=2,
                    chapter1Clears=1,chapter1BossFirstClear=true,seenStories=new[] {"opening","first-return","artisan-first-run",
                    "artisan-visit-1","village-ink-return-1","village-ink-return-2","village-ink-reward-1","village-executioner-note"} };
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(saved));
                StartingVillageWorld village=null;
                yield return Until(()=> {village=Object.FindAnyObjectByType<StartingVillageWorld>();return village && village.Ready;},30);
                var gui=Object.FindObjectsByType<MonoBehaviour>().First(b=>b.GetType().Name=="TemporaryGameGui");
                if((bool)Read(gui,"OpeningActive")) Call(gui,"SkipOpening");
                Call(gui,"CloseVillageInkModal");
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(saved));
                village.ColourPieces=0;
                Call(gui,"CheckVisit");
                Assert.That((bool)Read(gui,"VillageInkModalShowing"),Is.True);
                Assert.That(village.ColourPieces,Is.EqualTo(2),"saved colours must be present behind the recovered reward");
                Assert.That((bool)Read(gui,"VillageInkShowing"),Is.False,"used ink must never be poured twice");
                yield return Until(()=>(bool)Read(gui,"VillageInkRewardShowing"),3);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("village-ink-reward-2"),Is.False);
                Call(gui,"ConfirmVillageInkModal");
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("village-ink-reward-2"),Is.True);
            }
            finally {Chapter1ProfileHost.UseStore(null);}
            yield return new ExitPlayMode();
        }

        static IEnumerator UntilReveal(MonoBehaviour gui, float seconds, bool includeApproach = false)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            bool Ready() => (bool)Read(gui,"VillageInkShowing") && (includeApproach || (string)Read(gui,"VillageInkPhase")!="approach");
            while (!Ready() && Time.realtimeSinceStartup < deadline)
            {
                if (Read(gui, "BubbleShowing") != null)
                { Call(gui, "AdvanceBubble"); Call(gui, "TickBubble"); Call(gui, "AdvanceBubble"); }
                yield return null;
            }
            Assert.That(Ready(), Is.True);
        }

        static IEnumerator FinishReading(MonoBehaviour gui, float seconds)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while((int)Read(gui,"VisitPlaying")!=0 && Time.realtimeSinceStartup<deadline)
            {
                if(Read(gui,"BubbleShowing")!=null)
                { Call(gui,"AdvanceBubble"); Call(gui,"TickBubble"); Call(gui,"AdvanceBubble"); }
                yield return null;
            }
            Assert.That((int)Read(gui,"VisitPlaying"),Is.Zero);
        }

        [System.Serializable] sealed class MovieTiming { public float[] times; }
        [System.Serializable] sealed class ColourLayout { public StartingVillageWorld.Blot[] colourBlots; }
        static ProfileData VillageStage(int pieces) => new ProfileData
        {
            profileId = System.Guid.NewGuid().ToString(), chaptersCleared = pieces,
            chapter1Clears = pieces > 0 ? 1 : 0, chapter1BossFirstClear = pieces > 0,
            runsStarted = 1, runsDied = 1,
            seenStories = new[] { "opening", "first-return", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "artisan-visit-3", "artisan-visit-4", "artisan-visit-5", "village-ink-reward-1", "village-ink-reward-2", "village-ink-reward-3", "village-ink-reward-4", "village-ink-reward-5", "village-executioner-note" }
        };

        // Review-only capture: keep the same camera and actual colour-mask data at every owned-ink count.
        [UnityTest] public IEnumerator CaptureVillageStagesAndFirstInkReturn()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(VillageStage(0)));
                StartingVillageWorld village = null;
                yield return Until(() => { village = Object.FindAnyObjectByType<StartingVillageWorld>(); return village && village.Ready; }, 30);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                if ((bool)Read(gui, "OpeningActive")) Call(gui, "SkipOpening");
                var viewport = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "VillagePixelViewport");
                viewport.GetType().GetField("enableInBatchMode").SetValue(viewport, true); viewport.enabled = true;
                System.IO.Directory.CreateDirectory("../outputs/hub/capture/ink-flow");
                var player = village.World.arena.Player;
                for (int pieces = 0; pieces <= 5; pieces++)
                {
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore(VillageStage(pieces)));
                    village.ClearPreview(); village.ColourPieces = pieces;
                    player.Position = village.Find(StartingVillageWorld.ReturnWell).Position; player.velocity = Vector2.zero;
                    village.World.FixedCameraCenter = new Vector2(15, player.Position.y + 7);
                    Physics2D.SyncTransforms(); village.World.RefreshCamera(0, true);
                    yield return null; yield return null;
                    Assert.That(village.ColourPieces, Is.EqualTo(pieces));
                    if (pieces == 5)
                    {
                        var layout = JsonUtility.FromJson<ColourLayout>(Resources.Load<TextAsset>("HubLayout").text);
                        Assert.That(Shader.GetGlobalFloat("_VillageBlotCount"), Is.EqualTo(layout.colourBlots.Length),
                            "all five ink masks, including the final village-wide bloom, must reach the renderer");
                    }
                    CapturePanorama(village, pieces);
                    StartingVillageCaptureTests.Capture(gui, village, "village-stage-" + pieces + "-well", 1920, 1080);
                }
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData {
                    profileId=System.Guid.NewGuid().ToString(),chaptersCleared=1,chapter1Clears=1,chapter1BossFirstClear=true,
                    seenStories=new[]{"opening","first-return","artisan-first-run"} }));
                // Return beside the well, as in the playable arrival, after the centred panorama comparisons.
                player.Position = village.Find(StartingVillageWorld.ReturnWell).Position + Vector2.right * 3.6f;
                player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                village.World.FixedCameraCenter = new Vector2(15, player.Position.y + 7);
                village.World.RefreshCamera(0, true);
                gui.GetType().GetProperty("VisitData", Hidden).GetValue(gui);
                Call(gui, "StartVisit", 1);
                var beforePour = player.Position;
                yield return UntilReveal(gui, 24, true);
                var view = (MonoBehaviour)gui.GetType().GetField("villageInkPour", Hidden).GetValue(gui);
                var times = new System.Collections.Generic.List<float>();
                float next = 0, movieBegan=Time.unscaledTime;
                while ((bool)Read(gui, "VillageInkShowing"))
                {
                    float elapsed = Time.unscaledTime-movieBegan;
                    if (elapsed >= next)
                    {
                        StartingVillageCaptureTests.Capture(gui, village, "ink-flow/frame-" + times.Count.ToString("D4"));
                        times.Add(elapsed); next = elapsed + .1f;
                    }
                    yield return null;
                }
                System.IO.File.WriteAllText("../outputs/hub/capture/ink-flow/timing.json", JsonUtility.ToJson(new MovieTiming { times = times.ToArray() }, true));
                Assert.That(times.Count, Is.GreaterThan(20), "recorded the actual return sequence");
                Assert.That(player.Position.x,Is.LessThan(beforePour.x-.5f),"the hero takes a few steps towards the well before the bottle appears");
                Assert.That(player.facing,Is.EqualTo(-1));
                float turnDeadline=Time.realtimeSinceStartup+12;
                while((string)Read(gui,"BubbleShowing")!="나머지 잉크도 찾아줄 수 있을까?" && Time.realtimeSinceStartup<turnDeadline)
                {
                    if(Read(gui,"BubbleShowing")!=null) { Call(gui,"AdvanceBubble"); Call(gui,"TickBubble"); Call(gui,"AdvanceBubble"); }
                    yield return null;
                }
                Assert.That((string)Read(gui,"BubbleShowing"),Is.EqualTo("나머지 잉크도 찾아줄 수 있을까?"));
                Call(gui,"AdvanceBubble"); Call(gui,"TickBubble"); Call(gui,"AdvanceBubble");
                yield return Until(()=>(string)Read(gui,"BubbleShowing")=="고마워. 네 덕분에 희망이 생겼네.",4);
                Assert.That(player.facing,Is.EqualTo(1),"the hero turns back towards the artisan before the thanks");
                yield return new WaitForSecondsRealtime(1.6f);
                StartingVillageCaptureTests.Capture(gui,village,"red-ink-turn-before-thanks",1920,1080);
                yield return FinishReading(gui,35);
                Assert.That(village.LastArtisanExitOffscreen,Is.True,"the artisan leaves only after its whole sprite is outside the view");
                yield return Until(() => (bool)Read(gui,"VillageInkRewardShowing"),3);
                yield return new WaitForSecondsRealtime(.8f);
                StartingVillageCaptureTests.Capture(gui,village,"red-ink-reward-final",1920,1080);
                Call(gui,"ConfirmVillageInkModal"); yield return new WaitForSecondsRealtime(.5f);
                StartingVillageCaptureTests.Capture(gui,village,"executioner-notebook-final",1920,1080);
                Call(gui,"ConfirmVillageInkModal");
                Call(gui,"ConfirmVillageInkModal");
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }

        static void CapturePanorama(StartingVillageWorld village, int pieces)
        {
            const int width = 2688, height = 540;
            var camera = village.World.View; var target = camera.targetTexture; var rect = camera.rect;
            var position = camera.transform.position; float size = camera.orthographicSize, aspect = camera.aspect;
            int mask = camera.cullingMask;
            var sky = (SpriteRenderer)village.GetType().GetField("sky", Hidden).GetValue(village);
            var skyScale = sky.transform.localScale;
            var texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = texture; camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = width / (float)height; camera.orthographicSize = height / 64f;
                camera.transform.position = new Vector3(42, village.Find(StartingVillageWorld.ReturnWell).Position.y + 7, position.z);
                camera.cullingMask = mask & ~(1 << LayerMask.NameToLayer("UI"));
                sky.transform.localScale = new Vector3(84f / sky.sprite.bounds.size.x, skyScale.y, skyScale.z);
                Call(village, "TickApprovedBackground");
                camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes("../outputs/hub/capture/village-stage-" + pieces + "-panorama.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null; camera.targetTexture = target; camera.rect = rect;
                camera.orthographicSize = size; camera.aspect = aspect; camera.transform.position = position; camera.cullingMask = mask;
                sky.transform.localScale = skyScale; Call(village, "TickApprovedBackground");
                RenderTexture.ReleaseTemporary(texture); Object.Destroy(image);
            }
        }

        [UnityTest] public IEnumerator FiveReturnsPourOnceRememberProgressAndNeverInstallAStand()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData
                    { profileId = System.Guid.NewGuid().ToString(), seenStories = new[] { "opening", "artisan-first-run" } }));
                StartingVillageWorld village = null;
                yield return Until(() => { village = Object.FindAnyObjectByType<StartingVillageWorld>(); return village && village.Ready; }, 30);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                if ((bool)Read(gui, "OpeningActive")) Call(gui, "SkipOpening");
                gui.GetType().GetProperty("VisitData", Hidden).GetValue(gui);
                System.IO.Directory.CreateDirectory("../outputs/hub/capture");
                var pixel = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "VillagePixelViewport");
                if (pixel) { pixel.GetType().GetField("enableInBatchMode").SetValue(pixel, true); pixel.enabled = true; }
                for (int piece = 1; piece <= 5; piece++)
                {
                    if ((bool)Read(gui, "OpeningActive")) Call(gui, "SkipOpening");
                    var seen = new System.Collections.Generic.List<string> { "opening", "artisan-first-run" };
                    for(int k=1;k<piece;k++) { seen.Add("artisan-visit-"+k); seen.Add("village-ink-return-"+k); seen.Add("village-ink-reward-"+k); }
                    if(piece>1) seen.Add("village-executioner-note");
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData
                    {
                        profileId = System.Guid.NewGuid().ToString(), chaptersCleared = piece,
                        chapter1BossFirstClear = true, chapter1Clears = 1,
                        seenStories = seen.ToArray()
                    }));
                    if(piece==1) Call(gui, "StartVisit", piece);
                    else
                    {
                        yield return new WaitForSecondsRealtime(.5f);
                        Assert.That((int)Read(gui,"VisitPlaying"),Is.Zero,"later inks wait for F at the well");
                        Assert.That(village.ColourPieces,Is.EqualTo(piece-1));
                        Assert.That((bool)Read(gui,"VillageInkModalShowing"),Is.False);
                        if(piece==2) Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ReclaimBench)),Is.False,"the door stays locked until its artisan scene");
                        village.World.arena.Player.Position = village.Find(StartingVillageWorld.ReturnWell).Position + Vector2.right;
                        village.World.arena.Player.velocity=Vector2.zero;
                        Assert.That(village.TryInteract(),Is.True,"normal station interaction starts the ink return");
                    }
                    yield return UntilReveal(gui, 24);
                    var view = (MonoBehaviour)gui.GetType().GetField("villageInkPour", Hidden).GetValue(gui);
                    void Advance(float dt) => view.GetType().GetMethod("Step").Invoke(view, new object[] { dt });
                    Advance(1);
                    Assert.That(village.ColourPieces, Is.EqualTo(piece - 1), "colour waits for the ink to reach the well");
                    Assert.That((bool)Read(gui,"VillageInkRewardShowing"), Is.False, "the acquisition screen waits until the artisan leaves");
                    Assert.That(village.PaletteShown, Is.False);
                    var heroAt = village.World.arena.Player.Position;
                    var bottle = view.GetComponentsInChildren<SpriteRenderer>().First();
                    Assert.That(bottle.bounds.size.y, Is.LessThan(2.5f), "the in-world bottle stays near drop-item size");
                    Advance(2.65f);
                    var stream = view.GetComponentInChildren<LineRenderer>();
                    Assert.That(stream.positionCount, Is.EqualTo(2), "ink falls in one straight line");
                    Assert.That(stream.GetPosition(0).x, Is.EqualTo(stream.GetPosition(1).x).Within(.001f));
                    Assert.That(stream.GetPosition(0).y, Is.GreaterThan(stream.GetPosition(1).y));
                    Assert.That(village.World.arena.Player.facing, Is.EqualTo(village.Find(StartingVillageWorld.ReturnWell).Position.x < heroAt.x ? -1 : 1));
                    Assert.That(village.ColourPieces, Is.EqualTo(piece));
                    Assert.That(village.Blooming, Is.True);
                    Assert.That((int)Read(gui, "VisitPlaying"), Is.EqualTo(piece), "the director waits for the reveal before continuing");
                    Assert.That(village.World.arena.Player.Position, Is.EqualTo(heroAt), "the bottle moves while the hero stays put");
                    if (piece == 1) StartingVillageCaptureTests.Capture(gui,village,"village-ink-1-pour",1920,1080);
                    Advance(3);
                    Assert.That((bool)Read(gui,"VillageInkShowing"), Is.False);
                    Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("village-ink-return-"+piece), Is.True);
                    if (piece == 1)
                    {
                        yield return FinishReading(gui, 35);
                        Assert.That(village.ArtisanShown, Is.False);
                        Assert.That(village.LastArtisanExitOffscreen,Is.True);
                        yield return Until(() => (bool)Read(gui,"VillageInkRewardShowing"),3);
                        yield return new WaitForSecondsRealtime(.8f);
                        Assert.That(gui.GetComponentsInChildren<Text>().Single(t=>t.name=="마을 잉크 개수").text,Is.EqualTo("1 / 5"));
                        var reward = (MonoBehaviour)gui.GetType().GetField("villageInkView",Hidden).GetValue(gui);
                        Assert.That(reward.GetComponentsInChildren<Image>().Count(i=>i.name.StartsWith("마을 잉크 방울 ") && i.sprite.name.EndsWith("progress-drop-filled")),Is.EqualTo(1));
                        Assert.That(reward.GetComponentsInChildren<Image>().Single(i=>i.name=="획득한 닫힌 잉크 병").sprite.name,Does.EndWith("red-closed"));
                        var icon=reward.GetComponentsInChildren<Image>().Single(i=>i.name=="획득한 닫힌 잉크 병").rectTransform;
                        Assert.That(icon.pivot,Is.EqualTo(Vector2.one*.5f));
                        Assert.That(icon.anchoredPosition.x,Is.EqualTo(480));
                        Assert.That(icon.sizeDelta,Is.EqualTo(new Vector2(220,220)));
                        StartingVillageCaptureTests.Capture(gui,village,"red-ink-reward-final",1920,1080);
                        // Confirming the first ink closes it: the 침식자 / 마을 잉크 notebook is gone (user 2026-11-13).
                        Call(gui,"ConfirmVillageInkModal");
                        Assert.That((bool)Read(gui,"VillageInkRewardShowing"),Is.False);
                        Assert.That((bool)Read(gui,"VillageInkModalShowing"),Is.False);
                        Assert.That(gui.GetComponentsInChildren<Image>(true).Any(i=>i.name=="침식자와 우물 수첩 삽화"),Is.False);
                        Call(gui,"BeginVillageInkReward",piece);
                        Assert.That((bool)Read(gui,"VillageInkModalShowing"),Is.False,"completed cards never replay");
                    }
                    else
                    {
                        yield return Until(() => (bool)Read(gui,"VillageInkRewardShowing"),3);
                        Assert.That((int)Read(gui,"VisitPlaying"),Is.Zero);
                        Call(gui,"ConfirmVillageInkModal");
                        yield return null;
                        if (piece == 5)
                        {
                            yield return Until(() => (int)Read(gui, "VisitPlaying") == 5, 3);
                            yield return FinishReading(gui, 20);
                            yield return Until(() => !village.ArtisanMoving, 6);
                            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-visit-5"), Is.True);
                            Assert.That(village.ArtisanShown, Is.True, "the fifth visit leaves the artisan living at its workshop");
                            Assert.That(village.ArtisanX, Is.EqualTo(32.2f).Within(.05f));
                            Assert.That(village.ArtisanBusy, Is.Empty);
                        }
                        else if((bool)Read(gui,"OpeningActive")) Call(gui,"SkipOpening");
                        yield return new WaitForSecondsRealtime(.6f);
                        village.World.arena.Player.Position=village.Find(StartingVillageWorld.ReturnWell).Position+Vector2.right;
                        Assert.That(village.WellHasInk,Is.False,"a poured ink leaves nothing for the well");
                        Assert.That(village.TryInteract(),Is.False,"without an ink the well has no F at all");
                        Assert.That((bool)Read(gui,"VillageInkShowing"),Is.False,"used ink never pours again");
                        Assert.That((GameObject)gui.GetType().GetField("hubOpen",Hidden).GetValue(gui),Is.Null,"the well opens no screen or bubble");
                    }
                    Assert.That((int)Read(gui,"VisitPlaying"),Is.Zero);
                    Assert.That(village.PaletteShown,Is.False);
                    Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat),Is.False);
                    Call(gui, "BeginVillageInk", piece);
                    Assert.That((bool)Read(gui, "VillageInkShowing"), Is.False, "revisiting an already shown ink does not replay it");
                }
                // Skipping before the pour still settles the return and releases the controls.
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData
                { profileId = System.Guid.NewGuid().ToString(), chaptersCleared = 5, chapter1Clears = 1,
                    chapter1BossFirstClear = true, seenStories = new[] { "opening", "artisan-first-run", "artisan-visit-1", "village-ink-return-1", "village-ink-reward-1", "village-executioner-note" } }));
                Call(gui, "StartWellInkReturn", 2);
                yield return Until(() => (bool)Read(gui, "VillageInkShowing"), 3);
                Call(gui, "SkipOpening");
                Assert.That((bool)Read(gui, "VillageInkShowing"), Is.False);
                Assert.That(village.ColourPieces, Is.EqualTo(2));
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("village-ink-return-2"), Is.True);
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False);
                Call(gui,"CheckVisit");
                Assert.That((int)Read(gui,"VisitPlaying"),Is.EqualTo(2),"owning more inks must not skip the second ink's facility visit");
                Call(gui,"SkipOpening");
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(village.ColourPieces,Is.EqualTo(2),"unused green, blue and purple inks cannot colour the town");
                Assert.That((int)gui.GetType().GetProperty("PendingWellInk",Hidden).GetValue(gui),Is.EqualTo(3));

                // Skipping the final facility visit has the same permanent home as reading it to the end.
                var finalVisit = VillageStage(5);
                finalVisit.seenStories = finalVisit.seenStories.Where(id => id != "artisan-visit-5")
                    .Concat(new[] { "village-ink-return-5" }).ToArray();
                var finalStore = new MemoryProfileStore(finalVisit);
                Chapter1ProfileHost.UseStore(finalStore);
                Call(gui, "StartVisit", 5);
                Assert.That((int)Read(gui, "VisitPlaying"), Is.EqualTo(5));
                Call(gui, "SkipOpening");
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-visit-5"), Is.True);
                Assert.That(village.ArtisanShown, Is.True);
                Assert.That(village.ArtisanMoving, Is.False);
                Assert.That(village.ArtisanX, Is.EqualTo(32.2f).Within(.05f));
                Assert.That(village.ArtisanBusy, Is.Empty);
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False);

                // Reconstruct both the service and the village, so an old scene's artisan cannot satisfy this check.
                var completedService = Chapter1ProfileHost.Service;
                Chapter1ProfileHost.UseStore(finalStore);
                Assert.That(Chapter1ProfileHost.Service, Is.Not.SameAs(completedService));
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-visit-5"), Is.True);
                yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(StartingVillageTests.ScenePath);
                village = null; gui = null;
                yield return Until(() =>
                {
                    village = Object.FindAnyObjectByType<StartingVillageWorld>();
                    gui = Object.FindObjectsByType<MonoBehaviour>().FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
                    return village && village.Ready && gui && village.ArtisanShown;
                }, 30);
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(village.ArtisanShown, Is.True, "the artisan's fifth-visit home survives a saved-profile reload");
                Assert.That(village.ArtisanMoving, Is.False);
                Assert.That(village.ArtisanX, Is.EqualTo(32.2f).Within(.05f));
                Assert.That((int)Read(gui, "VisitPlaying"), Is.Zero, "the completed fifth visit does not replay on reload");
                Assert.That((bool)Read(gui, "VillageInkModalShowing"), Is.False);
                Assert.That(village.ColourPieces, Is.EqualTo(5));
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }
    }
}
