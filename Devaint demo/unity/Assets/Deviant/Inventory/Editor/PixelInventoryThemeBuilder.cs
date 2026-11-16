#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Deviant.Inventory.Unity.Editor
{
    public static class PixelInventoryThemeBuilder
    {
        const string Root = "Assets/Deviant/Inventory/Resources/PixelInventory";
        static Sprite Sprite(string name)
        {
            string path = Root + "/Art/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) throw new InvalidOperationException("Missing pixel inventory sprite: " + path);
            return sprite;
        }
        [MenuItem("Tools/deviant/UI/Build Pixel Inventory Theme")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in Directory.GetFiles(Root + "/Fonts", "*.ttf"))
            {
                var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(path);
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.fontTextureCase = FontTextureCase.CustomSet;
                importer.fontSize = 11;
                importer.customCharacters = File.ReadAllText(Root + "/Fonts/Glyphs.txt");
                importer.SaveAndReimport();
            }
            var theme = ScriptableObject.CreateInstance<PixelInventoryTheme>();
            theme.windowFrame = Sprite("window"); theme.windowFooter = Sprite("footer");
            theme.cardFrame = Sprite("card"); theme.selectedCardFrame = Sprite("card-selected");
            theme.slotFrame = Sprite("slot"); theme.selectedSlotFrame = Sprite("slot-selected");
            theme.buttonFrame = Sprite("button"); theme.primaryButtonFrame = Sprite("button-primary");
            theme.portrait = Sprite("portrait"); theme.bag = Sprite("bag");
            theme.titleLogo = Sprite("title-logo"); theme.closeIcon = Sprite("close");
            theme.regularFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Galmuri11.ttf");
            theme.boldFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Galmuri11-Bold.ttf");
            if (!theme.Ready || theme.regularFont.dynamic || theme.boldFont.dynamic) throw new InvalidOperationException("Static pixel theme fonts/sprites are incomplete.");
            foreach (var face in new[] { theme.regularFont, theme.boldFont })
                if (!face.GetCharacterInfo('가', out _, 11)) throw new InvalidOperationException("Pixel font lost Korean glyphs: " + face.name);
            string[] weapons = { "base_sword", "whip", "rapier", "dagger", "hammer", "shield", "greatsword", "launcher", "bomb", "shuriken" };
            theme.weaponIcons = weapons.Select(id => new PixelIcon { id = id, sprite = Sprite("weapon-" + id) }).ToArray();
            theme.itemIcons = new[] {
                new PixelIcon { id="stat.basic",sprite=Sprite("ink-power") }, new PixelIcon { id="stat.special",sprite=Sprite("ink-follow") },
                new PixelIcon { id="stat.speed",sprite=Sprite("ink-speed") }, new PixelIcon { id="stat.cooldown",sprite=Sprite("ink-special") },
                new PixelIcon { id="special.range",sprite=Sprite("ink-range") }, new PixelIcon { id="special.move",sprite=Sprite("ink-move") },
                new PixelIcon { id="special.follow",sprite=Sprite("ink-cooldown") }, new PixelIcon { id="form.whip",sprite=Sprite("ink-move") }
            };
            string assetPath = Root + "/Theme.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PixelInventoryTheme>(assetPath);
            if (existing) { EditorUtility.CopySerialized(theme, existing); UnityEngine.Object.DestroyImmediate(theme); theme = existing; }
            else AssetDatabase.CreateAsset(theme, assetPath);
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_INVENTORY_THEME_READY weapons=" + theme.weaponIcons.Length + " glyphs=" + theme.regularFont.characterInfo.Length + " customCase=" + (int)FontTextureCase.CustomSet + " raster=" + (int)FontRenderingMode.HintedRaster);
        }
        [MenuItem("Tools/deviant/UI/Save Live Pixel Inventory Prefab")]
        public static void SaveLivePrefab()
        {
            var view = UnityEngine.Object.FindAnyObjectByType<TemporaryInventoryView>();
            if (!view || !view.UsesPixelTheme) throw new InvalidOperationException("Run the pixel inventory before saving its prefab.");
            var systems = view.GetComponentsInChildren<EventSystem>(true);
            var parents = systems.Select(system => system.transform.parent).ToArray();
            var preview = view.transform.Find("Ink inventory/Current character preview");
            var image = preview ? preview.GetComponent<UnityEngine.UI.Image>() : null;
            Sprite previous = image ? image.sprite : null;
            var canvases = view.GetComponentsInChildren<Canvas>(true).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            try
            {
                foreach (var system in systems) system.transform.SetParent(null, true);
                foreach (var item in canvases) item.worldCamera = null;
                if (image) image.sprite = null;
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, Root + "/InventoryView.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("PIXEL_INVENTORY_PREFAB_READY " + Root + "/InventoryView.prefab");
            }
            finally
            {
                if (image) image.sprite = previous;
                for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].worldCamera = cameras[i];
                for (int i = 0; i < systems.Length; i++) if (systems[i]) systems[i].transform.SetParent(parents[i], true);
            }
        }
    }
}
#endif
