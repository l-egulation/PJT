using UnityEngine;

namespace ForestCombatV2
{
    // The hero trapped at the well (opening ②, 「저쪽에서도!」): looks at the closing fronts in turn (HTML prototype
    // 2026-11-05 「너무 두리번거린다」, 「떨지 마」): it glances away from where it faced, back, away again, every `interval`
    // seconds, then faces as it started and stays so. It does not tremble. Ends by itself after `seconds`.
    public sealed partial class StartingVillageWorld
    {
        const int FearGlances = 3;
        float fearUntil = -1, fearInterval, fearStarted;
        int fearStartFacing;
        public bool HeroFearing => fearUntil >= 0 && Time.unscaledTime < fearUntil;

        public void StartHeroFear(float seconds, float interval)
        {
            var player = World.arena.Player;
            if (!player) return;
            fearStarted = Time.unscaledTime; fearUntil = fearStarted + Mathf.Max(.1f, seconds);
            fearInterval = interval > 0 ? interval : .75f; fearStartFacing = player.facing == 0 ? 1 : player.facing;
        }

        void TickHeroFear()
        {
            if (fearUntil < 0) return;
            var player = World.arena.Player;
            if (!player) { fearUntil = -1; return; }
            if (Time.unscaledTime >= fearUntil) { player.facing = fearStartFacing; fearUntil = -1; return; }
            int turn = Mathf.FloorToInt((Time.unscaledTime - fearStarted) / fearInterval);
            player.facing = turn < FearGlances && turn % 2 == 0 ? -fearStartFacing : fearStartFacing;
        }
    }
}
