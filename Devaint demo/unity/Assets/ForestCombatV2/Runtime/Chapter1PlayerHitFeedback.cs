using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    [DefaultExecutionOrder(1000)]
    public sealed class Chapter1PlayerHitFeedback : MonoBehaviour
    {
        public CombatPlayer player;
        // Compatibility probe for existing scenes/tools. CombatFeelFeedback owns
        // the only hurt visual: a red screen edge. Never modify the player's sprite.
        public float FlashStrength { get; private set; }

        public void Sample()
        {
            FlashStrength = isActiveAndEnabled && player && !player.Dead && player.arena && player.arena.FeelFeedback
                ? player.arena.FeelFeedback.PlayerHurtEnvelope : 0;
        }

        void LateUpdate() => Sample();

        void OnDisable()
        {
            FlashStrength = 0;
        }
    }
}
