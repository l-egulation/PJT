using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Input.Contracts;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The free learning road (user request 2026-11-10). The moves stay up at the top as small cards while the player tries
    // them anywhere: each card plays its recorded ghost over and over with its keys under the picture (lit as the
    // picture presses them), no words, and F closes them. The chapter 1 starting room's signpost opens a window that goes
    // through the same moves one at a time (TemporaryGameGui.ControlsWindow). The road's two looping ghost demonstrations (wall climb, dash through a shot) carry
    // their keys over the ghost.
    public sealed partial class TemporaryGameGui
    {
        // Card: the picture (MovePicture: hero frames at half the world's 32 px a unit), then the keys. The basic moves keep
        // going off the card's right edge and back in from the left; the dodge and the wall climb stay still on their whole
        // stretch of road (user 2026-11-10). In a chapter the top of the HUD holds the health bar, the money and the
        // minimap: the cards go under.
        const float CardW = 128, CardPicture = 116, CardGroundPixels = 16, CardH = 154, CardGap = 8, CardScale = 16;
        const float VillageCardsTop = 40, CombatCardsTop = 92;
        RectTransform controlsStrip;
        CanvasGroup controlsGroup;
        OpeningControlItem[] controlsItems;
        string controlsSignature;
        float controlsShown = -1, controlsLabelCheck;
        sealed class MoveCard
        {
            public OpeningControlItem item; public StartingVillageWorld.GhostTrack track;
            public MovePicture picture;
            public readonly List<(RectTransform cap, Image face, string code, int same, int order)> caps = new List<(RectTransform, Image, string, int, int)>();
        }
        readonly List<MoveCard> moveCards = new List<MoveCard>();
        Chapter1ControlsSign controlsSign;
        int controlsSignReads, systemSignReads;
        Deviant.Tutorial.Unity.FirstRunTutorialWindow systemTutorialReplay;
        public bool ControlsShowing => controlsStrip && controlsStrip.gameObject.activeSelf;
        // The move names on the cards, in order (empty while they are down).
        public string[] ControlsTexts => ControlsShowing ? System.Array.ConvertAll(controlsItems, i => i.text) : new string[0];
        // Each card's picture now (tests and captures).
        public Sprite[] ControlsPictures => moveCards.ConvertAll(c => c.picture?.Sprite).ToArray();

        // The road's moves, as the opening's script lists them (the signpost shows the same).
        OpeningControlItem[] ScriptedControls()
        {
            foreach (var beat in OpeningData?.beats ?? new OpeningBeat[0])
                foreach (var action in beat.actions ?? new OpeningAction[0])
                    if (action.type == "controls" && action.mode == "show" && action.items != null && action.items.Length > 0) return action.items;
            return null;
        }

        // The body the cards draw: the hero's own in the village (the human on the road), the bare-handed ink body in a chapter.
        CharacterAnimationSet CardBody()
        {
            if (village) return village.HeroAnimations;
            var sets = host && host.Arena ? host.Arena.characterAnimations?.sets : null;
            if (sets == null) return null;
            CharacterAnimationSet found = null;
            foreach (var set in sets) if (set.form == "village_unarmed") found = set;
            if (found == null) foreach (var set in sets) if (set.form == "base_sword") found = set;
            return found;
        }

        void ShowControls(OpeningControlItem[] items)
        {
            if (items == null || items.Length == 0) { HideControls(); return; }
            if (!controlsStrip)
            {
                controlsStrip = Rect("조작 안내", stage, 0, village ? VillageCardsTop : CombatCardsTop, 960, CardH + 28);
                // Its own canvas: the cards redraw every frame and must not rebuild the whole GUI with them (it stuttered).
                controlsStrip.gameObject.AddComponent<Canvas>();
                controlsGroup = controlsStrip.gameObject.AddComponent<CanvasGroup>(); controlsGroup.blocksRaycasts = false;
                // Under the opening's own banner and cards in the village.
                if (village && banner) controlsStrip.SetSiblingIndex(banner.GetSiblingIndex() + 1);
                // In a chapter over the HUD and its paper wash (inside the HUD they came out faded).
            }
            controlsItems = items; controlsShown = Time.unscaledTime;
            if (!village && hud) controlsStrip.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);
            LayoutControls();
            controlsStrip.gameObject.SetActive(true);
            TickControls();
        }

        void HideControls() { if (controlsStrip) controlsStrip.gameObject.SetActive(false); }

        string ControlsSignature() => controlsItems == null ? "" : string.Join("/", System.Array.ConvertAll(controlsItems, i => KeySignature(i.keys))) + "/" + LessonKey("F");

        // The cards side by side at the top centre and 「닫기 - F -」 under them.
        void LayoutControls()
        {
            controlsSignature = ControlsSignature();
            foreach (Transform child in controlsStrip) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            moveCards.Clear();
            var ghosts = StartingVillageWorld.SharedGhosts;
            float total = controlsItems.Length * CardW + (controlsItems.Length - 1) * CardGap, x0 = Mathf.Round(480 - total / 2);
            for (int i = 0; i < controlsItems.Length; i++)
            {
                var item = controlsItems[i];
                var card = new MoveCard { item = item, track = System.Array.Find(ghosts.ghosts ?? new StartingVillageWorld.GhostTrack[0], g => g.id == item.ghost) };
                if (card.track == null || card.track.x == null || card.track.x.Length == 0) { Debug.LogWarning("Controls card ghost missing: " + item.ghost); card.track = null; }
                var root = Rect("Move " + item.text, controlsStrip, x0 + i * (CardW + CardGap), 0, CardW, CardH);
                NeutralPixelPanel(root, 0, 0, CardW, CardH);
                if (card.track != null)
                {
                    card.picture = new MovePicture(this, Rect("Picture", root, 2, 2, CardW - 4, CardPicture - 2), CardGroundPixels, CardScale);
                    var wall = WallFramesOf(card.track);
                    card.picture.Show(card.track, wall.clips, wall.times, StaysStill(card.track, wall.clips) ? MovePicture.Framing.Fixed : MovePicture.Framing.Wrap);
                }
                // Keys centred under the picture; no name (user 2026-11-10: the motion says what it is).
                var keys = Rect("Keys", root, 0, CardPicture + 4, 1, 28);
                float kx = 0;
                for (int k = 0; k < item.keys.Length; k++)
                {
                    string code = card.track?.codes != null && k < card.track.codes.Length ? card.track.codes[k] : item.keys[k];
                    int same = 0, order = 0;
                    for (int j = 0; j < item.keys.Length; j++)
                    {
                        string other = card.track?.codes != null && j < card.track.codes.Length ? card.track.codes[j] : item.keys[j];
                        if (other == code) { same++; if (j <= k) order++; }
                    }
                    float w = Keycap(keys, item.keys[k], kx, 0, out var cap, out var face);
                    card.caps.Add((cap, face, code, same, order)); kx += w + 4;
                }
                kx -= 4;
                keys.sizeDelta = new Vector2(kx, 28); keys.anchoredPosition = new Vector2(Mathf.Round((CardW - kx) / 2), -(CardPicture + 4));
                moveCards.Add(card);
            }
            // A plain line under the cards (user 2026-11-10: no keycap) in the one prompt form of every screen, 「[F] 닫기」
            // (user 2026-11-13).
            var close = BindPrompt(Label("", controlsStrip, 0, CardH + 6, 960, MenuHintHeight, MenuHintFontSize, KeyFace), "[{Interact}] 닫기");
            Deviant.UI.Theme.ApprovedTheme.ControlHint(close);
            close.alignment = TextAnchor.MiddleCenter; close.name = "Close";
            Stroke(close, new Color(23 / 255f, 23 / 255f, 15 / 255f, .6f));
        }

        bool dodgeCardsShowing;
        void TickControls()
        {
            if (village && openingActive)
            {
                bool dodge = village.InDodgePractice;
                if (dodge != dodgeCardsShowing)
                {
                    dodgeCardsShowing = dodge;
                    if (dodge) ShowControls(new[] { new OpeningControlItem { ghost = "dodge", text = "대시 무적", keys = new[] { "Shift" } } });
                    else HideControls();
                }
            }
            if (controlsSign == null && !village && world) controlsSign = world.GetComponent<Chapter1ControlsSign>();
            if (controlsSign && controlsSign.Reads != controlsSignReads)
            {
                controlsSignReads = controlsSign.Reads;
                // The signpost opens the window that goes through the moves one at a time.
                if (!ControlsWindowOpen) OpenControlsWindow();
            }
            if(controlsSign && controlsSign.SystemReads!=systemSignReads)
            {
                systemSignReads=controlsSign.SystemReads;
                if(!systemTutorialReplay)
                {
                    var replay=new GameObject("System tutorial replay");replay.transform.SetParent(transform,false);
                    systemTutorialReplay=replay.AddComponent<Deviant.Tutorial.Unity.FirstRunTutorialWindow>();
                }
                systemTutorialReplay.ShowSystem(PlayerControls.For(world.arena.Player),world.arena.Controls);
            }
            if (!ControlsShowing) return;
            // Key labels change only with the device: look twice a second, not every frame (building them is costly).
            if (Time.unscaledTime >= controlsLabelCheck) { controlsLabelCheck = Time.unscaledTime + .5f; if (controlsSignature != ControlsSignature()) LayoutControls(); }
            float now = Time.unscaledTime, alpha = Mathf.Clamp01((now - controlsShown) / BannerEnter);
            // Cut scenes (the bars) hide it as they hide the banner.
            controlsGroup.alpha = village && (openingLock != null || relockAfterWalk) ? 0 : alpha;
            var body = CardBody(); var climb = WindowClimb(); var player = StripPlayer;
            float fps = StartingVillageWorld.SharedGhosts.fps > 0 ? StartingVillageWorld.SharedGhosts.fps : 60;
            float stamina = player && player.arena != null && player.arena.Movement != null ? player.arena.Movement.WallStaminaDuration : 1;
            foreach (var card in moveCards)
            {
                if (card.picture == null) continue;
                card.picture.Tick(Time.unscaledDeltaTime, body, climb, village && village.HeroBody == "opening_human", player, stamina);
                LightCaps(card.caps, card.track, card.picture.Frame, card.picture.Holding, fps);
            }
        }

        // A move whose point is a place stays still on it: a shot to dodge, a wall to climb.
        static bool StaysStill(StartingVillageWorld.GhostTrack track, string[] wallClips) =>
            track.shotTo > track.shotFrom || wallClips != null && System.Array.Exists(wallClips, c => c != null);

        // The wall frames of a recording (wall contact, kick, ledge), as the road's ghosts find them.
        (string[] clips, float[] times) WallFramesOf(StartingVillageWorld.GhostTrack track)
        {
            var ghosts = StartingVillageWorld.SharedGhosts;
            float kick = CardBody()?.Find(StartingVillageWorld.WallKickClip)?.Duration ?? 0, ledge = WindowClimb()?.inkLedge?.Duration ?? 0;
            StartingVillageWorld.WallFrames(track, ghosts.fps > 0 ? ghosts.fps : 60, kick, ledge, out var clips, out var times);
            return (clips, times);
        }

        // A recorded move's keys, pressed (yellow, sunk) as the recording presses them: a tap for a moment, two of the same
        // key one after the other, a held key other than Space for as long as it is held.
        static void LightCaps(List<(RectTransform cap, Image face, string code, int same, int order)> caps, StartingVillageWorld.GhostTrack track, int frame, bool holding, float fps)
        {
            foreach (var c in caps)
            {
                bool down = false;
                if (frame >= 0 && !holding)
                {
                    int last = -1, count = 0;
                    foreach (int f in TapFrames(track, c.code)) { if (f > frame) break; last = f; count++; }
                    down = last >= 0 && (frame - last) / fps < GhostKeyLit;
                    if (c.same > 1) down = down && c.order == count;
                    if (c.same == 1 && c.code != "Space" && Has(track.held[frame], c.code)) down = true;
                }
                // Only a change touches the cap (moving rects every frame rebuilt the canvas).
                var colour = down ? KeyLit : KeyFace;
                if (c.face.color == colour) continue;
                c.face.color = colour;
                float sunk = down ? 3 : 0;
                c.face.rectTransform.anchoredPosition = new Vector2(2, -(2 + sunk)); c.face.rectTransform.sizeDelta = new Vector2(c.cap.sizeDelta.x - 4, 22 - sunk);
                var frameRect = (RectTransform)c.cap.Find("Frame");
                frameRect.anchoredPosition = new Vector2(0, -sunk); frameRect.sizeDelta = new Vector2(c.cap.sizeDelta.x, 28 - sunk);
                ((RectTransform)c.cap.Find("Text")).anchoredPosition = new Vector2(0, -sunk / 2);
            }
        }

        // The frames at which a recording taps a key, worked out once per track and key.
        static readonly Dictionary<(StartingVillageWorld.GhostTrack, string), int[]> tapFrames = new Dictionary<(StartingVillageWorld.GhostTrack, string), int[]>();
        static int[] TapFrames(StartingVillageWorld.GhostTrack track, string code)
        {
            if (tapFrames.TryGetValue((track, code), out var found)) return found;
            var list = new List<int>();
            for (int f = 0; f < track.tapped.Length; f++) if (Has(track.tapped[f], code)) list.Add(f);
            return tapFrames[(track, code)] = list.ToArray();
        }

        // ---- The road's demonstrations: keys and name over each looping ghost, lit as it presses them.
        sealed class DemoOverlay
        {
            public StartingVillageWorld.GhostDemo demo;
            public RectTransform root; public CanvasGroup group;
            public readonly List<(RectTransform cap, Image face, string code, int same, int order)> caps = new List<(RectTransform, Image, string, int, int)>();
            public string signature; public float labelCheck;
        }
        readonly List<DemoOverlay> demoOverlays = new List<DemoOverlay>();

        void PlayDemo(string ghost, string[] keys, string text)
        {
            if (ghost == null) { village.ClearDemos(); ClearDemoOverlays(); return; }
            var demo = village.PlayDemo(ghost, keys, text);
            if (demo == null || ghost == "dodge") return;
            var overlay = new DemoOverlay { demo = demo, root = Rect("시범 유령 키 " + ghost, stage, 0, 0, 1, 28) };
            overlay.root.gameObject.AddComponent<Canvas>(); // follows the ghost every frame: its own canvas
            overlay.group = overlay.root.gameObject.AddComponent<CanvasGroup>(); overlay.group.blocksRaycasts = false;
            if (banner) overlay.root.SetSiblingIndex(banner.GetSiblingIndex());
            demoOverlays.Add(overlay);
            LayoutDemoOverlay(overlay);
        }

        void ClearDemoOverlays()
        {
            foreach (var o in demoOverlays) if (o.root) Destroy(o.root.gameObject);
            demoOverlays.Clear();
        }

        void LayoutDemoOverlay(DemoOverlay o)
        {
            var keys = o.demo.Keys; var track = o.demo.Track;
            o.signature = KeySignature(keys);
            foreach (Transform child in o.root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            o.caps.Clear();
            float x = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                string code = track.codes != null && i < track.codes.Length ? track.codes[i] : keys[i];
                int same = 0, order = 0;
                for (int j = 0; j < keys.Length; j++) { string other = track.codes != null && j < track.codes.Length ? track.codes[j] : keys[j]; if (other == code) { same++; if (j <= i) order++; } }
                float w = Keycap(o.root, keys[i], x, 0, out var cap, out var face);
                o.caps.Add((cap, face, code, same, order)); x += w + 4;
            }
            o.root.sizeDelta = new Vector2(Mathf.Max(0, x - 4), 28);
        }

        void TickDemoOverlays()
        {
            if (!village) return;
            var view = village.World.View; var file = village.Ghosts;
            foreach (var o in demoOverlays)
            {
                if (!o.root) continue;
                if (Time.unscaledTime >= o.labelCheck) { o.labelCheck = Time.unscaledTime + .5f; if (o.signature != KeySignature(o.demo.Keys)) LayoutDemoOverlay(o); }
                // Over the ghost's head, following it; hidden while the ghost is off screen and in the cut scenes.
                var feet = o.demo.Feet;
                var screen = view.WorldToViewportPoint(new Vector3(feet.x, feet.y + 3.1f, 0));
                bool inView = screen.x > -.05f && screen.x < 1.05f && screen.y > 0 && screen.y < 1.1f;
                float hx = screen.x * 960, hy = (1 - screen.y) * 540, total = o.root.sizeDelta.x;
                o.root.anchoredPosition = new Vector2(Mathf.Round(hx - total / 2), -Mathf.Round(Mathf.Max(ControlsShowing ? VillageCardsTop + CardH + 34 : 70, hy - 28)));
                o.group.alpha = inView && openingLock == null && !relockAfterWalk ? Mathf.Max(.35f, o.demo.Visibility) : 0;
                LightCaps(o.caps, o.demo.Track, o.demo.Frame, o.demo.Holding, file.fps);
            }
        }
    }
}
