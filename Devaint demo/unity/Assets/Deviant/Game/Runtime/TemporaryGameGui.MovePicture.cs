using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // One recorded move drawn on the GUI (the controls cards and the signpost's window): the hero in the body it is given,
    // with the hero's 1-pixel ink rim, on the stretch of the opening road it was recorded on (paper ground with stone seams
    // under an ink line, the road's walls as paper in ink frames), the dodge's shot and, on a wall, the stamina ring over
    // the head emptying as the hero's does. Three framings (user requests 2026-11-10):
    // Wrap   each play passes through: the hero runs in from the left edge, does the move in the middle of the picture,
    //        runs out at the right edge, and the next play runs in again (no walls, the ground only; a move that ends
    //        up high falls back to the ground first);
    // Follow keeps the native scale and anchors the hero's feet while the terrain scrolls behind (signpost wall moves);
    // Fixed  the whole move stays in view, still, as it plays again from its start (the dodge and opening wall cards).
    public sealed partial class TemporaryGameGui
    {
        sealed class TutorialTerrainLifetime : MonoBehaviour
        {
            Sprite top, soil;
            readonly List<Sprite> edges = new List<Sprite>();
            public void Own(Sprite topSprite, Sprite soilSprite) { top = topSprite; soil = soilSprite; }
            public void OwnEdge(Sprite sprite) => edges.Add(sprite);
            void OnDestroy()
            {
                if (Application.isPlaying) { if (top) Destroy(top); if (soil) Destroy(soil); }
                else { if (top) DestroyImmediate(top); if (soil) DestroyImmediate(soil); }
                foreach (var edge in edges)
                    if (edge) { if (Application.isPlaying) Destroy(edge); else DestroyImmediate(edge); }
            }
        }

        sealed class MovePicture
        {
            public enum Framing { Wrap, Follow, Fixed }
            // A wrapping card keeps half the height of a jump, so a double jump stays on the card.
            const float Hold = .3f, WrapMargin = 40, WrapSqueezeY = .5f;
            // The run before and after the move and the fall back to the ground, as the recordings run and fall.
            const float RunSpeed = 7.5f, FallGravity = 104;
            static readonly Color Earth = new Color32(0xe6, 0xdb, 0xc3, 255), Seam = new Color32(0xb9, 0xab, 0x8f, 255);
            static readonly Sprite[] lawn = new Sprite[4];
            static Sprite TutorialLawn(int variant)
            {
                if (lawn[variant]) return lawn[variant];
                var atlas = Resources.Load<Texture2D>("Stage1Combat/terrain-atlas"); if (!atlas) return null;
                var shapes = new SortedSet<int>();
                for (int m = 0; m < 256; m++)
                {
                    int k = m & 15;
                    for (int j = 0; j < 4; j++) if ((m & (1 << j)) != 0 && (m & (1 << ((j + 1) % 4))) != 0 && (m & (16 << j)) != 0) k |= 16 << j;
                    shapes.Add(k);
                }
                int index = new List<int>(shapes).IndexOf(110) * 4 + variant, cols = atlas.width / 32;
                return lawn[variant] = UnityEngine.Sprite.Create(atlas, new Rect(index % cols * 32, atlas.height - (index / cols + 1) * 32, 32, 32), Vector2.one / 2, 32, 0, SpriteMeshType.FullRect);
            }
            readonly TemporaryGameGui gui;
            readonly RectTransform area;
            readonly float width, height, ground, maxScale;
            readonly Image groundInk, groundFace, hero, shot, ring;
            readonly List<Image> seams = new List<Image>(), rim = new List<Image>();
            readonly List<(Rect world, Image ink, Image face)> blocks = new List<(Rect, Image, Image)>();
            string[] wallClip; float[] wallTime;
            Sprite shotArt;
            readonly Sprite tutorialTop, tutorialSoil;
            readonly List<Image> tutorialGround = new List<Image>();
            readonly List<(Rect world, Image image)> tutorialWalls = new List<(Rect, Image)>();
            readonly Dictionary<int, Sprite> tutorialEdges = new Dictionary<int, Sprite>();
            readonly TutorialTerrainLifetime terrainLifetime;
            Framing framing;
            float scale, clock;
            // Wrap: the move's middle (units), the stretch shown (uIn..uOut) and the parts of one pass (seconds).
            float passCentre, passIn, passOut, passLead, passMove, passFall, passEndY;
            int passFrom, passTo;
            Vector2 camera;
            public StartingVillageWorld.GhostTrack Track { get; private set; }
            public int Frame { get; private set; }
            public bool Holding { get; private set; }
            public float Scale => scale;
            public Sprite Sprite => hero.enabled ? hero.sprite : null;
            public bool RingShown => ring.enabled;

            // area: the picture's rect (clipped); ground: pixels from its bottom to the ground line; maxScale: pixels a unit.
            public MovePicture(TemporaryGameGui owner, RectTransform pictureArea, float groundPixels, float pixelsPerUnit, bool gameTiles = false)
            {
                gui = owner; area = pictureArea; width = area.sizeDelta.x; height = area.sizeDelta.y; ground = groundPixels; maxScale = pixelsPerUnit;
                if (!area.GetComponent<RectMask2D>()) area.gameObject.AddComponent<RectMask2D>();
                groundInk = gui.Plain(area, 0, 0, 1, 1, InkColour, "Ground ink");
                groundFace = gui.Plain(area, 0, 0, 1, 1, Earth, "Ground");
                for (int i = 0; i < 128; i++) seams.Add(gui.Plain(area, 0, 0, 1, 1, Color.white, "Lawn tile"));
                groundInk.enabled = false; groundFace.color = new Color32(252, 247, 238, 255);
                if (gameTiles)
                {
                    // N is open above grass. Interior soil connects on every side.
                    tutorialTop = StartingVillageWorld.CreateTutorialGroundTile(110);
                    tutorialSoil = StartingVillageWorld.CreateTutorialGroundTile(255);
                    groundInk.enabled = groundFace.enabled = false;
                    foreach (var seam in seams) seam.enabled = false;
                    for (int i = 0; i < Mathf.CeilToInt(width / 4) + 2; i++)
                    {
                        var tile = Image("Game ground tile"); tile.sprite = tutorialTop;
                        tutorialGround.Add(tile);
                    }
                    terrainLifetime = area.gameObject.AddComponent<TutorialTerrainLifetime>();
                    terrainLifetime.Own(tutorialTop, tutorialSoil);
                }

                gui.MakeRim(area, rim, 8);
                hero = Image("Hero"); shot = Image("Shot"); ring = Image("Stamina ring");
            }

            Image Image(string name) { var image = gui.Rect(name, area, 0, 0, 1, 1).gameObject.AddComponent<Image>(); image.raycastTarget = false; image.enabled = false; return image; }

            public void Show(StartingVillageWorld.GhostTrack track, string[] wallClips, float[] wallTimes, Framing how)
            {
                Track = track; wallClip = wallClips; wallTime = wallTimes; framing = how; clock = 0; shotArt = null;
                foreach (var block in blocks) { if (block.ink) Destroy(block.ink.gameObject); if (block.face) Destroy(block.face.gameObject); }
                blocks.Clear();
                foreach (var tile in tutorialWalls) if (tile.image) Destroy(tile.image.gameObject);
                tutorialWalls.Clear();
                float minX = float.MaxValue, maxX = float.MinValue, maxY = 0;
                for (int f = 0; f < track.x.Length; f++) { minX = Mathf.Min(minX, track.x[f]); maxX = Mathf.Max(maxX, track.x[f]); maxY = Mathf.Max(maxY, track.y[f]); }
                if (track.shotTo > track.shotFrom)
                {
                    float shotEnd = track.shotX + track.shotSpeed * (track.shotTo - track.shotFrom) / 60f;
                    minX = Mathf.Min(minX, Mathf.Min(track.shotX, shotEnd)); maxX = Mathf.Max(maxX, Mathf.Max(track.shotX, shotEnd));
                }
                scale = maxScale;
                if (how == Framing.Fixed)
                {
                    // The whole move with the hero's height and the stamina ring over it.
                    scale = Mathf.Max(4, Mathf.Floor(Mathf.Min(maxScale, (width - 8) / (maxX - minX + 2.4f), (height - ground - 6) / (maxY + 3.1f))));
                    camera = new Vector2((minX + maxX) / 2, 0);
                }
                else camera = new Vector2(track.x[0], 0);
                if (how == Framing.Wrap) PlanPass(track);
                if (how != Framing.Wrap)
                    foreach (var piece in gui.OpeningData?.course?.pieces ?? new OpeningCoursePiece[0])
                    {
                        if (piece.kind != "wall" && piece.kind != "platform" && piece.kind != "crate" && piece.kind != "fence") continue;
                        float w = Mathf.Max(.6f, piece.width);
                        if (piece.x > maxX + 10 || piece.x + w < minX - 10) continue;
                        blocks.Add((new Rect(piece.x, piece.bottom, w, piece.top - piece.bottom), gui.Plain(area, 0, 0, 1, 1, InkColour, "Block ink"), gui.Plain(area, 0, 0, 1, 1, Earth, "Block")));
                    }
                if (tutorialSoil)
                    foreach (var block in blocks)
                    {
                        block.ink.sprite = tutorialSoil;
                        block.ink.color = Color.white;
                        block.ink.type = UnityEngine.UI.Image.Type.Tiled;
                        block.ink.pixelsPerUnitMultiplier = 100 / scale;
                        block.face.enabled = false;
                        if (how == Framing.Follow)
                        {
                            block.ink.enabled = false;
                            AddWallTiles(block.world);
                        }
                    }
                foreach (var r in rim) r.transform.SetAsLastSibling();
                hero.transform.SetAsLastSibling(); shot.transform.SetAsLastSibling(); ring.transform.SetAsLastSibling();
            }

            void AddWallTiles(Rect wall)
            {
                int columns = Mathf.CeilToInt(wall.width), rows = Mathf.CeilToInt(wall.height);
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < columns; x++)
                    {
                        bool n = y + 1 < rows, e = x + 1 < columns, s = y > 0 || wall.yMin <= 0, w = x > 0;
                        int mask = (n ? 1 : 0) | (e ? 2 : 0) | (s ? 4 : 0) | (w ? 8 : 0)
                            | (n && e ? 16 : 0) | (s && e ? 32 : 0) | (s && w ? 64 : 0) | (n && w ? 128 : 0);
                        if (!tutorialEdges.TryGetValue(mask, out var sprite))
                        {
                            sprite = StartingVillageWorld.CreateTutorialGroundTile(mask);
                            tutorialEdges.Add(mask, sprite); terrainLifetime.OwnEdge(sprite);
                        }
                        var tile = Image("Game wall tile"); tile.sprite = sprite; tile.enabled = true;
                        tutorialWalls.Add((new Rect(wall.xMin + x, wall.yMin + y,
                            Mathf.Min(1, wall.width - x), Mathf.Min(1, wall.height - y)), tile));
                    }
            }

            static void Destroy(Object o) => Object.Destroy(o);

            // One pass of a wrapping card: the move centred, the recording cut to the picture's width (and margin), a run in
            // when it starts too far right, a fall when it ends above the ground.
            void PlanPass(StartingVillageWorld.GhostTrack track)
            {
                float fps = StartingVillageWorld.SharedGhosts.fps > 0 ? StartingVillageWorld.SharedGhosts.fps : 60;
                int n = track.x.Length, a0 = -1, a1 = -1;
                for (int f = 0; f < n; f++)
                    if (track.clip[f] != "run" || track.y[f] > .01f || !string.IsNullOrEmpty(track.tapped[f])) { if (a0 < 0) a0 = f; a1 = f; }
                passCentre = a0 < 0 ? (track.x[0] + track.x[n - 1]) / 2 : (track.x[a0] + track.x[a1]) / 2;
                float reach = (width / 2 + WrapMargin) / scale;
                passIn = passCentre - reach; passOut = passCentre + reach;
                passFrom = 0; while (passFrom < n - 1 && track.x[passFrom] < passIn) passFrom++;
                passTo = n - 1; while (passTo > passFrom && track.x[passTo] > passOut) passTo--;
                passLead = Mathf.Max(0, (track.x[passFrom] - passIn) / RunSpeed);
                passMove = (passTo - passFrom) / fps;
                passEndY = track.y[passTo];
                passFall = passEndY > .01f ? Mathf.Sqrt(2 * passEndY / FallGravity) : 0;
            }

            // World units to picture pixels (top-left origin, y down) about the view's centre.
            Vector2 Point(float x, float y) => new Vector2(Mathf.Round(width / 2 + (x - camera.x) * scale), -Mathf.Round(height - ground - (y - camera.y) * scale));

            void Place(Image image, float x0, float y0, float x1, float y1)
            {
                Vector2 a = Point(x0, y1), b = Point(x1, y0);
                image.rectTransform.anchoredPosition = a; image.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, b.x - a.x), Mathf.Max(0, a.y - b.y));
            }

            public void Tick(float dt, CharacterAnimationSet body, WallClimbVisuals climb, bool human, CombatPlayer player, float staminaSeconds)
            {
                var track = Track; if (track == null) return;
                float fps = StartingVillageWorld.SharedGhosts.fps > 0 ? StartingVillageWorld.SharedGhosts.fps : 60;
                int last = track.x.Length - 1; float length = Mathf.Max(1, last) / fps;
                float t;
                StartingVillageWorld.GhostPose pose;
                if (framing == Framing.Wrap)
                {
                    // In, the move, a fall if it ended high, out; then the next pass comes in from the left.
                    float endX = track.x[passTo], fallEndX = endX + RunSpeed * passFall;
                    float cycle = passLead + passMove + passFall + Mathf.Max(0, (passOut - fallEndX) / RunSpeed);
                    clock = Mathf.Repeat(clock + dt, Mathf.Max(.1f, cycle)); t = clock; Holding = false;
                    float u, y; string clip; float clipTime;
                    if (t < passLead) { Frame = passFrom; u = passIn + RunSpeed * t; y = 0; clip = "run"; clipTime = t; }
                    else if (t < passLead + passMove)
                    {
                        Frame = Mathf.Min(passTo, passFrom + Mathf.FloorToInt((t - passLead) * fps));
                        u = track.x[Frame]; y = track.y[Frame]; clip = track.clip[Frame]; clipTime = track.clipTime[Frame];
                    }
                    else if (t < passLead + passMove + passFall)
                    {
                        float tf = t - passLead - passMove; Frame = passTo;
                        u = endX + RunSpeed * tf; y = Mathf.Max(0, passEndY - FallGravity * tf * tf / 2); clip = "fall_loop"; clipTime = tf;
                    }
                    else { float tr = t - passLead - passMove - passFall; Frame = passTo; u = fallEndX + RunSpeed * tr; y = 0; clip = "run"; clipTime = tr; }
                    pose = new StartingVillageWorld.GhostPose { sprite = StartingVillageWorld.TrackFrame(body, clip, clipTime, false), x = u, y = y, scale = 1, flip = track.face[Frame] < 0 };
                }
                else
                {
                    clock = Mathf.Repeat(clock + dt, length + Hold); t = clock; Holding = t > length;
                    Frame = Mathf.Min(last, Mathf.FloorToInt(t * fps));
                    pose = StartingVillageWorld.PoseAt(body, climb, human, player ? player.offset.x + player.size.x / 2 : float.NaN,
                        track, wallClip, wallTime, Frame, Holding ? t - length : -1);
                }
                if (framing == Framing.Follow)
                {
                    // Follow the rendered pose, including wall-contact offsets. No easing or auto zoom:
                    // feet stay centred, 12px above the normal ground line, including at the replay seam.
                    camera = new Vector2(pose.x, pose.y - 12 / scale);
                }
                float halfW = width / 2 / scale;
                if (tutorialTop)
                {
                    int first = Mathf.FloorToInt(camera.x - halfW) - 1;
                    for (int i = 0; i < tutorialGround.Count; i++)
                    {
                        var tile = tutorialGround[i];
                        tile.enabled = i < Mathf.CeilToInt(width / scale) + 2;
                        if (!tile.enabled) continue;
                        tile.rectTransform.sizeDelta = new Vector2(scale, scale);
                        tile.rectTransform.anchoredPosition = Point(first + i, .25f);
                    }
                }
                else
                {
                // Ground: paper under an ink line, its stone seams every unit (fixed seams on a wrapping card).
                Place(groundFace, camera.x - halfW - 1, -6, camera.x + halfW + 1, 0);
                int firstSeam = Mathf.FloorToInt(camera.x - halfW) - 1;

                for (int i = 0; i < seams.Count; i++)
                {
                    var at = Point(firstSeam + i, 0); var image = seams[i];
                    image.enabled = at.x > -scale && at.x < width;
                    if (!image.enabled) continue;
                    image.sprite = TutorialLawn(i % 4); image.color = Color.white;
                    image.rectTransform.anchoredPosition = new Vector2(at.x, at.y + scale / 4);
                    image.rectTransform.sizeDelta = new Vector2(scale, scale);
                }
                }
                float line = 2 / scale;
                foreach (var block in blocks)
                {
                    var r = block.world;
                    Place(block.ink, r.xMin, Mathf.Max(r.yMin, -1), r.xMax, r.yMax);
                    Place(block.face, r.xMin + line, Mathf.Max(r.yMin + (r.yMin > 0 ? line : 0), -1), r.xMax - line, r.yMax - line);
                }
                foreach (var tile in tutorialWalls)
                {
                    var r = tile.world;
                    Place(tile.image, r.xMin, r.yMin + .25f, r.xMax, r.yMax + .25f);
                }
                Vector2 heroAt = Point(pose.x, framing == Framing.Wrap ? pose.y * WrapSqueezeY : pose.y);
                if (framing == Framing.Wrap) heroAt.x = Mathf.Round(width / 2 + (pose.x - passCentre) * scale);
                hero.enabled = pose.sprite;
                if (pose.sprite)
                {
                    var rt = hero.rectTransform; hero.sprite = pose.sprite;
                    rt.sizeDelta = pose.sprite.rect.size / pose.sprite.pixelsPerUnit * scale; rt.pivot = pose.sprite.pivot / pose.sprite.rect.size;
                    rt.localScale = new Vector3(pose.flip ? -pose.scale : pose.scale, pose.scale, 1);
                    rt.anchoredPosition = heroAt;
                }
                SyncRim(rim, hero, Mathf.Max(1, scale / (pose.sprite ? pose.sprite.pixelsPerUnit : 32)));
                // The dodge's shot, its tail behind.
                bool flying = track.shotTo > track.shotFrom && !Holding && Frame >= track.shotFrom && Frame <= track.shotTo && !string.IsNullOrEmpty(track.shotArt);
                if (flying && shotArt == null) shotArt = StartingVillageWorld.ShotSprite(track.shotArt);
                var shotSprite = flying ? shotArt : null;
                shot.enabled = shotSprite;
                if (shotSprite)
                {
                    var rt = shot.rectTransform; shot.sprite = shotSprite;
                    rt.sizeDelta = shotSprite.rect.size / shotSprite.pixelsPerUnit * scale * track.shotScale; rt.pivot = new Vector2(.5f, .5f);
                    rt.anchoredPosition = Point(track.shotX + track.shotSpeed * (Frame - track.shotFrom) / fps, track.shotY);
                    shot.color = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Min((Frame - track.shotFrom + 1) / 6f, (track.shotTo - Frame + 1) / 10f)));
                }
                // On the wall: the stamina ring over the head (at least 13 px, the in-game size).
                bool onWall = staminaSeconds > 0 && wallClip?[Frame] == StartingVillageWorld.WallContactClip;
                ring.enabled = onWall;
                if (onWall)
                {
                    float top = player ? player.offset.y + player.size.y / 2 : 2;
                    ring.sprite = StaminaRing.For(1 - wallTime[Frame] / staminaSeconds);
                    float size = Mathf.Max(StaminaRing.Size, StaminaRing.Size / 32f * scale);
                    ring.rectTransform.sizeDelta = new Vector2(size, size); ring.rectTransform.pivot = new Vector2(.5f, .5f);
                    var at = Point(track.x[Frame], track.y[Frame] + top + WallStaminaRing.Above);
                    ring.rectTransform.anchoredPosition = new Vector2(at.x, Mathf.Min(at.y, -size / 2 - 2));
                }
            }
        }
    }
}
