using System;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable]
    public sealed class Chapter1EnemyDefinitions
    {
        public FieldMouseDefinition[] monsters;
        public static FieldMouseDefinition Load(string species)
        {
            var source = Resources.Load<TextAsset>("Chapter1Enemies");
            if (!source) throw new InvalidOperationException("Run tools/combat/export_chapter1_enemies.py.");
            var catalog = JsonUtility.FromJson<Chapter1EnemyDefinitions>(source.text);
            var definition = Array.Find(catalog.monsters, m => m.id == species);
            if (definition == null || definition.bodySize.Length != 2 || definition.attackFrameDurations.Length != 8)
                throw new InvalidOperationException("Missing approved enemy definition: " + species);
            return definition;
        }
    }
}
