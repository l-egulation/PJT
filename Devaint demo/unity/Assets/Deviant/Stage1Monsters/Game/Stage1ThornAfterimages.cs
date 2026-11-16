using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    public sealed class Stage1ThornAfterimages : MonoBehaviour
    {
        struct Sample { public float time;public Vector3 at;public Sprite sprite;public bool mirror; }
        readonly List<Sample> history=new List<Sample>();
        readonly SpriteRenderer[] trails=new SpriteRenderer[3];
        Stage1VillageMonsterAttack attack;
        public void Bind(Stage1VillageMonsterAttack owner)
        {
            attack=owner;
            for(int i=0;i<3;i++)
            {
                var node=new GameObject("Thorn afterimage");node.transform.SetParent(transform,false);
                var renderer=node.AddComponent<SpriteRenderer>();renderer.sharedMaterial=owner.Actor.visual.sharedMaterial;
                renderer.sortingLayerID=owner.Actor.visual.sortingLayerID;renderer.sortingOrder=owner.Actor.visual.sortingOrder-1;
                renderer.color=new Color(1,1,1,i==0?.38f:i==1?.22f:.10f);renderer.enabled=false;trails[i]=renderer;
            }
        }
        public void Draw()
        {
            foreach(var trail in trails)if(trail)trail.enabled=false;
            var actor=attack.Actor;float age=attack.MotionAge,now=actor.arena.Now;
            if(actor.Dead||!actor.SpeciesAttackVisible||age<attack.Spec.damageStart||age>attack.Spec.damageEnd+.12f)
            {history.Clear();return;}
            if(history.Count==0||now>history[history.Count-1].time)
                history.Add(new Sample{time=now,at=actor.visual.transform.position,sprite=actor.visual.sprite,mirror=actor.visual.flipX});
            while(history.Count>0&&history[0].time<now-.2f)history.RemoveAt(0);
            for(int i=0;i<3;i++)for(int n=history.Count-1;n>=0;n--)
                if(history[n].time<=now-.04f*(i+1))
                {var sample=history[n];trails[i].sprite=sample.sprite;trails[i].flipX=sample.mirror;trails[i].transform.position=sample.at;trails[i].enabled=true;break;}
        }
        void OnDisable(){history.Clear();foreach(var trail in trails)if(trail)trail.enabled=false;}
    }
}
