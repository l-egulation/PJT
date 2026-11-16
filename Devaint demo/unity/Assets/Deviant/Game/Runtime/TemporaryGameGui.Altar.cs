using System;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Boss room colour ink altar (journal design, faithful.css altar / reward): 보스의 잉크 shows five colour cards; after one
    // is chosen, 잉크 선택 shows up to three candidates of that colour, one is marked and 선택한 잉크 획득 takes it; the
    // screen then closes by itself after the acquisition effect.
    // The world altar (Chapter1InkAltar) owns the pause scope and the rules; this view only shows and forwards picks.
    public sealed partial class TemporaryGameGui
    {
        GameObject altarPanel;
        Text altarTitle, altarHint, altarMessage;
        Button altarClaim;
        readonly Button[] altarColors = new Button[5];
        readonly Button[] altarCards = new Button[3];
        int altarPick = -1;
        bool altarClosing;
        SpriteFlipbook altarAcquire;
        Chapter1InkAltar Altar => boss ? boss.Altar : chapter2 ? chapter2.Altar : null;
        static string AltarColourName(string color) => color switch
        { "red" => "붉은색", "yellow" => "노란색", "green" => "초록색", "blue" => "파란색", "purple" => "보라색", _ => color };

        void BuildAltar()
        {
            altarPanel = Book("보스의 잉크", false, out altarTitle, out var close);
            var page = altarPanel.transform;
            for (int i = 0; i < 5; i++)
            {
                int index = i; string color = Chapter1InkAltar.Colors[i];
                var face = Card(page, Mathf.Round(LeftX + i * 149.75f), 46, 138, 354); face.raycastTarget = true; face.name = color;
                altarColors[i] = face.gameObject.AddComponent<Button>(); altarColors[i].transition = Selectable.Transition.None;
                altarColors[i].onClick.AddListener(() => ChooseAltarColor(index));
                Drop(face.transform, null, color, 29, 101, 80, 112).color = Color.white;
                Words(AltarColourName(color), face.transform, 0, 233, 138, 21, 16, true, JInk, TextAnchor.MiddleCenter);
            }
            altarHint = Words("", page, LeftX, 53, 600, 25, 18, true);
            altarMessage = Words("", page, LeftX, 55, 736, 21, 16, false, JInk, TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var face = Card(page, Mathf.Round(LeftX + i * 249.5f), 96, 237, 268); face.raycastTarget = true; face.name = "Candidate " + i;
                altarCards[i] = face.gameObject.AddComponent<Button>(); altarCards[i].transition = Selectable.Transition.None;
                altarCards[i].onClick.AddListener(() => PickAltar(index));
                var swatch = Rect("Swatch", face.transform, 79, 44, 80, 112).gameObject.AddComponent<Image>(); swatch.raycastTarget = false; swatch.preserveAspect = true;
                Words("", face.transform, 0, 168, 237, 24, 20, true, JInk, TextAnchor.MiddleCenter).name = "Name";
                var text = Paragraph(face.transform, 16, 200, 205, 14, 20, JInk); text.name = "Text"; text.alignment = TextAnchor.UpperCenter;
            }
            altarClaim = Action("선택한 잉크 획득", page, 326, 376, 147, 36, () => { if (altarPick >= 0) ClaimAltar(altarPick); }, true);
            altarReroll=Action("공용 리롤",page,590,376,178,36,()=>{if(Altar&&Altar.TryReroll())altarPick=-1;Refresh();});
            RegisterScreen(altarPanel, close);
        }

        void ChooseAltarColor(int index)
        {
            var altar = Altar; if (!altar || !altar.IsOpen) return;
            altar.TryChooseColor(Chapter1InkAltar.Colors[index]); altarPick = -1;
            Refresh();
        }

        // A candidate card only marks the choice; 선택한 잉크 획득 takes it.
        void PickAltar(int index)
        {
            var altar = Altar; if (!altar || !altar.IsOpen || altar.Claimed != null) return;
            altarPick = index; Refresh();
        }

        void ClaimAltar(int index)
        {
            var altar = Altar; if (!altar || !altar.IsOpen) return;
            // The acquisition effect rises from the taken card's ink drop only when the claim succeeded; the altar then
            // closes by itself once that effect ends (docs/design/run/run-structure.md: 획득하면 제단은 꺼진다).
            if (altar.TryClaim(index))
            {
                altarAcquire = PlayUiEffect("Chapter1Art/InkAltar/choice-acquire", (RectTransform)altarCards[index].transform.Find("Swatch"));
                altarClosing = true; PresentationSfx.Play(PresentationSfx.AltarAcquire);
            }
            Refresh();
        }

        // Called every GUI frame (real time, the altar pauses the room): closes a claimed altar after its acquisition effect.
        void TickAltarClose()
        {
            if (!altarClosing) return;
            var altar = Altar;
            if (!altar || !altar.IsOpen || altar.Claimed == null) { altarClosing = false; altarAcquire = null; return; }
            if (altarAcquire != null && altarAcquire.Playing) return;
            altarClosing = false; altarAcquire = null;
            altar.Close(); Refresh();
        }

        void RefreshAltar()
        {
            var altar = Altar;
            if (!altar || !altar.IsOpen) { altarPick = -1; return; }
            bool choosing = altar.Candidates == null;
            altarReroll.gameObject.SetActive(!choosing&&altar.Claimed==null);
            altarReroll.GetComponentInChildren<Text>().text=RerollText;altarReroll.interactable=RerollsLeft>0;Paint(altarReroll,false);
            string title = choosing ? "보스의 잉크" : "잉크 선택";
            if (altarTitle.text != title) { altarTitle.text = title; FitTitle(altarTitle); }
            foreach (var b in altarColors) b.gameObject.SetActive(choosing);
            bool empty = !choosing && altar.Candidates.Length == 0;
            altarHint.text = choosing ? "" : empty ? "이 색에서 얻을 수 있는 후보가 없습니다" : $"{AltarColourName(altar.Color)} 하나만 가져갈 수 있습니다";
            altarMessage.text = choosing || empty ? "" : $"선택 {(altar.Claimed != null || altarPick >= 0 ? 1 : 0)} / 1";
            var growth = altar.Growth;
            for (int i = 0; i < altarCards.Length; i++)
            {
                var card = altarCards[i];
                bool shown = !choosing && i < altar.Candidates.Length;
                card.gameObject.SetActive(shown);
                if (!shown) continue;
                var part = Array.Find(growth.Data.parts, p => p.id == altar.Candidates[i]);
                var swatch = card.transform.Find("Swatch").GetComponent<Image>(); swatch.sprite = DropArt(part); swatch.enabled = swatch.sprite;
                card.transform.Find("Name").GetComponent<Text>().text = part.name;
                card.transform.Find("Text").GetComponent<Text>().text = Deviant.UI.Theme.ApprovedInkText.Display(part.description);
                bool marked = altar.Claimed != null ? altar.Claimed.Part.id == part.id : i == altarPick;
                card.interactable = altar.Claimed == null;
                SetCard(card.image, marked);
            }
            altarClaim.gameObject.SetActive(!choosing && !empty);
            altarClaim.GetComponentInChildren<Text>().text = altar.Claimed != null ? "획득 완료" : "선택한 잉크 획득";
            altarClaim.interactable = altar.Claimed == null && altarPick >= 0; Paint(altarClaim, true);
        }
        // HTML .choice.active swaps the card frame for slot-active.png.
        static void SetCard(Image image, bool active)
        { image.sprite = Art(active ? "slot-active" : "card", 10); image.pixelsPerUnitMultiplier = active ? 10 / 6f : 10 / 4f; }
    }
}
