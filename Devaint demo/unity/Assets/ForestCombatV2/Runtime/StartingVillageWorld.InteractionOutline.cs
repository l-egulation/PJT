using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class StartingVillageWorld
    {
        const int OutlineSamples = 16;
        const float OutlinePixels = 2f;
        SpriteRenderer[] interactionOutline;
        Material interactionOutlineMaterial;
        public Station HighlightedStation { get; private set; }

        void RefreshInteractionOutline(Station near)
        {
            HighlightedStation = near;
            if (near == null || !near.Art || !near.Art.sprite)
            {
                if (interactionOutline != null) foreach (var edge in interactionOutline) edge.enabled = false;
                return;
            }
            if (interactionOutline == null)
            {
                interactionOutlineMaterial = new Material(Resources.Load<Shader>("Hub/InteractionSilhouette"));
                interactionOutline = new SpriteRenderer[OutlineSamples];
                for (int i = 0; i < OutlineSamples; i++)
                {
                    var node = new GameObject("Interaction outline " + i);
                    node.transform.SetParent(transform, false);
                    var edge = node.AddComponent<SpriteRenderer>();
                    edge.sharedMaterial = interactionOutlineMaterial;
                    interactionOutline[i] = edge;
                }
            }
            // One pooled silhouette ring behind the nearest usable prop: two native display pixels.
            // Translating full sprites avoids cropping the border at a texture/mesh edge.
            var source = near.Art;
            for (int i = 0; i < OutlineSamples; i++)
            {
                var edge = interactionOutline[i];
                float angle = i * Mathf.PI * 2 / OutlineSamples;
                edge.sprite = source.sprite;
                edge.sortingLayerID = source.sortingLayerID;
                edge.sortingOrder = source.sortingOrder - 1;
                edge.flipX = source.flipX; edge.flipY = source.flipY;
                edge.transform.rotation = source.transform.rotation;
                edge.transform.localScale = source.transform.lossyScale;
                edge.transform.position = source.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (OutlinePixels / 32f);
                edge.enabled = source.enabled;
            }
        }
    }
}
