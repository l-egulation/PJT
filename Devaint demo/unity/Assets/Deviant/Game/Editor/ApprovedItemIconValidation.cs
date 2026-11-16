using System;
using UnityEditor;
using UnityEngine;

namespace Deviant.Game.Editor
{
    public static class ApprovedItemIconValidation
    {
        const string Folder = "Assets/Deviant/TemporaryGUI/Resources/ApprovedItemIcons";

        [MenuItem("Tools/deviant/Item Icons/Validate Applied Sprites")]
        public static void Run()
        {
            var ids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder });
            if (ids.Length != 71)
                throw new InvalidOperationException($"Expected 71 item icons, found {ids.Length}.");
            foreach (var id in ids)
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!sprite || sprite.rect.width <= 0 || sprite.rect.height <= 0 || importer == null ||
                    importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.filterMode != FilterMode.Bilinear || !importer.alphaIsTransparency)
                    throw new InvalidOperationException("Invalid item sprite import: " + path);
                if (Resources.Load<Sprite>("ApprovedItemIcons/" + sprite.name) == null)
                    throw new InvalidOperationException("Item sprite is not loadable: " + path);
            }
            Debug.Log("ApprovedItemIconValidation: loaded 71 sprites; alpha import, no compression, no mipmaps, bilinear filtering verified.");
        }
    }
}
