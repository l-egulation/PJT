using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace Stage1VillageExperiment
{
    public sealed class VillageSpringPad : MonoBehaviour
    {
        [Serializable] public sealed class Tuning
        {
            public float deckHeightPixels,deckWidthPixels,compressionPixels,compressionSeconds,holdSeconds;
            public float reboundPixels,reboundSeconds,settleSeconds,cooldownSeconds,launchHeightPixels;
            public float springBasePixels,springTopPixels;
        }
        public enum PadState { Idle, Compressing, Rebounding, Settling, Cooldown }
        public PadState State { get; private set; }
        public int ActivationCount { get; private set; }
        public int LaunchCount { get; private set; }
        public float OffsetPixels { get; private set; }
        public float DeckWorldY => transform.position.y+(tuning.deckHeightPixels+OffsetPixels)/32;
        public float LaunchVelocity => Mathf.Sqrt(2*arena.Movement.RiseGravity*tuning.launchHeightPixels/32);
        public float ExpectedRisePixels => tuning.launchHeightPixels;
        public BoxCollider2D Deck { get; private set; }
        public CombatPlayer Rider { get; private set; }
        CombatArena arena;
        Tuning tuning;
        Mesh mesh;
        Vector3[] sourceVertices,vertices;
        Vector2 previousFeet;
        float previousVelocity,timer,settleStart;
        bool releasePending,bound;

        public void Initialize(CombatArena owner,VillageJumpPadArtwork.Layout layout,Tuning motion,Material material,int order,ICollection<UnityEngine.Object> owned)
        {
            arena=owner;tuning=motion;
            if(!arena||motion.compressionSeconds<=0||motion.reboundSeconds<=0||motion.settleSeconds<=0||motion.launchHeightPixels<=0||
               motion.springTopPixels<=motion.springBasePixels||motion.compressionPixels>=motion.springTopPixels-motion.springBasePixels)
                throw new InvalidOperationException("Invalid jump-pad tuning.");
            float[] rows={-layout.canvasHeight+layout.anchorY,motion.springBasePixels,motion.springTopPixels,layout.anchorY};
            sourceVertices=new Vector3[8];vertices=new Vector3[8];var uv=new Vector2[8];var colors=new Color[8];var indices=new int[18];
            for(int row=0;row<4;row++)for(int side=0;side<2;side++)
            {
                int n=row*2+side;sourceVertices[n]=new Vector3((side*layout.canvasWidth-layout.anchorX)/32,rows[row]/32,0);
                uv[n]=new Vector2(side,(rows[row]+layout.canvasHeight-layout.anchorY)/layout.canvasHeight);colors[n]=Color.white;
            }
            for(int row=0;row<3;row++){int n=row*2,k=row*6;indices[k]=n;indices[k+1]=n+2;indices[k+2]=n+1;indices[k+3]=n+1;indices[k+4]=n+2;indices[k+5]=n+3;}
            mesh=new Mesh{name="Whole pad drawing - fixed base, elastic springs, rigid deck"};mesh.vertices=sourceVertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=indices;mesh.RecalculateBounds();owned.Add(mesh);
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            var deckNode=new GameObject("One-way moving pressure deck");deckNode.transform.SetParent(transform,false);
            deckNode.AddComponent<ArenaSurface>().oneWay=true;
            Deck=deckNode.AddComponent<BoxCollider2D>();Deck.size=new Vector2(motion.deckWidthPixels/32,.125f);
            SetOffset(0);
            OnEnable();
        }
        void OnEnable()
        {
            if(!arena||bound)return;
            arena.BeforePlayerStep+=BeforeStep;arena.PlayerStepCompleted+=AfterStep;arena.ResetCompleted+=ResetPad;bound=true;
        }
        void SetOffset(float value)
        {
            OffsetPixels=value;
            for(int i=0;i<sourceVertices.Length;i++)
            {
                vertices[i]=sourceVertices[i];float y=sourceVertices[i].y*32;
                vertices[i].y+=value*Mathf.Clamp01((y-tuning.springBasePixels)/(tuning.springTopPixels-tuning.springBasePixels))/32;
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();
            Deck.transform.localPosition=new Vector3(0,(tuning.deckHeightPixels+value)/32-.0625f,0);
            Physics2D.SyncTransforms();
        }
        bool OverDeck(CombatPlayer player)
        {
            float center=player.Center.x,half=player.size.x*.5f;
            return center+half>Deck.bounds.min.x+.04f&&center-half<Deck.bounds.max.x-.04f;
        }
        void BeforeStep(float dt)
        {
            var player=arena.Player;previousFeet=player.Position;previousVelocity=player.VelocityY;
            if(player.Dead){ResetPad();return;}
            if(State==PadState.Idle)return;
            float oldTop=DeckWorldY;
            timer+=dt;
            if(State==PadState.Compressing)
            {
                float t=Mathf.Clamp01(timer/tuning.compressionSeconds);t=t*t*(3-2*t);
                SetOffset(-tuning.compressionPixels*t);
                if(Rider&&Mathf.Abs(Rider.Position.y-oldTop)<.18f&&OverDeck(Rider))
                    Rider.Position+=new Vector2(0,DeckWorldY-oldTop);
                releasePending=timer>=tuning.compressionSeconds+tuning.holdSeconds;
            }
            else if(State==PadState.Rebounding)
            {
                float t=Mathf.Clamp01(timer/tuning.reboundSeconds);t=1-(1-t)*(1-t);
                SetOffset(Mathf.Lerp(-tuning.compressionPixels,tuning.reboundPixels,t));
                if(timer>=tuning.reboundSeconds){State=PadState.Settling;timer=0;settleStart=OffsetPixels;}
            }
            else if(State==PadState.Settling)
            {
                float t=Mathf.Clamp01(timer/tuning.settleSeconds);SetOffset(settleStart*(1-t)*(1-t));
                if(timer>=tuning.settleSeconds){SetOffset(0);State=PadState.Cooldown;timer=0;}
            }
            else if(timer>=tuning.cooldownSeconds){State=PadState.Idle;timer=0;}
        }
        void AfterStep(float dt)
        {
            var player=arena.Player;
            if(player.Dead)return;
            if(State==PadState.Idle)
            {
                // Only downward contact at the top; sides and underside cannot arm it.
                // The native ground probe may settle up to ~.075 units above a face.
                // Accept that grounded skin as well as a swept, exact-foot landing.
                float gap=player.Position.y-DeckWorldY;
                bool contact=(player.grounded&&gap<=.10f)||Mathf.Abs(gap)<.012f;
                if(contact&&previousVelocity<=.01f&&previousFeet.y>=DeckWorldY-.06f&&
                   gap>=-.06f&&OverDeck(player))
                {Rider=player;State=PadState.Compressing;timer=0;ActivationCount++;}
            }
            if(State!=PadState.Compressing)return;
            if(!Rider||!OverDeck(Rider)||Mathf.Abs(Rider.Position.y-DeckWorldY)>1)
            {Rider=null;State=PadState.Settling;timer=0;settleStart=OffsetPixels;releasePending=false;return;}
            Rider.Position=new Vector2(Rider.Position.x,DeckWorldY);Rider.VelocityY=0;Rider.grounded=true;
            if(!releasePending)return;
            // One environmental impulse, after player input/motor. No global jump profile edit.
            Rider.CancelAction();Rider.jumps.Reset();Rider.grounded=false;
            Rider.Position+=new Vector2(0,.025f);Rider.VelocityY=LaunchVelocity;
            Rider=null;releasePending=false;LaunchCount++;State=PadState.Rebounding;timer=0;
        }
        public void ResetPad()
        {
            Rider=null;releasePending=false;timer=0;State=PadState.Idle;
            if(mesh&&Deck)SetOffset(0);
        }
        void OnDisable()
        {
            if(!bound)return;
            arena.BeforePlayerStep-=BeforeStep;arena.PlayerStepCompleted-=AfterStep;arena.ResetCompleted-=ResetPad;
            ResetPad();bound=false;
        }
    }
}
