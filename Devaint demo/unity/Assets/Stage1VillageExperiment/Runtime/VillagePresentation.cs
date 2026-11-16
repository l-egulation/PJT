using System;
using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;

namespace Stage1VillageExperiment
{
    [Serializable] public class VillageProp { public string kind; public float x,y,scale=1,width,height; public int zone,sortingOrder=-20; public bool flipX;public float rotation; }
    [Serializable] public class VillageRoom { public string kind; public float x,y,width,height,floorY; }
    [Serializable] public class VillageArtLayout
    {
        public VillageProp[] props;
        public VillageRoom[] undergroundRects,shaftRects;
        public VillageProp[] waterAnchors,lanterns,aqueducts,fountainAnchors;
    }

    // World-fixed architecture sits directly on authored terrain. Only the sky,
    // distant skyline and detached cloud sprites move with parallax.
    // In a random chapter (ForestCombatWorld.ComposesInFrames) the build runs step by step over frames under the loading
    // screen, and the world waits for it before it answers Ready (IStagedWorldPresentation).
    [DefaultExecutionOrder(600)]
    public sealed class VillagePresentation : MonoBehaviour, IStagedWorldPresentation
    {
        public Stage1VillageWorld world;
        public ForestCombatWorld teamWorld;
        public ForestData MapData => world ? world.Data : teamWorld ? teamWorld.Data : null;
        public Camera ArtCamera => world ? world.View : teamWorld ? teamWorld.View : null;
        float CameraX => world ? world.CameraX : teamWorld.CameraX;
        float CameraY => world ? world.CameraY : teamWorld.CameraY;
        bool WorldReady => world ? world.Ready : teamWorld && teamWorld.Composed;
        bool building,failed;
        // Built, or stopped on an error it logged: the chapter's loading screen does not wait on a build that failed.
        public bool Settled => Ready || failed;
        readonly List<Renderer> replacedTeamRenderers = new List<Renderer>();
        public TextAsset presentationLayout;
        public bool animateEnvironment=true;
        public bool referenceGroundEnabled=true;
        public bool nearSketchInkEnabled=true;
        GameObject referenceGround,legacySketchWalls;
        GameObject colorTerrain,colorPlatforms;
        VillageArtTransition artTransition;
        public GameObject JumpPadArtworkRoot { get; private set; }
        public bool Ready { get; private set; }
        public int PropCount { get; private set; }
        public int TileCount { get; private set; }
        public Vector2Int FarNativeSize { get; private set; }
        public float FarCoverageBottomScreenPixels => FarTopScreenPixels+FarNativeSize.y;
        public SpriteRenderer FarSampleRenderer { get; private set; }
        public float FarRenderedBottomScreenPixels
        {
            get
            {
                if(!FarSampleRenderer||!ArtCamera)return 0;
                var bounds=FarSampleRenderer.bounds;
                var projected=ArtCamera.WorldToViewportPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
                return (1-projected.y)*540;
            }
        }
        public int UndergroundRearCount { get; private set; }
        public int UndergroundFrontCount { get; private set; }
        public int UndergroundLayerCount => UndergroundRearCount+UndergroundFrontCount;
        public int UndergroundRearLayerCount => UndergroundRearCount;
        public int UndergroundFrontLayerCount => UndergroundFrontCount;
        public Vector2 UndergroundParallaxOffsetPixels { get; private set; }
        public float UndergroundRearOffsetPixelsX => Mathf.Round(UndergroundParallaxOffsetPixels.x);
        public float UndergroundFrontOffsetPixelsX => 0;
        public Transform UndergroundFrontSample { get; private set; }
        public int UndergroundEntranceCount { get; private set; }
        public int MidgroundLayerCount => middle.Count;
        public Vector2Int MidgroundNativeSize { get; private set; }
        public float MidgroundParallaxOffsetPixelsX => WorldReady ? CameraX*.45f : 0;
        public SpriteRenderer MidgroundSampleRenderer { get; private set; }

        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        readonly List<Material> materials=new List<Material>();
        readonly Dictionary<Texture2D,Texture2D> sketchPairs=new Dictionary<Texture2D,Texture2D>();
        readonly HashSet<Texture2D> nearSketchTextures=new HashSet<Texture2D>();
        readonly Dictionary<(Material,Texture2D),Material> pairedMaterials=new Dictionary<(Material,Texture2D),Material>();
        readonly Dictionary<(Color,Color),Sprite> solidSprites=new Dictionary<(Color,Color),Sprite>();
        readonly List<Transform> far=new List<Transform>();
        readonly List<Transform> middle=new List<Transform>();
        readonly List<Cloud> clouds=new List<Cloud>();
        readonly List<Water> waters=new List<Water>();
        readonly List<Drop> droplets=new List<Drop>();
        readonly List<Transform> windHedges=new List<Transform>();
        readonly Dictionary<int,Sprite> waterSurfaces=new Dictionary<int,Sprite>();
        Transform sky;
        Transform overview;
        Material normal,terrainNormal,farNormal,middleNormal,rearNormal,waterNormal;
        float farWidth=1086,middleWidth=1086,elapsed;
        bool overviewActive;
        Deviant.Combat.ActorRenderInterpolation playerInterpolation;
        System.Func<Vector2> previousCameraTarget;
        bool ownsPlayerInterpolation;
        const float FarTopScreenPixels=22;
        static readonly string[] TileNames={"01_surface_A_32x32","02_surface_B_32x32","03_outer_top_right_32x32","04_outer_top_left_32x32","05_surface_C_32x32","06_surface_D_32x32","07_wall_right_32x32","08_wall_left_32x32","09_air_inner_left_32x32","10_air_inner_right_32x32","11_inner_floor_left_32x32","12_pit_floor_A_32x32","13_pit_floor_B_32x32","14_inner_floor_right_32x32","15_core_32x32"};
        struct Cloud { public Transform root; public float offset,height,speed; }
        struct Water { public Transform root; public float x,y,width,phase; }
        struct Drop { public Transform root; public float x,y,width,height,phase; public bool fountain; }

        void Start(){if(!world&&!teamWorld)world=GetComponent<Stage1VillageWorld>();TryBuild();}
        void LateUpdate()
        {
            if(!Ready){TryBuild();return;}
            if(animateEnvironment)elapsed+=Time.unscaledDeltaTime;
            RefreshForCapture();
        }
        void TryBuild()
        {
            if(Ready||building||failed||!WorldReady)return;
            if(teamWorld&&teamWorld.ComposesInFrames){building=true;StartCoroutine(BuildInFrames());return;}
            foreach(var step in BuildSteps())step();
        }
        System.Collections.IEnumerator BuildInFrames()
        {
            var slice=new FrameSlice();
            foreach(var step in BuildSteps())
            {
                if(slice.Due){yield return null;slice.Restart();}
                try{step();}
                catch(Exception error){Debug.LogException(error,this);failed=true;}
                if(failed)break;
            }
            building=false;
        }
        // The build in order; a step may use what the steps before it made.
        IEnumerable<Action> BuildSteps()
        {
            yield return PrepareBuild;
            foreach(var step in TerrainSteps())yield return step;
            yield return BuildSky;
            yield return BuildArchitecture;
            yield return BuildPlatforms;
            yield return ()=>{if(world)JumpPadArtworkRoot=VillageJumpPadArtwork.Build(transform,owned);};
            // Chapter-1 combat stages use the approved 32px lawn, native background and sky over these builds.
            if(teamWorld&&Stage1CombatArt.Applies(teamWorld))
            {
                yield return ()=>{CombatArt=GetComponent<Stage1CombatArt>();if(!CombatArt)CombatArt=gameObject.AddComponent<Stage1CombatArt>();};
                foreach(var step in CombatArt.InitializeSteps(teamWorld))yield return step;
            }
            yield return ()=>
            {
                Ready=true;RefreshForCapture();
                Debug.Log($"VILLAGE_ART_READY occupiedCells={TileCount} worldProps={PropCount} colorMaterials={materials.Count}");
            };
        }
        void PrepareBuild()
        {
            if(teamWorld)HideTeamEnvironment();
            artTransition=GetComponent<VillageArtTransition>();
            if(!artTransition)artTransition=gameObject.AddComponent<VillageArtTransition>();
            if(teamWorld)GetComponent<VillageArtTransition>().ConfigureTeamRegions(MapData);
            if(teamWorld&&teamWorld.arena.Player)
            {
                playerInterpolation=teamWorld.arena.Player.GetComponent<Deviant.Combat.ActorRenderInterpolation>();
                ownsPlayerInterpolation=!playerInterpolation;
                if(ownsPlayerInterpolation)playerInterpolation=teamWorld.arena.Player.gameObject.AddComponent<Deviant.Combat.ActorRenderInterpolation>();
                playerInterpolation.Bind(teamWorld.arena.Player);
                previousCameraTarget=teamWorld.CameraPresentationPosition;
                teamWorld.CameraPresentationPosition=playerInterpolation.RenderPosition;
            }
            VillageSceneComposite.Attach(ArtCamera,GetComponent<VillageArtTransition>());
            var shader=Resources.Load<Shader>("Village/VillageRegion");
            if(!shader)throw new InvalidOperationException("Village region shader is required.");
            normal=MakeMaterial(shader,0);
            terrainNormal=MakeMaterial(shader,0);
            waterNormal=MakeMaterial(shader,0);
            farNormal=MakeMaterial(shader,.25f);
            middleNormal=MakeMaterial(shader,.1f);
            rearNormal=MakeMaterial(shader,.08f);rearNormal.SetColor("_HazeColor",new Color32(74,69,62,255));
        }
        Material MakeMaterial(Shader shader,float haze)
        {
            var m=new Material(shader){name="Village registered paper art"};
            m.SetFloat("_Haze",haze);
            m.SetColor("_HazeColor",new Color32(153,186,191,255));
            owned.Add(m);materials.Add(m);return m;
        }


        Sprite Load(string resource,Vector2 pivot)
        {
            string key=resource+"/"+pivot;
            if(sprites.TryGetValue(key,out var result))return result;
            string originalPath=resource.StartsWith("backgrounds/")||resource=="color_seed"?resource:"color/"+resource;
            var tex=Resources.Load<Texture2D>("Village/"+originalPath);
            if(!tex)throw new InvalidOperationException("Missing original village color asset: "+originalPath);
            var sketch=Resources.Load<Texture2D>("Village/reference-sketch/"+originalPath);
            if(resource!="color_seed")
            {
                if(!sketch||sketch.width!=tex.width||sketch.height!=tex.height)
                    throw new InvalidOperationException("Missing registered village sketch: "+originalPath);
                sketchPairs[tex]=sketch;
                if((resource.StartsWith("props/",StringComparison.Ordinal)&&!resource.StartsWith("props/phase2_",StringComparison.Ordinal))||resource=="backgrounds/vault_front_native")
                    nearSketchTextures.Add(tex);
            }
            result=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),pivot,32,0,SpriteMeshType.FullRect);
            result.name=resource;owned.Add(result);sprites[key]=result;return result;
        }
        Sprite LoadSlice(string resource,Rect rect)
        {
            string key=resource+"/slice/"+rect;
            if(sprites.TryGetValue(key,out var result))return result;
            var texture=Load(resource,Vector2.zero).texture;
            result=Sprite.Create(texture,rect,Vector2.zero,32,0,SpriteMeshType.FullRect);
            result.name=key;owned.Add(result);sprites[key]=result;return result;
        }
        Transform Node(string name,Transform parent=null)
        {
            var t=new GameObject(name).transform;t.SetParent(parent?parent:transform,false);return t;
        }
        SpriteRenderer Draw(Transform parent,Sprite sprite,float x,float y,int order,Material material,float scale=1)
        {
            var t=Node(sprite.name,parent);t.position=new Vector3(x/32,-y/32,0);t.localScale=Vector3.one*scale;
            var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=order;r.sharedMaterial=PairMaterial(material,sprite.texture);return r;
        }
        Material PairMaterial(Material original,Texture2D color)
        {
            if(!sketchPairs.TryGetValue(color,out var sketch))return original;
            var key=(original,color);
            if(pairedMaterials.TryGetValue(key,out var cached))return cached;
            var material=new Material(original){name=color.name+" registered color/sketch"};
            material.mainTexture=color;material.SetTexture("_SketchTex",sketch);material.SetFloat("_HasSketch",1);
            if(nearSketchTextures.Contains(color))material.SetFloat("_NearSketchInk",nearSketchInkEnabled?1:0);
            owned.Add(material);pairedMaterials[key]=material;return material;
        }

        public void SetNearSketchInkEnabled(bool enabled)
        {
            nearSketchInkEnabled=enabled;
            foreach(var pair in pairedMaterials)
                if(nearSketchTextures.Contains(pair.Key.Item2))pair.Value.SetFloat("_NearSketchInk",enabled?1:0);
        }

        Sprite Solid(Color color,Color? sketchColor=null)
        {
            Color paper=sketchColor??color;var key=(color,paper);
            if(solidSprites.TryGetValue(key,out var cached))return cached;
            var tex=new Texture2D(1,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};tex.SetPixel(0,0,color);tex.Apply();owned.Add(tex);
            if(sketchColor.HasValue)
            {
                var sketch=new Texture2D(1,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
                sketch.SetPixel(0,0,paper);sketch.Apply();owned.Add(sketch);sketchPairs[tex]=sketch;
            }
            var sprite=Sprite.Create(tex,new Rect(0,0,1,1),Vector2.zero,32);owned.Add(sprite);solidSprites[key]=sprite;return sprite;
        }
        IEnumerable<Action> TerrainSteps()
        {
            bool[,] occupied=null;Sprite[] source=null;Transform parent=null;
            yield return ()=>
            {
                var d=MapData;occupied=new bool[d.cols,d.rows];
                // Collision rectangles are the authoritative terrain, not any legacy paint layer.
                foreach(var r in d.rects)for(int y=Mathf.Max(0,r.y);y<Mathf.Min(d.rows,r.y+r.h);y++)for(int x=Mathf.Max(0,r.x);x<Mathf.Min(d.cols,r.x+r.w);x++)occupied[x,y]=true;
                source=new Sprite[16];
                for(int i=0;i<15;i++)source[i]=Load("terrain/"+TileNames[i],Vector2.one*.5f);
                source[15]=Load("color_seed",Vector2.one*.5f);
                foreach(bool filled in occupied)if(filled)TileCount++;
                parent=Node("Original color terrain with registered paper drawings");
                colorTerrain=parent.gameObject;
            };
            // The quarter tiles go a map row per step: the whole map took 0.86 s in one go (2026-11-16).
            IEnumerator<bool> rows=null;bool more=true;
            yield return ()=>rows=Stage1TerrainQuarterRenderer.BuildInRows(parent,source,occupied,TerrainMaterial,owned,new Dictionary<string,int>()).GetEnumerator();
            while(more)yield return ()=>more=rows.MoveNext();
            yield return ()=>Stage1StoneTurnOverlay.Build(parent,Load("stone_trim/stone_turn_wall_right_32x32",new Vector2(.5f,.25f)),Load("stone_trim/stone_turn_wall_left_32x32",new Vector2(.5f,.25f)),occupied,TerrainMaterial,owned);
            yield return ()=>
            {
                legacySketchWalls=Node("Legacy sketch wall completion",parent).gameObject;
                VillageSketchStairWallCompletion.Build(legacySketchWalls.transform,occupied,terrainNormal.shader,owned);
            };
            yield return ()=>
            {
                referenceGround=VillageReferenceGround.Build(transform,occupied,MapData,owned);
                SetReferenceGroundEnabled(referenceGroundEnabled);
            };
        }
        public void SetReferenceGroundEnabled(bool enabled)
        {
            referenceGroundEnabled=enabled;

            if(legacySketchWalls)legacySketchWalls.SetActive(!enabled);
            if(terrainNormal)terrainNormal.SetFloat("_HideSketch",enabled?1:0);
            foreach(var pair in pairedMaterials)if(pair.Key.Item1==terrainNormal)pair.Value.SetFloat("_HideSketch",enabled?1:0);
            RefreshTerrainVisibility();
        }
        void RefreshTerrainVisibility()
        {
            if(!artTransition)return;
            bool onlySketch=artTransition.Mode==VillageArtMode.Sketch ||
                artTransition.Mode==VillageArtMode.Blend&&artTransition.Weight>=1;
            bool onlyColor=artTransition.Mode==VillageArtMode.Color ||
                artTransition.Mode==VillageArtMode.Blend&&artTransition.Weight<=0;
            // Alpha-zero terrain still costs tilemap submission and rasterization.
            // Region/authored/blend composition retains both endpoints. Legacy
            // sketch stays available when the complete reference art is disabled.
            Visible(colorTerrain,!referenceGroundEnabled||!onlySketch);
            Visible(colorPlatforms,!referenceGroundEnabled||!onlySketch);
            Visible(referenceGround,referenceGroundEnabled&&!onlyColor);
        }
        static void Visible(GameObject root,bool visible)
        { if(root&&root.activeSelf!=visible)root.SetActive(visible); }
        public GameObject ReferenceGroundRoot=>referenceGround;
        public Material TerrainMaterial(Sprite sprite)
        {
            if(!sketchPairs.ContainsKey(sprite.texture))
            {
                string donor=sprite.texture.name.StartsWith("inner_L_",StringComparison.Ordinal)
                    ?"14_inner_floor_right_32x32":sprite.texture.name.StartsWith("inner_R_",StringComparison.Ordinal)
                    ?"11_inner_floor_left_32x32":null;
                Texture2D sketch;
                if(donor!=null)
                {
                    sketch=Resources.Load<Texture2D>("Village/reference-sketch/color/terrain/"+donor);
                    if(!sketch)throw new InvalidOperationException("Missing registered concave terrain: "+donor);
                }
                else
                {
                    sketch=new Texture2D(1,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
                    sketch.SetPixel(0,0,Color.clear);sketch.Apply();owned.Add(sketch);
                }
                sketchPairs[sprite.texture]=sketch;
            }
            var material=PairMaterial(terrainNormal,sprite.texture);material.SetFloat("_TerrainArtwork",1);return material;
        }

        void BuildPlatforms()
        {
            var parent=Node("One-way cream stone decks");
            colorPlatforms=parent.gameObject;
            var pieces=new Sprite[5];
            for(int i=0;i<pieces.Length;i++)pieces[i]=Load("platforms/platform_c"+(i+1).ToString("00")+"_32x32",new Vector2(0,1));
            for(int index=0;index<MapData.platforms.Length;index++)
            {
                var p=MapData.platforms[index];
                int length=Mathf.CeilToInt(p.w/32f);
                // A vanishing deck gets its own node so Chapter1VanishingPlatforms can fade it alone.
                var deck=p.vanishing?Node("Vanishing platform art "+index,parent):parent;
                // Native six-pixel transparent margin preserves the authored landing plane.
                for(int n=0;n<length;n++)Draw(deck,pieces[n==0?0:n==length-1?4:1+(n%3)],p.x+n*32,p.y-6,6,terrainNormal);
            }
            parent.gameObject.SetActive(!referenceGroundEnabled);
        }
        void BuildSky()
        {
            sky=Node("Sky follows camera");
            var skyMaterial=MakeMaterial(normal.shader,.18f);skyMaterial.SetColor("_HazeColor",new Color32(185,205,211,255));
            skyMaterial.SetFloat("_RegionLayer",.14f);
            farNormal.SetFloat("_RegionLayer",.31f);
            middleNormal.SetFloat("_RegionLayer",.82f);
            Draw(sky,Load("backgrounds/sky_native",Vector2.zero),0,540,-100,skyMaterial);
            var silhouette=Load("backgrounds/village_tall_native",Vector2.zero);farWidth=silhouette.rect.width;
            FarNativeSize=new Vector2Int(Mathf.RoundToInt(silhouette.rect.width),Mathf.RoundToInt(silhouette.rect.height));
            if(FarCoverageBottomScreenPixels<540)throw new InvalidOperationException("Distant village artwork must cover the complete gameplay viewport below its skyline.");
            for(int i=0;i<4;i++)
            {
                var root=Node("Distant skyline mirrored repeat "+i);far.Add(root);
                var rendered=Draw(root,silhouette,0,0,-69,farNormal);
                if(!FarSampleRenderer)FarSampleRenderer=rendered;
                if(i%2==1)root.localScale=new Vector3(-1,1,1);
            }
            var middleArt=Load("backgrounds/village_mid_native",Vector2.zero);
            middleWidth=middleArt.rect.width;
            MidgroundNativeSize=new Vector2Int(Mathf.RoundToInt(middleArt.rect.width),Mathf.RoundToInt(middleArt.rect.height));
            for(int i=0;i<4;i++)
            {
                var root=Node("Middle village mirrored repeat "+i);middle.Add(root);
                var rendered=Draw(root,middleArt,0,0,-63,middleNormal);
                if(!MidgroundSampleRenderer)MidgroundSampleRenderer=rendered;
                if(i%2==1)root.localScale=new Vector3(-1,1,1);
            }
            for(int i=0;i<6;i++)
            {
                var root=Node("Detached cloud "+i);
                var cloud=Load("backgrounds/cloud_native_"+(i%3),new Vector2(.5f,.5f));
                Draw(root,cloud,0,0,-89,normal);
                clouds.Add(new Cloud{root=root,offset=i*303,height=40+(i%3)*65,speed=2+i%4});
            }
        }

        void BuildArchitecture()
        {
            if(teamWorld)BackdropWallRoot=VillageBackdropWalls.Build(transform,MapData,owned);
            var registration=Resources.Load<TextAsset>("Village/phase2-scenery");
            var groundOffsets=new Dictionary<string,float>();
            if(registration)foreach(var entry in JsonUtility.FromJson<SceneryRegistration>(registration.text).entries)
                groundOffsets[entry.kind]=entry.groundOffsetPixels;
            if(!presentationLayout)presentationLayout=Resources.Load<TextAsset>("Village/presentation_layout");
            if(!presentationLayout)throw new InvalidOperationException("Village near-layer placement is required.");
            var layout=teamWorld ? VillageTeamScenery.Create(MapData) : JsonUtility.FromJson<VillageArtLayout>(presentationLayout.text);
            var root=Node("World fixed near scenery");
            BuildUnderground(root,layout);
            var allProps=new List<VillageProp>();
            if(layout.props!=null)allProps.AddRange(layout.props);
            // Standalone lantern anchors cannot attach to a parallax rear wall.
            // Complete foreground arches already contain securely mounted lamps.
            if(layout.aqueducts!=null)allProps.AddRange(layout.aqueducts);
            foreach(var prop in allProps)
            {
                float scale=prop.scale>0?prop.scale:1;
                int order=prop.sortingOrder;
                var parent=root;
                if(prop.kind=="hedge"){parent=Node("Subtle hedge breeze",root);windHedges.Add(parent);}
                float contactOffset=groundOffsets.TryGetValue(prop.kind,out var registeredOffset)?registeredOffset:prop.kind=="hedge"?4:0;
                var image=Load("props/"+prop.kind,new Vector2(.5f,0));
                float heightScale=prop.height>0?prop.height/image.rect.height:scale;
                float visualY=prop.y+contactOffset*heightScale;
                var drawing=Draw(parent,Load("props/"+prop.kind,new Vector2(.5f,0)),prop.x,visualY,order+1,normal,scale);
                if(prop.width>0&&prop.height>0)drawing.transform.localScale=new Vector3(prop.width/image.rect.width,prop.height/image.rect.height,1);
                drawing.flipX=prop.flipX;drawing.transform.localRotation=Quaternion.Euler(0,0,prop.rotation);
                var region=new MaterialPropertyBlock();drawing.GetPropertyBlock(region);
                if(prop.kind.StartsWith("phase2_",StringComparison.Ordinal))region.SetFloat("_SketchPaperMatch",1);
                region.SetVector("_RegionAnchor",new Vector4(prop.x,prop.y,1,0));drawing.SetPropertyBlock(region);
                PropCount++;
            }
            if(layout.waterAnchors!=null)foreach(var water in layout.waterAnchors)
            {
                float width=water.width>0?water.width:128, height=water.height>0?water.height:3;
                if(water.kind=="waterfall")
                {
                    AddWaterfallStrands(water.x,water.y,width,height);
                    AddDrops(water.x,water.y,width,height,false,22);
                    continue;
                }
                var t=Node("Water glints");
                var r=Draw(t,WaterSurface(Mathf.Max(1,Mathf.RoundToInt(height))),water.x,water.y+height,8,waterNormal);r.transform.localScale=new Vector3(width,1,1);
                waters.Add(new Water{root=t,x=water.x,y=water.y,width=width,phase=water.x*.01f});
                AddDrops(water.x,water.y,width,4,false,12);
            }
            if(layout.fountainAnchors!=null)foreach(var f in layout.fountainAnchors)AddDrops(f.x,f.y,f.width,f.height,true,24);
        }
        public GameObject BackdropWallRoot { get; private set; }
        public Stage1CombatArt CombatArt { get; private set; }
        [Serializable] sealed class SceneryRegistration { public SceneryContact[] entries; }
        [Serializable] sealed class SceneryContact { public string kind; public float groundOffsetPixels; }
        Sprite WaterSurface(int height)
        {
            if(waterSurfaces.TryGetValue(height,out var cached))return cached;
            var color=new Texture2D(1,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var sketch=new Texture2D(1,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var sourcePixels=new Color32[height];var sketchPixels=new Color32[height];
            for(int y=0;y<height;y++)sourcePixels[y]=new Color32(106,170,179,180);
            sketchPixels[height-1]=new Color32(173,166,151,130);
            color.SetPixels32(sourcePixels);color.Apply();sketch.SetPixels32(sketchPixels);sketch.Apply();
            owned.Add(color);owned.Add(sketch);sketchPairs[color]=sketch;
            var sprite=Sprite.Create(color,new Rect(0,0,1,height),Vector2.zero,32);owned.Add(sprite);waterSurfaces[height]=sprite;return sprite;
        }
        void BuildUnderground(Transform parent,VillageArtLayout layout)
        {
            var rooms=new List<VillageRoom>();
            if(layout.undergroundRects!=null)rooms.AddRange(layout.undergroundRects);
            // Only underground shafts receive a backing. Outdoor steps remain open
            // to the village skyline instead of becoming tiled facades.
            if(layout.shaftRects!=null)foreach(var room in layout.shaftRects)
                if(room.kind=="vault")rooms.Add(room);
            if(rooms.Count==0)return;
            var rearColor=Load("backgrounds/vault_rear_native",Vector2.zero);
            rearNormal=PairMaterial(rearNormal,rearColor.texture);
            ConfigureRearMaterial(rearNormal,rearColor.texture);
            // Keep the upright wall's bottom near the dominant hallway floor. Using
            // a common phase preserves continuous texture coordinates across rooms.
            var floorWidths=new Dictionary<float,float>();
            foreach(var room in rooms)if(room!=null&&room.width>0)
            {floorWidths.TryGetValue(room.floorY,out float width);floorWidths[room.floorY]=width+room.width;}
            float mainFloor=0,widest=0;
            foreach(var floor in floorWidths)if(floor.Value>widest){mainFloor=floor.Key;widest=floor.Value;}
            float referenceCameraY=mainFloor-384;
            rearNormal.SetVector("_WorldOrigin",new Vector4(0,(mainFloor-referenceCameraY*.04f)/32,0,0));
            var rearRoot=Node("Underground distant rear walls",parent);
            BuildRearUnion(rearRoot,rooms,rearNormal);
            var frontRoot=Node("Underground world fixed arch columns",parent);
            var frontColor=Load("backgrounds/vault_front_native",Vector2.zero);
            var placed=new HashSet<string>();
            foreach(var room in rooms)
            {
                if(room==null||room.width<=0||room.height<=0)continue;
                // Keep every arch complete and supported by one actual level floor.
                // Narrow shafts and changes of level show the rear wall alone.
                if(room.floorY-room.y<frontColor.rect.height)continue;
                int row=Mathf.RoundToInt(room.floorY/32);
                int first=Mathf.Max(0,Mathf.CeilToInt(room.x/32));
                int last=Mathf.Min(MapData.cols,Mathf.FloorToInt((room.x+room.width)/32));
                int run=-1;
                for(int x=first;x<=last;x++)
                {
                    bool supported=x<last&&IsGround(x,row)&&!IsGround(x,row-1);
                    if(supported&&run<0)run=x;
                    if(supported||run<0)continue;
                    float left=run*32+16,right=x*32-16,width=right-left;
                    int count=Mathf.FloorToInt((width+96)/(frontColor.rect.width+96));
                    if(count>0)
                    {
                        float used=count*frontColor.rect.width+(count-1)*96;
                        float start=left+(width-used)*.5f;
                        for(int i=0;i<count;i++)
                        {
                            float px=Mathf.Round(start+i*(frontColor.rect.width+96));
                            string key=px+":"+room.floorY;
                            if(!placed.Add(key))continue;
                            var marker=Node("Complete supported vault bay "+key,frontRoot);
                            var front=Draw(marker,frontColor,px,room.floorY,-41,normal);
                            if(!UndergroundFrontSample)UndergroundFrontSample=front.transform;
                            UndergroundFrontCount++;
                        }
                    }
                    run=-1;
                }
            }
        }
        bool IsGround(int x,int y)
        {return x>=0&&x<MapData.cols&&y>=0&&y<MapData.rows&&MapData.ground[y*MapData.cols+x]!=0;}
        void ConfigureRearMaterial(Material material,Texture2D texture)
        {
            material.mainTexture=texture;material.SetFloat("_WorldUV",1);
            material.SetVector("_WorldRepeat",new Vector4(texture.width/32f,texture.height/32f,0,0));
        }
        void BuildRearUnion(Transform parent,List<VillageRoom> rooms,Material material)
        {
            int cols=MapData.cols,rows=MapData.rows,stride=cols+1;
            var covered=new bool[cols,rows];
            foreach(var room in rooms)
            {
                if(room==null||room.width<=0||room.height<=0)continue;
                for(int y=Mathf.Max(0,Mathf.FloorToInt(room.y/32));y<Mathf.Min(rows,Mathf.CeilToInt((room.y+room.height)/32));y++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt(room.x/32));x<Mathf.Min(cols,Mathf.CeilToInt((room.x+room.width)/32));x++)covered[x,y]=true;
            }
            // Opaque masonry ends at an actual coping or pier. Alpha crossfades made
            // the sky and wall occupy the same space for five tiles of every shaft.
            BuildEntranceMasonry(parent,covered);
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            var lookup=new int[(cols+1)*(rows+1)];for(int i=0;i<lookup.Length;i++)lookup[i]=-1;
            Func<int,int,int> vertex=(x,y)=>
            {
                int index=y*stride+x;if(lookup[index]>=0)return lookup[index];
                int result=vertices.Count;lookup[index]=result;vertices.Add(new Vector3(x,-y,0));uv.Add(Vector2.zero);
                colors.Add(Color.white);return result;
            };
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)if(covered[x,y])
            {
                int a=vertex(x,y),b=vertex(x+1,y),c=vertex(x+1,y+1),d=vertex(x,y+1);
                triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);
            }
            var node=Node("Opaque rear masonry inside capped entrances",parent);node.position=Vector3.zero;
            var mesh=new Mesh{name="Single union of underground rear walls",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();owned.Add(mesh);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=-58;
            UndergroundRearCount=triangles.Count>0?1:0;
        }
        void BuildEntranceMasonry(Transform parent,bool[,] covered)
        {
            int cols=MapData.cols,rows=MapData.rows;
            var root=Node("Courtyard coping at open underground entrances",parent);
            for(int y=1;y<rows;y++)
            {
                int start=-1;
                for(int x=0;x<=cols;x++)
                {
                    bool edge=x<cols&&covered[x,y]&&!covered[x,y-1]&&!IsGround(x,y)&&!IsGround(x,y-1);
                    if(edge&&start<0)start=x;
                    if(edge||start<0)continue;
                    bool joinsUpperPier=start>0&&covered[start-1,y-1]&&!covered[start,y-1]
                        &&!IsGround(start-1,y-1)&&!IsGround(start,y-1);
                    AddEntranceCoping(root,start*32,x*32,y*32,joinsUpperPier);start=-1;
                }
            }
            // The stepped market entrance also exposes one vertical mask edge.
            // A complete narrow pier joins its two coping heights instead of a fade.
            for(int x=1;x<cols;x++)
            {
                int start=-1;
                for(int y=0;y<=rows;y++)
                {
                    bool edge=y<rows&&covered[x,y]!=covered[x-1,y]&&!IsGround(x,y)&&!IsGround(x-1,y);
                    if(edge&&start<0)start=y;
                    if(edge||start<0)continue;
                    AddEntrancePier(root,x*32,start*32,y*32);start=-1;
                }
            }
        }
        void AddEntranceCoping(Transform parent,float left,float right,float y,bool joinsUpperPier)
        {
            var root=Node("Entrance coping "+left+":"+right+":"+y,parent);
            {
                const string path="props/courtyard_wall";
                // Reuse only the native stone coping course, not a repeated whole
                // courtyard wall. Its two short piers disappear into the shaft sides.
                var pier=LoadSlice(path,new Rect(4,0,30,81));
                // An internal stair junction already has the vertical pier above it.
                // End its collar at the coping instead of leaving a hanging stub below.
                var leftPier=joinsUpperPier?LoadSlice(path,new Rect(4,44,30,37)):pier;
                Draw(root,leftPier,left-8,y+(joinsUpperPier?20:64),-53,normal);
                Draw(root,pier,right-22,y+64,-53,normal);
                float end=right-22;
                for(float px=left+22;px<end;)
                {
                    int width=Mathf.Min(148,Mathf.CeilToInt(end-px));
                    var rect=new Rect(72,43,width,20);
                    Draw(root,LoadSlice(path,rect),px,y+18,-53,normal);
                    px+=width;
                }
            }
            UndergroundEntranceCount++;
        }
        void AddEntrancePier(Transform parent,float x,float top,float bottom)
        {
            var root=Node("Entrance step pier "+x+":"+top+":"+bottom,parent);
            {
                const string path="props/courtyard_wall";
                for(float py=top;py<bottom;)
                {
                    int height=Mathf.Min(32,Mathf.CeilToInt(bottom-py));
                    var rect=new Rect(4,8,30,height);
                    Draw(root,LoadSlice(path,rect),x-15,py+height,-51,normal);
                    py+=height;
                }
            }
            UndergroundEntranceCount++;
        }
        void AddWaterfallStrands(float x,float y,float width,float height)
        {
            var root=Node("Broken translucent waterfall strands");
            int count=Mathf.Max(3,Mathf.RoundToInt(width/5));
            var flow=Solid(new Color32(124,176,181,65),new Color32(174,168,155,65));
            for(int n=0;n<count;n++)
            {
                float px=Mathf.Round(x+(n+.5f)*width/count);
                float start=y+(n*11)%19,end=y+height-(n*17)%29;
                for(int segment=0;start<end;segment++)
                {
                    float length=Mathf.Min(53+(n*23+segment*17)%43,end-start);
                    float strandWidth=1+(n+segment)%3;
                    var r=Draw(root,flow,px,start+length,8,waterNormal);
                    r.transform.localScale=new Vector3(strandWidth,length,1);
                    start+=length+5+(n*7+segment*3)%13;
                }
            }
        }
        void AddDrops(float x,float y,float width,float height,bool fountain,int count)
        {
            for(int n=0;n<count;n++)
            {
                var t=Node(fountain?"Fountain spray":"Water highlights");
                var a=Draw(t,Solid(new Color32(182,222,224,190),new Color32(207,200,185,190)),0,0,8,waterNormal);a.transform.localScale=new Vector3(2,fountain?2:Mathf.Min(7,height),1);
                droplets.Add(new Drop{root=t,x=x,y=y,width=width,height=height,phase=n/(float)count,fountain=fountain});
            }
        }
        public void AdvanceVisualTime(float seconds){elapsed+=Mathf.Max(0,seconds);RefreshForCapture();}
        public Material MakeLandmarkMaterial(bool ink)
        {
            if(!Ready)throw new InvalidOperationException("Build the village colors before its landmarks.");
            var material=MakeMaterial(normal.shader,0);
            material.mainTexture=Texture2D.whiteTexture;
            material.SetFloat("_SketchVertex",1);material.SetFloat("_SketchInk",ink?1:0);
            return material;
        }
        public void RefreshForCapture()
        {
            if(!Ready)return;
            RefreshTerrainVisibility();
            sky.position=new Vector3(CameraX/32,-CameraY/32,0);
            float scroll=CameraX*.18f;
            int group=Mathf.FloorToInt(scroll/(2*farWidth));
            for(int i=0;i<far.Count;i++)
            {
                float start=CameraX+(group*2+i)*farWidth-scroll;
                if(i%2==1)start+=farWidth;
                far[i].position=new Vector3(Mathf.Round(start)/32,-Mathf.Round(CameraY+FarCoverageBottomScreenPixels)/32,0);
            }
            float middleScroll=MidgroundParallaxOffsetPixelsX;
            int middleGroup=Mathf.FloorToInt(middleScroll/(2*middleWidth));
            for(int i=0;i<middle.Count;i++)
            {
                float start=CameraX+(middleGroup*2+i)*middleWidth-middleScroll;
                if(i%2==1)start+=middleWidth;
                middle[i].position=new Vector3(Mathf.Round(start)/32,-Mathf.Round(CameraY+FarCoverageBottomScreenPixels)/32,0);
            }
            UndergroundParallaxOffsetPixels=overviewActive?Vector2.zero:new Vector2(CameraX*.08f,CameraY*.04f);
            var rearScroll=new Vector4(-Mathf.Round(UndergroundParallaxOffsetPixels.x)/32,Mathf.Round(UndergroundParallaxOffsetPixels.y)/32,0,0);
            rearNormal.SetVector("_WorldScroll",rearScroll);
            for(int i=0;i<clouds.Count;i++)
            {
                var c=clouds[i];float x=Mathf.Repeat(c.offset-CameraX*.055f+elapsed*c.speed,1800)-360;
                c.root.position=new Vector3(Mathf.Round(CameraX+x)/32,-Mathf.Round(CameraY+c.height)/32,0);
            }
            for(int i=0;i<windHedges.Count;i++)windHedges[i].localPosition=new Vector3(Mathf.Round(Mathf.Sin(elapsed*.85f+i*1.7f)*.7f)/32,0,0);
            foreach(var w in waters)w.root.localPosition=new Vector3(0,Mathf.Round(Mathf.Sin(elapsed*1.2f+w.phase))/32,0);
            foreach(var d in droplets)
            {
                float progress=Mathf.Repeat(elapsed*(d.fountain?.6f:.75f)+d.phase,1);
                float x=d.fountain?d.x+(d.phase-.5f)*d.width*progress:d.x+Mathf.Repeat(d.phase*7.31f,1)*d.width;
                float y=d.fountain?d.y-4*d.height*progress*(1-progress):d.y+progress*d.height;
                d.root.position=new Vector3(Mathf.Round(x)/32,-Mathf.Round(y)/32,0);
            }
            if(CombatArt)CombatArt.Refresh();
        }
        public void SetOverview(bool value)
        {
            if(!Ready)return;
            overviewActive=value;
            sky.gameObject.SetActive(!value);
            foreach(var t in far)t.gameObject.SetActive(!value);
            foreach(var t in middle)t.gameObject.SetActive(!value);
            foreach(var c in clouds)c.root.gameObject.SetActive(!value);
            if(value&&!overview)
            {
                overview=Node("Whole map backdrop projection");var d=MapData;
                var s=Draw(overview,Load("backgrounds/sky_native",Vector2.zero),0,d.height,-100,farNormal);s.transform.localScale=new Vector3(d.width/960f,d.height/540f,1);
                // The overview uses the same city artwork behind the authored terrain,
                // expanded to the map camera rather than a single gameplay viewport.
                // One continuous distant horizon for this inspection camera. The
                // former per-column floor anchoring left city strips floating in air.
                var deep=Draw(overview,Solid(new Color32(125,159,145,255),new Color32(244,234,216,255)),0,d.height,-73,farNormal);deep.transform.localScale=new Vector3(d.width,d.height-2350,1);
                for(int x=0;x<d.width;x+=4344)
                {
                    float bottom=2350+(FarNativeSize.y-362)*4;
                    Draw(overview,Load("backgrounds/village_tall_native",Vector2.zero),x,bottom,-69,farNormal,4);
                }
            }
            if(overview)overview.gameObject.SetActive(value);
            if(CombatArt)CombatArt.SetOverview(value);
            RefreshForCapture();
        }
        void HideTeamEnvironment()
        {
            // Hide only the old terrain/backdrop renderers; never disable collider,
            // actor, blueprint, camera, map, input or combat GameObjects.
            foreach(Transform child in teamWorld.transform)
            {
                bool backdrop=Array.Exists(MapData.layers,l=>l.id==child.name);
                if(!child.GetComponent<Grid>()&&!backdrop)continue;
                foreach(var renderer in child.GetComponentsInChildren<Renderer>())
                    if(renderer.enabled){renderer.enabled=false;replacedTeamRenderers.Add(renderer);}
            }
        }
        void OnDestroy()
        {
            if(playerInterpolation)
            {
                if(teamWorld&&teamWorld.CameraPresentationPosition==playerInterpolation.RenderPosition)teamWorld.CameraPresentationPosition=previousCameraTarget;
                if(ownsPlayerInterpolation){playerInterpolation.Unbind();Destroy(playerInterpolation);}
            }
            foreach(var r in replacedTeamRenderers)if(r)r.enabled=true;foreach(var o in owned)if(o)Destroy(o);owned.Clear();
        }
    }
}
