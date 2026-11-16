using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Paper name cards over the village stations (docs/design/progression/village-palette-pieces.md, from the HTML opening
    // prototype): the station's short name on a pale paper card with a 1 px edge and a soft shadow. A workbench that waits
    // for more village palette pieces ("N챕터 클리어 필요") or for the first settled run (the supply office, "첫 런을
    // 마치면 열림") has a faded card with a lock and that condition above it, and no F keycap.
    // When it opens, the lock shakes and cracks, breaks into falling bits while the condition fades, the name inks in from
    // grey with a small pop, and sparks scatter around the bench.
    // No card shows before the first departure into chapter 1 (playtest 2026-11-03): the opening's village keeps its
    // stations unnamed, F keycap only.
    public sealed partial class TemporaryGameGui
    {
        const float CardHeight = 22, CardRise = 27, WorldPixels = 32;
        const float UnlockShake = .28f, UnlockBreak = .3f, UnlockInkFrom = .38f, UnlockInkTo = .85f, UnlockDone = 1, UnlockSpark = 1.2f;
        static readonly Color CardPaper = new Color32(255, 255, 255, 255), CardEdge = new Color32(36, 35, 29, 255), CardEdgeFaded = new Color32(165, 156, 143, 255);
        static readonly Color CardInk = new Color32(23,23,17,255), CardInkFaded = new Color32(23,23,17,255), LockInk = new Color32(111, 103, 93, 255);
        static readonly Color LockPaper = new Color32(243, 238, 228, 255), CardShadow = new Color(40 / 255f, 32 / 255f, 26 / 255f, .18f), Spark = new Color32(255, 236, 170, 255);
        // Lock pixels (x, y, w, h from its top-left): shackle, body; then the keyhole and the two crack strokes in paper colour.
        static readonly RectInt[] LockBody = { new RectInt(2, 0, 4, 1), new RectInt(1, 1, 1, 3), new RectInt(6, 1, 1, 3), new RectInt(0, 4, 8, 6) };
        static readonly RectInt[] LockCrack = { new RectInt(4, 4, 1, 3), new RectInt(3, 7, 1, 3) };
        // Bits the broken lock falls apart into: rect and velocity (px/s); gravity pulls them down.
        static readonly (RectInt bit, Vector2 velocity)[] LockBits =
        {
            (new RectInt(2, 0, 4, 1), new Vector2(-38, -70)), (new RectInt(1, 1, 1, 3), new Vector2(-60, -30)), (new RectInt(6, 1, 1, 3), new Vector2(55, -45)),
            (new RectInt(0, 4, 4, 6), new Vector2(-30, -10)), (new RectInt(4, 4, 4, 6), new Vector2(40, -20)), (new RectInt(3, 6, 2, 2), new Vector2(5, -80)),
        };

        sealed class StationCard
        {
            public StartingVillageWorld.Station station;
            public RectTransform root, frame, lockRow;
            public MaintenanceBubbleGraphic graphic; public Text name, need;
            public Image[] lockBody, crack, bits; public Image keyhole;
            public Image[] sparks;
            public float unlockAt = -1, sparkAt = -1;
        }

        RectTransform stationCards;
        Canvas cardCanvas;
        readonly List<StationCard> cards = new List<StationCard>();
        bool cardsChecked;

        void BuildStationCards()
        {
            // The cards belong to the world picture: a world-space canvas at one canvas pixel per world pixel (32 per unit,
            // the village camera's scale), drawn between the scenery and the characters (StartingVillageWorld.MarkerOrder).
            // No card covers a character, the GUI's speech bubbles and the dim veil of an open book stay over them, and a
            // card stays on its station whatever size the camera draws at. The canvas's top-left is the world origin.
            cardCanvas = new GameObject("작업대 이름표 캔버스", typeof(RectTransform)).AddComponent<Canvas>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cardCanvas.gameObject, village.gameObject.scene);
            cardCanvas.renderMode = RenderMode.WorldSpace;
            cardCanvas.sortingOrder = StartingVillageWorld.MarkerOrder;
            var plane = (RectTransform)cardCanvas.transform;
            plane.pivot = new Vector2(0, 1); plane.sizeDelta = Vector2.zero; plane.position = Vector3.zero; plane.localScale = Vector3.one / WorldPixels;
            stationCards = Rect("작업대 이름표", plane, 0, 0, 960, 540);
            stationCards.gameObject.SetActive(false);
            BuildStationUpdates(plane);
        }

        StationCard MakeCard(StartingVillageWorld.Station station)
        {
            var card = new StationCard { station = station };
            card.root = Rect("이름표 " + station.Label, stationCards, 0, 0, 1, 1);
            card.frame = Rect("Card", card.root, 0, 0, 10, CardHeight);
            card.graphic = card.frame.gameObject.AddComponent<MaintenanceBubbleGraphic>();
            card.graphic.UseApprovedFrame = false;
            card.name = Words(station.Label, card.frame, 0, 0, 10, CardHeight, 12, false, CardInk, TextAnchor.MiddleCenter);
            card.name.font = Deviant.UI.Theme.ApprovedTheme.Font ?? font;
            card.name.color = CardInk;
            card.lockRow = Rect("Lock", card.root, 0, 0, 1, 10);
            card.lockBody = System.Array.ConvertAll(LockBody, r => Fill(card.lockRow, r.x, r.y, r.width, r.height, LockInk));
            card.keyhole = Fill(card.lockRow, 3, 6, 2, 2, LockPaper);
            card.crack = System.Array.ConvertAll(LockCrack, r => Fill(card.lockRow, r.x, r.y, r.width, r.height, LockPaper));
            card.bits = System.Array.ConvertAll(LockBits, b => Fill(card.lockRow, b.bit.x, b.bit.y, b.bit.width, b.bit.height, LockInk));
            card.need = Words(Profile.StationNeed(station.Id), card.lockRow, 12, -1, 10, 12, 12, true, LockInk);
            card.sparks = new Image[18];
            for (int i = 0; i < card.sparks.Length; i++) card.sparks[i] = Fill(stationCards, 0, 0, 3, 3, Spark);
            Layout(card);
            return card;
        }

        void Layout(StationCard card)
        {
            float w = Width(card.name) + 20;
            card.frame.sizeDelta = new Vector2(w, CardHeight); card.frame.anchoredPosition = new Vector2(-Mathf.Round(w / 2), 0);
            card.graphic.Configure(Mathf.RoundToInt(w), (int)CardHeight, -1, 0, CardPaper, CardEdge, false);
            card.name.rectTransform.sizeDelta = new Vector2(w, CardHeight);
            card.name.fontStyle = FontStyle.Normal;
            float tw = Width(card.need);
            card.need.rectTransform.sizeDelta = new Vector2(tw + 4, 12);
            card.lockRow.anchoredPosition = new Vector2(-Mathf.Round((tw + 12) / 2), 15);
        }

        // Plays the opening of a workbench's card now (the visit scenes call it at their moment).
        public void PlayStationUnlock(string stationId)
        {
            var card = cards.Find(c => c.station.Id == StartingVillageWorld.PhysicalStationId(stationId));
            if (card != null) card.unlockAt = Time.unscaledTime;
        }

        // Sparks only, around a station that is already open (or the palette stand taking a piece).
        public void PlayStationSparkle(string stationId)
        {
            var card = cards.Find(c => c.station.Id == StartingVillageWorld.PhysicalStationId(stationId));
            if (card != null) card.sparkAt = Time.unscaledTime;
        }

        public bool StationUnlockPlaying => cards.Exists(c => c.unlockAt >= 0 && Time.unscaledTime - c.unlockAt < UnlockSpark);

        // From the first return on: the first departure counts its run before the hero walks out, and the cards must not
        // flash up on the way out (playtest 2026-11-05).
        static bool StationNamesShown => Profile.Snapshot.RunsStarted > 0;

        void RefreshStationCards()
        {
            if (!stationCards) return;
            RefreshStationUpdates();
            // The record's dark veil lets the village show through on purpose, but not the cards' white names (playtest 2026-11-13).
            bool show = village && village.Ready && !village.Departing && !departing && !village.StationsSuppressed && !storyActive && (!openingActive || OpeningInVillage) && StationNamesShown
                && hubOpen != record;
            stationCards.gameObject.SetActive(show);
            if (!show) return;
            // Stations can come later (the palette stand from the first piece on).
            if (cards.Count != village.Stations.Count)
                foreach (var station in village.Stations) if (!cards.Exists(c => c.station == station)) cards.Add(MakeCard(station));
            if (!cardsChecked) { cardsChecked = true; PlayNewlyOpened(); }
            float now = Time.unscaledTime;
            foreach (var card in cards)
            {
                var top = CardPoint(new Vector2(card.station.Position.x, card.station.Top));
                card.root.anchoredPosition = new Vector2(Mathf.Round(top.x), -Mathf.Round(top.y - CardRise));
                float fx = card.unlockAt >= 0 ? now - card.unlockAt : -1;
                bool unlocking = fx >= 0 && fx < UnlockDone, open = village.StationOpen(card.station);
                // The name stays faded until it inks in, then grows a little and settles.
                float k = unlocking ? Mathf.Clamp01((fx - UnlockInkFrom) / (UnlockInkTo - UnlockInkFrom)) : open ? 1 : 0;
                float pop = unlocking && fx >= UnlockInkFrom ? 1 + .14f * Mathf.Sin(Mathf.PI * k) : 1;
                card.frame.localScale = new Vector3(pop, pop, 1);
                card.graphic.Configure(Mathf.RoundToInt(card.frame.sizeDelta.x), (int)CardHeight, -1, 0, CardPaper, CardEdge, false);
                card.name.color = Color.Lerp(CardInkFaded, CardInk, k);
                bool locked = !open || unlocking && fx < UnlockBreak, breaking = unlocking && fx >= UnlockBreak;
                card.lockRow.gameObject.SetActive(locked || breaking);
                foreach (var piece in card.lockBody) piece.enabled = locked;
                card.keyhole.enabled = locked;
                // Shaking harder as it cracks.
                float jitter = unlocking && locked ? Mathf.Round(Mathf.Sin(fx * 90) * Mathf.Min(2, fx * 10)) : 0;
                foreach (var stroke in card.crack) stroke.enabled = unlocking && locked && fx > .12f;
                for (int i = 0; i < card.bits.Length; i++)
                {
                    card.bits[i].enabled = breaking;
                    if (!breaking) continue;
                    float u = fx - UnlockBreak, fade = Mathf.Max(0, 1 - u / .55f);
                    var (bit, velocity) = LockBits[i];
                    card.bits[i].rectTransform.anchoredPosition = new Vector2(Mathf.Round(bit.x + velocity.x * u), -Mathf.Round(bit.y + velocity.y * u + 180 * u * u));
                    card.bits[i].color = new Color(LockInk.r, LockInk.g, LockInk.b, fade);
                }
                float needFade = breaking ? Mathf.Max(0, 1 - (fx - UnlockBreak) / .55f) * .8f : 1;
                card.need.color = new Color(LockInk.r, LockInk.g, LockInk.b, needFade);
                card.need.rectTransform.anchoredPosition = new Vector2(12, breaking ? 1 + Mathf.Round((fx - UnlockBreak) * 10) : 1);
                for (int i = 0; i < LockBody.Length; i++) card.lockBody[i].rectTransform.anchoredPosition = new Vector2(LockBody[i].x + jitter, -LockBody[i].y);
                card.keyhole.rectTransform.anchoredPosition = new Vector2(3 + jitter, -6);
                for (int i = 0; i < LockCrack.Length; i++) card.crack[i].rectTransform.anchoredPosition = new Vector2(LockCrack[i].x + jitter, -LockCrack[i].y);
                // Sparks around the bench, starting when the lock breaks.
                float spark = card.sparkAt >= 0 ? now - card.sparkAt : fx - UnlockBreak;
                if (card.sparkAt >= 0 && spark >= UnlockSpark) card.sparkAt = -1;
                var centre = CardPoint(new Vector2(card.station.Position.x, card.station.Position.y + 1.2f));
                for (int i = 0; i < card.sparks.Length; i++)
                {
                    bool lit = (fx >= 0 || card.sparkAt >= 0) && spark >= 0 && spark < UnlockSpark;
                    card.sparks[i].enabled = lit;
                    if (!lit) continue;
                    float t = spark / UnlockSpark, angle = i * 2.4f, r = 10 + t * 46 + (i % 3) * 6;
                    card.sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Round(centre.x + Mathf.Cos(angle) * r), -Mathf.Round(centre.y + Mathf.Sin(angle) * r * .6f));
                    card.sparks[i].color = new Color(Spark.r, Spark.g, Spark.b, 1 - t);
                }
                if (fx >= UnlockSpark + UnlockBreak) card.unlockAt = -1;
            }
        }

        // A world point on the card canvas: world pixels from the origin, x right and y down like the GUI's stage.
        static Vector2 CardPoint(Vector2 world) => new Vector2(world.x * WorldPixels, -world.y * WorldPixels);

        // A workbench that opened since the profile last saw it plays its opening once.
        void PlayNewlyOpened()
        {
            foreach (var card in cards)
            {
                if (!Profile.StationGated(card.station.Id) || !village.StationOpen(card.station)) continue;
                // The artisan's first-run visit opens the supply office itself. The cards show during the first return at
                // the well too, so recording the office here first made that visit pass for an old save and skip
                // (playtest 2026-11-09).
                if (card.station.Id == StartingVillageWorld.SupplyCounter && !Profile.Snapshot.HasSeen(FirstRunVisitStory)) continue;
                if (Profile.MarkStorySeen(StationOpenedStory(card.station.Id))) PlayStationUnlock(card.station.Id);
            }
        }

        // The lock-break playback record shares the profile's seen-story list but is not a story to list in the record book.
        const string StationOpenedPrefix = "station-opened-";
        static string StationOpenedStory(string stationId) => StationOpenedPrefix + stationId;
        static bool IsStationOpenedStory(string id) => id.StartsWith(StationOpenedPrefix, System.StringComparison.Ordinal);
    }
}
