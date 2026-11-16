using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace Deviant.Chapter2
{
    public enum ElitePattern { Rest, Teleport, Combo, Rock }

    // Gameplay and approved presentation use the same arena simulation clock.
    public sealed class Chapter2EliteEnemy : TestEnemy, ICustomPlayerHitReactionSource
    {
        public Chapter2EliteSettings Settings {get;private set;}
        protected override bool ReceivesAutomaticHitReaction=>Settings!=null&&Settings.automaticHitReaction;
        public ElitePattern Pattern {get;private set;}
        public float Age=>arena.Now-began;
        public override void DelayRunInkMotion(float seconds) { if (Pattern != ElitePattern.Rest) began += seconds; }
        public int Direction {get;private set;}
        public int FragmentCount=>stones.Count;
        public bool Flying=>flight;
        public bool BlocksDash=>arena&&arena.Now<wallStunUntil;
        public string ArrivalFailure {get;private set;}
        public bool OverridesPlayerHitReaction=>dedicatedHit;
        public Chapter2ElitePresentation Presentation {get;private set;}
        bool dedicatedHit;
        Sprite placeholder;
        public void SetPlaceholder(Sprite sprite){placeholder=sprite;if(sprite){visual.sprite=sprite;FitBody(false);}}
        void FitBody(bool crouch)
        {
            if(Presentation)return;
            if(!visual)return;
            float factor=crouch?.8f:1;
            if(placeholder)
            {
                float scale=size.y/placeholder.bounds.size.y;
                visual.transform.localScale=new Vector3(scale,scale*factor,1);
                var center=placeholder.bounds.center;
                visual.transform.localPosition=new Vector3(-center.x*scale,size.y*factor/2-center.y*scale*factor,0);
                visual.color=Dead?Color.gray:Color.white;
            }
            else{visual.transform.localScale=new Vector3(size.x,size.y*factor,1);visual.transform.localPosition=new Vector3(0,size.y*factor/2,0);}
        }
        public bool NeedsPursuitTeleport {get;set;}
        public bool ShowRanges=true;
        public Bounds AttackBox {get;private set;}
        public bool AttackActive {get;private set;}
        public string StateLabel=>Dead?"처치 완료":Pattern==ElitePattern.Rest?"휴식":Pattern==ElitePattern.Teleport?"뒤잡기 펀치":Pattern==ElitePattern.Combo?"전진 3연타":"돌 파편";
        readonly List<Stone> stones=new List<Stone>();
        readonly bool[] struck=new bool[3];
        readonly float[] advanced=new float[3];
        System.Random random=new System.Random();
        float began,readyAt,flightEnd,wallStunUntil; int flightDirection;
        bool arrived,teleportChecked,rockFired,rockLocked,flight,flightStarted,resetting;
        Vector2 rockAt,rockAim; SpriteRenderer warning,rock;
        public Vector2 RockPosition=>rockAt;
        public Vector2 RockAim=>rockAim;
        public bool RockLocked=>rockLocked;
        sealed class Stone {public Vector2 at,velocity;public SpriteRenderer view;public CombatActor source;public int artIndex;}
        public Vector2[] FragmentVelocities=>stones.ConvertAll(s=>s.velocity).ToArray();
        public SpriteRenderer[] FragmentViews=>stones.ConvertAll(s=>s.view).ToArray();

        public void Initialize(CombatArena owner,Vector2 at)
        {
            Settings=Chapter2EliteSettings.Load();
            Configure(owner,at,new Vector2(Settings.bodyWidth,Settings.bodyHeight),new Vector2(0,Settings.bodyHeight/2),false);
            spawn=at;maxHealth=Settings.hp;aggressive=true;
            visual=Box("A3 body",new Color(.72f,.57f,.3f),size,Center,12);
            visual.transform.SetParent(transform,true);
            warning=Box("Attack range",Color.clear,Vector2.one,at,5);
            rock=Box("Forming stone",Color.clear,Vector2.one,at,14);
            owner.PlayerStepCompleted+=AfterPlayer;
            owner.BeforePlayerStep+=BeforePlayer;
            owner.Player.DashStarted+=CancelFlight;
            ResetEnemy();
            Presentation=gameObject.AddComponent<Chapter2ElitePresentation>();Presentation.Bind(this);
        }
        SpriteRenderer Box(string label,Color color,Vector2 dimensions,Vector2 at,int order)
        {
            var node=new GameObject(label);node.transform.SetParent(arena.transform,false);
            node.transform.position=at;node.transform.localScale=dimensions;
            var r=node.AddComponent<SpriteRenderer>();r.sprite=arena.TechnicalSprite;r.color=color;r.sortingOrder=order;
            if(arena.spriteMaterial)r.sharedMaterial=arena.spriteMaterial;
            return r;
        }
        void OnDestroy()
        {
            CancelFlight();
            if(arena){arena.PlayerStepCompleted-=AfterPlayer;arena.BeforePlayerStep-=BeforePlayer;if(arena.Player)arena.Player.DashStarted-=CancelFlight;}
            ClearStones();if(warning)Destroy(warning.gameObject);if(rock)Destroy(rock.gameObject);
        }
        public override void ResetEnemy()
        {
            if(Settings==null)return;
            resetting=true;ResetActor(spawn);resetting=false;aggressive=true;
            Pattern=ElitePattern.Rest;readyAt=arena.Now+Settings.rest;began=arena.Now;
            wallStunUntil=0;ClearStones();CancelFlight();ClearAttack();
            visual.color=new Color(.72f,.57f,.3f);visual.transform.localPosition=offset;
            FitBody(false);
            if(Presentation)Presentation.ResetPose();
        }
        public override void CancelAttack()
        {
            base.CancelAttack();
            if(Settings==null||resetting)return;
            Rest();
            if(Presentation)Presentation.ResetPose();
        }
        void ClearAttack()
        {
            AttackActive=false;AttackBox=default;if(warning)warning.enabled=false;if(rock)rock.enabled=false;
            FitBody(false);
        }
        void Rest(){Pattern=ElitePattern.Rest;readyAt=arena.Now+Settings.rest;ClearAttack();}
        public void ActivateEncounter(){aggressive=true;Rest();}
        public void StopEncounter(){aggressive=false;Rest();ClearStones();CancelFlight();if(Presentation)Presentation.ResetPose();}
        public void BeginPattern(ElitePattern pattern)
        {
            if(Dead||arena.Player.Dead||pattern==ElitePattern.Rest)return;
            Pattern=pattern;began=arena.Now;Direction=arena.Player.Center.x>=Center.x?1:-1;SyncFacing(Direction);
            Array.Clear(struck,0,3);Array.Clear(advanced,0,3);arrived=false;teleportChecked=false;rockFired=false;rockLocked=false;
            rockAt=new Vector2(Position.x+Direction*Settings.rockAhead,Position.y+Settings.rockHeight);
            ClearAttack();
            if(Presentation)Presentation.Begin(pattern);
        }
        public override void Step(float dt)
        {
            if(Dead||arena.Player.Dead){ClearAttack();ClearStones();CancelFlight();return;}
            StepStones(dt);
            if(!aggressive)return;
            PhysicsStep(dt,0,false);
            if(arena.Now<stunUntil||arena.Now<staggerUntil)return;
            if(Pattern==ElitePattern.Rest)
            {
                if(arena.Now>=readyAt)
                {
                    // Uniform random among eligible patterns, with no forced rotation or repeat restriction.
                    if(NeedsPursuitTeleport)
                    {
                        if(arena.Player.grounded)BeginPattern(ElitePattern.Teleport);
                        return;
                    }
                    int selected=arena.Player.grounded?random.Next(3)+1:random.Next(2)+2;
                    BeginPattern((ElitePattern)selected);
                }
                return;
            }
            if(Pattern==ElitePattern.Teleport)Teleport();
            else if(Pattern==ElitePattern.Combo)Combo(dt);
            else Rock(dt);
            Render();
        }
        void Teleport()
        {
            if(!teleportChecked&&Age>=Settings.teleportCast)
            {
                teleportChecked=true;
                if(!arena.Player.grounded||!TryArrival(out var destination,out var direction)){Rest();if(Presentation)Presentation.ResetPose();return;}
                var departure=Position;Position=destination;velocity=Vector2.zero;Direction=direction;SyncFacing(direction);
                Physics2D.SyncTransforms();grounded=ProbeGround();arrived=true;
                if(Presentation)Presentation.Arrive(departure);
            }
            if(!arrived)return;
            float impact=Settings.teleportCast+Settings.punchPrepare;
            AttackBox=ForwardBox(Settings.punchWidth);AttackActive=Age>=impact&&Age<impact+Settings.activeSeconds;
            if(AttackActive&&!struck[0]&&Hit(Settings.punchDamage,0))
            {
                struck[0]=true;StartFlight(Direction);
            }
            if(Age>=impact+Settings.activeSeconds)Rest();
        }
        public bool TryArrival(out Vector2 destination,out int direction)
        {
            Physics2D.SyncTransforms();ArrivalFailure="";
            var p=arena.Player;
            direction=p.facing;
            destination=p.Position;
            if(!p.grounded){ArrivalFailure="Player airborne";return false;}
            if(TryArrivalSide(direction,out destination))return true;
            if(!NeedsPursuitTeleport)return false;
            direction=-direction;
            return TryArrivalSide(direction,out destination);
        }
        bool TryArrivalSide(int direction,out Vector2 destination)
        {
            var p=arena.Player;
            float edge=direction>0?p.hurtCollider.bounds.min.x:p.hurtCollider.bounds.max.x;
            destination=new Vector2(edge-direction*(Settings.arrivalGap+size.x/2),p.Position.y);
            if(Blocked(destination,direction)){ArrivalFailure="Body blocked at "+destination;return false;}
            foreach(float x in new[]{destination.x-size.x/2+.03f,destination.x,destination.x+size.x/2-.03f})
            {
                bool supported=false;
                foreach(var hit in Physics2D.RaycastAll(new Vector2(x,destination.y+.06f),Vector2.down,.16f))
                    if(hit.normal.y>.9f&&hit.collider.GetComponent<ArenaSurface>()){supported=true;break;}
                if(!supported){ArrivalFailure="No support at "+x+", foot "+destination.y;return false;}
            }
            ArrivalFailure="";
            return true;
        }
        Bounds ForwardBox(float width)=>new Bounds(new Vector2(Position.x+Direction*(size.x/2+width/2),Position.y+Settings.attackHeight/2),new Vector2(width,Settings.attackHeight));
        // True only for a landed hit. A parried hit marks its strike as used (so it is countered once) and
        // returns false, so the punch flight / combo pin never follow a parry.
        bool Hit(float damage,int strike)
        {
            Physics2D.SyncTransforms();
            dedicatedHit=true;int parries=arena.Player.ParryCount;
            try
            {
                bool landed=AttackBox.Intersects(arena.Player.hurtCollider.bounds)&&arena.Player.Receive(damage,this,Direction,false,null);
                if(!landed&&arena.Player.ParryCount!=parries)struck[strike]=true;
                return landed;
            }
            finally{dedicatedHit=false;}
        }
        void Combo(float dt)
        {
            float age=Age*Settings.comboSpeed;
            AttackBox=ForwardBox(Settings.comboWidth);AttackActive=false;
            for(int i=0;i<3;i++)
            {
                float target=Settings.comboStep*Mathf.Clamp01((age-(Settings.comboTimes[i]-Settings.comboStepSeconds))/Settings.comboStepSeconds);
                float move=target-advanced[i];
                if(move>0){PhysicsStep(dt,Direction*move/dt,true);advanced[i]=target;}
                float t=Settings.comboTimes[i];
                if(age>=t&&age<t+Settings.activeSeconds)
                {
                    AttackBox=ForwardBox(Settings.comboWidth);AttackActive=true;
                    if(!struck[i]&&Hit(Settings.comboDamage[i],i))
                    {
                        struck[i]=true;
                        var p=arena.Player;p.SetKnockMotion(arena.Now,0);
                        p.TeleportToward(p.Position+Vector2.right*Direction*Settings.comboPush);
                    }
                }
            }
            if(age>=Settings.comboTimes[2]+Settings.activeSeconds)Rest();
        }
        void Rock(float dt)
        {
            float sampleAge=Age+dt; // Arena clock advances after enemy steps.
            // Follow during condensation, then sample once at its completion.
            // The punch preparation and projectile flight never retarget.
            if(!rockLocked)
            {
                Direction=arena.Player.Center.x>=Center.x?1:-1;SyncFacing(Direction);
                rockAt=new Vector2(Position.x+Direction*Settings.rockAhead,Position.y+Settings.rockHeight);
                rockAim=((Vector2)arena.Player.hurtCollider.bounds.center-rockAt).normalized;
                if(rockAim.sqrMagnitude<.0001f)rockAim=Vector2.right*Direction;
                if(sampleAge>=Settings.rockForm)rockLocked=true;
            }
            if(!rockFired)
            {
                rock.enabled=!Presentation;rock.transform.position=rockAt;
                rock.transform.localScale=Vector2.one*Settings.rockSize*Mathf.Clamp01(Age/Settings.rockForm);
                rock.color=new Color(.55f,.6f,.62f);
            }
            if(!rockFired&&sampleAge>=Settings.rockFire)
            {
                rockFired=true;rock.enabled=false;
                Vector2 aim=rockAim;
                for(int i=0;i<Settings.fragmentCount;i++)
                {
                    float degrees=-Settings.fanDegrees/2+i*Settings.fanDegrees/(Settings.fragmentCount-1);
                    var velocity=(Vector2)(Quaternion.Euler(0,0,degrees)*(Vector3)aim)*Settings.fragmentSpeed;
                    var view=Box("A3 stone fragment "+i,Color.white,Vector2.one*Settings.fragmentVisualSize,rockAt,19);
                    if(Presentation)Presentation.ConfigureFragment(view,i,velocity);
                    stones.Add(new Stone{at=rockAt,velocity=velocity,source=this,view=view,artIndex=i});
                }
                Rest();
            }
        }
        void StepStones(float dt)
        {
            for(int i=stones.Count-1;i>=0;i--)
            {
                var s=stones[i];Vector2 delta=s.velocity*dt;float wall=float.PositiveInfinity;
                foreach(var h in Physics2D.BoxCastAll(s.at,Vector2.one*Settings.fragmentColliderSize,0,delta.normalized,delta.magnitude))
                    if(h.collider.GetComponent<ArenaSurface>())wall=Mathf.Min(wall,h.distance);
                // Include target in the same sweep, so high-speed fragments cannot tunnel through it.
                float player=float.PositiveInfinity;
                CombatActor target=s.source.isPlayer?(CombatActor)this:arena.Player;
                foreach(var h in Physics2D.BoxCastAll(s.at,Vector2.one*Settings.fragmentColliderSize,0,delta.normalized,delta.magnitude))
                    if(h.collider==target.hurtCollider)player=Mathf.Min(player,h.distance);
                bool remove=false;
                if(player<wall&&player<=delta.magnitude)
                {
                    if(target.isPlayer&&target.currentCast&&target.currentCast.TryParry(s.source,true,s.at))
                    {s.source=target;s.velocity=-s.velocity;if(Presentation)Presentation.ConfigureFragment(s.view,s.artIndex,s.velocity);continue;}
                    target.Receive(Settings.fragmentDamage,s.source,s.velocity.x>=0?1:-1,false,null,false,true);
                    remove=true; // A contacting fragment is consumed, including an invulnerable dodge.
                }
                else if(wall<=delta.magnitude)remove=true;
                if(remove){Destroy(s.view.gameObject);stones.RemoveAt(i);continue;}
                s.at+=delta;s.view.transform.position=s.at;
            }
        }
        public void StartFlight(int direction)
        {
            flight=true;flightStarted=false;flightDirection=direction;flightEnd=arena.Now+Settings.knockSeconds;
            var p=arena.Player;p.CancelAction();p.HorizontalKnockback=true;p.moveLockUntil=arena.Now;p.VelocityY=0;p.SetKnockMotion(flightEnd,direction*Settings.knockDistance/Settings.knockSeconds);
        }
        void BeforePlayer(float dt)
        {
            if(!flight||flightStarted)return;
            flightStarted=true;flightEnd=arena.Now+Settings.knockSeconds;
            arena.Player.SetKnockMotion(flightEnd,flightDirection*Settings.knockDistance/Settings.knockSeconds);
        }
        void CancelFlight()
        {
            if(!flight)return;flight=false;
            if(arena&&arena.Player){arena.Player.HorizontalKnockback=false;arena.Player.SetKnockMotion(arena.Now,0);arena.Player.VelocityX=0;}
        }
        void AfterPlayer(float dt)
        {
            if(!flight)return;
            var p=arena.Player;
            if(p.Dead||p.Dashing){CancelFlight();return;}
            if(p.wallHit)
            {
                CancelFlight();p.Receive(Settings.wallDamage,this,flightDirection,false,null,true);
                p.SetKnockMotion(arena.Now,0);wallStunUntil=arena.Now+Settings.wallStun;p.stunUntil=Mathf.Max(p.stunUntil,wallStunUntil);
            }
            else if(arena.Now+dt>=flightEnd-.00001f)CancelFlight();
        }
        void ClearStones(){foreach(var s in stones)if(s.view)Destroy(s.view.gameObject);stones.Clear();}
        void Render()
        {
            if(!visual)return;
            visual.color=Dead?Color.gray:new Color(.72f,.57f,.3f);
            bool crouch=Pattern==ElitePattern.Teleport&&Age<Settings.teleportCast;
            FitBody(crouch);
            warning.enabled=ShowRanges&&(Pattern==ElitePattern.Combo||Pattern==ElitePattern.Teleport&&arrived);
            if(warning.enabled)
            {
                warning.transform.position=AttackBox.center;warning.transform.localScale=AttackBox.size;
                warning.color=AttackActive?new Color(1,.18f,.12f,.65f):new Color(1,.75f,.15f,.18f);
            }
        }
    }
}
