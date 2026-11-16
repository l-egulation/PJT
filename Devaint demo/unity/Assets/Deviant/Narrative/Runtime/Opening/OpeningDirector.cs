using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Narrative.Opening
{
    // What the opening reads from and does to the village. The Unity side owns the actual world, GUI and input scope.
    public interface IOpeningStage
    {
        float PlayerX { get; }
        // Height of the hero's feet above the ground line, and whether it stands on something.
        float PlayerY { get; }
        bool Grounded { get; }
        // True while the opening notebook is up, and while the broom of the village still stands (a falling one is down).
        bool NotebookOpen { get; }
        bool BroomStanding { get; }
        int JumpsUsed { get; }
        bool Dashing { get; }
        // True while a scripted walk started by Walk is still moving the player.
        bool Walking { get; }
        // True while an advance-mode bubble waits for the player to read it.
        bool BubbleWaiting { get; }
        // True while a clip, a colour drain or an actor's entrance started by the stage is still playing.
        bool Animating { get; }
        // True while a guide card (lesson) pauses the game and waits for the player to confirm it.
        bool LessonOpen { get; }
        // The left erasure front's x, NaN before the erasure starts.
        float ErasureLeft { get; }
        void Bubble(string speaker, string text, bool advance);
        void Banner(string[] keys, string text);
        void BannerDone();
        void Village(string color, bool interactables);
        // Teleports to fromX (unless NaN) and walks to toX.
        void Walk(float fromX, float toX);
        void Sfx(string id);
        void Shake(float seconds);
        // The hero glances away and back in turn (three glances, interval apart) and ends facing as it began; the beat goes on.
        void Fear(float seconds, float interval);
        void LockInput(bool locked);
        void Checkpoint(string value);
        void Flag(string name);
        // The left erasure front appears `behind` units left of the hero, still; it never passes holdAt (NaN: no hold) and
        // is slowed while it covers what lies past slowFromX. ErasureGo starts it, ErasureRelease drops the hold and lets it
        // chase after a grace time. ErasureClose brings a right front in rightAhead units right of the hero, both fronts
        // drifting in, and ErasureMeet closes them on the hero within seconds; the stage reports the meeting as "erasureMet".
        void ErasureChase(float behind, float holdAt, float slowFromX, float rollIn);
        void ErasureGo();
        void ErasureRelease(float graceSeconds);
        void ErasureClose(float rightAhead, float rightSpeed, float leftSpeed);
        void ErasureMeet(float seconds);
        // "hands": the hero turns to sketch where it stands, from both hands (Animating until it has covered the body);
        // "revive": the helper's ink floods the sketch from the chest and drains into the face as the colour comes back,
        // leaving the ink body (Animating until it has).
        void HeroSketch(string mode);
        // The village is the hero's again: the road and the erasure go, every movement opens, the ink body stays.
        void Awaken();
        // Opens a station's screen as if the player had pressed F there (the beat waits for its doneWhen).
        void OpenStation(string id);
        // The village artisan of the visits (enter, walk, leave and inside play until done, as Animating). x is NaN where
        // heroOffset (units right of the hero) places it instead; face 0 keeps the facing; busy null keeps the pose.
        void Artisan(string mode, float x, float heroOffset, int face, string busy);
        // The hero and the artisan walk to a station (the artisan heroOffset right of it) and the camera settles on it.
        void Pan(string station, float heroOffset, int face);
        // A workbench's lock breaks (sparkleOnly: sparks around an open one).
        void UnlockStation(string station, bool sparkleOnly);
        void Bloom(int piece);
        void Palette();
        void Thud(float volume);
        void Drain(string actor, float seconds);
        // x (NaN: where the actor stands) places the actor first; the first return's "inkGather" wakes the hero there.
        void Clip(string actor, string clip, float x, bool reverse, bool holdLast);
        void Spawn(string actor, string sprite, float x, string behind, string effect);
        void Despawn(string actor);
        // Movement the player may use from now on; names are OpeningScriptRules.Abilities. replace=false adds them.
        void Abilities(string[] names, bool replace);
        // Pauses the game while a translucent copy of the hero (ghost, recorded) shows the move with its keys above it, or
        // shows a guide card without a ghost; Space closes it once the ghost has shown the move.
        void Lesson(string[] keys, string text, string ghost);
        // The investigator's notebook over a dark screen; Space turns its pages and closing it ends the action.
        void Notebook();
        // "appear": the village broom stands at x, still; "fight": it comes at the hero (the action waits until it falls).
        void Broom(string mode, float x);
        // An F keycap stays over the station (null or empty: none).
        void Mark(string station);
        // A 「!」 leads to the station (null or empty: none): over it with a white outline while it is on screen, at the
        // screen's edge pointing its way while it is not.
        void Guide(string station);
        // The hero's ink flickers as it runs short.
        void InkFlicker();
        // The hero turns to face 1 (right) or -1 (left).
        void Face(int face);
        // The camera eases onto x as if the hero stood there (x NaN: heroOffset right of the hero; both NaN: back on the
        // hero), and closes in by zoom about the hero (1: the usual view).
        void Focus(float x, float heroOffset);
        void Zoom(float zoom);
        // A 「!」 pops over the artisan's head (it has noticed the hero).
        void Exclaim();
        void HeroExclaim();
        // The village's magic pigment chip appears for the first time.
        void Pigment();
        // The strip of moves at the top of the screen (null: taken down); the game goes on under it and F closes it.
        void Controls(OpeningControlItem[] items) { }
        // A recorded ghost plays its move over and over where it was recorded, the game going on around it, with its keys
        // over it (ghost null: every demonstration goes).
        void Demo(string ghost, string[] keys, string text) { }
    }

    public enum OpeningOutcome { Running, Handoff, Skipped, Failed }

    // Runs the beats of the parts up to lastPart. Parts follow each other: a beat can start only in the current part, and
    // the next part begins when its first beat (in file order) triggers, so a later part's position triggers stay quiet
    // while an earlier part walks over them. A beat starts when its trigger fires (each beat at most once) and runs
    // its actions in order; banners, waits, walks and advance bubbles hold their beat until done, and only one banner
    // is up at a time; drains, clips and actor entrances hold it until the stage stops animating. When a beat of a later
    // part triggers, the director stops with Handoff so the caller can continue with what that part still uses. Cleaning
    // the stage up afterwards is the caller's job.
    public sealed class OpeningDirector
    {
        sealed class Run
        {
            public OpeningBeat Beat; public int Index; public bool Entered;
            public float Waited, Moved;
        }

        readonly OpeningScript script;
        readonly IOpeningStage stage;
        readonly int lastPart;
        readonly HashSet<string> started = new HashSet<string>(), finished = new HashSet<string>();
        readonly List<Run> runs = new List<Run>();
        readonly List<(string kind, string key)> notices = new List<(string, string)>();
        Run bannerOwner;
        int part;
        bool firstTick = true;
        float lastX; int lastJumps; bool lastDashing;
        // Input observed during the current tick.
        float moved; bool jumped, airJumped, dashed;

        public OpeningOutcome Outcome { get; private set; }
        public int HandoffPart { get; private set; }
        public int Part => part;
        public string Error { get; private set; }
        public IReadOnlyCollection<string> Finished => finished;
        public bool Started(string beat) => started.Contains(beat);

        public OpeningDirector(OpeningScript script, IOpeningStage stage, int lastPart)
        {
            this.script = script ?? throw new ArgumentNullException(nameof(script));
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.lastPart = lastPart;
            var errors = OpeningScriptRules.Validate(script);
            if (errors.Count > 0) Fail(string.Join("; ", errors));
        }

        public void Notify(string eventName) => notices.Add(("event", eventName));
        public void Interacted(string target) => notices.Add(("interact", target));
        public void ScreenClosed(string screen) => notices.Add(("screenClosed", screen));
        public void Touched(string target) => notices.Add(("touch", target));

        public void Skip() { if (Outcome == OpeningOutcome.Running) Stop(OpeningOutcome.Skipped); }

        // Goes on from the beat with this id as if every beat before it in the file had played: what was running stops
        // (its banner too), and the beat starts now in its own part. False when there is no such beat.
        public bool JumpTo(string id)
        {
            if (Outcome != OpeningOutcome.Running) return false;
            int at = Array.FindIndex(script.beats, b => b.id == id);
            if (at < 0) return false;
            var beat = script.beats[at];
            if (bannerOwner != null) { stage.BannerDone(); bannerOwner = null; }
            runs.Clear(); notices.Clear();
            for (int i = 0; i < at; i++)
                if (script.beats[i].part <= beat.part) { started.Add(script.beats[i].id); finished.Add(script.beats[i].id); }
            part = beat.part; firstTick = false;
            started.Add(beat.id); runs.Add(new Run { Beat = beat });
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (Outcome != OpeningOutcome.Running) return;
            ObserveInput();
            // The first tick only starts the opening: the player is still at the lobby spawn until the start beat moves it.
            foreach (var beat in script.beats)
            {
                if (started.Contains(beat.id) || !Eligible(beat) || (firstTick ? beat.trigger.type != "start" : !Fires(beat.trigger, null))) continue;
                if (beat.part > lastPart) { HandoffPart = beat.part; Stop(OpeningOutcome.Handoff); return; }
                if (beat.part > part) EnterPart(beat.part);
                started.Add(beat.id); runs.Add(new Run { Beat = beat });
            }
            firstTick = false;
            // Notices of this tick start beats above and finish banners and screens below, then they are spent.
            foreach (var run in runs.ToArray())
            {
                Advance(run, deltaSeconds);
                if (Outcome != OpeningOutcome.Running) return;
                if (run.Index >= run.Beat.actions.Length) { runs.Remove(run); finished.Add(run.Beat.id); }
            }
            notices.Clear();
        }

        // A new part drops what the previous one left unfinished (a lesson the player walked past), banner included.
        void EnterPart(int next)
        {
            part = next;
            if (bannerOwner != null && bannerOwner.Beat.part < next) { stage.BannerDone(); bannerOwner = null; }
            runs.RemoveAll(run => run.Beat.part < next);
        }

        bool Eligible(OpeningBeat beat)
        {
            if (part == 0) return beat.trigger.type == "start";
            if (beat.part == part) return true;
            return beat.part == part + 1 && script.beats.First(b => b.part == beat.part) == beat;
        }

        void ObserveInput()
        {
            float x = stage.PlayerX; int jumps = stage.JumpsUsed; bool dashing = stage.Dashing;
            moved = firstTick ? 0 : Math.Abs(x - lastX);
            jumped = !firstTick && jumps > lastJumps; airJumped = jumped && jumps >= 2;
            dashed = !firstTick && dashing && !lastDashing;
            lastX = x; lastJumps = jumps; lastDashing = dashing;
            foreach (var run in runs) run.Moved += moved;
        }

        // A trigger is checked for a beat start (run null) or for the banner its run is showing.
        bool Fires(OpeningTrigger t, Run run)
        {
            switch (t.type)
            {
                case "start": return false;
                case "after": return finished.Contains(t.beat);
                case "reachX":
                    float x = stage.PlayerX, y = stage.PlayerY;
                    return (float.IsNaN(t.lte) || x <= t.lte) && (float.IsNaN(t.gte) || x >= t.gte) && (!t.grounded || stage.Grounded)
                        && (float.IsNaN(t.minY) || y >= t.minY - .01f) && (float.IsNaN(t.maxY) || y <= t.maxY + .01f);
                case "inputDone":
                    switch (t.action)
                    {
                        case "move": return run != null && run.Moved >= t.distance;
                        case "jump": return jumped;
                        case "airJump": return airJumped;
                        case "dash": return dashed;
                        default: return false;
                    }
                case "wait": return run != null && run.Waited >= t.seconds;
                case "interact": return notices.Contains(("interact", t.target));
                case "screenClosed": return notices.Contains(("screenClosed", t.screen));
                case "touch": return notices.Contains(("touch", t.target));
                case "event": return notices.Contains(("event", t.name));
                default: return false;
            }
        }

        void Advance(Run run, float deltaSeconds)
        {
            var actions = run.Beat.actions;
            while (run.Index < actions.Length)
            {
                var a = actions[run.Index];
                if (!run.Entered)
                {
                    if (a.type == "banner" && bannerOwner != null && bannerOwner != run) return;
                    run.Entered = true; run.Waited = 0; run.Moved = 0;
                    if (!Enter(run, a)) return;
                    // A banner counts only what the player does after it is shown.
                    if (a.type == "banner") return;
                }
                else run.Waited += deltaSeconds;
                if (!Done(run, a)) return;
                if (a.type == "banner") { stage.BannerDone(); bannerOwner = null; }
                run.Index++; run.Entered = false;
            }
        }

        bool Enter(Run run, OpeningAction a)
        {
            switch (a.type)
            {
                case "bubble": stage.Bubble(a.speaker, a.text, a.mode == "advance"); return true;
                case "banner": stage.Banner(a.keys ?? Array.Empty<string>(), a.text); bannerOwner = run; return true;
                case "wait": return true;
                case "autoWalk": stage.Walk(a.fromX, a.toX); return true;
                case "lockInput": stage.LockInput(true); return true;
                case "unlockInput": stage.LockInput(false); return true;
                case "village": stage.Village(a.color, a.interactables == "shown"); return true;
                case "sfx": stage.Sfx(a.id); return true;
                case "erasureChase": stage.ErasureChase(a.fromBehind, a.holdAt, a.slowFromX, a.rollIn); return true;
                case "erasureGo": stage.ErasureGo(); return true;
                case "erasureRelease": stage.ErasureRelease(a.seconds); return true;
                case "waitErasure": return true;
                case "erasureClose": stage.ErasureClose(a.rightAhead, a.rightSpeed, a.leftSpeed); return true;
                case "erasureMeet": stage.ErasureMeet(a.seconds); return true;
                case "heroSketch": stage.HeroSketch(a.mode); return true;
                case "waitAnimation": return true;
                case "awaken": stage.Awaken(); return true;
                case "openStation": stage.OpenStation(a.id); return true;
                case "artisan": stage.Artisan(a.mode, a.x, a.heroOffset, a.face, a.mode == "busy" || a.mode == "place" ? a.id ?? "" : null); return true;
                case "pan": stage.Pan(a.id, a.heroOffset, a.face); return true;
                case "unlockStation": stage.UnlockStation(a.id, false); return true;
                case "sparkle": stage.UnlockStation(a.id, true); return true;
                case "bloom": stage.Bloom(a.piece); return true;
                case "palette": stage.Palette(); return true;
                case "thud": stage.Thud(a.volume); return true;
                case "drainColor": stage.Drain(a.actor, a.seconds); return true;
                case "clip": stage.Clip(a.actor, a.clip, a.x, a.reverse, a.holdLast); return true;
                case "actor":
                    if (!string.IsNullOrEmpty(a.spawn)) stage.Spawn(a.spawn, a.sprite, a.x, a.behind, a.effect);
                    else stage.Despawn(a.despawn);
                    return true;
                case "cameraShake": stage.Shake(a.seconds); return true;
                case "fear": stage.Fear(a.seconds, a.interval); return true;
                case "checkpoint": stage.Checkpoint(a.value); return true;
                case "flag": stage.Flag(a.name); return true;
                case "abilities": stage.Abilities(a.set ?? a.grant, a.set != null); return true;
                case "lesson": stage.Lesson(a.keys ?? Array.Empty<string>(), a.text, a.ghost); return true;
                case "notebook": stage.Notebook(); return true;
                case "broom": stage.Broom(a.mode, a.x); return true;
                case "mark": stage.Mark(a.id); return true;
                case "guide": stage.Guide(a.id); return true;
                case "inkFlicker": stage.InkFlicker(); return true;
                case "face": stage.Face(a.face); return true;
                case "focus": stage.Focus(a.x, a.heroOffset); return true;
                case "zoom": stage.Zoom(a.zoom); return true;
                case "exclaim": if (a.id == "hero") stage.HeroExclaim(); else stage.Exclaim(); return true;
                case "pigment": stage.Pigment(); return true;
                case "controls": stage.Controls(a.mode == "show" ? a.items : null); return true;
                case "demo": if (a.mode == "play") stage.Demo(a.ghost, a.keys ?? Array.Empty<string>(), a.text); else stage.Demo(null, null, null); return true;
                default: Fail("beat " + run.Beat.id + ": action " + a.type + " is not implemented yet"); return false;
            }
        }

        bool Done(Run run, OpeningAction a)
        {
            switch (a.type)
            {
                case "bubble": return a.mode != "advance" || !stage.BubbleWaiting;
                case "banner": return Fires(a.doneWhen, run);
                case "wait": return run.Waited >= a.seconds;
                case "autoWalk": return !stage.Walking;
                case "lesson": return !stage.LessonOpen;
                case "notebook": return !stage.NotebookOpen;
                case "broom": return a.mode != "fight" || !stage.BroomStanding;
                case "waitErasure": return stage.ErasureLeft >= a.x;
                case "openStation": return Fires(a.doneWhen, run);
                case "drainColor": case "clip": case "actor": case "waitAnimation": case "pan": return !stage.Animating;
                case "artisan": return a.mode != "enter" && a.mode != "enterSlow" && a.mode != "walk" && a.mode != "walkFast" && a.mode != "leave" && a.mode != "inside" || !stage.Animating;
                default: return true;
            }
        }

        void Fail(string error) { Error = error; Stop(OpeningOutcome.Failed); }

        void Stop(OpeningOutcome outcome)
        {
            Outcome = outcome; runs.Clear(); notices.Clear();
            bannerOwner = null;
        }
    }
}
