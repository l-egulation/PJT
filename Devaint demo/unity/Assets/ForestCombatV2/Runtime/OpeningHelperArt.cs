using System;
using UnityEngine;

namespace ForestCombatV2
{
    // Native 320x200 cells cut from the preserved sheet. Loading individual textures avoids the sheet's 8960px width.
    public static class OpeningHelperArt
    {
        public const int Width = 320, Height = 200, AppearFrames = 28, IdleFrames = 6, LeapFrames = 9;
        public static readonly Vector2 Anchor = new Vector2(160, 194);

        public static Sprite[][] Load(string root)
        {
            string[] clips = { "appear", "idle", "leap" };
            int[] counts = { AppearFrames, IdleFrames, LeapFrames };
            var frames = new Sprite[clips.Length][];
            for (int row = 0; row < clips.Length; row++)
            {
                frames[row] = new Sprite[counts[row]];
                for (int i = 0; i < counts[row]; i++)
                {
                    string path = root + "/" + clips[row] + "/" + i.ToString("00");
                    var sprite = Resources.Load<Sprite>(path);
                    if (!sprite || sprite.texture.width != Width || sprite.texture.height != Height || !sprite.texture.isReadable
                        || sprite.rect.size != new Vector2(Width, Height) || sprite.pixelsPerUnit != 32
                        || (sprite.pivot - new Vector2(Anchor.x, Height - Anchor.y)).sqrMagnitude > .001f)
                        throw new InvalidOperationException("Opening helper frame missing or invalid: " + path
                            + ". Apply the pinned resources and preserve its 320x200 canvas, foot pivot, PPU and Read/Write setting.");
                    frames[row][i] = sprite;
                }
            }
            return frames;
        }
    }
}
