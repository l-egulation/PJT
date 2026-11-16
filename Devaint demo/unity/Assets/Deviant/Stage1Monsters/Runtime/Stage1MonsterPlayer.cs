using UnityEngine;

namespace Deviant.Stage1Monsters
{
    // Presentation only. The caller owns movement, accepted hits, damage and hitstun.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Stage1MonsterPlayer : MonoBehaviour
    {
        public Stage1MonsterCatalog catalog;
        public string monsterId;
        public bool tickAutomatically=true;
        public bool autoReturnToIdle=true;
        public bool paused;
        public bool faceRight;
        public string Motion { get; private set; }
        public float Age { get; private set; }
        public int Frame { get; private set; }
        public Stage1MonsterDefinition Definition { get; private set; }
        public Stage1MonsterClip Clip { get; private set; }
        SpriteRenderer target;
        bool hold;

        void Start() { Initialize(); }
        void Update() { if(tickAutomatically && !paused)Advance(Time.deltaTime); }
        public void Initialize()
        {
            target=GetComponent<SpriteRenderer>();Definition=catalog ? catalog.Find(monsterId) : null;
            if(Definition==null) { enabled=false;return; }
            if(Clip==null)Play(Definition.Find("idle")!=null?"idle":"base");
        }
        public bool Play(string motion,bool holdUntilChanged=false)
        {
            if(Definition==null) { target=GetComponent<SpriteRenderer>();Definition=catalog?catalog.Find(monsterId):null; }
            var clip=Definition?.Find(motion);
            if(clip==null)return false; // Missing boss motions must stay missing.
            Clip=clip;Motion=motion;Age=0;hold=holdUntilChanged;Sample();return true;
        }
        public void Advance(float deltaSeconds)
        {
            if(Clip==null)return;
            Age+=Mathf.Max(0,deltaSeconds);
            if(!Clip.loop && Age>=Clip.DurationMs/1000f && !hold && autoReturnToIdle && Motion!="death")
            {
                string fallback=Definition.Find("idle")!=null?"idle":"base";
                if(Motion!=fallback) { Play(fallback);return; }
            }
            Sample();
        }
        public void SampleAt(float seconds) { Age=Mathf.Max(0,seconds);Sample(); }
        void Sample()
        {
            if(!target || Clip==null)return;
            Frame=Clip.FrameAt(Age);target.sprite=Clip.frames[Frame];target.flipX=faceRight;
            target.enabled=Motion!="death" || Age<Clip.DurationMs/1000f;
        }
    }
}
