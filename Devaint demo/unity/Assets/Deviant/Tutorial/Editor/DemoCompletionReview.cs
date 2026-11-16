using System.IO;
using Deviant.Tutorial.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Tutorial.Editor
{
    public static class DemoCompletionReview
    {
        [MenuItem("Tools/Deviant/Tutorial/Capture demo completion")]
        public static void Capture()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Demo review camera", typeof(Camera)).GetComponent<Camera>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(58, 57, 55, 255);
            var root = new GameObject("Demo review", typeof(Canvas), typeof(CanvasScaler));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.pixelPerfect = true;
            root.GetComponent<CanvasScaler>().scaleFactor = 2;
            canvas.scaleFactor = 2;
            var stage = new GameObject("960x540", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(root.transform, false); stage.sizeDelta = new Vector2(960, 540);
            var background = new GameObject("Existing game capture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(stage, false); background.rectTransform.sizeDelta = new Vector2(960, 540);
            background.sprite = Resources.Load<Sprite>("FirstRunTutorial/Preview/background");
            var dim = new GameObject("Dim world", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(stage, false); dim.rectTransform.sizeDelta = new Vector2(960, 540);
            dim.color = new Color(0, 0, 0, .65f);
            var view = DemoCompletionPanel.Create(stage, () => {}, () => {});
            view.SetSummary("플레이 시간 24:18 / 처치 126 / 마법안료 +38");
            var target = new RenderTexture(1920, 1080, 24); target.Create(); camera.targetTexture = target;
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                var output = Path.GetFullPath("../outputs/demo-completion/preview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, image.EncodeToPNG());
                Object.DestroyImmediate(image); Debug.Log("DEMO_COMPLETION_CAPTURED " + output);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); Object.DestroyImmediate(target);
                Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
