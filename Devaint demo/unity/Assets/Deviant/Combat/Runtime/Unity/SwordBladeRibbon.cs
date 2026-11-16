using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Deviant.Combat
{
    [Serializable] public sealed class SwordBladePose
    {
        public string frame;
        public float[] hilt,tip;
        public bool emit;
        public int sweepDirection;
        public static Vector3 Local(Sprite sprite,float[] topLeft,bool mirrored)
        {
            var result=new Vector3(topLeft[0]-sprite.pivot.x,sprite.rect.height-topLeft[1]-sprite.pivot.y,0)/sprite.pixelsPerUnit;
            if(mirrored)result.x=-result.x;return result;
        }
    }
    [Serializable] public sealed class SwordRibbonProfile
    {
        public int schema,maxSamples;
        public float basicTailSeconds,heavyTailSeconds,basicBladeWidth,heavyBladeWidth,angularStepDegrees;
        public float skillReleaseDelay,skillReleaseSeconds,skillReleaseWidth;
        public float skillSweepMotionSeconds;
        public float skillSweepSeconds,skillSweepWidth,skillSweepAngle,skillSweepHeight;
        public SwordBladePose[] poses;
        static SwordRibbonProfile loaded;
        public static SwordRibbonProfile Load()
        {
            if(loaded!=null)return loaded;
            var source=Resources.Load<TextAsset>("SwordRibbon");if(!source)throw new InvalidOperationException("Export the sword ribbon blade anchors.");
            var result=JsonUtility.FromJson<SwordRibbonProfile>(source.text);
            if(result==null||result.schema!=1||result.maxSamples<8||result.maxSamples>128||
                !In(result.basicTailSeconds,.02f,.2f)||!In(result.heavyTailSeconds,.02f,.25f)||
                !In(result.basicBladeWidth,.1f,1)||!In(result.heavyBladeWidth,.1f,1)||!In(result.angularStepDegrees,2,15)||!In(result.skillSweepSeconds,.1f,.5f)||!In(result.skillSweepMotionSeconds,.025f,.15f)||!In(result.skillSweepWidth,.5f,2)||!In(result.skillSweepAngle,90,300)||!In(result.skillSweepHeight,.5f,1)||!In(result.skillReleaseDelay,0,.15f)||!In(result.skillReleaseSeconds,.05f,.4f)||!In(result.skillReleaseWidth,.1f,1)||result.poses==null)
                throw new ArgumentException("Invalid sword ribbon profile.");
            var unique=new HashSet<string>();
            foreach(var pose in result.poses)if(string.IsNullOrEmpty(pose.frame)||!unique.Add(pose.frame)||!Point(pose.hilt)||!Point(pose.tip)||pose.sweepDirection<-1||pose.sweepDirection>1)
                throw new ArgumentException("Every blade pose needs unique frame identity and finite pixel anchors.");
            return loaded=result;
        }
        static bool In(float n,float lo,float hi)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=lo&&n<=hi;
        static bool Point(float[] p)=>p!=null&&p.Length==2&&In(p[0],0,256)&&In(p[1],0,256);
        public SwordBladePose Find(string key)
        {
            int slash=key.LastIndexOf('/');string name=slash<0?key:key.Substring(slash+1);
            foreach(var pose in poses)if(pose.frame==name)return pose;return null;
        }
    }
    sealed class SwordRibbonFactory : ICueViewFactory
    {
        readonly CharacterPresentationBinding binding;
        readonly AttackMotionSnapshot motion;
        readonly string id,version;
        public SwordRibbonFactory(CharacterPresentationBinding binding,AttackMotionSnapshot motion,ICueDefinition definition)
        {this.binding=binding;this.motion=motion;id=definition.Id;version=definition.ContentHash;}
        public IDisposable Begin(ICueDefinition requested,CueAnchor anchor,Action<CueViewResult> ready)
        {
            if(requested.Id!=id||requested.ContentHash!=version){ready(CueViewResult.Failure("The blade ribbon requires its captured cue version."));return null;}
            ready(CueViewResult.Ready(new SwordRibbonView(binding,motion,false,id)));return null;
        }
    }
    // Uses the same captured native body frame, foot pivot, facing and clock as the actor.
    // Each strip joins the previous blade pose to the current pose; no fixed crescent is emitted.
    internal sealed class SwordRibbonView : ICueView
    {
        struct Pair {public Vector3 hilt,tip;public float at;}
        readonly CharacterPresentationBinding binding;
        readonly AttackMotionSnapshot motion;
        readonly SwordRibbonProfile profile;
        readonly bool heavy;
        readonly GameObject root;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly List<Pair> history=new List<Pair>();
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> indices=new List<int>();
        float lastTime=-1;
        int lastFrame=-1;
        bool suppressed,disposed;
        public static bool Supports(AttackMotionSnapshot motion)
        {
            if(motion==null||motion.Form!="base_sword")return false;
            var profile=SwordRibbonProfile.Load();
            for(int i=0;i<motion.Timing.Frames.Count;i++) {
                string frame=motion.Timing.Frames[i];var sprite=motion.FrameSprite(i);
                if(!frame.StartsWith("CharacterApproved11/base_sword/",StringComparison.Ordinal)||profile.Find(frame)==null||!sprite||sprite.rect.width!=160||sprite.rect.height!=160)return false;
            }
            return true;
        }
        internal static Vector3 SwingDirection(AttackMotionSnapshot motion,int facing)
        {
            var profile=SwordRibbonProfile.Load();
            for(int i=1;i<motion.Timing.Frames.Count;i++) {
                var pose=profile.Find(motion.Timing.Frames[i]);if(!pose.emit)continue;
                int prior=i-1;while(prior>0&&profile.Find(motion.Timing.Frames[prior]).emit)prior--;
                var from=profile.Find(motion.Timing.Frames[prior]);
                return SwordBladePose.Local(motion.FrameSprite(i),pose.tip,facing<0)-SwordBladePose.Local(motion.FrameSprite(prior),from.tip,facing<0);
            }
            return Vector3.zero;
        }
        internal SwordRibbonView(CharacterPresentationBinding binding,AttackMotionSnapshot motion,bool heavy,string name)
        {
            this.binding=binding;this.motion=motion;this.heavy=heavy;profile=SwordRibbonProfile.Load();
            root=new GameObject(name);root.AddComponent<MeshFilter>().sharedMesh=mesh=new Mesh{name="Continuous blade sweep"};mesh.MarkDynamic();
            renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            var properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",Texture2D.whiteTexture);renderer.SetPropertyBlock(properties);
            renderer.sortingLayerID=binding.Output.sortingLayerID;renderer.sortingOrder=binding.Output.sortingOrder-1;
            renderer.enabled=false;
        }
        Pair WorldPair(SwordBladePose pose,Sprite sprite,bool flip,float at)
        {
            var target=binding.Output.transform;
            return new Pair{hilt=target.TransformPoint(SwordBladePose.Local(sprite,pose.hilt,flip)),tip=target.TransformPoint(SwordBladePose.Local(sprite,pose.tip,flip)),at=at};
        }
        public void Sample(float elapsed)
        {
            if(disposed)return;
            if(!root||!binding.Output)throw new InvalidOperationException("The captured blade ribbon output was destroyed.");
            if(!binding.UseContinuousSwordTrail){suppressed=true;history.Clear();mesh.Clear();renderer.enabled=false;return;}
            if(suppressed||!binding.TryRead(out var actor))return;
            float now=actor.Now;
            if(now<lastTime){history.Clear();lastTime=-1;lastFrame=-1;}
            float age=Mathf.Max(0,now-motion.StartedAt);int frame=motion.Timing.FrameAt(age);
            var pose=profile.Find(motion.Timing.Frames[frame]);var sprite=motion.At(age);
            float tail=heavy?profile.heavyTailSeconds:profile.basicTailSeconds;
            float sampleAt=now;
            // A first draw may skip a very short contact pose. Reconstruct only
            // its still-live tail using the captured action's frame boundaries.
            if(history.Count==0&&pose!=null&&!pose.emit) {
                int prior=frame-1;
                while(prior>=0&&!profile.Find(motion.Timing.Frames[prior]).emit)prior--;
                if(prior>=0) {
                    float sourceEnd=0;for(int i=0;i<=prior;i++)sourceEnd+=motion.Timing.Seconds[i];
                    float lag=Mathf.Max(0,age-motion.Timing.ActionTime(sourceEnd));
                    if(lag<tail-.015f){frame=prior;pose=profile.Find(motion.Timing.Frames[prior]);sprite=motion.FrameSprite(prior);sampleAt=now-lag;}
                }
            }
            bool same=actor.Presentation.HasAction&&actor.Action.ExecutionId==motion.ExecutionId;
            if(now>lastTime&&same&&pose!=null&&pose.emit&&sprite) {
                var current=WorldPair(pose,sprite,actor.Kinematics.Facing<0,sampleAt);
                if(history.Count==0&&frame>0) {
                    int windup=frame-1;
                    while(windup>0&&profile.Find(motion.Timing.Frames[windup]).emit)windup--;
                    var priorPose=profile.Find(motion.Timing.Frames[windup]);
                    // The first active native frame follows the windup. Seed that real blade pose.
                    if(priorPose!=null) {
                        var priorSprite=motion.FrameSprite(windup);
                        if(priorSprite)history.Add(WorldPair(priorPose,priorSprite,actor.Kinematics.Facing<0,sampleAt-.04f));
                    }
                }
                var previousTip=history.Count>0?history[history.Count-1].tip:current.tip;
                if(history.Count==0)history.Add(current);
                else Append(current,lastFrame!=frame?pose.sweepDirection*(actor.Kinematics.Facing<0?-1:1):0);
                binding.FeelFeedback?.ObserveBladeSweep(motion.ExecutionId,current.tip-previousTip);
            }
            lastTime=now;lastFrame=frame;
            while(history.Count>1&&now-history[0].at>tail)history.RemoveAt(0);
            while(history.Count>profile.maxSamples)history.RemoveAt(0);
            Draw(now,tail);
        }
        void Append(Pair end,int winding)
        {
            var start=history[history.Count-1];var a=start.tip-start.hilt;var b=end.tip-end.hilt;
            float angleA=Mathf.Atan2(a.y,a.x)*Mathf.Rad2Deg,angleB=Mathf.Atan2(b.y,b.x)*Mathf.Rad2Deg;
            float delta=Mathf.DeltaAngle(angleA,angleB);
            // Fast native frames can cross more than half a turn. Preserve their
            // authored front-facing swing instead of cutting behind the actor.
            if(winding>0&&delta<0)delta+=360;
            if(winding<0&&delta>0)delta-=360;
            int count=Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta)/profile.angularStepDegrees),1,32);
            for(int i=1;i<=count;i++) {
                float t=i/(float)count,angle=(angleA+delta*t)*Mathf.Deg2Rad;
                var hilt=Vector3.Lerp(start.hilt,end.hilt,t);
                float length=Mathf.Lerp(a.magnitude,b.magnitude,t);
                history.Add(new Pair{hilt=hilt,tip=hilt+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*length,at=Mathf.Lerp(Mathf.Max(start.at,end.at-.045f),end.at,t)});
            }
        }
        void Draw(float now,float tail)
        {
            // Pixel effects redraw the same real blade samples as a tip crescent on the pixel grid.
            var feel=binding.FeelFeedback;
            if(feel!=null&&feel.UsePixelEffects) {
                var samples=new List<CombatFeelPixelFx.TrailSample>(history.Count);
                foreach(var pair in history)samples.Add(new CombatFeelPixelFx.TrailSample(pair.hilt,pair.tip,pair.at));
                feel.PixelFx.QueueTrail(samples,now,tail);renderer.enabled=false;return;
            }
            vertices.Clear();colors.Clear();uv.Clear();indices.Clear();
            float width=heavy?profile.heavyBladeWidth:profile.basicBladeWidth;
            for(int i=0;i<history.Count;i++) {
                var pair=history[i];float remaining=Mathf.Clamp01(1-(now-pair.at)/tail);
                float taper=remaining*remaining;
                var ray=pair.tip-pair.hilt;var tip=pair.tip;
                // Old heavy samples flare outward; the newest edge remains on the actual blade tip.
                if(heavy)tip+=ray*(.22f*(1-remaining));
                var inner=Vector3.Lerp(tip,pair.hilt,width*taper);
                var rim=tip+ray.normalized*.025f;
                vertices.Add(Snap(inner));vertices.Add(Snap(tip-ray.normalized*.035f));vertices.Add(Snap(rim));
                colors.Add(new Color(1,1,1,taper*(heavy?.38f:.24f)));colors.Add(new Color(1,1,1,taper));colors.Add(new Color(.18f,.21f,.22f,taper*.48f));
                uv.Add(Vector2.zero);uv.Add(Vector2.zero);uv.Add(Vector2.zero);
                if(i==0)continue;
                int p=(i-1)*3,n=i*3;
                for(int j=0;j<2;j++){indices.Add(p+j);indices.Add(n+j);indices.Add(p+j+1);indices.Add(p+j+1);indices.Add(n+j);indices.Add(n+j+1);}
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            renderer.enabled=history.Count>1&&indices.Count>0;
        }
        static Vector3 Snap(Vector3 p)=>new Vector3(Mathf.Round(p.x*32)/32,Mathf.Round(p.y*32)/32,p.z);
        public void Dispose()
        {
            if(disposed)return;disposed=true;if(renderer)renderer.enabled=false;
            if(Application.isPlaying){if(root)Object.Destroy(root);if(mesh)Object.Destroy(mesh);}
            else{if(root)Object.DestroyImmediate(root);if(mesh)Object.DestroyImmediate(mesh);}
            history.Clear();
        }
    }
}
