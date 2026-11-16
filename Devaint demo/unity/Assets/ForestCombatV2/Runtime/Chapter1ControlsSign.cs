using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    // A signpost in the chapter 1 starting room (user request 2026-11-10: for players who skipped the opening's road).
    // F beside it opens the window that goes through the opening road's moves (TemporaryGameGui.ControlsWindow). The
    // village's sketched signpost (the chapter is drawn in sketch) with 「튜토리얼」 written over it, a little behind the spawn
    // on the same surface the run starts on (user request 2026-11-13: the start room begins on a ledge above the floor's
    // monsters, and the signpost stands at the start's height, one-way ledge or solid floor), with open air above it and
    // clear of the blueprint stands.
    public sealed class Chapter1ControlsSign : MonoBehaviour
    {
        public const string Art = "Hub/props/sketch/departure-sign", Label = "조작 튜토리얼", SystemLabel = "시스템 튜토리얼";
        public const float SignSpacing = 4.5f;
        // Headroom: the sign (2.6), its label and the F keycap above it.
        // HalfWidth: the signpost's footing, both ends of which must rest on the start's surface.
        const float Reach = 1.6f, PreferredOffset = -2.3f, StandClearance = 1.8f, Headroom = 4.6f, HalfWidth = .5f;
        ForestCombatWorld world;
        bool attempted;
        SpriteRenderer art;
        public KeycapMarker Key { get; private set; }
        public KeycapMarker SystemKey { get; private set; }
        public Vector2 SystemPosition { get; private set; }
        public UnityEngine.UI.Text SystemLabelText { get; private set; }
        public int SystemReads { get; private set; }
        public bool Placed { get; private set; }
        public Vector2 Position { get; private set; }
        // Counts each F at the sign; the GUI opens or closes the strip when it changes.
        public int Reads { get; private set; }

        public void Initialize(ForestCombatWorld owner) => world = owner;

        public bool Prepare()
        {
            if (Placed) return true;
            if (attempted || !world || !world.Ready || world.Data.rooms == null || world.Data.rooms.Length == 0) return false;
            attempted = true;
            var room = world.Data.rooms[0];
            if (room.kind != "start") return false;
            var player = world.arena.Player; if (!player) return false;
            var stands = GetComponent<Chapter1StartingBlueprints>();
            Physics2D.SyncTransforms();
            var bounds = new Rect(room.x, -(room.y + room.h), room.w, room.h);
            var spots = StartLevelSpots(bounds, player.spawn.y)
                .Where(p => Mathf.Abs(p.x - player.spawn.x) >= Reach + .5f &&
                    (!stands || Enumerable.Range(0, 3).All(i => !stands.IsOffered(i) || Mathf.Abs(stands.OfferPosition(i).x - p.x) >= StandClearance)))
                .OrderBy(p => Mathf.Abs(p.x - (player.spawn.x + PreferredOffset))).ThenBy(p => p.x).ToList();
            var pair = spots.SelectMany(left => spots.Where(right => right.x-left.x >= SignSpacing && right.x-left.x <= SignSpacing+.5f)
                .Select(right => new { left, right }))
                .OrderBy(p => Mathf.Abs(p.left.x-(player.spawn.x+PreferredOffset))+Mathf.Abs(p.right.x-(player.spawn.x-PreferredOffset))).FirstOrDefault();
            if (pair == null) { Debug.LogWarning("No room for two tutorial signs at the start's height in the starting room.", this); return false; }
            Position = pair.left; SystemPosition = pair.right;
            var go = new GameObject("Controls sign"); go.transform.SetParent(transform, false); go.transform.position = Position;
            art = go.AddComponent<SpriteRenderer>(); art.sortingOrder = 5;
            art.sprite = Resources.Load<Sprite>(Art);
            if (!art.sprite) throw new InvalidOperationException("Missing controls sign art: " + Art);
            if (world.arena.spriteMaterial) art.sharedMaterial = world.arena.spriteMaterial;
            float top = KeycapMarker.OpaqueTop(art);
            LabelText = MakeLabel(new Vector2(Position.x, top + .12f), Label);
            Key = KeycapMarker.Create(transform, 'F', new Vector2(Position.x, top + .95f), world.arena.spriteMaterial, 6, PlayerControls.For(player));
            Key.SetVisible(false);
            var system = new GameObject("System tutorial sign"); system.transform.SetParent(transform, false); system.transform.position = SystemPosition;
            var systemArt = system.AddComponent<SpriteRenderer>();systemArt.sprite=art.sprite;systemArt.sortingOrder=art.sortingOrder;
            systemArt.sharedMaterial=art.sharedMaterial;
            float systemTop=KeycapMarker.OpaqueTop(systemArt);
            SystemLabelText=MakeLabel(new Vector2(SystemPosition.x,systemTop+.12f),SystemLabel);
            SystemKey=KeycapMarker.Create(transform,'F',new Vector2(SystemPosition.x,systemTop+.95f),world.arena.spriteMaterial,6,PlayerControls.For(player));
            SystemKey.SetVisible(false);
            Placed = true;
            return true;
        }

        // Half-unit steps across the room at the start's height y: both ends of the footing rest on a level surface top at y
        // (solid floor or one-way ledge) and nothing solid or pit sits in the headroom above.
        static IEnumerable<Vector2> StartLevelSpots(Rect room, float y)
        {
            for (float x = Mathf.Ceil(room.xMin + 1) + .5f; x <= room.xMax - 1; x += .5f)
            {
                var at = new Vector2(x, y);
                if (!Supported(at + Vector2.left * HalfWidth) || !Supported(at + Vector2.right * HalfWidth)) continue;
                bool blocked = Physics2D.OverlapBoxAll(at + Vector2.up * (Headroom / 2 + .08f), new Vector2(HalfWidth * 2, Headroom), 0)
                    .Any(c => c.GetComponent<ArenaSurface>() || c.GetComponent<ArenaPit>());
                if (!blocked) yield return at;
            }
        }
        static bool Supported(Vector2 at) => Physics2D.RaycastAll(at + Vector2.up * .15f, Vector2.down, .3f)
            .Any(h => h.collider.GetComponent<ArenaSurface>() && h.normal.y > .9f && Mathf.Abs(h.point.y - at.y) < .05f);

        public UnityEngine.UI.Text LabelText { get; private set; }

        // 「튜토리얼」 in the HUD's pixel font, ink with a paper rim, standing on the sign (world space, one font pixel a world pixel).
        UnityEngine.UI.Text MakeLabel(Vector2 at, string caption)
        {
            var go = new GameObject("Controls sign label", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false); go.transform.position = at; go.transform.localScale = Vector3.one / KeycapMarker.Ppu;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 6;
            var rect = (RectTransform)go.transform; rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(128, 16);
            var text = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            text.transform.SetParent(go.transform, false);
            text.font = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12; text.alignment = TextAnchor.MiddleCenter; text.color = new Color32(0x1f, 0x1b, 0x19, 255); text.text = caption;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            var rim = text.gameObject.AddComponent<UnityEngine.UI.Outline>(); rim.effectColor = new Color32(0xff, 0xfa, 0xf0, 255); rim.effectDistance = Vector2.one;
            return text;
        }

        public bool PlayerNear
        {
            get
            {
                var p = world ? world.arena.Player : null;
                return Placed && p && !p.Dead && Mathf.Abs(p.Position.x - Position.x) <= Reach && Mathf.Abs(p.Position.y - Position.y) <= 1;
            }
        }

        public bool SystemPlayerNear
        {
            get
            {
                var p=world?world.arena.Player:null;
                return Placed && p && !p.Dead && Mathf.Abs(p.Position.x-SystemPosition.x)<=Reach && Mathf.Abs(p.Position.y-SystemPosition.y)<=1;
            }
        }
        public bool TryReadSystem()
        {
            if(!SystemPlayerNear || !InteractionAllowed)return false;
            SystemReads++;return true;
        }

        bool InteractionAllowed => world && world.arena && world.arena.Player && !world.arena.Player.Dead && !world.arena.Player.InputBlocked &&
            !world.arena.Controls.Current.SimulationPaused && !world.arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0;

        public bool TryRead()
        {
            if (!PlayerNear || !InteractionAllowed) return false;
            Reads++;
            return true;
        }

        void Update()
        {
            if (!Prepare()) return;
            bool offer = PlayerNear && InteractionAllowed;
            Key.SetVisible(offer);
            bool systemOffer=SystemPlayerNear && InteractionAllowed;
            SystemKey.SetVisible(systemOffer);
            if (offer) PlayerControls.TryPerform(world.arena.Player, GameAction.Interact, TryRead);
            else if(systemOffer) PlayerControls.TryPerform(world.arena.Player,GameAction.Interact,TryReadSystem);
        }
    }
}
