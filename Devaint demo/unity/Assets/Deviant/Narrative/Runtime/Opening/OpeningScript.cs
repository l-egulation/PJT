using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Narrative.Opening
{
    // Playable opening beats (data/story/opening_playable.json, docs/design/story/opening/opening-playable.md). Plain serializable
    // fields so the Unity side can read the file with JsonUtility; one flat action type carries every action's fields.
    [Serializable] public sealed class OpeningScript
    {
        public int schemaVersion; public string id, title, status;
        public string[] checkpoints;
        public OpeningSkip skip;
        // The road the opening is played on, left of the village (negative x); removed when the opening ends.
        public OpeningCourse course;
        // How the erasure chases the hero down the road (erasureChase); required when a beat starts a chase.
        public OpeningChase chase;
        // Name tags of speakers other than the hero and the residents (whose names are on their course pieces).
        public OpeningSpeakerName[] speakerNames;
        // The investigator's notebook the opening starts with (action notebook): one spread per page, Space turns it.
        public OpeningNotebook notebook;
        // The village monster the hero meets after the record desk (action broom): fought bare-handed, never kills.
        public OpeningBroom broom;
        public OpeningBeat[] beats;
    }

    // art: a Resources sprite path; x, y: its top-left on the open notebook (px, 600x420 spread at 180,14 of the 960x540
    // screen); scale shrinks it about its centre; lines: the caption under the notebook; circle: the red pencil circle drawn
    // over a circled page once its picture is in (frames at circleSeconds each, the last one held), centred on circleX, circleY
    // in the 1400x940 spread texture, each circle pixel circleScale spread pixels (the map's own export scale).
    [Serializable] public sealed class OpeningNotebook { public string book, circle; public int circleFrames; public float circleSeconds, circleX, circleY, circleScale = 1; public OpeningNotebookPage[] pages; }
    [Serializable] public sealed class OpeningNotebookPage { public string art; public float x, y, scale = 1; public bool circled; public string[] lines; }

    // Frames under art (idle, walk, attack, hit, death; numbered 00, 01...), facing left. It walks at walk towards the hero
    // until near, raises its broom for windup seconds, strikes for strike seconds (a hit within reach knocks the hero
    // back), rests cool seconds; hp punches bring it down.
    [Serializable] public sealed class OpeningBroom { public string art; public int hp; public float walk, near, windup, strike, cool, reach; }

    // The road starts at leftX; the camera stops at cameraLeftX (the walk-in starts off screen between the two).
    [Serializable] public sealed class OpeningCourse { public float leftX, cameraLeftX = float.NaN; public OpeningCoursePiece[] pieces; }
    // kind "decoration": a solid village prop (id) on the ground at x; kind "platform": width stone tiles from x, top above the
    // ground; kind "wall": a climbable ground block width wide and top high; kind "pit": a gap width wide in the ground;
    // kind "resident": a bystander drawn from the numbered frames in art, facing right (face 1) or left (-1), whose id is
    // its speaker name in bubbles and whose name tags its bubbles; kind "fence": a solid wooden fence width wide and top
    // high; kind "crate": a solid wooden crate. A wall with a bottom hangs from bottom up to top (walk under it).
    [Serializable] public sealed class OpeningCoursePiece { public string kind, id, art, name; public float x, top, width, bottom; public int face, phase; }

    // The left erasure front: average speed by the hero's x (speeds, ascending fromX) times a repeating surge pattern, pulled
    // faster when the hero gets more than leash.far ahead, held for a moment whenever it swallows an object (bite). It
    // catches the hero within catchMargin; the hero then starts again at checkpointX and the front checkpointBack behind.
    // Ground pieces turn groundLead units ahead of the front, objects objectLagMin..objectLagMax behind it.
    [Serializable] public sealed class OpeningChase
    {
        public float checkpointX, checkpointBack, catchMargin, groundLead, objectLagMin, objectLagMax, slow;
        // What the hero says when the front catches it.
        public string caughtLine;
        public OpeningChaseSpeed[] speeds;
        public OpeningSurge[] surge;
        public OpeningLeash leash;
        public OpeningBite bite;
    }
    [Serializable] public sealed class OpeningChaseSpeed { public float fromX, speed; }
    [Serializable] public sealed class OpeningSurge { public float seconds, rate; }
    [Serializable] public sealed class OpeningLeash { public float far, k, max; }
    [Serializable] public sealed class OpeningBite { public float hold, shake, amp; }

    [Serializable] public sealed class OpeningSpeakerName { public string speaker, name; }

    // Holding holdKey for holdSeconds before the chase runs jumps to beat (its trigger is "skip": nothing else starts it);
    // later, up to the village part, it skips to the well as before.
    [Serializable] public sealed class OpeningSkip { public string holdKey; public float holdSeconds; public string[] from; public string to, beat; }

    // One move of the controls strip (action controls): a small card that plays the recorded ghost (ghost, from
    // data/story/opening_ghosts.json) over and over, with its keys and its name under the picture.
    [Serializable] public sealed class OpeningControlItem { public string[] keys; public string text, ghost; }

    [Serializable] public sealed class OpeningBeat
    {
        public string id; public int part; public bool once; public int pendingDecision; public string flag;
        public OpeningTrigger trigger;
        public OpeningAction[] actions;
    }

    // reachX bounds default to NaN: a missing side means "no limit on that side".
    [Serializable] public sealed class OpeningTrigger
    {
        public string type, beat, action, target, screen, name, checkpoint, hit;
        public float lte = float.NaN, gte = float.NaN, distance, seconds;
        // reachX only: the hero must also stand on something (grounded) and be between minY and maxY above the ground.
        public bool grounded;
        public float minY = float.NaN, maxY = float.NaN;
    }

    [Serializable] public sealed class OpeningAction
    {
        public string type, speaker, mode, text, color, interactables, id, value, name, sprite, behind, effect, actor, clip, spawn, despawn;
        // lesson: the recorded guide ghost (Resources/HubOpeningGhosts) that demonstrates the move instead of a card.
        public string ghost;
        public string[] keys, set, grant;
        // controls: the moves the strip shows (mode hide takes it down).
        public OpeningControlItem[] items;
        public OpeningTrigger doneWhen;
        public float fromX = float.NaN, toX = float.NaN, x = float.NaN, seconds;
        // erasureChase / erasureClose
        public float fromBehind, holdAt = float.NaN, slowFromX = float.NaN, rightAhead, rightSpeed, leftSpeed;
        // erasureChase: seconds the front takes to sweep in (0 = it appears at once). exclaim: id "hero" pops the 「!」 over the hero.
        public float rollIn;
        // fear: the hero glances away and back every interval seconds (three glances), for seconds (does not wait).
        public float interval;
        // Village visits (data/story/village_visits.json): artisan and pan places, facing, the piece that blooms, a blow's volume.
        public float heroOffset = float.NaN, volume;
        // zoom: how close the camera comes (1 = the usual view).
        public float zoom;
        public int face, piece;
        public bool pushBack, reverse, holdLast;
    }

    public static class OpeningScriptRules
    {
        public static readonly string[] Triggers = { "start", "after", "reachX", "inputDone", "interact", "screenClosed", "touch", "event", "wait", "skip" };
        public static readonly string[] Inputs = { "move", "jump", "airJump", "dash", "attack1", "attack2", "skill1", "skill2" };
        public static readonly string[] Actions =
        {
            "bubble", "banner", "lockInput", "unlockInput", "village", "autoWalk",
            "erasureChase", "erasureGo", "erasureRelease", "waitErasure", "erasureClose", "erasureMeet", "heroSketch", "waitAnimation", "awaken", "openStation",
            "artisan", "pan", "unlockStation", "sparkle", "bloom", "palette", "thud",
            "cameraShake", "sfx", "wait", "drainColor", "clip", "actor", "checkpoint", "flag", "abilities", "lesson", "fear",
            "notebook", "broom", "inkFlicker", "face", "mark", "guide", "focus", "zoom", "exclaim", "pigment", "controls", "demo",
        };
        // Movement names an abilities action may set or grant (untaught movement is locked).
        public static readonly string[] Abilities = { "jump", "airJump", "wall", "dash" };
        // Actors the stage can animate; bubbles may also come from the course's residents.
        public static readonly string[] Speakers = { "hero", "helper" };
        public static readonly string[] CourseKinds = { "decoration", "platform", "wall", "pit", "resident", "fence", "crate", "dodgeGate" };
        public static readonly string[] ArtisanModes = { "enter", "enterSlow", "place", "walk", "walkFast", "busy", "face", "leave", "inside" };
        public static readonly string[] ArtisanBusy = { "", "sort", "hammer", "look" };

        // Every problem in the script, empty when it is usable. The director refuses a script with any error.
        public static List<string> Validate(OpeningScript script)
        {
            var errors = new List<string>();
            if (script == null) { errors.Add("script is missing"); return errors; }
            if (script.schemaVersion != 1) errors.Add("schemaVersion must be 1");
            if (string.IsNullOrEmpty(script.id)) errors.Add("id is missing");
            // JsonUtility fills absent blocks with empty objects: a course counts once it has pieces, the chase once a beat
            // starts one.
            if (script.course?.pieces != null && script.course.pieces.Length > 0) CheckCourse(script.course, errors);
            bool chases = script.beats != null && script.beats.Any(b => b?.actions != null && b.actions.Any(a => a?.type == "erasureChase"));
            if (chases && script.chase != null) CheckChase(script.chase, errors);
            bool Uses(string type) => script.beats != null && script.beats.Any(b => b?.actions != null && b.actions.Any(x => x?.type == type));
            if (Uses("notebook") && (script.notebook?.pages == null || script.notebook.pages.Length == 0 || script.notebook.pages.Any(pg => pg == null || string.IsNullOrEmpty(pg.art) || pg.lines == null || pg.lines.Length == 0)))
                errors.Add("notebook: pages need art and lines");
            if (Uses("broom") && (script.broom == null || string.IsNullOrEmpty(script.broom.art) || script.broom.hp < 1 || script.broom.walk <= 0))
                errors.Add("broom: needs art, hp and walk");
            if (script.beats == null || script.beats.Length == 0) { errors.Add("beats are missing"); return errors; }
            var speakers = new HashSet<string>(Speakers);
            foreach (var piece in script.course?.pieces ?? new OpeningCoursePiece[0])
                if (piece?.kind == "resident" && !string.IsNullOrEmpty(piece.id)) speakers.Add(piece.id);
            foreach (var entry in script.speakerNames ?? new OpeningSpeakerName[0])
                if (!string.IsNullOrEmpty(entry?.speaker)) speakers.Add(entry.speaker);
            var ids = new HashSet<string>();
            foreach (var beat in script.beats)
            {
                string at = "beat " + (beat?.id ?? "?");
                if (beat == null || string.IsNullOrEmpty(beat.id)) { errors.Add("a beat has no id"); continue; }
                if (!ids.Add(beat.id)) errors.Add(at + ": duplicate id");
                if (beat.part < 1) errors.Add(at + ": part must be 1 or more");
                CheckTrigger(beat.trigger, at + " trigger", errors);
                if (beat.actions == null || beat.actions.Length == 0) errors.Add(at + ": no actions");
                else for (int i = 0; i < beat.actions.Length; i++) CheckAction(beat.actions[i], at + " action " + i, speakers, errors);
                if ((script.chase == null || script.chase.speeds == null) && beat.actions != null && beat.actions.Any(a => a?.type == "erasureChase")) errors.Add(at + ": erasureChase needs the script's chase");
            }
            foreach (var beat in script.beats.Where(b => b?.trigger?.type == "after"))
                if (!ids.Contains(beat.trigger.beat)) errors.Add("beat " + beat.id + ": after unknown beat " + beat.trigger.beat);
            if (!string.IsNullOrEmpty(script.skip?.beat) && script.beats.FirstOrDefault(b => b?.id == script.skip.beat)?.trigger?.type != "skip")
                errors.Add("skip: beat " + script.skip.beat + " must exist and start only by a skip");
            return errors;
        }

        static void CheckCourse(OpeningCourse course, List<string> errors)
        {
            if (!(course.leftX < 0)) errors.Add("course: leftX must be left of the village (negative)");
            foreach (var piece in course.pieces ?? new OpeningCoursePiece[0])
            {
                string at = "course piece at x " + piece?.x;
                if (piece == null || !CourseKinds.Contains(piece.kind)) { errors.Add(at + ": kind must be " + string.Join(", ", CourseKinds)); continue; }
                if (piece.x < course.leftX || piece.x + Math.Max(1, piece.width) > 0) errors.Add(at + ": outside the road");
                if (piece.kind == "decoration" && string.IsNullOrEmpty(piece.id)) errors.Add(at + ": decoration needs id");
                if (piece.kind == "platform" && (piece.width < 2 || piece.top <= 0)) errors.Add(at + ": platform needs width 2+ and a top above the ground");
                if ((piece.kind == "wall" || piece.kind == "dodgeGate") && (piece.width < 1 || piece.top <= 0 || piece.bottom < 0 || piece.bottom >= piece.top)) errors.Add(at + ": wall needs width 1+ and a height (top) above its bottom");
                if ((piece.kind == "fence" || piece.kind == "crate") && (piece.width <= 0 || piece.top <= 0)) errors.Add(at + ": " + piece.kind + " needs width and top");
                if (piece.kind == "pit" && piece.width < 1) errors.Add(at + ": pit needs width 1+");
                if (piece.kind == "resident" && (string.IsNullOrEmpty(piece.id) || string.IsNullOrEmpty(piece.art) || string.IsNullOrEmpty(piece.name) || Math.Abs(piece.face) != 1))
                    errors.Add(at + ": resident needs id, art, name and face 1 or -1");
            }
        }

        static void CheckChase(OpeningChase chase, List<string> errors)
        {
            if (chase.speeds == null || chase.speeds.Length == 0 || chase.speeds.Any(s => s == null || s.speed <= 0)) errors.Add("chase: speeds need a positive speed each");
            else for (int i = 1; i < chase.speeds.Length; i++) if (chase.speeds[i].fromX <= chase.speeds[i - 1].fromX) errors.Add("chase: speeds must rise in fromX");
            if (chase.surge == null || chase.surge.Length == 0 || chase.surge.Any(s => s == null || s.seconds <= 0 || s.rate < 0)) errors.Add("chase: surge needs steps with seconds and a rate");
            if (chase.leash == null || chase.leash.far <= 0 || chase.leash.k < 0 || chase.leash.max <= 0) errors.Add("chase: leash needs far, k and max");
            if (chase.bite == null || chase.bite.hold < 0 || chase.bite.shake < 0) errors.Add("chase: bite needs hold and shake");
            if (chase.checkpointBack <= chase.catchMargin) errors.Add("chase: the front must start behind the hero (checkpointBack > catchMargin)");
            if (chase.objectLagMax < chase.objectLagMin || chase.groundLead < 0) errors.Add("chase: groundLead and objectLagMin..Max are distances");
            if (chase.slow <= 0 || chase.slow > 1) errors.Add("chase: slow is a rate in (0, 1]");
        }

        static void CheckTrigger(OpeningTrigger t, string at, List<string> errors)
        {
            if (t == null || !Triggers.Contains(t.type)) { errors.Add(at + ": unknown type " + t?.type); return; }
            switch (t.type)
            {
                case "after": if (string.IsNullOrEmpty(t.beat)) errors.Add(at + ": after needs beat"); break;
                case "reachX":
                    if (float.IsNaN(t.lte) && float.IsNaN(t.gte)) errors.Add(at + ": reachX needs lte or gte");
                    if (!float.IsNaN(t.lte) && !float.IsNaN(t.gte) && t.gte > t.lte) errors.Add(at + ": reachX range is empty");
                    break;
                case "inputDone":
                    if (!Inputs.Contains(t.action)) errors.Add(at + ": unknown input " + t.action);
                    if (t.action == "move" && t.distance <= 0) errors.Add(at + ": move needs a distance");
                    break;
                case "interact": case "touch": if (string.IsNullOrEmpty(t.target)) errors.Add(at + ": needs target"); break;
                case "screenClosed": if (string.IsNullOrEmpty(t.screen)) errors.Add(at + ": needs screen"); break;
                case "event": if (string.IsNullOrEmpty(t.name)) errors.Add(at + ": needs name"); break;
                case "wait": if (t.seconds <= 0) errors.Add(at + ": wait needs seconds"); break;
            }
        }

        static void CheckAction(OpeningAction a, string at, HashSet<string> speakers, List<string> errors)
        {
            if (a == null || !Actions.Contains(a.type)) { errors.Add(at + ": unknown type " + a?.type); return; }
            switch (a.type)
            {
                case "bubble":
                    if (!speakers.Contains(a.speaker)) errors.Add(at + ": unknown speaker " + a.speaker);
                    if (a.mode != "auto" && a.mode != "advance") errors.Add(at + ": mode must be auto or advance");
                    if (string.IsNullOrEmpty(a.text)) errors.Add(at + ": empty text");
                    break;
                case "banner":
                    if (string.IsNullOrEmpty(a.text)) errors.Add(at + ": empty text");
                    if (a.doneWhen == null) errors.Add(at + ": banner needs doneWhen");
                    else CheckTrigger(a.doneWhen, at + " doneWhen", errors);
                    break;
                // Without fromX the walk starts where the player stands.
                case "autoWalk": if (float.IsNaN(a.toX)) errors.Add(at + ": autoWalk needs toX"); break;
                case "village":
                    if (a.color != "allColor" && a.color != "allSketch" && a.color != "profile") errors.Add(at + ": unknown color " + a.color);
                    if (a.interactables != "hidden" && a.interactables != "shown") errors.Add(at + ": interactables must be hidden or shown");
                    break;
                case "wait": if (a.seconds <= 0) errors.Add(at + ": wait needs seconds"); break;
                case "checkpoint": if (string.IsNullOrEmpty(a.value)) errors.Add(at + ": checkpoint needs value"); break;
                case "flag": if (string.IsNullOrEmpty(a.name)) errors.Add(at + ": flag needs name"); break;
                case "sfx": if (string.IsNullOrEmpty(a.id)) errors.Add(at + ": sfx needs id"); break;
                case "erasureChase": if (a.fromBehind <= 0) errors.Add(at + ": erasureChase needs fromBehind (units left of the hero)"); break;
                case "erasureRelease": if (a.seconds < 0) errors.Add(at + ": erasureRelease seconds is a grace time"); break;
                case "waitErasure": if (float.IsNaN(a.x)) errors.Add(at + ": waitErasure needs x"); break;
                case "erasureClose": if (a.rightAhead <= 0 || a.rightSpeed < 0 || a.leftSpeed < 0) errors.Add(at + ": erasureClose needs rightAhead and speeds"); break;
                case "erasureMeet": if (a.seconds <= 0) errors.Add(at + ": erasureMeet needs seconds"); break;
                case "heroSketch": if (a.mode != "hands" && a.mode != "revive") errors.Add(at + ": heroSketch mode must be hands or revive"); break;
                case "artisan":
                    if (!ArtisanModes.Contains(a.mode)) errors.Add(at + ": artisan mode must be one of " + string.Join(", ", ArtisanModes));
                    else if (a.mode == "place" && float.IsNaN(a.x)) errors.Add(at + ": artisan place needs x");
                    else if ((a.mode == "walk" || a.mode == "walkFast") && float.IsNaN(a.x) && float.IsNaN(a.heroOffset)) errors.Add(at + ": artisan walk needs x or heroOffset");
                    else if ((a.mode == "enter" || a.mode == "enterSlow") && float.IsNaN(a.x) && float.IsNaN(a.heroOffset)) errors.Add(at + ": artisan enter needs x or heroOffset");
                    if (a.mode == "busy" && !ArtisanBusy.Contains(a.id ?? "")) errors.Add(at + ": unknown busy " + a.id);
                    if (a.face != 0 && Math.Abs(a.face) != 1) errors.Add(at + ": face is 1 or -1");
                    break;
                case "pan": if (string.IsNullOrEmpty(a.id) || float.IsNaN(a.heroOffset)) errors.Add(at + ": pan needs a station id and heroOffset"); break;
                case "unlockStation": case "sparkle": if (string.IsNullOrEmpty(a.id)) errors.Add(at + ": " + a.type + " needs a station id"); break;
                case "bloom": if (a.piece < 1) errors.Add(at + ": bloom needs piece 1 or more"); break;
                case "thud": if (a.volume <= 0) errors.Add(at + ": thud needs a volume"); break;
                case "openStation":
                    if (string.IsNullOrEmpty(a.id)) errors.Add(at + ": openStation needs id");
                    if (a.doneWhen == null) errors.Add(at + ": openStation needs doneWhen");
                    else CheckTrigger(a.doneWhen, at + " doneWhen", errors);
                    break;
                case "drainColor": if (!Speakers.Contains(a.actor) || a.seconds <= 0) errors.Add(at + ": drainColor needs a known actor and seconds"); break;
                case "clip": if (!Speakers.Contains(a.actor) || string.IsNullOrEmpty(a.clip)) errors.Add(at + ": clip needs a known actor and clip"); break;
                case "lesson": if (string.IsNullOrEmpty(a.text)) errors.Add(at + ": empty text"); break;
                case "controls":
                    if (a.mode != "show" && a.mode != "hide") errors.Add(at + ": controls mode must be show or hide");
                    else if (a.mode == "show" && (a.items == null || a.items.Length == 0 || a.items.Any(i => i == null || string.IsNullOrEmpty(i.text) || string.IsNullOrEmpty(i.ghost) || i.keys == null || i.keys.Length == 0)))
                        errors.Add(at + ": controls show needs items with keys, text and ghost");
                    break;
                case "demo":
                    if (a.mode != "play" && a.mode != "clear") errors.Add(at + ": demo mode must be play or clear");
                    else if (a.mode == "play" && (string.IsNullOrEmpty(a.ghost) || string.IsNullOrEmpty(a.text))) errors.Add(at + ": demo play needs ghost and text");
                    break;
                case "broom": if (float.IsNaN(a.x) || (a.mode != "appear" && a.mode != "fight")) errors.Add(at + ": broom needs x and mode appear or fight"); break;
                case "face": if (Math.Abs(a.face) != 1) errors.Add(at + ": face is 1 or -1"); break;
                case "zoom": if (a.zoom < 1) errors.Add(at + ": zoom is 1 or more"); break;
                case "abilities":
                    if ((a.set == null) == (a.grant == null)) errors.Add(at + ": abilities needs exactly one of set or grant");
                    foreach (var name in a.set ?? a.grant ?? new string[0])
                        if (!Abilities.Contains(name)) errors.Add(at + ": unknown ability " + name);
                    break;
                case "actor":
                    if (string.IsNullOrEmpty(a.spawn) == string.IsNullOrEmpty(a.despawn)) errors.Add(at + ": actor needs exactly one of spawn or despawn");
                    else if (!string.IsNullOrEmpty(a.spawn) && (string.IsNullOrEmpty(a.sprite) || float.IsNaN(a.x))) errors.Add(at + ": spawn needs sprite and x");
                    break;
            }
        }
    }
}
