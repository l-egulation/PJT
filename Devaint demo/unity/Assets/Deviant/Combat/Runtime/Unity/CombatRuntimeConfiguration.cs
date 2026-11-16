using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public enum CombatCompositionProfile { Unconfigured, Game, Lab }

    // Scene roots supply content and creation requests before the first simulation tick.
    public sealed class CombatRuntimeConfiguration
    {
        public CombatCompositionProfile Profile;
        public Catalog Catalog;
        public Deviant.Equipment.Contracts.IWeaponLoadout Loadout;
        public LocomotionData Locomotion;
        public CharacterAnimationCatalog Animations;
        public Camera Camera;
        public Material SpriteMaterial;
        // Optional player-only material; other sprites keep SpriteMaterial.
        public Material PlayerMaterial;
        public Sprite PlayerSprite;
        public Vector2 PlayerSpawn, PlayerSize, PlayerOffset;
        public int PlayerSortingOrder, SecondaryFormIndex;
        public System.Action<CombatArena> PrepareEnvironment;
        public System.Action<CombatPlayer> BindPlayerInput;
        public System.Action<CombatArena> BindFeedback;
        public IReadOnlyList<CombatEnemySpawn> Enemies;
        // Custom factories fully initialize an actor; the arena registers the returned instance.
        public System.Func<CombatArena, CombatEnemySpawn, TestEnemy> EnemyFactory;
    }

    public readonly struct CombatEnemySpawn
    {
        public readonly string Name;
        public readonly int Profile, SortingOrder;
        public readonly Vector2 Position, Size, Offset;
        public readonly float MaxHealth;
        public readonly bool IsBoss, Aggressive;
        public CombatEnemySpawn(string name, int profile, Vector2 position, Vector2 size, Vector2 offset,
            float maxHealth, bool isBoss, bool aggressive, int sortingOrder)
        {
            Name = name; Profile = profile; Position = position; Size = size; Offset = offset;
            MaxHealth = maxHealth; IsBoss = isBoss; Aggressive = aggressive; SortingOrder = sortingOrder;
        }
    }

    public readonly struct CombatDebugFlash
    {
        public readonly Bounds box;
        public readonly float end;
        public readonly Color color;
        public CombatDebugFlash(Bounds box, float end, Color color) { this.box = box; this.end = end; this.color = color; }
    }
}
