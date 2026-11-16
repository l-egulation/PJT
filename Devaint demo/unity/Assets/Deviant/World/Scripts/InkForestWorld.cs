using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using Deviant.World.Contracts;
using Deviant.World.Streaming;

namespace Deviant.World
{
    public partial class InkForestWorld : MonoBehaviour
    {
        public InkForestData data;
        public InkForestChunk chunkPrefab;
        public Transform followTarget;
        public Camera followCamera;
        public int themeIndex;
        public bool showDebugHUD=true;
        public Transform parallaxRoot;
        readonly Dictionary<int,InkForestChunk> active=new Dictionary<int,InkForestChunk>();
        // Existing prototype policy; these are streaming templates, not eight combat maps.
        static readonly RepeatingStripPlanner planner = new RepeatingStripPlanner(64, 8, 1, 2);
        public StreamingWindow CurrentWindow { get; private set; }
        readonly Queue<InkForestChunk> pool=new Queue<InkForestChunk>();
        public readonly List<BoxCollider2D> PlatformColliders=new List<BoxCollider2D>();
        public int ActiveCount=>active.Count;
        public int RecycledCount {get;private set;}
        public float FarPhase {get;private set;}
        public float MidPhase {get;private set;}
        SpriteRenderer[] far,mid,front;
        PrototypeForestSession sessionOwner;
        internal void AcquireSession(PrototypeForestSession owner)
        {
            if (sessionOwner != null) throw new System.InvalidOperationException("The prototype forest already has a session owner.");
            sessionOwner = owner;
        }
        internal bool IsSessionOwner(PrototypeForestSession owner) => ReferenceEquals(sessionOwner, owner);
        internal void ReleaseSession(PrototypeForestSession owner) { if (ReferenceEquals(sessionOwner, owner)) sessionOwner = null; }
        void Awake(){IndexExisting();CreateParallax();}
        void IndexExisting()
        {
            if(active.Count>0)return;
            foreach(var c in GetComponentsInChildren<InkForestChunk>())
            {active[c.index]=c;foreach(var b in c.platformRoot.GetComponentsInChildren<BoxCollider2D>())PlatformColliders.Add(b);}
        }
        void Update(){if(followTarget)EnsureChunks(followTarget.position.x);}
        public void EnsureChunks(float x)
        {
            if (!EnsureLayoutWindow(x)) throw new System.InvalidOperationException("Bind the actual common movement profile before streaming this forest.");
        }
        public void Populate(InkForestChunk chunk,int index)
        {
            if (layoutSource == null) throw new System.InvalidOperationException("A frozen validated forest source is required before populating terrain.");
            layoutSource.Populate(chunk, index, themeIndex, PreparePopulatedChunk);
        }
        void PreparePopulatedChunk(InkForestChunk chunk)
        {
            foreach (var box in chunk.platformRoot.GetComponentsInChildren<BoxCollider2D>())
                if (!PlatformColliders.Contains(box)) PlatformColliders.Add(box);
            ChunkPopulated?.Invoke(chunk);
        }
        public event System.Action<InkForestChunk> ChunkPopulated;
        public void VisitChunks(System.Action<InkForestChunk> visitor)
        {
            IndexExisting();
            foreach(var chunk in active.Values) visitor(chunk);
        }
        public void ChangeTheme(int index)
        {
            themeIndex=Mathf.Clamp(index,0,data.themes.Length-1);
            foreach(var c in active.Values)Populate(c,c.index);
            ApplyParallaxSprites();
        }
        public void CreateParallax()
        {
            if(!parallaxRoot){parallaxRoot=new GameObject("Parallax_Far_Mid_Front").transform;parallaxRoot.SetParent(transform,false);}
            far=Layer("Far",3,0);mid=Layer("Mid",3,10);front=Layer("FrontTrimmed",5,40);ApplyParallaxSprites();
        }
        SpriteRenderer[] Layer(string name,int count,int order)
        {
            var list=new SpriteRenderer[count];
            for(int i=0;i<count;i++)
            {
                var child=parallaxRoot.Find(name+"_"+i);
                if(!child){child=new GameObject(name+"_"+i).transform;child.SetParent(parallaxRoot,false);child.gameObject.AddComponent<SpriteRenderer>();}
                list[i]=child.GetComponent<SpriteRenderer>();list[i].sortingOrder=order;list[i].sharedMaterial=data.material;
            }
            return list;
        }
        void ApplyParallaxSprites()
        {
            if(far==null)return;var t=data.themes[themeIndex];
            foreach(var r in far)r.sprite=t.far;foreach(var r in mid)r.sprite=t.mid;foreach(var r in front)r.sprite=t.front;
        }
        public void PositionParallax()
        {
            if(far==null)CreateParallax();var cp=followCamera.transform.position;
            FarPhase=(cp.x-10)*.16f;MidPhase=(cp.x-10)*.46f;
            Place(far,cp,FarPhase,40);Place(mid,cp,MidPhase,40);
            float phase=(cp.x-10)*1.1f;int first=Mathf.FloorToInt(phase/7.5f);
            for(int i=0;i<front.Length;i++)
            {
                int n=first+i;var r=front[i];r.flipX=(n&1)!=0;r.enabled=n%4!=2;
                r.transform.position=new Vector3(cp.x-10+n*7.5f-phase+1.8f,cp.y-5.625f+.55f,0);
            }
        }
        static void Place(SpriteRenderer[] rs,Vector3 cp,float phase,float width)
        {
            int first=Mathf.FloorToInt(phase/width);
            for(int i=0;i<rs.Length;i++){int n=first+i;rs[i].flipX=(n&1)!=0;rs[i].transform.position=new Vector3(cp.x-10+n*width-phase+width*.5f,cp.y,0);}
        }
        public static Vector3 Snap(Vector3 v)=>new Vector3(Mathf.Round(v.x*32)/32,Mathf.Round(v.y*32)/32,v.z);
        void OnDestroy() { sessionOwner?.Dispose(); CloseWorldLayout(); }
        void OnGUI()
        {
            if(!showDebugHUD)return;
            GUI.color=new Color(1,1,1,.95f);
            GUI.Box(new Rect(8,8,640,64),"Forest / "+data.themes[themeIndex].id+" / chunks="+active.Count+" / recycled="+RecycledCount);
        }
    }
}
