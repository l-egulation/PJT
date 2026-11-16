using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // Parts 1-3 of the playable opening (docs/design/story/opening/opening-playable.md) with real key presses: walk in at the far
    // end of the road, find the strip of moves up and every move but the wall open at once (F closes the strip), pass the
    // looping wall-climb and dash-dodge ghosts (the wall opens by its ghost), overhear the two bystanders, watch the erasure swallow them, get caught once (and start the
    // chase again at its checkpoint, also after a fall into a chase pit), be closed in at the well, read the helper's lines
    // with F and wake up there. Captures go to outputs/hub/capture/opening-*.png.
    public sealed class StartingVillageOpeningTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Keyboard keyboard;

        static string Banner(MonoBehaviour gui) => (string)gui.GetType().GetProperty("BannerShowing").GetValue(gui);
        static string Bubble(MonoBehaviour gui) => (string)gui.GetType().GetProperty("BubbleShowing").GetValue(gui);
        // Every speaker's bubble is the maintenance shops' bubble.
        static RectTransform Speech() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "MaintenanceBubbleView")
            .GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Speech bubble");
        static string SpeechName() => Speech().GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t => t.name == "Merchant name").text;
        // The village screen that is open, by the GUI's own field name (record, workbench).
        static string HubOpen(MonoBehaviour gui)
        {
            var open = (GameObject)gui.GetType().GetField("hubOpen", Private).GetValue(gui);
            if (!open) return null;
            foreach (var name in new[] { "record", "workbench" })
                if ((GameObject)gui.GetType().GetField(name, Private).GetValue(gui) == open) return name;
            return open.name;
        }
        static string Lesson(MonoBehaviour gui) => (string)gui.GetType().GetProperty("LessonShowing").GetValue(gui);
        static string State(MonoBehaviour gui, StartingVillageWorld village)
        {
            var director = gui.GetType().GetField("opening", Private).GetValue(gui);
            string Get(string name) => director?.GetType().GetProperty(name)?.GetValue(director)?.ToString();
            return $"outcome={Get("Outcome")} part={Get("Part")} error={Get("Error")} finished=[{string.Join(",", (System.Collections.Generic.IEnumerable<string>)director?.GetType().GetProperty("Finished").GetValue(director) ?? new string[0])}] " +
                $"story={gui.GetType().GetField("storyActive", Private).GetValue(gui)} x={village.World.arena.Player.Position.x:0.00} walking={village.ScriptedWalking} banner={Banner(gui)} bubble={Bubble(gui)} lesson={Lesson(gui)} paused={gui.GetType().GetField("hubPaused", Private).GetValue(gui)} open={gui.GetType().GetField("hubOpen", Private).GetValue(gui)}";
        }
        static int Solids(StartingVillageWorld village, string id) => village.World.GetComponentsInChildren<BoxCollider2D>().Count(c => c.name == "Opening course " + id);

        IEnumerator Press(Key key, float seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            // At least two frames down: a short press could fall between two slow batch-mode frames.
            int frames = 0;
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < seconds || frames < 2; frames++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            for (float at = Time.realtimeSinceStartup; !done() && Time.realtimeSinceStartup - at < seconds;) yield return null;
        }

        // Waits for a line, lets it type out, and reads it with F.
        IEnumerator Read(MonoBehaviour gui, StartingVillageWorld village, string line, float seconds = 4)
        {
            yield return WaitFor(() => Bubble(gui) == line, seconds);
            Assert.That(Bubble(gui), Is.EqualTo(line), State(gui, village));
            yield return WaitFor(() => false, Mathf.Min(2, line.Length / 30f + .2f));
            // An F that lands while the line is still typing only finishes it: press again until it moves on.
            for (int tries = 0; tries < 3 && Bubble(gui) == line; tries++)
            {
                yield return Press(Key.F, .05f);
                yield return WaitFor(() => Bubble(gui) != line, 1);
            }
        }

        [UnityTest] public IEnumerator TheOpeningTeachesMovingThenErasesTheVillageAndWakesTheHero()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                yield return null; yield return null;
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                // The GUI may have checked the real editor profile before the memory store was installed: check again.
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
                yield return null; yield return null;
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.True, "a new profile walks into the village");
                Assert.That(village.Preview, Is.False, "the village is in colour before the erasure");
                Assert.That(village.HeroBody, Is.EqualTo(village.OpeningBody), "a human walks the road, bare-handed");
                Assert.That(village.StationsSuppressed, Is.True); Assert.That(village.NearestStation, Is.Null);
                Assert.That(Solids(village, "handcart"), Is.EqualTo(0), "decoration: walked past");
                Assert.That(Solids(village, "wall"), Is.EqualTo(15), "moss walls: 9 on the learning road, 6 in the chase");
                Assert.That(Solids(village, "fence"), Is.EqualTo(5)); Assert.That(Solids(village, "crate"), Is.EqualTo(1));
                Assert.That(Solids(village, "platform"), Is.EqualTo(1), "one ledge in the chase");
                Assert.That(village.ResidentTop("merchant"), Is.Not.Null); Assert.That(village.ResidentName("traveler"), Is.EqualTo("주민 2"));
                Assert.That(village.World.CameraMinPixels, Is.EqualTo(-259 * 32f), "the camera stops right of where the hero walks in");
                var player = village.World.arena.Player;

                // The notebook first: three pages turned with Space, then the road comes up from black.
                Assert.That((bool)gui.GetType().GetProperty("NotebookShowing").GetValue(gui), Is.True, State(gui, village));
                Directory.CreateDirectory("../outputs/hub/capture");
                Assert.That(gui.GetType().GetMethod("LessonKey", Private).Invoke(gui, new object[] { "→" }), Is.EqualTo("→"));
                var output = (MonoBehaviour)gui.GetType().GetField("worldOutput", Private).GetValue(gui);
                var viewport = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "VillagePixelViewport");
                viewport.GetType().GetField("enableInBatchMode").SetValue(viewport, true);
                viewport.enabled = true; viewport.SendMessage("LateUpdate"); output.SendMessage("LateUpdate");
                var clip = output.GetComponentsInChildren<UnityEngine.UI.RectMask2D>().Single();
                var clipPosition = clip.rectTransform.anchoredPosition;
                var black = output.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i => i.name == "Letterbox");
                Assert.That(black.color, Is.EqualTo(Color.black), "exposed edges are opaque black");
                foreach (var shake in new[] { new Vector2(4, -4), new Vector2(-4, 4) })
                {
                    output.GetType().GetProperty("ShakePixels").SetValue(output, shake);
                    output.SendMessage("LateUpdate");
                    Assert.That(clip.rectTransform.anchoredPosition, Is.EqualTo(clipPosition), "the black viewport edge stays fixed");
                    Assert.That(clip.GetComponentInChildren<UnityEngine.UI.RawImage>().rectTransform.anchoredPosition, Is.Not.EqualTo(Vector2.zero));
                }
                output.GetType().GetProperty("ShakePixels").SetValue(output, Vector2.zero);
                viewport.enabled = false; output.SendMessage("LateUpdate");
                yield return WaitFor(() => (float)gui.GetType().GetField("notebookClock", Private).GetValue(gui) >= 4.5f, 10);
                for (int page = 0; page < 3; page++)
                {
                    if (page > 0) yield return WaitFor(() =>
                        (float)gui.GetType().GetField("notebookClock", Private).GetValue(gui) -
                        (float)gui.GetType().GetField("notebookPageAt", Private).GetValue(gui) >= 1.1f, 4);
                    yield return Press(Key.Space, .05f); // Complete unread lines before advancing.
                    StartingVillageCaptureTests.Capture(gui, village, "opening-notebook-" + (page + 1));
                    yield return Press(Key.Space, .05f);
                }
                yield return WaitFor(() => !(bool)gui.GetType().GetProperty("NotebookShowing").GetValue(gui), 2);
                Assert.That((bool)gui.GetType().GetProperty("NotebookShowing").GetValue(gui), Is.False, State(gui, village));

                yield return WaitFor(() => Bubble(gui) == "저 마을이구나.", 5);
                Assert.That(Bubble(gui), Is.EqualTo("저 마을이구나."), State(gui, village));
                Assert.That(player.Position.x, Is.InRange(-255.5f, -254.5f), "walked in from off screen");
                Assert.That(player.Abilities, Is.EqualTo(MovementAbilities.None), "the road starts with walking only");
                StartingVillageCaptureTests.Capture(gui, village, "opening-road");
                Assert.That(Speech().gameObject.activeInHierarchy, Is.True, "the hero speaks in the workshop bubble");
                Assert.That(SpeechName(), Is.Empty, "the hero has no name tag");
                yield return Read(gui, village, "저 마을이구나.");
                foreach (var line in new[] { "아직은 멀쩡해 보이는데.", "해 지기 전엔 닿겠다." }) yield return Read(gui, village, line);
                // No lesson stops the game: the strip of moves goes up and the road is free to practise on.
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui), 2);
                Assert.That((bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui), Is.True, State(gui, village));
                Assert.That((string[])gui.GetType().GetProperty("ControlsTexts").GetValue(gui), Is.EqualTo(new[] { "달리기", "점프", "더블 점프", "대시" }));
                Assert.That(Lesson(gui), Is.Null, "no lesson holds the game");
                // Each card plays its move in the hero's own body, keys and name under the picture.
                var pictures = (Sprite[])gui.GetType().GetProperty("ControlsPictures").GetValue(gui);
                Assert.That(pictures.Length, Is.EqualTo(4)); Assert.That(pictures, Has.All.Not.Null, "every card draws its move");
                Assert.That(player.Abilities, Is.EqualTo(MovementAbilities.All), "every move on the cards is open");
                Assert.That(village.Demos.Select(d => d.Text), Is.EqualTo(new[] { "대시로 피하기", "벽타기", "벽차기" }), "the road's demonstrations play from the start");
                yield return WaitFor(() => player.grounded, 1);
                float groundY = player.Position.y, highest = groundY;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < .25f;) { highest = Mathf.Max(highest, player.Position.y); yield return null; }
                Assert.That((bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui), Is.True, "Space does not close the strip");
                StartingVillageCaptureTests.Capture(gui, village, "opening-free-strip");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return WaitFor(() => player.grounded, 2);
                Assert.That(highest, Is.GreaterThan(groundY + .5f), "the hero jumps straight away");
                yield return WaitFor(() => false, 1);
                StartingVillageCaptureTests.Capture(gui, village, "opening-free-strip-middle");
                yield return WaitFor(() => false, .6f);
                StartingVillageCaptureTests.Capture(gui, village, "opening-free-strip-next");

                // Each demonstration loops where it was recorded, the game going on: the wall climb in the climb-v4 art.
                var dodgeDemo = village.Demos[0]; var climbDemo = village.Demos[1];
                player.Position = new Vector2(-180f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return WaitFor(() => climbDemo.WallClip == "wall_contact", 6);
                Assert.That(climbDemo.WallClip, Is.EqualTo("wall_contact"), "the wall ghost climbs");
                var climbArt = village.World.GetComponent<Chapter1TraversalPresentation>().ClimbVisuals;
                yield return WaitFor(() => System.Array.IndexOf(climbArt.humanClimb.sprites, climbDemo.Sprite) >= 0, 2);
                Assert.That(System.Array.IndexOf(climbArt.humanClimb.sprites, climbDemo.Sprite), Is.GreaterThanOrEqualTo(0), "the ghost climbs in the climb-v4 loop");
                yield return WaitFor(() => climbDemo.RingShown, 3);
                Assert.That(climbDemo.RingShown, Is.True, "the stamina ring empties over the climbing ghost");
                StartingVillageCaptureTests.Capture(gui, village, "opening-demo-climb");
                int plays = climbDemo.Plays;
                yield return WaitFor(() => climbDemo.Plays > plays, 6);
                Assert.That(climbDemo.Plays, Is.GreaterThan(plays), "it plays again by itself");

                // The dash ghost runs at an ink drop and dashes through it.
                player.Position = new Vector2(-112f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                // The shot meets the ghost in the middle of its dash (by the track: a batch-mode frame can step over 0.2 s).
                var dodge = dodgeDemo.Track; int dashFrom = System.Array.IndexOf(dodge.clip, "dash"), dashTo = System.Array.LastIndexOf(dodge.clip, "dash");
                float ShotX(int f) => dodge.shotX + dodge.shotSpeed * (f - dodge.shotFrom) / village.Ghosts.fps;
                Assert.That(ShotX(dashFrom), Is.GreaterThan(dodge.x[dashFrom]), "the shot is still ahead when the dash starts");
                Assert.That(ShotX(dashTo), Is.LessThan(dodge.x[dashTo]), "and behind the ghost when it ends");
                Assert.That(dodge.tapped[dashFrom], Is.EqualTo("ShiftLeft"));
                Assert.That(dodgeDemo.ShotShown, Is.False, "the dodge demonstration belongs to its top card, not the live road");
                yield return WaitFor(() => ((string[])gui.GetType().GetProperty("ControlsTexts").GetValue(gui)).Contains("대시 무적"), 1);
                StartingVillageCaptureTests.Capture(gui, village, "opening-demo-dodge");
                // Standing on the dodge's stretch, real ink balls come at the hero and knock it back.
                player.Position = new Vector2(-114f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return WaitFor(() => village.PracticeHits > 0, 5);
                Assert.That(village.PracticeHits, Is.GreaterThan(0), "a practice shot hits the hero standing still");

                // F closes the strip; the demonstrations stay.
                yield return Press(Key.F, .05f);
                yield return WaitFor(() => !(bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui), 1);
                Assert.That((bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui), Is.False, "F closes the strip");
                Assert.That(village.Demos.Count, Is.EqualTo(3));
                StartingVillageCaptureTests.Capture(gui, village, "opening-done");

                // Two bystanders talk by the road; the hero walks up and listens.
                player.Position = new Vector2(-98.5f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return Read(gui, village, "요샌 어딜 가나 스케치화 얘기야.", 6);
                Assert.That(player.Position.x, Is.InRange(-95.2f, -94.2f), "walked up behind the merchant: " + State(gui, village));
                Assert.That(village.Demos.Count, Is.Zero, "the road's demonstrations end where the story goes on");
                yield return WaitFor(() => Bubble(gui) == "윗마을은 며칠째 소식도 없다던데.", 2);
                Assert.That(SpeechName(), Is.EqualTo("주민 2"), "a bystander's bubble carries its name tag");
                StartingVillageCaptureTests.Capture(gui, village, "opening-residents");
                yield return Read(gui, village, "윗마을은 며칠째 소식도 없다던데.");
                yield return Read(gui, village, "조사원 보낸다더니 코빼기도 안 보이고.");
                yield return Read(gui, village, "나 들으라고 하는 소리 같은데... 서두르자.");
                Assert.That(village.ErasureActive, Is.False);
                // In colour the bystanders play their idle.
                var idle = village.ResidentPose("merchant").Value;
                Assert.That(idle.sketch, Is.False);
                yield return WaitFor(() => false, .3f);
                Assert.That(village.ResidentPose("merchant").Value.frame, Is.Not.EqualTo(idle.frame), "a bystander in colour plays its idle");

                player.Position = new Vector2(-88.4f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return null;
                Assert.That(Bubble(gui), Is.Null.Or.Empty, "no extra resident aside after the conversation");

                // A little further on the erasure appears behind them and swallows them while they cry out.
                player.Position = new Vector2(-82.5f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return Read(gui, village, "무슨 소리지..?", 4);
                Assert.That(village.ErasureActive, Is.False, "the front comes only once the hero has turned");
                yield return Read(gui, village, "어.. 색이..!", 4);
                Assert.That(village.ErasureActive, Is.True); Assert.That(village.ErasureChasing, Is.False, "held for the bystanders");
                yield return Read(gui, village, "으악! 이, 이게 뭐야!", 6);
                StartingVillageCaptureTests.Capture(gui, village, "opening-swallowed");
                yield return Read(gui, village, "뛰어야 해!");
                yield return WaitFor(() => Banner(gui) == "뛰기", 10);
                Assert.That(Banner(gui), Is.EqualTo("뛰기"), State(gui, village));
                Assert.That(village.ErasureChasing, Is.True); Assert.That(village.SketchAt(-91), Is.True, "both bystanders are in sketch");
                // Swallowed, they stand still in the pose they had (user 2026-11-13).
                var merchant = village.ResidentPose("merchant").Value; var traveler = village.ResidentPose("traveler").Value;
                Assert.That(merchant.sketch && traveler.sketch, Is.True, "the front has swallowed both bystanders");
                yield return WaitFor(() => false, .8f);
                Assert.That(village.ResidentPose("merchant").Value.frame, Is.EqualTo(merchant.frame), "a sketch bystander stands still");
                Assert.That(village.ResidentPose("traveler").Value.frame, Is.EqualTo(traveler.frame), "a sketch bystander stands still");
                StartingVillageCaptureTests.Capture(gui, village, "opening-chase");

                // Standing still gets the hero caught: the screen goes dark and the chase starts again at its checkpoint.
                yield return WaitFor(() => village.Caught != null, 8);
                Assert.That(village.Caught, Is.EqualTo("erase"), State(gui, village));
                yield return WaitFor(() => village.Caught == null, 4);
                Assert.That(player.Position.x, Is.InRange(-80.2f, -79.8f), "back at the checkpoint: " + State(gui, village));
                Assert.That(village.ErasureLeft, Is.LessThan(-85f), "the front starts again 7 units behind");

                // At the well a second front comes in from the right and both close in on the hero.
                // The hero dashes in: the beat locks input at x 12 and the dash carries it on towards the helper's spot.
                player.Position = new Vector2(8f, village.WalkingLine); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return WaitFor(() => player.Position.x > 10.6f, 3);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow, Key.LeftShift)); yield return WaitFor(() => false, .12f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return WaitFor(() => false, .6f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                float wellX = player.Position.x;
                Assert.That(wellX, Is.GreaterThan(13.5f), "the dash slid the hero past where a walk stops");
                yield return Read(gui, village, "어, 저쪽에서도..!", 4);
                StartingVillageCaptureTests.Capture(gui, village, "opening-erasure");
                var music = Deviant.Music.BackgroundMusicPlayer.Ensure();
                Assert.That(music.RequestedCue, Is.EqualTo("opening_erase"), "the chase music plays up to the meeting");
                int stings = music.DeathStings;
                yield return WaitFor(() => village.ErasureMet, 5);
                Assert.That(village.ErasureMet, Is.True, State(gui, village));
                Assert.That(Mathf.Abs(player.Position.x - wellX), Is.LessThan(.7f), "the fronts meet where the hero stands");

                // The hero turns to sketch where it stands, from the hands, while it speaks.
                yield return WaitFor(() => Bubble(gui) == "손이, 왜 이래..", 3);
                // Turning to sketch is not a death: no death sting, and the chase music fades out slowly instead of cutting.
                Assert.That(music.RequestedCue, Is.Null, "the chase music is left at the meeting");
                Assert.That(music.DeathStings, Is.EqualTo(stings), "no death sting at the well");
                Assert.That(music.DeathLatched, Is.False);
                var chase = music.Voices.FirstOrDefault(s => s.clip && s.clip.name == "final-chase" && s.isPlaying);
                Assert.That(chase, Is.Not.Null, "the chase music is still heard as the hero starts to speak");
                Assert.That(music.GainOf(chase), Is.GreaterThan(.1f), "still fading, not cut");
                Assert.That(village.HeroSketching, Is.True); Assert.That(player.visual.forceRenderingOff, Is.True, "the sketch view draws the hero");
                yield return Read(gui, village, "손이, 왜 이래..");
                StartingVillageCaptureTests.Capture(gui, village, "opening-sketch");
                yield return Read(gui, village, "여긴 예정된 마을이 아니었는데… 왜…");
                yield return WaitFor(() => village.HelperShown, 6);
                Assert.That(village.HeroSketchReach, Is.EqualTo(1), "the sketch has covered the hero first");
                Assert.That(village.HelperTop.x - player.Position.x, Is.GreaterThan(4.4f), "the helper stands clear of the hero, not behind it");
                yield return WaitFor(() => Bubble(gui) == "이런, 또 당해 버렸네. 얼마 안 남았는데..", 5);
                Assert.That(Bubble(gui), Is.EqualTo("이런, 또 당해 버렸네. 얼마 안 남았는데.."), State(gui, village));
                yield return WaitFor(() => false, .8f);
                StartingVillageCaptureTests.Capture(gui, village, "opening-helper");
                Assert.That(SpeechName(), Is.EqualTo("의문의 조력자"), "the helper's bubble carries its name tag");
                yield return Read(gui, village, "이런, 또 당해 버렸네. 얼마 안 남았는데..");
                yield return Read(gui, village, "남은 잉크를 너한테 전부 줄게. 나는 여기까지야.");
                yield return Read(gui, village, "나머지는 부탁해.");
                yield return WaitFor(() => village.HeroReviving, 2);
                Assert.That(village.HeroBody, Is.EqualTo(village.VillageBody));
                yield return WaitFor(() => Bubble(gui) == "나, 살았어..?", 4);
                Assert.That(Bubble(gui), Is.EqualTo("나, 살았어..?"), "the hero speaks once the colour is back: " + State(gui, village));
                Assert.That(village.HeroSketching, Is.False); Assert.That(village.HelperShown, Is.False, "the helper has sunk in");
                yield return Read(gui, village, "나, 살았어..?");
                yield return Read(gui, village, "잉크..가 된건가?");
                yield return Read(gui, village, "일단 마을 밖으로 나가 보자.");

                // Part 4 is the village itself: the road and the erasure are gone and the hero walks it bare-handed.
                yield return WaitFor(() => Banner(gui) == "이정표로 가기", 3);
                Assert.That(Banner(gui), Is.EqualTo("이정표로 가기"), State(gui, village));
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.True, "the opening goes on in the village");
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"), Is.False, "seen only once the hero departs");
                Assert.That(village.Preview, Is.True); Assert.That(village.StationsSuppressed, Is.False); Assert.That(village.ErasureActive, Is.False);
                Assert.That(village.HelperShown, Is.False); Assert.That(player.visual.forceRenderingOff, Is.False);
                Assert.That(village.HeroBody, Is.EqualTo(village.VillageBody), "bare-handed in the village");
                Assert.That(player.Abilities, Is.EqualTo(MovementAbilities.All));
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False, "the opening returns its input lock");
                yield return null;
                Assert.That(Solids(village, "handcart") + Solids(village, "wall") + Solids(village, "fence") + Solids(village, "floor"), Is.Zero, "the road is gone");
                Assert.That(village.World.CameraMinPixels, Is.Zero);
                // The farmhouse update marker invites optional reading; only the sign guides progress.
                Assert.That(village.GuideStation, Is.EqualTo(StartingVillageWorld.DepartureSign));
                Assert.That((bool)gui.GetType().GetProperty("RecordHasUpdate").GetValue(gui), Is.True);
                var updates = (RectTransform)gui.GetType().GetField("stationUpdates", Private).GetValue(gui);
                yield return null;
                Assert.That(updates.Find("새 소식 " + StartingVillageWorld.RecordDesk).gameObject.activeInHierarchy, Is.True);
                StartingVillageCaptureTests.Capture(gui, village, "opening-awake");
                player.Position = new Vector2(29.7f, village.WalkingLine);
                player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return WaitFor(() => false, 1);
                Assert.That(village.BroomStanding, Is.False);
                Assert.That(village.PunchUnlocked, Is.False);
                Assert.That(Bubble(gui), Is.Null);
                Assert.That(Banner(gui), Is.EqualTo("이정표로 가기"));

                player.Position = village.Find(StartingVillageWorld.RecordDesk).Position;
                player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return WaitFor(() => false, .4f);
                yield return Press(Key.F, .05f);
                yield return WaitFor(() => HubOpen(gui) != null, 1);
                Assert.That(HubOpen(gui), Is.EqualTo("record"), State(gui, village));
                yield return WaitFor(() => false, .3f);
                Assert.That((bool)gui.GetType().GetProperty("RecordHasUpdate").GetValue(gui), Is.False, "opening the list selects story 1, which reads it and clears the !");
                gui.GetType().GetMethod("OpenRecordStory", Private).Invoke(gui, new object[] { 0 });
                yield return Press(Key.Escape, .05f);
                yield return Press(Key.Escape, .05f);
                yield return WaitFor(() => HubOpen(gui) == null, 1);
                Assert.That(Banner(gui), Is.EqualTo("이정표로 가기"));
                Assert.That(village.GuideStation, Is.EqualTo(StartingVillageWorld.DepartureSign));

                player.Position = new Vector2(35.3f, village.WalkingLine);
                player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return WaitFor(() => Banner(gui) == "마을 밖으로 출발하기", 3);
                Assert.That(HubOpen(gui), Is.Null);
                StartingVillageCaptureTests.Capture(gui, village, "opening-sign");
                player.Position = village.Find(StartingVillageWorld.DepartureSign).Position; Physics2D.SyncTransforms();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Press(Key.F, .05f);
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.False, State(gui, village));
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"), Is.True);
                Assert.That(village.Departing, Is.True, "F at the sign starts the unarmed departure");
                Assert.That((bool)gui.GetType().GetField("storyActive", Private).GetValue(gui), Is.False, "no still scene follows");
                Assert.That(village.World.arena.Controls.Current.SimulationPaused, Is.False);
                StartingVillageCaptureTests.Capture(gui, village, "opening-departure");
                yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == Chapter1RunHost.ChapterScene, 30);
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, Is.EqualTo(Chapter1RunHost.ChapterScene),
                    village ? "departure stalled: " + State(gui, village) : "the chapter must load after the opening");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                Chapter1ProfileHost.UseStore(null);
            }
            yield return new ExitPlayMode();
        }

        // Holding Esc on the learning road (here: still on the notebook) skips to where the chase starts: the hero stands
        // past the bystanders with the front held behind it, every move open and 「뛰기」 up; the front then gives chase.
        [UnityTest] public IEnumerator HoldingEscOnTheRoadStartsTheChaseWhereRunShows()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                yield return null; yield return null;
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
                yield return null; yield return null;
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.True);
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("NotebookShowing").GetValue(gui), 3);
                Assert.That(village.Demos.Count, Is.Zero, "the notebook comes before the road");
                yield return Press(Key.Escape, .45f);
                Assert.That((bool)gui.GetType().GetField("hubPaused", Private).GetValue(gui), Is.False,
                    "releasing an attempted hold-skip only cancels the skip, without opening pause");
                yield return Press(Key.Escape, .05f);
                Assert.That((bool)gui.GetType().GetField("hubPaused", Private).GetValue(gui), Is.True, "a short tap pauses");
                yield return Press(Key.Escape, .05f);
                Assert.That((bool)gui.GetType().GetField("hubPaused", Private).GetValue(gui), Is.False, "another tap resumes");
                yield return Press(Key.Escape, 1.3f);
                yield return WaitFor(() => Banner(gui) == "뛰기", 3);
                var player = village.World.arena.Player;
                Assert.That(Banner(gui), Is.EqualTo("뛰기"), State(gui, village));
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.True, "the opening goes on from the chase");
                Assert.That((bool)gui.GetType().GetProperty("NotebookShowing").GetValue(gui), Is.False);
                Assert.That((bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui), Is.False);
                Assert.That(player.Position.x, Is.InRange(-84f, -83f), State(gui, village));
                Assert.That(player.Abilities, Is.EqualTo(MovementAbilities.All));
                Assert.That(village.ErasureActive, Is.True); Assert.That(village.SketchAt(-91), Is.True, "the bystanders are already swallowed");
                Assert.That(village.HeroBody, Is.EqualTo(village.OpeningBody), "still the human on the road");
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False, "the hero can run");
                StartingVillageCaptureTests.Capture(gui, village, "opening-skip-chase");
                yield return WaitFor(() => village.ErasureChasing, 2);
                Assert.That(village.ErasureChasing, Is.True, "the front gives chase after its grace");
                // Holding Esc again now skips to the well, as before.
                yield return Press(Key.Escape, 1.3f);
                yield return WaitFor(() => !(bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), 2);
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.False, State(gui, village));
                Assert.That(village.ErasureActive, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                Chapter1ProfileHost.UseStore(null);
            }
            yield return new ExitPlayMode();
        }
    }
}
