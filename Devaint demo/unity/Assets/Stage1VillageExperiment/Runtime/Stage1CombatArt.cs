using System;
using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace Stage1VillageExperiment
{
    // Approved stage-1 combat art: 32px lawn/soil tiles and wood decks (resources
    // environments/stage1-pixel-lawn-wood-32), the separated native background
    // (stage1-pixel-background-native-20261111) and the seeded sky with paper ash
    // (stage1-atmosphere-sky-ash-20261111). Only renderers change: colliders, map
    // data, actors and the village hub / dummy training rooms are left alone.
    public sealed class Stage1CombatArt : MonoBehaviour
    {
        const string Art="Stage1Combat/";
        // Tile tops are drawn 8px above the collision contact so the grass rises over the walking line.
        public const float SurfaceLift=.25f;
        public static readonly Color32 Paper=new Color32(252,247,238,255);   // the floor's cream sky (#FCF7EE)
        static readonly string[] OldTerrain={"Original color terrain with registered paper drawings","One-way cream stone decks","Paired reference terrain - shared contacts and complete artwork"};

        sealed class Layer
        {
            public Vector2 parallax;public Vector3 anchor;public float width;
            public readonly List<Transform> copies=new List<Transform>();
        }

        ForestCombatWorld world;
        readonly List<Object> owned=new List<Object>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        readonly Dictionary<int,Tile> tiles=new Dictionary<int,Tile>();
        readonly List<Renderer> hidden=new List<Renderer>();
        readonly List<Layer> layers=new List<Layer>();
        Transform terrainRoot,backgroundRoot;
        Vector3 cameraAnchor;
        CameraClearFlags previousClear;Color previousBackground;
        bool overview;

        public bool Ready{get;private set;}
        public int TerrainCells{get;private set;}
        public int WoodPieces{get;private set;}
        public Stage1Atmosphere Atmosphere{get;private set;}
        public Transform TerrainRoot=>terrainRoot;
        public Transform BackgroundRoot=>backgroundRoot;

        Material SpriteMaterial=>world.arena.spriteMaterial?world.arena.spriteMaterial:world.spriteMaterial;

        public static bool Applies(ForestCombatWorld w)=>w&&w.proceduralRooms&&!w.villageHub&&!w.dummyTrainingOnly;

        public void Initialize(ForestCombatWorld owner){foreach(var step in InitializeSteps(owner))step();}
        // The same build in steps, for VillagePresentation's build over frames under the chapter's loading screen.
        public IEnumerable<Action> InitializeSteps(ForestCombatWorld owner)
        {
            if(Ready)yield break;
            world=owner;
            IEnumerator<bool> rows=null;bool more=true;
            yield return ()=>rows=BuildTerrainInRows().GetEnumerator();
            while(more)yield return ()=>more=rows.MoveNext();
            yield return BuildBackground;
            yield return ReplaceSoilBackwalls;
            yield return BuildSky;
            yield return BuildBossSpace;
            yield return ()=>
            {
                HideOldArt();
                Ready=true;
                var vanishing=GetComponent<Chapter1VanishingPlatforms>();if(vanishing)vanishing.CollectArt();
                Refresh();
                Debug.Log($"STAGE1_COMBAT_ART cells={TerrainCells} wood={WoodPieces} layers={layers.Count}");
            };
        }

        Sprite Load(string key)
        {
            if(sprites.TryGetValue(key,out var sprite))return sprite;
            sprite=Resources.Load<Sprite>(Art+key);
            if(!sprite)throw new InvalidOperationException("Missing approved stage-1 art: "+Art+key);
            return sprites[key]=sprite;
        }
        Shader LoadShader(string name)
        {
            var shader=Resources.Load<Shader>(Art+"Shaders/"+name);
            if(!shader)throw new InvalidOperationException("Missing approved stage-1 shader: "+name);
            return shader;
        }
        Material Own(Material material){owned.Add(material);return material;}

        // The 47 connected shapes: corners count only when both of their sides are solid.
        static int Canonical(int m)
        {
            int result=m&15;
            for(int i=0;i<4;i++)if((m&(1<<i))!=0&&(m&(1<<((i+1)%4)))!=0&&(m&(16<<i))!=0)result|=16<<i;
            return result;
        }
        static readonly int[] Shapes=BuildShapes();
        static int[] BuildShapes()
        {
            var set=new SortedSet<int>();
            for(int m=0;m<256;m++)set.Add(Canonical(m));
            var list=new int[set.Count];set.CopyTo(list);return list;
        }

        // Atlas cell k (row-major, 16 per row) holds shape k/4, variant k%4 (runtime/tileset.json).
        Tile TileFor(Texture2D atlas,int mask,int variant)
        {
            int k=Array.IndexOf(Shapes,mask)*4+variant;
            if(tiles.TryGetValue(k,out var tile))return tile;
            var sprite=Sprite.Create(atlas,new Rect(k%16*32,atlas.height-(k/16+1)*32,32,32),Vector2.one*.5f,32,0,SpriteMeshType.FullRect);
            sprite.name="tile-"+mask.ToString("D3")+"-"+variant;owned.Add(sprite);
            tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;owned.Add(tile);
            return tiles[k]=tile;
        }

        void Place(Transform parent,string key,Vector2 position,int order)
        {
            var sprite=Load(key);
            var go=new GameObject(sprite.name,typeof(SpriteRenderer));
            go.transform.SetParent(parent,false);go.transform.position=position;
            var renderer=go.GetComponent<SpriteRenderer>();
            renderer.sprite=sprite;renderer.sortingOrder=order;renderer.sharedMaterial=SpriteMaterial;
        }

        // A map row (then a junction column) per step: the whole map took 0.6 s in one go (2026-11-16).
        IEnumerable<bool> BuildTerrainInRows()
        {
            var atlas=Resources.Load<Texture2D>(Art+"terrain-atlas");
            if(!atlas)throw new InvalidOperationException("Missing approved stage-1 terrain atlas.");
            var data=world.Data;
            bool Solid(int x,int y)=>x>=0&&y>=0&&x<data.cols&&y<data.rows&&data.ground[y*data.cols+x]!=0;
            terrainRoot=new GameObject("Approved stage-1 lawn and wood",typeof(Grid)).transform;
            terrainRoot.SetParent(transform,false);terrainRoot.localPosition=new Vector3(0,SurfaceLift,0);
            var terrain=new GameObject("Lawn and soil tiles",typeof(Tilemap),typeof(TilemapRenderer));
            terrain.transform.SetParent(terrainRoot,false);
            var map=terrain.GetComponent<Tilemap>();
            var tileRenderer=terrain.GetComponent<TilemapRenderer>();tileRenderer.sharedMaterial=SpriteMaterial;tileRenderer.sortingOrder=0;
            var decor=new GameObject("Flowers, hatching and wall grass").transform;decor.SetParent(terrainRoot,false);
            int[] dx={0,1,0,-1,1,1,-1,-1},dy={-1,0,1,0,-1,1,1,-1};
            var positions=new List<Vector3Int>();var cells=new List<TileBase>();
            for(int y=0;y<data.rows;y++){for(int x=0;x<data.cols;x++)
            {
                if(!Solid(x,y))continue;
                int mask=0;
                for(int i=0;i<8;i++)if(Solid(x+dx[i],y+dy[i]))mask|=1<<i;
                uint mark=unchecked((uint)x*73856093u^(uint)y*19349663u);mark^=mark>>13;
                positions.Add(new Vector3Int(x,-y-1,0));cells.Add(TileFor(atlas,Canonical(mask),(int)(mark%4)));
                if(!Solid(x,y-1)&&(x*29+y*17)%17==3)Place(decor,"decor/daisy",new Vector2(x+.5f,-y+SurfaceLift),2);
                bool inside=Solid(x,y-1)&&Solid(x,y+1)&&Solid(x-1,y)&&Solid(x+1,y);
                if(inside&&mark%9<2)Place(decor,"decor/pencil-patch-"+mark%4,new Vector2(x+.5f,-y-SurfaceLift),1);
                if(inside&&mark%19==2)Place(decor,"decor/embedded-pebble-"+mark%3,new Vector2(x+.5f,-y-SurfaceLift),1);
                if(Solid(x,y-1)&&Solid(x,y+1)&&mark%5==2)
                    foreach(int direction in new[]{-1,1})
                        if(!Solid(x+direction,y))Place(decor,"decor/wall-blade-"+(direction<0?"left":"right"),new Vector2(x+(direction>0?1:0),-y-SurfaceLift),2);
            }
            yield return true;}
            map.SetTiles(positions.ToArray(),cells.ToArray());map.CompressBounds();
            TerrainCells=positions.Count;
            // Native 32px corner overlays keep the rounded inside joins and the common outline.
            bool Body(int px,int py)=>px>=0&&py>=0&&px<data.cols*32&&py<data.rows*32&&Solid(px/32,py/32)&&(py%32>=8||Solid(px/32,py/32-1));
            for(int x=1;x<data.cols;x++){for(int y=0;y<data.rows;y++)foreach(int offset in new[]{0,8})
            {
                int px=x*32,py=y*32+offset,count=0,sx=0,sy=0;
                foreach(int i in new[]{-1,1})foreach(int j in new[]{-1,1})
                    if(!Body(px+(i<0?-1:0),py+(j<0?-1:0))){count++;sx=i;sy=j;}
                if(count==1)Place(decor,"junctions/inside-"+(sx<0?"left":"right")+"-"+(sy<0?"up":"down"),new Vector2(px/32f,-py/32f+SurfaceLift),2);
            }
            yield return true;}
            var wood=new GameObject("One-way wood decks").transform;wood.SetParent(terrainRoot,false);
            for(int index=0;index<data.platforms.Length;index++)
            {
                var p=data.platforms[index];int count=Mathf.Max(1,p.w/32);
                // Chapter1VanishingPlatforms fades whatever is drawn under this name.
                Transform deck=wood;
                if(p.vanishing){deck=new GameObject("Vanishing platform art "+index).transform;deck.SetParent(wood,false);}
                for(int i=0;i<count;i++)
                {
                    string key=count==1?"single":i==0?"left":i==count-1?"right":i%2==0?"middle-b":"middle-a";
                    Place(deck,"wood/"+key,new Vector2(p.x/32f+i+.5f,-p.y/32f-SurfaceLift),2);WoodPieces++;
                }
            }
        }

        void BuildBackground()
        {
            var shader=LoadShader("NativeStage1Background");
            backgroundRoot=new GameObject("Approved separated native background").transform;backgroundRoot.SetParent(transform,false);
            var player=world.arena.Player;
            var feet=player?(Vector3)player.Position:world.View.transform.position;
            cameraAnchor=world.View.transform.position;
            float depth=world.Data.rows+64;
            // Mountains at the back, then two outline-free village bands; each copy reaches below to the map floor.
            void Add(string key,int order,Vector2 offset,Vector2 parallax)
            {
                var sprite=Load("background/"+key);
                var material=Own(new Material(shader){name="Stage-1 "+key,mainTexture=sprite.texture});
                float width=sprite.rect.width/32,height=sprite.rect.height/32;
                var mesh=new Mesh{name=key+" continuous lower background"};owned.Add(mesh);
                mesh.vertices=new[]{new Vector3(0,-depth),new Vector3(width,-depth),new Vector3(width,height),new Vector3(0,height)};
                mesh.uv=new[]{new Vector2(0,-depth/height),new Vector2(1,-depth/height),Vector2.one,Vector2.up};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
                var layer=new Layer{parallax=parallax,anchor=feet+(Vector3)offset,width=width};
                for(int i=0;i<5;i++)
                {
                    var node=new GameObject(key+" repeat "+i,typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(backgroundRoot,false);
                    node.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
                    layer.copies.Add(node.transform);
                }
                layers.Add(layer);
            }
            // Vertically the bands nearly follow the camera, so deep rooms (the boss hall, shafts) still show the
            // village behind them instead of the flat fill under the band; horizontally they keep the approved parallax.
            Add("near-village",-80,new Vector2(-12,-2),new Vector2(.55f,.96f));
            Add("far-village",-90,new Vector2(-9,1),new Vector2(.82f,.98f));
            Add("mountains",-100,new Vector2(-14,1),new Vector2(.92f,.99f));
            previousClear=world.View.clearFlags;previousBackground=world.View.backgroundColor;
            world.View.clearFlags=CameraClearFlags.SolidColor;world.View.backgroundColor=Paper;
        }

        // The pale pass-through soil supports keep their shapes; only their texture becomes the approved soil.
        void ReplaceSoilBackwalls()
        {
            var root=transform.Find("Pass-through pale background walls");
            if(!root)return;
            var material=Own(new Material(LoadShader("VirtualWallOutlineSoil")){name="Approved stage-1 soil back wall"});
            material.SetTexture("_MainTex",Resources.Load<Texture2D>(Art+"terrain-atlas"));
            for(int i=0;i<4;i++)material.SetTexture("_Hatch"+i,Load("decor/pencil-patch-"+i).texture);
            material.SetVector("_SoilPaper",(Color)new Color32(0xE9,0xE2,0xD5,255));
            material.SetFloat("_TextureContrast",1.5f);
            // A one-dot rim one shade under the soil along each support's edges (user choice A, 2026-11-12).
            material.SetFloat("_OutlineWidth",2);material.SetVector("_OutlineColor",(Color)new Color32(0xCC,0xC5,0xB7,255));
            var rim=Own(new Material(material){name="Approved stage-1 soil back wall rim"});rim.SetFloat("_Rim",1);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial=material;
                // The rim copy sits one order under every body, so overlapping supports never show a rim inside.
                var copy=new GameObject("Rim",typeof(MeshFilter),typeof(MeshRenderer));copy.transform.SetParent(renderer.transform,false);
                copy.GetComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var under=copy.GetComponent<MeshRenderer>();under.sharedMaterial=rim;under.sortingLayerID=renderer.sortingLayerID;under.sortingOrder=renderer.sortingOrder-1;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);under.SetPropertyBlock(block);
            }
        }

        void BuildSky()
        {
            Sprite[] Set(string folder){var set=new Sprite[6];for(int i=0;i<6;i++)set[i]=Load("sky/"+folder+"/"+folder+"-"+i);return set;}
            var skyMaterial=Own(new Material(LoadShader("NativeStage1Background")){name="Stage-1 clouds and birds"});
            var root=new GameObject("Seeded sky and paper ash");root.transform.SetParent(transform,false);
            Atmosphere=root.AddComponent<Stage1Atmosphere>();
            Atmosphere.Initialize(world.View,Set("clouds"),Set("birds"),skyMaterial,LoadShader("Stage1PaperAsh"),7312026);
        }

        // Boss approach hall and boss room (resources environments/stage1-boss-approach-pixel-20261112):
        // a repeating low colonnade along the hall, two towers with the top beam over the room, and a faint
        // rose window drawn between the mountains and the village bands. Replaces Chapter1BossBackdrop's art.
        public Transform BossRoot{get;private set;}
        public const float ColonnadeLength=40;
        void BuildBossSpace()
        {
            ForestRoom room=null;
            foreach(var r in world.Data.rooms)if(r!=null&&r.kind=="boss"){room=r;break;}
            if(room==null)return;
            float floor=-(room.y+room.entryY);
            float hall=0;
            if(world.Data.passages!=null)foreach(var p in world.Data.passages)if(p.to==room.id)hall=p.w;
            BossRoot=new GameObject("Approved stage-1 boss approach and room").transform;BossRoot.SetParent(transform,false);
            SpriteRenderer Put(string key,Vector2 at,int order)
            {
                var go=new GameObject(key,typeof(SpriteRenderer));go.transform.SetParent(BossRoot,false);go.transform.position=at;
                var r=go.GetComponent<SpriteRenderer>();r.sprite=Load("boss/"+key);r.sortingOrder=order;r.sharedMaterial=SpriteMaterial;
                return r;
            }
            Put("boss-room-rose-window",new Vector2(room.x,floor),-96);
            Put("boss-room-towers",new Vector2(room.x,floor),-45);
            if(hall>0)
            {
                // The colonnade rises partway in: the first stretch of the hall stays open and the last
                // ColonnadeLength units up to the boss room are colonnaded, starting on a pier.
                float length=Mathf.Min(hall,ColonnadeLength);
                var colonnade=Put("approach-colonnade",new Vector2(room.x-length,floor),-46);
                colonnade.drawMode=SpriteDrawMode.Tiled;colonnade.tileMode=SpriteTileMode.Continuous;
                colonnade.size=new Vector2(length,colonnade.sprite.rect.height/32);
            }
        }

        static bool OldBackground(string n)=>n=="Sky follows camera"||n.StartsWith("Distant skyline",StringComparison.Ordinal)||
            n.StartsWith("Middle village",StringComparison.Ordinal)||n.StartsWith("Detached cloud",StringComparison.Ordinal)||n=="World fixed near scenery"||
            n.StartsWith("Underground ",StringComparison.Ordinal)||n.StartsWith("Water ",StringComparison.Ordinal)||n.StartsWith("Broken translucent",StringComparison.Ordinal)||
            n.StartsWith("Fountain ",StringComparison.Ordinal)||n=="Whole map backdrop projection"||
            n=="Clock room rear art"||n=="Clock approach rear art";

        // Renderers are disabled, never their GameObjects, so presentation toggles (SetActive) cannot bring them back.
        void HideOldArt()
        {
            foreach(Transform child in transform)
            {
                if(child==terrainRoot||child==backgroundRoot||child==BossRoot||Atmosphere&&child==Atmosphere.transform)continue;
                bool old=Array.IndexOf(OldTerrain,child.name)>=0||OldBackground(child.name);
                if(!old)continue;
                foreach(var renderer in child.GetComponentsInChildren<Renderer>(true))
                    if(renderer.enabled){renderer.enabled=false;hidden.Add(renderer);}
            }
        }

        static Vector3 Pixel(Vector3 p)=>new Vector3(Mathf.Round(p.x*32)/32,Mathf.Round(p.y*32)/32,0);
        static readonly int[] Offsets={0,-1,1,-2,2};

        // Each band repeats with every other copy mirrored, centred on the camera.
        public void Refresh()
        {
            if(!Ready||!world||!world.View)return;
            var camera=world.View.transform.position;var delta=camera-cameraAnchor;
            foreach(var layer in layers)
            {
                var anchor=layer.anchor+new Vector3(delta.x*layer.parallax.x,delta.y*layer.parallax.y);
                int center=Mathf.FloorToInt((camera.x-anchor.x)/layer.width);
                for(int i=0;i<layer.copies.Count;i++)
                {
                    int index=center+Offsets[i];bool mirrored=(index&1)!=0;
                    layer.copies[i].position=Pixel(anchor+Vector3.right*(index*layer.width+(mirrored?layer.width:0)));
                    layer.copies[i].localScale=new Vector3(mirrored?-1:1,1,1);
                }
            }
        }

        // The whole-map inspection view shows the terrain on plain paper.
        public void SetOverview(bool value)
        {
            overview=value;
            if(backgroundRoot)backgroundRoot.gameObject.SetActive(!value);
            if(Atmosphere)Atmosphere.gameObject.SetActive(!value);
            if(value)HideOldArt();
        }
        public bool Overview=>overview;

        void OnDestroy()
        {
            foreach(var r in hidden)if(r)r.enabled=true;hidden.Clear();
            if(world&&world.View){world.View.clearFlags=previousClear;world.View.backgroundColor=previousBackground;}
            foreach(var o in owned)if(o)Destroy(o);owned.Clear();
        }
    }
}
