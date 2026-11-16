using UnityEngine;

namespace Deviant.Inventory.Unity
{
    public sealed class GroundInkPickup : MonoBehaviour
    {
        public InkInstance Item { get; private set; }
        public bool Ready { get; private set; }
        public bool Collected { get; private set; }
        public InventoryRuntimeHost Owner { get; private set; }
        public SpriteRenderer Visual { get; private set; }
        public bool ReportedFull;
        Vector2 landing;
        float spawnedAt, collectAfter;
        bool waitForExit;
        LootToss toss;
        public LootToss Toss => toss;
        public bool WaitingForPlayerToLeave => waitForExit;
        public void WaitForPlayerToLeave() => waitForExit = true;
        public void Initialize(InventoryRuntimeHost owner, InkInstance item, Vector2 at, bool settled, Vector2? source = null)
        {
            Owner = owner; Item = item; landing = at; spawnedAt = owner.Arena.Now;
            collectAfter = spawnedAt + (settled ? .45f : .6f); Ready = settled;
            transform.position = at;
            Visual = gameObject.AddComponent<SpriteRenderer>();
            Visual.sprite = owner.ItemSprite(item);
            Visual.color = owner.ItemTint(item); Visual.sortingOrder = 50;
            float scale = .5f * Visual.sprite.pixelsPerUnit / Visual.sprite.rect.height;
            Visual.transform.localScale = Vector3.one * scale;
            if (GroundLootArt.Apply(Visual, GroundLootArt.InkKey(item.Definition)))
            {
                var floor = owner.Arena.FindFloor(at + Vector2.up * .5f, 64);
                if (floor.HasValue) landing.y = floor.Value + Visual.sprite.bounds.extents.y;
                transform.position = landing;
            }
            if (owner.Arena.spriteMaterial) Visual.sharedMaterial = owner.Arena.spriteMaterial;
            // Thrown out of the monster: collectable only once it has come to rest.
            if (!settled && source.HasValue)
            {
                var settings = LootTossSettings.Current;
                toss = LootToss.Plan(owner.Arena, source.Value, landing, Visual.bounds.extents.y, LootToss.NextIndex(source.Value, spawnedAt), settings);
                landing = toss.Rest; collectAfter = spawnedAt + toss.Duration + settings.collectDelay;
                transform.position = LootToss.Snap(toss.Launch);
            }
        }
        public void Step(float now)
        {
            if (Collected) return;
            if (!Ready)
            {
                if (toss != null)
                {
                    transform.position = LootToss.Snap(toss.Evaluate(now - spawnedAt));
                    Ready = now - spawnedAt >= toss.Duration;
                }
                else
                {
                    float t = Mathf.Clamp01((now - spawnedAt) / .6f);
                    transform.position = landing + Vector2.up * (4 * 1.1f * t * (1 - t));
                    Ready = t >= 1;
                }
            }
            var player = Owner.Arena.Player;
            if (!Ready || !player || player.Dead) return;
            bool touching = new Bounds(player.Center, player.size).Intersects(new Bounds(transform.position, new Vector3(.6f, .6f, 1)));
            // A discarded ink re-arms once the player has stepped off it, independent of the collect delay.
            if (waitForExit) { if (!touching) waitForExit = false; return; }
            if (now >= collectAfter && touching) Owner.TryCollect(this);
        }
        internal void Claim()
        { Collected = true; gameObject.SetActive(false); Destroy(gameObject); }
    }
}
