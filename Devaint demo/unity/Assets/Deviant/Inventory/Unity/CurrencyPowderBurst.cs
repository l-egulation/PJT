using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace Deviant.Inventory.Unity
{
    // A view of currency already paid by the run. No target-following or inventory transactions live here.
    public sealed class CurrencyPowderBurst : MonoBehaviour
    {
        public const float PopSeconds = .20f, RestSeconds = .12f, DustSeconds = .95f;
        const float Stagger = .04f, PixelsPerUnit = 32;
        const float Gravity = 10;
        static Sprite pixel;
        sealed class Piece { public SpriteRenderer image; public Vector2 offset; public bool burst; }
        sealed class Grain
        {
            public SpriteRenderer image; public Vector2 position, velocity; public Color color;
            public float born, life, simulatedAge, restitution, radius; public bool grounded;
        }
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Grain> grains = new List<Grain>();
        readonly List<RaycastHit2D> contacts = new List<RaycastHit2D>(16);
        static ContactFilter2D SurfaceFilter => new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers };
        CombatArena arena;
        Vector2 origin;
        float bornAt, floor;
        bool palette;
        public bool HasBurst { get; private set; }
        public bool Complete { get; private set; }
        public int BurstNotifications { get; private set; }
        public int FloorBounces { get; private set; }
        public int ActiveGrains => grains.Count;
        public float Duration => PopSeconds + RestSeconds + (pieces.Count - 1) * Stagger + DustSeconds;
        public event Action Popped;

        public void Initialize(CombatArena owner, SpriteRenderer art, bool isPalette, int visiblePieces = 1)
        {
            arena = owner; palette = isPalette; origin = art.transform.position; bornAt = owner.Now;
            floor = origin.y - art.bounds.extents.y;
            int count = Mathf.Clamp(visiblePieces, 1, 3);
            for (int i = 0; i < count; i++)
            {
                var image = art;
                if (i > 0)
                {
                    image = new GameObject("Currency visual " + i).AddComponent<SpriteRenderer>();
                    image.transform.SetParent(transform, false);
                    image.sprite = art.sprite; image.sharedMaterial = art.sharedMaterial;
                    image.sortingLayerID = art.sortingLayerID; image.sortingOrder = art.sortingOrder;
                }
                float spread = count == 1 ? 0 : (i - (count - 1) * .5f) * .36f;
                var clear = owner.DropPoint(origin, spread, .25f) - origin;
                pieces.Add(new Piece { image = image, offset = clear });
            }
            Step(owner.Now);
        }

        public void Step(float now)
        {
            if (Complete || !arena || arena.Controls.Current.SimulationPaused) return;
            float elapsed = Mathf.Max(0, now - bornAt);
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                float burstAt = PopSeconds + RestSeconds + i * Stagger;
                if (elapsed < burstAt)
                {
                    float t = Mathf.Clamp01(elapsed / PopSeconds);
                    Vector2 at = origin + piece.offset * t + Vector2.up * (4 * .42f * t * (1 - t));
                    piece.image.transform.position = Snap(at);
                }
                else if (!piece.burst)
                {
                    piece.burst = true;
                    piece.image.enabled = false;
                    Scatter(origin + piece.offset, bornAt + burstAt, i, piece.image);
                    if (!HasBurst) { HasBurst = true; BurstNotifications++; Popped?.Invoke(); }
                }
            }
            for (int i = grains.Count - 1; i >= 0; i--)
            {
                var grain = grains[i];
                float age = Mathf.Max(0, now - grain.born);
                if (age >= grain.life)
                { if (grain.image) Destroy(grain.image.gameObject); grains.RemoveAt(i); continue; }
                Advance(grain, age);
                grain.image.transform.position = Snap(grain.position);
                var color = grain.color;
                color.a = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, age / grain.life));
                grain.image.color = color;
            }
            Complete = elapsed >= Duration;
        }

        void Scatter(Vector2 at, float birth, int piece, SpriteRenderer source)
        {
            if (!pixel) pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, PixelsPerUnit);
            int count = 80;
            for (int i = 0; i < count; i++)
            {
                var image = new GameObject("Currency powder").AddComponent<SpriteRenderer>();
                image.transform.SetParent(transform, false);
                image.sprite = pixel; image.sharedMaterial = source.sharedMaterial;
                image.sortingLayerID = source.sortingLayerID; image.sortingOrder = source.sortingOrder + 1;
                // Integer texels stay crisp; the gold burst now uses more visible 2px and 3px grains.
                int size = palette ? (i % 4 == 0 ? 2 : 1) : (i % 5 == 0 ? 3 : 2);
                float radius = Mathf.Ceil(size * .5f) / PixelsPerUnit;
                image.transform.localScale = Vector3.one * (size / transform.lossyScale.x);
                float angle = (-10 + ((i * 7 + piece * 3) % count) * 200f / count) * Mathf.Deg2Rad;
                float speed = 1.4f + (i % 7) * .32f;
                Vector2 center = at + new Vector2((i % 3 - 1) * .125f, (i % 2) * .0625f);
                center.y = Mathf.Max(floor + radius, center.y);
                Color color = palette ? (i % 5) switch
                {
                    0 => new Color32(231, 102, 70, 255), 1 => new Color32(69, 161, 193, 255),
                    2 => new Color32(99, 164, 83, 255), 3 => new Color32(248, 201, 74, 255),
                    _ => new Color32(255, 231, 155, 255),
                } : (i % 4) switch
                {
                    0 => new Color32(255, 216, 65, 255), 1 => new Color32(255, 238, 87, 255),
                    2 => new Color32(255, 249, 172, 255), _ => new Color32(255, 255, 224, 255),
                };
                image.color = color;
                grains.Add(new Grain { image = image, position = center, velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    color = color, born = birth, life = DustSeconds * (.78f + (i % 5) * .055f), restitution = .28f + (i % 4) * .06f, radius = radius });
            }
        }

        void Advance(Grain grain, float age)
        {
            // Fixed small steps make the bounce independent of render FPS and large manual test steps.
            while (grain.simulatedAge < age - .00001f)
            {
                float dt = Mathf.Min(1f / 60, age - grain.simulatedAge);
                bool wasGrounded = grain.grounded;
                if (wasGrounded) grain.velocity.x = Mathf.MoveTowards(grain.velocity.x, 0, 6 * dt);
                grain.velocity.y -= Gravity * dt;
                float dx = grain.velocity.x * dt;
                float travel = Mathf.Abs(dx);
                if (travel > .00001f)
                {
                    contacts.Clear();
                    Physics2D.defaultPhysicsScene.BoxCast(grain.position, new Vector2(grain.radius * 2, grain.radius * 2 - .01f), 0,
                        Vector2.right * Mathf.Sign(dx), travel + .002f, SurfaceFilter, contacts);
                    foreach (var hit in contacts)
                    {
                        var surface = hit.collider ? hit.collider.GetComponent<ArenaSurface>() : null;
                        if (!surface || surface.oneWay || hit.normal.x * Mathf.Sign(dx) > -.5f) continue;
                        travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - .002f));
                    }
                    grain.position.x += Mathf.Sign(dx) * travel;
                    if (travel + .001f < Mathf.Abs(dx)) grain.velocity.x *= -.35f;
                }
                float nextY = grain.position.y + grain.velocity.y * dt;
                grain.grounded = false;
                if (grain.velocity.y <= 0)
                {
                    contacts.Clear();
                    Physics2D.defaultPhysicsScene.Raycast(grain.position + Vector2.up * (grain.radius + .005f), Vector2.down,
                        grain.position.y - nextY + grain.radius * 2 + .01f, SurfaceFilter, contacts);
                    float? ground = null;
                    foreach (var hit in contacts)
                    {
                        if (!hit.collider || !hit.collider.GetComponent<ArenaSurface>() || hit.normal.y < .8f) continue;
                        if (nextY - grain.radius > hit.point.y + .001f || grain.position.y - grain.radius < hit.point.y - .005f) continue;
                        if (!ground.HasValue || hit.point.y > ground.Value) ground = hit.point.y;
                    }
                    if (ground.HasValue)
                    {
                        nextY = ground.Value + grain.radius;
                        if (wasGrounded) grain.velocity.y = 0;
                        else
                        {
                            FloorBounces++;
                            grain.velocity.y *= -grain.restitution;
                            grain.velocity.x *= .68f;
                        }
                        grain.grounded = grain.velocity.y < .65f;
                        if (grain.grounded) grain.velocity.y = 0;
                    }
                }
                grain.position.y = nextY;
                grain.simulatedAge += dt;
            }
        }

        static Vector2 Snap(Vector2 at) => new Vector2(Mathf.Round(at.x * PixelsPerUnit), Mathf.Round(at.y * PixelsPerUnit)) / PixelsPerUnit;
        void Update() { if (arena) Step(arena.Now); }
    }
}
