using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Deviant.Combat
{
    // World-space, vertex-coloured presentation geometry. Never owns a collider.
    internal sealed class CombatFeelStroke : IDisposable
    {
        readonly GameObject root;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        public CombatFeelStroke(string name,SpriteRenderer source,int order)
        {
            root=new GameObject(name);mesh=new Mesh{name=name};mesh.MarkDynamic();root.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=root.AddComponent<MeshRenderer>();renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=order;
            renderer.sharedMaterial=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            var properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",Texture2D.whiteTexture);renderer.SetPropertyBlock(properties);
        }
        public void Clear(){vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();}
        public void Stroke(IReadOnlyList<Vector3> path,float width,Color color,bool torn=false,bool leading=false)
        {
            int start=vertices.Count;
            for(int i=0;i<path.Count;i++) {
                Vector3 tangent=path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)];
                var normal=new Vector3(-tangent.y,tangent.x).normalized;
                float t=i/(float)(path.Count-1);
                float taper=leading?Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.65f)*(.08f+.92f*t):(.12f+.88f*Mathf.Sin(t*Mathf.PI));
                float radius=width*.5f*taper;
                float inner=torn&&i%9==4?.28f:1;
                vertices.Add(Snap(path[i]-normal*radius*inner));vertices.Add(Snap(path[i]+normal*radius));
                colors.Add(color);colors.Add(color);uv.Add(Vector2.zero);uv.Add(Vector2.zero);
                if(i==0)continue;
                int a=start+(i-1)*2,b=start+i*2;
                triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
            }
        }
        public void Commit()
        {
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            renderer.enabled=triangles.Count>0;
        }
        static Vector3 Snap(Vector3 p)=>new Vector3(Mathf.Round(p.x*32)/32,Mathf.Round(p.y*32)/32,p.z);
        public void Dispose()
        {
            if(renderer)renderer.enabled=false;
            if(Application.isPlaying){if(root)Object.Destroy(root);if(mesh)Object.Destroy(mesh);}
            else{if(root)Object.DestroyImmediate(root);if(mesh)Object.DestroyImmediate(mesh);}
        }
    }
    internal sealed class WhipSkillSweep : IDisposable
    {
        readonly CombatFeelStroke stroke;
        readonly WhipTrailProfile profile;
        readonly Vector3 center;
        readonly float radius,height;
        readonly int facing;
        readonly Vector3[] curve=new Vector3[65];
        public WhipSkillSweep(SpriteRenderer source,Bounds bounds,Vector2 player,int facing)
        {
            profile=WhipTrailProfile.Load();this.facing=facing;
            float end=facing>0?bounds.max.x:bounds.min.x;
            radius=Mathf.Abs(end-player.x)*.5f;
            center=new Vector3((end+player.x)*.5f,bounds.center.y,0);
            height=Mathf.Max(bounds.extents.y,profile.skillHeight);
            stroke=new CombatFeelStroke("Whip skill wrapping sweep",source,source.sortingOrder+2);
        }
        public void Sample(float age)
        {
            stroke.Clear();float fade=Mathf.Clamp01((profile.skillSeconds-age)/.16f);
            for(int layer=1;layer>=0;layer--) {
                float time=age-layer*.018f;if(time<0)continue;
                float progress=Mathf.Clamp01(time/.14f),head=Mathf.Lerp(150,-185,progress);
                float tail=head+Mathf.Lerp(35,175,progress),scale=1-layer*.13f;
                for(int i=0;i<curve.Length;i++) {
                    float t=i/(float)(curve.Length-1),a=Mathf.Lerp(tail,head,t)*Mathf.Deg2Rad;
                    curve[i]=center+new Vector3(Mathf.Cos(a)*radius*facing*scale,Mathf.Sin(a)*height*scale,0);
                }
                float opacity=fade*(layer==0?1:.3f),width=profile.skillWidth*(layer==0?1:.55f);
                stroke.Stroke(curve,width+.1f,new Color(.12f,.26f,.29f,opacity*.55f),true);
                stroke.Stroke(curve,width,new Color(.85f,.94f,1,opacity*.55f),true);
                stroke.Stroke(curve,width*.62f,new Color(1,1,1,opacity),true);
            }
            stroke.Commit();
        }
        public void Dispose()=>stroke.Dispose();
    }
    internal sealed class HeavyCleaveSweep : IDisposable
    {
        readonly CombatFeelStroke stroke;
        readonly Vector3 origin;
        readonly float reach,height;
        readonly int facing;
        readonly SwordRibbonProfile profile;
        readonly Vector3[] path=new Vector3[65];
        public HeavyCleaveSweep(SpriteRenderer source,Bounds bounds,Vector2 player,int facing)
        {
            profile=SwordRibbonProfile.Load();this.facing=facing;origin=new Vector3((player.x+bounds.center.x)*.5f,bounds.center.y,0);
            reach=Mathf.Max(1,Mathf.Abs((facing>0?bounds.max.x:bounds.min.x)-origin.x));
            height=Mathf.Max(1.5f,reach*profile.skillSweepHeight);
            stroke=new CombatFeelStroke("Heavy cleave wide sweep",source,source.sortingOrder+2);
        }
        public void Sample(float age)
        {
            stroke.Clear();float alpha=age<.065f?1:Mathf.Pow(Mathf.Clamp01((profile.skillSweepSeconds-age)/.095f),2);
            // Fast leading edge, then a short cut-off tail on the existing action clock.
            for(int layer=1;layer>=0;layer--) {
                float local=age-layer*.012f;if(local<0)continue;
                float progress=Mathf.Clamp01(local/profile.skillSweepMotionSeconds);
                float snap=1-Mathf.Pow(1-progress,3);
                float head=Mathf.Lerp(155,-155,snap);
                float tail=head+profile.skillSweepAngle*Mathf.Lerp(.18f,1,snap)*Mathf.Lerp(1,.45f,Mathf.Clamp01((local-profile.skillSweepMotionSeconds)/.085f));
                float radius=1-layer*.15f,opacity=alpha*(layer==0?1:.3f);
                FillArc(tail,head,radius);
                float width=profile.skillSweepWidth*(layer==0?1:.5f);
                stroke.Stroke(path,width+.14f,new Color(.12f,.23f,.25f,opacity*.48f),true,true);
                stroke.Stroke(path,width,new Color(.85f,.94f,1,opacity*.55f),true,true);
                stroke.Stroke(path,width*.72f,new Color(1,1,1,opacity),true,true);
            }
            stroke.Commit();
        }
        void FillArc(float tail,float head,float radius)
        {
            for(int i=0;i<path.Length;i++) {
                float t=i/(float)(path.Length-1),a=Mathf.Lerp(tail,head,t)*Mathf.Deg2Rad;
                path[i]=origin+new Vector3(Mathf.Cos(a)*reach*facing*radius,Mathf.Sin(a)*height*radius,0);
            }
        }
        public void Dispose()=>stroke.Dispose();
    }
}
