using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class BombProjectile : MonoBehaviour
    {
        public CombatArena arena;
        public CombatActor owner;
        public BombProjectileDefinition Definition { get; private set; }
        public Vector2 velocity;
        public float damage;
        public float fragmentMultiplier=1;
        public bool approvedGrenade;
        Vector2? impactContact;
        Transform approvedVisual;
        // A bomb this wide or wider (the large-bomb skill throws radius 0.4; the basic bomb is 0.2) flies as the
        // dedicated round "bomb_large" art, drawn to the size of the bomb held in its throw motion. Drawing only.
        public const float LargeArtRadius=.4f;
        internal CombatHitOrigin HitOrigin { get; private set; }
        public Vector2 Position { get { return transform.position; } set { transform.position=value; } }
        ContactBombExecution contact;

        public static BombProjectile Spawn(CombatArena arena,CombatActor owner,Vector2 origin,Vector2 velocity,BombData data,float damage,CombatHitOrigin hitOrigin=default)
            => Spawn(arena,owner,origin,velocity,new BombProjectileDefinition(data),damage,hitOrigin);
        public static BombProjectile Spawn(CombatArena arena,CombatActor owner,Vector2 origin,Vector2 velocity,BombProjectileDefinition definition,float damage,CombatHitOrigin hitOrigin=default,bool approvedGrenade=false)
        {
            // Chosen from the authored radius, before ink range growth below changes the hit geometry.
            bool largeArt=definition.Radius>=LargeArtRadius-.0001f;
            if(arena.MvpGrowth!=null&&owner.isPlayer&&hitOrigin.Kind!=CombatHitKind.Fragment){definition=definition.WithRadius(arena.MvpGeometry(hitOrigin,true));}
            var b=new GameObject("ContactBomb").AddComponent<BombProjectile>();
            b.arena=arena; b.owner=owner; b.Position=origin; b.velocity=velocity; b.Definition=definition; b.damage=damage;
            b.HitOrigin = (hitOrigin.IsSpecified ? hitOrigin : arena.DirectHitOrigin(owner)).WithEffect(System.Guid.NewGuid(), hitOrigin.Kind == CombatHitKind.Fragment ? CombatHitKind.Fragment : CombatHitKind.Blast);
            b.contact.Initialize(arena.Now,definition.Lifetime); arena.bombs.Add(b);
            b.approvedGrenade=approvedGrenade || definition.FragmentCount>0;
            if(b.approvedGrenade && arena.RangedPresentation!=null && arena.RangedPresentation.GrenadeProjectile(b.transform,approvedGrenade,velocity))
                b.approvedVisual=b.transform.Find("ApprovedGrenadeProjectile");
            else if(!(largeArt && ApprovedThrownVisual.TryAttach(b.transform,arena,"bomb_large")) && !ApprovedThrownVisual.TryAttach(b.transform,arena,"bomb"))
            {
                b.Sprite(arena.CircleSprite,arena.spriteMaterial,new Color(.16f,.12f,.10f),definition.Radius*2,0);
                b.Sprite(arena.CircleSprite,arena.spriteMaterial,new Color(1,.45f,.05f),definition.Radius*.8f,1);
            }
            BombFuseVisual.TryAttach(b);
            arena.Record("bomb launched damage="+damage.ToString("0.##")); return b;
        }
        void Sprite(Sprite sprite,Material material,Color color,float size,int order)
        {
            var child=new GameObject("BombVisual"); child.transform.SetParent(transform,false); child.transform.localScale=Vector3.one*size;
            var r=child.AddComponent<SpriteRenderer>(); r.sprite=sprite; r.color=color; r.sortingOrder=20+order;
            if(material) r.sharedMaterial=material;
        }
        bool Obstacle(Collider2D c,Vector2 movement,Vector2 normal)
        {
            var host=new ContactObstacleHost(this,c,movement,normal);
            return ContactBombSweepExecution.IsObstacle(ref host);
        }
        public void SweepTo(Vector2 destination)
        {
            var host=new ContactSweepHost(this,destination);
            ContactBombSweepExecution.Sweep(ref host);
        }
        public void Step(float dt)
        {
            if(timed.Stopped) return;
            if(!owner || owner.Dead) { Remove(); return; }
            if(Definition.FuseSeconds>0) { StepTimed(dt); return; }
            var host=new ContactBombHost(this);
            contact.Step(dt,ref host);
            if(approvedVisual)approvedVisual.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
        }
        struct ContactBombHost : IContactBombHost
        {
            readonly BombProjectile bomb;
            public ContactBombHost(BombProjectile bomb) { this.bomb=bomb; }
            public float Now => bomb.arena.Now;
            public bool Stopped => bomb.timed.Stopped;
            public bool ExplodeOnExpiry => bomb.Definition.ExplodeOnExpiry;
            public float Gravity => bomb.Definition.Gravity;
            public float VelocityX => bomb.velocity.x;
            public float VelocityY => bomb.velocity.y;
            public float PositionX => bomb.Position.x;
            public float PositionY => bomb.Position.y;
            public void SetVelocityY(float velocity) => bomb.velocity.y=velocity;
            public void SweepTo(float x,float y) => bomb.SweepTo(new Vector2(x,y));
            public void Explode() => bomb.Explode();
            public void Remove() => bomb.Remove();
        }
        void Explode()
        {
            if(!timed.Stop()) return;
            ExplodeStopped();
        }
        void ExplodeStopped()
        {
            var host=new BlastHost(this);
            BombBlastExecution.ExplodeStopped(ref host);
        }
        public void Release() => Remove();
        void Remove() { timed.Stop(); arena.bombs.Remove(this); Destroy(gameObject); }
        void OnDestroy() { timed.Stop(); }
        void OnDrawGizmos()
        { if(arena && arena.showBoxes) { Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(Position,Definition.Radius); } }
    }

    public class BombBurst : MonoBehaviour
    {
        public CombatArena arena;
        public float radius,started,boxSize;
        public SpriteRenderer visual;
        public void Step()
        {
            float t=(arena.Now-started)/.18f;
            if(t>=1) { arena.bombBursts.Remove(this); Destroy(gameObject); return; }
            transform.localScale=Vector3.one*(boxSize > 0 ? boxSize : radius*2*Mathf.Lerp(.8f,1,t));
            visual.color=new Color(1,.35f,.03f,.4f*(1-t));
        }
    }
}
