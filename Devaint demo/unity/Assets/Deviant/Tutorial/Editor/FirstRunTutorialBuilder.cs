using System.IO;
using Deviant.Tutorial.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deviant.Tutorial.Editor
{
    public static class FirstRunTutorialBuilder
    {
        const string Root = "Assets/Deviant/Tutorial";
        public const string PrefabPath = Root + "/Prefabs/FirstRunTutorialWindow.prefab";
        public const string ScenePath = Root + "/Preview/FirstRunTutorialPreview.unity";

        [MenuItem("Tools/Deviant/Tutorial/Build approved tutorial")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(Root + "/Resources/FirstRunTutorial", "*.png", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Point;
                importer.spriteBorder = path.EndsWith("/keycap.png") ? new Vector4(5,5,5,5) : path.EndsWith("/panel.png") ? new Vector4(8,8,8,8) : Vector4.zero;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Preview");
            var viewObject = new GameObject("FirstRunTutorialWindow", typeof(RectTransform));
            var view = viewObject.AddComponent<FirstRunTutorialWindow>();
            view.Build();
            PrefabUtility.SaveAsPrefabAsset(viewObject, PrefabPath);
            Object.DestroyImmediate(viewObject);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(58, 57, 55, 255);
            var backdrop = Resources.Load<Sprite>("FirstRunTutorial/Preview/background");
            if (backdrop)
            {
                var background = new GameObject("Review background only", typeof(Canvas), typeof(CanvasScaler));
                background.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = background.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(960, 540);
                var image = new GameObject("Actual game capture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(background.transform, false);
                image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
                image.sprite = backdrop; image.preserveAspect = true; image.raycastTarget = false;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var preview = new GameObject("Tutorial art review").AddComponent<FirstRunTutorialPreview>();
            preview.window = instance.GetComponent<FirstRunTutorialWindow>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("APPROVED_TUTORIAL_BUILT " + PrefabPath + " " + ScenePath);
        }
    }
}
