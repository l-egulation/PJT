using System;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // 하늘길 screens (docs/design/world/village/starting-village.md 「하늘길」): the top-left run line (height, best, falls, arrows, time) only
    // while a run is on, each zone's name once, the cleared time faded in at the centre as plain text, the door's short
    // black fade, the record board (the summit's and the village's) and the bell keeper's lines in the shops' bubble.
    public sealed partial class TemporaryGameGui
    {
        GameObject skyHud, skyBoard;
        Text skyHeight, skyLine, skyBanner, skyClearTag, skyClearTime, skyClearLine, skyBoardBest;
        Image skyBannerRule, skyFade;
        RectTransform skyBoardRows;
        float skyBannerAt = -100, skyClearedAt = -100, skyDoorAt = -100;
        bool skyDoorMoved = true;
        string skyBannerText;
        // The keeper's welcome on arrival: shown on its own (no key) after the bells, then gone; talking to the keeper ends it.
        const float SkyWelcomeDelay = .5f, SkyWelcomeSeconds = 4.2f;
        float skyWelcomeAt = -100;
        int skyKeeperLine = -1, skyKeeperFrame; float skyKeeperStarted; IControlScope skyTalkScope;
        const float SkyBannerSeconds = 2.4f, SkyClearIn = .6f, SkyClearHold = 3f, SkyClearOut = 1f, SkyDoorIn = .28f, SkyDoorOut = .32f;
        static readonly Color SkyInkText = new Color32(43, 37, 32, 255), SkyNightText = new Color32(241, 231, 212, 255), SkyHot = new Color32(212, 80, 42, 255);

        void BuildSky()
        {
            skyHud = Rect("Sky path HUD", stage, 0, 0, 960, 540).gameObject;
            skyHeight = Words("", skyHud.transform, 18, 12, 300, 30, 24, true, SkyInkText);
            skyLine = Words("", skyHud.transform, 18, 44, 520, 16, 12, false, SkyInkText);
            skyBanner = Words("", skyHud.transform, 0, 82, 960, 30, 24, true, SkyInkText, TextAnchor.MiddleCenter);
            skyBannerRule = Fill(skyHud.transform, 410, 114, 140, 2, SkyHot);
            skyClearTag = Words("", stage, 0, 128, 960, 16, 12, true, SkyHot, TextAnchor.MiddleCenter);
            // The village sky stays paper even at the top (the HTML prototype's night sky is not in the hub): ink letters
            // with a paper shadow read over the sky, the clouds and the bell tower.
            skyClearTime = Words("", stage, 0, 148, 960, 56, 48, true, SkyInkText, TextAnchor.MiddleCenter);
            skyClearLine = Words("", stage, 0, 210, 960, 16, 12, false, SkyInkText, TextAnchor.MiddleCenter);
            foreach (var t in new[] { skyClearTag, skyClearTime, skyClearLine })
            { var shadow = t.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color32(251, 246, 234, 230); shadow.effectDistance = new Vector2(2, -2); }
            skyFade = Fill(stage, 0, 0, 960, 540, Color.black); skyFade.raycastTarget = false;
            skyBoard = Book("하늘길 기록", false, 230, 96, 500, 368, out _, out var close);
            skyBoardBest = Words("", skyBoard.transform, 36, 48, 428, 18, 13, true, SkyHot);
            Fill(skyBoard.transform, 36, 74, 428, 1, JRule);
            skyBoardRows = Rect("기록", skyBoard.transform, 36, 84, 428, 240);
            RegisterScreen(skyBoard, close);
            village.SkyDoorRequested += SkyDoor;
            village.SkyTalkRequested += SkyTalk;
            village.SkyZoneEntered += zone => { skyBannerText = zone; skyBannerAt = Time.unscaledTime; };
            village.SkyFinished += _ => { skyClearedAt = Time.unscaledTime; skyWelcomeAt = Time.unscaledTime + SkyWelcomeDelay; };
            RefreshSky();
        }

        void SkyDoor()
        {
            if (!skyDoorMoved) return;
            skyDoorAt = Time.unscaledTime; skyDoorMoved = false;
            PresentationSfx.PlaySelected("sfx_ui_station_open");
        }

        void SkyTalk(StartingVillageWorld.SkyTarget target)
        {
            if (target == StartingVillageWorld.SkyTarget.Keeper) { StartSkyKeeper(); return; }
            OpenHubPanel(skyBoard);
        }

        // Runs from HubUpdate before the screens: the door fade moves the hero at its darkest; the keeper's lines advance on F.
        void TickSky()
        {
            if (!skyDoorMoved && Time.unscaledTime - skyDoorAt >= SkyDoorIn) { skyDoorMoved = true; village.EnterSkyStart(); }
            if (skyKeeperLine >= 0) { skyWelcomeAt = -100; TickSkyKeeper(); }
            else TickSkyWelcome();
        }

        void TickSkyWelcome()
        {
            var keeper = village.Sky?.keeper;
            if (keeper == null || string.IsNullOrEmpty(keeper.welcome) || skyWelcomeAt < 0) return;
            float t = Time.unscaledTime - skyWelcomeAt;
            if (t < 0) return;
            if (t > SkyWelcomeSeconds) { skyWelcomeAt = -100; if (bubbleView) bubbleView.Hide(); return; }
            if (!bubbleView)
            {
                bubbleView = MaintenanceBubbleView.Create(village.World.View);
                bubbleView.transform.SetParent(village.transform, false);
                bubbleView.GetComponent<Canvas>().sortingOrder = BubbleCanvasOrder;
            }
            int chars = Mathf.Min(keeper.welcome.Length, Mathf.FloorToInt(t * BubbleCharsPerSecond) + 1);
            bubbleView.Show(keeper.welcome, chars, keeper.name, false, village.SkyKeeperTop, Time.unscaledTime, false);
        }

        void StartSkyKeeper()
        {
            var lines = village.Sky?.keeper?.lines;
            if (lines == null || lines.Length == 0 || skyKeeperLine >= 0) return;
            var scope = VillageControls.OpenScope("sky path keeper");
            try { scope.BlockInput(InputChannels.Combat | InputChannels.Interaction); scope.PauseSimulation(); skyTalkScope = scope; }
            catch { scope.Dispose(); throw; }
            skyKeeperLine = 0; skyKeeperStarted = Time.unscaledTime; skyKeeperFrame = Time.frameCount;
            if (!bubbleView)
            {
                bubbleView = MaintenanceBubbleView.Create(village.World.View);
                bubbleView.transform.SetParent(village.transform, false);
                bubbleView.GetComponent<Canvas>().sortingOrder = BubbleCanvasOrder;
            }
        }

        void TickSkyKeeper()
        {
            var keeper = village.Sky.keeper; string line = keeper.lines[skyKeeperLine];
            int chars = Mathf.Min(line.Length, Mathf.FloorToInt((Time.unscaledTime - skyKeeperStarted) * BubbleCharsPerSecond) + 1);
            bubbleView.Show(line, chars, keeper.name, village.SketchAt(keeper.x), village.SkyKeeperTop, Time.unscaledTime, true);
            // The talk scope blocks Interaction, so lines advance on the UI Advance action (F, Space, Enter, pad south) as the
            // opening's bubbles do.
            if (Time.frameCount == skyKeeperFrame) { TakeAdvance(); return; }   // the F that opened the talk is not an advance
            if (!TakeAdvance()) return;
            if (chars < line.Length) { skyKeeperStarted = -1000; return; }
            PresentationSfx.PlaySelected("sfx_ui_dialogue_next");
            if (++skyKeeperLine < keeper.lines.Length) { skyKeeperStarted = Time.unscaledTime; return; }
            skyKeeperLine = -1; bubbleView.Hide();
            hubClosedFrame = Time.frameCount; Controls?.SuppressGameplay();
            skyTalkScope?.Dispose(); skyTalkScope = null;
        }

        void RefreshSky()
        {
            if (!skyHud || !village || !village.Ready) return;
            float now = Time.unscaledTime;
            bool run = village.SkyRunActive && hubHud.activeSelf;
            skyHud.SetActive(run);
            // The run line replaces the pigment chip at the top left while climbing.
            if (run) hubCurrency.SetActive(false);
            if (run)
            {
                bool night = village.SkyHeight > 230;
                var ink = night ? SkyNightText : SkyInkText;
                skyHeight.color = ink; skyLine.color = new Color(ink.r, ink.g, ink.b, .7f);
                skyHeight.text = $"{Mathf.Max(0, Mathf.Round(village.SkyHeight))}m";
                skyLine.text = $"최고 {Mathf.Round(village.SkyBest)}m   추락 {village.SkyFalls}   화살 {village.SkyHits}   {SkyTime(village.SkyClock)}";
                float b = now - skyBannerAt;
                float a = b < SkyBannerSeconds ? Mathf.Min(1, b * 3, (SkyBannerSeconds - b) * 2) : 0;
                skyBanner.text = skyBannerText; skyBanner.color = new Color(ink.r, ink.g, ink.b, a);
                float rule = Mathf.Min(1, b * 2.5f) * 140;
                ((RectTransform)skyBannerRule.transform).anchoredPosition = new Vector2(480 - rule / 2, -114);
                ((RectTransform)skyBannerRule.transform).sizeDelta = new Vector2(rule, 2);
                skyBannerRule.color = new Color(SkyHot.r, SkyHot.g, SkyHot.b, a);
            }
            // Cleared: the time fades in at the centre (no box), holds and fades out.
            var record = village.SkyLastRecord; float c = now - skyClearedAt;
            float clear = record == null || !village.SkyCleared ? 0 : c < SkyClearIn ? c / SkyClearIn : c < SkyClearIn + SkyClearHold ? 1 : Mathf.Max(0, 1 - (c - SkyClearIn - SkyClearHold) / SkyClearOut);
            foreach (var t in new[] { skyClearTag, skyClearTime, skyClearLine }) t.gameObject.SetActive(clear > 0);
            if (clear > 0)
            {
                float lift = (1 - Mathf.Min(1, c / SkyClearIn)) * 10;
                skyClearTag.text = record.assisted ? "연습 기록" : village.SkyNewBest ? "최고 기록" : "";
                skyClearTag.color = record.assisted ? (Color)JMuted : SkyHot;
                skyClearTime.text = SkyTime(record.seconds);
                skyClearLine.text = $"추락 {record.falls}   화살 {record.hits}";
                foreach (var t in new[] { skyClearTag, skyClearTime, skyClearLine })
                {
                    var col = t.color; col.a = clear; t.color = col;
                    var rt = (RectTransform)t.transform; rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(t == skyClearTag ? 128 : t == skyClearTime ? 148 : 210) - lift);
                }
            }
            // The door: black in, the hero moves, black out.
            float d = now - skyDoorAt, fade = d < SkyDoorIn ? d / SkyDoorIn : d < SkyDoorIn + SkyDoorOut ? 1 - (d - SkyDoorIn) / SkyDoorOut : 0;
            skyFade.gameObject.SetActive(fade > 0); skyFade.color = new Color(0.08f, 0.07f, 0.06f, fade);
            skyFade.transform.SetAsLastSibling();
            if (hubOpen == skyBoard) RefreshSkyBoard();
        }

        void RefreshSkyBoard()
        {
            var records = Profile.Snapshot.SkyParkourRecords; var best = Profile.Snapshot.BestSkyParkour;
            skyBoardBest.text = best != null ? $"최고 {SkyTime(best.seconds)}   추락 {best.falls}   화살 {best.hits}   {SkyDate(best.at)}" : "최고 기록 없음";
            foreach (Transform row in skyBoardRows) Destroy(row.gameObject);
            if (records.Count == 0) { Words("기록 없음", skyBoardRows, 0, 0, 428, 20, 13, false, JInk); return; }
            var last = village.SkyLastRecord;
            for (int i = 0; i < Mathf.Min(10, records.Count); i++)
            {
                var r = records[i]; float y = i * 22;
                Color ink = best != null && r.at == best.at ? SkyHot : (Color)JInk;
                Words(SkyDate(r.at), skyBoardRows, 0, y, 120, 20, 13, false, ink);
                Words(SkyTime(r.seconds), skyBoardRows, 124, y, 80, 20, 13, true, ink);
                Words($"추락 {r.falls}", skyBoardRows, 214, y, 70, 20, 13, false, ink);
                Words($"화살 {r.hits}", skyBoardRows, 290, y, 70, 20, 13, false, ink);
                if (r.assisted) Words("연습", skyBoardRows, 366, y, 40, 20, 13, false, JMuted);
                if (last != null && r.at == last.at) Words("방금", skyBoardRows, 400, y, 40, 20, 13, true, SkyHot);
            }
        }

        static string SkyTime(float seconds) => $"{Mathf.FloorToInt(seconds / 60)}:{(seconds % 60).ToString("00.0")}";
        static string SkyDate(long at)
        {
            try { var d = DateTime.FromBinary(at); return d.ToString("MM'/'dd HH:mm", System.Globalization.CultureInfo.InvariantCulture); }
            catch (ArgumentException) { return ""; }
        }
    }
}
