using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // One owner for the receiver's combined property block. No global light registry.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class WindowLightReceiver : MonoBehaviour
    {
        public const int MaxWindows = 4;
        readonly List<WindowLightView> windows = new List<WindowLightView>();
        MaterialPropertyBlock block;
        readonly WindowLightBuffer buffer = new WindowLightBuffer();
        SpriteRenderer target;
        public int WindowCount => windows.Count;
        internal void Register(WindowLightView window)
        {
            windows.RemoveAll(item => !item);
            if (windows.Contains(window)) return;
            if (windows.Count == MaxWindows) throw new InvalidOperationException("A receiver supports at most four directional windows; registration was rejected.");
            windows.Add(window);
        }
        internal void Unregister(WindowLightView window) { windows.Remove(window); SampleNow(); }
        public void SampleNow()
        {
            if (!target) target = GetComponent<SpriteRenderer>();
            if (!target) return;
            if (block == null) block = new MaterialPropertyBlock();
            buffer.Clear(); int slot = 0;
            if (isActiveAndEnabled) foreach (var window in windows) if (window) window.Write(buffer, slot++, target);
            target.GetPropertyBlock(block); buffer.Apply(block, slot); target.SetPropertyBlock(block);
        }
        void OnEnable() => SampleNow();
        void OnDisable() => SampleNow();
        void OnDestroy()
        {
            if (!target || block == null) return;
            buffer.Clear(); target.GetPropertyBlock(block); buffer.Apply(block, 0); target.SetPropertyBlock(block);
        }
    }

    // Fixed lengths matter: Unity fixes a property-array's capacity on first upload.
    internal sealed class WindowLightBuffer
    {
        const int Count = WindowLightReceiver.MaxWindows;
        internal readonly Matrix4x4[] WorldToLocal = new Matrix4x4[Count];
        internal readonly Vector4[] Planes = new Vector4[16 * Count], Edges = new Vector4[128 * Count], EdgeOwners = new Vector4[128 * Count];
        internal readonly Vector4[] OpeningPlanes = new Vector4[WindowAperture.MaxVertices * Count], Pieces = new Vector4[WindowAperture.MaxPieces * Count];
        internal readonly Vector4[] Direction = new Vector4[Count], ToLight = new Vector4[Count], Color = new Vector4[Count], Shadow = new Vector4[Count], Data = new Vector4[Count], Counts = new Vector4[Count];
        internal void Clear() { Array.Clear(Data, 0, Count); Array.Clear(Counts, 0, Count); }
        internal void Apply(MaterialPropertyBlock block, int count)
        {
            block.SetInteger("_WindowCount", count);
            block.SetMatrixArray("_WindowWorldToLocal", WorldToLocal); block.SetVectorArray("_WindowPlanes", Planes);
            block.SetVectorArray("_WindowEdges", Edges); block.SetVectorArray("_WindowEdgeOwners", EdgeOwners);
            block.SetVectorArray("_WindowOpeningPlanes", OpeningPlanes); block.SetVectorArray("_WindowPieces", Pieces);
            block.SetVectorArray("_WindowDirection", Direction); block.SetVectorArray("_WindowToLight", ToLight);
            block.SetVectorArray("_WindowColor", Color); block.SetVectorArray("_WindowShadow", Shadow);
            block.SetVectorArray("_WindowData", Data); block.SetVectorArray("_WindowCounts", Counts);
        }
    }
}
