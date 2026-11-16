using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    [Serializable] public sealed class WindowOpeningPiece
    {
        [Tooltip("Convex perimeter in window-anchor local units. Either winding is accepted. No repeated closing vertex.")]
        public Vector2[] vertices;
    }
    [CreateAssetMenu(menuName = "deviant/Lighting/Window Aperture Art")]
    public sealed class WindowApertureArt : ScriptableObject
    {
        public WindowOpeningPiece[] openings;
        public WindowAperture Capture()
        {
            if (openings == null) throw new ArgumentException("Explicit window openings are required.");
            var pieces = new WindowPoint[openings.Length][];
            for (int p = 0; p < pieces.Length; p++)
            {
                var vertices = openings[p]?.vertices;
                if (vertices == null) throw new ArgumentException("An opening perimeter is missing.");
                pieces[p] = new WindowPoint[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) pieces[p][i] = new WindowPoint(vertices[i].x, vertices[i].y);
            }
            return new WindowAperture(pieces);
        }
    }
}
