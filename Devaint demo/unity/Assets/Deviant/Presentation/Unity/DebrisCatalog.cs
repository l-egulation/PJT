using UnityEngine;

namespace Deviant.Presentation.Unity
{
    public sealed class DebrisCatalog : ScriptableObject
    {
        public DebrisBakeAsset[] bakes;
        public DebrisBakeAsset Find(Sprite sprite)
        {
            if(bakes!=null)foreach(var bake in bakes)if(bake && bake.source==sprite)return bake;
            return null;
        }
    }
}
