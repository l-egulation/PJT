using System;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed class RoseBlueActor : TestEnemy, ICustomPlayerHitReactionSource
    {
        public bool OverridesPlayerHitReaction=>true;
        public Func<bool> DamageAllowed,ControlAllowed;
        public Func<float> HealthFloor;
        public Action Interrupted;
        public Func<CombatHitOrigin,bool> HitAdmission;
        public Action<CombatHitOrigin> HitAccepted;
        protected override bool AcceptsHitOrigin(CombatHitOrigin origin)=>HitAdmission?.Invoke(origin)??true;
        protected override void OnAcceptedHitOrigin(CombatHitOrigin origin)=>HitAccepted?.Invoke(origin);
        public bool PresentationVisible {get=>visual&&visual.enabled;set{if(visual)visual.enabled=value;}}
        protected override bool AcceptsIncomingDamage=>DamageAllowed?.Invoke()??false;
        protected override bool AcceptsIncomingControl=>ControlAllowed?.Invoke()??false;
        protected override float MinimumHealthAfterDamage=>HealthFloor?.Invoke()??0;
        protected override bool ReceivesAutomaticHitReaction=>false;
        public void Initialize(CombatArena arena,Vector2 at,Vector2 dimensions,Sprite sprite,Color tint,float hp,bool boss=true)
        {
            Configure(arena,at,dimensions,new Vector2(0,dimensions.y/2),false);spawn=at;maxHealth=hp;health=hp;isBoss=boss;bodyCollider.enabled=false;aggressive=true;
            var view=new GameObject("Presentation");view.transform.SetParent(transform,false);visual=view.AddComponent<SpriteRenderer>();visual.sprite=sprite?sprite:arena.TechnicalSprite;visual.color=tint;visual.sortingOrder=12;
            if(arena.spriteMaterial)visual.sharedMaterial=arena.spriteMaterial;
            visual.transform.localPosition=offset;var bounds=visual.sprite.bounds;visual.transform.localScale=new Vector3(dimensions.x/bounds.size.x,dimensions.y/bounds.size.y,1);
        }
        public override void Step(float dt) { }
        public override void CancelAttack(){base.CancelAttack();if(AcceptsIncomingControl&&arena.Now<stunUntil)Interrupted?.Invoke();}
        public void SetHittable(bool value){if(hurtCollider)hurtCollider.enabled=value&&!Dead;}
        public override void ResetEnemy(){ResetActor(spawn);bodyCollider.enabled=false;}
    }
}
