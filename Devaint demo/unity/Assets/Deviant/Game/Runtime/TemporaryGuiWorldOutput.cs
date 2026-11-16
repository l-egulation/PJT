using Stage1VillageExperiment;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Native world output must be behind overlay canvases. IMGUI's depth cannot
    // order a full-screen texture behind uGUI, so retain the same buffer in uGUI.
    public sealed class TemporaryGuiWorldOutput : MonoBehaviour
    {
        VillagePixelViewport viewport;
        GameObject root;
        RawImage image;
        RectTransform clip;
        // Screen shake of the world picture only (native pixels); the camera and the GUI stay put.
        public Vector2 ShakePixels { get; set; }
        void LateUpdate()
        {
            if (!viewport) viewport = Object.FindAnyObjectByType<VillagePixelViewport>();
            if (!viewport || !viewport.Ready) { if (root) root.SetActive(false); return; }
            if (!root)
            {
                root = new GameObject("Native world behind GUI", typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-1000;
                var background=new GameObject("Letterbox",typeof(RectTransform),typeof(Image));background.transform.SetParent(root.transform,false);
                var bounds=background.GetComponent<RectTransform>();bounds.anchorMin=Vector2.zero;bounds.anchorMax=Vector2.one;bounds.offsetMin=bounds.offsetMax=Vector2.zero;
                background.GetComponent<Image>().color=Color.black;background.GetComponent<Image>().raycastTarget=false;
                var mask=new GameObject("Fixed viewport",typeof(RectTransform),typeof(RectMask2D));mask.transform.SetParent(root.transform,false);
                clip=mask.GetComponent<RectTransform>();clip.anchorMin=clip.anchorMax=clip.pivot=new Vector2(0,1);
                var output=new GameObject("Native 960x540",typeof(RectTransform),typeof(RawImage));output.transform.SetParent(clip,false);
                image=output.GetComponent<RawImage>();image.raycastTarget=false;
            }
            viewport.ExternalPresentation=true;root.SetActive(true);image.texture=viewport.NativeTexture;
            var rect=viewport.DisplayRect;var rt=image.rectTransform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
            float scale=rect.width/960f;
            clip.anchoredPosition=new Vector2(rect.x,-rect.y);clip.sizeDelta=rect.size;
            rt.anchoredPosition=new Vector2(ShakePixels.x*scale,ShakePixels.y*scale);rt.sizeDelta=rect.size;
        }
        void OnDisable(){if(viewport)viewport.ExternalPresentation=false;if(root)root.SetActive(false);}
        void OnDestroy(){if(root)Destroy(root);}
    }
}
