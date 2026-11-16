using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // Ink equipment changes are allowed only inside a maintenance facility room. The fixed ver2 map's
    // shop slots are the same unified facility; the random stage places one maintenance room.
    public sealed class Chapter1MaintenanceFacility : MonoBehaviour
    {
        public static bool IsFacility(string kind) => kind == "maintenance" || kind == "shop";
        readonly List<Rect> rooms = new List<Rect>();
        ForestCombatWorld world;

        public IReadOnlyList<Rect> Rooms => rooms;

        public void Initialize(ForestCombatWorld owner)
        {
            world = owner;
            rooms.Clear();
            // Room tiles map to world units with y pointing down, as in the generated layout.
            foreach (var room in world.Data.rooms ?? Array.Empty<ForestRoom>())
                if (IsFacility(room.kind)) rooms.Add(new Rect(room.x, -(room.y + room.h), room.w, room.h));
            world.arena.Player.MaintenanceSite = PlayerInside;
        }

        public bool Contains(Vector2 point) => RoomAt(point) >= 0;

        public int RoomAt(Vector2 point)
        {
            for (int i = 0; i < rooms.Count; i++) if (rooms[i].Contains(point)) return i;
            return -1;
        }

        public bool PlayerInside() => world && world.arena && world.arena.Player && Contains(world.arena.Player.Position);

        void OnDestroy()
        {
            if (world && world.arena && world.arena.Player) world.arena.Player.MaintenanceSite = null;
        }

        public bool ExternalPresentation { get; set; }

    }
}
