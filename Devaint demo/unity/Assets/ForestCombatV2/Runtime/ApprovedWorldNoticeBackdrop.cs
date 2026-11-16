using UnityEngine;
using UnityEngine.UI;

namespace ForestCombatV2
{
    // The world notice's dark backing belongs below the gameplay Canvas (100), while its IMGUI
    // labels remain above the world. No raycaster or input scope is attached to this presentation.
    internal sealed class ApprovedWorldNoticeBackdrop : MonoBehaviour
    {
        RawImage image;
        Texture2D texture;
        int publishedFrame=-2;
        internal bool Visible=>image&&image.enabled;

        internal void Present(Texture2D opacity,Rect screenBounds,float alpha)
        {
            if(!image)
            {
                var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
                var group=gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
                var child=new GameObject("Top falloff",typeof(RectTransform),typeof(RawImage));child.transform.SetParent(transform,false);
                image=child.GetComponent<RawImage>();image.raycastTarget=false;
                image.rectTransform.anchorMin=image.rectTransform.anchorMax=image.rectTransform.pivot=new Vector2(0,1);
                // Store explicit linear RGB in a linear texture: its alpha stays identical to the authored falloff.
                var pixels=opacity.GetPixels();bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
                for(int i=0;i<pixels.Length;i++)
                {
                    var color=new Color(36/255f,39/255f,43/255f,pixels[i].a);pixels[i]=linear?color.linear:color;
                }
                texture=new Texture2D(opacity.width,opacity.height,TextureFormat.RGBA32,false,linear)
                {name="World notice backing",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                texture.SetPixels(pixels);texture.Apply(false,false);image.texture=texture;
            }
            image.rectTransform.anchoredPosition=new Vector2(screenBounds.x,-screenBounds.y);
            image.rectTransform.sizeDelta=screenBounds.size;image.color=new Color(1,1,1,alpha);
            image.enabled=alpha>0;publishedFrame=Time.frameCount;
        }

        void LateUpdate()
        {
            // OnGUI publishes after LateUpdate, so preserve the immediately preceding rendered frame only.
            if(image&&Time.frameCount>publishedFrame+1)image.enabled=false;
        }
        void OnDisable(){if(image)image.enabled=false;}
        void OnDestroy()
        {
            if(!texture)return;
            if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);
        }
    }
}
