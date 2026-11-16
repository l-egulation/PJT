using System;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Approved stage-1 sky (resources environments/stage1-atmosphere-sky-ash-20261111):
    // seeded clouds and birds that stand still in the drained world and shift only with the camera's parallax, and one reusable mesh of torn paper ash.
    // No Editor API, scene lookup, physics or per-frame spawning.
    public sealed class Stage1Atmosphere : MonoBehaviour
    {
        [Range(0,180)] public int ashCount=96;
        public float windPixelsPerSecond=30;
        public bool manualTime;
        public bool ashEnabled=true;
        Camera view;
        SpriteRenderer[] clouds,birds;
        Vector2[] cloudOffsets,birdOffsets;
        Vector3 anchor;
        Mesh ashMesh;
        Material ashMaterial;
        Vector3[] vertices;
        Color[] colors;
        int[] triangles;
        Fleck[] flecks;
        float clock;
        static readonly Vector2[] TornShape={new Vector2(-1,-.25f),new Vector2(-.25f,-1),new Vector2(1,-.4f),new Vector2(.6f,.7f),new Vector2(-.5f,1)};
        struct Fleck { public float x,y,depth,phase,speed,size,angle; }
        // Fixed per-fragment colors keep the motion RNG and palette stable across frames.
        static Color AshTint(int index)
        {
            int slot=(index*7)%10;
            if(slot==0)return new Color32(20,20,20,255);
            if(slot<3)return Color.white;
            return new Color(.49f,.46f,.41f,1);
        }
        public void Initialize(Camera camera,Sprite[] cloudSprites,Sprite[] birdSprites,Material skyMaterial,Shader ashShader,int seed)
        {
            if(!camera||cloudSprites.Length!=6||birdSprites.Length!=6)throw new ArgumentException("Camera and six variants per sky set are required.");
            if(!skyMaterial||!ashShader)throw new ArgumentException("Sky material and paper ash shader are required.");
            view=camera;anchor=view.transform.position;
            var random=new System.Random(seed);
            clouds=Scatter(cloudSprites,7,5,8,6,-110,skyMaterial,random,out cloudOffsets);
            birds=Scatter(birdSprites,6,4,10,9,-95,skyMaterial,random,out birdOffsets);
            flecks=new Fleck[ashCount];vertices=new Vector3[ashCount*5];colors=new Color[vertices.Length];triangles=new int[ashCount*9];
            int[] local={0,1,2,0,2,4,2,3,4};
            for(int i=0;i<ashCount;i++)
            {
                flecks[i]=new Fleck{x=Next(random),y=Next(random),depth=Next(random),phase=Next(random)*6.283f,speed=.7f+Next(random)*.7f,size=i%7==0?6.5f:2.5f+Next(random)*2f,angle=Next(random)*6.283f};
                int v=i*5,t=i*9;for(int j=0;j<9;j++)triangles[t+j]=v+local[j];
            }
            var node=new GameObject("Paper ash - one reusable dynamic mesh",typeof(MeshFilter),typeof(MeshRenderer));
            node.transform.SetParent(transform,false);
            ashMesh=new Mesh{name="Pixel-snapped paper ash"};ashMesh.MarkDynamic();
            node.GetComponent<MeshFilter>().sharedMesh=ashMesh;
            ashMaterial=new Material(ashShader);var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=ashMaterial;renderer.sortingOrder=50;
            RenderAt(0);
        }
        static float Next(System.Random r)=>(float)r.NextDouble();
        SpriteRenderer[] Scatter(Sprite[] sprites,int cols,int rows,float sx,float sy,int order,Material material,System.Random random,out Vector2[] offsets)
        {
            var result=new SpriteRenderer[cols*rows];offsets=new Vector2[result.Length];
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
            {
                int i=y*cols+x;var node=new GameObject(sprites[0].name+" seeded "+i,typeof(SpriteRenderer));node.transform.SetParent(transform,false);
                var r=node.GetComponent<SpriteRenderer>();r.sprite=sprites[random.Next(sprites.Length)];r.sharedMaterial=material;r.sortingOrder=order;r.flipX=random.Next(2)==0;
                result[i]=r;offsets[i]=new Vector2((x+.15f+Next(random)*.7f)*sx-cols*sx*.5f,(y+.15f+Next(random)*.7f)*sy-rows*sy*.5f);
            }
            return result;
        }
        static float Wrap(float value,float span)=>Mathf.Repeat(value+span*.5f,span)-span*.5f;
        static Vector3 Snap(Vector3 v)=>new Vector3(Mathf.Round(v.x*32)/32,Mathf.Round(v.y*32)/32,0);
        void LateUpdate(){if(view&&!manualTime){clock+=Time.deltaTime;RenderAt(clock);}}
        public void RenderAt(float seconds)
        {
            if(!view)return;
            var camera=view.transform.position;var delta=camera-anchor;
            for(int i=0;i<clouds.Length;i++)
            {
                // The combat stages are the drained (sketch) world: clouds and birds stand still and only shift with the
                // camera's parallax (user 2026-11-12; they move only in colour, in the village).
                Vector2 p=cloudOffsets[i]-new Vector2(delta.x*.06f,delta.y*.025f);
                clouds[i].transform.position=Snap(camera+new Vector3(Wrap(p.x,56),Wrap(p.y,30),0));
            }
            for(int i=0;i<birds.Length;i++)
            {
                Vector2 p=birdOffsets[i]-new Vector2(delta.x*.08f,delta.y*.04f);
                birds[i].transform.position=Snap(camera+new Vector3(Wrap(p.x,60),Wrap(p.y,36),0));
            }
            float h=view.orthographicSize*64,w=h*view.aspect;
            for(int i=0;i<flecks.Length;i++)
            {
                var f=flecks[i];float drift=windPixelsPerSecond*f.speed*(.5f+f.depth);
                float x=Mathf.Repeat(f.x*(w+48)+seconds*drift+Mathf.Sin(seconds*.8f+f.phase)*13,w+48)-24;
                float y=Mathf.Repeat(f.y*(h+48)-seconds*(5+f.depth*7)+Mathf.Sin(seconds*1.5f+f.phase)*10,h+48)-24;
                // Torn asymmetric pentagons turn edge-on, then open again in a common gust.
                float angle=f.angle+seconds*(.5f+f.depth*.4f)+Mathf.Sin(seconds*.65f+f.phase)*.55f;
                float width=f.size*(.25f+.75f*Mathf.Abs(Mathf.Sin(seconds*2.3f+f.phase))),height=f.size*.55f;
                var right=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var up=new Vector2(-right.y,right.x);
                float edge=Mathf.Min(Mathf.Min(x+24,w+24-x),Mathf.Min(y+24,h+24-y));
                float alpha=ashEnabled?Mathf.Clamp01(edge/24)*(.28f+f.depth*.25f):0;
                // Reduce overlap around the player/camera focal area while keeping the full-screen field.
                float center=Vector2.Distance(new Vector2(x,y),new Vector2(w*.45f,h*.30f));alpha*=Mathf.Lerp(.3f,1,Mathf.Clamp01(center/75));
                Color tint=AshTint(i);
                for(int j=0;j<5;j++)
                {
                    Vector2 q=new Vector2(x,y)+right*(TornShape[j].x*width*.5f)+up*(TornShape[j].y*height*.5f);
                    vertices[i*5+j]=Snap(new Vector3(camera.x+(q.x-w*.5f)/32,camera.y+(q.y-h*.5f)/32,0));
                    colors[i*5+j]=new Color(tint.r,tint.g,tint.b,alpha*(j==2?.65f:1));
                }
            }
            ashMesh.vertices=vertices;ashMesh.colors=colors;ashMesh.triangles=triangles;ashMesh.RecalculateBounds();
        }
        void OnDestroy(){if(ashMesh)Destroy(ashMesh);if(ashMaterial)Destroy(ashMaterial);}
    }
}
