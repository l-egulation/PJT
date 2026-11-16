using System.Linq;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The village artisan's visits (docs/design/progression/village-palette-pieces.md, data/story/village_visits.json compiled
    // to Hub/Resources/HubVisits.json): back in the village with a new ink, the visit of that ink plays once,
    // through the same beat director, bubbles and banners as the opening. Its workbench stays closed until its lock breaks
    // in the scene. Locked stretches show cutscene bars; holding Esc skips to the visit's end state. From the fifth visit on
    // the artisan keeps its shop. The temporary ink reveal restores colour without installing a village station.
    public sealed partial class TemporaryGameGui
    {
        // Cutscene bars as the HTML opening prototype draws them: 60 px top and bottom, eased in and out at 6 a second (a
        // guide card folds them away at 4 bar heights a second).
        const float SceneBars = 60, SceneBarsEase = 6, SceneBarsFold = 4;
        const float LeaveWalk = 1.1f, LeaveDarken = .7f, LeaveBrighten = .5f;
        float ArtisanHome => village.Find(StartingVillageWorld.ServiceHub).Position.x;

        OpeningScript visitScript;
        bool visitsChecked, artisanHomeChecked, visitIncludesFirstRun;
        int visitPiece, visitBeats;
        RectTransform sceneBarTop, sceneBarBottom;
        float sceneBars, leaveAt = -1;

        public int VisitPlaying => visitPiece;
        static string VisitStory(int piece) => piece == FirstRunVisit ? FirstRunVisitStory : "artisan-visit-" + piece;

        OpeningScript VisitData
        {
            get
            {
                if (visitsChecked) return visitScript;
                visitsChecked = true;
                var text = Resources.Load<TextAsset>("HubVisits");
                if (!text) { Debug.LogError("HubVisits resource is missing; run tools/assets/build_hub_bindings.py."); return null; }
                var script = JsonUtility.FromJson<OpeningScript>(text.text);
                var errors = OpeningScriptRules.Validate(script);
                if (errors.Count > 0) { Debug.LogError("HubVisits is invalid: " + string.Join("; ", errors)); return null; }
                return visitScript = script;
            }
        }

        void BuildVisits()
        {
            sceneBarTop = Fill(stage, 0, 0, 960, 0, Color.black).rectTransform; sceneBarTop.name = "장면 위 띠";
            sceneBarBottom = Fill(stage, 0, 540, 960, 0, Color.black).rectTransform; sceneBarBottom.name = "장면 아래 띠";
            BuildVillageInk();
        }

        // Back in the village: the newest unseen ink return plays, and after the last visit the
        // artisan is home at its shop. Returns true when a visit took over.
        bool CheckVisit()
        {
            if (openingActive || VillageInkModalShowing || storyActive || StoryRequired || !village.Ready || village.Departing || ChapterSceneFade.Busy) return false;
            int pieces = Profile.PalettePieces;
            // A recovered reward must already sit over the restored town, even before its confirmation.
            village.ColourPieces = ReturnedInkPieces;
            var script = VisitData;
            // The unknown red ink is explained before it restores colour. An unshown supply lesson joins this visit.
            if (script != null && pieces >= 1 && !Profile.Snapshot.HasSeen(VisitStory(1))) { StartVisit(1); return true; }
            if (CheckFirstRunVisit()) return true;
            // Reserve the facility lock before the player chooses to use the ink at the well.
            if (village.HeldClosed == null && script != null)
                village.HeldClosed = script.beats.Where(b => b.part <= pieces && !Profile.Snapshot.HasSeen(VisitStory(b.part)))
                    .SelectMany(b => b.actions).FirstOrDefault(a => a.type == "unlockStation")?.id;
            for (int k = 1; k <= pieces; k++)
                if (InkReturned(k) && !Profile.Snapshot.HasSeen(InkRewardStory(k)))
                { BeginVillageInkReward(k); return true; }
            int last = script == null ? 0 : script.beats.Max(b => b.part);
            for (int k = 2; script != null && k <= Mathf.Min(pieces, last); k++)
                if (InkReturned(k) && !Profile.Snapshot.HasSeen(VisitStory(k)))
                {
                    if (script.beats.Any(b => b.part == k)) { StartVisit(k); return true; }
                    Profile.MarkStorySeen(VisitStory(k));
                }
            if (village.PaletteShown) village.HidePalette();
            village.ColourPieces = ReturnedInkPieces;
            RefreshCarriedVillageInk();
            if (!artisanHomeChecked)
            {
                artisanHomeChecked = true;
                if (last > 0 && pieces >= last && Profile.Snapshot.HasSeen(VisitStory(last))) village.ArtisanAt(ArtisanHome, -1, "", false, false);
            }
            return false;
        }

        void StartVisit(int piece)
        {
            // Earlier visits a skipped return never showed count as seen; their workbenches open without a scene.
            var beats = visitScript.beats.Where(b => b.part == piece).Select(b => JsonUtility.FromJson<OpeningBeat>(JsonUtility.ToJson(b))).ToArray();
            // Later facility introductions follow the well reward; they never consume another ink.
            wellInkReturn = false;
            if (piece > 1) foreach (var beat in beats) beat.actions = beat.actions.Where(a => a.type != "bloom").ToArray();
            visitIncludesFirstRun = piece == 1 && !Profile.Snapshot.HasSeen(FirstRunVisitStory);
            if (visitIncludesFirstRun)
            {
                var actions = beats[0].actions.ToList();
                if (!MetArtisan)
                {
                    var greet = actions.FirstOrDefault(a => a.type == "bubble");
                    if (greet != null) greet.text = "어? 이 마을엔 어쩐 일이야?";
                }
                if (!Profile.Snapshot.HasSeen(StationOpenedStory(StartingVillageWorld.SupplyCounter)))
                    actions = FirstClearSupplyActions(actions);
                beats[0].actions = actions.ToArray();
            }
            foreach (var beat in beats) beat.part = 1;
            var script = new OpeningScript { schemaVersion = 1, id = visitScript.id + "-" + piece, speakerNames = visitScript.speakerNames, beats = beats };
            string held = beats.SelectMany(b => b.actions).FirstOrDefault(a => a.type == "unlockStation")?.id;
            foreach (var station in village.Stations)
                if (station.Id != held && !(visitIncludesFirstRun && station.Id == StartingVillageWorld.SupplyCounter)
                    && village.StationOpen(station)) Profile.MarkStorySeen(StationOpenedStory(station.Id));
            village.HeldClosed = held; cardsChecked = true;
            village.HidePalette();
            // A saved reveal may precede an unfinished artisan visit; don't erase or replay that ink.
            village.ColourPieces = ReturnedInkPieces;
            if(piece>=1 && piece<=5 && !Profile.Snapshot.HasSeen(VillageInkStory(piece))) VillageInkPocketGlow.Carry(village.World.arena.Player,piece);
            speakerNames.Clear();
            foreach (var entry in script.speakerNames ?? new OpeningSpeakerName[0]) speakerNames[entry.speaker] = entry.name;
            openingActive = true; escHeldSince = -1; visitPiece = piece; visitBeats = beats.Length; leaveAt = -1;
            village.SceneCamera = true;
            opening = new OpeningDirector(script, new OpeningStage(this), 1);
        }

        [System.Serializable] sealed class FirstClearLine { public string id, text; }
        [System.Serializable] sealed class FirstClearSupplySource { public OpeningAction[] firstRunSupplyLesson; public FirstClearLine[] firstRunDialogue; }
        static System.Collections.Generic.List<OpeningAction> FirstClearSupplyActions(System.Collections.Generic.List<OpeningAction> actions)
        {
            var data = JsonUtility.FromJson<FirstClearSupplySource>(Resources.Load<TextAsset>("HubVisits").text);
            foreach (var line in data.firstRunDialogue ?? System.Array.Empty<FirstClearLine>())
            {
                var action = actions.Find(a => a.type == "bubble" && a.id == line.id);
                if (action == null) throw new System.InvalidOperationException("Missing first-clear dialogue anchor: " + line.id);
                action.text = line.text;
            }
            int start = actions.FindIndex(a => a.id == "red-ink-workshop-start");
            int end = actions.FindIndex(a => a.type == "artisan" && a.mode == "leave");
            if (start < 0 || end <= start || data?.firstRunSupplyLesson == null || data.firstRunSupplyLesson.Length == 0)
                throw new System.InvalidOperationException("The first-clear pigment conversation is missing from HubVisits.");
            actions.RemoveRange(start, end - start);
            actions.InsertRange(start, data.firstRunSupplyLesson.Select(a => JsonUtility.FromJson<OpeningAction>(JsonUtility.ToJson(a))));
            return actions;
        }

        // The visit's end: by its own last beat, or skipped to the state it leaves behind.
        void FinishVisit(bool skipped)
        {
            int piece = visitPiece;
            bool pouredAtWell = wellInkReturn; wellInkReturn = false;
            FinishVillageInk(true);
            if (piece == 1 || pouredAtWell) Profile.MarkStorySeen(VillageInkStory(piece));
            if (!pouredAtWell) Profile.MarkStorySeen(VisitStory(piece));
            if (visitIncludesFirstRun)
            {
                Profile.MarkStorySeen(FirstRunVisitStory);
                Profile.MarkStorySeen(StationOpenedStory(StartingVillageWorld.SupplyCounter));
            }
            visitIncludesFirstRun = false;
            if (!pouredAtWell && village.HeldClosed != null) { Profile.MarkStorySeen(StationOpenedStory(village.HeldClosed)); village.HeldClosed = null; }
            openingActive = false; visitPiece = 0; escHeldSince = -1; skipHint.SetActive(false);
            HideBubble(); pendingBanner = null; banner.gameObject.SetActive(false); bannerDone = -1;
            openingLock?.Dispose(); openingLock = null; relockAfterWalk = false; leaveAt = -1; caughtFade.gameObject.SetActive(false);
            village.StopScriptedWalk(); village.FocusCamera(null); village.SceneCamera = false;
            village.HidePalette();
            village.ColourPieces = ReturnedInkPieces;
            pocketInk = 0;
            openingStationReadyAt = Time.unscaledTime + OpeningStationDelay;
            if (!skipped) { if (piece == 1 || pouredAtWell) BeginVillageInkReward(piece); return; }
            if (piece == 1 || pouredAtWell) Profile.MarkStorySeen(InkRewardStory(piece));
            // Where the visit leaves the artisan: home at its shop after the last visit, otherwise gone (visit 2 ends
            // with it inside the reclaim workshop).
            if (piece == FirstRunVisit) village.RemoveArtisan();
            else if (piece >= (visitScript?.beats.Max(b => b.part) ?? 5)) village.ArtisanAt(ArtisanHome, -1, "", false, false);
            else village.RemoveArtisan();
        }

        // Every frame of the village: bars while a cutscene (the opening, a visit, the first return) holds the controls, the
        // screen dimming while the artisan leaves.
        void TickVisit()
        {
            float dt = village.SceneDelta;
            if (villageInkView) villageInkView.Step(dt);
            TickVillageInkPour(dt);
            TickPigmentPop();
            if (LessonShowing != null) sceneBars = Mathf.MoveTowards(sceneBars, 0, dt * SceneBars * SceneBarsFold);
            else sceneBars += ((openingActive && (openingLock != null || relockAfterWalk) ? SceneBars : 0) - sceneBars) * Mathf.Min(1, dt * SceneBarsEase);
            float bar = Mathf.Round(sceneBars);
            sceneBarTop.sizeDelta = sceneBarBottom.sizeDelta = new Vector2(960, bar);
            sceneBarBottom.anchoredPosition = new Vector2(0, -(540 - bar));
            if (leaveAt < 0) return;
            float t = village.SceneTime - leaveAt, dark = 0;
            if (t >= LeaveWalk && t < LeaveWalk + LeaveDarken) dark = (t - LeaveWalk) / LeaveDarken;
            else if (t >= LeaveWalk + LeaveDarken)
            {
                if (village.ArtisanShown) village.RemoveArtisan();
                dark = 1 - (t - LeaveWalk - LeaveDarken) / LeaveBrighten;
                if (dark <= 0) { leaveAt = -1; dark = 0; }
            }
            caughtFade.gameObject.SetActive(dark > 0); caughtFade.color = new Color(0, 0, 0, Mathf.Clamp01(dark));
        }

        bool VisitAnimating => VillageInkShowing || village.ArtisanMoving || village.Panning || leaveAt >= 0;
    }
}
