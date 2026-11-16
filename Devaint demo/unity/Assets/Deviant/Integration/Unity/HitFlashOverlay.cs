using UnityEngine;

namespace Deviant.Integration
{
    // Copies the body's final frame after every presenter's LateUpdate. Species presenters
    // (e.g. Chapter1NewMonsterPresentation at 950) rewrite sprite/flipX after the bridge runs,
    // so copying in the bridge left the white flash mirrored against the body.
    [DefaultExecutionOrder(32000)]
    public sealed class HitFlashOverlay:MonoBehaviour
    {
        public SpriteRenderer Source { get; set; }
        public SpriteRenderer Overlay { get; private set; }
        void Awake()=>Overlay=GetComponent<SpriteRenderer>();
        void LateUpdate()=>Follow();
        public void Follow()
        {
            if(!Source||!Overlay)return;
            Overlay.sprite=Source.sprite;Overlay.flipX=Source.flipX;Overlay.flipY=Source.flipY;
            Overlay.sortingLayerID=Source.sortingLayerID;Overlay.sortingOrder=Source.sortingOrder+1;
        }
    }
}
