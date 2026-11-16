using System;
using UnityEngine;

namespace ForestCombatV2
{
    // References the team's original imported sprites; no copied or regenerated PNGs.
    [CreateAssetMenu(menuName="Deviant/Chapter 1 Team Terrain Palette")]
    public sealed class Chapter1TeamTerrainPalette : ScriptableObject
    {
        public Sprite[] terrain;
        public Sprite[] platforms;
        public void Validate()
        {
            if(terrain==null||terrain.Length!=15||platforms==null||platforms.Length!=5)
                throw new InvalidOperationException("The Stage1Village palette needs 15 terrain and 5 platform sprites.");
            foreach(var set in new[]{terrain,platforms})foreach(var sprite in set)
                if(!sprite||sprite.rect.width!=32||sprite.rect.height!=32||sprite.pixelsPerUnit!=32||
                    sprite.texture.filterMode!=FilterMode.Point||sprite.texture.mipmapCount!=1)
                    throw new InvalidOperationException("Restore the team's Stage1Village 32px sprites and original import settings.");
        }
    }
}
