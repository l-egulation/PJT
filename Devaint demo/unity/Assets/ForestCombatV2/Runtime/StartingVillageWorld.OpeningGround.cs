using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // The village and tutorial-road ground in the chapter-1 pixel lawn (2026-11-12). The drained state draws the same
    // 32 px connected lawn tiles as the combat stages (resources stage1-pixel-lawn-wood-32); colour draws their colour
    // version, the hollow chapter-2 style ground whose soil runs only along the open faces and falls to a dark inside
    // (stage1-background-color-20261112/runtime/village). Every tile is a paired piece, so regions, the palette blots and
    // the opening's erasure treat it like the rest of the village. Tile pictures are lifted 8 px: the grass blades stand
    // above the cell and each picture's top surface is its 8th row.
    public sealed partial class StartingVillageWorld
    {
        const string PixelVillage = "Hub/pixel-village/";
        const string GroundSketchAtlas = PixelVillage + "ground/sketch/terrain-atlas", GroundColourAtlas = PixelVillage + "ground/color/terrain-atlas";
        const int TilePixels = 32, TileLift = 8;
        const float GroundDepth = 10f;
        // Grounded course walls continue this far below the walking line, over the floor tiles they stand on.
        const int CourseWallFoot = 32;

        // Neighbour bits N 1, E 2, S 4, W 8, NE 16, SE 32, SW 64, NW 128; a corner counts only when both of its sides are
        // solid. The atlas holds the 47 resulting shapes in ascending order, four variants each (as Stage1CombatArt).
        static int[] tileShapes;
        static int CanonicalMask(int m)
        {
            int r = m & 15;
            for (int i = 0; i < 4; i++)
                if ((m & (1 << i)) != 0 && (m & (1 << ((i + 1) % 4))) != 0 && (m & (16 << i)) != 0) r |= 16 << i;
            return r;
        }
        static int TileIndex(int mask, int variant)
        {
            if (tileShapes == null)
            {
                var shapes = new SortedSet<int>();
                for (int m = 0; m < 256; m++) shapes.Add(CanonicalMask(m));
                tileShapes = new int[shapes.Count]; shapes.CopyTo(tileShapes);
            }
            return System.Array.IndexOf(tileShapes, CanonicalMask(mask)) * 4 + variant;
        }
        // The combat stages' variant choice, so the same cell looks the same wherever it is drawn.
        static int TileVariant(int x, int y)
        {
            uint mark = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u); mark ^= mark >> 13;
            return (int)(mark % 4);
        }

        Texture2D groundSketch, groundColour;
        Color32[] groundSketchPixels, groundColourPixels;
        void LoadGroundAtlases()
        {
            if (groundSketch) return;
            groundSketch = Resources.Load<Texture2D>(GroundSketchAtlas); groundColour = Resources.Load<Texture2D>(GroundColourAtlas);
            if (!groundSketch || !groundColour) throw new System.InvalidOperationException("Village pixel ground atlases are missing (run tools/assets/update_assets.py).");
            groundSketch.filterMode = groundColour.filterMode = FilterMode.Point;
        }

        // One tile picture, its pivot on the cell's top-left corner (the picture's top 8 px stand above the cell).
        readonly Dictionary<(Texture2D, int), Sprite> tileSprites = new Dictionary<(Texture2D, int), Sprite>();
        Sprite TileSprite(Texture2D atlas, int mask, int variant)
        {
            int k = TileIndex(mask, variant);
            if (tileSprites.TryGetValue((atlas, k), out var sprite)) return sprite;
            int columns = atlas.width / TilePixels;
            var rect = new Rect(k % columns * TilePixels, atlas.height - (k / columns + 1) * TilePixels, TilePixels, TilePixels);
            sprite = Sprite.Create(atlas, rect, new Vector2(0, 1 - TileLift / (float)TilePixels), 32, 0, SpriteMeshType.FullRect);
            sprite.name = atlas.name + " " + k;
            made.Add(sprite); return tileSprites[(atlas, k)] = sprite;
        }

        // Uses the same atlas cells as the world. The preview owns and releases the returned sprite.
        public static Sprite CreateTutorialGroundTile(int mask)
        {
            var atlas = Resources.Load<Texture2D>(GroundSketchAtlas);
            if (!atlas) throw new System.InvalidOperationException("Tutorial ground atlas is missing: " + GroundSketchAtlas);
            atlas.filterMode = FilterMode.Point;
            int k = TileIndex(mask, 0), columns = atlas.width / TilePixels;
            return Sprite.Create(atlas, new Rect(k % columns * TilePixels,
                atlas.height - (k / columns + 1) * TilePixels, TilePixels, TilePixels),
                new Vector2(0, 1), 32, 0, SpriteMeshType.FullRect);
        }

        // Ground cells from `left` to `right` (whole units), GroundDepth rows down from the walking line. openLeft/Right: the
        // run ends at a pit, so its end column shows its side face. interior: no top face (the far wall inside a pit).
        void GroundColumns(int left, int right, bool openLeft, bool openRight, int order, bool interior = false)
        {
            LoadGroundAtlases();
            int rows = Mathf.CeilToInt(GroundDepth);
            for (int x = left; x < right; x++)
            {
                string region = RegionOver(x, x + 1);
                for (int row = 0; row < rows; row++)
                {
                    int mask = 255;
                    if (row == 0 && !interior) mask &= ~1;
                    if (openLeft && x == left) mask &= ~8;
                    if (openRight && x == right - 1) mask &= ~2;
                    int variant = TileVariant(x, row);
                    var sketch = TileSprite(groundSketch, mask, variant); var colour = TileSprite(groundColour, mask, variant);
                    var r = Renderer("Ground body", sketch, order);
                    paired.Add(new Piece { renderer = r, region = region, colorSprite = colour, sketchSprite = sketch, rowHeights = new Vector2(TilePixels, TilePixels), row = row, column = x });
                }
            }
        }

        // Daisies on the grass and stones in the soil, sparse and fixed to the world (the combat stages' rules).
        void GroundDecor(int left, int right)
        {
            string colour = PixelVillage + "ground/color/decor/", sketch = PixelVillage + "ground/sketch/decor/";
            var centre = new Vector2(.5f, .5f);
            for (int x = left; x < right; x++)
            {
                string region = RegionOver(x, x + 1);
                if (((x * 29) % 17 + 17) % 17 == 3)
                    Textured("Ground", colour + "daisy", sketch + "daisy", centre, -4, region).transform.position = new Vector3(x + .5f, GroundTop + TileLift / 32f, 0);
                for (int row = 1; row < 4; row++)
                {
                    uint mark = unchecked((uint)x * 73856093u ^ (uint)row * 19349663u); mark ^= mark >> 13;
                    if (mark % 19 != 2) continue;
                    string name = "embedded-pebble-" + mark % 3;
                    Textured("Ground", colour + name, sketch + name, centre, -4, region).transform.position = new Vector3(x + .5f, GroundTop - row - TileLift / 32f, 0);
                }
            }
        }

        // A course wall as one picture in each state, cut from the lawn tiles: its own tile grid is anchored at its top and
        // left edge (the right column and, when it hangs, the bottom row are anchored at the far edge), so walls of any
        // size in pixels keep whole top, side and bottom faces. Below the walking line a grounded wall is inside the
        // ground: its sides are closed there and the foot covers the floor's grass it stands on.
        void PixelCourseWall(float worldX, float bottom, int width, int height)
        {
            LoadGroundAtlases();
            groundSketchPixels ??= groundSketch.GetPixels32(); groundColourPixels ??= groundColour.GetPixels32();
            bool hanging = bottom > 0;
            int foot = hanging ? 0 : CourseWallFoot, canvasHeight = TileLift + height + foot;
            int originX = Mathf.RoundToInt(worldX * 32), topY = Mathf.RoundToInt((bottom * 32 + height));
            var colour = new Color32[width * canvasHeight]; var sketch = new Color32[width * canvasHeight];
            int atlasWidth = groundSketch.width, atlasHeight = groundSketch.height, columns = atlasWidth / TilePixels;
            for (int cy = 0; cy < canvasHeight; cy++)
            {
                int ly = cy - TileLift;   // 0 = the wall's top surface
                bool fromBottom = hanging && ly + TileLift >= (height + TileLift) / 2;
                int iy = fromBottom ? TilePixels - 1 - (height - 1 - ly) % TilePixels : (ly + TileLift) % TilePixels;
                int cellY = fromBottom ? -1000 - (height - 1 - ly) / TilePixels : (ly + TileLift) / TilePixels;
                bool buried = !hanging && ly >= height;
                // Below the walking line the foot's tiles are anchored at the ground line, and its first row is the
                // lawn's inside corner (ground beside, wall above, open air in the corner between them): the wall's
                // side face turns into the floor's face and the inside falls to the ground's own dark (colour) or fill.
                int footRow = 0;
                if (buried)
                {
                    int fy = ly - height + TileLift;
                    footRow = fy / TilePixels; iy = fy % TilePixels; cellY = 2000 + footRow;
                }
                for (int lx = 0; lx < width; lx++)
                {
                    bool fromRight = lx >= width / 2;
                    int ix = fromRight ? TilePixels - 1 - (width - 1 - lx) % TilePixels : lx % TilePixels;
                    int cellX = (originX + lx - ix) / TilePixels;
                    int mask = 255;
                    if (ly + TileLift < TilePixels) mask &= ~1;
                    if (hanging && height - 1 - ly < TilePixels) mask &= ~4;
                    if (!buried && lx < TilePixels) mask &= ~8;
                    if (!buried && width - 1 - lx < TilePixels) mask &= ~2;
                    if (buried && footRow == 0 && lx < TilePixels) mask &= ~128;
                    if (buried && footRow == 0 && width - 1 - lx < TilePixels) mask &= ~16;
                    int k = TileIndex(mask, TileVariant(cellX, cellY + topY / TilePixels));
                    int at = (atlasHeight - 1 - (k / columns * TilePixels + iy)) * atlasWidth + k % columns * TilePixels + ix;
                    colour[cy * width + lx] = groundColourPixels[at]; sketch[cy * width + lx] = groundSketchPixels[at];
                }
            }
            string key = $"pixel wall {width}x{height}x{hanging}@{originX}:{topY}";
            AddCourseArt("Opening course wall", colour, width, canvasHeight, Vector2.zero, sketch,
                new Vector2(worldX, GroundTop + bottom - foot / 32f), key);
        }

        // A platform of lawn wood planks (left end, middles, right end), the plank top on the platform's surface.
        void WoodPlatform(string name, float x, float top, int width, int order, List<SpriteRenderer> into)
        {
            string colour = PixelVillage + "ground/color/wood/", sketch = PixelVillage + "ground/sketch/wood/";
            for (int i = 0; i < width; i++)
            {
                string piece = width == 1 ? "single" : i == 0 ? "left" : i == width - 1 ? "right" : i % 2 == 1 ? "middle-a" : "middle-b";
                var r = Textured(name, colour + piece, sketch + piece, new Vector2(0, 1 - TileLift / (float)TilePixels), order);
                r.transform.position = new Vector3(x + i, GroundTop + top, 0);
                into?.Add(r);
            }
        }
    }
}
