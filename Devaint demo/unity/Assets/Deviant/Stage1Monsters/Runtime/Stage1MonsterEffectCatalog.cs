using System;
using UnityEngine;

namespace Deviant.Stage1Monsters
{
    public sealed class Stage1MonsterEffectCatalog : ScriptableObject
    {
        public string sourceSHA256;
        public Stage1MonsterClip[] clips;
        public Stage1MonsterClip Find(string id)=>Array.Find(clips,c=>c.id==id);
    }
}
