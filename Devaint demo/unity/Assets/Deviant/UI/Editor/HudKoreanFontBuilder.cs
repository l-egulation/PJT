#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Deviant.UI.Editor
{
    // Bake the actual HUD vocabulary. The player does not need an OS font or a dynamic TTF import.
    public static class HudKoreanFontBuilder
    {
        public const string AssetPath = "Assets/Deviant/UI/Resources/UI/KoreanHUD.asset";
        public static string RequiredCharacters()
        {
            var chars = new SortedSet<char>();
            for (char c = ' '; c <= '~'; c++) chars.Add(c);
            foreach (char c in "가방잉크정비장착해제획득검기본폭탄채찍레이피어단망치방패대수리범위이동후속타능력피해공격속쿨감전체특수선택닫기일시지계합니다숲챕터확인실제표시비활성종류칸사용 →↔×·…") chars.Add(c);
            string[] folders = { "Assets/Deviant/UI/Scripts", "Assets/Deviant/Inventory", "Assets/Deviant/Game/Runtime" };
            foreach (var folder in folders)
                if (Directory.Exists(folder)) foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)) Add(File.ReadAllText(path), chars);
            string[] data = { "Assets/Deviant/Combat/Resources/MeleeCatalog.json", "Assets/Deviant/Inventory/Resources/TemporaryInventoryContent.asset" };
            foreach (string path in data) if (File.Exists(path)) Add(File.ReadAllText(path), chars);
            return new string(chars.ToArray());
        }
        static void Add(string text, SortedSet<char> chars)
        {
            foreach (Match match in Regex.Matches(text, @"[가-힣ㄱ-ㅎㅏ-ㅣ]+")) foreach (char c in match.Value) chars.Add(c);
        }
        [MenuItem("Tools/deviant/UI/Bake Korean HUD Font")]
        public static void Build()
        {
            string source = Path.Combine(Application.dataPath, "Deviant/UI/Fonts/malgun.ttf");
            if (!File.Exists(source)) source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
            if (!File.Exists(source)) throw new FileNotFoundException("The checked-in Malgun font source is required to bake HUD glyphs.");
            string glyphs = RequiredCharacters();
            var baked = TMP_FontAsset.CreateFontAsset(source, 0, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048);
            if (!baked) throw new InvalidOperationException("HUD font face load failed: " + source);
            if (!baked.TryAddCharacters(glyphs, out string missing))
                throw new InvalidOperationException("HUD glyph bake failed: " + missing);
            baked.name = "KoreanHUD"; baked.atlasPopulationMode = AtlasPopulationMode.Static;
            var serialized = new SerializedObject(baked);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serialized.FindProperty("m_SourceFontFile").objectReferenceValue = null;
            serialized.FindProperty("m_SourceFontFilePath").stringValue = "";
            serialized.FindProperty("m_SourceFontFileGUID").stringValue = "";
            var editorSource = serialized.FindProperty("m_SourceFontFile_EditorRef");
            if (editorSource != null) editorSource.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            TMP_FontAsset target;
            if (existing)
            {
                foreach (var child in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                    if (child != existing && (child is Texture2D || child is Material)) UnityEngine.Object.DestroyImmediate(child, true);
                EditorUtility.CopySerialized(baked, existing); target = existing;
            }
            else { AssetDatabase.CreateAsset(baked, AssetPath); target = baked; }
            target.material.name = "KoreanHUD Material"; AssetDatabase.AddObjectToAsset(target.material, target);
            foreach (var atlas in target.atlasTextures)
            {
                atlas.name = "KoreanHUD Atlas"; atlas.filterMode = FilterMode.Bilinear;
                AssetDatabase.AddObjectToAsset(atlas, target);
            }
            EditorUtility.SetDirty(target); AssetDatabase.SaveAssets();
            if (existing)
            {
                // TMP destroys its atlas/material in OnDestroy. They now belong to target.
                var detached = new SerializedObject(baked);
                detached.FindProperty("m_AtlasTextures").arraySize = 0;
                detached.FindProperty("m_Material").objectReferenceValue = null;
                detached.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(baked);
            }
            target.ReadFontAssetDefinition();
            if (!target.HasCharacters(glyphs)) throw new InvalidOperationException("Saved HUD font lost required characters.");
            Debug.Log("KOREAN_HUD_FONT_READY glyphs=" + glyphs.Length + " atlases=" + target.atlasTextures.Length + " path=" + AssetPath);
        }
    }
}
#endif
