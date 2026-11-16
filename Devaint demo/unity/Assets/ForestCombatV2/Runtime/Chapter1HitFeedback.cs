using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    // Presentation only. CombatActor owns damage, stagger, collision and world knockback.
    public sealed class Chapter1HitFeedback : IDisposable
    {
        readonly SpriteRenderer visual, flash;
        readonly ParticleSystem particles;
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        readonly GameObject effects;
        readonly BasicSwordHitFeedback sword;
        readonly CombatActor actor;
        readonly CombatAcceptedHit[] accepted=new CombatAcceptedHit[32];
        readonly Dictionary<int,CombatAcceptedHit> pending=new Dictionary<int,CombatAcceptedHit>();
        CombatOutcomeCursor cursor;
        public BasicSwordHitFeedback Sword => sword;
        bool paused,disposed,swordFlash,externalVisuals;
        public bool VisualsEnabled { get; set; } = true;
        static readonly int ColorId=Shader.PropertyToID("_Color");

        public Chapter1HitFeedback(SpriteRenderer target)
        {
            visual=target;
            sword=new BasicSwordHitFeedback(target);
            actor=target.GetComponentInParent<CombatActor>();
            if(actor && actor.arena && actor.arena.HitEvents.TryGetCursor(out var current))
                cursor=new CombatOutcomeCursor(current.Session,0);
            var material=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            effects=new GameObject("Chapter1 hit feedback");effects.transform.SetParent(target.transform.parent,false);
            var overlay=new GameObject("White hit flash");overlay.transform.SetParent(target.transform,false);
            flash=overlay.AddComponent<SpriteRenderer>();flash.sharedMaterial=material;
            flash.sortingLayerID=target.sortingLayerID;flash.sortingOrder=target.sortingOrder+1;flash.enabled=false;
            var emitter=new GameObject("World hit particles");emitter.transform.SetParent(effects.transform,false);
            particles=emitter.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;
            main.playOnAwake=false;main.loop=false;main.duration=.4f;main.startLifetime=.38f;
            main.startSpeed=0;main.startSize3D=true;main.maxParticles=128;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=particles.emission;emission.enabled=false;
            var shape=particles.shape;shape.enabled=false;
            var force=particles.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.World;force.y=-310f/64;
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.45f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.sortingLayerID=target.sortingLayerID;renderer.sortingOrder=target.sortingOrder+2;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
        }

        public static bool TryAnchors(string species,out Vector2 idle,out Vector2 hurt)
        {
            switch(species)
            {
                case "01_field_mouse":idle=new Vector2(59,120);hurt=new Vector2(58,120);return true;
                case "02_mole":idle=new Vector2(29,118);hurt=new Vector2(54,120);return true;
                case "07_scarecrow":idle=new Vector2(54,120);hurt=new Vector2(53,120);return true;
                case "08_hay_bale":idle=new Vector2(31,120);hurt=new Vector2(32,120);return true;
                case "10_water_bucket":idle=new Vector2(55,119);hurt=new Vector2(49,120);return true;
                default:idle=hurt=Vector2.zero;return false; // Boar retains its original pose.
            }
        }
        public static Vector3 FootLocal(Sprite sprite,Vector2 topLeftPixel,bool flipped)
        {
            return new Vector3((topLeftPixel.x-sprite.pivot.x)*(flipped?-1:1),
                sprite.rect.height-topLeftPixel.y-sprite.pivot.y,0)/sprite.pixelsPerUnit;
        }
        static float Ease(float t) { return 1-Mathf.Pow(1-Mathf.Clamp01(t),3); }
        public static void ApplyPose(SpriteRenderer target,Sprite idleSprite,string species,float age,int direction)
        {
            if(!target.sprite || !idleSprite || !TryAnchors(species,out var idle,out var hurt))return;
            float kick=0;
            if(age>=0 && age<.3f)
                kick=age<.04f?Ease(age/.04f):age<.075f?1:age<.23f?1-Ease((age-.075f)/.155f):-.14f*Mathf.Sin((age-.23f)/.07f*Mathf.PI);
            var rotation=Quaternion.Euler(0,0,-direction*.105f*kick*Mathf.Rad2Deg);
            var scale=new Vector3(1+.055f*kick,1-.04f*kick,1);
            var sourceFoot=FootLocal(target.sprite,hurt,target.flipX);
            var restFoot=FootLocal(idleSprite,idle,target.flipX);
            target.transform.localRotation=rotation;target.transform.localScale=scale;
            target.transform.localPosition=restFoot-rotation*Vector3.Scale(sourceFoot,scale);
        }

        // Opaque bounds of the approved first idle frame, in native top-left pixels.
        // SpriteRenderer.bounds includes transparent padding around small monsters.
        public static Vector3 ImpactLocal(Sprite idle,string species,bool flipped,int direction)
        {
            Vector4 bounds;
            switch(species)
            {
                case "01_field_mouse":bounds=new Vector4(40,86,88,120);break;
                case "02_mole":bounds=new Vector4(22,60,106,120);break;
                case "05_boar":bounds=new Vector4(6,44,118,118);break;
                case "07_scarecrow":bounds=new Vector4(32,67,95,120);break;
                case "08_hay_bale":bounds=new Vector4(17,52,111,120);break;
                case "10_water_bucket":bounds=new Vector4(35,47,92,119);break;
                default:bounds=new Vector4(0,0,idle.rect.width,idle.rect.height);break;
            }
            float inset=Mathf.Min(13,(bounds.z-bounds.x)/4);
            bool right=(direction<0)!=flipped;
            var pixel=new Vector2(right?bounds.z-inset:bounds.x+inset,bounds.y+(bounds.w-bounds.y)*.48f);
            return FootLocal(idle,pixel,flipped);
        }
        public void Hit(Vector3 origin,int serial,bool visualEffects) => Hit(origin,serial,default,0,visualEffects);
        public void Hit(Vector3 origin,int serial,CombatHitOrigin hit=default,float now=0,bool visualEffects=true)
        {
            if(disposed || !visual)return;
            externalVisuals=!visualEffects;
            // The approved sword contact is an independent layer. Main's generic
            // flash replacement and the prefab VFX must not suppress its frames.
            bool hasCommitted=TryAccepted(serial,out var committed);
            if(!hit.IsSpecified && hasCommitted && actor.arena.Player &&
                committed.Origin.Source.ActorId==actor.arena.Player.ActorId && BasicSwordHitFeedback.Matches(committed.Origin))
            {
                hit=committed.Origin;now=committed.Now;
                origin=new Vector3(committed.TargetPose.X+actor.offset.x*committed.TargetPose.Facing-hit.SourceFacing*actor.size.x*.3f,
                    committed.TargetPose.Y+actor.offset.y,0);
            }
            swordFlash=visualEffects && sword.Hit(origin,hit,now);
            if(swordFlash)
            {
                var exit=origin;
                if(actor && actor.hurtCollider)
                {
                    float rootX=hasCommitted?committed.TargetPose.X:actor.Position.x;
                    int facing=hasCommitted?committed.TargetPose.Facing:actor.facing;
                    float offsetX=actor.hurtCollider.offset.x*(facing==actor.facing?1:-1);
                    exit.x=rootX+offsetX+hit.SourceFacing*(actor.hurtCollider.size.x*.5f+BasicSwordHitFeedback.ParticleSettings.exitOffset);
                }
                EmitSwordParticles(exit,serial,hit.SourceFacing);return;
            }
            if(!visualEffects || !VisualsEnabled) return;
            if(!particles.isPlaying)particles.Play();
            for(int i=0;i<16;i++)
            {
                float random=Mathf.Repeat(Mathf.Sin(i*127.1f+serial*311.7f)*43758.5453f,1);
                float angle=i/16f*Mathf.PI*2+serial*.77f,speed=(100+random*125)/64;
                float size=(i%4==0?4:3)/64f;
                var particle=new ParticleSystem.EmitParams {
                    position=origin+new Vector3(Mathf.Cos(angle),-Mathf.Sin(angle)*.8f,0)*(5f/64),
                    velocity=new Vector3(Mathf.Cos(angle)*speed,-Mathf.Sin(angle)*speed*.8f+24f/64,0),
                    startLifetime=.25f+random*.13f,startSize3D=Vector3.one*size,rotation=angle*Mathf.Rad2Deg,
                    startColor=i%3==0?new Color32(255,249,220,255):i%3==1?new Color32(241,204,133,255):new Color32(199,141,79,255)
                };
                particles.Emit(particle,1);
            }
            for(int i=0;i<2;i++)particles.Emit(new ParticleSystem.EmitParams {
                position=origin,velocity=Vector3.zero,startLifetime=.07f,startSize3D=new Vector3(.46f,.06f,.06f),
                rotation=45+i*90,startColor=new Color32(255,246,204,255)},1);
        }
        void EmitSwordParticles(Vector3 origin,int serial,int direction)
        {
            var style=BasicSwordHitFeedback.ParticleSettings;
            if(!particles.isPlaying)particles.Play();
            for(int i=0;i<style.count;i++)
            {
                float random=Mathf.Repeat(Mathf.Sin(i*127.1f+serial*311.7f)*43758.5453f,1);
                float angle=Mathf.Lerp(-style.spreadHalfAngle,style.spreadHalfAngle,(i+random)/style.count)*Mathf.Deg2Rad;
                float speed=Mathf.Lerp(style.minSpeed,style.maxSpeed,random);
                var velocity=new Vector3(direction*Mathf.Cos(angle)*speed,Mathf.Sin(angle)*speed,0);
                bool streak=i%3==0;
                particles.Emit(new ParticleSystem.EmitParams {
                    position=origin,
                    velocity=velocity,
                    startLifetime=Mathf.Lerp(style.minLifetime,style.maxLifetime,random),
                    startSize3D=streak?new Vector3(style.lineLength,style.lineWidth,style.lineWidth):Vector3.one*style.smallSize,
                    rotation=-Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg,
                    startColor=i%3==0?new Color32(18,17,20,255):i%3==1?new Color32(35,32,38,255):new Color32(53,49,57,255)
                },1);
            }
        }
        bool TryAccepted(int serial,out CombatAcceptedHit hit)
        {
            hit=default;if(!actor||!actor.arena)return false;
            while(actor.arena.HitEvents.TryRead(cursor,accepted,out var page))
            {
                cursor=page.Next;
                for(int i=0;i<page.Count;i++)if(accepted[i].Target.ActorId==actor.ActorId && accepted[i].After.ReceivedHits>=serial)
                    pending[accepted[i].After.ReceivedHits]=accepted[i];
                if(page.Count<accepted.Length)break;
            }
            if(!pending.TryGetValue(serial,out hit))return false;
            pending.Remove(serial);return true;
        }
        public void Draw(float age,bool simulationPaused,float now=float.NaN)
        {
            if(disposed || !visual)return;
            if(float.IsNaN(now))now=actor && actor.arena?actor.arena.Now:0;
            sword.Draw(now);
            if(simulationPaused!=paused)
            {
                paused=simulationPaused;
                if(paused) particles.Pause();
                else if(particles.particleCount>0)particles.Play();
            }
            if(externalVisuals || !VisualsEnabled && !swordFlash)
            { flash.enabled=false;if(particles.particleCount>0)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return; }
            float duration=swordFlash?BasicSwordHitFeedback.FlashDuration:.065f;
            float hold=swordFlash?BasicSwordHitFeedback.FlashHold:.028f;
            float fadeAlpha=swordFlash?.92f:.65f;
            float alpha=age<0 || age>=duration?0:age<hold?.92f:fadeAlpha*(1-(age-hold)/(duration-hold));
            flash.enabled=visual.enabled && alpha>0;
            if(!flash.enabled)return;
            flash.sprite=visual.sprite;flash.transform.localScale=new Vector3(visual.flipX?-1:1,1,1);
            properties.SetColor(ColorId,new Color(1,253f/255,229f/255,alpha));flash.SetPropertyBlock(properties);
        }
        public void Clear()
        {
            if(disposed)return;
            sword.Clear();swordFlash=false;externalVisuals=false;
            pending.Clear();if(actor&&actor.arena)actor.arena.HitEvents.TryGetCursor(out cursor);
            if(flash)flash.enabled=false;
            if(particles)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            paused=false;
        }
        public void Dispose()
        {
            if(disposed)return;Clear();disposed=true;
            sword.Dispose();
            if(flash)UnityEngine.Object.Destroy(flash.gameObject);
            if(effects)UnityEngine.Object.Destroy(effects);
        }
    }
}
