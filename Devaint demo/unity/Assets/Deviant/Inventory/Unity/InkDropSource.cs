using UnityEngine;

namespace Deviant.Inventory.Unity
{
    // Optional per-monster override. Missing/empty tables intentionally produce no loot.
    public sealed class InkDropSource : MonoBehaviour
    {
        public string tableKey;
    }
}
