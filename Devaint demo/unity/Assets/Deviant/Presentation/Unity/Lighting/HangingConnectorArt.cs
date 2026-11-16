using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    [Serializable] public sealed class ConnectorSpritePiece
    {
        public Sprite sprite;
        [Tooltip("Attachment in sprite-local Unity units, relative to its original pivot.")]
        public Vector2 topSocket;
        public Vector2 bottomSocket;
        public void Validate()
        {
            if (!sprite) throw new ArgumentException("A connector sprite is required.");
            if (!Finite(topSocket.x) || !Finite(topSocket.y) || !Finite(bottomSocket.x) || !Finite(bottomSocket.y) || (topSocket - bottomSocket).sqrMagnitude < 1e-8f)
                throw new ArgumentException("Distinct finite sprite-local connector sockets are required.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    [CreateAssetMenu(menuName = "deviant/Lighting/Hanging Connector Art")]
    public sealed class HangingConnectorArt : ScriptableObject
    {
        public ConnectorSpritePiece chainFace, chainEdge, rope;
        public Material spriteMaterial;
        public bool HasChain => chainFace != null && chainFace.sprite && chainEdge != null && chainEdge.sprite;
        public bool HasRope => rope != null && rope.sprite;
        public void Validate()
        {
            bool face = chainFace != null && chainFace.sprite, edge = chainEdge != null && chainEdge.sprite;
            if (face != edge) throw new ArgumentException("Provide both face and edge sprites for a chain, or neither.");
            if ((face || HasRope) && !spriteMaterial) throw new ArgumentException("Sprite connector art needs an explicit material.");
            if (face) { chainFace.Validate(); chainEdge.Validate(); }
            if (HasRope) rope.Validate();
        }
    }
}
