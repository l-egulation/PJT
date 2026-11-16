using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Reward role of a chapter 1 enemy. Content marks elites; bosses use the combat boss flag.
    public sealed class Chapter1EnemyRole : MonoBehaviour
    {
        public const string Normal = "normal", Elite = "elite", Boss = "boss";
        public string role = Normal;

        void Awake(){var actor=GetComponent<CombatActor>();if(actor)actor.isElite=role==Elite;}

        public static string Of(TestEnemy enemy)
        {
            if (!enemy) return Normal;
            var marker = enemy.GetComponent<Chapter1EnemyRole>();
            if (marker && !string.IsNullOrEmpty(marker.role)) return marker.role;
            return enemy.isBoss ? Boss : Normal;
        }

        public static void Mark(Component enemy, string role)
        {
            var marker = enemy.GetComponent<Chapter1EnemyRole>();
            if (!marker) marker = enemy.gameObject.AddComponent<Chapter1EnemyRole>();
            marker.role = role;
            var actor = enemy.GetComponent<CombatActor>();
            if(actor) actor.isElite = role == Elite;
        }
    }
}
