using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Where a stage puts its seal challenge or locked chest (docs/design/world/stage-gimmicks.md). World space: x right, y up.
    public sealed class StageGimmickSite
    {
        public string RoomId;
        // Tablet or chest foot.
        public Vector2 Marker;
        // Seal drops: columns DropLeft..DropRight are probed from DropTop down to DropBottom for a floor.
        public float DropLeft, DropRight, DropTop, DropBottom;
        // Seal gates: one box per doorway the challenge closes.
        public Rect[] Gates = Array.Empty<Rect>();
    }

    // The seal challenge and the locked chest with its key carrier run on any stage that supplies this:
    // chapter 1 (Chapter1GimmickHost) and chapter 2 (Deviant.Chapter2.Chapter2GimmickHost).
    public interface IStageGimmickHost
    {
        CombatArena Arena { get; }
        bool Ready { get; }
        int Seed { get; }
        // The run whose reward choices the chests open; null in direct editor loads.
        RunSession Run { get; }
        // Resources folder of the tablet, gate, chest, key and shadow sprites (ends with '/').
        string ArtFolder { get; }
        StageGimmickSite Seal { get; }
        StageGimmickSite Chest { get; }
        // Species the seal drops (the normal species placed in this stage's combat rooms).
        IReadOnlyList<string> SealSpecies { get; }
        FieldMouseEnemy SpawnSealMonster(string species, Vector2 feet, int facing);
        void RemoveSealMonster(TestEnemy enemy);
        // Health a successful seal challenge restores (the same as a cleared combat room).
        float SealClearHealth { get; }
        event Action<TestEnemy> EnemyDefeated;
        // Plain walking monsters in the main-route combat rooms beyond the chest branch.
        IEnumerable<FieldMouseEnemy> CarrierCandidates();
        // Lets the chosen carrier follow FieldMouseEnemy.MovementOverride.
        void BindCarrier(FieldMouseEnemy carrier);
        // The carrier's starting room and its neighbours (verification and debugging).
        Rect CarrierHome(FieldMouseEnemy carrier);
        Chapter1KeyCarrierRoute BuildCarrierRoute(Vector2 body, float speed, float fallGravity, float maxClimb);
    }

    // Chapter 1: the procedural forest map (ForestData) and its run binding.
    public sealed class Chapter1GimmickHost : IStageGimmickHost
    {
        public readonly ForestCombatWorld World;
        readonly Chapter1RunBinding binding;
        StageGimmickSite seal, chest;
        List<string> species;

        public Chapter1GimmickHost(ForestCombatWorld world, Chapter1RunBinding runBinding)
        {
            World = world; binding = runBinding;
            if (binding) binding.EnemyDefeated += enemy => EnemyDefeated?.Invoke(enemy);
        }

        public event Action<TestEnemy> EnemyDefeated;
        public CombatArena Arena => World ? World.arena : null;
        public bool Ready => World && World.Ready;
        public int Seed => World.Data.generationSeed;
        public RunSession Run => binding ? binding.Run : null;
        public string ArtFolder => "Chapter1Gimmicks/";
        public ForestRoom SealRoom => Ready ? Array.Find(World.Data.rooms, r => r.kind == "seal") : null;

        public StageGimmickSite Seal
        {
            get
            {
                if (seal != null || !Ready) return seal;
                var room = SealRoom;
                var mark = Array.Find(World.Data.landmarks ?? Array.Empty<ForestLandmark>(), l => l.kind == "seal");
                if (room == null || mark == null) return null;
                int frame = BlockMap.BlockSpec.Frame;
                float width = Chapter1SealChallenge.GateWidth;
                Rect Gate(float x, int doorFloor) { float top = -room.y, bottom = -(room.y + doorFloor); return new Rect(x, bottom, width, top - bottom); }
                return seal = new StageGimmickSite
                {
                    RoomId = room.id.ToString(), Marker = new Vector2(mark.x / 32, -mark.y / 32f),
                    DropLeft = room.x + 3, DropRight = room.x + room.w - 3, DropTop = -room.y - frame - 1, DropBottom = -(room.y + room.h),
                    Gates = new[] { Gate(room.x, room.entryY), Gate(room.x + room.w - width, room.exitY) },
                };
            }
        }

        public StageGimmickSite Chest
        {
            get
            {
                if (chest != null || !Ready) return chest;
                var room = Array.Find(World.Data.rooms, r => r.kind == "chest");
                var mark = Array.Find(World.Data.landmarks ?? Array.Empty<ForestLandmark>(), l => l.kind == "chest");
                if (room == null || mark == null) return null;
                return chest = new StageGimmickSite { RoomId = room.id.ToString(), Marker = new Vector2(mark.x / 32, -mark.y / 32f) };
            }
        }

        public IReadOnlyList<string> SealSpecies
        {
            get
            {
                if (species != null || !Ready) return species ?? new List<string>();
                species = new List<string>();
                foreach (var actor in World.Data.actors)
                {
                    var source = Array.Find(World.Data.rooms, r => r.id == actor.zone);
                    string id = World.Data.monsters[actor.kind].id;
                    if (source != null && source.kind == "combat" && !species.Contains(id)) species.Add(id);
                }
                return species;
            }
        }

        public FieldMouseEnemy SpawnSealMonster(string kind, Vector2 feet, int facing) => World.SpawnMonster(kind, feet, true, facing);
        public void RemoveSealMonster(TestEnemy enemy) => World.RemoveMonster(enemy);
        public float SealClearHealth => binding && binding.Run?.Economy != null ? binding.Run.Economy.RoomClearHealth("combat") : 0;

        public IEnumerable<FieldMouseEnemy> CarrierCandidates()
        {
            foreach (var room in World.Data.rooms)
            {
                if (!Chapter1KeyChest.CarrierRoom(World.Data, room)) continue;
                var area = Area(room);
                foreach (var enemy in World.arena.enemies)
                    if (enemy is FieldMouseEnemy mob && Chapter1KeyChest.CanCarry(mob) && area.Contains(mob.spawn + Vector2.up * .5f))
                        yield return mob;
            }
        }

        public void BindCarrier(FieldMouseEnemy carrier) { }

        // Its room and the rooms next to it, with the passages between (playtest 2026-11-06: it may run into the next room).
        public Rect CarrierHome(FieldMouseEnemy carrier)
        {
            ForestRoom home = null;
            foreach (var room in World.Data.rooms)
                if (Area(room).Contains(carrier.spawn + Vector2.up * .5f)) home = room;
            if (home == null) return default;
            var area = Area(home);
            foreach (var passage in World.Data.passages ?? Array.Empty<ForestPassage>())
            {
                if (passage.from != home.id && passage.to != home.id) continue;
                var next = Array.Find(World.Data.rooms, r => r.id == (passage.from == home.id ? passage.to : passage.from));
                area = Union(Union(area, new Rect(passage.x, -(passage.y + passage.h), passage.w, passage.h)), next != null ? Area(next) : area);
            }
            return area;
        }

        public Chapter1KeyCarrierRoute BuildCarrierRoute(Vector2 body, float speed, float fallGravity, float maxClimb) =>
            new Chapter1KeyCarrierRoute(World.Data, body, speed, fallGravity, maxClimb);

        static Rect Area(ForestRoom room) => new Rect(room.x, -(room.y + room.h), room.w, room.h);
        public static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }
}
