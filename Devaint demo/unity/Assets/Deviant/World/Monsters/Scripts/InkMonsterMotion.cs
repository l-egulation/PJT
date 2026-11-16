using UnityEngine;
namespace Deviant.World
{
    public class InkMonsterMotion : MonoBehaviour
    {
        public InkMonsterData data;
        public SpriteRenderer target;
        public int Frame {get;private set;}
        public string State {get;private set;}="move";
        public void ShowDeath(float seconds,int facing)
        {
            State="death";var clip=data.death;
            Frame=clip.FrameAt(Mathf.Min(seconds,clip.Duration-.00001f));
            target.sprite=clip.frames[Frame];
            target.flipX=data.sourceFacesLeft?facing>0:facing<0;
        }
        public void Show(string state,float seconds,int facing)
        {
            State=state;var clip=state=="attack"?data.attack:data.move;
            Frame=clip.FrameAt(seconds);target.sprite=clip.frames[Frame];
            target.flipX=data.sourceFacesLeft?facing>0:facing<0;
            // Per-motion importer pivots align the 128px walk and 160px charge canvases.
            // Never crop, normalize width, or animate the Transform scale/position here.
        }
    }
}
