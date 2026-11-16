using UnityEngine;
using UnityEngine.Tilemaps;

namespace RuralNature32
{
    // Input Tilemaps hold editable collision cells. Visuals is an automatically
    // composed 32px Tilemap; its grass/roots can extend outside collision cells.
    [DisallowMultipleComponent]
    public sealed class RuralNatureTerrain : MonoBehaviour
    {
        public Tilemap ground;
        public Tilemap platforms;
        public Tilemap visuals;
        public bool autoConnect = true;
        public enum PlatformLook { Original, CleanSketch, UnfinishedSketch, PaintedEnds, Mixed, ColoredFoliage, PartialSketch }
        public PlatformLook platformStyle;
        public enum GroundLook { Original, CleanSketch, UnfinishedSketch, PartialSketch }
        public GroundLook groundStyle;
        [System.Serializable] public class SketchRegion
        {
            public string name;
            public bool platform;
            public float x,y,radiusX=90,radiusY=55;
        }
        [Tooltip("Native pixel coordinates, Y down. Sketch is local to these editable regions.")]
        public SketchRegion[] sketchRegions = new SketchRegion[0];
        [HideInInspector] public int bakedTileCount;
        [HideInInspector] public string lastBakeHash;
    }
}
