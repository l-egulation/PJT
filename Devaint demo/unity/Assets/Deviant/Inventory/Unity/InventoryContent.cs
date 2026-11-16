using System;
using UnityEngine;

namespace Deviant.Inventory.Unity
{
    [Serializable]
    public sealed class InkContentEntry
    {
        public string id, displayName, description, formId, weaponCategory;
        public InkKind kind;
        public int value;
        public Sprite icon;
        public Color tint = Color.white;
        public InkDefinition CreateDefinition() => new InkDefinition(id, displayName, description, kind, value, formId, weaponCategory);
    }
    [Serializable] public sealed class InkDropEntry { public string itemId; public int amount = 1; }
    [Serializable]
    public sealed class InkDropTable
    {
        public string key;
        // Legacy serialized presentation hint; loot no longer waits for it.
        [Min(0)] public float deathSeconds;
        public InkDropEntry[] entries = Array.Empty<InkDropEntry>();
    }
    [CreateAssetMenu(menuName = "Deviant/Inventory content")]
    public sealed class InventoryContent : ScriptableObject
    {
        [Min(1)] public int capacity = 5;
        public InkContentEntry[] items = Array.Empty<InkContentEntry>();
        // Temporary guaranteed payouts. This asset is not a final reward/balance table.
        public InkDropTable[] tables = Array.Empty<InkDropTable>();
        public InkContentEntry Find(string id) => Array.Find(items, item => item.id == id);
        public InkDropTable Table(string key) => Array.Find(tables, table => table.key == key);
        public void Validate()
        {
            if (capacity < 1 || items == null || tables == null) throw new InvalidOperationException("Invalid inventory content.");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
                if (item == null || !ids.Add(item.id)) throw new InvalidOperationException("Duplicate/null ink definition.");
                else item.CreateDefinition();
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                if (table == null || string.IsNullOrWhiteSpace(table.key) || !keys.Add(table.key) || table.entries == null ||
                    float.IsNaN(table.deathSeconds) || float.IsInfinity(table.deathSeconds) || table.deathSeconds < 0)
                    throw new InvalidOperationException("Invalid drop table.");
                int count = 0;
                foreach (var entry in table.entries)
                {
                    if (entry == null || Find(entry.itemId) == null || entry.amount <= 0 || entry.amount > 32)
                        throw new InvalidOperationException("A drop needs a known ID and quantity 1..32.");
                    count += entry.amount;
                }
                if (count > 32) throw new InvalidOperationException("Too many temporary drops per monster.");
            }
        }
    }
}
