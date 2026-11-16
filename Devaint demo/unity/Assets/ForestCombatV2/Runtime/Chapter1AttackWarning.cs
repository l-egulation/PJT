using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    [DefaultExecutionOrder(700)]
    public sealed class Chapter1AttackWarning : MonoBehaviour
    {
        public FieldMouseEnemy actor;
        SpriteRenderer fill, top, bottom, left, right;

        SpriteRenderer MakePart(string label, Color color)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = actor.arena.TechnicalSprite;
            renderer.sharedMaterial = actor.visual.sharedMaterial;
            renderer.sortingLayerID = actor.visual.sortingLayerID;
            renderer.sortingOrder = actor.visual.sortingOrder + 1;
            renderer.color = color;
            renderer.enabled = false;
            return renderer;
        }

        void EnsureParts()
        {
            if (fill || !actor || !actor.visual || !actor.arena) return;
            fill = MakePart("Attack warning fill", new Color(1f, .29f, .08f, .12f));
            top = MakePart("Attack warning top", Color.clear);
            bottom = MakePart("Attack warning bottom", Color.clear);
            left = MakePart("Attack warning left", Color.clear);
            right = MakePart("Attack warning right", Color.clear);
        }

        static void Place(SpriteRenderer renderer, Vector2 center, Vector2 size, Color color)
        {
            renderer.transform.position = new Vector3(center.x, center.y, 0);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1);
            renderer.color = color;
            renderer.enabled = true;
        }

        public bool Visible => fill && fill.enabled;
        public void Sample()
        {
            if (!isActiveAndEnabled || !actor || !actor.arena || actor.Dead || !actor.aggressive || !actor.Brain.Executing ||
                actor.Brain.State != FieldMouseState.Prepare)
            { Clear(); return; }
            EnsureParts();
            if (!fill) return;
            float progress = Mathf.Clamp01(actor.SpeciesAttackAge / actor.Definition.prepare);
            var bounds = actor.AttackBounds;
            if (actor.Definition.chargeDistance > 0)
            {
                // The charge sweeps the contact box along its authored travel distance.
                bounds.Encapsulate(bounds.center + Vector3.right * (actor.facing * actor.Definition.chargeDistance));
            }
            var center = new Vector2(bounds.center.x, bounds.center.y);
            var size = new Vector2(bounds.size.x, bounds.size.y);
            var color = new Color(1f, Mathf.Lerp(.65f, .15f, progress), .08f, .7f + .25f * progress);
            const float edge = .075f;
            Place(fill, center, size, new Color(color.r, color.g, color.b, .08f + .1f * progress));
            Place(top, center + Vector2.up * (size.y - edge) / 2, new Vector2(size.x, edge), color);
            Place(bottom, center - Vector2.up * (size.y - edge) / 2, new Vector2(size.x, edge), color);
            Place(left, center - Vector2.right * (size.x - edge) / 2, new Vector2(edge, size.y), color);
            Place(right, center + Vector2.right * (size.x - edge) / 2, new Vector2(edge, size.y), color);
        }

        public void Clear()
        {
            if (fill) fill.enabled = false;
            if (top) top.enabled = false;
            if (bottom) bottom.enabled = false;
            if (left) left.enabled = false;
            if (right) right.enabled = false;
        }

        void LateUpdate() => Sample();
        void OnDisable() => Clear();
    }
}
