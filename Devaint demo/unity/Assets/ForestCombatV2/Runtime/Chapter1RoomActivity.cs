using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Room-based activation for the procedural chapter: the player's room and the rooms its passages lead to are
    // awake; idle monsters standing in any other room rest. A player in a passage wakes both rooms it joins. When the
    // player or the monster is in no room, the distance rule of the monster (distantIdleRange) decides instead.
    public sealed class Chapter1RoomActivity
    {
        readonly ForestRoom[] rooms;
        readonly ForestPassage[] passages;
        readonly List<int>[] neighbours;
        readonly bool[] awake;
        readonly CombatArena arena;
        readonly Func<long> tickNow;
        readonly Func<Vector2?> playerAt;
        long refreshedTick = -1;
        Vector2? refreshedPlayer;
        bool playerPlaced;

        public Chapter1RoomActivity(ForestData data, CombatArena arena)
            : this(data, () => arena ? arena.SimulationTickCount : -1, () => arena && arena.Player ? arena.Player.Position : (Vector2?)null)
        { this.arena = arena ? arena : throw new ArgumentNullException(nameof(arena)); }
        // Clock and player source for tests; the decision is cached per simulation tick.
        public Chapter1RoomActivity(ForestData data, Func<long> tick, Func<Vector2?> player)
        {
            if (data == null || data.rooms == null || data.rooms.Length == 0) throw new ArgumentException("Room activation requires procedural rooms.");
            tickNow = tick ?? throw new ArgumentNullException(nameof(tick)); playerAt = player ?? throw new ArgumentNullException(nameof(player));
            rooms = data.rooms; passages = data.passages ?? Array.Empty<ForestPassage>();
            var index = new Dictionary<int, int>();
            for (int i = 0; i < rooms.Length; i++) index[rooms[i].id] = i;
            neighbours = new List<int>[rooms.Length];
            for (int i = 0; i < rooms.Length; i++) neighbours[i] = new List<int>();
            foreach (var p in passages)
                if (index.TryGetValue(p.from, out int a) && index.TryGetValue(p.to, out int b) && a != b)
                { if (!neighbours[a].Contains(b)) neighbours[a].Add(b); if (!neighbours[b].Contains(a)) neighbours[b].Add(a); }
            awake = new bool[rooms.Length];
        }

        // The room index containing a world point, or -1. Rooms are tile rectangles with rows growing downward.
        public int RoomAt(Vector2 world)
        {
            float x = world.x, y = -world.y;
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                if (x >= r.x && x < r.x + r.w && y >= r.y && y < r.y + r.h) return i;
            }
            return -1;
        }
        int PassageAt(Vector2 world)
        {
            float x = world.x, y = -world.y;
            for (int i = 0; i < passages.Length; i++)
            {
                var p = passages[i];
                if (x >= p.x && x < p.x + p.w && y >= p.y && y < p.y + p.h) return i;
            }
            return -1;
        }
        public bool Uses(ForestData data, CombatArena owner) => data != null && ReferenceEquals(rooms, data.rooms) && arena && arena == owner;
        public bool Awake(int room) { Refresh(); return room >= 0 && room < awake.Length && awake[room]; }
        public bool PlayerPlaced { get { Refresh(); return playerPlaced; } }

        void Refresh()
        {
            // Recomputed each simulation tick, and at once when the player is moved within a tick (warps, portals).
            long tick = tickNow(); var player = playerAt();
            if (tick == refreshedTick && player == refreshedPlayer) return;
            refreshedTick = tick; refreshedPlayer = player;
            Array.Clear(awake, 0, awake.Length); playerPlaced = false;
            if (player == null) return;
            var at = player.Value;
            int room = RoomAt(at);
            if (room >= 0)
            {
                awake[room] = true; foreach (int n in neighbours[room]) awake[n] = true;
                playerPlaced = true; return;
            }
            int passage = PassageAt(at);
            if (passage < 0) return;
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i].id == passages[passage].from || rooms[i].id == passages[passage].to) { awake[i] = true; playerPlaced = true; }
        }

        // true: the monster's room is asleep; false: awake; null: the player or the monster is in no room.
        public bool? Rests(Vector2 monster)
        {
            Refresh();
            int room = playerPlaced ? RoomAt(monster) : -1;
            return room < 0 ? (bool?)null : !awake[room];
        }
        // FieldMouseEnemy.OutsideActiveArea: true when the idle monster may rest this step.
        public bool Outside(FieldMouseEnemy enemy) => Rests(enemy.Position) ?? enemy.BeyondDistantIdleRange();
    }
}
