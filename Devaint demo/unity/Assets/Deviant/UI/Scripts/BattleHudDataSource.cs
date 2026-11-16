using System;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;
namespace Deviant.UI
{
    [Serializable] public class BattleHudState
    {
        public int hp=70,maxHP=100,exp=40,maxEXP=100,level=3;
        public string weaponName="모션 테스트";
        public string region="숲 · 모션 테스트";
        public int bagUsed=0,bagCapacity=15;
    }
    // Visual test data only; no economy, mixing, equipment or permanent save rules.
    public interface IBattleHudDataSource { BattleHudState Read(); }
    public class BattleHudDataSource : MonoBehaviour,IBattleHudDataSource
    {
        public Deviant.Combat.CombatActor liveHealth;
        public BattleHudState demo=new BattleHudState();
        ICombatReadModel combatReads;
        CombatActorHandle actorHandle;
        CombatArena boundArena;
        CombatActor boundActor;
        public bool UsesDemoHP => ReferenceEquals(liveHealth, null) && combatReads == null;
        void OnEnable() => BindLiveHealth();
        public void BindLiveHealth()
        {
            UnbindArena();
            boundActor = liveHealth;
            combatReads = null; actorHandle = default;
            if (!boundActor || !boundActor.arena) return;
            boundArena = boundActor.arena;
            combatReads = boundArena.ReadModel;
            boundArena.Initialized += BindActorHandle;
            boundArena.ResetCompleted += BindActorHandle;
            BindActorHandle();
        }
        void BindActorHandle()
        {
            actorHandle = default;
            if (boundArena && ReferenceEquals(liveHealth, boundActor)) boundArena.TryBindActor(boundActor, out actorHandle);
        }
        void OnDisable() => UnbindArena();
        void UnbindArena()
        {
            if (boundArena)
            {
                boundArena.Initialized -= BindActorHandle;
                boundArena.ResetCompleted -= BindActorHandle;
            }
            boundArena = null;
        }
        public BattleHudState Read()
        {
            if (!UsesDemoHP)
            {
                if (ReferenceEquals(liveHealth, boundActor) && combatReads != null && combatReads.TryReadActor(actorHandle, out var actor))
                {
                    demo.hp = Mathf.CeilToInt(actor.Vitals.Health);
                    demo.maxHP = Mathf.CeilToInt(actor.Vitals.Maximum);
                    if (actor.IsPlayer) demo.weaponName = actor.ActiveWeaponName;
                }
                else
                {
                    demo.hp = demo.maxHP = 0;
                    demo.weaponName = null;
                }
            }
            return demo;
        }
    }
}
