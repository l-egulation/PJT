using System.Linq;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // The artisan after the first run (playtest 2026-11-05, HTML prototype firstRunScript; data/story/opening_playable.json
    // beats first-run-visit*): back in the village from the first run, a death or not, once the supply office has opened
    // (first_run_settled), the artisan walks in, notices the hero with a 「!」, greets it (as the customer of the forest
    // workshop when they met there, as a stranger otherwise), points out the magic pigment in its pocket (the pigment chip
    // appears then), opens the supply office and leaves. It plays as a visit of its own, before the palette piece visits,
    // through the same director, bubbles, bars and Esc skip.
    public sealed partial class TemporaryGameGui
    {
        const int FirstRunVisit = 100;
        public const string FirstRunVisitStory = "artisan-first-run";
        const string FirstRunGreetMet = "first-run-visit-met", FirstRunGreetStranger = "first-run-visit-stranger", FirstRunCommon = "first-run-visit";
        bool pigmentRevealed;
        float pigmentPopAt = -1;

        // The workshop artisan was met in chapter 1 (its first visit there).
        bool MetArtisan => Profile.Snapshot.HasSeen(MaintenanceRoomPresentation.IntroStories[0]);

        bool CheckFirstRunVisit()
        {
            var snapshot = Profile.Snapshot;
            if (snapshot.HasSeen(FirstRunVisitStory)) return false;
            var supply = village.Find(StartingVillageWorld.SupplyCounter);
            if (supply == null || !village.StationOpen(supply)) return false;
            // Saves from before this scene already saw the office open: nothing to tell any more.
            if (snapshot.HasSeen(StationOpenedStory(StartingVillageWorld.SupplyCounter))) { Profile.MarkStorySeen(FirstRunVisitStory); return false; }
            var source = OpeningData; if (source == null) return false;
            OpeningBeat Copy(string id)
            {
                var beat = source.beats.FirstOrDefault(b => b.id == id);
                return beat == null ? null : JsonUtility.FromJson<OpeningBeat>(JsonUtility.ToJson(beat));
            }
            bool met = MetArtisan;
            var greet = Copy(met ? FirstRunGreetMet : FirstRunGreetStranger);
            var common = Copy(FirstRunCommon);
            var bye = Copy(FirstRunCommon + (met ? "-bye-met" : "-bye-stranger"));
            if (greet == null || common == null || bye == null) { Debug.LogError("First run visit beats are missing in HubOpening."); Profile.MarkStorySeen(FirstRunVisitStory); return false; }
            greet.trigger = new OpeningTrigger { type = "start" };
            common.trigger = new OpeningTrigger { type = "after", beat = greet.id };
            bye.trigger = new OpeningTrigger { type = "after", beat = common.id };
            var beats = new[] { greet, common, bye };
            foreach (var beat in beats) beat.part = 1;
            var script = new OpeningScript { schemaVersion = 1, id = source.id + "-" + FirstRunVisitStory, speakerNames = source.speakerNames, beats = beats };
            // The office stays shut until the artisan opens it; every other open station counts as shown.
            foreach (var station in village.Stations)
                if (station.Id != StartingVillageWorld.SupplyCounter && village.StationOpen(station)) Profile.MarkStorySeen(StationOpenedStory(station.Id));
            village.HeldClosed = StartingVillageWorld.SupplyCounter; cardsChecked = true;
            speakerNames.Clear();
            foreach (var entry in script.speakerNames ?? new OpeningSpeakerName[0]) if (!string.IsNullOrEmpty(entry?.speaker)) speakerNames[entry.speaker] = entry.name;
            openingActive = true; escHeldSince = -1; visitPiece = FirstRunVisit; visitBeats = beats.Length; leaveAt = -1;
            village.SceneCamera = true;
            opening = new OpeningDirector(script, new OpeningStage(this), 1);
            return true;
        }

        // The pigment chip appears with a little pop (the artisan has just noticed the pigment).
        void RevealPigment() { pigmentRevealed = true; pigmentPopAt = Time.unscaledTime; PresentationSfx.Play("story-ink-awaken"); RefreshHub(); }

        void TickPigmentPop()
        {
            if (pigmentPopAt < 0 || !hubCurrency) return;
            float k = Time.unscaledTime - pigmentPopAt, scale = k < .35f ? 1 + .25f * Mathf.Sin(k / .35f * Mathf.PI) : 1;
            hubCurrency.transform.localScale = new Vector3(scale, scale, 1);
            if (k >= .35f) pigmentPopAt = -1;
        }
    }
}
