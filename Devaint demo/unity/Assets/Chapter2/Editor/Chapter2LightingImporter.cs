using System;
using System.IO;
using System.Linq;
using Deviant.Presentation.Unity;
using UnityEditor;
using UnityEngine;

namespace Deviant.Chapter2.Editor
{
    public sealed class Chapter2LightingImporter : AssetPostprocessor
    {
        const string Root = "Assets/Chapter2/Resources/Chapter2Lighting/";
        [Serializable] sealed class Recipe { public Part[] parts; public WindowRecipe window; }
        [Serializable] sealed class WindowRecipe { public int[] frameCanvas, grilleTopLeft, opening; public float pixelsPerWorldUnit; public Bar[] grilleBars; }
        [Serializable] sealed class Bar { public int[] rect; }
        [Serializable] sealed class Part { public string name; public int[] size; public Sockets sockets; }
        [Serializable] sealed class Sockets { public float[] top, bottom, chain, flame; }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        }
        [MenuItem("Tools/deviant/Chapter2 Lighting/Rebuild Art Binding")]
        public static void Build()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText("../resources/environments/waterway-lighting/source/recipe.json"));
            Sprite Load(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + id + ".png")
                ? AssetDatabase.LoadAssetAtPath<Sprite>(Root + id + ".png") : throw new InvalidOperationException("Restore " + id);
            Part PartOf(string id) => recipe.parts.Single(p => p.name == id);
            Vector2 Socket(string id, string key)
            {
                var part = PartOf(id); var points = part.sockets;
                var value = key == "top" ? points.top : key == "bottom" ? points.bottom : key == "chain" ? points.chain : points.flame;
                return new Vector2(value[0] - part.size[0] * .5f, part.size[1] * .5f - value[1]) / 32;
            }
            T Asset<T>(string name) where T : ScriptableObject
            {
                var value = AssetDatabase.LoadAssetAtPath<T>(Root + name + ".asset");
                if (!value) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, Root + name + ".asset"); }
                return value;
            }
            var chain = Asset<HangingConnectorArt>("Chain");
            chain.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deviant/World/Data/OriginalPixelUnlit.mat");
            chain.chainFace = new ConnectorSpritePiece { sprite = Load("chain-face"), topSocket = Socket("chain-face", "top"), bottomSocket = Socket("chain-face", "bottom") };
            chain.chainEdge = new ConnectorSpritePiece { sprite = Load("chain-edge"), topSocket = Socket("chain-edge", "top"), bottomSocket = Socket("chain-edge", "bottom") };
            chain.Validate(); EditorUtility.SetDirty(chain);
            var art = Asset<Chapter2LightingArt>("Art");
            art.housing = Load("lantern-housing"); art.mount = Load("lantern-mount");
            art.torch = Load("torch-hardware"); art.flame = Load("flame"); art.chain = chain;
            art.contactGlint = Load("contact-glint");
            art.housingTop = Socket("lantern-housing", "top"); art.housingFlame = Socket("lantern-housing", "flame");
            art.mountChain = Socket("lantern-mount", "chain");
            // The flame sprite's center sits half its height above the torch's basket socket.
            art.torchFlame = Socket("torch-hardware", "flame") + Vector2.up * art.flame.bounds.extents.y;
            var window = recipe.window ?? throw new InvalidOperationException("Pinned window geometry is required.");
            Vector3 WindowPoint(float x, float y) => new Vector3(x, window.frameCanvas[1] - y) / window.pixelsPerWorldUnit;
            var opening = window.opening;
            var min = WindowPoint(opening[0], opening[3]); var max = WindowPoint(opening[2], opening[1]);
            art.windowOpening = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            art.grilleVertices = new Vector3[window.grilleBars.Length * 4]; art.grilleEdges = new int[window.grilleBars.Length * 8];
            for (int i = 0; i < window.grilleBars.Length; i++)
            {
                var bar = window.grilleBars[i].rect; float x = window.grilleTopLeft[0], y = window.grilleTopLeft[1];
                art.grilleVertices[i * 4] = WindowPoint(x + bar[0], y + bar[1]);
                art.grilleVertices[i * 4 + 1] = WindowPoint(x + bar[2], y + bar[1]);
                art.grilleVertices[i * 4 + 2] = WindowPoint(x + bar[2], y + bar[3]);
                art.grilleVertices[i * 4 + 3] = WindowPoint(x + bar[0], y + bar[3]);
                for (int edge = 0; edge < 4; edge++) { art.grilleEdges[i * 8 + edge * 2] = i * 4 + edge; art.grilleEdges[i * 8 + edge * 2 + 1] = i * 4 + (edge + 1) % 4; }
            }
            EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
            Debug.Log("CHAPTER2_LIGHTING_ART_READY");
        }
    }
}
