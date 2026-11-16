using UnityEngine;
using Deviant.UI.Theme;

namespace Deviant.Game
{
    // Map icons (playtest 2026-11-03/04: the boss room, the shop and the portal must stand apart from the enemy dots): an 11 x 11
    // pixel skull for the boss room and a ring for each installed portal (C and V), each with a 1 px dark rim so they read on
    // the dark map well and on the terrain. Point-filtered, drawn at 1:1 on the map.
    public sealed partial class TemporaryGameGui
    {
        const int IconSize = 11;
        static Sprite bossIcon, shopIcon, sealIcon, chestIcon;
        static readonly Sprite[] numberedPortalIcons = new Sprite[2];

        // Both maps reuse the approved marker artwork; their viewports and layout remain independent.
        static Sprite ApprovedFullMapIcon(Sprite original)
        {
            string id = original == PlayerIcon ? "player" : original == ShopIcon ? "workshop"
                : original == BossIcon ? "boss-crimson" : original == SealIcon ? "seal"
                : original == ChestIcon ? "chest" : original == NumberedPortalIcon(0) ? "portal-1"
                : original == NumberedPortalIcon(1) ? "portal-2" : null;
            return id == null ? original : MapArt(id) ?? original;
        }

        // X body, o hole (dark), . empty; the rim is added around every body pixel.
        static readonly string[] BossPattern =
        {
            "..XXXXX..",
            ".XXXXXXX.",
            "XXXXXXXXX",
            "XooXXXooX",
            "XooXXXooX",
            "XXXXoXXXX",
            ".XXXXXXX.",
            "..XoXoX..",
            "..XXXXX..",
        };

        // The maintenance shop: a gold anvil (playtest 2026-11-04: each mark should look like what it is).
        static readonly string[] ShopPattern =
        {
            ".........",
            "XXXXXXXX.",
            ".XXXXXXXX",
            "..XXXXX..",
            "...XXX...",
            "...XXX...",
            "..XXXXX..",
            ".XXXXXXX.",
            ".........",
        };
        // Seal challenge room: a standing tablet with its diamond seal (docs/design/world/stage-gimmicks.md).
        static readonly string[] SealPattern =
        {
            "..XXXXX..",
            ".XXXXXXX.",
            ".XXXoXXX.",
            ".XXoXoXX.",
            ".XXXoXXX.",
            ".XXXXXXX.",
            ".XXXXXXX.",
            "XXXXXXXXX",
            ".........",
        };
        // Locked chest room: a chest with its dark band and keyhole.
        static readonly string[] ChestPattern =
        {
            ".........",
            ".XXXXXXX.",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "ooooXoooo",
            "XXXXoXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            ".........",
        };
        static Sprite recoveryIcon;
        static Sprite RecoveryIcon => recoveryIcon ? recoveryIcon : recoveryIcon = IconSprite("recovery", new[]
        {
            ".........", "...XXX...", "...XXX...", ".XXXXXXX.", ".XXXXXXX.", ".XXXXXXX.", "...XXX...", "...XXX...", "........."
        }, new Color32(107, 224, 161, 255));
        static Sprite SealIcon => sealIcon ? sealIcon : sealIcon = IconSprite("seal", SealPattern, new Color32(120, 206, 196, 255));
        static Sprite ChestIcon => chestIcon ? chestIcon : chestIcon = IconSprite("chest", ChestPattern, new Color32(214, 128, 62, 255));
        static Sprite ShopIcon => shopIcon ? shopIcon : shopIcon = IconSprite("shop", ShopPattern, new Color32(232, 196, 92, 255));
        static Sprite BossIcon => bossIcon ? bossIcon : bossIcon = IconSprite("boss", BossPattern, new Color32(214, 58, 46, 255));
        static Sprite NumberedPortalIcon(int slot)
        {
            if (numberedPortalIcons[slot]) return numberedPortalIcons[slot];
            var pattern = PortalPattern();
            // A bright 3x5 digit stays readable on both the full map and minimap, independently of colour.
            string[] digit = slot == 0 ? new[] { ".X.", "XX.", ".X.", ".X.", "XXX" }
                : new[] { "XXX", "..X", "XXX", "X..", "XXX" };
            for (int y = 0; y < pattern.Length; y++)
            {
                var row = pattern[y].Replace('c', 'o').ToCharArray();
                for (int x = 0; x < 3; x++)
                    if (y >= 2 && y < 7) row[x + 3] = digit[y - 2][x] == 'X' ? 'c' : 'o';
                pattern[y] = new string(row);
            }
            var colour = slot == 0 ? new Color32(170, 120, 236, 255) : new Color32(112, 190, 255, 255);
            return numberedPortalIcons[slot] = IconSprite((slot + 1) + "번 포탈", pattern, colour, new Color32(255, 255, 255, 255));
        }

        // The hero (playtest 2026-11-05: the old 7 px teal square sank into the floor and the enemy dots). A bright cyan
        // diamond with a white heart and a dark rim, a ring that pulses out of it and a bobbing arrow above it, so the
        // "you are here" mark never reads as an enemy, a facility or the terrain.
        static readonly Color32 PlayerMarkColor = new Color32(84, 236, 222, 255);
        static Sprite playerIcon, playerHalo, playerPointer;
        static readonly string[] PlayerPattern =
        {
            "....X....",
            "...XXX...",
            "..XXXXX..",
            ".XXXcXXX.",
            "XXXcccXXX",
            ".XXXcXXX.",
            "..XXXXX..",
            "...XXX...",
            "....X....",
        };
        static readonly string[] PointerPattern =
        {
            "XXXXXXX",
            ".XXXXX.",
            "..XXX..",
            "...X...",
        };
        const int PlayerHaloSize = 17;
        static Sprite PlayerIcon => playerIcon ? playerIcon : playerIcon = IconSprite("player", PlayerPattern, PlayerMarkColor, new Color32(255, 255, 255, 255));
        static Sprite PlayerPointer => playerPointer ? playerPointer : playerPointer = IconSprite("player pointer", PointerPattern, PlayerMarkColor);
        static Sprite PlayerHalo => playerHalo ? playerHalo : playerHalo = IconSprite("player halo", RingPattern(PlayerHaloSize, 6.6f, 8.4f), Color.white, rim: false);

        static string[] RingPattern(int size, float inner, float outer)
        {
            var rows = new string[size]; float c = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            {
                var row = new char[size];
                for (int x = 0; x < size; x++) { float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)); row[x] = r >= inner && r <= outer ? 'X' : '.'; }
                rows[y] = new string(row);
            }
            return rows;
        }

        // A ring with a pale core: the portal's swirl.
        static string[] PortalPattern()
        {
            var rows = new string[9];
            for (int y = 0; y < 9; y++)
            {
                var row = new char[9];
                for (int x = 0; x < 9; x++)
                {
                    float dx = x - 4, dy = (y - 4) * 1.15f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    row[x] = r <= 1.2f ? 'c' : r >= 2.4f && r <= 4.3f ? 'X' : r < 2.4f ? 'o' : '.';
                }
                rows[y] = new string(row);
            }
            return rows;
        }

        // The sprite is the pattern plus a 1 px dark rim on every side (none when rim is false).
        static Sprite IconSprite(string name, string[] pattern, Color body, Color32? coreColor = null, bool rim = true)
        {
            var rimColor = new Color32(24, 22, 19, 255); var hole = new Color32(36, 33, 29, 255); var core = coreColor ?? new Color32(238, 226, 255, 255);
            int pad = rim ? 1 : 0, pw = pattern[0].Length, ph = pattern.Length, width = pw + pad * 2, height = ph + pad * 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Map icon " + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[width * height];
            bool Body(int x, int y) => x >= 0 && y >= 0 && x < pw && y < ph && pattern[y][x] != '.';
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int px = x - pad, py = y - pad; Color32 c = new Color32(0, 0, 0, 0);
                    if (Body(px, py)) { char k = pattern[py][px]; c = k == 'o' ? hole : k == 'c' ? core : (Color32)body; }
                    else if (rim)
                        for (int oy = -1; oy <= 1 && c.a == 0; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                if ((ox == 0 || oy == 0) && Body(px + ox, py + oy)) { c = rimColor; break; }
                    // Rows run top-down in the pattern; texture rows run bottom-up.
                    pixels[(height - 1 - y) * width + x] = c;
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
