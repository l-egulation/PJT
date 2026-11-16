using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable] public sealed class ApprovedRangedCatalog { public ApprovedRangedEffect[] effects; }
    [Serializable] public sealed class ApprovedRangedEffect
    {
        public string form,kind,variant,projectile,core;
        public float angle,muzzleScale=1;
        public CharacterClip muzzle,beam,blast;
        [NonSerialized] public Sprite projectileSprite,coreSprite;
        public void Load()
        {
            // JsonUtility materializes absent nested objects with empty fields.
            muzzle=LoadOptional(muzzle);beam=LoadOptional(beam);blast=LoadOptional(blast);
            if(!string.IsNullOrEmpty(projectile))projectileSprite=Resources.Load<Sprite>(projectile);
            if(!string.IsNullOrEmpty(core))coreSprite=Resources.Load<Sprite>(core);
        }
        static CharacterClip LoadOptional(CharacterClip clip)
        {
            if(clip?.frames==null || clip.frames.Length==0)return null;
            clip.Load();return clip;
        }
    }

    // Only observes actual fire/impact events. This class never queries targets or deals damage.
    public sealed class RangedWeaponPresentation : IDisposable
    {
        sealed class Burst
        {
            public GameObject root;public SpriteRenderer renderer;public CharacterClip clip;
            public float started;public CombatActor follow;public string form,look;
            public int facing;public Vector2 fallback;public Guid execution;
            public float scale,angle;
        }
        readonly CombatArena arena;
        readonly Dictionary<string,ApprovedRangedEffect> definitions=new Dictionary<string,ApprovedRangedEffect>();
        readonly List<Burst> bursts=new List<Burst>();
        public int ActiveCount=>bursts.Count;
        public RangedWeaponPresentation(CombatArena arena)
        {
            this.arena=arena;
            var text=Resources.Load<TextAsset>("CharacterV2/ApprovedRangedEffects");if(!text)return;
            foreach(var effect in JsonUtility.FromJson<ApprovedRangedCatalog>(text.text).effects){effect.Load();definitions.Add(effect.form,effect);}
        }
        public ApprovedRangedEffect Find(string form)=>definitions.TryGetValue(form,out var effect)?effect:null;
        public Vector2 MuzzlePosition(CombatActor owner,string form)
        {
            var set=Array.Find(arena.characterAnimations.sets,s=>s.form==form);
            var clip=set?.Find(arena.Presentation.CurrentBodyClipName??"idle")??set?.Find("attack_single");
            if(clip?.muzzleX==null)return owner.Position+new Vector2(owner.facing,1);
            var cast=owner.currentCast;
            float time=cast?clip.SourceTime(cast.Age,cast.ImpactTime,cast.MotionDuration):0;
            int i=clip.FrameIndex(time);
            return owner.Position+new Vector2(clip.muzzleX[i]*owner.facing,clip.muzzleY[i]);
        }
        string Look(CombatActor owner,string form)=>owner is CombatPlayer player && MotionSourceRegistry.HasInkAppearance(player.Form.id)?MotionSourceRegistry.ConversionForm(player.Form.id,player.Weapon.SpecialIndex):form;
        public void Muzzle(CombatActor owner,string form)
        {
            var definition=Find(form);if(definition?.muzzle==null)return;
            // The flash belongs to the fired attack, but sits on the muzzle of the gun the player is drawn with.
            string look=Look(owner,form);
            var b=Create("ApprovedMuzzle_"+form,definition.muzzle,MuzzlePosition(owner,look),definition.muzzleScale,owner.facing);
            // The shotgun recoil throws the body back at once: its flash stays where the shot left (approved 2026-11-06).
            if(form!="launcher_shotgun" || !LauncherSkillFx.Enabled(arena))b.follow=owner;
            b.form=form;b.look=look;b.fallback=b.root.transform.position;
            b.execution=owner.currentCast?owner.currentCast.ExecutionId:Guid.Empty;b.angle=definition.angle;
            // Preserve the approved atlas rotation and mirror it with the firing weapon.
            b.root.transform.rotation=Quaternion.Euler(0,0,-definition.angle*owner.facing);
        }
        Burst Create(string name,CharacterClip clip,Vector2 at,float scale,int facing)
        {
            var root=new GameObject(name);root.transform.position=at;root.transform.localScale=new Vector3(scale*facing,scale,1);
            var r=root.AddComponent<SpriteRenderer>();r.sortingOrder=25;r.sprite=clip.At(0,false);
            if(arena.spriteMaterial)r.sharedMaterial=arena.spriteMaterial;
            var b=new Burst{root=root,renderer=r,clip=clip,started=arena.Now,facing=facing,scale=scale};bursts.Add(b);return b;
        }
        public bool Blast(Vector2 at,float radius)
        {
            if(LauncherSkillFx.Enabled(arena))
            {
                // pixelfx blasts (approved 2026-11-06): body or fragment, rising from the floor when it went off on the floor.
                bool main=radius>=.9f;var floor=FloorBelow(at,.35f);
                var book=LauncherSkillFx.Play(arena,"grenade_"+(main?"main":"frag")+"_"+(floor.HasValue?"ground":"air"),floor??at,1,LauncherSkillFx.FrontOrder,
                    objectName:main?"ApprovedGrenadeImpact":"ApprovedGrenadeFragmentImpact");
                if(book)return true;
            }
            var d=Find("launcher_grenade");if(d?.blast==null)return false;
            // Main blast uses the accepted native silhouette. Fragments use the same silhouette at half scale.
            Create("ApprovedGrenadeImpact",d.blast,at,radius/1.2f,1);return true;
        }
        public bool GrenadeProjectile(Transform parent,bool fragment,Vector2 velocity)
        {
            if(LauncherSkillFx.Enabled(arena))
            {
                // Shell or fragment flipbook under the same child name the bomb turns with its velocity, plus a smoke trail.
                var book=LauncherSkillFx.Play(arena,fragment?"grenade_frag":"grenade_shell",Vector2.zero,1,LauncherSkillFx.Order,parent,
                    Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg,objectName:"ApprovedGrenadeProjectile");
                if(book){parent.gameObject.AddComponent<LauncherSmokeTrail>().Begin(arena,fragment?.04f:.03f,fragment?.04f:.05f);return true;}
            }
            var sprite=Find("launcher_grenade")?.projectileSprite;if(!sprite)return false;
            var child=new GameObject("ApprovedGrenadeProjectile");child.transform.SetParent(parent,false);
            child.transform.localScale=Vector3.one*(fragment?.5f:1);
            child.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
            var r=child.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=25;
            if(arena.spriteMaterial)r.sharedMaterial=arena.spriteMaterial;return true;
        }
        public void Laser(LaserVisual laser,CombatActor owner,Vector2 endpoint,int direction,List<Vector2> hits=null,bool ignoresTerrain=false)
        {
            var d=Find("launcher_laser");if(d?.beam==null||!d.coreSprite)return;
            Vector2 muzzle=MuzzlePosition(owner,"launcher_laser");float length=Mathf.Max(0,(endpoint.x-muzzle.x)*direction);
            var cast=owner.currentCast;
            float duration=.1f;
            if(cast && arena.Presentation.TryGetLaserReleaseFraction(cast.ExecutionId,out var fraction))
                duration=fraction*(cast.MotionDuration-cast.ImpactTime);
            laser.ConfigureApproved(d,muzzle,length,direction,duration,arena.spriteMaterial);
            if(LauncherSkillFx.Has(arena,"laser_beam"))laser.ConfigureLauncherFx(muzzle,length,direction,!ignoresTerrain&&WallAt(muzzle,length,direction),hits,arena.TechnicalSprite);
        }

        // ---- Launcher skill effects (user-approved review GIFs, 2026-11-06) ----
        static readonly Vector2 ChargeOrbAt=new Vector2(1.3125f+.12f,.875f); // F048 aim pose muzzle, a little ahead
        int pendingChargeStep;
        LauncherFxFlipbook chargeOrb,chargeSuck;int chargeStep;
        LauncherFxFlipbook recoilWind;bool recoilWas;

        // Fire-time pops on the muzzle of the gun the player is drawn with; they stay where the shot left.
        public void FirePop(CombatActor owner,SkillRangedLaunchDefinition data)
        {
            pendingChargeStep=0;
            if(!LauncherSkillFx.Enabled(arena)||data?.Projectile==null)return;
            switch(data.Projectile.Type)
            {
                case "ball":Pop(owner,"launcher","ball_pop");break;
                case "shotgun":Pop(owner,"launcher_shotgun","shotgun_pop");break;
                case "charge_ball":
                    int step=pendingChargeStep=LauncherSkillFx.ChargeStep(data.Projectile.Radius,data.ChargeRadius);
                    Pop(owner,"launcher",step==1?"ball_pop":"charge_pop_big");
                    if(step==3)LauncherSkillFx.Play(arena,"charge_up",Orb(owner),1,LauncherSkillFx.FrontOrder);
                    EndCharge();break;
            }
        }
        public void GrenadePop(CombatActor owner){if(LauncherSkillFx.Enabled(arena))Pop(owner,"launcher_grenade","grenade_pop");}
        void Pop(CombatActor owner,string form,string clip)=>LauncherSkillFx.Play(arena,clip,MuzzlePosition(owner,Look(owner,form)),owner.facing,LauncherSkillFx.FrontOrder);

        // The flipbook body of a player shot. The hit circle (and its scaled root) stays as it was; the body follows it unscaled.
        internal LauncherProjectileVisual AttachShot(PlayerProjectile shot,string type,Vector2 velocity)
        {
            if(!LauncherSkillFx.Enabled(arena))return null;
            int facing=velocity.x<0?-1:1;string clip,hit=null;Vector2 offset;float gap,angle=0;bool tail=false;int step=0;
            switch(type)
            {
                case "ball":clip="ball_bullet";offset=new Vector2(0,-.125f);gap=.3f;hit="ball_hit";break;
                case "charge_ball":step=Mathf.Clamp(pendingChargeStep,1,3);clip="charge_shot"+step;offset=new Vector2(0,-.125f);gap=.3f;break;
                case "shotgun":
                    float a=Mathf.Atan2(velocity.y,Mathf.Abs(velocity.x));clip="shotgun_pellet";offset=new Vector2(0,.11f);
                    gap=.256f/Mathf.Max(.2f,Mathf.Cos(a));angle=a*Mathf.Rad2Deg*facing;hit="shotgun_hit";tail=true;break;
                default:return null;
            }
            var book=LauncherSkillFx.Play(arena,clip,(Vector2)shot.transform.position+offset,facing,LauncherSkillFx.Order,null,angle);
            if(!book)return null;
            var visual=shot.gameObject.AddComponent<LauncherProjectileVisual>();
            visual.Body=book;visual.Spawn=shot.transform.position;visual.Gap=gap;visual.DrawOffset=offset;visual.HitClip=hit;visual.TerrainTail=tail;visual.Step=step;
            book.Visible=false;return visual;
        }
        internal void ShotRemoved(LauncherProjectileVisual visual,bool enemy,bool terrain,Vector2 at,int facing)
        {
            if(!visual)return;
            if(visual.Body)UnityEngine.Object.Destroy(visual.Body.gameObject);
            ShotContact(visual,enemy,terrain,at,facing);
        }
        internal void ShotContact(LauncherProjectileVisual visual,bool enemy,bool terrain,Vector2 at,int facing)
        {
            if(!visual)return;
            if(visual.HitClip==null||!(enemy||terrain&&visual.TerrainTail))return;
            var clip=LauncherSkillFx.Find(visual.HitClip);if(clip==null)return;
            LauncherSkillFx.Play(arena,visual.HitClip,at+visual.DrawOffset,facing,LauncherSkillFx.FrontOrder,startAge:enemy?0:clip.StartOf(2));
        }
        internal bool ChargeBurst(int step,Vector2 at,int facing)
            =>LauncherSkillFx.Play(arena,"charge_burst"+Mathf.Clamp(step,1,3),at,facing,LauncherSkillFx.FrontOrder)!=null;

        Vector2 Orb(CombatActor owner)=>owner.Position+new Vector2(ChargeOrbAt.x*owner.facing,ChargeOrbAt.y);
        void EndCharge()
        {
            if(chargeOrb)UnityEngine.Object.Destroy(chargeOrb.gameObject);
            if(chargeSuck)UnityEngine.Object.Destroy(chargeSuck.gameObject);
            chargeOrb=chargeSuck=null;chargeStep=0;
        }
        // While the charge shot is held: the orb grows a step at each charge step and sparkles when it does.
        void TickCharge()
        {
            var player=arena.Player;var cast=player?player.currentCast:null;
            bool charging=cast && cast.ChargeActive && !cast.ChargeRelease.HasValue && cast.Definition.Ranged?.Projectile?.Type=="charge_ball" && cast.Age>=.05f;
            if(!charging||!LauncherSkillFx.Enabled(arena)){if(chargeOrb||chargeSuck)EndCharge();return;}
            float min=cast.Definition.ChargeMin,max=cast.Definition.ChargeMax,span=Mathf.Max(.001f,max-min);
            int step=cast.Age<min+span/3?1:cast.Age<min+span*2/3?2:3;
            Vector2 at=Orb(player);
            if(!chargeSuck)chargeSuck=LauncherSkillFx.Play(arena,"charge_suck",at,1,LauncherSkillFx.Order);
            if(!chargeOrb||step!=chargeStep)
            {
                if(chargeOrb)UnityEngine.Object.Destroy(chargeOrb.gameObject);
                chargeOrb=LauncherSkillFx.Play(arena,"charge_orb"+step,at,player.facing,LauncherSkillFx.Order+1);
                if(chargeStep>0&&step>chargeStep)LauncherSkillFx.Play(arena,"charge_up",at,1,LauncherSkillFx.FrontOrder);
                chargeStep=step;
            }
            if(chargeSuck)chargeSuck.transform.position=at;
            if(chargeOrb)chargeOrb.transform.position=at;
        }
        // Shotgun recoil: dust where it kicked off and where it lands, wind on the gun side while thrown back.
        void TickRecoil()
        {
            var player=arena.Player;var cast=player?player.currentCast:null;
            bool now=cast && cast.Definition.Ranged?.Projectile?.Type=="shotgun" && cast.Recoiling && LauncherSkillFx.Enabled(arena);
            if(now&&!recoilWas)
            {
                if(player.grounded)Dust(player);
                recoilWind=LauncherSkillFx.Play(arena,"shotgun_wind",WindAt(player),player.facing,LauncherSkillFx.BackOrder);
            }
            if(now&&recoilWind)recoilWind.transform.position=WindAt(player);
            if(!now&&recoilWas)
            {
                if(recoilWind)UnityEngine.Object.Destroy(recoilWind.gameObject);recoilWind=null;
                if(player&&player.grounded)Dust(player);
            }
            recoilWas=now;
        }
        static Vector2 WindAt(CombatActor p)=>p.Position+new Vector2(.4f*p.facing,1.05f);
        void Dust(CombatActor p)=>LauncherSkillFx.Play(arena,"shotgun_dust",p.Position+new Vector2(.3f*p.facing,0),1,LauncherSkillFx.BackOrder);

        static bool WallAt(Vector2 muzzle,float length,int direction)
        {
            foreach(var hit in Physics2D.RaycastAll(muzzle,Vector2.right*direction,length+.25f))
            {
                if(!hit.collider||hit.collider.isTrigger)continue;
                var surface=hit.collider.GetComponent<ArenaSurface>();
                if(surface&&!surface.oneWay&&hit.distance>=length-.3f)return true;
            }
            return false;
        }
        static Vector2? FloorBelow(Vector2 at,float maxDrop)
        {
            float best=float.PositiveInfinity;
            foreach(var hit in Physics2D.RaycastAll(at+Vector2.up*.05f,Vector2.down,maxDrop+.05f))
            {
                if(!hit.collider||hit.collider.isTrigger||!hit.collider.GetComponent<ArenaSurface>())continue;
                if(hit.distance<best)best=hit.distance;
            }
            return float.IsPositiveInfinity(best)?(Vector2?)null:new Vector2(at.x,at.y+.05f-best);
        }

        public void Tick()
        {
            TickCharge();TickRecoil();
            for(int i=bursts.Count-1;i>=0;i--)
            {
                var b=bursts[i];float age=arena.Now-b.started;
                if(!b.root||age>=b.clip.Duration){if(b.root)UnityEngine.Object.Destroy(b.root);bursts.RemoveAt(i);continue;}
                b.renderer.sprite=b.clip.At(age,false);
                if(b.follow && !b.follow.Dead)
                {
                    // Follow the firing weapon only while it is still equipped. Released effects persist independently.
                    if(b.follow.currentCast && b.follow.currentCast.ExecutionId==b.execution && (!(b.follow is CombatPlayer p)||MotionSourceRegistry.ConversionForm(p.Form.id,p.Weapon.SpecialIndex)==b.look))
                    {
                        b.fallback=MuzzlePosition(b.follow,b.look);b.facing=b.follow.facing;
                        b.root.transform.localScale=new Vector3(b.scale*b.facing,b.scale,1);
                        b.root.transform.rotation=Quaternion.Euler(0,0,-b.angle*b.facing);
                    }
                    b.root.transform.position=b.fallback;
                }
            }
        }
        public void Reset()
        {
            foreach(var b in bursts)if(b.root)UnityEngine.Object.Destroy(b.root);bursts.Clear();
            chargeOrb=chargeSuck=recoilWind=null;chargeStep=pendingChargeStep=0;recoilWas=false;
            LauncherSkillFx.Clear(arena);
        }
        public void Dispose()=>Reset();
    }
}
