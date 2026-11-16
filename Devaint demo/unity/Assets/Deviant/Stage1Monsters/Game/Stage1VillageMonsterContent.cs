using System;
using System.Collections.Generic;
using Deviant.Combat;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    [Serializable] public sealed class VillageMonsterMapping { public string from, to; }
    [Serializable] public sealed class VillageBurrowSpec
    {
        public bool enabled;
        public float meltEnd,warningStart,eruptionStart,damageEnd,cooldown,maxTravel,groundSearch,attackHeight;
        public float[] attackSize;
    }
    // Wind-up cue shared by the village monsters (data/combat/stage1_village_monsters.json).
    [Serializable] public sealed class VillageWindupCue
    {
        public float shakeStepSeconds, shakeWidenFrom, flashSeconds;
        public int[] flashColor;
        // Melee attack kinds whose body reddens from the attack's start to the flash, up to tintMax alpha.
        public string[] tintKinds;
        public int[] tintColor;
        public float tintMax;
    }
    [Serializable] public sealed class VillageMonsterSpec
    {
        public string id, name, attackKind;
        public FieldMouseDefinition definition;
        public float damageStart, damageEnd, projectileSpeed;
        public float[] muzzleOffset;
        public float[] projectileSize;
        public float attackEffectStart=-1;
        public float hookStart, hookPeak, hookReturn, hookReach, hookHeight, hookSize, pullSpeed, pullGap;
        public float hookSpeed=1,clawStart;
        // Wind-up telegraph: the approved hold frame stays on screen prepareHoldExtra seconds longer.
        public int prepareHoldFrame=-1;
        // Game-side size of the approved attack stroke (broom dust), so it reaches the hit box; the art stays as delivered.
        public float attackEffectScale=1;
        public float prepareHoldExtra;
        [NonSerialized] public float appliedHold;
        // The wind-up cue runs from this attack frame to the damage window; -1 for none.
        public int windupCueFrame=-1;
        [NonSerialized] public VillageWindupCue cue;
        public VillageBurrowSpec burrow;
    }
    [Serializable] public sealed class VillageMonsterData
    {
        public string status;
        public VillageMonsterMapping[] legacyMap;
        public VillageWindupCue windupCue;
        public VillageMonsterSpec[] monsters;
        public static VillageMonsterData Parse(string json)
        {
            var data=JsonUtility.FromJson<VillageMonsterData>(json);
            foreach(var spec in data.monsters)spec.cue=data.windupCue;
            return data;
        }
    }

    // Scene opt-in: terrain/catalog authoring and other maps remain unchanged.
    public sealed class Stage1VillageMonsterContent : MonoBehaviour, IChapter1MonsterContent, IChapter1BossArt
    {
        public Stage1MonsterCatalog motions;
        public Stage1MonsterEffectCatalog effects;
        public TextAsset content;
        public VillageMonsterData Data { get; private set; }
        public VillageMonsterSpec Find(string id)
        {
            if(Data==null)LoadContent();
            var spec=Array.Find(Data.monsters, m=>m.id==id);
            if(spec==null || id=="05_clock-executioner")throw new ArgumentException("Village spawn is not approved: "+id);
            return spec;
        }
        void LoadContent()
        {
            Data=VillageMonsterData.Parse(content.text);
            var all=new List<VillageMonsterSpec>(Data.monsters);
            foreach(var spec in Chapter1NewMonsterCatalog.Load().monsters)
                all.Add(new VillageMonsterSpec{id=spec.id,name=spec.name,attackKind=spec.ranged?"prototype-bombard":"prototype-charge",definition=spec.definition});
            Data.monsters=all.ToArray();
        }
        public FieldMouseDefinition Definition(string id)=>Find(id).definition;
        public const string BossId="05_clock-executioner";
        public string BossName=>motions?.Find(BossId)?.displayName ?? "시계탑 집행자";
        // Only the approved base and hit frames exist; other boss motions are not substituted.
        public bool TryGetBossArt(out Sprite idle,out Sprite hit)
        {
            var boss=motions?.Find(BossId);
            idle=boss?.Find("base")?.frames?.Length>0?boss.Find("base").frames[0]:null;
            hit=boss?.Find("hit")?.frames?.Length>0?boss.Find("hit").frames[0]:null;
            return idle;
        }
        public void Prepare(ForestData data)
        {
            // Motions are needed only by scenes that draw chapter-one monsters (CreateEnemy). Chapter 2 reads the species
            // data alone; a serialized catalog there kept every chapter-one monster sprite in memory (Web heap, 2026-11-16).
            if(!content)throw new InvalidOperationException("Village monster content is required.");
            LoadContent();
            if(data.rooms?.Length>0 && data.width>0)
            {
                // The assembled map owns the roaming space. Keep the species' sight, floor and return rules.
                foreach(var monster in Data.monsters)
                {
                    monster.definition.leash=data.width/32f+1;
                    monster.definition.reengageTargetLimit=monster.definition.leash;
                }
            }
            var old=data.monsters;
            var actors=new List<MonsterPlacement>();
            foreach(var placement in data.actors)
            {
                // Boss room and every unknown/boss species stay empty, including future template additions.
                if(data.rooms!=null && Array.Exists(data.rooms,r=>r.id==placement.zone && r.kind=="boss"))continue;
                var mapping=Array.Find(Data.legacyMap,m=>m.from==old[placement.kind].id);
                string id=mapping!=null?mapping.to:old[placement.kind].id;
                int index=Array.FindIndex(Data.monsters,m=>m.id==id);
                if(index<0)continue;
                actors.Add(new MonsterPlacement {kind=index,zone=placement.zone,x=placement.x,y=placement.y,
                    roaming=placement.roaming,roamSeed=placement.roamSeed});
            }
            int elite=Array.FindIndex(Data.monsters,m=>m.id=="04_well-warden");
            // Reuse the authored elite landmark's existing floor; never add new terrain or a boss.
            foreach(var marker in data.landmarks ?? Array.Empty<ForestLandmark>())
                if(marker.kind=="elite" && !actors.Exists(p=>p.zone==marker.zone && p.kind==elite))
                    actors.Add(new MonsterPlacement {kind=elite,zone=marker.zone,x=marker.x,y=marker.y});
            data.actors=actors.ToArray();
            data.monsters=Array.ConvertAll(Data.monsters,s=>new MonsterKind {id=s.id,name=s.name,
                width=128,height=128,baseline=120,bodyWidth=Mathf.RoundToInt(s.definition.bodySize[0]*32),
                bodyHeight=Mathf.RoundToInt(s.definition.bodySize[1]*32),bodyOffset=s.definition.bodyOffset[0],
                ranged=s.attackKind=="spit"||s.attackKind=="prototype-bombard"});
        }
        public FieldMouseEnemy CreateEnemy(CombatArena arena, CombatEnemySpawn request, MonsterKind kind, StaticArenaSurfaceIndex surfaces)
        {
            var spec=Find(kind.id);
            if(!motions)throw new InvalidOperationException("Approved chapter-one monster motions are required to draw "+kind.id+".");
            if(spec.attackKind.StartsWith("prototype-"))
                return Chapter1PrototypeEnemy.Create(this,arena,request,Array.Find(Chapter1NewMonsterCatalog.Load().monsters,s=>s.id==kind.id),surfaces);
            var node=new GameObject(spec.name);node.transform.SetParent(transform,false);
            var actor=node.AddComponent<FieldMouseEnemy>();actor.SurfaceIndex=surfaces;
            // Each elite selects its own execution timings without mutating the shared catalog.
            var definition=spec.attackKind=="hook-claw"?
                JsonUtility.FromJson<FieldMouseDefinition>(JsonUtility.ToJson(spec.definition)):spec.definition;
            actor.InitializeSpecies(arena,request.Position,definition);
            actor.profile=request.Profile;actor.spawn=request.Position;actor.maxHealth=spec.definition.maxHP;
            actor.aggressive=request.Aggressive;actor.isBoss=false;
            if(spec.id=="04_well-warden")Chapter1EnemyRole.Mark(actor,Chapter1EnemyRole.Elite);
            var visual=new GameObject("Approved native monster");visual.transform.SetParent(node.transform,false);
            actor.visual=visual.AddComponent<SpriteRenderer>();actor.visual.sharedMaterial=arena.spriteMaterial;
            actor.visual.sortingOrder=request.SortingOrder;
            var player=visual.AddComponent<Stage1MonsterPlayer>();player.catalog=motions;player.monsterId=spec.id;
            player.tickAutomatically=false;player.autoReturnToIdle=false;player.Initialize();
            actor.ResetEnemy();
            var attack=node.AddComponent<Stage1VillageMonsterAttack>();attack.Bind(actor,spec);
            if(spec.attackKind=="spit")node.AddComponent<Stage1GutterTrajectory>().Bind(attack);
            if(spec.burrow!=null && spec.burrow.enabled)
                attack.Burrow=node.AddComponent<Stage1EliteBurrow>();
            if(attack.Burrow)attack.Burrow.Bind(actor,spec.burrow);
            if(effects)node.AddComponent<Stage1VillageMonsterEffects>().Bind(actor,attack,effects);
            var presentation=node.AddComponent<Stage1VillageMonsterPresentation>();presentation.Bind(actor,player);
            return actor;
        }
    }
}
