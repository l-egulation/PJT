using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable] public sealed class SwordHitParticleSettings
    {
        public int count;
        public float minSpeed,maxSpeed,minLifetime,maxLifetime,smallSize,lineLength,lineWidth,spreadHalfAngle,exitOffset;
    }
    // Presentation only; each target reuses its renderers and samples the arena clock.
    public sealed class BasicSwordHitFeedback : IDisposable
    {
        [Serializable] sealed class Settings { public float flashHold,flashDuration; public float[] frameEnds; public int frameCount; public int pixelsPerUnit; public SwordHitParticleSettings particles; }
        sealed class Slot { public SpriteRenderer renderer; public Vector3 position; public float start; public bool active; }
        static Sprite[] frames;
        static Settings settings;
        public static SwordHitParticleSettings ParticleSettings => Load()?settings.particles:null;
        public static float FlashHold => Load()?settings.flashHold:.06f;
        public static float FlashDuration => Load()?settings.flashDuration:.14f;
        readonly SpriteRenderer target;
        readonly List<Slot> slots=new List<Slot>();
        public int Emissions { get; private set; }
        public int ActiveCount { get { int n=0;foreach(var s in slots)if(s.active)n++;return n; } }
        public bool LastFlipY { get; private set; }
        public bool LastFlipX { get; private set; }
        public Vector3 LastPosition { get; private set; }
        public BasicSwordHitFeedback(SpriteRenderer target) { this.target=target; }
        public static bool Matches(CombatHitOrigin origin) => origin.IsBasicAction && origin.Kind==CombatHitKind.Melee &&
            origin.Reflector.ActorId==0 && (origin.SourceFacing==1 || origin.SourceFacing==-1) &&
            (origin.ActionId=="base_sword_basic_0" || origin.ActionId=="base_sword_basic_1");
        static bool Load()
        {
            if(frames!=null)return true;
            var json=Resources.Load<TextAsset>("BasicSwordHit/settings");if(!json)return false;
            var candidate=JsonUtility.FromJson<Settings>(json.text);
            if(candidate==null||candidate.frameCount!=6||candidate.frameEnds==null||candidate.frameEnds.Length!=6)return false;
            if(candidate.flashHold<=0||candidate.flashDuration<=candidate.flashHold||candidate.flashDuration>=1)return false;
            if(candidate.particles==null||candidate.particles.count<=0||candidate.particles.count>32||
                candidate.particles.spreadHalfAngle<=0||candidate.particles.spreadHalfAngle>=90||candidate.particles.exitOffset<0||
                candidate.particles.minSpeed<=0||candidate.particles.maxSpeed<candidate.particles.minSpeed||
                candidate.particles.minLifetime<=0||candidate.particles.maxLifetime<candidate.particles.minLifetime||candidate.particles.maxLifetime>=1||
                candidate.particles.smallSize<=0||candidate.particles.lineLength<=0||candidate.particles.lineWidth<=0)return false;
            var media=new Sprite[6];float previous=0;
            for(int i=0;i<media.Length;i++)
            {
                media[i]=Resources.Load<Sprite>("BasicSwordHit/"+i.ToString("00"));
                if(!media[i]||candidate.frameEnds[i]<=previous||media[i].pixelsPerUnit!=candidate.pixelsPerUnit)return false;
                previous=candidate.frameEnds[i];
            }
            settings=candidate;frames=media;return true;
        }
        public bool Hit(Vector3 position,CombatHitOrigin origin,float now)
        {
            if(!target||!Matches(origin)||!Load())return false;
            Slot slot=null;foreach(var s in slots)if(!s.active){slot=s;break;}
            if(slot==null)
            {
                // Owned by the target's root so the pool leaves the hierarchy with the monster instead of piling up at scene root.
                slot=new Slot();var go=new GameObject("Basic sword A contact");go.transform.SetParent(target.transform.parent,false);
                slot.renderer=go.AddComponent<SpriteRenderer>();
                slot.renderer.sharedMaterial=target.sharedMaterial;
                slot.renderer.sortingLayerID=target.sortingLayerID;slot.renderer.sortingOrder=target.sortingOrder+10;
                slots.Add(slot);
            }
            slot.active=true;slot.start=now;slot.renderer.enabled=true;
            slot.position=position;slot.renderer.transform.position=position;slot.renderer.sprite=frames[0];
            slot.renderer.flipX=origin.SourceFacing<0;slot.renderer.flipY=origin.ActionId=="base_sword_basic_1";
            Emissions++;LastPosition=position;LastFlipX=slot.renderer.flipX;LastFlipY=slot.renderer.flipY;return true;
        }
        public void Draw(float now)
        {
            foreach(var slot in slots)
            {
                if(!slot.active)continue;
                float age=now-slot.start;int index=0;
                while(index<settings.frameEnds.Length && age>=settings.frameEnds[index])index++;
                if(age<0||index==frames.Length){slot.active=false;slot.renderer.enabled=false;}
                else{slot.renderer.transform.position=slot.position;slot.renderer.sprite=frames[index];}
            }
        }
        public void Clear(){foreach(var slot in slots){slot.active=false;if(slot.renderer)slot.renderer.enabled=false;}Emissions=0;}
        public void Dispose(){foreach(var slot in slots)if(slot.renderer)UnityEngine.Object.Destroy(slot.renderer.gameObject);slots.Clear();}
    }
}
