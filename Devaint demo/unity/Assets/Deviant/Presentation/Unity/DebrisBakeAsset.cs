using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    [Serializable] public sealed class DebrisPiece
    {
        public Vector2 center;
        public Vector2[] hull;
        public Mesh mesh, mirroredMesh;
        public float area;
    }
    public sealed class DebrisBakeAsset : ScriptableObject
    {
        public Sprite source;
        public string sourceHash;
        public string dependencyHash;
        public Rect sourceRect;
        public Vector2 sourcePivot;
        public float pixelsPerUnit;
        public int version=1,seed;
        public DebrisPiece[] pieces;
        public bool IsCurrentSource
        {
            get
            {
                if(!source||source.packed||source.rect!=sourceRect||source.pivot!=sourcePivot||source.pixelsPerUnit!=pixelsPerUnit)return false;
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(dependencyHash)&&UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(source)).ToString()!=dependencyHash)return false;
#endif
                return true;
            }
        }
    }
}
