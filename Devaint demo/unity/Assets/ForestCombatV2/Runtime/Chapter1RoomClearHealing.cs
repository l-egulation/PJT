using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Room membership follows the spawn, so chasing an enemy across a doorway cannot move its reward.
    public sealed class Chapter1RoomClearHealing : MonoBehaviour
    {
        sealed class Encounter
        {
            public ForestRoom Room;
            public readonly List<TestEnemy> Enemies = new List<TestEnemy>();
            public bool Cleared;
        }
        readonly List<Encounter> encounters = new List<Encounter>();
        readonly HashSet<TestEnemy> registered = new HashSet<TestEnemy>();
        ForestCombatWorld world;
        Chapter1RunBinding binding;
        CombatArena arena;
        // Raised with the health actually restored; a clear at full health restores nothing and stays silent.
        public event Action<float> Healed;
        // Extra health per cleared room (run augment 회복력).
        public int BonusHealth { get; set; }
        public void Initialize(ForestCombatWorld owner, Chapter1RunBinding runBinding)
        { world = owner; binding = runBinding; }

        public void Tick()
        {
            if (!world || !world.Ready || world.dummyTrainingOnly || !binding || binding.Run.Ended) return;
            if (!arena)
            {
                arena = world.arena;
                arena.ResetCompleted += ResetRooms;
                ResetRooms();
            }
            var player = arena.Player;
            if (!player || player.Dead) return;
            foreach (var enemy in arena.enemies)
            {
                if (!enemy || !registered.Add(enemy)) continue;
                foreach (var encounter in encounters)
                {
                    var room = encounter.Room;
                    if (!new Rect(room.x, -(room.y + room.h), room.w, room.h).Contains(enemy.spawn + Vector2.up * .5f)) continue;
                    encounter.Enemies.Add(enemy);
                    break;
                }
            }
            foreach (var encounter in encounters)
            {
                if (encounter.Cleared || encounter.Enemies.Count == 0) continue;
                bool alive = false;
                foreach (var enemy in encounter.Enemies) if (enemy && !enemy.Dead) { alive = true; break; }
                if (alive) continue;
                encounter.Cleared = true;
                int amount = (binding.Run.Economy?.RoomClearHealth(encounter.Room.kind) ?? 0) + BonusHealth;
                float restored = player.Heal(amount);
                if (restored > 0) Healed?.Invoke(restored);
            }
        }
        void ResetRooms()
        {
            encounters.Clear(); registered.Clear();
            foreach (var room in world.Data.rooms)
                if (room.kind == "combat" || room.kind == "elite" || room.kind == "boss")
                    encounters.Add(new Encounter { Room = room });
        }
        void OnDestroy() { if (arena) arena.ResetCompleted -= ResetRooms; }
    }
}
