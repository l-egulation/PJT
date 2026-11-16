using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        // Entrance cutscene (docs/design/enemies/chapter2/rose-blue/chapter2-rose-blue-cutscenes.md, data/combat/chapter2_rose_blue_intro.json).
        // The clock is the reviewed HTML clock: intro.startAt + cutAge. Positions are px of the 960px room (32 px per unit).
        [Serializable] sealed class IntroTimes { public float barsIn, walkStart, walkEnd, appear, lookRose, lookBlue, roseStep, roseTurn, blueFlinch, castStart, bubbleStart, bubbleFormed, floorEraseStart, floorEraseEnd, platformsStart, machineStart, liftEnd, bubblePop, vanish, barsOut, roseEyeGlint; }
        [Serializable] sealed class IntroPositions { public float playerEnterX, playerStopX, roseX, blueX, roseStepPx, blueStepBackPx; }
        [Serializable] sealed class IntroHeads { public float rose, blue; }
        [Serializable] sealed class IntroPlan { public string motion; public float from, to; public bool loop; }
        [Serializable] sealed class IntroLineData { public string id, speaker, text, type, cover; public float from, to, cps; }
        [Serializable] sealed class IntroData { public string format; public float startAt, duration; public IntroTimes times; public IntroPositions positions; public IntroHeads headPx; public IntroPlan[] rosePlan, bluePlan; public IntroLineData[] dialogue; }

        /// <summary>One speech bubble of a boss cutscene for the game GUI to draw (MaintenanceBubbleView).</summary>
        public readonly struct CutsceneLine
        {
            public readonly string Id, Speaker, Text, CoverOf; public readonly int Visible; public readonly Vector2 Head; public readonly Color32 Tag;
            public CutsceneLine(string id, string speaker, string text, int visible, Vector2 head, Color32 tag, string coverOf)
            { Id = id; Speaker = speaker; Text = text; Visible = visible; Head = head; Tag = tag; CoverOf = coverOf; }
        }
        static readonly Color32 RoseTag = new Color32(0xa8, 0x32, 0x5f, 255), BlueTag = new Color32(0x2f, 0x4f, 0x9e, 255);

        static IntroData intro;
        static IntroData Intro
        {
            get
            {
                if (intro != null) return intro;
                var text = Resources.Load<TextAsset>("RoseBlueIntro");
                if (!text) throw new InvalidOperationException("Missing Rose/Blue intro cutscene data (RoseBlueIntro.json)");
                var data = JsonUtility.FromJson<IntroData>(text.text);
                if (data.format != "deviant-roseblue-intro-v1" || data.duration <= data.startAt) throw new InvalidOperationException("Bad Rose/Blue intro cutscene data");
                return intro = data;
            }
        }
        /// <summary>Game seconds the entrance cutscene runs (HTML clock from startAt to the end).</summary>
        public static float IntroSeconds => Intro.duration - Intro.startAt;
        /// <summary>Game seconds from the start of the entrance cutscene to a named HTML-clock time (data times.*).</summary>
        public static float IntroAt(string time)
        {
            var f = typeof(IntroTimes).GetField(time) ?? throw new ArgumentException("Unknown intro time: " + time);
            return (float)f.GetValue(Intro.times) - Intro.startAt;
        }
        /// <summary>Game seconds from the cutscene start to a dialogue line's start.</summary>
        public static float IntroLineAt(string id) => (Intro.dialogue.FirstOrDefault(d => d.id == id) ?? throw new ArgumentException("Unknown intro line: " + id)).from - Intro.startAt;
        /// <summary>Letterbox bars are down (boss cutscene).</summary>
        public bool CutsceneBars { get; private set; }
        /// <summary>Speech bubbles on screen this frame, covered lines first.</summary>
        public readonly List<CutsceneLine> CutsceneLines = new List<CutsceneLine>();

        bool introRoseFlip, introBlueFlip, introPlatforms, introLifting, introRoseShown, introBlueShown;
        Vector2 introRoseAt, introBlueAt, introLiftFrom;
        SpriteRenderer introCharge;

        void BeginIntroPresentation()
        {
            introRoseFlip = Rose.visual.flipX; introBlueFlip = Blue.visual.flipX; introRoseAt = Rose.Position; introBlueAt = Blue.Position;
            introPlatforms = introLifting = introRoseShown = introBlueShown = false;
        }

        void EndIntroPresentation()
        {
            Rose.visual.flipX = introRoseFlip; Blue.visual.flipX = introBlueFlip; Rose.Position = introRoseAt; Blue.Position = introBlueAt;
            Rose.visual.transform.localPosition = Blue.visual.transform.localPosition = Vector3.zero;
            Rose.visual.color = Blue.visual.color = Color.white;
            if (introCharge) introCharge.enabled = false;
            if (!introPlatforms) { Stage.ShowPlatforms(true); introPlatforms = true; }
            CutsceneBars = false; CutsceneLines.Clear(); HideLiftBubble();
        }

        static float Ease(float v) { v = Mathf.Clamp01(v); return v * v * (3 - 2 * v); }

        /// <summary>Called every cutscene step while Phase.Introduction, after the common actor refresh.</summary>
        void StepIntro()
        {
            LoadBossMotions();
            var D = Intro; var S = D.times; var P = D.positions; float t = D.startAt + cutAge;

            // Siblings: Rose on the left facing right, Blue on the right facing left; both flash in and leave before the floor goes.
            float rx = P.roseX + P.roseStepPx * Ease((t - S.roseStep) / .35f), bx = P.blueX + P.blueStepBackPx * Ease((t - S.roseStep - .25f) / .3f);
            Rose.Position = Stage.World(new Vector2(rx / 32f, 3)); Blue.Position = Stage.World(new Vector2(bx / 32f, 3));
            bool shown = t >= S.appear && t < S.vanish;
            if (shown && !introRoseShown) { StartSheetFx("blue-vanish", Blue.Position, 1, false, AbovePlayer(2)); }
            Rose.PresentationVisible = Blue.PresentationVisible = shown;
            Rose.visual.flipX = true; Blue.visual.flipX = false;

            string rose = "rose-breath"; double roseAge = arena.Now; bool roseLoop = true;
            foreach (var p in D.rosePlan) if (t >= p.from && t < p.to) { rose = p.motion; roseAge = t - p.from; roseLoop = p.loop; }
            string blue = "blue-breath"; double blueAge = arena.Now; bool blueLoop = true;
            foreach (var p in D.bluePlan) if (t >= p.from && t < p.to) { blue = p.motion; blueAge = t - p.from; blueLoop = p.loop; }
            ShowIntroMotion(Rose, rose, roseAge, roseLoop); RoseMotion = rose;
            ShowIntroMotion(Blue, blue, blueAge, blueLoop); BlueMotion = blue;
            // Blue shrinks back 2 px while Rose holds the stage, and jolts when cut short.
            bool shy = t >= S.roseStep + .25f && t < S.castStart;
            float flinch = t >= S.blueFlinch && t < S.blueFlinch + .3f ? Mathf.Round(Mathf.Sin((t - S.blueFlinch) * 48) * 2) : 0;
            Blue.visual.transform.localPosition = new Vector3(flinch / 32f, shy ? -2 / 32f : 0, 0);
            Rose.visual.transform.localPosition = Vector3.zero;
            float arrive = Mathf.Clamp01((t - S.appear) / .2f);
            Rose.visual.color = Blue.visual.color = new Color(1, 1, 1, arrive);
            if (!shown && introRoseShown) { Ghost(Rose.visual.sprite, Rose.visual.transform.position, Rose.visual.transform.localScale, true); Ghost(Blue.visual.sprite, Blue.visual.transform.position, Blue.visual.transform.localScale, false); StartSheetFx("blue-vanish", Blue.Position, 1, false, AbovePlayer(2)); }
            introRoseShown = introBlueShown = shown;

            // Blue's ink gathers at her casting hand once the spell is under way.
            if (shown && t >= S.castStart + .9f)
            {
                if (!introCharge) introCharge = ArtView("intro ink charge", VfxFrame("ink-charge", 0), Blue.Position, AbovePlayer(2));
                introCharge.enabled = true; introCharge.sprite = VfxFrame("ink-charge", (t - S.castStart) % .5f);
                introCharge.transform.position = Blue.Position + new Vector2(-34 / 32f, 80 / 32f);
                introCharge.transform.localScale = Vector3.one * .55f * (vfxSheets["ink-charge"].scale > 0 ? vfxSheets["ink-charge"].scale : 1);
            }
            else if (introCharge) introCharge.enabled = false;

            // The hallucination battlefield: the floor goes, the phase 1 platforms and the machine come in.
            if (t >= S.platformsStart && !introPlatforms) { Stage.ShowPlatforms(true); introPlatforms = true; }
            Machine.PresentationVisible = t >= S.machineStart;

            StepIntroPlayer(t, S, rx, bx);
            CutsceneBars = t >= S.barsIn && t < S.barsOut;
            IntroLines(t, rx, bx);
        }

        void ShowIntroMotion(RoseBlueActor actor, string id, double age, bool loop)
        {
            var (m, cells) = BossMotionEntry(id);
            actor.visual.sprite = cells[m.sequence[MotionStepAt(m.durationsMs, age, loop)]];
            actor.visual.transform.localScale = Vector3.one;
        }

        void StepIntroPlayer(float t, IntroTimes S, float rx, float bx)
        {
            var p = arena.Player; float roseWorld = Stage.World(new Vector2(rx / 32f, 0)).x, blueWorld = Stage.World(new Vector2(bx / 32f, 0)).x;
            // The player looks at Rose, then at Blue, then at whoever speaks; at the spell they watch Blue.
            float? look = null;
            if (t >= S.lookRose && t < S.lookBlue) look = roseWorld;
            else if (t >= S.lookBlue && t < S.lookBlue + .5f) look = blueWorld;
            var speaking = Intro.dialogue.FirstOrDefault(d => t >= d.from && t < d.to);
            if (t >= S.lookBlue + .5f && speaking != null) look = speaking.speaker == "rose" ? roseWorld : blueWorld;
            if (t >= S.castStart && t < S.bubbleStart) look = blueWorld;
            if (look.HasValue && Mathf.Abs(look.Value - p.Position.x) > .05f) p.facing = look.Value < p.Position.x ? -1 : 1;

            // Blue-violet magic closes over the player, carries them to the phase 1 start platform and breaks on landing.
            if (t >= S.bubbleFormed)
            {
                if (!introLifting) { introLifting = true; introLiftFrom = p.Position; }
                float lift = Ease((t - S.bubbleFormed) / (S.liftEnd - S.bubbleFormed));
                p.Position = Vector2.Lerp(introLiftFrom, cutTo, lift) + Vector2.up * (Mathf.Sin(lift * Mathf.PI) * 28 / 32f);
                p.velocity = Vector2.zero;
            }
            if (t >= S.bubbleStart && t < S.bubblePop)
            {
                if (!liftBubbleSprite) liftBubbleSprite = ProceduralArt("bubble");
                if (!liftBubble) liftBubble = ArtView("player lift bubble", liftBubbleSprite, p.Center, p.visual.sortingOrder + 3);
                liftBubble.enabled = true; liftBubble.sortingLayerID = p.visual.sortingLayerID; liftBubble.transform.position = p.Center;
                liftBubble.transform.localScale = Vector3.one * (Mathf.Max(p.size.x, p.size.y) + .65f) / 4;
                // Rises from the feet in eight steps (soft fades read wrong in this game).
                float grow = Mathf.Floor(Mathf.Clamp01((t - S.bubbleStart) / (S.bubbleFormed - S.bubbleStart)) * 8) / 8;
                liftBubble.color = new Color(1, 1, 1, grow);
            }
            else HideLiftBubble();
        }

        void IntroLines(float t, float rx, float bx)
        {
            var D = Intro;
            CutsceneSpeech(D.dialogue, t, speaker => speaker == "rose"
                ? Stage.World(new Vector2(rx / 32f, 3 + (D.headPx.rose + 6) / 32f))
                : Stage.World(new Vector2(bx / 32f, 3 + (D.headPx.blue + 6) / 32f)));
        }

        // HTML speech.mjs rules: cps 30 by default, 'instant' shows the whole line, a covering line freezes the line it cuts.
        void CutsceneSpeech(IntroLineData[] lines, float t, Func<string, Vector2> Head)
        {
            CutsceneLines.Clear();
            foreach (var d in lines)
            {
                var coverer = lines.FirstOrDefault(c => c.cover == d.id && t >= c.from && t < c.to);
                bool active = t >= d.from && t < d.to;
                if (!active && coverer == null) continue;
                float cps = d.type == "instant" ? float.PositiveInfinity : d.cps > 0 ? d.cps : 30;
                float typed = (coverer != null ? Mathf.Min(t, coverer.from) : t) - d.from;
                int visible = float.IsPositiveInfinity(cps) ? d.text.Length : Mathf.Clamp(Mathf.FloorToInt(typed * cps) + 1, 0, d.text.Length);
                string name = d.speaker == "rose" ? RoseDisplayName : BlueDisplayName;
                var line = new CutsceneLine(d.id, name, d.text, visible, Head(d.speaker), d.speaker == "rose" ? RoseTag : BlueTag, d.cover);
                if (coverer != null) CutsceneLines.Insert(0, line); else CutsceneLines.Add(line);
            }
        }
    }
}
