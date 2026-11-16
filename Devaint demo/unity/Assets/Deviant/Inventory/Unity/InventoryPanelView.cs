using UnityEngine;

namespace Deviant.Inventory.Unity
{
    // A final GUI can replace the temporary view prefab while keeping the same session/commands.
    public abstract class InventoryPanelView : MonoBehaviour
    {
        public abstract void Bind(InventoryRuntimeHost host);
        public abstract void Refresh();
    }
}
