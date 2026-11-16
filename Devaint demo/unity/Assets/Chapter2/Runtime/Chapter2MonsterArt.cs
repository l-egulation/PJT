using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Integration;
using Deviant.Stage1Monsters;
using Deviant.Stage1Monsters.Game;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class C2ArtClip { public string id; public string[] paths; public int[] milliseconds; public bool loop; }
    [Serializable] public sealed class C2ArtMonster { public string id,name; public C2ArtClip[] clips; }
    [Serializable] public sealed class C2ArtData { public C2ArtMonster[] monsters; }
    public static class Chapter2MonsterArt
    {
        static Stage1MonsterCatalog catalog;
        static readonly Dictionary<string,string> mapping=new Dictionary<string,string> {
            {"01_thorn-root","M3"},{"02_broom-stalker","K4"},{"03_gutter-spitter","R-selected"},
            {"draft_06_stationary_turret","H3"},{"draft_07_heavy_charger","B3"}};
        public static string ArtId(string species)=>mapping.TryGetValue(species,out var id)?id:null;
        public static Stage1MonsterCatalog Load()
        {
            if(catalog)return catalog;
            var json=Resources.Load<TextAsset>("Chapter2MonsterArt");
            if(!json)throw new InvalidOperationException("Missing Chapter2MonsterArt catalog");
            var data=JsonUtility.FromJson<C2ArtData>(json.text);catalog=ScriptableObject.CreateInstance<Stage1MonsterCatalog>();
            var definitions=new List<Stage1MonsterDefinition>();
            foreach(var monster in data.monsters) {
                var clips=new List<Stage1MonsterClip>();
                foreach(var source in monster.clips) {
                    var frames=Array.ConvertAll(source.paths,path=>Resources.Load<Sprite>(path));
                    if(Array.Exists(frames,f=>!f)||frames.Length!=source.milliseconds.Length)throw new InvalidOperationException("Missing Chapter2 art: "+monster.id+"/"+source.id);
                    clips.Add(new Stage1MonsterClip{id=source.id,frames=frames,milliseconds=source.milliseconds,loop=source.loop});
                }
                // Reuse the approved body only for the finishing flash; the shared bridge owns shattering.
                var body=clips.Find(c=>c.id=="idle").frames[0];
                clips.Add(new Stage1MonsterClip{id="death",frames=new[]{body},milliseconds=new[]{80},loop=false});
                definitions.Add(new Stage1MonsterDefinition{id=monster.id,displayName=monster.name,motions=clips.ToArray()});
            }
            catalog.monsters=definitions.ToArray();return catalog;
        }
        public static void Apply(FieldMouseEnemy actor,string species)
        {
            string id=ArtId(species);if(id==null)return;
            var art=Load();var player=actor.visual.GetComponent<Stage1MonsterPlayer>();
            if(player) {
                player.catalog=art;player.monsterId=id;player.Initialize();player.Play("idle",true);
            } else {
                var presentation=actor.gameObject.AddComponent<Chapter2PrototypeArt>();presentation.Bind(actor,art.Find(id));
            }
        }
    }
    // Later chapters reuse the actor's existing combat state; no damage, timing, or collider changes.
    [DefaultExecutionOrder(800)]
    public sealed class Chapter2PrototypeArt : MonoBehaviour
    {
        FieldMouseEnemy actor;Stage1MonsterDefinition definition;Chapter1HitFeedback feedback;
        HitFeedbackBridge deathFeedback;long deathToken;bool deathHandled;float deathAt;
        int hits;float lastNow,hitAt=float.NegativeInfinity,start;string state;
        public void Bind(FieldMouseEnemy target,Stage1MonsterDefinition art) {actor=target;definition=art;hits=actor.ReceivedHitCount;lastNow=start=actor.arena.Now;feedback=new Chapter1HitFeedback(actor.visual);}
        void LateUpdate()
        {
            if(!actor||!actor.arena)return;float now=actor.arena.Now;
            if(now<lastNow||actor.ReceivedHitCount<hits){ClearDeath();hitAt=float.NegativeInfinity;start=now;state=null;feedback.Clear();}
            if(actor.ReceivedHitCount>hits) {
                hitAt=now;feedback.VisualsEnabled=!(actor.arena.FeelFeedback&&actor.arena.FeelFeedback.EffectsEnabled);
                for(int i=hits;i<actor.ReceivedHitCount;i++)feedback.Hit(actor.Center,i+1);
            }
            hits=actor.ReceivedHitCount;lastNow=now;
            if(actor.Dead){
                if(!deathHandled) {
                    deathHandled=true;deathAt=now;deathFeedback=actor.arena.GetComponent<HitFeedbackBridge>();
                    var body=definition.Find("idle").frames[0];actor.visual.sprite=body;actor.visual.flipX=actor.facing>0;
                    if(deathFeedback){deathToken=deathFeedback.BeginDeath(actor,body);deathFeedback.CompleteDeath(actor,deathToken);}
                }
                actor.visual.enabled=now-deathAt<.08f;
                feedback.Draw(now-hitAt,actor.arena.Controls.Current.SimulationPaused);return;
            }
            if(deathHandled)ClearDeath();
            var prototype=actor as Chapter1PrototypeEnemy;
            var normal=actor as Chapter2NormalEnemy;
            bool attacking=normal?normal.Attacking:prototype&&prototype.AttackInProgress;
            string next=now-hitAt<.15f?"hit":attacking?"attack":Mathf.Abs(actor.velocity.x)>.001f?"walk":"idle";
            if(state!=next){state=next;start=now;}
            var clip=definition.Find(state);float age=normal&&state=="attack"?normal.BodyAge:now-start;
            actor.visual.sprite=clip.frames[clip.FrameAt(age)];actor.visual.flipX=actor.facing>0;actor.visual.enabled=true;
            actor.visual.color=Color.white;actor.visual.transform.localScale=Vector3.one;actor.visual.transform.localPosition=Vector3.zero;actor.visual.transform.localRotation=Quaternion.identity;
            feedback.VisualsEnabled=!(actor.arena.FeelFeedback&&actor.arena.FeelFeedback.EffectsEnabled);feedback.Draw(now-hitAt,actor.arena.Controls.Current.SimulationPaused);
        }
        void ClearDeath(){if(deathFeedback&&actor)deathFeedback.CancelDeath(actor,deathToken);deathToken=0;deathHandled=false;}
        void OnDisable(){ClearDeath();feedback?.Clear();hitAt=float.NegativeInfinity;}
        void OnDestroy()=>feedback?.Dispose();
    }
}
