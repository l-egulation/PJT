using System.Linq;
using Deviant.Narrative.Opening;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // The first return (docs/design/story/opening/opening-playable.md part 5, data/story/opening_playable.json beat first-return):
    // back in the village from the first death in chapter 1, the hero wakes at the well instead of the usual spawn, ink
    // gathering into its shape, and two banners tell what a death keeps. It plays once, through the same beat director,
    // bubble and banners as the opening; holding Esc skips it to the hero standing at the well.
    public sealed partial class TemporaryGameGui
    {
        public const string FirstReturnStory = "first-return";
        const string FirstReturnEvent = "runDeathReturn";

        bool returnChecked, returnPlaying;
        int returnBeats;

        public bool FirstReturnPlaying => returnPlaying;

        // The run the village was entered from ended in a death (a run left from the pause menu has no result).
        static bool ReturnedFromDeath => Chapter1RunHost.Instance && Chapter1RunHost.Instance.Flow.Current?.Result?.Outcome == RunOutcome.Died;

        // Once per village load, before the visits: true when the first return took over.
        bool CheckFirstReturn()
        {
            if (returnChecked || openingActive || storyActive || StoryRequired || !village.Ready || village.Departing || ChapterSceneFade.Busy) return false;
            returnChecked = true;
            if (!ReturnedFromDeath || Profile.Snapshot.HasSeen(FirstReturnStory)) return false;
            var source = OpeningData;
            var beats = source?.beats.Where(b => b.trigger?.type == "event" && b.trigger.name == FirstReturnEvent)
                .Select(b => JsonUtility.FromJson<OpeningBeat>(JsonUtility.ToJson(b))).ToArray();
            if (beats == null || beats.Length == 0) return false;
            // The village raises the event by starting the scene: its beats run as a part of their own.
            foreach (var beat in beats) { beat.part = 1; beat.trigger = new OpeningTrigger { type = "start" }; }
            var script = new OpeningScript { schemaVersion = 1, id = source.id + "-" + FirstReturnStory, speakerNames = source.speakerNames, beats = beats };
            speakerNames.Clear();
            foreach (var entry in script.speakerNames ?? new OpeningSpeakerName[0]) if (!string.IsNullOrEmpty(entry?.speaker)) speakerNames[entry.speaker] = entry.name;
            openingActive = true; returnPlaying = true; returnBeats = beats.Length; escHeldSince = -1;
            opening = new OpeningDirector(script, new OpeningStage(this), 1);
            return true;
        }

        void MarkFirstReturnSeen()
        {
            if (!Profile.MarkStorySeen(FirstReturnStory) && !Profile.Snapshot.HasSeen(FirstReturnStory))
                Debug.LogWarning("First return was not recorded: " + Profile.LastSaveError);
        }

        // The scene's end: by its own last beat, or skipped to the hero standing at the well.
        void FinishFirstReturn(bool skipped)
        {
            if (opening.Outcome == OpeningOutcome.Failed) Debug.LogError("First return stopped: " + opening.Error);
            MarkFirstReturnSeen();
            openingActive = false; returnPlaying = false; escHeldSince = -1; skipHint.SetActive(false);
            HideBubble(); pendingBanner = null; banner.gameObject.SetActive(false); bannerDone = -1;
            openingLock?.Dispose(); openingLock = null; relockAfterWalk = false;
            village.EndOpening();
            openingStationReadyAt = Time.unscaledTime + OpeningStationDelay;
            if (!skipped) return;
            var well = village.Find(StartingVillageWorld.ReturnWell);
            if (well == null) return;
            var player = village.World.arena.Player;
            player.Position = new Vector2(well.Position.x, player.Position.y); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
            village.World.RefreshCamera(0, true);
        }
    }
}
