using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Chapter2
{
    // Pause, manual stepping and repeated draws sample the same arena clock.
    [DefaultExecutionOrder(950)]
    public sealed class Chapter2ElitePresentation : MonoBehaviour
    {
        Chapter2EliteEnemy actor;Chapter2EliteArtData data;ElitePattern lastPattern;
        readonly List<SpriteRenderer> pool=new List<SpriteRenderer>();int used;
        float began;bool active,arrived;Vector2 departure,arrival;int startDirection,attackDirection;
        Material fragmentMaterial;
        struct BodyShard { public Sprite sprite; public Vector2 offset; public Vector2 velocity; }
        readonly List<BodyShard> shards=new List<BodyShard>();
        float deathAt;
        bool deathFlip;
        public string CurrentClip {get;private set;}
        public int CurrentFrame {get;private set;}
        public int GroundDebrisCount {get;private set;}
        public int ActiveEffectCount=>used;
        public Chapter2EliteArtData Art=>data;
        public float BodyScale=>actor.Settings.bodyHeight*actor.Settings.bodyArtScale*data.ppu/data.bodyReferenceHeightPx;
        float Unit=>BodyScale/data.ppu;
        public Material FragmentMaterial=>fragmentMaterial;
        public void Bind(Chapter2EliteEnemy owner)
        {
            actor=owner;data=Chapter2EliteArt.Load();
            var template=Resources.Load<Material>("Chapter2EliteFragmentOutline");
            if(!template)throw new InvalidOperationException("A3 white fragment outline material missing");
            fragmentMaterial=new Material(template){name="A3 white-outlined projectiles"};ResetPose();
        }
        public void Begin(ElitePattern pattern)
        {
            lastPattern=pattern;began=actor.arena.Now;active=true;arrived=false;
            departure=actor.Position;startDirection=attackDirection=actor.Direction;Sample();
        }
        public void Arrive(Vector2 from)
        {departure=from;arrival=actor.Position;arrived=true;attackDirection=actor.Direction;Sample();}
        public void ResetPose(){active=false;lastPattern=ElitePattern.Rest;began=actor.arena.Now;Sample();}
        public void ConfigureFragment(SpriteRenderer view,int index,Vector2 velocity)
        {
            int i=index%5;view.sprite=data.Find("fragment").frames[i].sprite;view.color=Color.white;view.sharedMaterial=fragmentMaterial;
            view.transform.localScale=Vector3.one*(actor.Settings.fragmentVisualSize/(16f/data.ppu));
            view.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-data.fragmentAxes[i]);
        }
        void LateUpdate()=>Sample();
        public void Sample()
        {
            if(!actor||data==null||!actor.visual)return;
            used=0;GroundDebrisCount=0;float age=actor.arena.Now-began;
            var body=actor.visual;body.transform.localPosition=Vector3.zero;body.transform.localRotation=Quaternion.identity;body.transform.localScale=Vector3.one*BodyScale;
            if(actor.Dead)
            {
                active=false;body.enabled=false;
                SampleDeath(body);
                Finish();return;
            }
            if(shards.Count>0)ClearShards();
            body.enabled=true;body.color=Color.white;
            if(!active){SetBody("idle",age,actor.facing>0);Finish();return;}
            if(lastPattern==ElitePattern.Teleport)
            {
                bool ended=age*1000>=data.teleportEndMs;
                SetBody(ended?"idle":"teleport",age,arrived?attackDirection<0:startDirection>0);
                body.enabled=!(age*1000>=data.teleportVanishMs&&!arrived&&actor.Pattern==ElitePattern.Teleport);
                TeleportEffects(age);
            }
            else if(lastPattern==ElitePattern.Combo)
            {age*=actor.Settings.comboSpeed;SetBody(age*1000>=data.Find("combo").durationMs?"idle":"combo",age,attackDirection>0);ComboEffects(age);}
            else
            {
                if(actor.Pattern==ElitePattern.Rock&&!actor.RockLocked)attackDirection=actor.Direction;
                SetBody(age*1000>=data.rockBodyEndMs?"idle":"punch",age,attackDirection>0);RockEffects(age);
            }
            Finish();
        }
        void SampleDeath(SpriteRenderer body)
        {
            if(shards.Count==0)
            {
                var source=body.sprite;
                if(!source)return;
                deathAt=actor.arena.Now; deathFlip=body.flipX;
                var rect=source.rect;
                const int pieces=4;
                for(int y=0;y<pieces;y++)for(int x=0;x<pieces;x++)
                {
                    int left=Mathf.RoundToInt(rect.width*x/pieces),right=Mathf.RoundToInt(rect.width*(x+1)/pieces);
                    int bottom=Mathf.RoundToInt(rect.height*y/pieces),top=Mathf.RoundToInt(rect.height*(y+1)/pieces);
                    var region=new Rect(rect.x+left,rect.y+bottom,right-left,top-bottom);
                    var offset=(new Vector2((left+right)*.5f,(bottom+top)*.5f)-source.pivot)/source.pixelsPerUnit*BodyScale;
                    if(deathFlip)offset.x=-offset.x;
                    var sprite=Sprite.Create(source.texture,region,Vector2.one*.5f,source.pixelsPerUnit,0,SpriteMeshType.FullRect);
                    shards.Add(new BodyShard{sprite=sprite,offset=offset,velocity=new Vector2((x-1.5f)*(deathFlip?-2:2),3+y*.8f)});
                }
            }
            float age=actor.arena.Now-deathAt;
            if(age>=.65f)return;
            foreach(var shard in shards)
            {
                var at=actor.Position+shard.offset+shard.velocity*age+Vector2.down*(11*age*age);
                Use(shard.sprite,at,Vector2.one*BodyScale,deathFlip,Mathf.Clamp01((.65f-age)/.25f),shard.velocity.x*age*45);
            }
        }
        void ClearShards(){foreach(var shard in shards)Destroy(shard.sprite);shards.Clear();}
        void SetBody(string clip,float time,bool flip)
        {var c=data.Find(clip);CurrentClip=clip;CurrentFrame=c.FrameAt(time);actor.visual.sprite=c.frames[CurrentFrame].sprite;actor.visual.flipX=flip;}
        void Finish(){for(int i=used;i<pool.Count;i++)if(pool[i])pool[i].enabled=false;}
        SpriteRenderer Use(Sprite sprite,Vector2 at,Vector2 scale,bool flip=false,float alpha=1,float rotation=0,int order=17)
        {
            if(used==pool.Count)
            {
                var node=new GameObject("A3 presentation layer "+used);node.transform.SetParent(transform,false);
                var created=node.AddComponent<SpriteRenderer>();created.sharedMaterial=actor.visual.sharedMaterial;pool.Add(created);
            }
            var view=pool[used++];view.enabled=true;view.sprite=sprite;view.color=new Color(1,1,1,Mathf.Clamp01(alpha));view.flipX=flip;view.sortingOrder=order;
            view.transform.position=at;view.transform.rotation=Quaternion.Euler(0,0,rotation);view.transform.localScale=new Vector3(scale.x,scale.y,1);return view;
        }
        bool ClipEffect(string id,float age,Vector2 at,Vector2 scale,bool flip=false,float alpha=1)
        {
            var clip=data.Find(id);if(age<0||age*1000>=clip.durationMs)return false;
            Use(clip.At(age).sprite,at,scale,flip,alpha*Mathf.Min(1,(clip.durationMs-age*1000)/25));return true;
        }
        void TeleportEffects(float age)
        {
            float vanish=data.teleportVanishMs/1000f,appear=data.teleportArrivalMs/1000f,hit=data.teleportHitMs/1000f;
            if(age<appear)departure=actor.Position;
            ClipEffect("blink",age-vanish,departure+Vector2.up*(43*Unit),Vector2.one*(1.2f*BodyScale),startDirection>0);
            if(arrived)
            {
                ClipEffect("blink",age-appear,arrival+Vector2.up*(43*Unit),Vector2.one*(1.05f*BodyScale),attackDirection<0);
                float tail=1-(age-appear)/.18f;
                if(tail>0){var charge=data.Find("teleport").frames[8].sprite;for(int i=0;i<7;i++)Use(charge,Vector2.Lerp(departure,arrival,i/6f),Vector2.one*BodyScale,attackDirection<0,(.08f+.22f*i/6)*tail,0,11);}
            }
            var contact=actor.Position+new Vector2(82*Unit*attackDirection,58*Unit);
            ClipEffect("teleHit",age-hit,contact,Vector2.one*(.95f*BodyScale),attackDirection<0);
        }
        void ComboEffects(float age)
        {
            var fx=data.debris;
            for(int i=0;i<3;i++)
            {
                float elapsed=age-fx.impactTimesMs[i]/1000f;var origin=actor.Position+Vector2.right*(59*Unit*attackDirection);
                float waveScale=i==0?1.25f:i==1?1.4f:1.95f;
                ClipEffect("groundWave",elapsed,origin,Vector2.one*(waveScale*BodyScale));
                if(elapsed<0||elapsed>.4f)continue;
                foreach(var p in fx.bursts[i].particles)
                {
                    float ms=elapsed*1000-p.delayMs;if(ms<0||ms>=p.flightMs)continue;
                    float t=ms/1000,dx=p.originX+p.vx*t,dy=p.vy*t-.5f*fx.gravity*t*t;
                    var at=origin+new Vector2(dx*Unit,dy*Unit+4*p.scale*Unit);
                    Use(data.Find("fragment").frames[p.image].sprite,at,Vector2.one*(p.scale*BodyScale),false,Mathf.Min(1,(p.flightMs-ms)/45),(p.angle+p.spin*t)*Mathf.Rad2Deg,18);GroundDebrisCount++;
                }
            }
        }
        Vector2 Fist(float forward,float up)=>actor.Position+new Vector2(forward*Unit*attackDirection,up*Unit);
        void RockEffects(float age)
        {
            float hit=actor.Settings.rockFire;
            if(age<hit&&actor.Pattern==ElitePattern.Rock)
            {float scale=actor.Settings.rockSize/(191f/data.ppu);Use(data.Find("formation").At(age).sprite,actor.RockPosition,Vector2.one*scale,attackDirection<0,1,0,13);}
            float wrap=age-data.rockWrapMs/1000f;
            if(wrap>=0&&wrap<.065f)
            {
                int source=data.Find("punch").At(age).source;var point=source==44?Fist(52,53.64f):source==45?Fist(84,52.925f):Fist(84,52);
                float q=Mathf.Clamp01(wrap/.015f),growth=.72f+.28f*q*q*(3-2*q),fade=Mathf.Min(1,(.065f-wrap)/.02f);
                Use(data.Find("wrap").At(wrap).sprite,point,new Vector2(.9f,.525f)*BodyScale*growth,attackDirection>0,(.35f+.65f*Mathf.Min(1,wrap/.015f))*fade,0,11);
            }
            ClipEffect("contact",age-hit,Fist(84,52),Vector2.one*(1.4f*BodyScale));
            ClipEffect("shatter",age-hit,actor.RockPosition,Vector2.one*BodyScale,attackDirection<0);
        }
        void OnDisable(){foreach(var p in pool)if(p)p.enabled=false;}
        void OnDestroy(){ClearShards();if(fragmentMaterial)Destroy(fragmentMaterial);}
    }
}
