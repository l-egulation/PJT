using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ForestCombatV2
{
    // Vanishing wood decks (docs/design/world/stage-gimmicks.md). Standing on one starts a
    // 0.6s fade, then the deck is gone for 2.5s and reforms in 0.35s, waiting while
    // the player still overlaps it. Only the collider is switched; the deck art is
    // whatever the active terrain renderer drew under "Vanishing platform art <index>".
    public sealed class Chapter1VanishingPlatforms : MonoBehaviour
    {
        public const float FadeSeconds=.6f,GoneSeconds=2.5f,ReformSeconds=.35f;
        public enum DeckState{Solid,Fading,Gone,Reform}
        static readonly Color Grey=new Color(.62f,.6f,.57f,1);

        sealed class Deck
        {
            public int index;public BoxCollider2D collider;public float left,right,top;
            public DeckState state;public float timer;
            public readonly List<SpriteRenderer> sprites=new List<SpriteRenderer>();
            public readonly List<Tilemap> tiles=new List<Tilemap>();
            public readonly List<(Transform node,Vector3 home)> nodes=new List<(Transform,Vector3)>();
        }
        readonly List<Deck> decks=new List<Deck>();
        readonly Dictionary<int,Deck> byIndex=new Dictionary<int,Deck>();
        ForestCombatWorld world;
        int collectedFrame=-1,collections;

        public int Count=>decks.Count;
        public DeckState StateOf(int platformIndex)=>byIndex[platformIndex].state;
        public int ArtPiecesOf(int platformIndex){var d=byIndex[platformIndex];return d.sprites.Count+d.tiles.Count;}

        public void Initialize(ForestCombatWorld owner)
        {
            world=owner;
            foreach(var (index,collider) in owner.VanishingSurfaces)
            {
                var p=owner.Data.platforms[index];
                var deck=new Deck{index=index,collider=collider,left=p.x/32f,right=(p.x+p.w)/32f,top=-p.y/32f};
                decks.Add(deck);byIndex[index]=deck;
            }
        }

        // Terrain art is built by several presentations, some after this component starts.
        public void CollectArt()
        {
            foreach(var deck in decks){deck.sprites.Clear();deck.tiles.Clear();deck.nodes.Clear();}
            const string prefix="Vanishing platform art ";
            foreach(var node in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!node.name.StartsWith(prefix,System.StringComparison.Ordinal))continue;
                if(!int.TryParse(node.name.Substring(prefix.Length),out int index)||!byIndex.TryGetValue(index,out var deck))continue;
                deck.nodes.Add((node,node.localPosition));
                deck.sprites.AddRange(node.GetComponentsInChildren<SpriteRenderer>(true));
                var map=node.GetComponent<Tilemap>();if(map)deck.tiles.Add(map);
            }
            collectedFrame=Time.frameCount;collections++;
        }

        void Update()
        {
            if(!world||!world.Ready)return;
            // Once on the first frame, once after late presentations have finished building.
            if(collections==0||collections==1&&Time.frameCount-collectedFrame>=30)CollectArt();
            Step(Time.deltaTime);
        }

        public void Step(float dt)
        {
            var player=world.arena.Player;
            foreach(var deck in decks)
            {
                switch(deck.state)
                {
                    case DeckState.Solid:
                        if(player&&Standing(player,deck)){deck.state=DeckState.Fading;deck.timer=FadeSeconds;}
                        break;
                    case DeckState.Fading:
                        if((deck.timer-=dt)<=0){deck.state=DeckState.Gone;deck.timer=GoneSeconds;deck.collider.enabled=false;}
                        break;
                    case DeckState.Gone:
                        if((deck.timer-=dt)<=0){deck.state=DeckState.Reform;deck.timer=ReformSeconds;}
                        break;
                    case DeckState.Reform:
                        deck.timer-=dt;
                        if(deck.timer<=0&&!(player&&Overlaps(player,deck))){deck.state=DeckState.Solid;deck.timer=0;deck.collider.enabled=true;}
                        break;
                }
                Paint(deck);
            }
        }

        static bool Standing(CombatPlayer player,Deck deck)
        {
            var feet=player.Position;
            return player.grounded&&Mathf.Abs(feet.y-deck.top)<.1f&&feet.x+.3f>deck.left&&feet.x-.3f<deck.right;
        }
        static bool Overlaps(CombatPlayer player,Deck deck)
        {
            var feet=player.Position;
            return feet.x+.47f>deck.left&&feet.x-.47f<deck.right&&feet.y<deck.top+.06f&&feet.y+2>deck.top-.5f;
        }

        void Paint(Deck deck)
        {
            Color color;float jitter=0;
            switch(deck.state)
            {
                case DeckState.Fading:
                {
                    float k=1-deck.timer/FadeSeconds;
                    color=Color.Lerp(Color.white,Grey,k);color.a=1-k*.65f;
                    jitter=Mathf.Round((Random.value-.5f)*2.5f*k)/32f;
                    break;
                }
                case DeckState.Gone:color=Grey;color.a=.14f;break;
                case DeckState.Reform:color=Grey;color.a=Mathf.FloorToInt(Time.time*24)%2==0?.6f:.2f;break;
                default:color=Color.white;break;
            }
            foreach(var sprite in deck.sprites)if(sprite)sprite.color=color;
            foreach(var map in deck.tiles)if(map)map.color=color;
            foreach(var (node,home) in deck.nodes)if(node)node.localPosition=home+new Vector3(jitter,0,0);
        }
    }
}
