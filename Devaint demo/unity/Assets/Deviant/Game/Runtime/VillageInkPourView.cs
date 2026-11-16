using System;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // Lives in the village, beside the stationary hero. The later reward screen owns no pouring animation.
    public sealed class VillageInkPourView : MonoBehaviour
    {
        public const float PourAt = 2.6f, Duration = 5.2f;
        public event Action<int> Poured;
        public bool Playing { get; private set; }
        public int Piece { get; private set; }
        public float Elapsed { get; private set; }
        public string Phase => !Playing ? "idle" : Elapsed < 1.3f ? "float" : Elapsed < 2.2f ? "uncap" : Elapsed < 3.8f ? "pour" : "colour";
        VillageInkAssets art;
        Transform bottle;
        SpriteRenderer body, cap;
        LineRenderer stream;
        Vector3 origin, destination, mouth;
        bool poured;
        static readonly Color32[] InkColors = { new Color32(226,73,74,255), new Color32(238,193,64,255), new Color32(88,177,103,255), new Color32(81,155,222,255), new Color32(168,106,202,255) };
        readonly Vector3[] points = new Vector3[2];
        public void Configure()
        {
            art = new VillageInkAssets();
            bottle = new GameObject("마을에서 떠오르는 작은 잉크 병").transform;
            bottle.SetParent(transform, false);
            body = Layer("잉크 병"); cap = Layer("분리된 병뚜껑");
            stream = new GameObject("우물로 흐르는 잉크").AddComponent<LineRenderer>();
            stream.transform.SetParent(transform, false); stream.useWorldSpace = true;
            stream.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            stream.sortingOrder = 19; stream.positionCount = points.Length;
            stream.startWidth = .07f; stream.endWidth = .035f;
            gameObject.SetActive(false);
        }
        SpriteRenderer Layer(string name)
        {
            var layer = new GameObject(name).AddComponent<SpriteRenderer>(); layer.transform.SetParent(bottle, false);
            layer.sortingOrder = 20; return layer;
        }
        public void Begin(int piece, Vector2 hero, Vector2 well)
        {
            Piece = Mathf.Clamp(piece, 1, 5); Elapsed = 0; Playing = true; poured = false;
            origin = hero + Vector2.up * 3.8f; destination = well + Vector2.up * 1.8f;
            string color = VillageInkAssets.Colors[Piece - 1];
            body.sprite = art.Get("acquisition/" + color + "-open"); cap.sprite = art.Get("acquisition/" + color + "-cap");
            var frame = art.GetFrame(Piece);
            float width = 1.8f;
            bottle.localScale = Vector3.one * (width / body.sprite.bounds.size.x);
            mouth = new Vector3((frame.pour[0] - .5f) * body.sprite.bounds.size.x, (.5f - frame.pour[1]) * body.sprite.bounds.size.y, 0);
            gameObject.SetActive(true); Render();
        }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t*t*(3-2*t); }
        public void Step(float dt)
        {
            if (!Playing) return;
            Elapsed += Mathf.Max(0, dt);
            if (!poured && Elapsed >= PourAt) { poured = true; Poured?.Invoke(Piece); }
            Render(); if (Elapsed >= Duration) Cancel();
        }
        void Render()
        {
            float t = Elapsed, lift = Ease((t - 1.3f) / .6f), tilt = Ease((t - 2) / .6f);
            bottle.rotation = Quaternion.Euler(0, 0, tilt * 62);
            // The neck settles over the well, then serves as the pivot while the bottle tips.
            var mouthOffset = bottle.rotation * Vector3.Scale(mouth, bottle.localScale);
            var overWell = destination + Vector3.up * 1.35f - mouthOffset;
            // Let the bottle materialise above the head before travelling towards the well.
            bottle.position = Vector3.Lerp(origin, overWell, Ease((t-.65f) / .65f));
            cap.transform.localPosition = Vector3.up * lift * 2.5f;
            float alpha = Ease(t / .55f) * (1 - Ease((t - 4.1f) / .75f));
            body.color = new Color(1, 1, 1, alpha); cap.color = new Color(1, 1, 1, alpha * (1 - Ease((t - 1.9f) / .5f)));
            body.sprite = art.Get("acquisition/" + VillageInkAssets.Colors[Piece - 1] + (t < 3.5f ? "-open" : "-empty"));
            float flow = Ease((t - 2.2f) / .4f) * (1 - Ease((t - 3.6f) / .6f));
            stream.enabled = flow > 0;
            var color = InkColors[Piece-1];
            stream.startColor = new Color(color.r/255f, color.g/255f, color.b/255f, flow);
            stream.endColor = new Color(color.r/255f, color.g/255f, color.b/255f, flow * .65f);
            points[0] = bottle.TransformPoint(mouth);
            points[1] = new Vector3(points[0].x, destination.y, points[0].z);
            stream.SetPositions(points);
        }
        public void Cancel() { Playing = false; gameObject.SetActive(false); }
        void OnDestroy() { art?.Dispose(); if (stream && stream.sharedMaterial) Destroy(stream.sharedMaterial); }
    }
}
