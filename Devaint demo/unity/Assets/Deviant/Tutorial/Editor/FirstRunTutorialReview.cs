using System.IO;
using System.Reflection;
using Deviant.Tutorial.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Tutorial.Editor
{
    public static class FirstRunTutorialReview
    {
        public static void BuildAndCapture()
        {
            FirstRunTutorialBuilder.Build();
            Capture();
        }

        [MenuItem("Tools/Deviant/Tutorial/Capture approved tutorial")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(FirstRunTutorialBuilder.ScenePath);
            var camera = Object.FindAnyObjectByType<Camera>();
            var view = Object.FindAnyObjectByType<FirstRunTutorialWindow>();
            var output = Path.GetFullPath("../outputs/tutorial-check/captures"); Directory.CreateDirectory(output);
            var target = new RenderTexture(960, 540, 24); target.Create();
            camera.targetTexture = target;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = canvas.sortingOrder > 0 ? 1 : 2;
            }
            view.ShowRewards();
            typeof(FirstRunTutorialWindow).GetField("shownAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, Time.unscaledTime - 1);
            for (int i = 0; i < 5; i++)
            {
                view.SendMessage("Fit");
                Canvas.ForceUpdateCanvases();
                Save(camera, target, Path.Combine(output, "page-" + (i + 1) + ".png"));
                view.NextPage();
            }
            view.ShowAttack(); view.SendMessage("Fit"); Canvas.ForceUpdateCanvases();
            Save(camera, target, Path.Combine(output, "attack.png"));
            camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target);
            Debug.Log("APPROVED_TUTORIAL_CAPTURED " + output);
        }

        static void Save(Camera camera, RenderTexture target, string path)
        {
            camera.Render(); var before = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = before; Object.DestroyImmediate(image);
        }
    }
}
