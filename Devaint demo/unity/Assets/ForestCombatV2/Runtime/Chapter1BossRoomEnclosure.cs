using System;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Every boss room is exactly one fixed camera view (30 x 16.875 units). While the player is inside,
    // the camera stays on the room before, during and after the fight, and the entry is closed so both
    // sides and the ceiling remain walls just outside the view. Leaving (a checkpoint return) reopens it.
    public sealed class Chapter1BossRoomEnclosure : MonoBehaviour
    {
        ForestCombatWorld world;
        Rect room;
        float floor;
        BoxCollider2D gate;
        public bool Holding { get; private set; }
        public bool EntryShut => gate && gate.enabled;
        public float RoomWidth => room.width;
        public float? ApproachCameraRightPixels
        {
            get
            {
                var player=world&&world.arena?world.arena.Player:null;
                return player && !Contains(player.Position) && player.Position.x<room.x+1 && Mathf.Abs(player.Position.y-floor)<15
                    ? room.x*32 : (float?)null;
            }
        }
        public Vector2 CameraCenter => new Vector2(room.x + room.width / 2, floor + 6);

        public void Initialize(ForestCombatWorld owner)
        {
            world = owner;
            var source = Array.Find(world.Data.rooms ?? Array.Empty<ForestRoom>(), r => r.kind == "boss");
            if (source == null) { enabled = false; return; }
            if (!GetComponent<Chapter1BossBackdrop>()) gameObject.AddComponent<Chapter1BossBackdrop>().Initialize(owner, source);
            floor = -(source.y + source.entryY);
            room = new Rect(source.x, floor - 3, source.w, source.h);
            var go = new GameObject("Boss room entry wall"); go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(source.x - .125f, floor + 7.5f);
            go.AddComponent<ArenaSurface>(); gate = go.AddComponent<BoxCollider2D>(); gate.size = new Vector2(.25f, 15); gate.enabled = false;
        }

        // Entering needs one unit past the entry; once held, standing against the closed entry still counts as inside.
        public bool Contains(Vector2 position) =>
            position.x >= room.x + (Holding ? -2 : 1) && position.x <= room.xMax && position.y >= floor - 2 && position.y <= floor + 15;

        bool BossCleared => world.arena.enemies.Exists(e => e && e.isBoss && e.Dead);

        void LateUpdate() => Hold();

        // Also called before the camera refresh by anything that moves the player into the room directly.
        public void Hold()
        {
            var player = world && world.arena ? world.arena.Player : null;
            if (!player) return;
            bool inside = Contains(player.Position);
            if (inside != Holding)
            {
                Holding = inside;
                if (!inside) world.FixedCameraCenter = null;
            }
            // The entry stays shut only until the boss falls, so the player can walk back out afterwards.
            bool shut = inside && !BossCleared;
            if (gate.enabled != shut) { gate.enabled = shut; Physics2D.SyncTransforms(); }
            if (inside) world.FixedCameraCenter = CameraCenter;
            HoldFraming(inside);
        }

        // The combat-feel camera zooms in 12%; inside the room that would crop the attacks' edge warnings.
        void HoldFraming(bool hold)
        {
            var camera = world && world.arena && world.arena.FeelFeedback ? world.arena.FeelFeedback.CombatCamera : null;
            if (camera) camera.HoldBaseFraming = hold;
        }

        void OnDisable() { if (Holding && world) world.FixedCameraCenter = null; Holding = false; if (gate) gate.enabled = false; HoldFraming(false); }
    }
}
