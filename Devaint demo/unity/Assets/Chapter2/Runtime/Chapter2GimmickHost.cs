using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Chapter2
{
    // Chapter 2's seal challenge room and locked chest with its key carrier (docs/design/world/stage-gimmicks.md, applied to
    // chapter 2 by user decision 2026-11-13). The chapter 1 components run unchanged on this host; the sprites are the same
    // art recoloured to the v26 sewer palette (Resources/Chapter2Gimmicks).
    public sealed class Chapter2GimmickHost : IStageGimmickHost
    {
        public const string Art = "Chapter2Gimmicks/";
        readonly Chapter2World world;
        StageGimmickSite seal, chest;

        public Chapter2GimmickHost(Chapter2World owner) { world = owner; }

        public event Action<TestEnemy> EnemyDefeated;
        internal void RaiseDefeated(TestEnemy enemy) => EnemyDefeated?.Invoke(enemy);

        public CombatArena Arena => world ? world.arena : null;
        public bool Ready => world && world.Ready;
        public int Seed => world.Layout.variant;
        public RunSession Run => world.SourceRun;
        public string ArtFolder => Art;

        public StageGimmickSite Seal => seal ??= Site(world.Layout.seal);
        public StageGimmickSite Chest => chest ??= Site(world.Layout.chest);
        static StageGimmickSite Site(C2Gimmick g)
        {
            if (g == null) return null;
            var site = new StageGimmickSite { RoomId = g.roomId, Marker = g.Marker };
            if (g.drop != null) { site.DropLeft = g.drop.x; site.DropRight = g.drop.x + g.drop.w; site.DropBottom = g.drop.y; site.DropTop = g.drop.y + g.drop.h; }
            site.Gates = (g.gates ?? Array.Empty<C2Bounds>()).Select(b => b.Bounds).ToArray();
            return site;
        }

        // The normal species placed in this stage's combat rooms (the elite is not one of them).
        public IReadOnlyList<string> SealSpecies =>
            world.Layout.actors.Where(a => a.id != "E:elite" && world.Layout.Room(a.roomId)?.kind == "combat").Select(a => a.species).Distinct().ToList();

        public FieldMouseEnemy SpawnSealMonster(string species, Vector2 feet, int facing) => world.SpawnGimmickMonster(species, feet, facing, world.Layout.seal.roomId);
        public void RemoveSealMonster(TestEnemy enemy) => world.RemoveGimmickMonster(enemy);
        public float SealClearHealth => world.Economy != null ? world.Economy.RoomClearHealth("combat") : 0;

        // Only plain walkers carry the key (user 2026-11-05): the broom stalker (K4) and the thorn root (M3). The charger (B3)
        // has its own charge movement; the gutter spitter and the turret stand and shoot.
        public static bool CanCarry(Chapter2NormalEnemy mob) => mob && !mob.Dead && (mob.ArtId == "K4" || mob.ArtId == "M3") &&
            mob.Definition != null && !mob.Definition.immovable && mob.Definition.walkSpeed > .1f;

        public IEnumerable<FieldMouseEnemy> CarrierCandidates()
        {
            var rooms = new HashSet<string>(world.Layout.carrierRooms ?? Array.Empty<string>());
            foreach (var entry in world.Layout.actors)
                if (rooms.Contains(entry.roomId) && world.Actors.TryGetValue(entry.id, out var actor) && actor is Chapter2NormalEnemy mob && CanCarry(mob))
                    yield return mob;
        }

        public void BindCarrier(FieldMouseEnemy carrier) { if (carrier is Chapter2NormalEnemy mob) mob.FollowsMovementOverride = true; }

        public Rect CarrierHome(FieldMouseEnemy carrier)
        {
            var room = world.Layout.RoomAt(carrier.spawn);
            return room != null ? room.Bounds : default;
        }

        public Chapter1KeyCarrierRoute BuildCarrierRoute(Vector2 body, float speed, float fallGravity, float maxClimb) =>
            new Chapter1KeyCarrierRoute(world.Layout.carrierMap, body, speed, fallGravity, maxClimb, world.Layout.carrierMapOffset);
    }
}
