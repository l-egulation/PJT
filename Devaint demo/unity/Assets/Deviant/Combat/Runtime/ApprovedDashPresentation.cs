using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Reads the existing dash deadlines only. No movement, input or combat writes.
    public sealed class ApprovedDashPresentation : IDisposable
    {
        [Serializable] public sealed class Recipe
        {
            public float movementDurationMs,recoveryAfterMovementMs,afterimageLifeMs;
            public float afterimageMaximumOpacity,afterimageFadeExponent,afterimageSpawnIntervalMs;
            public float[] afterPoseEndTimesMs;
            public string afterimageColor;
        }
        struct Echo { public SpriteRenderer renderer; public float born; }
        readonly Recipe recipe;
        readonly List<Echo> echoes=new List<Echo>();
        readonly Color ink;
        float observedStart=float.NaN,originalEnd,nextEcho,lastNow=-1;
        Vector3 lastEchoPosition;
        bool hasEchoPosition;
        bool cancelled;
        string form;
        CharacterClip dashClip;
        Sprite airbornePose;
        bool startedAirborne, plain;
        public int ActiveAfterimages => echoes.Count;
        public ApprovedDashPresentation()
        {
            var asset=Resources.Load<TextAsset>("CharacterV2/ApprovedDashPresentation");
            if(!asset)return;
            recipe=JsonUtility.FromJson<Recipe>(asset.text);
            if(!ColorUtility.TryParseHtmlString(recipe.afterimageColor,out ink))throw new InvalidOperationException("Invalid approved dash ink.");
        }
        public static int PoseIndex(Recipe source,float now,float start,float end)
        {
            float ms=now<end?(now-start)/Mathf.Max(.001f,end-start)*source.movementDurationMs:
                source.movementDurationMs+(now-end)*1000;
            for(int i=0;i<source.afterPoseEndTimesMs.Length;i++)if(ms<source.afterPoseEndTimesMs[i])return i;
            return -1;
        }
        static bool PosesFit(int[] poses,int frames)
        {
            if(poses==null||poses.Length!=5)return false;
            foreach(int pose in poses)if(pose<0||pose>=frames)return false;
            return true;
        }
        public Sprite Pose(in ActorReadSnapshot actor,CharacterAnimationSet set,string state)
        {
            if(recipe==null)return null;
            if(actor.Now<lastNow)Reset();
            lastNow=actor.Now;
            bool active=actor.Now<actor.Dash.EndsAt;
            if(active&&actor.Dash.StartedAt!=observedStart)
            {
                Reset();observedStart=actor.Dash.StartedAt;originalEnd=actor.Dash.EndsAt;nextEcho=actor.Now;
                form=set?.form;plain=set!=null&&set.plainAfterimage;var clip=set?.Find("dash");
                startedAirborne=!actor.Kinematics.Grounded;
                // Ground dash poses compress the legs. Keep the approved airborne
                // silhouette during an air dash instead of briefly shrinking into that crouch.
                airbornePose=startedAirborne ? set?.Find("jump_apex")?.At(0,false) : null;
                // The approved five-pose recipe never remaps a different clip shape on its own. A body with a longer dash
                // (village, opening) names its five poses in dashPoses, so it dashes and air-dashes like the weapons.
                if(clip?.sprites?.Length==5)dashClip=new CharacterClip{sprites=(Sprite[])clip.sprites.Clone()};
                else if(clip?.sprites!=null&&PosesFit(set.dashPoses,clip.sprites.Length))
                {
                    var poses=new Sprite[5];for(int i=0;i<5;i++)poses[i]=clip.sprites[set.dashPoses[i]];
                    dashClip=new CharacterClip{sprites=poses};
                }
            }
            if(actor.Vitals.Dead||actor.Kinematics.WallHit||actor.Dash.EndsAt<originalEnd||form!=set?.form||actor.Presentation.HasAction||actor.Presentation.Approaching)
            { cancelled=true;ClearEchoes();return null; }
            // A dash can leave a ledge after starting on the ground. Drop its
            // ground-crouch trail and use the same silhouette as an air-start dash.
            if(active&&!startedAirborne&&!actor.Kinematics.Grounded)
            {
                startedAirborne=true;airbornePose=set?.Find("jump_apex")?.At(0,false);
                ClearEchoes();
            }
            if(cancelled||dashClip==null)return null;
            if(!active&&(startedAirborne||!actor.Kinematics.Grounded||state!="idle"||actor.Now>=actor.Dash.EndsAt+recipe.recoveryAfterMovementMs/1000))
            { dashClip=null;return null; }
            int index=PoseIndex(recipe,actor.Now,observedStart,actor.Dash.EndsAt);
            return index>=0?(airbornePose ? airbornePose : dashClip.sprites[index]):null;
        }
        public void Draw(in ActorReadSnapshot actor,SpriteRenderer source)
        {
            if(recipe==null)return;
            if(!source||!source.enabled||!source.gameObject.activeInHierarchy||actor.Vitals.Dead)
            { Reset();return; }
            // The body's recovery deadline also bounds the trail. Otherwise a
            // late dash echo can hang at the old height while the body is falling.
            float recovery=recipe.recoveryAfterMovementMs/1000;
            float tail=actor.Now<actor.Dash.EndsAt?1:recovery>0?Mathf.Clamp01(1-(actor.Now-actor.Dash.EndsAt)/recovery):0;
            for(int i=echoes.Count-1;i>=0;i--)
            {
                var echo=echoes[i];float life=recipe.afterimageLifeMs/1000;
                float age=actor.Now-echo.born;
                if(!echo.renderer||age>=life||age<0||tail<=0){Destroy(echo.renderer);echoes.RemoveAt(i);continue;}
                var color=Tint;color.a=recipe.afterimageMaximumOpacity*Mathf.Pow(1-age/life,recipe.afterimageFadeExponent)*tail;
                echo.renderer.color=color;
            }
            if(cancelled||actor.Now>=actor.Dash.EndsAt||actor.Now<nextEcho||!source.sprite)return;
            // Dash movement eases out, but the old time-only emitter kept
            // stacking almost identical bodies at the stop. Require real travel.
            var scale=source.transform.lossyScale;
            float spacing=2/Mathf.Max(1,source.sprite.pixelsPerUnit)*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y));
            if(hasEchoPosition&&(source.transform.position-lastEchoPosition).sqrMagnitude<spacing*spacing)return;
            var node=new GameObject("Approved dash afterimage",typeof(SpriteRenderer));
            node.hideFlags=HideFlags.DontSave;
            node.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            node.transform.localScale=source.transform.lossyScale;
            var drawing=node.GetComponent<SpriteRenderer>();drawing.sprite=source.sprite;
            drawing.sharedMaterial=source.sharedMaterial;drawing.flipX=source.flipX;drawing.flipY=source.flipY;
            drawing.sortingLayerID=source.sortingLayerID;drawing.sortingOrder=source.sortingOrder-1;
            var initial=Tint;initial.a=recipe.afterimageMaximumOpacity;drawing.color=initial;
            echoes.Add(new Echo{renderer=drawing,born=actor.Now});
            lastEchoPosition=source.transform.position;hasEchoPosition=true;
            // Never backfill missed emissions at a single position after a slow frame.
            float interval=Mathf.Max(.001f,recipe.afterimageSpawnIntervalMs/1000);
            nextEcho+=(Mathf.Floor((actor.Now-nextEcho)/interval)+1)*interval;
        }
        static void Destroy(SpriteRenderer renderer)
        {
            if(!renderer)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(renderer.gameObject);
            else UnityEngine.Object.DestroyImmediate(renderer.gameObject);
        }
        // Ink for the ink body; the body's own colours (untinted) for a set that is not ink yet.
        Color Tint=>plain?Color.white:ink;
        void ClearEchoes(){foreach(var echo in echoes)Destroy(echo.renderer);echoes.Clear();hasEchoPosition=false;}
        public void Reset(){ClearEchoes();observedStart=float.NaN;dashClip=null;airbornePose=null;startedAirborne=false;form=null;cancelled=false;plain=false;lastNow=-1;}
        public void Dispose()=>Reset();
    }
}
