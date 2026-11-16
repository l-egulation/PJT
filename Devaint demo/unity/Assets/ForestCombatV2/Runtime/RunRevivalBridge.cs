using Deviant.Combat;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Connects the run's revival unlock (docs/design/progression/roguelite-progression.md 충전소와 부활) to the chapter
    // arena's player: a lethal hit restores the run's revival share once per run. Weapons, rewards and shops of the run
    // stay in SketchRunState; this bridge owns no equipment.
    public sealed class RunRevivalBridge : MonoBehaviour
    {
        CombatArena arena;
        RunSession run;
        // The chapter's run; the GUI reads its shared reroll budget from here.
        public RunSession Run => run;

        public static RunRevivalBridge Attach(CombatArena arena, RunSession run)
        {
            if (!arena || run == null) return null;
            var bridge = arena.GetComponent<RunRevivalBridge>() ?? arena.gameObject.AddComponent<RunRevivalBridge>();
            bridge.arena = arena; bridge.run = run;
            bridge.Bind();
            return bridge;
        }

        // The player may appear after the bridge; bind once it exists.
        void Update() => Bind();
        public void Bind()
        {
            if (arena && arena.Player && arena.Player.RunRevival == null) arena.Player.RunRevival = Revive;
        }
        float Revive() => run.Perks.TryRevive(arena.Player.health, arena.Player.maxHealth, out float restored) ? restored : 0;

        void OnDestroy()
        {
            if (arena && arena.Player) arena.Player.RunRevival = null;
        }
    }
}
