using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Playable first visit (docs/design/story/opening/opening-playable.md, data/story/opening_playable.json): the player walks the
    // road to the village while guide cards teach each movement and the lines float above the speaker's head; two
    // bystanders are swallowed by the erasure, which chases the hero to the well (a catch fades out and starts the chase
    // again at its checkpoint); there the fronts close in, the ink helper gives its ink and the hero wakes up (parts 1-3).
    // Part 4 guides the hero to the departure sign; the farmhouse is optional and shows its own unread-record marker.
    // Departing records the opening as seen. Holding Esc on the learning road (before the chase runs) starts the chase where
    // 「뛰기」 shows; later, before part 4, it skips to the well.
    public sealed partial class TemporaryGameGui
    {
        const int OpeningLastPart = 4, VillagePart = 4;
        // The F that read the last line must not also open the station the hero stands at.
        const float OpeningStationDelay = .3f;
        float openingStationReadyAt;
        // A locked stretch that walks the hero (a visit's walk to a workbench) lifts the lock while the walk drives the hero:
        // a combat block would clear the walk's own input, and the walk ignores the keys anyway.
        bool relockAfterWalk;
        const float ShakeSeconds = .4f;
        const float OpeningSkipSeconds = 1f, OpeningSkipDelay = .2f, BubbleCharsPerSecond = 30, BannerEnter = .2f, BannerLeave = .5f, BannerNudgeEvery = 15f;
        const float BannerTop = 32, BannerHeight = 60;

        OpeningScript openingScript;
        OpeningDirector opening;
        bool openingActive, openingChecked;
        IControlScope openingLock;
        float escHeldSince = -1, shakeUntil = -1, shakeAmp = 2;
        bool erasureMetSent;

        // A catch in the chase: the hero stops (and fades on the erasure's touch), the screen goes dark, the chase starts
        // again at its checkpoint and the screen comes back.
        const float CaughtDarkAt = 1, CaughtDarken = .3f, CaughtRespawnAt = 2.1f, CaughtBrighten = .45f, CaughtDrain = .35f;
        Image caughtFade;
        IControlScope caughtLock;
        float caughtAt = -1, respawnedAt = -1;
        string caughtLine;
        readonly Dictionary<string, string> speakerNames = new Dictionary<string, string>();
        GameObject skipHint;
        Text skipKeyHint;
        Image skipFill;

        // Every speaker's bubble (the maintenance shops' bubble); its canvas sits just under the GUI's, so the banner,
        // guide card, fades and menus stay in front of it.
        MaintenanceBubbleView bubbleView;
        const int BubbleCanvasOrder = 99;
        string bubbleLine, bubbleSpeaker;
        bool bubbleAdvance, bubbleShown;
        float bubbleStarted;

        RectTransform banner, bannerKeys;
        CanvasGroup bannerGroup;
        Text bannerText;
        readonly List<Image> bannerKeyFrames = new List<Image>();
        (string[] keys, string text)? pendingBanner;

        // Guide card (HTML opening prototype): the game pauses behind a dim layer until Space, F or Enter confirms it.
        RectTransform lessonCard, lessonKeys;
        GameObject lessonDim;
        Text lessonText, lessonConfirm;
        IControlScope lessonPause;
        float lessonShown = -1;
        const float LessonConfirmDelay = .2f, LessonEnter = .15f;
        public string LessonShowing => ghostLesson != null ? ghostLesson.text : lessonCard && lessonCard.gameObject.activeSelf ? lessonText.text : null;
        float bannerShown = -1, bannerDone = -1, bannerNudged;

        public bool OpeningActive => openingActive;
        // Part 4 plays in the ordinary village: its HUD, stations and departure are the player's again. So do the visits
        // and the first return.
        bool OpeningInVillage => openingActive && opening != null && (visitPiece > 0 || returnPlaying || opening.Part >= VillagePart);
        // Holding Esc skips the opening up to its village part, and a visit or the first return at any point.
        bool SkipAllowed => visitPiece > 0 || returnPlaying || !OpeningInVillage;
        // The departure needs the opening seen, except from inside its own last part.
        bool DepartureLocked => StoryRequired && !OpeningInVillage;
        public string BannerShowing => banner && banner.gameObject.activeSelf ? bannerText.text : null;
        // The line on screen.
        public string BubbleShowing => bubbleLine;

        OpeningScript OpeningData
        {
            get
            {
                if (openingChecked) return openingScript;
                openingChecked = true;
                var text = Resources.Load<TextAsset>("HubOpening");
                if (!text) { Debug.LogError("HubOpening resource is missing; run tools/assets/build_hub_bindings.py."); return null; }
                var script = JsonUtility.FromJson<OpeningScript>(text.text);
                var errors = OpeningScriptRules.Validate(script);
                if (errors.Count > 0) { Debug.LogError("HubOpening is invalid: " + string.Join("; ", errors)); return null; }
                return openingScript = script;
            }
        }

        void BuildOpening()
        {
            caughtFade = Picture("", stage, 0, 0, 960, 540, Color.black);
            caughtFade.sprite = null; caughtFade.name = "추격 암전"; caughtFade.gameObject.SetActive(false);

            // The top strip (HTML drawBanner): a full-width dark band 60 px high from y 32, darkest (.45) in its middle,
            // keycaps on paper and the text in paper white with a dark rim, all centred on y 62.
            banner = Rect("튜토리얼 안내", stage, 0, BannerTop, 960, BannerHeight);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>(); bannerGroup.blocksRaycasts = false;
            var band = Plain(banner, 0, 0, 960, BannerHeight, StripInk, "Frame"); band.sprite = StripSprite();
            bannerKeys = Rect("Keys", banner, 0, BannerHeight / 2 - 14, 1, 28);
            bannerText = Label("", banner, 0, BannerHeight / 2 - 18, 300, 36, 20, KeyFace);
            Stroke(bannerText, new Color(23 / 255f, 23 / 255f, 15 / 255f, .6f));
            banner.gameObject.SetActive(false);

            lessonDim = Picture("", stage, 0, 0, 960, 540, new Color(12 / 255f, 10 / 255f, 9 / 255f, .45f)).gameObject;
            lessonDim.GetComponent<Image>().sprite = null; lessonDim.name = "조작 안내 배경"; lessonDim.SetActive(false);
            // The small lines are 16 px (playtest 2026-11-04): 12 px broke apart on the pixel canvas.
            lessonCard = Rect("조작 안내", stage, 300, 184, 360, 140);
            Picture("panel", lessonCard, 0, 0, 360, 140).name = "Frame";
            Label("조작 안내", lessonCard, 0, 10, 360, 22, 16, Muted).alignment = TextAnchor.MiddleCenter;
            lessonKeys = Rect("Keys", lessonCard, 0, 44, 1, 36);
            lessonText = Label("", lessonCard, 0, 42, 300, 40, 24);
            lessonConfirm = BindPrompt(Label("", lessonCard, 0, 104, 360, 22, 16), "[{Advance}] 확인"); lessonConfirm.alignment = TextAnchor.MiddleCenter;
            lessonCard.gameObject.SetActive(false);

            // Quiet corner text, with only the hold-progress line beneath it.
            skipHint = Rect("건너뛰기 안내", stage, 788, 500, 160, 30).gameObject;
            skipKeyHint = BindPrompt(Label("", skipHint.transform, 0, 4, 160, MenuHintHeight, MenuHintFontSize, new Color32(238, 232, 215, 230)), "[{Pause}] 건너뛰기"); skipKeyHint.alignment = TextAnchor.MiddleRight;
            Deviant.UI.Theme.ApprovedTheme.ControlHint(skipKeyHint);
            Stroke(skipKeyHint, new Color(0, 0, 0, .6f));
            skipFill = Picture("", skipHint.transform, 0, 25, 0, 2, new Color32(238, 232, 215, 220));
            skipFill.sprite = null; skipFill.type = Image.Type.Simple;
            skipHint.SetActive(false);
        }

        // First visit only: true when the playable opening took over the story check.
        bool StartOpening()
        {
            if (openingActive) return true;
            var script = OpeningData; if (script == null) return false;
            openingActive = true; escHeldSince = -1; erasureMetSent = false; caughtAt = respawnedAt = -1;
            var chase = script.chase;
            caughtLine = chase?.caughtLine;
            speakerNames.Clear();
            foreach (var entry in script.speakerNames ?? new OpeningSpeakerName[0]) if (!string.IsNullOrEmpty(entry?.speaker)) speakerNames[entry.speaker] = entry.name;
            // The hero is a human until the helper's ink revives it as the ink body.
            village.SetHeroBody(village.OpeningBody);
            // Before the road: the bystanders take their erasure lag from it.
            village.ConfigureChase(chase == null ? null : new StartingVillageWorld.ChaseTuning
            {
                checkpointX = chase.checkpointX, checkpointBack = chase.checkpointBack, catchMargin = chase.catchMargin,
                groundLead = chase.groundLead, objectLagMin = chase.objectLagMin, objectLagMax = chase.objectLagMax, slow = chase.slow,
                leashFar = chase.leash.far, leashK = chase.leash.k, leashMax = chase.leash.max,
                biteHold = chase.bite.hold, biteShake = chase.bite.shake, biteAmp = chase.bite.amp,
                speeds = System.Array.ConvertAll(chase.speeds, z => new Vector2(z.fromX, z.speed)),
                surge = System.Array.ConvertAll(chase.surge, z => new Vector2(z.seconds, z.rate)),
            });
            var broom = script.broom;
            village.ConfigureBroom(broom == null || string.IsNullOrEmpty(broom.art) ? null : new StartingVillageWorld.BroomTuning
            { art = broom.art, hp = broom.hp, walk = broom.walk, near = broom.near, windup = broom.windup, strike = broom.strike, cool = broom.cool, reach = broom.reach });
            if (script.course != null)
                village.BuildOpeningCourse(script.course.leftX, script.course.cameraLeftX, System.Array.ConvertAll(script.course.pieces ?? new OpeningCoursePiece[0],
                    p => new StartingVillageWorld.CoursePiece { kind = p.kind, id = p.id, art = p.art, name = p.name, x = p.x, top = p.top, width = p.width, bottom = p.bottom, face = p.face, phase = p.phase }));
            opening = new OpeningDirector(script, new OpeningStage(this), OpeningLastPart);
            return true;
        }

        // Runs while the opening owns the lobby. Returns false while a pause menu is up, so the usual Esc handling closes it.
        bool OpeningUpdate(Keyboard k)
        {
            skipHint.SetActive(!hubOpen && !hubPaused && SkipAllowed && !notebookOpen && ghostLesson == null);
            if (hubOpen || hubPaused) { escHeldSince = -1; if (bubbleView) bubbleView.Hide(); return false; }
            var input = Controls;
            if (input)
            {
                skipKeyHint.text = Prompt("[{Pause}] 건너뛰기");
                lessonConfirm.text = Prompt("[{Advance}] 확인");
                // The prologue book owns Back through its cover, all three pages and closing animation.
                // Consume it and clear the gesture so a release cannot pause or skip the playable opening.
                if (notebookOpen) { input.TakeBack(); escHeldSince = -1; }
                else if (input.Pressed(GameAction.Pause))
                { input.TakeBack(); escHeldSince = Time.unscaledTime; }
                if (escHeldSince >= 0)
                {
                    if (!input.Read(GameAction.Pause).IsPressed)
                    {
                        bool tap = Time.unscaledTime - escHeldSince < OpeningSkipDelay;
                        escHeldSince = -1; skipFill.rectTransform.sizeDelta = new Vector2(0, 2);
                        // Once a hold has begun, releasing it cancels only that gesture.
                        if (tap) { HubBack(); return true; }
                    }
                    else if (Time.unscaledTime - escHeldSince >= OpeningSkipSeconds && SkipAllowed) { escHeldSince = -1; if (!JumpToChase()) opening.Skip(); }
                }
                bool confirm = input.Pressed(GameAction.Advance);
                if (notebookOpen) { }
                else if (ghostLesson != null) TickGhostLesson(k);
                else if (LessonShowing != null) { if (confirm && Time.unscaledTime - lessonShown >= LessonConfirmDelay) { TakeAdvance(); CloseLesson(); } }
                else if (ControlsShowing && input.Take(GameAction.Interact)) { input.SuppressGameplay(); HideControls(); }
                else if (bubbleAdvance && confirm) { TakeAdvance(); AdvanceBubble(); }
                else if (input.Take(GameAction.Weapon1Basic) && openingLock == null && !relockAfterWalk) village.TryPunch();
            }
            skipFill.rectTransform.sizeDelta = new Vector2(escHeldSince < 0 ? 0 : 160 * Mathf.Clamp01((Time.unscaledTime - escHeldSince - OpeningSkipDelay) / (OpeningSkipSeconds - OpeningSkipDelay)), 2);
            // The village reports the erasure; the director turns it into the beats waiting for it.
            if (village.ErasureMet && !erasureMetSent) { erasureMetSent = true; opening.Notify("erasureMet"); }
            // The learning road: a fall into a pit stands the hero again just before it (in the chase it is a catch).
            if (village.CatchPitFall()) Shake(.15f, 2);
            if (openingLock != null && village.ScriptedWalking) { openingLock.Dispose(); openingLock = null; relockAfterWalk = true; }
            else if (relockAfterWalk && !village.ScriptedWalking) { relockAfterWalk = false; LockOpening(true); }
            if (village.TakeBite(out float biteSeconds, out float biteAmp)) Shake(biteSeconds, biteAmp);
            if (village.TakeShake(out float askedSeconds, out float askedAmp)) Shake(askedSeconds, askedAmp);
            var caught = village.TakeCaught();
            if (caught != null) BeginCaught(caught);
            TickCaught();
            opening.Tick(Time.deltaTime);
            TickNotebook(k); if (ghostLesson != null && k == null) TickGhostLesson(null);
            TickBubble(); TickBanner(); TickLesson(); TickControls(); TickDemoOverlays();
            int amp = Mathf.RoundToInt(shakeAmp);
            if (worldOutput) worldOutput.ShakePixels = Time.unscaledTime < shakeUntil ? new Vector2(UnityEngine.Random.Range(-amp, amp + 1), UnityEngine.Random.Range(-amp, amp + 1)) : Vector2.zero;
            if (opening.Outcome != OpeningOutcome.Running) FinishOpening();
            else if (visitPiece > 0 && opening.Finished.Count == visitBeats) FinishVisit(false);
            else if (returnPlaying && opening.Finished.Count == returnBeats) FinishFirstReturn(false);
            return true;
        }

        void Shake(float seconds, float amplitude) { shakeUntil = Time.unscaledTime + seconds; shakeAmp = amplitude; }

        // On the learning road, until the chase runs, a skip starts the chase where its 「뛰기」 shows (skip.beat): whatever
        // was up (the notebook, a line, a cut scene) goes and the beat puts the hero and the held front in place.
        bool JumpToChase()
        {
            string beat = OpeningData?.skip?.beat;
            if (string.IsNullOrEmpty(beat) || visitPiece > 0 || returnPlaying || opening.Part > 2 || opening.Started(beat) ||
                opening.Started("erasure-close") || village.ErasureChasing) return false;
            CloseNotebook(); CloseLesson(); HideBubble(); HideControls();
            pendingBanner = null; banner.gameObject.SetActive(false); bannerDone = -1;
            openingLock?.Dispose(); openingLock = null; relockAfterWalk = false;
            caughtLock?.Dispose(); caughtLock = null; caughtAt = respawnedAt = -1; caughtFade.gameObject.SetActive(false);
            village.FocusCamera(null); village.ZoomCamera(1);
            if (!opening.JumpTo(beat)) return false;
            opening.Tick(0);
            return true;
        }

        void BeginCaught(string kind)
        {
            caughtAt = village.SceneTime; respawnedAt = -1;
            caughtLock?.Dispose();
            caughtLock = village.World.arena.Controls.OpenScope("opening caught");
            caughtLock.BlockInput(InputChannels.Combat | InputChannels.Interaction);
            PresentationSfx.Play("story-collapse");
            if (kind == "erase")
            {
                village.DrainHero(CaughtDrain);
                if (!string.IsNullOrEmpty(caughtLine) && !bubbleAdvance) ShowBubble("hero", caughtLine, false);
            }
        }

        void TickCaught()
        {
            float now = village.SceneTime, dark = 0;
            if (caughtAt >= 0)
            {
                float t = now - caughtAt;
                dark = Mathf.Clamp01((t - CaughtDarkAt) / CaughtDarken);
                if (t >= CaughtRespawnAt)
                {
                    village.RespawnAtCheckpoint();
                    if (bubbleLine == caughtLine) HideBubble();
                    caughtLock?.Dispose(); caughtLock = null;
                    caughtAt = -1; respawnedAt = now; dark = 1;
                }
            }
            else if (respawnedAt >= 0)
            {
                dark = 1 - Mathf.Clamp01((now - respawnedAt) / CaughtBrighten);
                if (dark <= 0) respawnedAt = -1;
            }
            caughtFade.gameObject.SetActive(dark > 0);
            caughtFade.color = new Color(0, 0, 0, dark);
        }

        // Skipped, failed or handed off: the opening ends and, unless the hero already walks the village, control starts
        // at the well.
        void FinishOpening()
        {
            if (opening.Outcome == OpeningOutcome.Failed) Debug.LogError("Playable opening stopped: " + opening.Error);
            if (visitPiece > 0) { FinishVisit(true); return; }
            if (returnPlaying) { FinishFirstReturn(true); return; }
            bool inVillage = OpeningInVillage;
            CloseOpening();
            if (inVillage) return;
            var well = village.Find(StartingVillageWorld.ReturnWell);
            if (well != null)
            {
                var player = village.World.arena.Player;
                player.Position = well.Position; player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                village.World.RefreshCamera(0, true);
            }
        }

        // Records the opening as seen and gives the village back: road, erasure, banners and locks go, every movement opens
        // and the hero is the bare-handed ink body.
        void CloseOpening()
        {
            openingActive = false; escHeldSince = -1; skipHint.SetActive(false);
            HideBubble(); pendingBanner = null; banner.gameObject.SetActive(false); bannerDone = -1; CloseLesson(); CloseNotebook();
            HideControls(); ClearDemoOverlays();
            openingLock?.Dispose(); openingLock = null; relockAfterWalk = false;
            caughtLock?.Dispose(); caughtLock = null; caughtAt = respawnedAt = -1; caughtFade.gameObject.SetActive(false);
            if (worldOutput) worldOutput.ShakePixels = Vector2.zero;
            village.EndOpening();
            if (Story != null && !Profile.MarkStorySeen(Story.id) && !Profile.Snapshot.HasSeen(Story.id))
                Debug.LogWarning("Opening was not recorded: " + Profile.LastSaveError);
            Awaken();
        }

        // Everything the opening kept locked is learned by now (and a skipped opening must not leave the hero stuck); the
        // ink body, bare-handed as everywhere in the village (a skipped opening never reached the revive).
        void Awaken()
        {
            village.World.arena.Player.SetMovementAbilities(MovementAbilities.All);
            village.SetHeroBody(village.VillageBody);
        }

        // The village side reports what the player did at a station; true when the opening keeps the station closed for now.
        bool OpeningStation(StartingVillageWorld.Station station)
        {
            if (!openingActive) return false;
            if (!OpeningInVillage || Time.unscaledTime < openingStationReadyAt) return true;
            opening.Interacted(station.Id);
            // Leaving from the sign ends the opening before the unarmed run starts.
            if (station.Id == StartingVillageWorld.DepartureSign)
            {
                // Leaving while the first return's banners are still up: it has been seen.
                if (returnPlaying) MarkFirstReturnSeen();
                else if (visitPiece == 0) { opening.Tick(0); CloseOpening(); }
            }
            return false;
        }

        void LockOpening(bool locked)
        {
            openingLock?.Dispose(); openingLock = null; relockAfterWalk = false;
            if (!locked) { openingStationReadyAt = Time.unscaledTime + OpeningStationDelay; return; }
            var scope = village.World.arena.Controls.OpenScope("opening");
            scope.BlockInput(InputChannels.Combat | InputChannels.Interaction); openingLock = scope;
        }

        // A screen closed with its goal met (the record read, two blueprints confirmed).
        void OpeningScreenClosed(string screen) { if (openingActive) opening.ScreenClosed(screen); }

        void DisposeOpening() { openingLock?.Dispose(); openingLock = null; caughtLock?.Dispose(); caughtLock = null; openingActive = false; }

        // Same as holding Esc (review captures and tests that start from the lobby).
        void SkipOpening() { if (!openingActive) return; opening.Skip(); FinishOpening(); }

        // ---- Speech bubble: every speaker talks in the maintenance shops' bubble (MaintenanceBubbleView: Galmuri11 Bold,
        // 184 px lines, stepped 2 px border, name tag on the border, as the HTML opening prototype draws it) over its head,
        // typed out, then held for a reading time. The artisan also has its letter sound (TemporaryGameGui.ArtisanBubble).

        void ShowBubble(string speaker, string text, bool advance)
        {
            bubbleSpeaker = speaker; bubbleLine = text; bubbleAdvance = advance; bubbleShown = false; bubbleStarted = village.SceneTime;
            if (!bubbleView)
            {
                bubbleView = MaintenanceBubbleView.Create(village.World.View);
                bubbleView.transform.SetParent(village.transform, false);
                bubbleView.GetComponent<Canvas>().sortingOrder = BubbleCanvasOrder;
            }
            StartArtisanLine();
            TickBubble();
        }

        void AdvanceBubble()
        {
            if (!bubbleShown) { bubbleStarted = -1000; return; }
            PresentationSfx.PlaySelected("sfx_ui_dialogue_next");
            HideBubble();
        }

        void HideBubble() { bubbleLine = null; bubbleAdvance = false; if (bubbleView) bubbleView.Hide(); }

        const float SilentDotsPerSecond = 2.5f;
        static bool SilentLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            foreach (char c in line) if (c != '.' && c != '…') return false;
            return true;
        }

        void TickBubble()
        {
            if (bubbleLine == null) return;
            float now = village.SceneTime, age = now - bubbleStarted;
            // A line of only dots (the ink hero does not talk to people, playtest 2026-11-06) fills one dot at a time, as if
            // it were trying to say something.
            float rate = SilentLine(bubbleLine) ? SilentDotsPerSecond : BubbleCharsPerSecond;
            int chars = Mathf.Min(bubbleLine.Length, Mathf.FloorToInt(age * rate) + 1);
            bubbleShown = chars >= bubbleLine.Length;
            if (!bubbleAdvance && age > bubbleLine.Length / rate + 1.2f + .06f * bubbleLine.Length) { HideBubble(); return; }
            // Non-hero speakers carry a name tag; the hero speaks without one.
            string name = village.ResidentName(bubbleSpeaker) ?? (speakerNames.TryGetValue(bubbleSpeaker, out var tag) ? tag : bubbleSpeaker == ArtisanSpeaker ? "마을 장인" : null);
            var head = SpeakerTop();
            // Never over the scene bars or the banner (the bubble stays above the speaker, pushed down if need be).
            int top = (banner.gameObject.activeSelf ? (int)(BannerTop + BannerHeight) + 4 : Mathf.RoundToInt(sceneBars) + 6) + (name != null ? 15 : 0);
            bubbleView.Show(bubbleLine, chars, name, village.SketchAt(head.x), head, now, bubbleAdvance, top);
            if (bubbleSpeaker == ArtisanSpeaker) TickArtisanLine(chars);
        }

        // Where the bubble points: the hero's hurtbox top (the drawn sprite carries weapon space), the helper's drawn top,
        // or the artisan's or a resident's head.
        Vector2 SpeakerTop()
        {
            var player = village.World.arena.Player;
            if (bubbleSpeaker == "helper" && village.HelperShown) return village.HelperTop + new Vector2(0, .2f);
            var resident = bubbleSpeaker == ArtisanSpeaker && village.ArtisanShown ? village.ArtisanTop : village.ResidentTop(bubbleSpeaker);
            return resident ?? new Vector2(player.Center.x, player.Center.y + player.size.y * .5f);
        }

        // ---- Guide card: keys and the action in the middle of the screen; the game waits behind it.

        void ShowLesson(string[] keys, string text, string ghost)
        {
            var track = string.IsNullOrEmpty(ghost) ? null : village.FindGhost(ghost);
            if (!string.IsNullOrEmpty(ghost) && track == null) Debug.LogWarning("Opening ghost missing: " + ghost);
            if (track != null) { ShowGhostLesson(track, keys, text); return; }
            ShowLesson(keys, text);
        }

        void ShowLesson(string[] keys, string text)
        {
            lessonDisplayKeys = keys; lessonText.text = text;
            LayoutLessonKeys();
            lessonShown = Time.unscaledTime;
            lessonDim.SetActive(true); lessonCard.gameObject.SetActive(true);
            lessonPause?.Dispose();
            lessonPause = village.World.arena.Controls.OpenScope("opening lesson");
            lessonPause.PauseSimulation(); lessonPause.BlockInput(InputChannels.Combat | InputChannels.Interaction);
            TickLesson();
        }
        void LayoutLessonKeys()
        {
            var keys = lessonDisplayKeys;
            lessonKeySignature = KeySignature(keys);
            foreach (Transform child in lessonKeys) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            float x = 0;
            foreach (var key in keys)
            {
                string display = LessonKey(key);
                float w = Mathf.Max(36, display.Length * 9 + 18);
                var frame = Picture("button", lessonKeys, x, 0, w, 36);
                Label(display, frame.transform, 0, 0, w, 36, 16).alignment = TextAnchor.MiddleCenter;
                x += w + 6;
            }
            lessonText.color = Cream;
            lessonText.horizontalOverflow = HorizontalWrapMode.Overflow;
            float textWidth = Mathf.Ceil(lessonText.preferredWidth), line = x + (keys.Length > 0 ? 8 : 0) + textWidth;
            float width = Mathf.Max(360, line + 64), left = Mathf.Round((width - line) / 2);
            lessonCard.sizeDelta = new Vector2(width, 140); lessonCard.anchoredPosition = new Vector2(Mathf.Round(480 - width / 2), -184);
            lessonCard.Find("Frame").GetComponent<RectTransform>().sizeDelta = new Vector2(width, 140);
            foreach (var label in lessonCard.GetComponentsInChildren<Text>()) if (label != lessonText && label.transform.parent == lessonCard) label.rectTransform.sizeDelta = new Vector2(width, label.rectTransform.sizeDelta.y);
            lessonKeys.anchoredPosition = new Vector2(left, -44);
            lessonText.rectTransform.anchoredPosition = new Vector2(left + line - textWidth, -42); lessonText.rectTransform.sizeDelta = new Vector2(textWidth + 4, 40);
        }

        void CloseLesson()
        {
            lessonPause?.Dispose(); lessonPause = null;
            HideGhostLesson();
            if (lessonDim) lessonDim.SetActive(false);
            if (lessonCard) lessonCard.gameObject.SetActive(false);
        }

        void TickLesson()
        {
            if (LessonShowing == null) return;
            float now = Time.unscaledTime, a = Mathf.Clamp01((now - lessonShown) / LessonEnter);
            lessonDim.GetComponent<Image>().color = new Color(12 / 255f, 10 / 255f, 9 / 255f, .45f * a);
            lessonConfirm.color = (now % 1) < .65f ? Cream : Muted;
        }

        // ---- Tutorial banner: one at a time at the top centre; a finished one shows its keys pressed before it leaves.

        void ShowBanner(string[] keys, string text)
        {
            if (bannerDone >= 0) { pendingBanner = (keys, text); return; }
            bannerDisplayKeys = keys; bannerText.text = text;
            bannerShown = bannerNudged = Time.unscaledTime; bannerDone = -1;
            LayoutBannerKeys();
            banner.gameObject.SetActive(true); TickBanner();
        }
        void LayoutBannerKeys()
        {
            var keys = bannerDisplayKeys;
            bannerKeySignature = KeySignature(keys);
            foreach (Transform child in bannerKeys) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            bannerKeyFrames.Clear();
            float x = 0;
            foreach (var key in keys)
            {
                float w = Keycap(bannerKeys, key, x, 0, out _, out var face); bannerKeyFrames.Add(face);
                x += w + 4;
            }
            bannerText.color = KeyFace;
            if (bannerDone >= 0) foreach (var face in bannerKeyFrames) face.color = KeyLit;
            bannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            float textWidth = Mathf.Ceil(bannerText.preferredWidth), keysWidth = x + (keys.Length > 0 ? 4 : 0);
            float left = Mathf.Round(480 - (keysWidth + textWidth) / 2);
            bannerKeys.anchoredPosition = new Vector2(left, -(BannerHeight / 2 - 14));
            bannerText.rectTransform.anchoredPosition = new Vector2(left + keysWidth, -(BannerHeight / 2 - 18)); bannerText.rectTransform.sizeDelta = new Vector2(textWidth + 4, 36);
        }

        // A finished banner lights its keys yellow, then fades after half a second.
        void CompleteBanner()
        {
            if (!banner.gameObject.activeSelf || bannerDone >= 0) return;
            bannerDone = Time.unscaledTime;
            foreach (var face in bannerKeyFrames) face.color = KeyLit;
        }

        void TickBanner()
        {
            if (!banner.gameObject.activeSelf) return;
            float now = Time.unscaledTime, alpha = Mathf.Clamp01((now - bannerShown) / BannerEnter);
            if (bannerDone >= 0)
            {
                float leave = Mathf.Clamp01((now - bannerDone - BannerLeave) / BannerLeave);
                alpha = Mathf.Min(alpha, 1 - leave);
                if (leave >= 1)
                {
                    banner.gameObject.SetActive(false); bannerDone = -1;
                    if (pendingBanner.HasValue) { var next = pendingBanner.Value; pendingBanner = null; ShowBanner(next.keys, next.text); }
                    return;
                }
            }
            // Cut scenes (the bars) hide the strip, as the prototype does.
            bannerGroup.alpha = openingLock != null || relockAfterWalk ? 0 : alpha;
        }

        // The village side of the opening, as the director sees it.
        sealed class OpeningStage : IOpeningStage
        {
            readonly TemporaryGameGui gui;
            public OpeningStage(TemporaryGameGui gui) { this.gui = gui; }
            StartingVillageWorld Lobby => gui.village;
            public float PlayerX => Lobby.World.arena.Player.Position.x;
            public float PlayerY => Lobby.World.arena.Player.Position.y - Lobby.WalkingLine;
            public bool Grounded => Lobby.World.arena.Player.grounded;
            public bool NotebookOpen => gui.notebookOpen;
            public bool BroomStanding => Lobby.BroomStanding;
            public void Notebook() => gui.ShowNotebook();
            public void Broom(string mode, float x) { if (mode == "fight") Lobby.FightBroom(); else Lobby.AppearBroom(x); }
            public void Mark(string station) => Lobby.MarkedStation = string.IsNullOrEmpty(station) ? null : station;
            public void Guide(string station) => Lobby.GuideStation = string.IsNullOrEmpty(station) ? null : station;
            public void InkFlicker() => Lobby.InkFlicker();
            public void Face(int face) => Lobby.World.arena.Player.facing = face;
            public void Focus(float x, float heroOffset) => Lobby.FocusCamera(!float.IsNaN(x) ? x : !float.IsNaN(heroOffset) ? Lobby.World.arena.Player.Position.x + heroOffset : (float?)null);
            public void Zoom(float zoom) => Lobby.ZoomCamera(zoom);
            public void Exclaim() => Lobby.ArtisanExclaim();
            public void HeroExclaim() => Lobby.HeroExclaim();
            public void Pigment() => gui.RevealPigment();
            public void Controls(OpeningControlItem[] items) => gui.ShowControls(items);
            public void Demo(string ghost, string[] keys, string text) => gui.PlayDemo(ghost, keys, text);
            public int JumpsUsed => Lobby.World.arena.Player.jumps.Used;
            // The recovery after a dash also counts, so a dash cut short by a wall still finishes the lesson.
            public bool Dashing => Lobby.World.arena.Player.Dashing || Lobby.World.arena.Player.DashRemaining > 0;
            public bool Walking => Lobby.ScriptedWalking;
            public bool BubbleWaiting => gui.bubbleAdvance && gui.bubbleLine != null;
            public bool Animating => Lobby.HeroAnimating || Lobby.HelperAnimating || gui.VisitAnimating;
            public bool LessonOpen => gui.LessonShowing != null;
            public float ErasureLeft => Lobby.ErasureLeft;
            public void Lesson(string[] keys, string text, string ghost) => gui.ShowLesson(keys, text, ghost);
            public void Bubble(string speaker, string text, bool advance) => gui.ShowBubble(speaker, text, advance);
            public void Banner(string[] keys, string text) => gui.ShowBanner(keys, text);
            public void BannerDone() => gui.CompleteBanner();
            public void Village(string color, bool interactables)
            {
                if (color == "allColor") Lobby.SetSketch(false); else if (color == "allSketch") Lobby.SetSketch(true); else Lobby.ClearPreview();
                Lobby.StationsSuppressed = !interactables;
            }
            public void Walk(float fromX, float toX) => Lobby.StartScriptedWalk(fromX, toX);
            public void LockInput(bool locked) => gui.LockOpening(locked);
            public void Sfx(string id) => PresentationSfx.Play(id);
            public void Shake(float seconds) => gui.Shake(seconds > 0 ? seconds : ShakeSeconds, 2);
            public void Fear(float seconds, float interval) => Lobby.StartHeroFear(seconds, interval);
            public void ErasureChase(float behind, float holdAt, float slowFromX, float rollIn) => Lobby.StartErasureChase(behind, holdAt, slowFromX, rollIn);
            public void ErasureGo() => Lobby.GoErasure();
            public void ErasureRelease(float graceSeconds) => Lobby.ReleaseErasure(graceSeconds);
            public void ErasureClose(float rightAhead, float rightSpeed, float leftSpeed) => Lobby.CloseErasure(rightAhead, rightSpeed, leftSpeed);
            public void ErasureMeet(float seconds) => Lobby.MeetErasure(seconds);
            public void HeroSketch(string mode) { if (mode == "revive") Lobby.StartHeroRevive(); else Lobby.StartHeroSketch(); }
            public void Awaken()
            {
                gui.caughtLock?.Dispose(); gui.caughtLock = null; gui.caughtAt = gui.respawnedAt = -1; gui.caughtFade.gameObject.SetActive(false);
                Lobby.EndOpening(); gui.Awaken();
            }
            public void Artisan(string mode, float x, float heroOffset, int face, string busy)
            {
                switch (mode)
                {
                    case "enterSlow": Lobby.ArtisanPace = 2.4f; goto case "enter";
                    case "enter": Lobby.ArtisanAt(float.IsNaN(x) ? Lobby.World.arena.Player.Position.x + heroOffset : x, face, busy, true, false); break;
                    case "place": Lobby.ArtisanAt(x, face, busy, false, false); break;
                    case "walkFast": Lobby.ArtisanPace = 7.2f; goto case "walk";
                    case "walk": Lobby.ArtisanAt(float.IsNaN(x) ? Lobby.World.arena.Player.Position.x + heroOffset : x, face, null, false, true); break;
                    case "busy": Lobby.ArtisanBusyAs(busy, face); break;
                    case "face": Lobby.ArtisanFace(face); break;
                    case "leave": Lobby.ArtisanLeave(); gui.leaveAt = -1; break;
                    case "inside": Lobby.ArtisanInside(); break;
                }
            }
            public void Pan(string station, float heroOffset, int face) => Lobby.PanTo(station, heroOffset, face);
            public void UnlockStation(string station, bool sparkleOnly)
            {
                PresentationSfx.Play("story-ink-awaken");
                if (sparkleOnly) { gui.PlayStationSparkle(station); return; }
                if (Lobby.HeldClosed == station) Lobby.HeldClosed = null;
                Profile.MarkStorySeen(StationOpenedStory(station));
                gui.PlayStationUnlock(station);
            }
            // The piece's colour spreads from the well; a stand already there takes the piece and sparkles.
            public void Bloom(int piece)
            {
                gui.BeginVillageInk(piece);
            }
            // The stand appears beside the well holding this visit's piece.
            public void Palette()
            {
                // Retained for older story fixtures; the current script has no physical palette station.
                Lobby.HidePalette();
            }
            public void Thud(float volume) => Lobby.Thud(volume);
            public void OpenStation(string id)
            {
                var station = Lobby.Find(id);
                if (station != null) gui.OpenStation(station, true);
                else Debug.LogWarning("Opening station missing: " + id);
            }
            public void Drain(string actor, float seconds) { if (actor == "hero") Lobby.DrainHero(seconds); }
            public void Clip(string actor, string clip, float x, bool reverse, bool holdLast)
            {
                if (actor == "helper") Lobby.PlayHelperClip(clip);
                // The helper's ink flowing in gives the hero its colour back before it gets up.
                else if (clip == "inkFlowIn") Lobby.RestoreHero();
                else if (clip == "inkGather") Lobby.StartInkGather(x);
                else Lobby.PlayHeroClip(clip, reverse);
            }
            public void Spawn(string actor, string sprite, float x, string behind, string effect) { if (actor == "helper") Lobby.SpawnHelper(sprite, x, behind); }
            public void Despawn(string actor) { if (actor == "helper") Lobby.DespawnHelper(); }
            // Saved with the village tutorial (part 4); until then a restart plays the opening from the start.
            public void Checkpoint(string value) => Debug.Log("Opening checkpoint not saved yet: " + value);
            // The first return is saved as a seen story (its flag); the opening's flags are not saved yet.
            public void Flag(string name)
            {
                if (gui.returnPlaying) gui.MarkFirstReturnSeen();
                else Debug.Log("Opening flag not saved yet: " + name);
            }
            // Untaught movement stays locked: the opening opens each one when its guide appears.
            public void Abilities(string[] names, bool replace)
            {
                var player = Lobby.World.arena.Player;
                var set = replace ? MovementAbilities.None : player.Abilities;
                foreach (var name in names) if (MovementAbilitiesExtensions.TryParse(name, out var ability)) set |= ability;
                player.SetMovementAbilities(set);
            }
        }
    }
}
