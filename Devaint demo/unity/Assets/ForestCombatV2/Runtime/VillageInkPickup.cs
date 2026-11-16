using System;
using Deviant.Combat;
using Deviant.Control.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    // The first red village ink is a story item, separate from weapon ink parts and the boss altar.
    // Picking it up keeps the existing chapter-exit settlement as the permanent progression boundary.
    public class VillageInkPickup : MonoBehaviour
    {
        public const string DiscoveryStory = "village-red-ink-discovery";
        const float Reach = 1.8f, LandSeconds = .7f;
        CombatArena arena;
        Func<bool> ready, bossDead, required;
        Func<Vector2> bossPosition;
        Func<float> exitX;
        int piece;
        SpriteRenderer body;
        Sprite sprite;
        Vector2 origin;
        float droppedAt;
        int closedFrame = -1;
        public bool Spawned { get; private set; }
        public bool Collected { get; private set; }
        public bool Required => required != null && required();
        public bool BlocksExit => Required && (!Collected || Time.frameCount == closedFrame);
        public Vector2 Position { get; private set; }
        public KeycapMarker Key { get; private set; }
        public SpriteRenderer Body => body;
        public bool Landed => Spawned && Time.unscaledTime - droppedAt >= LandSeconds;
        public bool PlayerNear => arena && arena.Player && !arena.Player.Dead &&
            Vector2.Distance(arena.Player.Position, Position) <= Reach;

        public void Initialize(CombatArena owner, Func<bool> isReady, Func<bool> dead, Func<Vector2> bossAt, Func<float> exitAt, Func<bool> needed, int inkPiece)
        { arena=owner; ready=isReady; bossDead=dead; bossPosition=bossAt; exitX=exitAt; required=needed; piece=inkPiece; }

        protected virtual void Update()
        {
            if (!arena || !ready()) return;
            if (!Required) { if (body) body.enabled = false; if (Key) Key.SetVisible(false); return; }
            if (!Spawned && bossDead()) Spawn();
            if (!Spawned || Collected) return;
            float t = Mathf.Clamp01((Time.unscaledTime - droppedAt) / LandSeconds);
            var at = Vector2.Lerp(origin, Position, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * .9f);
            if (Landed) at += Vector2.up * (.12f + .06f * Mathf.Sin(Time.unscaledTime * 3));
            body.transform.position = at;
            bool available = Landed && PlayerNear && !arena.Controls.Current.SimulationPaused && !arena.Controls.Current.Blocks(InputChannels.Interaction);
            Key.MoveTo(new Vector2(at.x, at.y + 1.45f)); Key.SetVisible(available);
            if (available) PlayerControls.TryPerform(arena.Player, Deviant.Input.Contracts.GameAction.Interact, TryCollect);
        }

        void Spawn()
        {
            var boss = bossPosition();
            float x = Mathf.Min(boss.x + .8f, exitX() - 6);
            var floor = arena.FindFloor(new Vector2(x, boss.y + 2), 64);
            Position = new Vector2(x, floor ?? boss.y);
            origin = boss + Vector2.up * 1.8f; droppedAt = Time.unscaledTime;
            sprite = LoadDrop(piece);
            var go = new GameObject("Village ink drop " + piece); go.transform.SetParent(transform, false);
            body = go.AddComponent<SpriteRenderer>(); body.sprite = sprite; body.sortingOrder = 8;
            if (arena.spriteMaterial) body.sharedMaterial = arena.spriteMaterial;
            body.transform.position = origin;
            Key = KeycapMarker.Create(transform, 'F', Position + Vector2.up * 1.5f, arena.spriteMaterial, 9, PlayerControls.For(arena.Player));
            Key.SetVisible(false); Spawned = true;
        }

        [Serializable] sealed class Bindings { public Image[] images; }
        [Serializable] sealed class Image { public string id, resource; public int width, height; public int[] bounds; }
        static Sprite LoadDrop(int piece)
        {
            var data = JsonUtility.FromJson<Bindings>(Resources.Load<TextAsset>("VillageInk/Bindings").text);
            string id = "drops/" + new[] { "red", "yellow", "green", "blue", "purple" }[piece-1];
            var entry = Array.Find(data.images, e => e.id == id);
            var texture = Resources.Load<Texture2D>(entry.resource);
            float sx = texture.width / (float)entry.width, sy = texture.height / (float)entry.height;
            var rect = new Rect(entry.bounds[0] * sx, (entry.height - entry.bounds[3]) * sy,
                (entry.bounds[2] - entry.bounds[0]) * sx, (entry.bounds[3] - entry.bounds[1]) * sy);
            var result = Sprite.Create(texture, rect, new Vector2(.5f, 0), rect.height / 1.125f, 0, SpriteMeshType.FullRect);
            result.name = "VillageInk/" + id; result.hideFlags = HideFlags.DontSave; return result;
        }

        public bool TryCollect()
        {
            if (!Required || !Spawned || !Landed || Collected || !PlayerNear || arena.Controls.Current.SimulationPaused || arena.Controls.Current.Blocks(InputChannels.Interaction)) return false;
            Collected = true; body.enabled = false; Key.SetVisible(false); closedFrame = Time.frameCount;
            PresentationSfx.Play("story-ink-awaken");
            VillageInkPocketGlow.Carry(arena.Player, piece);
            string story = piece == 1 ? DiscoveryStory : "village-ink-collected-" + piece;
            if (!Chapter1ProfileHost.Service.Snapshot.HasSeen(story) && !Chapter1ProfileHost.Service.MarkStorySeen(story))
                Debug.LogWarning("Village ink collection was not saved: " + Chapter1ProfileHost.Service.LastSaveError);
            return true;
        }
        protected virtual void OnDestroy() { if (sprite) Destroy(sprite); }
    }
}
