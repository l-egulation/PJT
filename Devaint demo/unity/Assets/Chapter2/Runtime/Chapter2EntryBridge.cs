using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Chapter2
{
    // Separate adapter: the existing chapter-one scene and its result buttons stay intact.
    public sealed class Chapter2EntryBridge : MonoBehaviour
    {
        public const string ScenePath="Assets/Chapter2/Scenes/Chapter2Procedural.unity";
        static Chapter2EntryBridge instance;
        Chapter2Carry carry;
        bool pending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Chapter1RunHost.ContinueChapter = TryEnterFromChapterOne;
            Chapter1RunHost.EnterChapterTwoForDebug = TryEnterForDebug;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            if(instance)return;
            var go=new GameObject("Chapter2 entry adapter");DontDestroyOnLoad(go);instance=go.AddComponent<Chapter2EntryBridge>();
        }
        // A failed transition can return to the title before Chapter2World takes its carry. Abandon that
        // pending handoff at the run's lobby boundaries so it cannot block the next run's chapter-two exit.
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if(!instance || scene.path!=Chapter1RunHost.TitleScene && scene.path!=Chapter1RunHost.VillageScene)return;
            instance.pending=false;instance.carry=null;
        }
        static bool TryEnterForDebug()
        {
            if (!DevFeatures.Enabled || !instance || instance.pending || ChapterSceneFade.Busy) return false;
            // A direct debug jump must not reuse an old chapter's equipment, economy or pending carry.
            instance.carry = null;
            var host = Chapter1RunHost.Ensure();
            host.LeaveChapterTwoPending();
            host.Flow.StartDebugChapter(Chapter1RunHost.Now, 2);
            Deviant.Stage1Monsters.Game.Chapter1NewMonsterArt.ReleaseShared();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            return true;
        }
        public static bool TryEnterFromChapterOne()
        {
            var host=Chapter1RunHost.Instance;
            var run=host ? host.Flow.Current : null;
            if(!instance||instance.pending||ChapterSceneFade.Busy||run==null||run.Ended||run.Chapter!=1||!run.BossDefeated||SceneManager.GetActiveScene().path!=Chapter1RunHost.ChapterScene)return false;
            var world=FindAnyObjectByType<ForestCombatWorld>();
            if(!world||!world.Ready||!world.arena.Player||world.arena.Player.Dead)return false;
            var arena=world.arena;
            instance.carry=Chapter2Carry.Capture(arena);
            if(!host.Flow.TryAdvanceChapter(arena.Now)){instance.carry=null;return false;}
            var binding=world.GetComponent<Chapter1RunBinding>();
            if(binding&&binding.ProfileRun){Chapter1ProfileHost.Service.RecordChapterOneClear(run.RunId,false);Chapter1ProfileHost.Service.FlushPigment();}
            instance.pending=true;
            host.LeaveChapterTwoPending();
            var scope=arena.Controls.OpenScope("chapter one to two");
            scope.BlockInput(Deviant.Control.Contracts.InputChannels.Combat|Deviant.Control.Contracts.InputChannels.Interaction|Deviant.Control.Contracts.InputChannels.Navigation);
            scope.PauseSimulation();
            ChapterSceneFade.Load(LoadingPlace.Chapter2,()=>{scope.Dispose();Deviant.Stage1Monsters.Game.Chapter1NewMonsterArt.ReleaseShared();ChapterSceneFade.LoadSceneBehind(ScenePath);},()=>{var next=FindAnyObjectByType<Chapter2World>();return next&&next.Ready;});
            return true;
        }
        // Development reset during the boss fight: carry the present player into the reloaded chapter-two scene.
        public static void Reload(CombatArena arena,string scenePath)
        {
            if(instance&&arena&&arena.Player&&!arena.Player.Dead){instance.carry=Chapter2Carry.Capture(arena);instance.pending=true;}
            SceneManager.LoadScene(scenePath);
        }
        public static Chapter2Carry Take()
        {
            if(!instance||!instance.pending)return null;
            instance.pending=false;var result=instance.carry;instance.carry=null;return result;
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }

    public sealed class Chapter2Carry
    {
        public WeaponLoadoutSnapshot Weapons {get;private set;}
        public float Health {get;private set;} public float CapturedAt {get;private set;}
        public bool[] Ready {get;private set;}
        InkInstance[][] equipped; InkInstance[] bag;
        public bool Restoring {get;private set;}
        public static Chapter2Carry Capture(CombatArena arena)
        {
            var result=new Chapter2Carry{Weapons=arena.Player.Loadout,Health=arena.Player.health,CapturedAt=arena.Now,Ready=new[]{arena.Player.IsWeaponSlotReady(0),arena.Player.IsWeaponSlotReady(1)}};
            var inventory=arena.GetComponent<InventoryRuntimeHost>()?.Inventory;
            if(inventory!=null){result.bag=Enumerable.Range(0,inventory.Capacity).Select(inventory.BagAt).ToArray();result.equipped=Enumerable.Range(0,inventory.WeaponCount).Select(w=>Enumerable.Range(0,inventory.StatSlots+2).Select(s=>inventory.EquippedAt(w,s)).ToArray()).ToArray();}
            return result;
        }
        public IWeaponLoadout CreateLoadout()=>WeaponResources.CreateLoadout(Weapons.Weapons.Select(w=>(IWeaponInstance)new ContinuedWeapon(w,CapturedAt)).ToArray(),Weapons.ActiveSlot);
        public IEnumerator RestoreInventory(CombatArena arena)
        {
            if(bag==null)yield break;
            InventoryRuntimeHost host=null;while(arena&&(!(host=arena.GetComponent<InventoryRuntimeHost>())||host.Inventory==null))yield return null;
            if(!arena)yield break;
            Restoring=true;
            try
            {
                for(int w=0;w<equipped.Length;w++)for(int s=0;s<equipped[w].Length;s++)
                {
                    var item=equipped[w][s];if(item==null)continue;
                    if(!host.Inventory.TryAcquire(item,out var error))throw new InvalidOperationException(error);
                    int slot=Enumerable.Range(0,host.Inventory.Capacity).First(i=>host.Inventory.BagAt(i)==item);
                    if(!host.Inventory.TryEquip(slot,w,s,out error))throw new InvalidOperationException(error);
                }
                foreach(var item in bag)if(item!=null&&!host.Inventory.TryAcquire(item,out var error))throw new InvalidOperationException(error);
            }
            finally{Restoring=false;}
        }
        // Keep weapon identity so bound colored palettes and stamps still address the same weapon.
        sealed class ContinuedWeapon : IWeaponInstance
        {
            readonly IWeaponInstance inner=WeaponResources.CreateWeapon();
            public Guid InstanceId {get;}
            public ContinuedWeapon(WeaponSnapshot snapshot,float oldNow)
            {
                InstanceId=snapshot.InstanceId;inner.Select(snapshot.Selection);inner.Stats.ReplaceSlots(snapshot.Stats.Slots.ToArray());inner.ExperimentalChargeStamp=snapshot.ExperimentalChargeStamp;
                if(snapshot.InkPalette!=null)for(int i=0;i<snapshot.InkPalette.Slots.Count;i++){var p=snapshot.InkPalette.Slots[i];if(p!=null&&!inner.InkPalette.TrySet(i,p.PartId,p.Duplicate,out var error))throw new InvalidOperationException(error);}
                var c=snapshot.Charges;inner.Charges.Reset(c.Capacity,c.Period);
                for(int i=c.Available;i<c.Capacity;i++)inner.Charges.Spend(0);
                if(c.Available<c.Capacity)inner.Charges.Refund(Mathf.Clamp(c.Period-(c.NextReady-oldNow),0,c.Period),0);
            }
            public WeaponSelection ReadSelection()=>inner.ReadSelection();public void Select(WeaponSelection value)=>inner.Select(value);
            public IWeaponStats Stats=>inner.Stats;public IInkPalette InkPalette=>inner.InkPalette;public IWeaponCharges Charges=>inner.Charges;
            public bool ExperimentalChargeStamp {get=>inner.ExperimentalChargeStamp;set=>inner.ExperimentalChargeStamp=value;}
            public WeaponSnapshot Read()=>new WeaponSnapshot(InstanceId,ReadSelection(),Stats.Read(),Charges.Read(),InkPalette.Read(),ExperimentalChargeStamp);
            public WeaponSnapshot Reconfigure(WeaponConfiguration configuration,float now){inner.Reconfigure(configuration,now);return Read();}
        }
    }
}
