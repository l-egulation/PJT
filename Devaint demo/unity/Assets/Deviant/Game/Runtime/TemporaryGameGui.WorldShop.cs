using System.Collections.Generic;
using Deviant.Input.Contracts;
using Deviant.Inventory;
using Deviant.Run;
using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Maintenance shelves (MaintenanceShelfDisplay): the coin and price centred on every piece's plate (the remaining shared
    // rerolls on a supply crate's plate), and over the piece in reach a card with its name, what it does, the interact key
    // and the price. Crates get no card (user 2026-11-14). Buying happens in the world; there is no shop window.
    public sealed partial class TemporaryGameGui
    {
        const float ShelfCardWidth = 260, ShelfCardPad = 10;
        static readonly Color ShelfPlate = new Color32(36, 39, 43, 255), ShelfEdge = new Color32(92, 98, 106, 255);
        MaintenanceShelfDisplay shelfSource; bool shelfHooked;
        RectTransform shelfLayer, shelfTagLayer, shelfCardLayer, shelfCard;
        readonly List<RectTransform> shelfTags = new List<RectTransform>();
        string shelfCardKey;
        MaintenanceShelfDisplay Shelves
        {
            get
            {
                if (!shelfSource && shop) shelfSource = shop.GetComponent<MaintenanceShelfDisplay>();
                if (shelfSource && !shelfHooked) { shelfHooked = true; shelfSource.Purchased += OnShelfPurchase; shelfSource.Refused += Presenter.NotifyPickupFailure; }
                return shelfSource;
            }
        }
        bool TryShelfInteract() => Shelves && Shelves.TryInteract();

        // World-anchored pieces are placed again just before the canvas renders: the chapter camera moves in its own
        // LateUpdate, after this GUI's update, and a tag placed earlier would trail the picture by a frame.
        readonly List<KeyValuePair<RectTransform, Vector2>> shelfPlaces = new List<KeyValuePair<RectTransform, Vector2>>();
        Vector2 shelfCardAnchor; Camera shelfCamera;

        void BuildWorldShop()
        {
            shelfLayer = Rect("Maintenance shelves", hud.transform, 0, 0, 960, 540);
            shelfLayer.gameObject.SetActive(false);
            // Two fixed layers so the card in reach always covers the price tags around it.
            // Prices belong to the shelf in the world, below the hero, rather than the HUD hidden by cutscenes.
            shelfTagLayer=Rect("Shelf prices",host.Arena.transform,0,0,960,540);
            shelfTagLayer.localScale=Vector3.one/32;
            var prices=shelfTagLayer.gameObject.AddComponent<Canvas>();prices.renderMode=RenderMode.WorldSpace;
            prices.overrideSorting=true;prices.sortingLayerID=host.Arena.Player.visual.sortingLayerID;
            prices.sortingOrder=host.Arena.Player.visual.sortingOrder-3;
            shelfTagLayer.gameObject.SetActive(false);
            shelfCardLayer = Rect("Shelf card", shelfLayer, 0, 0, 960, 540);
            Canvas.willRenderCanvases += PlaceWorldShop;
        }
        void DisposeWorldShop() { Canvas.willRenderCanvases -= PlaceWorldShop;if(shelfTagLayer)Destroy(shelfTagLayer.gameObject); }

        void OnShelfPurchase(SketchChoice item)
        {
            if (item == null) return;
            Presenter.NotifyAcquisition(item.Weapon != null ? InventoryPresenter.AcquisitionText(LiveWeaponName(item.Weapon), "무기")
                : InventoryPresenter.AcquisitionText(item.Ink.name, "잉크"));
            shelfCardKey = "sold";
        }

        void RefreshWorldShop()
        {
            var shelves = Shelves;
            shelfCamera = chapter2 ? chapter2.sceneCamera : world ? world.View : null;
            bool show = shelves && shelves.Active && shelfCamera && !Presenter.IsOpen && !Presenter.Paused && !(map && map.IsOpen);
            shelfLayer.gameObject.SetActive(show);
            shelfTagLayer.gameObject.SetActive(show);
            if(show)shelfTagLayer.GetComponent<Canvas>().worldCamera=shelfCamera;
            shelfPlaces.Clear();
            if (!show) return;
            int gold = Sketch.Economy.Gold, tag = 0;
            foreach (var spot in shelves.Spots)
            {
                if (!shelves.Available(spot)) continue;
                if (tag == shelfTags.Count) shelfTags.Add(PlateLabel(shelfTagLayer));
                var t = shelfTags[tag++];
                if (spot.Reroll) SetPlateLabel(t, null, (shelves.Perks?.RerollsRemaining ?? 0) + "회", shelves.Chapter, true);
                else { var item = shelves.ChoiceAt(spot); SetPlateLabel(t, CombatSprite("gold-coin"), item.Price < 0 ? "?" : item.Price.ToString(), shelves.Chapter, item.Price >= 0 && gold >= item.Price); }
                shelfPlaces.Add(new KeyValuePair<RectTransform, Vector2>(t, spot.PlateCenter));
            }
            for (int i = tag; i < shelfTags.Count; i++) shelfTags[i].gameObject.SetActive(false);
            RefreshShelfCard(shelves, gold);
            PlaceWorldShop();
        }

        void RefreshShelfCard(MaintenanceShelfDisplay shelves, int gold)
        {
            var spot = shelves.Nearest;
            if (spot != null && spot.Reroll) spot = null;                           // the crate needs no card
            var item = shelves.ChoiceAt(spot);
            string key = spot == null || item == null ? null : spot.GetHashCode() + ":" + item.GetHashCode() + ":" + (gold >= item.Price) + ":" + KeyLabel(GameAction.Interact);
            if (key != shelfCardKey)
            {
                shelfCardKey = key;
                if (shelfCard) { shelfCard.gameObject.SetActive(false); Destroy(shelfCard.gameObject); shelfCard = null; }
                if (spot != null) shelfCard = BuildShelfCard(shelves, spot, item, gold);
            }
            if (shelfCard && spot != null && item != null) { shelfCardAnchor = new Vector2(spot.Center.x, spot.Ground.y + spot.Height + .2f); }
        }

        void PlaceWorldShop()
        {
            if (!shelfTagLayer || !shelfTagLayer.gameObject.activeInHierarchy || !shelfCamera) return;
            foreach (var place in shelfPlaces)
            {
                var at = ShelfPoint(shelfCamera, place.Value); var t = place.Key;
                t.gameObject.SetActive(at.HasValue); if (!at.HasValue) continue;
                var size = t.sizeDelta;t.position=new Vector3(place.Value.x-size.x/64,place.Value.y+size.y/64,0);
            }
            if (!shelfCard) return;
            var top = ShelfPoint(shelfCamera, shelfCardAnchor);
            shelfCard.gameObject.SetActive(top.HasValue); if (!top.HasValue) return;
            var s = shelfCard.sizeDelta;
            float x = Mathf.Clamp(Mathf.Round(top.Value.x - s.x / 2), 8, 952 - s.x), y = Mathf.Clamp(Mathf.Round(top.Value.y - s.y), 8, 532 - s.y);
            shelfCard.anchoredPosition = new Vector2(x, -y);
        }

        RectTransform BuildShelfCard(MaintenanceShelfDisplay shelves, MaintenanceShelfDisplay.Spot spot, SketchChoice item, int gold)
        {
            var card = Rect("Shelf card", shelfCardLayer, 0, 0, ShelfCardWidth, 10);
            var face = Rounded("Shelf card face", card, 0, 0, ShelfCardWidth, 10, 4, 1, ShelfPlate, ShelfEdge);
            var body = Rect("Shelf card body", card, ShelfCardPad, ShelfCardPad, ShelfCardWidth - 2 * ShelfCardPad, 10);
            float w = ShelfCardWidth - 2 * ShelfCardPad, y;
            if (item.Weapon != null)
            {
                y = LiveParagraph(body, LiveWeaponName(item.Weapon), 0, 16, width: w);
                y = LiveParagraph(body, LiveSkill(item.Weapon), y, 13, CoreBlue, w);
                y = LiveParagraph(body, LiveSkillDescription(item.Weapon), y + 2, 12, width: w);
                for (int i = 0; i < item.Weapon.Slots.Count; i++) LiveArt(ApprovedTheme.Palette(item.Weapon.Slots[i].Color), body, i * 34, y + 4, 30, 21);
                y += 29;
            }
            else
            {
                y = LiveParagraph(body, item.Ink.name, 0, 16, width: w);
                y = LiveParagraph(body, $"+{Sketch.Catalog.inkUsesPerAcquisition}회", y, 13, CoreGold, w);
                y = LiveParagraph(body, LiveInkText(item.Ink), y + 2, 12, width: w);
            }
            // Bottom row: the interact key, then the price (dimmed when the gold falls short).
            y += 6;
            var keycap = WorldInteractionKey(body,0,y,KeyLabel(GameAction.Interact),out _); keycap.name = "Shelf interact key";
            var price = PriceTag(body); SetPrice(price, item.Price, item.Price >= 0 && gold >= item.Price);
            price.anchoredPosition = new Vector2(w - price.sizeDelta.x, -(y + 6));
            y += 30;
            float h = y + 2 * ShelfCardPad;
            card.sizeDelta = new Vector2(ShelfCardWidth, h); face.rectTransform.sizeDelta = new Vector2(ShelfCardWidth, h); body.sizeDelta = new Vector2(w, y);
            return card;
        }

        Image WorldInteractionKey(Transform parent,float x,float y,string binding,out Text label)
        {
            // Share the pause menu keycap geometry and ink, with a dark translucent backing for the world.
            var key=ApprovedKeycap(parent,binding,x,y,28,30,14);
            key.sprite=RoundedSprite(3,1,new Color32(24,29,31,190),new Color32(173,194,204,210));
            label=key.GetComponentInChildren<Text>();
            SetWorldInteractionKey(key,label,binding);return key;
        }
        void SetWorldInteractionKey(Image key,Text label,string binding)
        {
            label.text=binding;
            FitApprovedKeycap(label);
        }

        RectTransform PriceTag(Transform parent)
        {
            var tag = Rect("Shelf price", parent, 0, 0, 40, 18);
            Rounded("Price plate", tag, 0, 0, 40, 18, 3, 1, ShelfPlate, ShelfEdge);
            Glyph(CombatSprite("gold-coin"), tag, 4, 3, 12, 12, Color.white);
            var text = Words("", tag, 19, 0, 30, 18, 12, true, CoreGold, TextAnchor.MiddleLeft);
            if (ApprovedTheme.Font) text.font = ApprovedTheme.Font;
            return tag;
        }

        void SetPrice(RectTransform tag, int price, bool affordable)
        {
            var text = tag.GetComponentInChildren<Text>();
            text.text = price < 0 ? "?" : price.ToString(); text.color = affordable ? CoreGold : CoreMuted;
            float w = 19 + Mathf.Ceil(text.preferredWidth) + 6;
            tag.sizeDelta = new Vector2(w, 18); tag.GetChild(0).GetComponent<RectTransform>().sizeDelta = new Vector2(w, 18);
            text.rectTransform.sizeDelta = new Vector2(w - 19, 18);
        }

        // Plate text in the shop art's own ink: chapter 1 grey outline colour, chapter 2 paper; dimmed when the gold falls
        // short. Galmuri11 Bold at its native 12 px, an 11 px coin before it, both centred on the plate.
        static readonly Color[] PlateText = { new Color32(111, 110, 105, 255), new Color32(243, 240, 226, 255) }, PlateTextDim = { new Color32(160, 157, 150, 255), new Color32(143, 168, 154, 255) };
        RectTransform PlateLabel(Transform parent)
        {
            var tag = Rect("Shelf plate label", parent, 0, 0, 40, 16);
            Glyph(CombatSprite("gold-coin"), tag, 0, 2, 11, 11, Color.white).name = "Coin";
            var text = Rect("Text", tag, 14, 0, 30, 16).gameObject.AddComponent<Text>();
            text.font = boldFont; text.fontSize = 12; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return tag;
        }
        void SetPlateLabel(RectTransform tag, Sprite coin, string words, int chapter, bool bright)
        {
            var image = tag.GetChild(0).GetComponent<Image>(); var text = tag.GetComponentInChildren<Text>();
            image.enabled = coin; if (coin) image.sprite = coin;
            text.text = words; var c = Mathf.Clamp(chapter, 0, 1); text.color = bright ? PlateText[c] : PlateTextDim[c];
            float tw = Mathf.Ceil(text.preferredWidth), lead = coin ? 14 : 0, w = lead + tw;
            tag.sizeDelta = new Vector2(w, 16); text.rectTransform.anchoredPosition = new Vector2(lead, 0); text.rectTransform.sizeDelta = new Vector2(tw, 16);
        }

        // The 960x540 HUD point (y down) of a world point, or null when it is off the picture.
        static Vector2? ShelfPoint(Camera camera, Vector2 world)
        {
            var p = camera.WorldToViewportPoint(world);
            if (p.z <= 0 || p.x < -.05f || p.x > 1.05f || p.y < -.05f || p.y > 1.05f) return null;
            return new Vector2(p.x * 960, (1 - p.y) * 540);
        }
    }
}
