using Deviant.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // Development shortcuts for gimmick playtests (Editor and Development Builds only, like F5):
    // Alt+1 seal tablet, Alt+2 locked chest, Alt+3 key carrier (Ctrl+digits are Unity editor window shortcuts). The player lands two units left of the target.
    public sealed class Chapter1GimmickDevKeys : MonoBehaviour
    {
        ForestCombatWorld world;

        public void Initialize(ForestCombatWorld owner) => world = owner;

        public bool JumpToSeal()
        {
            var seal = GetComponent<Chapter1SealChallenge>();
            return seal && seal.Prepare() && Place(seal.Room, seal.TabletPosition);
        }

        public bool JumpToChest()
        {
            var keys = GetComponent<Chapter1KeyChest>();
            if (!keys) return false;
            keys.Prepare();
            return keys.Chest && Place(System.Array.Find(world.Data.rooms, r => r.kind == "chest"), keys.Chest.Position);
        }

        public bool JumpToCarrier()
        {
            var keys = GetComponent<Chapter1KeyChest>();
            if (!keys) return false;
            keys.Prepare();
            if (!keys.Carrier || keys.Carrier.Dead) return false;
            var at = keys.Carrier.Position;
            ForestRoom room = null;
            foreach (var r in world.Data.rooms) if (new Rect(r.x, -(r.y + r.h), r.w, r.h).Contains(keys.Carrier.spawn + Vector2.up * .5f)) room = r;
            return Place(room, at);
        }

        bool Place(ForestRoom room, Vector2 target)
        {
            if (room == null) return false;
            // Skipping the start room would leave the player unarmed: take the first offered blueprints.
            var blueprints = world.GetComponent<Chapter1StartingBlueprints>();
            if (blueprints && blueprints.SelectedCount == 0) blueprints.ApplyPreselected(new[] { "sword", "blunt" });
            world.Warp(room.id);
            var player = world.arena.Player;
            player.Position = target + Vector2.left * 2 + Vector2.up * .1f; player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            world.RefreshCamera(0, true);
            return true;
        }
    }
}
