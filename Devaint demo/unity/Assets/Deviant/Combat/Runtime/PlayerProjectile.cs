using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    // Independent of the attack animation: cancellation/switching cannot recall a released shot.
    public partial class PlayerProjectile : MonoBehaviour, IPlayerProjectileExecutionHost
    {
        public CombatArena arena;
        public CombatActor owner;
        public RangedProjectileDefinition Definition => execution.Definition;
        // 바닥을 긁고 지나가는 탄인가: 큰 관통 수리검의 원 아래 절반에만 걸친 면(밟고 선 바닥)은 막지 않는다.
        public static bool SkimsFloor(string type, float centerY, float radius, float surfaceTop)
            => type == "shuriken_special" && surfaceTop <= centerY - radius * .5f;
        public WeaponState sourceWeapon;
        public Vector2 velocity;
        public float damage;
        internal CombatHitOrigin HitOrigin { get; private set; }
        public float traveled => execution.Traveled;
        PlayerProjectileExecution execution;
        internal LauncherProjectileVisual launcherVisual;
        bool lastEnemy, lastTerrain; Vector2 lastContact;
        public Vector2 Position { get { return transform.position; } set { transform.position=value; } }
        public static PlayerProjectile Spawn(CombatArena arena,CombatActor owner,Vector2 origin,Vector2 velocity,RangedData data,float damage,CombatHitOrigin hitOrigin=default)
            => Spawn(arena,owner,origin,velocity,new RangedProjectileDefinition(data),damage,hitOrigin);
        public static PlayerProjectile Spawn(CombatArena arena,CombatActor owner,Vector2 origin,Vector2 velocity,RangedProjectileDefinition definition,float damage,CombatHitOrigin hitOrigin=default)
        {
            if(arena.MvpGrowth!=null&&owner.isPlayer)definition=definition.WithGeometry(arena.MvpGeometry(hitOrigin,false),arena.MvpGeometry(hitOrigin,true));
            var p=new GameObject("PlayerShot").AddComponent<PlayerProjectile>();
            p.arena=arena; p.owner=owner; p.Position=origin; p.velocity=velocity; p.execution=new PlayerProjectileExecution(definition); p.damage=damage;
            p.HitOrigin = (hitOrigin.IsSpecified ? hitOrigin : arena.DirectHitOrigin(owner)).WithEffect(System.Guid.NewGuid(), CombatHitKind.Projectile);
            var r=p.gameObject.AddComponent<SpriteRenderer>(); r.sprite=arena.CircleSprite; r.color=new Color(.05f,.35f,.9f); r.sortingOrder=25;
            if(arena.RangedPresentation!=null && (definition.Type=="ball" || definition.Type=="charge_ball" || definition.Type=="shotgun"))r.color=new Color32(27,22,22,255);
            p.transform.localScale=Vector3.one*p.Definition.Radius*2;
            // Approved launcher bodies (2026-11-06) replace the dark circle; the circle stays the hit shape.
            if(arena.RangedPresentation!=null && owner && owner.isPlayer && (p.launcherVisual=arena.RangedPresentation.AttachShot(p,definition.Type,velocity)))r.enabled=false;
            // Approved shuriken skill stars (2026-11-08) come first; the older thrown star stays as their fallback.
            bool approved=ShurikenSkillFx.TryAttachShot(p,definition.Type) || definition.Type=="shuriken" && ApprovedThrownVisual.TryAttach(p.transform,arena,"shuriken");
            if(approved)r.enabled=false;
            if(arena.spriteMaterial) r.sharedMaterial=arena.spriteMaterial;
            if(p.Definition.HitInterval>0 && !approved)
            {
                r.color=new Color(.05f,.35f,.9f,.18f);
                // Temporary rotating blades make rotation visible without changing the circular hurt query.
                for(int i=0;i<4;i++)
                {
                    var blade=new GameObject("RotatingBlade"); blade.transform.SetParent(p.transform,false);
                    blade.transform.localRotation=Quaternion.Euler(0,0,i*90);
                    blade.transform.localPosition=Quaternion.Euler(0,0,i*90)*new Vector3(.18f,0,0);
                    blade.transform.localScale=new Vector3(.6f,.13f,1);
                    var visual=blade.AddComponent<SpriteRenderer>(); visual.sprite=arena.CircleSprite;
                    visual.color=new Color(.05f,.2f,.6f); visual.sortingOrder=26;
                    if(arena.spriteMaterial) visual.sharedMaterial=arena.spriteMaterial;
                }
            }
            arena.playerShots.Add(p); arena.PlayerShotCount++;
            return p;
        }
        bool Valid(Collider2D c)
        {
            var host=new PlayerProjectileContactHost(this,c);
            return PlayerProjectileSweepExecution.IsValid(ref host);
        }
        bool Resolve(Collider2D c,float contactAge=-1, CombatHitContact contact=default)
        {
            if (!contact.HasPoint) contact = CombatHitContacts.At(c.ClosestPoint(Position), velocity, HitAnchorKind.OverlapRepresentative);
            if (Definition.ImpactBoxSize > 0 && Valid(c) && c.GetComponent<HurtRegion>())
            { DetonateCharge(new Vector2(contact.X, contact.Y)); return true; }
            var host=new PlayerProjectileHitHost(this,c,contact);
            bool resolved=PlayerProjectileHitExecution.Resolve(contactAge,ref host);
            if(resolved && launcherVisual)
            { lastEnemy=c.GetComponent<HurtRegion>(); lastTerrain=!lastEnemy && c.GetComponent<ArenaSurface>(); lastContact=new Vector2(contact.X,contact.Y); }
            return resolved;
        }
        public void Detonate()
        {
            var host=new PlayerProjectileBlastHost(this);
            PlayerProjectileHitExecution.Detonate(ref host);
            arena.FinishSketchHit();
        }
        public void SweepTo(Vector2 destination)
        {
            var host=new PlayerProjectileSweepHost(this,destination);
            PlayerProjectileSweepExecution.Sweep(ref host);
        }
        static int Priority(Collider2D c)
        {
            var host=new PlayerProjectileContactHost(null,c);
            return PlayerProjectileSweepExecution.Priority(ref host);
        }
        int CompareOverlap(Collider2D a,Collider2D b)
        {
            var host=new PlayerProjectileOverlapHost(this,a,b);
            return PlayerProjectileSweepExecution.CompareOverlap(ref host);
        }
        public void Step(float dt)
        {
            if(execution.Removed) return;
            bool alive=owner && !owner.Dead;
            if(alive && Definition.Homing) Home(dt);
            execution.Step(dt,alive,alive ? velocity.magnitude : 0,this);
        }
        // Weak homing (basic shuriken): lock the nearest living enemy inside the cone around the throw direction and
        // turn toward it at a capped rate, never leaving the deflection cap. Speed, range and collision stay unchanged.
        // Mirrors tools/combat/shuriken-homing-sim/index.html.
        Vector2 throwDirection; TestEnemy homingTarget;
        void Home(float dt)
        {
            float speed=velocity.magnitude; if(speed<=.000001f) return;
            if(throwDirection==Vector2.zero) throwDirection=velocity/speed;
            float remaining=Definition.Distance-traveled;
            if(!HomingCandidate(homingTarget,remaining))
            {
                homingTarget=null; float best=float.PositiveInfinity;
                foreach(var e in arena.enemies)
                {
                    if(!HomingCandidate(e,remaining)) continue;
                    float d=(e.AimPoint-Position).sqrMagnitude;
                    if(d<best || d==best && homingTarget && e.ActorId<homingTarget.ActorId){best=d;homingTarget=e;}
                }
            }
            if(!homingTarget) return;
            float current=Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg;
            Vector2 to=homingTarget.AimPoint-Position;
            float turn=Mathf.Clamp(Mathf.DeltaAngle(current,Mathf.Atan2(to.y,to.x)*Mathf.Rad2Deg),-Definition.HomingTurnRate*dt,Definition.HomingTurnRate*dt);
            float baseAngle=Mathf.Atan2(throwDirection.y,throwDirection.x)*Mathf.Rad2Deg;
            float heading=baseAngle+Mathf.Clamp(Mathf.DeltaAngle(baseAngle,current+turn),-Definition.HomingMaxAngle,Definition.HomingMaxAngle);
            velocity=new Vector2(Mathf.Cos(heading*Mathf.Deg2Rad),Mathf.Sin(heading*Mathf.Deg2Rad))*speed;
        }
        bool HomingCandidate(TestEnemy e,float remaining)
        {
            if(!e || e.Dead || e.isDecoy || !e.isActiveAndEnabled) return false;
            Vector2 to=e.AimPoint-Position; float distance=to.magnitude;
            if(distance<=.000001f) return true;
            return distance<=remaining+Definition.Radius && Vector2.Angle(throwDirection,to)<=Definition.HomingCone;
        }
        void IPlayerProjectileExecutionHost.SweepByTime(float duration) => SweepTo(Position+velocity*duration);
        void IPlayerProjectileExecutionHost.SweepByDistance(float distance) => SweepTo(Position+velocity.normalized*distance);
        void IPlayerProjectileExecutionHost.SweepCurrent() => SweepTo(Position);
        void IPlayerProjectileExecutionHost.Rotate(float duration) => transform.Rotate(0,0,-720*duration);
        void IPlayerProjectileExecutionHost.Remove()
        {
            if(launcherVisual)arena.RangedPresentation?.ShotRemoved(launcherVisual,lastEnemy,lastTerrain,lastContact,velocity.x<0?-1:1);
            arena.playerShots.Remove(this); Destroy(gameObject);
        }
        public void Release() => execution.Release(this);
    }

    public class LaserVisual : MonoBehaviour
    {
        public CombatArena arena;
        public float until;
        CharacterClip beam;
        SpriteRenderer glow,nextGlow,core;
        float started;
        public void ConfigureApproved(ApprovedRangedEffect effect,Vector2 muzzle,float length,int direction,float duration,Material material)
        {
            beam=effect.beam;started=arena.Now;until=started+duration;
            transform.position=muzzle;transform.localScale=new Vector3(direction*length/beam.sprites[0].bounds.size.x,1,1);
            glow=GetComponent<SpriteRenderer>();glow.sprite=beam.sprites[0];glow.color=Color.white;
            nextGlow=Layer("BeamGlowBlend",material,25);nextGlow.sprite=beam.sprites[0];
            core=Layer("WhiteBeamCore",material,26);core.sprite=effect.coreSprite;
            core.transform.localScale=new Vector3(beam.sprites[0].bounds.size.x/effect.coreSprite.bounds.size.x,1,1);
            Step();
        }

        // Approved laser skill look (2026-11-06): thinner tapered beam that reaches its end in a fixed time, a head while
        // it grows, a pop and a held light at the muzzle, a hit on each target as the head passes, a wall hit where it stops,
        // and a short thinning with scattered pieces after release. Damage already happened once at fire.
        const float Grow=.05f,Thicken=.08f,ThickFrom=.35f,Vanish=.06f,VanishTo=.12f;
        bool fx,wall,wallShown,vanishing;float fxLength,fullScaleX;int fxDirection;Vector2 fxMuzzle;Sprite shardSprite;
        LauncherFxFlipbook head,hold,wallHold;
        internal void ConfigureLauncherFx(Vector2 muzzle,float length,int direction,bool stopsAtWall,System.Collections.Generic.List<Vector2> hits,Sprite square)
        {
            var clip=LauncherSkillFx.Find("laser_beam");if(clip==null||!glow)return;
            beam=new CharacterClip{frames=clip.data.frames,seconds=(float[])clip.data.seconds.Clone(),sprites=clip.sprites};
            float width=beam.sprites[0].bounds.size.x;
            glow.sprite=nextGlow.sprite=beam.sprites[0];
            if(core&&core.sprite)core.transform.localScale=new Vector3(width/core.sprite.bounds.size.x,1,1);
            fx=true;wall=stopsAtWall;fxLength=length;fxDirection=direction;fxMuzzle=muzzle;fullScaleX=direction*length/width;shardSprite=square;
            LauncherSkillFx.Play(arena,"laser_pop",muzzle,direction,LauncherSkillFx.FrontOrder);
            head=LauncherSkillFx.Play(arena,"laser_head",muzzle,direction,LauncherSkillFx.FrontOrder);
            if(hits!=null)foreach(var at in hits)
                LauncherSkillFx.Play(arena,"laser_hit",at,direction,LauncherSkillFx.FrontOrder,delay:Grow*Mathf.Clamp01(Mathf.Abs(at.x-muzzle.x)/Mathf.Max(.001f,length)));
            Step();
        }
        void StepLauncherFx()
        {
            float t=arena.Now-started,grow=Mathf.Clamp01(t/Grow);
            transform.localScale=new Vector3(fullScaleX*grow,ThickFrom+(1-ThickFrom)*Mathf.Clamp01(t/Thicken),1);
            if(head){if(t<Grow)head.transform.position=fxMuzzle+Vector2.right*fxDirection*fxLength*grow;else Destroy(head.gameObject);}
            if(t>=.06f&&!hold)hold=LauncherSkillFx.Play(arena,"laser_hold",fxMuzzle,fxDirection,LauncherSkillFx.FrontOrder);
            if(wall&&!wallShown&&t>=Grow)
            {
                wallShown=true;Vector2 end=fxMuzzle+Vector2.right*fxDirection*fxLength;
                LauncherSkillFx.Play(arena,"laser_wall_hit",end,fxDirection,LauncherSkillFx.FrontOrder);
                var impact=LauncherSkillFx.Find("laser_wall_hit");
                wallHold=LauncherSkillFx.Play(arena,"laser_wall_hold",end,fxDirection,LauncherSkillFx.FrontOrder,delay:impact!=null?impact.Duration:.12f);
            }
        }
        void BeginVanish()
        {
            vanishing=true;
            foreach(var book in new[]{head,hold,wallHold})if(book)Destroy(book.gameObject);
            transform.localScale=new Vector3(fullScaleX,1,1);
            if(shardSprite)new GameObject("LaserVanish").AddComponent<LauncherShards>().Begin(arena,fxMuzzle,fxLength,fxDirection,shardSprite);
        }
        void LateUpdate()
        {
            if(!vanishing)return;
            // A reset clock (or a removed arena) ends the leftover picture at once.
            if(!arena||arena.Now<until-.001f){Destroy(gameObject);return;}
            float f=Mathf.Clamp01((arena.Now-until)/Vanish);
            if(f>=1){Destroy(gameObject);return;}
            transform.localScale=new Vector3(fullScaleX,1-(1-VanishTo)*f,1);
            float a=1-.5f*f;
            if(glow)glow.color=new Color(1,1,1,a);if(nextGlow)nextGlow.color=new Color(1,1,1,0);if(core)core.color=new Color(1,1,1,a);
        }
        SpriteRenderer Layer(string label,Material material,int order)
        {
            var child=new GameObject(label);child.transform.SetParent(transform,false);
            var renderer=child.AddComponent<SpriteRenderer>();renderer.sortingOrder=order;
            if(material)renderer.sharedMaterial=material;return renderer;
        }
        public void Step()
        {
            if(arena.Now>=until)
            {
                arena.lasers.Remove(this);
                if(fx&&!vanishing&&LauncherSkillFx.Enabled(arena))BeginVanish();else if(!vanishing)Destroy(gameObject);
                return;
            }
            if(beam==null)return;
            if(fx)StepLauncherFx();
            float phase=Mathf.Repeat(arena.Now-started,beam.Duration);int frame=beam.FrameIndex(phase,true);
            float beginning=0;for(int i=0;i<frame;i++)beginning+=beam.seconds[i];
            float blend=(phase-beginning)/beam.seconds[frame];
            glow.sprite=beam.sprites[frame];glow.color=new Color(1,1,1,1-blend);
            nextGlow.sprite=beam.sprites[(frame+1)%beam.sprites.Length];nextGlow.color=new Color(1,1,1,blend);
        }
    }
}
