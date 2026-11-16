#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Deviant.UI.Editor
{
// Native dimensions are intentional. All GUI sprites share one PPU.
public sealed class DeviantUIImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Deviant/UI/Sprites/")) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);
        t.spritePixelsPerUnit = 100f;
        t.filterMode = FilterMode.Point;
        t.mipmapEnabled = false;
        t.alphaIsTransparency = true;
        t.textureCompression = TextureImporterCompression.Uncompressed;
        t.npotScale = TextureImporterNPOTScale.None;
        t.maxTextureSize = 2048;
        t.spriteBorder = Vector4.zero;
        string id=System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string manifest="Assets/Deviant/UI/Data/sprite-manifest.json";
        if(System.IO.File.Exists(manifest))
        {
            var list=JsonUtility.FromJson<GuiManifest>("{\"sprites\":"+System.IO.File.ReadAllText(manifest)+"}");
            var part=System.Array.Find(list.sprites,p=>p.id==id);
            if(part!=null)t.spriteBorder=new Vector4(part.border[0],part.border[1],part.border[2],part.border[3]);
        }
        if(id=="hp_fill"||id=="exp_fill")t.spriteBorder=new Vector4(6,0,6,0);
    }
}
[System.Serializable] public class GuiManifest { public GuiSpriteRecord[] sprites; }
[System.Serializable] public class GuiSpriteRecord { public string id;public int[] size,border;public string sha256; }
}
#endif

