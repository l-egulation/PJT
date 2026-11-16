using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable] internal sealed class WhipPose {public string frame;public float[] points;}
    [Serializable] internal sealed class WhipTrailProfile
    {
        public float skillSeconds,skillWidth,skillHeight;
        public int schema;public float coreWidth,bandWidth,tailSeconds;public WhipPose[] poses;
        static WhipTrailProfile loaded;
        public static WhipTrailProfile Load()=>loaded??(loaded=JsonUtility.FromJson<WhipTrailProfile>(Resources.Load<TextAsset>("WhipTrail").text));
        public WhipPose Find(string key)=>Array.Find(poses,p=>key.EndsWith("/"+p.frame,StringComparison.Ordinal));
    }
    sealed class WhipTrailFactory : ICueViewFactory
    {
        readonly CharacterPresentationBinding binding;readonly AttackMotionSnapshot motion;readonly string id,version;
        public WhipTrailFactory(CharacterPresentationBinding binding,AttackMotionSnapshot motion,ICueDefinition definition)
        {this.binding=binding;this.motion=motion;id=definition.Id;version=definition.ContentHash;}
        public IDisposable Begin(ICueDefinition requested,CueAnchor anchor,Action<CueViewResult> ready)
        {
            if(requested.Id!=id||requested.ContentHash!=version){ready(CueViewResult.Failure("Whip cue version changed."));return null;}
            ready(CueViewResult.Ready(new WhipTrailView(binding,motion,id)));return null;
        }
    }
    internal sealed class WhipTrailView : ICueView
    {
        readonly CharacterPresentationBinding binding;readonly AttackMotionSnapshot motion;
        readonly WhipTrailProfile profile;readonly CombatFeelStroke stroke;
        readonly List<Vector3> curve=new List<Vector3>();
        Vector3[] previous;float previousAt,lastTime=-1;int lastFrame=-1;bool disposed,suppressed;
        public static bool Supports(AttackMotionSnapshot motion)
        {
            if(motion==null||motion.Form!="whip")return false;
            foreach(var key in motion.Timing.Frames)if(!key.StartsWith("CharacterApproved11/whip/",StringComparison.Ordinal)||WhipTrailProfile.Load().Find(key)==null)return false;
            return motion.FrameSprite(0)&&motion.FrameSprite(0).rect.width==256;
        }
        public WhipTrailView(CharacterPresentationBinding binding,AttackMotionSnapshot motion,string name)
        {this.binding=binding;this.motion=motion;profile=WhipTrailProfile.Load();stroke=new CombatFeelStroke(name,binding.Output,binding.Output.sortingOrder+1);}
        public void Sample(float elapsed)
        {
            if(disposed)return;
            stroke.Clear();
            if(!binding.FeelFeedback.UseWhipTrail){suppressed=true;previous=null;stroke.Commit();return;}
            if(suppressed||!binding.TryRead(out var actor)){stroke.Commit();return;}
            float now=actor.Now,age=Mathf.Max(0,now-motion.StartedAt);int frame=motion.Timing.FrameAt(age);
            if(!actor.Presentation.HasAction||actor.Action.ExecutionId!=motion.ExecutionId){previous=null;stroke.Commit();return;}
            if(frame!=lastFrame&&curve.Count>0&&now>lastTime){previous=curve.ToArray();previousAt=now;}
            curve.Clear();var pose=profile.Find(motion.Timing.Frames[frame]);var sprite=motion.FrameSprite(frame);
            var anchors=new List<Vector3>();
            for(int i=0;i<pose.points.Length;i+=2)anchors.Add(binding.Output.transform.TransformPoint(SwordBladePose.Local(sprite,new[]{pose.points[i],pose.points[i+1]},actor.Kinematics.Facing<0)));
            for(int i=0;i<anchors.Count-1;i++)for(int j=0;j<6;j++) {
                float t=j/6f;var a=anchors[Mathf.Max(0,i-1)];var b=anchors[i];var c=anchors[i+1];var d=anchors[Mathf.Min(anchors.Count-1,i+2)];
                curve.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            if(anchors.Count>0)curve.Add(anchors[anchors.Count-1]);
            if(previous!=null&&now-previousAt<profile.tailSeconds)stroke.Stroke(previous,profile.bandWidth,new Color(1,1,1,.22f*(1-(now-previousAt)/profile.tailSeconds)));
            if(curve.Count>1){stroke.Stroke(curve,profile.bandWidth,new Color(.95f,.94f,.83f,.45f));stroke.Stroke(curve,profile.coreWidth,Color.white);}
            stroke.Commit();lastTime=now;lastFrame=frame;
        }
        public void Dispose(){if(disposed)return;disposed=true;stroke.Dispose();}
    }
}
