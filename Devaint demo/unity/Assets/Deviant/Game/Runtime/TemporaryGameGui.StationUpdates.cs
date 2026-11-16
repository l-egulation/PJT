using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        RectTransform stationUpdates;
        readonly Dictionary<string, Text> stationUpdateMarks = new Dictionary<string, Text>();

        public bool StationUpdatePending(string stationId)
        {
            if (stationId == StartingVillageWorld.RecordDesk) return RecordHasUpdate;
            if (stationId == StartingVillageWorld.ReturnWell) return PendingWellInk > 0;
            return false;
        }

        void BuildStationUpdates(Transform plane)
        {
            stationUpdates = Rect("마을 새 소식", plane, 0, 0, 960, 540);
            foreach (string id in new[] { StartingVillageWorld.RecordDesk, StartingVillageWorld.ReturnWell })
            {
                var mark = Words("!", stationUpdates, 0, 0, 20, 24, 18, true, new Color32(255, 224, 116, 255), TextAnchor.MiddleCenter);
                mark.name = "새 소식 " + id;
                var edge = mark.gameObject.AddComponent<Outline>();
                edge.effectColor = CardInk; edge.effectDistance = new Vector2(1, -1);
                mark.gameObject.SetActive(false);
                stationUpdateMarks.Add(id, mark);
            }
            stationUpdates.gameObject.SetActive(false);
        }

        void RefreshStationUpdates()
        {
            if (!stationUpdates) return;
            bool freeOpeningVillage = openingActive && opening != null && opening.Part >= VillagePart
                && visitPiece == 0 && !returnPlaying && openingLock == null;
            bool show = village && village.Ready && !village.Departing && !departing && !village.StationsSuppressed
                && (Profile.Snapshot.HasSeen("opening") || freeOpeningVillage)
                && (!openingActive || freeOpeningVillage) && !storyActive && !returnPlaying
                && !VillageInkModalShowing && !notebookOpen && !hubOpen && !hubPaused && !ChapterSceneFade.Busy;
            stationUpdates.gameObject.SetActive(show);
            if (!show) return;
            foreach (var entry in stationUpdateMarks)
            {
                var station = village.Find(entry.Key);
                bool pending = station != null && StationUpdatePending(entry.Key);
                entry.Value.gameObject.SetActive(pending);
                if (!pending) continue;
                var point = CardPoint(new Vector2(station.Position.x, station.Top));
                float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 3) * 2);
                entry.Value.rectTransform.anchoredPosition = new Vector2(Mathf.Round(point.x) - 10, -Mathf.Round(point.y - CardRise - 26) + bob);
            }
        }
    }
}
