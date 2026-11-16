using System;
using System.Linq;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        // Phase 1 to 2 cutscene (docs/design/enemies/chapter2/rose-blue/chapter2-rose-blue-cutscenes.md, data/combat/chapter2_rose_blue_phase_shift.json).
        // The machine break, the platforms going, the floor returning and the fall from the current X stay the existing
        // transition (BeginFloorCutscene, StepNaturalFall). On top of it the reviewed HTML clock (startAt + cutAge) adds the
        // letterbox, the siblings at the floor ends, their motions and the argument; phase 2 starts when the clock ends.
        [Serializable] sealed class ShiftTimes { public float machineBlast, platformsGone, floorRestoreStart, floorRestoreEnd, barsIn, appear, lookRose, lookBlue, roseRaise, barsOut; }
        [Serializable] sealed class ShiftData { public string format; public float startAt, duration; public ShiftTimes times; public IntroHeads headPx; public IntroPlan[] rosePlan, bluePlan; public IntroLineData[] dialogue; }

        static ShiftData shift;
        static ShiftData Shift
        {
            get
            {
                if (shift != null) return shift;
                var text = Resources.Load<TextAsset>("RoseBluePhaseShift");
                if (!text) throw new InvalidOperationException("Missing Rose/Blue phase shift cutscene data (RoseBluePhaseShift.json)");
                var data = JsonUtility.FromJson<ShiftData>(text.text);
                if (data.format != "deviant-roseblue-phase-shift-v1" || data.duration <= data.startAt) throw new InvalidOperationException("Bad Rose/Blue phase shift cutscene data");
                return shift = data;
            }
        }
        /// <summary>Game seconds the phase 1 to 2 cutscene runs from the machine break (HTML clock from startAt to the end).</summary>
        public static float PhaseShiftSeconds => Shift.duration - Shift.startAt;
        /// <summary>Game seconds from the machine break to a named HTML-clock time (data times.*).</summary>
        public static float PhaseShiftAt(string time)
        {
            var f = typeof(ShiftTimes).GetField(time) ?? throw new ArgumentException("Unknown phase shift time: " + time);
            return (float)f.GetValue(Shift.times) - Shift.startAt;
        }
        /// <summary>Game seconds from the machine break to a dialogue line's start.</summary>
        public static float PhaseShiftLineAt(string id) => (Shift.dialogue.FirstOrDefault(d => d.id == id) ?? throw new ArgumentException("Unknown phase shift line: " + id)).from - Shift.startAt;

        bool shiftShown, shiftRoseFlip, shiftBlueFlip;

        void BeginPhaseShiftPresentation()
        {
            shiftShown = false; shiftRoseFlip = Rose.visual.flipX; shiftBlueFlip = Blue.visual.flipX;
            // The ink blast where the machine broke (the crystal shards come from BreakMachineArt).
            StartSheetFx("machine-ink-blast", Machine.Position + Vector2.up * (40 / 32f), 1, false, AbovePlayer(4), null, "phase shift machine break blast");
        }

        void EndPhaseShiftPresentation()
        {
            Rose.visual.flipX = shiftRoseFlip; Blue.visual.flipX = shiftBlueFlip;
            Rose.visual.transform.localPosition = Blue.visual.transform.localPosition = Vector3.zero;
            Rose.visual.color = Blue.visual.color = Color.white;
            CutsceneBars = false; CutsceneLines.Clear();
        }

        /// <summary>Called every cutscene step while Phase.FloorTransition, after the common actor refresh.</summary>
        void StepPhaseShift()
        {
            LoadBossMotions();
            var D = Shift; var S = D.times; float t = D.startAt + cutAge;
            // Siblings at the floor ends (phase 2 markers): Rose left facing right, Blue right facing left.
            Rose.Position = Stage.World(Stage.Data.Marker("rose-phase2").Feet); Blue.Position = Stage.World(Stage.Data.Marker("blue-phase2").Feet);
            bool shown = t >= S.appear;
            if (shown && !shiftShown) StartSheetFx("blue-vanish", Blue.Position, 1, false, AbovePlayer(2));
            shiftShown = shown;
            Rose.PresentationVisible = Blue.PresentationVisible = shown;
            Rose.visual.flipX = true; Blue.visual.flipX = false;

            string rose = "rose-breath"; double roseAge = arena.Now; bool roseLoop = true;
            foreach (var p in D.rosePlan) if (t >= p.from && t < p.to) { rose = p.motion; roseAge = t - p.from; roseLoop = p.loop; }
            string blue = "blue-breath"; double blueAge = arena.Now; bool blueLoop = true;
            foreach (var p in D.bluePlan) if (t >= p.from && t < p.to) { blue = p.motion; blueAge = t - p.from; blueLoop = p.loop; }
            ShowIntroMotion(Rose, rose, roseAge, roseLoop); RoseMotion = rose;
            ShowIntroMotion(Blue, blue, blueAge, blueLoop); BlueMotion = blue;
            // Rose bounces up 2 px when she perks up (only on her idle); from then on Blue stays shrunk back 2 px.
            bool raise = rose == "rose-breath" && t >= S.roseRaise && t < S.roseRaise + .22f;
            Rose.visual.transform.localPosition = new Vector3(0, raise ? 2 / 32f : 0, 0);
            Blue.visual.transform.localPosition = new Vector3(0, t >= S.roseRaise + .2f ? -2 / 32f : 0, 0);
            float arrive = Mathf.Clamp01((t - S.appear) / .2f);
            Rose.visual.color = Blue.visual.color = new Color(1, 1, 1, arrive);

            // Once landed, the player looks at Rose, then at Blue, then at whoever speaks.
            if (landedAge >= 0)
            {
                var p = arena.Player; float? look = null;
                if (t >= S.lookRose && t < S.lookBlue) look = Rose.Position.x;
                else if (t >= S.lookBlue && t < S.lookBlue + .3f) look = Blue.Position.x;
                var speaking = D.dialogue.FirstOrDefault(d => t >= d.from && t < d.to);
                if (t >= S.lookBlue + .3f && speaking != null) look = speaking.speaker == "rose" ? Rose.Position.x : Blue.Position.x;
                if (look.HasValue && Mathf.Abs(look.Value - p.Position.x) > .05f) p.facing = look.Value < p.Position.x ? -1 : 1;
            }

            CutsceneBars = t >= S.barsIn && t < S.barsOut;
            Vector2 roseHead = Rose.Position + Vector2.up * ((D.headPx.rose + 6) / 32f), blueHead = Blue.Position + Vector2.up * ((D.headPx.blue + 6) / 32f);
            CutsceneSpeech(D.dialogue, t, speaker => speaker == "rose" ? roseHead : blueHead);
        }
    }
}
