using System;
using System.Collections.Generic;
using Deviant.Presentation.Unity;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // 무기가 내구도로 부서질 때(사용자 결정 2026-11-13, 시안 04_테스트/전투 HUD 도트 편집기 「파괴 연출 2차 HUD」·「3차 캐릭터」).
    // HUD: 실제 칸은 바로 맨손이 되고, 그 위에 부서지기 직전 무기 묶음의 복제(유령)를 얹는다. 유령의 무기 원(원판·링·무기 그림)만
    //      비스듬히 지워지며 아래 맨손이 드러나고(지운 자리 가장자리에서 가루가 떨어진다), 스킬 원·스킬 키·팔레트는 흐려진다.
    //      층 순서는 묶음 그대로라 지워지는 원은 키캡·팔레트 판 뒤에 있다. 내구도 원호는 연출에 넣지 않는다(유령에 그대로 있다가 함께 사라진다).
    // 캐릭터: 손 자리(발 가운데에서 앞 19, 위 23 게임 화소)에서 WeaponBreakBurst. 가방에 넣어 둔 무기가 부서질 때는 둘 다 없다.
    public sealed partial class TemporaryGameGui
    {
        const float BreakFrameSeconds = .06f; const int BreakWipeFrames = 12, BreakTotalFrames = BreakWipeFrames + 11;
        const int BreakBox = 96, BreakCrumbHeight = 140;
        static readonly Vector2 HandOffset = new Vector2(19f / 32, 23f / 32);
        sealed class BreakGhost
        {
            public int Slot; public GameObject Root; public float Born; public float[] Order; public RawImage Cover; public Texture2D CoverTexture; public Color32[] CoverBase;
            public RawImage Crumbs; public Texture2D CrumbTexture; public List<CanvasGroup> Fading = new List<CanvasGroup>();
            public List<Vector4> CrumbList = new List<Vector4>(); public List<Color32> CrumbColours = new List<Color32>(); public int Drawn = -1; public System.Random Random;
        }
        readonly List<BreakGhost> breakGhosts = new List<BreakGhost>();
        readonly SketchWeapon[] lastEquippedSketch = new SketchWeapon[2];
        SketchArsenal breakArsenal;
        public int ActiveBreakGhosts => breakGhosts.Count;

        SketchArsenal CurrentArsenal() { var binding = SketchBinding; return binding && binding.State != null ? binding.State.Arsenal : Sketch?.State.Arsenal; }

        // 매 갱신: 부서짐 알림을 받을 무기 창고를 잇고, 칸마다 장착 무기를 기억해 둔다(알림이 올 때 어느 칸인지 찾는다).
        void TrackWeaponBreaks()
        {
            var arsenal = CurrentArsenal();
            if (arsenal != breakArsenal)
            {
                if (breakArsenal != null) breakArsenal.WeaponDestroyed -= OnSketchWeaponDestroyed;
                breakArsenal = arsenal;
                if (breakArsenal != null) breakArsenal.WeaponDestroyed += OnSketchWeaponDestroyed;
            }
            for (int w = 0; w < 2; w++) lastEquippedSketch[w] = arsenal?.Equipped(w);
            UpdateBreakGhosts();
        }
        void DisposeWeaponBreaks() { if (breakArsenal != null) breakArsenal.WeaponDestroyed -= OnSketchWeaponDestroyed; breakArsenal = null; }

        void OnSketchWeaponDestroyed(SketchWeapon weapon, bool stored)
        {
            if (stored) return;
            int slot = Array.IndexOf(lastEquippedSketch, weapon);
            if (slot < 0) return;
            var player = host && host.Arena ? host.Arena.Player : null;
            if (player)
            {
                var hand = player.Position + new Vector2(HandOffset.x * player.facing, HandOffset.y);
                var visual = player.visual;
                WeaponBreakBurst.Play(hand, visual ? visual.sortingLayerID : 0, (visual ? visual.sortingOrder : 0) + 30);
            }
            StartBreakGhost(slot);
        }

        // 원본 묶음 안의 자식과 같은 자리의 복제 자식.
        static Transform Twin(Transform original, Transform root, Transform clone)
        {
            var path = new List<int>();
            for (var t = original; t && t != root; t = t.parent) path.Add(t.GetSiblingIndex());
            var at = clone;
            for (int i = path.Count - 1; i >= 0; i--) at = at.GetChild(path[i]);
            return at;
        }

        void StartBreakGhost(int w)
        {
            var cluster = combatWeaponRoots[w];
            if (!cluster || !cluster.gameObject.activeInHierarchy) return;
            foreach (var old in breakGhosts.FindAll(g => g.Slot == w)) { Destroy(old.Root); breakGhosts.Remove(old); }
            var root = Instantiate(cluster.gameObject, cluster.parent);
            root.name = "Weapon break ghost " + w; root.transform.SetSiblingIndex(cluster.GetSiblingIndex() + 1);
            var ghost = new BreakGhost { Slot = w, Root = root, Born = Time.unscaledTime, Random = new System.Random(4 + w) };
            Transform T(Component c) => Twin(c.transform, cluster, root.transform);
            // 지워지는 무기 원: 원판·링·무기 그림을 부서지는 순간 한 장(화면 2배 해상도)으로 굽고, 복제의 세 그림은 끈다.
            // 가림막(Mask)은 스텐실을 써서 캡처·일부 환경에서 통째로 사라졌다(2026-11-13 확인) → 구운 그림의 칸을 직접 지운다.
            var disc = T(weaponCards[w]); var ring = T(combatSelected[w]); var icon = T(weaponIcons[w]);
            var box = CanvasBox(weaponCards[w].rectTransform).center;
            var centre = new Vector2(Mathf.Round(box.x - BreakBox / 2f) + BreakBox / 2f, Mathf.Round(box.y - BreakBox / 2f) + BreakBox / 2f);
            ghost.CoverTexture = BakeCover(w, centre);
            ghost.CoverBase = ghost.CoverTexture.GetPixels32();
            var cover = Rect("Wipe cover", root.transform, 0, 0, 1, 1); cover.SetSiblingIndex(disc.GetSiblingIndex());
            ghost.Cover = cover.gameObject.AddComponent<RawImage>(); ghost.Cover.raycastTarget = false; ghost.Cover.texture = ghost.CoverTexture;
            SnapPixels(cover, centre, BreakBox, BreakBox);
            foreach (var part in new[] { disc, ring, icon }) part.gameObject.SetActive(false);
            // 지우는 순서: 왼쪽 위에서 오른쪽 아래로, 줄마다 물결과 칸마다 작은 흔들림(시안과 같은 값).
            ghost.Order = new float[BreakBox * BreakBox]; float c = BreakBox / 2f;
            for (int y = 0; y < BreakBox; y++) for (int x = 0; x < BreakBox; x++)
            {
                float u = ((x - (c - 42)) + (y - (c - 42)) * .55f) / (84 * 1.55f);
                ghost.Order[y * BreakBox + x] = Mathf.Clamp01(u + Mathf.Sin((y - c) / 5.5f) * .06f + ((float)ghost.Random.NextDouble() - .5f) * .1f);
            }
            // 가루: 지운 가장자리에서 생겨 아래로 떨어진다(원 아래쪽까지 그릴 수 있게 세로로 긴 판).
            var crumbs = Rect("Wipe crumbs", root.transform, 0, 0, 1, 1); crumbs.SetSiblingIndex(cover.GetSiblingIndex() + 1);
            ghost.Crumbs = crumbs.gameObject.AddComponent<RawImage>(); ghost.Crumbs.raycastTarget = false;
            ghost.CrumbTexture = new Texture2D(BreakBox, BreakCrumbHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            ghost.Crumbs.texture = ghost.CrumbTexture;
            SnapPixels(crumbs, centre + new Vector2(0, -(BreakCrumbHeight - BreakBox) / 2f), BreakBox, BreakCrumbHeight);
            // 흐려질 것: 스킬 원 묶음·스킬 키·팔레트.
            string g = "weapon-" + w;
            foreach (var name in new[] { g + " special", g + " special key", g + " palette" })
            {
                var part = root.transform.Find(name);
                if (part) ghost.Fading.Add(part.gameObject.AddComponent<CanvasGroup>());
            }
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            breakGhosts.Add(ghost);
            DrawBreakGhost(ghost, 0);
        }

        // 원판·링·무기 그림을 화면 2배 해상도 한 장으로 굽는다(centre 를 가운데로 BreakBox 게임 화소 정사각형).
        Texture2D BakeCover(int w, Vector2 centre)
        {
            int size = BreakBox * 2;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active; RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix(); GL.LoadPixelMatrix(0, size, size, 0);
            float left = centre.x - BreakBox / 2f, top = centre.y + BreakBox / 2f;
            foreach (var image in new[] { weaponCards[w], combatSelected[w], weaponIcons[w] })
            {
                if (!image || !image.enabled || !image.sprite || !image.gameObject.activeInHierarchy) continue;
                var at = CanvasBox(image.rectTransform); var sprite = image.sprite; var area = sprite.textureRect; var texture = sprite.texture;
                if (image.preserveAspect)
                {
                    float k = Mathf.Min(at.width / area.width, at.height / area.height), cx = at.center.x, cy = at.center.y;
                    at = new UnityEngine.Rect(cx - area.width * k / 2, cy - area.height * k / 2, area.width * k, area.height * k);
                }
                var screen = new UnityEngine.Rect((at.xMin - left) * 2, (top - at.yMax) * 2, at.width * 2, at.height * 2);
                var uv = new UnityEngine.Rect(area.x / texture.width, area.y / texture.height, area.width / texture.width, area.height / texture.height);
                Graphics.DrawTexture(screen, texture, uv, 0, 0, 0, 0, new Color(.5f, .5f, .5f, .5f));
            }
            GL.PopMatrix();
            var baked = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Weapon break cover", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            baked.ReadPixels(new UnityEngine.Rect(0, 0, size, size), 0, 0); baked.Apply(false);
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            return baked;
        }

        void UpdateBreakGhosts()
        {
            for (int i = breakGhosts.Count - 1; i >= 0; i--)
            {
                var ghost = breakGhosts[i];
                int frame = Mathf.FloorToInt((Time.unscaledTime - ghost.Born) / BreakFrameSeconds);
                if (frame >= BreakTotalFrames || !ghost.Root)
                {
                    if (ghost.Root) Destroy(ghost.Root);
                    if (ghost.CoverTexture) Destroy(ghost.CoverTexture);
                    if (ghost.CrumbTexture) Destroy(ghost.CrumbTexture);
                    breakGhosts.RemoveAt(i); continue;
                }
                if (frame != ghost.Drawn) DrawBreakGhost(ghost, frame);
            }
        }

        void DrawBreakGhost(BreakGhost ghost, int frame)
        {
            ghost.Drawn = frame;
            float p = (frame + 1f) / BreakWipeFrames * 1.12f, c = BreakBox / 2f;
            int size = BreakBox * 2; var pixels = (Color32[])ghost.CoverBase.Clone();
            for (int y = 0; y < BreakBox; y++) for (int x = 0; x < BreakBox; x++)
            {
                int i = y * BreakBox + x;
                // 지운 칸(게임 화소 = 구운 그림 2×2)은 비운다. 구운 그림은 아래에서 위로 쌓여 있다.
                if (ghost.Order[i] < p)
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) pixels[(size - 1 - (y * 2 + dy)) * size + x * 2 + dx] = HudPixels.Clear;
                if (frame < BreakWipeFrames && Mathf.Abs(ghost.Order[i] - p) < .03f && (x - c) * (x - c) + (y - c) * (y - c) <= 40 * 40 && ghost.Random.NextDouble() < .06)
                {
                    ghost.CrumbList.Add(new Vector4(x, y, (float)ghost.Random.NextDouble() * 1.2f - .6f, (float)ghost.Random.NextDouble() * .7f - .4f));
                    int k = ghost.Random.Next(3); ghost.CrumbColours.Add(k == 0 ? new Color32(214, 206, 192, 255) : k == 1 ? new Color32(170, 162, 150, 255) : HudPixels.Cream);
                }
            }
            ghost.CoverTexture.SetPixels32(pixels); ghost.CoverTexture.Apply(false);
            var crumbs = new Color32[BreakBox * BreakCrumbHeight];
            for (int k = ghost.CrumbList.Count - 1; k >= 0; k--)
            {
                var q = ghost.CrumbList[k]; q.x += q.z; q.y += q.w; q.w += .35f; ghost.CrumbList[k] = q;
                if (q.y >= BreakCrumbHeight) { ghost.CrumbList.RemoveAt(k); ghost.CrumbColours.RemoveAt(k); continue; }
                int x = Mathf.FloorToInt(q.x), y = Mathf.FloorToInt(q.y);
                if (x >= 0 && x < BreakBox && y >= 0) crumbs[(BreakCrumbHeight - 1 - y) * BreakBox + x] = ghost.CrumbColours[k];
            }
            ghost.CrumbTexture.SetPixels32(crumbs); ghost.CrumbTexture.Apply(false);
            float dead = Mathf.Clamp01((frame - 4) / 8f);
            foreach (var group in ghost.Fading) if (group) group.alpha = 1 - dead;
        }
    }
}
