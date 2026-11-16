using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using TMPro;

namespace Deviant.ThemePreview
{
    [Serializable] public sealed class PreviewConfig { public string packagePath, fontPath; public bool coreScreens, extendedScreens; }
    [Serializable] public sealed class PreviewFile { public string file, sha256; }
    [Serializable] public sealed class PreviewSprite
    {
        public string id, file; public float[] view, rect; public int[] imageSize;
    }
    [Serializable] public sealed class PreviewMark { public float[] center; public float size; }
    [Serializable] public sealed class PreviewInk { public string id, name, description, color, bottle, effect, condition; public float[] view; public PreviewMark label, badge; }
    [Serializable] public sealed class PreviewPackage { public int version; public PreviewFile[] files; public PreviewSprite[] sprites; public PreviewInk[] inks; }
    public sealed class PreviewAssets : IDisposable
    {
        public readonly PreviewPackage Package;
        public readonly TMP_FontAsset Font;
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly string root;
        public PreviewAssets(PreviewConfig config)
        {
            root = Path.GetFullPath(config.packagePath);
            Package = JsonUtility.FromJson<PreviewPackage>(File.ReadAllText(Path.Combine(root, "manifest.json")));
            if (Package.version != 3) throw new InvalidDataException("승인 패키지 v3가 필요합니다.");
            Font = TMP_FontAsset.CreateFontAsset(config.fontPath, 0, 64, 8,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
            if (Font == null) throw new InvalidDataException("선택한 로컬 폰트를 읽을 수 없습니다.");
            Font.name = "Local preview font (not deployed)";
            owned.Add(Font);
        }
        public Texture2D Texture(string file)
        {
            if (textures.TryGetValue(file, out var found)) return found;
            var entry = Package.files.FirstOrDefault(f => f.file == file) ?? throw new InvalidDataException(file);
            var path = Path.GetFullPath(Path.Combine(root, file));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Invalid path");
            var bytes = File.ReadAllBytes(path);
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() != entry.sha256)
                    throw new InvalidDataException("승인 원본과 해시가 다릅니다: " + file);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = file; texture.LoadImage(bytes); texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            textures.Add(file, texture); owned.Add(texture); return texture;
        }
        public Texture2D Backdrop()
        {
            const string key = "ApprovedVillageBackdrop";
            if(textures.TryGetValue(key,out var cached))return cached;
            var path=Path.GetFullPath(Path.Combine(root,"../screen-set-v6/assets/village.jpg"));
            if(!File.Exists(path))throw new FileNotFoundException("UI design 저장소의 승인 마을 배경이 필요합니다.",path);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            texture.name=key;texture.LoadImage(File.ReadAllBytes(path));
            // Match CSS source-over in sRGB before Unity's linear canvas blend.
            var pixels=texture.GetPixels32();
            for(int y=0;y<texture.height;y++)
            {float alpha=Mathf.Lerp(.95f,.96f,y/(float)(texture.height-1));
             for(int x=0;x<texture.width;x++){int i=y*texture.width+x;var c=pixels[i];pixels[i]=new Color32((byte)Mathf.RoundToInt(c.r*(1-alpha)+36*alpha),(byte)Mathf.RoundToInt(c.g*(1-alpha)+39*alpha),(byte)Mathf.RoundToInt(c.b*(1-alpha)+43*alpha),255);}}
            texture.SetPixels32(pixels);texture.Apply(false,false);texture.filterMode=FilterMode.Bilinear;
            texture.wrapMode=TextureWrapMode.Clamp;owned.Add(texture);textures.Add(key,texture);return texture;
        }
        public PreviewSprite Find(string id) => Package.sprites.FirstOrDefault(x => x.id == id) ?? throw new InvalidDataException(id);
        public Texture2D SmoothFrameTexture(string file)
        {
            string key=file+":frame-mips";if(textures.TryGetValue(key,out var cached))return cached;
            var source=Texture(file);var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true);
            texture.name=key;texture.SetPixels32(source.GetPixels32());texture.Apply(true,false);texture.filterMode=FilterMode.Trilinear;texture.wrapMode=TextureWrapMode.Clamp;textures.Add(key,texture);return texture;
        }
        public Sprite Sprite(string id, Vector4 border = default)
        {
            var s = Find(id); var t = Texture(s.file); var v = s.rect ?? s.view;
            return Slice(t, new Rect(v[0], v[1], v[2], v[3]), border);
        }
        public Sprite Slice(Texture2D t, Rect topLeftRect, Vector4 border = default)
        {
            var key = t.name + ":" + topLeftRect + ":" + border;
            if (sprites.TryGetValue(key, out var cached)) return cached;
            var rect = new Rect(topLeftRect.x, t.height - topLeftRect.yMax, topLeftRect.width, topLeftRect.height);
            var sprite = UnityEngine.Sprite.Create(t, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            owned.Add(sprite); sprites.Add(key, sprite); return sprite;
        }
        public void Dispose()
        {
            if (Font != null)
            {
                foreach (var atlas in Font.atlasTextures) if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
                if (Font.material != null) UnityEngine.Object.DestroyImmediate(Font.material);
            }
            foreach (var o in owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            owned.Clear(); textures.Clear(); sprites.Clear();
        }
    }
}
