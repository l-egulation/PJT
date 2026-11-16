using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        sealed class InkWorldEffect
        {
            public string Kind;
            public InkHit Cause;
            public InkPartMount Mount;
            public InkPartCatalog.Effect Effect;
            public Vector2 Position;
            public float Until, Distance, Damage, Amount;
            public int Direction;
            public GameObject View;
            public readonly HashSet<long> HitLives=new HashSet<long>();
        }
        readonly List<InkWorldEffect> inkWorld=new List<InkWorldEffect>();
        Vector2 inkMoveFrom;
        bool inkMoveEligible;
        internal void BeginInkMovement()
        { inkMoveFrom=Player.Position;inkMoveEligible=InkVoluntaryMovement(); }
        bool InkVoluntaryMovement() => Player && !Player.Dead && (Player.Dashing || Player.axis!=0 && (!Player.currentCast || Player.currentCast.IsInkPreparing)) &&
            Now>=Player.knockUntil && Now>=Player.moveLockUntil && Now>=Player.stunUntil && Now>=Player.rootUntil;
        internal void EndInkMovement()
        {
            if(!inkMoveEligible && !Player.Dashing || !InkVoluntaryMovement())return;
            float distance=Mathf.Abs(Player.Position.x-inkMoveFrom.x);if(distance<=0)return;
            if(MvpGrowth!=null){MvpMoved(distance);return;}
            var w=InkWeapon(LastStartedInkWeaponId);if(w==null)return;
            var palette=w.InkPalette.Read();
            foreach(var mount in palette.Slots)
            {
                if(mount==null)continue;var part=InkPartCatalog.Find(mount.PartId);if(part==null || part.trigger!="move")continue;
                var state=inkStates.GetOrCreateValue(mount);
                foreach(var fx in part.effects)
                {
                    state.Distance+=distance;
                    if(state.Distance<fx.distance || Now<state.ReadyAt)continue;
                    while(state.Distance>=fx.distance)
                    {
                        state.Distance-=fx.distance;state.ReadyAt=Now+part.cooldown;
                        if(fx.op=="movement_wave")state.Stored=Mathf.Min((int)fx.cap,state.Stored+1);
                        else if(fx.op=="distance_windup")state.Stored=1;
                        else
                        {
                            var origin=InkOrigin(w,palette,"ink_move",false);
                            foreach(var enemy in InkNearby(Player.Center,fx.radius,null,fx.op=="distance_mark"?1:0))
                            {
                                var hit=InkSyntheticHit(origin,enemy);
                                DealInk(hit,mount,fx.damage);
                                if(fx.op=="distance_mark")AddInkMarks(hit,fx.stacks);
                                else AddInkSlow(enemy,fx.amount,fx.duration);
                            }
                        }
                        if(part.cooldown>0)break;
                    }
                }
            }
        }
        CombatHitOrigin InkOrigin(WeaponState w,InkPaletteSnapshot palette,string id,bool basic,Guid execution=default)
        {
            TryBindActor(Player,out var source);
            return new CombatHitOrigin(source,default,execution==Guid.Empty?Guid.NewGuid():execution,0,id,Guid.Empty,CombatHitKind.InkPart,
                stamp:new CombatAttackStamp(w.InstanceId,catalog.forms[w.FormIndex].id,id,"experimental",basic,-1,palette),sourceFacing:Player.facing);
        }
        InkHit InkSyntheticHit(CombatHitOrigin origin,CombatActor target)
        {
            TryBindActor(target,out var handle);var snapshot=target.Vitals;
            return new InkHit(new CombatAcceptedHit(0,SimulationTickCount,Now,origin,handle,0,snapshot,new ActorDamageResult(true,0,0,snapshot),target.Kinematics),target,Player);
        }
        GameObject InkView(string name,Vector2 center,Vector2 size,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=center;
            var sprite=go.AddComponent<SpriteRenderer>();sprite.sprite=TechnicalSprite;sprite.color=color;sprite.sortingOrder=19;
            if(spriteMaterial)sprite.sharedMaterial=spriteMaterial;
            if(TechnicalSprite)go.transform.localScale=new Vector3(size.x/TechnicalSprite.bounds.size.x,size.y/TechnicalSprite.bounds.size.y,1);
            return go;
        }
        void SpawnInkWave(InkHit hit,InkPartMount mount,float damage,float range)
        {
            var fx=new InkWorldEffect{Kind="wave",Cause=hit,Mount=mount,Position=hit.Target==Player?Player.Center:hit.Target.Center,
                Direction=hit.Hit.Origin.SourceFacing==0?Player.facing:hit.Hit.Origin.SourceFacing,Damage=damage,Distance=range,Until=Now+range/InkPartCatalog.Settings.waveSpeed+1};
            fx.View=InkView("Ink wave",fx.Position,new Vector2(InkPartCatalog.Settings.waveWidth,InkPartCatalog.Settings.waveHeight),new Color(1,.5f,.2f,.8f));inkWorld.Add(fx);
        }
        void SpawnInkField(InkHit hit,InkPartMount mount,InkPartCatalog.Effect effect)
        {
            var fx=new InkWorldEffect{Kind="field",Cause=hit,Mount=mount,Effect=effect,Position=hit.Target.Center,Until=Now+effect.duration};
            fx.View=InkView("Ink frost field",fx.Position,Vector2.one*effect.radius*2,new Color(.2f,.7f,1,.18f));inkWorld.Add(fx);
        }
        void SpawnInkSeed(Vector2 position,float heal)
        {
            var fx=new InkWorldEffect{Kind="seed",Position=position,Amount=heal,Until=Now+InkPartCatalog.Settings.seedLifetime};
            fx.View=InkView("Ink healing seed",position,Vector2.one*.25f,new Color(.3f,1,.4f));inkWorld.Add(fx);
        }
        float inkWorldLastTime;
        void StepInkWorld()
        {
            float dt=Mathf.Max(0,Now-inkWorldLastTime);inkWorldLastTime=Now;
            for(int i=inkWorld.Count-1;i>=0;i--)
            {
                var fx=inkWorld[i];bool remove=Now>=fx.Until || !Player || Player.Dead;
                if(fx.Kind!="seed" && (!fx.Cause.Source || fx.Cause.Source.LifeVersion!=fx.Cause.SourceLife))remove=true;
                if(!remove && fx.Kind=="seed")
                { if(Vector2.Distance(Player.Center,fx.Position)<=InkPartCatalog.Settings.seedPickupRadius){HealInk(fx.Amount);remove=true;} }
                else if(!remove && fx.Kind=="field")
                { foreach(var enemy in InkNearby(fx.Position,fx.Effect.radius))AddInkSlow(enemy,fx.Effect.amount,fx.Effect.duration); }
                else if(!remove && fx.Kind=="wave")
                {
                    float travel=Mathf.Min(fx.Distance,dt*InkPartCatalog.Settings.waveSpeed);var next=fx.Position+Vector2.right*fx.Direction*travel;
                    foreach(var contact in Physics2D.LinecastAll(fx.Position,next))
                    { var surface=contact.collider.GetComponent<ArenaSurface>();if(surface && !surface.oneWay){next=contact.point;remove=true;break;} }
                    var box=new Bounds((fx.Position+next)*.5f,new Vector3(Mathf.Abs(next.x-fx.Position.x)+InkPartCatalog.Settings.waveWidth,InkPartCatalog.Settings.waveHeight,1));
                    foreach(var enemy in enemies)
                    {
                        if(!enemy || enemy.Dead || enemy.isDecoy)continue;
                        long life=((long)enemy.ActorId<<32)|(uint)enemy.LifeVersion;
                        if(fx.HitLives.Contains(life) || !enemy.HurtOverlaps(box))continue;
                        fx.HitLives.Add(life);DealInk(Retarget(fx.Cause,enemy),fx.Mount,fx.Damage,fx.Cause.Hit.Origin.InkGeneration);
                    }
                    fx.Position=next;fx.Distance-=travel;if(fx.View)fx.View.transform.position=next;if(fx.Distance<=0)remove=true;
                }
                if(remove){if(fx.View)Destroy(fx.View);inkWorld.RemoveAt(i);}
            }
        }
        public IEnumerable<Bounds> InkDebugBoxes()
        {
            foreach(var fx in inkWorld)
            {
                if(fx.Kind=="wave")yield return new Bounds(fx.Position,new Vector3(InkPartCatalog.Settings.waveWidth,InkPartCatalog.Settings.waveHeight,1));
                if(fx.Kind=="field")yield return new Bounds(fx.Position,new Vector3(fx.Effect.radius*2,fx.Effect.radius*2,1));
            }
        }
        void ClearInkWorld()
        {
            foreach(var fx in inkWorld)if(fx.View)Destroy(fx.View);inkWorld.Clear();inkWorldLastTime=Now;
            inkShields.Clear();inkGuards.Clear();
        }
    }
}
