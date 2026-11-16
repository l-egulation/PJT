using UnityEngine;
using Deviant.Presentation.Unity;

namespace Deviant.Chapter2
{
    // Captured from the pinned resource recipe by the Editor importer.
    public sealed class Chapter2LightingArt : ScriptableObject
    {
        public Sprite housing, mount, flame, torch, contactGlint;
        public Vector2 housingTop, housingFlame, mountChain, torchFlame;
        public HangingConnectorArt chain;
        public Rect windowOpening;
        public Vector3[] grilleVertices;
        public int[] grilleEdges;
    }
}
