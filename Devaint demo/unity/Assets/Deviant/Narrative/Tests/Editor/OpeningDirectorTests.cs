using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Narrative.Opening;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Narrative.Tests
{
    // The playable opening's beat runner against a scripted stage, and the real beat file (data/story/opening_playable.json).
    public sealed class OpeningDirectorTests
    {
        sealed class Stage : IOpeningStage
        {
            public float PlayerX { get; set; } = 18;
            public int JumpsUsed { get; set; }
            public bool Dashing { get; set; }
            public bool Walking { get; set; }
            public bool BubbleWaiting { get; set; }
            public bool Animating { get; set; }
            public string Banner; public readonly List<string> Bubbles = new List<string>(), Log = new List<string>();
            public string Colour = "profile"; public bool Interactables = true;
            void IOpeningStage.Bubble(string speaker, string text, bool advance) { Bubbles.Add(text); BubbleWaiting = advance; Log.Add(speaker + ": " + text); }
            void IOpeningStage.Banner(string[] keys, string text) { Assert.That(Banner, Is.Null, "one banner at a time"); Banner = text; Log.Add("banner " + text); }
            void IOpeningStage.BannerDone() { Log.Add("done " + Banner); Banner = null; }
            void IOpeningStage.Village(string color, bool interactables) { Colour = color; Interactables = interactables; }
            void IOpeningStage.Walk(float fromX, float toX) { if (!float.IsNaN(fromX)) PlayerX = fromX; Walking = true; Log.Add(float.IsNaN(fromX) ? $"walk to {toX}" : $"walk {fromX}->{toX}"); }
            void IOpeningStage.Sfx(string id) => Log.Add("sfx " + id);
            void IOpeningStage.Shake(float seconds) => Log.Add("shake");
            void IOpeningStage.Fear(float seconds, float interval) => Log.Add("fear");
            void IOpeningStage.LockInput(bool locked) => Log.Add(locked ? "lock" : "unlock");
            void IOpeningStage.Checkpoint(string value) => Log.Add("checkpoint " + value);
            void IOpeningStage.Flag(string name) => Log.Add("flag " + name);
            public float ErasureLeft { get; set; } = float.NaN;
            void IOpeningStage.ErasureChase(float behind, float holdAt, float slowFromX, float rollIn) => Log.Add($"erasureChase {behind} hold {holdAt}");
            void IOpeningStage.ErasureGo() => Log.Add("erasureGo");
            void IOpeningStage.ErasureRelease(float graceSeconds) => Log.Add($"erasureRelease {graceSeconds}");
            void IOpeningStage.ErasureClose(float rightAhead, float rightSpeed, float leftSpeed) => Log.Add($"erasureClose {rightAhead}");
            void IOpeningStage.ErasureMeet(float seconds) => Log.Add($"erasureMeet {seconds}");
            void IOpeningStage.HeroSketch(string mode) { Animating = true; Log.Add("heroSketch " + mode); }
            void IOpeningStage.Awaken() => Log.Add("awaken");
            void IOpeningStage.OpenStation(string id) => Log.Add("open " + id);
            void IOpeningStage.Artisan(string mode, float x, float heroOffset, int face, string busy) { if (mode == "enter" || mode == "walk" || mode == "leave" || mode == "inside") Animating = true; Log.Add("artisan " + mode + (busy != null ? " " + busy : "")); }
            void IOpeningStage.Pan(string station, float heroOffset, int face) { Animating = true; Log.Add("pan " + station); }
            void IOpeningStage.UnlockStation(string station, bool sparkleOnly) => Log.Add((sparkleOnly ? "sparkle " : "unlock ") + station);
            void IOpeningStage.Bloom(int piece) => Log.Add("bloom " + piece);
            void IOpeningStage.Palette() => Log.Add("palette");
            void IOpeningStage.Thud(float volume) => Log.Add("thud");
            void IOpeningStage.Drain(string actor, float seconds) { Animating = true; Log.Add("drain " + actor); }
            void IOpeningStage.Clip(string actor, string clip, float x, bool reverse, bool holdLast) { Animating = true; Log.Add($"clip {actor} {clip}{(float.IsNaN(x) ? "" : " at " + x)}{(reverse ? " reversed" : "")}"); }
            void IOpeningStage.Spawn(string actor, string sprite, float x, string behind, string effect) { Animating = true; Log.Add($"spawn {actor} {x}"); }
            void IOpeningStage.Despawn(string actor) => Log.Add("despawn " + actor);
            void IOpeningStage.Abilities(string[] names, bool replace) => Log.Add((replace ? "abilities " : "grant ") + string.Join(",", names));
            public bool LessonOpen { get; set; }
            public string Lesson;
            void IOpeningStage.Lesson(string[] keys, string text, string ghost) { Lesson = text; LessonOpen = true; Log.Add("lesson " + text + (ghost != null ? " ghost " + ghost : "")); }
            public float PlayerY { get; set; }
            public bool Grounded { get; set; } = true;
            public bool NotebookOpen { get; set; }
            public bool BroomStanding { get; set; }
            void IOpeningStage.Notebook() { NotebookOpen = true; Log.Add("notebook"); }
            void IOpeningStage.Broom(string mode, float x) { if (mode == "appear") BroomStanding = true; Log.Add("broom " + mode); }
            void IOpeningStage.Mark(string station) => Log.Add("mark " + station);
            void IOpeningStage.Guide(string station) => Log.Add("guide " + station);
            void IOpeningStage.InkFlicker() => Log.Add("inkFlicker");
            void IOpeningStage.Face(int face) => Log.Add("face " + face);
            void IOpeningStage.Focus(float x, float heroOffset) => Log.Add(float.IsNaN(x) && float.IsNaN(heroOffset) ? "focus hero" : $"focus {x} {heroOffset}");
            void IOpeningStage.Zoom(float zoom) => Log.Add("zoom " + zoom);
            void IOpeningStage.Exclaim() => Log.Add("exclaim");
            void IOpeningStage.HeroExclaim() => Log.Add("exclaim hero");
            void IOpeningStage.Pigment() => Log.Add("pigment");
            public string[] Controls;
            void IOpeningStage.Controls(OpeningControlItem[] items) { Controls = items?.Select(i => i.text).ToArray(); Log.Add(items == null ? "controls hide" : "controls " + string.Join(",", Controls)); }
            void IOpeningStage.Demo(string ghost, string[] keys, string text) => Log.Add(ghost == null ? "demo clear" : "demo " + ghost + " " + text);
        }

        // Plays everything that waits on the stage at once: animations end, advance bubbles are read, waits run out.
        static void Settle(Stage stage, OpeningDirector director, int ticks)
        {
            for (int i = 0; i < ticks && director.Outcome == OpeningOutcome.Running; i++)
            { stage.Animating = false; stage.BubbleWaiting = false; director.Tick(2); }
        }

        static string RepositoryFile(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../..", path));
        static OpeningScript RealScript() => JsonUtility.FromJson<OpeningScript>(File.ReadAllText(RepositoryFile("data/story/opening_playable.json")));

        static OpeningTrigger Trigger(string type, string beat = null, float lte = float.NaN) => new OpeningTrigger { type = type, beat = beat, lte = lte };
        static OpeningAction Banner(string text, OpeningTrigger done) => new OpeningAction { type = "banner", text = text, keys = new string[0], doneWhen = done };
        static OpeningScript Script(params OpeningBeat[] beats) => new OpeningScript { schemaVersion = 1, id = "fixture", beats = beats };
        static OpeningBeat Beat(string id, int part, OpeningTrigger trigger, params OpeningAction[] actions) => new OpeningBeat { id = id, part = part, trigger = trigger, actions = actions };

        [Test] public void TheBeatFileIsValidAndKeepsReachLimitsOpen()
        {
            var script = RealScript();
            Assert.That(OpeningScriptRules.Validate(script), Is.Empty);
            var meet = script.beats.First(b => b.id == "road-meet").trigger;
            Assert.That(meet.gte, Is.EqualTo(-99)); Assert.That(float.IsNaN(meet.lte), Is.True, "a missing lte stays open");
            Assert.That(script.course.leftX, Is.EqualTo(-263)); Assert.That(script.course.cameraLeftX, Is.EqualTo(-259), "the hero walks in from off screen");
            Assert.That(script.course.pieces.Count(p => p.kind == "pit"), Is.Zero, "no pits (2026-11-04)");
            Assert.That(script.course.pieces.Count(p => p.kind == "wall" || p.kind == "dodgeGate"), Is.EqualTo(15), "9 on the learning road, 6 in the chase");
            Assert.That(script.course.pieces.Count(p => p.kind == "fence"), Is.EqualTo(5));
            Assert.That(script.course.pieces.Count(p => p.kind == "wall" && p.bottom > 0), Is.EqualTo(1), "one hanging wall for the wall kick");
            Assert.That(script.notebook.pages.Length, Is.EqualTo(3));
            Assert.That(script.beats.Where(b => b.part == 1).SelectMany(b => b.actions).Select(a => a.type).Distinct(),
                Is.SubsetOf(new[] { "bubble", "village", "autoWalk", "abilities", "controls", "demo", "lockInput", "unlockInput", "notebook", "wait" }));
            Assert.That(script.skip.beat, Is.EqualTo("chase-from-skip"));
            // Every lesson is a recorded ghost.
            foreach (var lesson in script.beats.SelectMany(b => b.actions).Where(a => a.type == "lesson"))
                Assert.That(lesson.ghost, Is.Not.Empty, lesson.text);
            // No middle dots in what is said or shown (user rule 2026-11-04).
            foreach (var text in script.beats.SelectMany(b => b.actions).Select(a => a.text).Concat(script.notebook.pages.SelectMany(pg => pg.lines)).Where(s => s != null))
                Assert.That(text, Does.Not.Contain("·"), text);
        }

        [Test] public void WallsNeedAHeightAndPitsAWidth()
        {
            var script = Script(Beat("a", 1, Trigger("start"), new OpeningAction { type = "lesson", keys = new string[0], text = "" }));
            script.course = new OpeningCourse { leftX = -40, pieces = new[] {
                new OpeningCoursePiece { kind = "wall", x = -30, width = 2, top = 0 },
                new OpeningCoursePiece { kind = "pit", x = -20, width = 0 },
                new OpeningCoursePiece { kind = "wall", x = -10, width = 2, top = 3 },
                new OpeningCoursePiece { kind = "pit", x = -6, width = 3 } } };
            var errors = OpeningScriptRules.Validate(script);
            Assert.That(errors.Count(e => e.Contains("wall needs width 1+")), Is.EqualTo(1));
            Assert.That(errors.Count(e => e.Contains("pit needs width 1+")), Is.EqualTo(1));
            Assert.That(errors, Has.Some.Contains("empty text"), "a lesson card says what the keys do");
        }

        [Test] public void AbilitiesNeedExactlyOneOfSetOrGrantWithKnownNames()
        {
            OpeningScript One(OpeningAction a) => new OpeningScript { schemaVersion = 1, id = "t",
                beats = new[] { new OpeningBeat { id = "b", part = 1, trigger = Trigger("start"), actions = new[] { a } } } };
            Assert.That(OpeningScriptRules.Validate(One(new OpeningAction { type = "abilities", set = new string[0] })), Is.Empty, "set [] = walking only");
            Assert.That(OpeningScriptRules.Validate(One(new OpeningAction { type = "abilities", grant = new[] { "jump", "airJump", "wall", "dash" } })), Is.Empty);
            Assert.That(OpeningScriptRules.Validate(One(new OpeningAction { type = "abilities" })), Has.Some.Contains("exactly one of set or grant"));
            Assert.That(OpeningScriptRules.Validate(One(new OpeningAction { type = "abilities", grant = new[] { "fly" } })), Has.Some.Contains("unknown ability fly"));
        }

        [Test] public void PartOneTeachesMovementInOrderAndHandsOffAtTheWell()
        {
            var stage = new Stage(); var director = new OpeningDirector(RealScript(), stage, 1);
            director.Tick(.02f);
            Assert.That(stage.Log.First(), Is.EqualTo("abilities "), "the road starts with walking only");
            Assert.That(stage.Colour, Is.EqualTo("allColor")); Assert.That(stage.Interactables, Is.False);
            Assert.That(stage.PlayerX, Is.EqualTo(-260.5f), "the hero is put off screen left of the road's camera stop before anything else");
            Assert.That(director.Outcome, Is.EqualTo(OpeningOutcome.Running), "the lobby spawn (x 18) does not count as reaching the village");
            stage.Walking = false; director.Tick(.02f);
            Assert.That(stage.Log.Last(), Is.EqualTo("notebook"), "then the notebook");
            director.Tick(.02f); Assert.That(stage.Log, Has.No.Member("lock"), "waits until the notebook is closed");
            stage.NotebookOpen = false; director.Tick(.02f); director.Tick(.6f); director.Tick(.02f);
            Assert.That(stage.Log, Has.Member("walk to -255"), "walks in under the bars");
            stage.PlayerX = -255; stage.Walking = false; director.Tick(.02f); director.Tick(.3f); director.Tick(.02f);
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("저 마을이구나."));
            Assert.That(stage.Log, Has.Member("lock"), "the arrival line holds the player");
            // The hero goes on talking to the road before it is theirs.
            foreach (var line in new[] { "아직은 멀쩡해 보이는데.", "해 지기 전엔 닿겠다." })
            {
                stage.BubbleWaiting = false; director.Tick(.02f);
                Assert.That(stage.Bubbles.Last(), Is.EqualTo(line)); Assert.That(stage.Lesson, Is.Null);
            }
            // Then the road is free: no lesson stops it, the strip of moves goes up with those moves open, and the two
            // demonstrations start playing on the road (user request 2026-11-10).
            stage.BubbleWaiting = false; director.Tick(.02f);
            Assert.That(stage.Lesson, Is.Null);
            Assert.That(stage.Log, Has.Member("grant jump,airJump,wall,dash"), "every move on the cards opens with them");
            Assert.That(stage.Controls, Is.EqualTo(new[] { "달리기", "점프", "더블 점프", "대시" }));
            Assert.That(stage.Log, Has.Member("demo climb 벽타기")); Assert.That(stage.Log, Has.Member("demo dodge 대시로 피하기"));
            Assert.That(stage.Log.IndexOf("unlock"), Is.LessThan(stage.Log.FindIndex(l => l.StartsWith("controls 달리기"))));
            Assert.That(director.Finished, Has.Member("prologue"));

            Assert.That(stage.Log.Count(l => l.StartsWith("lesson")), Is.Zero, "the road has no lessons");
            Assert.That(director.Outcome, Is.EqualTo(OpeningOutcome.Running));
            stage.PlayerX = -99; stage.PlayerY = 0; director.Tick(.02f);
            Assert.That(director.Outcome, Is.EqualTo(OpeningOutcome.Handoff)); Assert.That(director.HandoffPart, Is.EqualTo(2));
            Assert.That(stage.Log, Has.No.Member("walk to -94.7"), "a handed-off part runs none of its actions");
        }

        // Holding Esc on the road (here on the notebook) jumps to the chase beat: the strip and the demonstrations go, the
        // hero is put past the bystanders, the front appears held behind it and is released, and 「뛰기」 shows.
        [Test] public void JumpingToTheChaseRunsItsBeatAndKeepsTheRestOfThePart()
        {
            var stage = new Stage(); var script = RealScript(); var director = new OpeningDirector(script, stage, 4);
            director.Tick(.02f); stage.Walking = false; director.Tick(.02f);
            Assert.That(stage.NotebookOpen, Is.True);
            Assert.That(director.JumpTo("missing"), Is.False);
            Assert.That(director.JumpTo(script.skip.beat), Is.True);
            stage.Log.Clear(); director.Tick(.02f);
            Assert.That(director.Part, Is.EqualTo(2));
            Assert.That(stage.Log.Take(4), Is.EqualTo(new[] { "controls hide", "demo clear", "abilities jump,airJump,wall,dash", "walk -83.5->-83.5" }));
            stage.Walking = false; director.Tick(.02f);
            Assert.That(stage.Log, Has.Member("erasureChase 2.45 hold -85.95")); Assert.That(stage.Log, Has.Member("erasureRelease 1.2"));
            Assert.That(stage.Banner, Is.EqualTo("뛰기"));
            Assert.That(stage.Log, Has.No.Member("walk to -94.7"), "the bystanders' talk is passed");
            Assert.That(director.Started("erasure-begin") && director.Finished.Contains("erasure-begin"), Is.True);
            // The rest of the chase plays as usual.
            stage.PlayerX = -74; director.Tick(.02f);
            Assert.That(stage.Banner, Is.Null);
            stage.PlayerX = -13; director.Tick(.02f);
            Assert.That(stage.Banner, Is.Null, "the chase has no extra well banner");
            Assert.That(director.JumpTo(script.skip.beat), Is.True, "the director itself does not refuse; the GUI decides when a skip may jump");
        }

        [Test] public void TheSkipBeatMustStartOnlyByASkip()
        {
            var script = Script(Beat("a", 1, Trigger("start"), new OpeningAction { type = "wait", seconds = 1 }));
            script.skip = new OpeningSkip { beat = "a" };
            Assert.That(OpeningScriptRules.Validate(script), Has.Some.Contains("skip: beat a"));
            script.beats[0].trigger = Trigger("skip");
            Assert.That(OpeningScriptRules.Validate(script), Is.Empty);
        }

        [Test] public void OneBannerAtATimeAndInputBeforeABannerDoesNotCount()
        {
            var stage = new Stage { PlayerX = 50 };
            var director = new OpeningDirector(Script(
                Beat("a", 1, Trigger("start"), Banner("first", new OpeningTrigger { type = "inputDone", action = "jump" })),
                Beat("b", 1, Trigger("start"), Banner("second", new OpeningTrigger { type = "inputDone", action = "dash" }))), stage, 1);
            director.Tick(.02f);
            Assert.That(stage.Banner, Is.EqualTo("first"));
            stage.Dashing = true; director.Tick(.02f); stage.Dashing = false; director.Tick(.02f);
            Assert.That(stage.Banner, Is.EqualTo("first"), "the queued banner does not take a dash made while it waited");
            stage.JumpsUsed = 1; director.Tick(.02f);
            Assert.That(stage.Banner, Is.EqualTo("second"));
            stage.Dashing = true; director.Tick(.02f);
            Assert.That(stage.Banner, Is.Null);
            Assert.That(director.Finished, Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test] public void TheBystandersTalkThenTheErasureSwallowsThemAndChasesTheHeroToTheWell()
        {
            var stage = new Stage(); var director = new OpeningDirector(RealScript(), stage, 3);
            director.Tick(.02f); stage.Walking = false; director.Tick(.02f); stage.NotebookOpen = false; director.Tick(.02f);
            for (int i = 0; i < 10; i++) { stage.Walking = false; stage.BubbleWaiting = false; director.Tick(.5f); }
            // Straight to the bystanders past the wall: part 2 drops the road's beats and takes the strip and ghosts down.
            stage.PlayerX = -99; director.Tick(.02f);
            Assert.That(director.Part, Is.EqualTo(2)); Assert.That(director.Finished, Has.No.Member("road-meet"));
            Assert.That(stage.Log, Has.Member("controls hide")); Assert.That(stage.Log, Has.Member("demo clear"));
            Assert.That(stage.Log, Has.Member("walk to -94.7"), "the hero walks up to them from where it stands");
            stage.Walking = false; stage.PlayerX = -94.7f;
            for (int i = 0; i < 8; i++) { stage.BubbleWaiting = false; director.Tick(.2f); }
            Assert.That(stage.Log.SkipWhile(l => !l.StartsWith("merchant")).Where(l => l.Contains(": ")).Take(4), Is.EqualTo(new[] {
                "merchant: 요샌 어딜 가나 스케치화 얘기야.", "traveler: 윗마을은 며칠째 소식도 없다던데.",
                "merchant: 조사원 보낸다더니 코빼기도 안 보이고.", "hero: 나 들으라고 하는 소리 같은데... 서두르자." }));
            Assert.That(director.Finished, Has.Member("road-meet"));
            Assert.That(stage.Log, Has.No.Member("checkpoint erasure"), "the erasure waits until the hero walks on");

            // The front appears behind and stays still until the hero has seen it; it swallows the bystanders while they cry
            // out (the beat waits on the front, not on time) and is released to chase only once they are covered.
            // First a double thud under the bars and the hero wonders; it turns, and only then the front sweeps in.
            stage.PlayerX = -83; director.Tick(.02f);
            for (int i = 0; i < 40 && stage.Bubbles.LastOrDefault() != "무슨 소리지..?"; i++) director.Tick(.1f);
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("무슨 소리지..?"));
            Assert.That(stage.Log.Count(l => l == "thud"), Is.EqualTo(2), "쿠쿵: two thuds");
            Assert.That(stage.Log, Has.No.Member("erasureChase 16.5 hold -85.95"), "nothing to see before the hero turns");
            stage.BubbleWaiting = false;
            for (int i = 0; i < 40 && stage.Bubbles.LastOrDefault() != "어.. 색이..!"; i++) director.Tick(.1f);
            Assert.That(stage.Log, Has.Member("erasureChase 16.5 hold -85.95"));
            Assert.That(stage.Log.LastIndexOf("lock"), Is.LessThan(stage.Log.IndexOf("erasureChase 16.5 hold -85.95")), "the hero watches it come");
            Assert.That(stage.Log.LastIndexOf("face -1"), Is.LessThan(stage.Log.IndexOf("erasureChase 16.5 hold -85.95")), "turns round first");
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("어.. 색이..!"));
            Assert.That(stage.Log, Has.No.Member("erasureGo"), "still until the line is read");
            stage.BubbleWaiting = false; director.Tick(.02f);
            Assert.That(stage.Log, Has.Member("erasureGo"));
            director.Tick(.02f); Assert.That(stage.Bubbles.Last(), Is.EqualTo("어.. 색이..!"), "waits for the front to reach the bystanders");
            stage.ErasureLeft = -93.9f; director.Tick(.02f);
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("으악! 이, 이게 뭐야!"));
            stage.BubbleWaiting = false; director.Tick(.02f);
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("뛰어야 해!"));
            stage.BubbleWaiting = false; director.Tick(.02f);
            Assert.That(stage.Log, Has.No.Member("erasureRelease 1.2"), "held until both are covered");
            stage.ErasureLeft = -85.95f; director.Tick(.02f);
            Assert.That(stage.Log.Skip(stage.Log.IndexOf("erasureGo")), Has.Member("unlock"));
            Assert.That(stage.Log, Has.Member("erasureRelease 1.2"));
            Assert.That(stage.Banner, Is.EqualTo("뛰기"));
            stage.PlayerX = -74; director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.Banner, Is.Null);
            stage.PlayerX = -13; director.Tick(.02f);
            Assert.That(stage.Banner, Is.Null, "the chase has no extra well banner");
            stage.PlayerX = 1; director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.Banner, Is.Null);

            // At the well a second front comes in; both close on the hero after the line.
            stage.PlayerX = 12; director.Tick(.02f);
            Assert.That(stage.Log, Has.Member("erasureClose 17"));
            for (int i = 0; i < 30 && stage.Bubbles.LastOrDefault() != "어, 저쪽에서도..!"; i++) director.Tick(.1f);
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("어, 저쪽에서도..!"));
            Assert.That(stage.Log.IndexOf("exclaim hero"), Is.LessThan(stage.Log.IndexOf("hero: 어, 저쪽에서도..!")), "a 「!」 over the hero first");
            stage.BubbleWaiting = false; director.Tick(.02f);
            Assert.That(stage.Log, Has.Member("erasureMeet 2.2"));
            Assert.That(stage.Log.IndexOf("fear"), Is.LessThan(stage.Log.IndexOf("hero: 어, 저쪽에서도..!")), "the hero looks round while it speaks");

            // The hero does not fall: it turns to sketch from the hands while it speaks, and the helper comes only once the
            // sketch has covered it.
            director.Notify("erasureMet"); director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.Log, Has.Member("heroSketch hands"));
            director.Tick(1); director.Tick(.02f);
            Assert.That(stage.Bubbles.Last(), Is.EqualTo("손이, 왜 이래.."));
            stage.BubbleWaiting = false; director.Tick(.02f); stage.BubbleWaiting = false; director.Tick(.02f); director.Tick(2);
            Assert.That(stage.Log.Any(l => l.StartsWith("spawn helper")), Is.False, "waits for the sketch to cover the hero");
            Settle(stage, director, 40);
            Assert.That(director.Outcome, Is.EqualTo(OpeningOutcome.Handoff)); Assert.That(director.HandoffPart, Is.EqualTo(4));
            var order = new[] { "heroSketch hands", "hero: 여긴 예정된 마을이 아니었는데… 왜…", "spawn helper 17", "helper: 이런, 또 당해 버렸네. 얼마 안 남았는데..",
                "helper: 나머지는 부탁해.", "clip helper leap", "heroSketch revive", "despawn helper", "hero: 나, 살았어..?", "hero: 일단 마을 밖으로 나가 보자." };
            Assert.That(order.Select(entry => stage.Log.IndexOf(entry)), Is.Ordered.And.All.GreaterThanOrEqualTo(0), string.Join(" | ", stage.Log));
            Assert.That(stage.Log.Count(l => l.StartsWith("helper: ")), Is.EqualTo(3), "the helper says three lines, each read with a key");
        }

        [Test] public void TheVillagePartOffersAnOptionalRecordAndGuidesDepartureWithoutCombat()
        {
            var script = RealScript();
            script.beats = script.beats.Where(b => b.part == 4).ToArray();
            script.beats[0].trigger = Trigger("start"); script.skip = null;
            var stage = new Stage { PlayerX = 12.5f };
            var director = new OpeningDirector(script, stage, 4);
            director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.Log.Take(2), Is.EqualTo(new[] { "checkpoint awakened", "awaken" }));
            Assert.That(stage.Colour, Is.EqualTo("allSketch"));
            Assert.That(stage.Interactables, Is.True);
            Assert.That(stage.Banner, Is.EqualTo("이정표로 가기"));
            Assert.That(stage.Log, Has.Member("guide departure_sign"));
            Assert.That(stage.Log, Has.No.Member("guide record_desk"));
            Assert.That(stage.Log.Any(l => l.StartsWith("broom ")), Is.False);

            // Reading is optional and does not consume departure guidance.
            director.Interacted("record_desk"); director.Tick(.02f);
            director.ScreenClosed("record_desk"); director.Tick(.02f);
            Assert.That(stage.Banner, Is.EqualTo("이정표로 가기"));
            stage.PlayerX = 29.7f; director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.BroomStanding, Is.False);
            Assert.That(stage.Log.Any(l => l.StartsWith("broom ")), Is.False);
            Assert.That(stage.Log, Has.No.Member("lock"));

            stage.PlayerX = 35.2f; director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.Banner, Is.EqualTo("마을 밖으로 출발하기"));
            director.ScreenClosed("departure_table"); director.Tick(.02f);
            Assert.That(director.Finished, Has.No.Member("awake-blueprints"));
            director.Interacted("departure_sign"); director.Tick(.02f);
            Assert.That(director.Finished, Has.Member("awake-blueprints"));
        }

        [Test] public void TheFirstDepartureDoesNotRequireReadingTheFarmhouse()
        {
            var script = RealScript();
            script.beats = script.beats.Where(b => b.part == 4).ToArray();
            script.beats[0].trigger = Trigger("start"); script.skip = null;
            var stage = new Stage { PlayerX = 12.5f };
            var director = new OpeningDirector(script, stage, 4);
            director.Tick(.02f); director.Tick(.02f);
            stage.PlayerX = 36; director.Tick(.02f); director.Tick(.02f);
            Assert.That(stage.Banner, Is.EqualTo("마을 밖으로 출발하기"));
            director.Interacted("departure_sign"); director.Tick(.02f);
            Assert.That(director.Finished, Has.Member("awake-blueprints"));
            Assert.That(stage.Log.Any(l => l.StartsWith("open ") || l.StartsWith("broom ")), Is.False);
        }

        static OpeningScript Visits() => JsonUtility.FromJson<OpeningScript>(File.ReadAllText(RepositoryFile("data/story/village_visits.json")));

        [Test] public void EveryArtisanVisitIsValidAndPlaysToItsEnd()
        {
            var all = Visits();
            Assert.That(OpeningScriptRules.Validate(all), Is.Empty);
            Assert.That(all.beats.Select(b => b.part).Distinct(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }), "every ink has a return reveal");
            var expected = new Dictionary<int, string[]>
            {
                [1] = new[] { "artisan enterSlow", "exclaim", "artisan walkFast", "artisan: 어, 또 보네!", "artisan: 너… 시계탑 집행자를 쓰러뜨렸구나!", "artisan: 이 우물은 마을 곳곳의 물길과 이어져 있거든.", "bloom 1", "artisan: 고마워. 네 덕분에 희망이 생겼네.", "pan artisan_hub", "unlock palette_bench", "artisan: 조심해서 다녀와. 또 보자!", "artisan leave" },
                [2] = new[] { "artisan place ", "pan artisan_hub", "unlock reclaim_bench", "artisan leave" },
                [3] = new[] { "lock", "unlock" },
                [4] = new[] { "artisan place ", "pan artisan_hub", "sparkle palette_bench", "artisan leave" },
                [5] = new[] { "artisan place look", "artisan: …다 모았구나.", "artisan walk", "sparkle palette_bench" },
            };
            foreach (int piece in expected.Keys)
            {
                var beats = all.beats.Where(b => b.part == piece).ToArray();
                foreach (var beat in beats) beat.part = 1;
                var script = new OpeningScript { schemaVersion = 1, id = "visit", speakerNames = all.speakerNames, beats = beats };
                var stage = new Stage { PlayerX = 14.5f }; var director = new OpeningDirector(script, stage, 1);
                for (int i = 0; i < 140 && director.Finished.Count < beats.Length; i++)
                {
                    stage.Animating = stage.BubbleWaiting = stage.Walking = false;
                    // The free stretches of visits 2 and 4: the hero walks to the hammering, then puts the piece in.
                    if (stage.Banner == "망치 소리가 나는 쪽으로") stage.PlayerX = 54;
                    if (stage.Banner == "마을 팔레트에 조각 끼우기") director.Interacted("village_palette");
                    director.Tick(2);
                }
                Assert.That(director.Finished.Count, Is.EqualTo(beats.Length), $"visit {piece}: " + string.Join(" | ", stage.Log));
                Assert.That(expected[piece].Select(e => stage.Log.IndexOf(e)), Is.Ordered.And.All.GreaterThanOrEqualTo(0), $"visit {piece}: " + string.Join(" | ", stage.Log));
                Assert.That(stage.Log.Last(), Is.EqualTo("unlock"), $"visit {piece} gives the controls back last");
                if(piece>1) Assert.That(stage.Log.Any(l=>l.StartsWith("bloom ")),Is.False,"facility scenes cannot pour ink automatically");
            }
        }

        // Part 5 as the village plays it: on its own, started at once instead of by the death-return event.
        [Test] public void TheFirstReturnWakesTheHeroAtTheWellThenShowsWhatADeathKeeps()
        {
            var beats = RealScript().beats.Where(b => b.trigger.type == "event" && b.trigger.name == "runDeathReturn").ToArray();
            Assert.That(beats.Select(b => b.id), Is.EqualTo(new[] { "first-return" }));
            foreach (var beat in beats) { beat.part = 1; beat.trigger = Trigger("start"); }
            var stage = new Stage(); var director = new OpeningDirector(Script(beats), stage, 1);
            for (int i = 0; i < 20 && director.Finished.Count < 1; i++) { stage.Animating = false; director.Tick(2); }
            Assert.That(director.Finished.Count, Is.EqualTo(1), string.Join(" | ", stage.Log));
            Assert.That(stage.Log, Is.EqualTo(new[]
            {
                "lock", "clip hero inkGather at 14.5", "clip hero death reversed", "unlock", "hero: 또 우물 앞이야..?",
                "flag firstReturnSeen",
            }));
        }

        // The first run's artisan as the village plays it: a greeting (met or not), the pigment and the office, a goodbye.
        [Test] public void TheFirstRunVisitShowsThePigmentAndOpensTheSupplyOffice()
        {
            var source = RealScript();
            foreach (var met in new[] { true, false })
            {
                OpeningBeat Copy(string id) => JsonUtility.FromJson<OpeningBeat>(JsonUtility.ToJson(source.beats.First(b => b.id == id)));
                var greet = Copy(met ? "first-run-visit-met" : "first-run-visit-stranger"); var common = Copy("first-run-visit");
                var bye = Copy(met ? "first-run-visit-bye-met" : "first-run-visit-bye-stranger");
                greet.trigger = Trigger("start"); common.trigger = Trigger("after", greet.id); bye.trigger = Trigger("after", common.id);
                foreach (var b in new[] { greet, common, bye }) b.part = 1;
                var script = new OpeningScript { schemaVersion = 1, id = "first-run", speakerNames = source.speakerNames, beats = new[] { greet, common, bye } };
                Assert.That(OpeningScriptRules.Validate(script), Is.Empty);
                var stage = new Stage { PlayerX = 14.5f }; var director = new OpeningDirector(script, stage, 1);
                for (int i = 0; i < 80 && director.Finished.Count < 3; i++) { stage.Animating = stage.BubbleWaiting = stage.Walking = false; director.Tick(2); }
                Assert.That(director.Finished.Count, Is.EqualTo(3), string.Join(" | ", stage.Log));
                var order = new[] { "lock", "artisan enter", "exclaim", "artisan walk", met ? "artisan: 어, 너 마을 밖 정비소에 들렀던 손님 아니니?" : "artisan: 어머, 이 마을에 손님이라니.",
                    "artisan: 근데 너 주머니에 반짝이는 거, 그거 뭐야?", "pigment", "hero: ...", "pan artisan_hub", "unlock supply_counter", "artisan leave", "unlock" };
                Assert.That(order.Select(e => stage.Log.IndexOf(e)), Is.Ordered.And.All.GreaterThanOrEqualTo(0), string.Join(" | ", stage.Log));
                foreach (var line in stage.Bubbles) Assert.That(line, Does.Not.Contain("·"));
            }
        }

        [Test] public void ResidentsSpeakAndTheChaseIsChecked()
        {
            var script = RealScript();
            Assert.That(script.chase.speeds.Select(z => z.fromX), Is.Ordered.Ascending);
            float period = script.chase.surge.Sum(z => z.seconds), mean = script.chase.surge.Sum(z => z.seconds * z.rate) / period;
            Assert.That(mean, Is.EqualTo(1).Within(.02f), "the surge keeps the zone speed as the average speed");
            Assert.That(script.course.pieces.Where(p => p.kind == "resident").Select(p => p.id), Is.EquivalentTo(new[] { "merchant", "traveler" }));

            var bad = Script(Beat("a", 1, Trigger("start"),
                new OpeningAction { type = "bubble", speaker = "merchant", mode = "auto", text = "x" },
                new OpeningAction { type = "erasureChase", fromBehind = 10 }));
            var errors = OpeningScriptRules.Validate(bad);
            Assert.That(errors, Has.Some.Contains("unknown speaker merchant"), "only course residents speak besides the hero and the helper");
            Assert.That(errors, Has.Some.Contains("erasureChase needs the script's chase"));
            bad.course = new OpeningCourse { leftX = -40, pieces = new[] { new OpeningCoursePiece { kind = "resident", id = "merchant", name = "주민 1", art = "a", x = -30, face = 1 } } };
            bad.chase = script.chase;
            Assert.That(OpeningScriptRules.Validate(bad), Is.Empty);
            bad.course.pieces[0].face = 0;
            Assert.That(OpeningScriptRules.Validate(bad), Has.Some.Contains("resident needs id, art, name and face"));
        }

        [Test] public void SkipStopsEverything()
        {
            var stage = new Stage(); var director = new OpeningDirector(RealScript(), stage, 3);
            director.Tick(.02f); director.Skip(); director.Tick(.02f);
            Assert.That(director.Outcome, Is.EqualTo(OpeningOutcome.Skipped));
            Assert.That(stage.Banner, Is.Null);
        }

        [Test] public void InvalidScriptsAreRefused()
        {
            var bad = Script(Beat("a", 1, Trigger("after", "missing"), new OpeningAction { type = "bubble", speaker = "nobody", mode = "auto", text = "x" }),
                Beat("a", 1, Trigger("reachX"), Banner("b", null)));
            var errors = OpeningScriptRules.Validate(bad);
            Assert.That(errors, Has.Some.Contains("duplicate id"));
            Assert.That(errors, Has.Some.Contains("after unknown beat missing"));
            Assert.That(errors, Has.Some.Contains("unknown speaker nobody"));
            Assert.That(errors, Has.Some.Contains("reachX needs lte or gte"));
            Assert.That(errors, Has.Some.Contains("banner needs doneWhen"));
            bad.course = new OpeningCourse { leftX = -10, pieces = new[] { new OpeningCoursePiece { kind = "platform", x = -2, width = 3, top = 2 } } };
            Assert.That(OpeningScriptRules.Validate(bad), Has.Some.Contains("outside the road"), "a platform may not reach into the village");
            Assert.That(new OpeningDirector(bad, new Stage(), 1).Outcome, Is.EqualTo(OpeningOutcome.Failed));
        }
    }
}
