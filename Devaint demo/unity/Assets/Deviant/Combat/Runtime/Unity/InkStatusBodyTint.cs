using UnityEngine;

namespace Deviant.Combat
{
    // The ink status body tint (poison purple, mark pop flash) copies the body's final frame after every presenter's
    // LateUpdate. The arena refreshes statuses at execution order 0, but species presenters (Stage1 village monsters 650,
    // Chapter2MonsterArt 800, Chapter1NewMonsterPresentation and Chapter2ElitePresentation 950) rewrite sprite and flipX
    // afterwards: their approved frames face left, while SyncFacing leaves flipX=facing<0. Copying at the arena's time
    // drew the purple silhouette mirrored against the body. Same fix as the white hit flash (HitFlashOverlay).
    [DefaultExecutionOrder(32000)]
    public sealed class InkStatusBodyTint : MonoBehaviour
    {
        public SpriteRenderer Source { get; set; }
        public SpriteRenderer Overlay { get; private set; }
        // Whether a status wants the tint this frame; the body's own visibility decides the rest.
        public bool Wanted { get; set; }
        void Awake()=>Overlay=GetComponent<SpriteRenderer>();
        void LateUpdate()=>Follow();
        public void Follow()
        {
            if(!Overlay)Overlay=GetComponent<SpriteRenderer>();
            if(!Overlay)return;
            if(!Wanted || !Source || !Source.enabled || !Source.sprite){Overlay.enabled=false;return;}
            transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one;
            Overlay.sprite=Source.sprite;Overlay.flipX=Source.flipX;Overlay.flipY=Source.flipY;
            Overlay.sortingLayerID=Source.sortingLayerID;Overlay.sortingOrder=Source.sortingOrder+1;
            Overlay.enabled=true;
        }
    }
}
