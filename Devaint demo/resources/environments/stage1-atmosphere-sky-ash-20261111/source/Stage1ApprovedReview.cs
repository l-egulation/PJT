using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ForestCombatV2;
using ForestCombatV2.BlockMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Stage1ApprovedReview {
 const string Flag="Stage1Approved.Camera.Pending",LiveFlag="Stage1Approved.Camera.Playable";
 static string Package=>Environment.GetEnvironmentVariable("STAGE_BACKGROUND_CAPTURE_ROOT") ?? Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../resources/environments/stage1-pixel-lawn-wood-32"));
 static string NativePackage=>Environment.GetEnvironmentVariable("STAGE_NATIVE_BACKGROUND_ROOT") ?? Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../resources/environments/stage1-pixel-background-native-20261111"));
 static string WeatherPackage=>Environment.GetEnvironmentVariable("STAGE_WEATHER_ROOT") ?? Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../resources/environments/stage1-atmosphere-sky-ash-20261111"));
 static string[] Rooms={"S1-04","S1-07","S1-08"};
 static readonly Dictionary<string,Sprite> sprites=new();
 static readonly Dictionary<string,Tile> tiles=new();
 static Transform replacementRoot,backgroundRoot;
 static readonly List<SpriteRenderer> midCopies=new(),farCopies=new(),mountainCopies=new();
 static Vector3 midAnchor,farAnchor,mountainAnchor,cameraAnchor;
 static readonly Dictionary<SpriteRenderer,MeshRenderer> extended=new();
 static Stage1Atmosphere weather;
 static Material layerMaterial;
 static int roomIndex,waitFrames,seed;static bool placed;
 static Stage1ApprovedReview(){ForestCombatWorld.PreparingEditorPlay+=Configure;EditorApplication.update+=Tick;}
    public static void Run()
    {
        if (Application.unityVersion != "6000.6.5f1")
            throw new InvalidOperationException("This review uses Unity 6000.6.5f1.");
        EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
        SessionState.SetBool(LiveFlag, false);
        SessionState.SetBool(Flag, true);
        Debug.Log("BACKGROUND_CAPTURE Enter Play Mode " + Package);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/deviant/Stage1/Play approved stage1 art")]
    public static void PlayReview()
    {
        EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
        SessionState.SetBool(LiveFlag,true);
        SessionState.SetBool(Flag,true);
        EditorApplication.EnterPlaymode();
    }

    static void Configure(ForestCombatWorld world)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        seed=1;
        string target=Environment.GetEnvironmentVariable("STAGE_LAWN_REVIEW_TARGETS");
        if(!string.IsNullOrEmpty(target))
        {
            string[] wanted=target.Split(',');
            for(int candidate=0;candidate<150;candidate++)
            {
                var layout=BlockGenerator.Stage1(candidate);
                if(layout!=null&&wanted.All(id=>layout.rooms.Any(room=>room.refId==id)))
                {seed=candidate;break;}
            }
        }
        world.proceduralSeed=seed;
        Debug.Log("BACKGROUND_CAPTURE seed "+seed);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
        try
        {
            var world = Object.FindFirstObjectByType<ForestCombatWorld>();
            if (!world || !world.Ready || !world.arena.Player ||
                !world.transform.Find("Paired reference terrain - shared contacts and complete artwork")) return;
            if (sprites.Count == 0) { Install(world); InstallBackground(world); PositionBackground(world); }
            if(SessionState.GetBool(LiveFlag,false)) {if(!weather)InstallWeather(world);weather.manualTime=false;RefreshBackground(world);return;}
            var room = world.Data.rooms.First(r => r.name == BlockSpec.Room(Rooms[roomIndex]).name);
            if (!placed)
            {
                foreach (var enemy in world.arena.enemies) enemy.aggressive = false;
                world.Warp(room.id);
                PickUnobstructedLanding(world);
                using (var sim = world.arena.AcquireManualSimulation())
                    for (int i = 0; i < 20; ++i) sim.Step(1f / 120);
                world.RefreshCamera(0, true);
                PositionBackground(world);
                placed = true;
                waitFrames = Time.frameCount + 4;
                return;
            }
            if (Time.frameCount < waitFrames) return;
            world.RefreshCamera(0, true);
            RefreshBackground(world);
            Capture(world, room, Rooms[roomIndex++]);
            placed = false;
            if (roomIndex < Rooms.Length) return;
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(3);
        }
    }

    static void Finish(int result)
    {
        SessionState.SetBool(Flag, false);
        Debug.Log("BACKGROUND_CAPTURE exit " + result);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(result);
    }

    static Sprite LoadSprite(string directory, string key, Vector2 pivot)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(directory, key + ".png"))))
            throw new InvalidDataException(key);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            pivot, 32, 0, SpriteMeshType.FullRect);
        sprite.name = key;
        sprites.Add(key, sprite);
        return sprite;
    }

    static Tile TileFor(string key)
    {
        if (tiles.TryGetValue(key, out var tile)) return tile;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprites[key];
        tile.colliderType = Tile.ColliderType.None;
        tiles.Add(key, tile);
        return tile;
    }

    static void PickUnobstructedLanding(ForestCombatWorld world)
    {
        var player = world.arena.Player;
        var data = world.Data;
        var start = player.Position;
        float bestScore = float.NegativeInfinity;
        Vector2 best = start;
        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < data.cols && y < data.rows &&
            data.ground[y * data.cols + x] != 0;
        int originalRow = Mathf.RoundToInt(-start.y);
        for (int step = -18; step <= 18; ++step)
        {
            int row = originalRow;
            var feet = new Vector2(start.x + step * .5f, -row);
            int left = Mathf.FloorToInt(feet.x - .45f), right = Mathf.FloorToInt(feet.x + .65f);
            if (!Solid(left, row) || !Solid(right, row) ||
                Solid(left, row - 1) || Solid(right, row - 1) ||
                Solid(left, row - 2) || Solid(right, row - 2)) continue;
            var clearance = world.arena.enemies.Select(e => (e.Position - feet).magnitude).DefaultIfEmpty(10).Min();
            bool nearWall = Solid(left - 1, row - 1) || Solid(right + 1, row - 1);
            float score = Mathf.Min(clearance, 6) - .045f * Mathf.Abs(step) - (nearWall ? .6f : 0);
            if (score > bestScore) { best = feet; bestScore = score; }
        }
        player.Position = best;
        Physics2D.SyncTransforms();
        player.grounded = player.ProbeGround();
    }

    static int Canonical(int m) {
        int result=m&15;
        for(int i=0;i<4;i++) if((m&(1<<i))!=0&&(m&(1<<((i+1)%4)))!=0&&(m&(16<<i))!=0) result|=16<<i;
        return result;
    }

    static void Install(ForestCombatWorld world) {
        foreach(string folder in new[]{"terrain","wood","decor","junctions"})
            foreach(string file in Directory.GetFiles(Path.Combine(Package,"runtime",folder),"*.png"))
                LoadSprite(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file),Vector2.one*.5f);
        var original=world.Visuals;
        foreach(var renderer in original.transform.parent.GetComponentsInChildren<TilemapRenderer>()) renderer.enabled=false;
        foreach(string name in new[]{"Original color terrain with registered paper drawings","One-way cream stone decks","Paired reference terrain - shared contacts and complete artwork"}) {
            var art=world.transform.Find(name);
            if(art) foreach(var renderer in art.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
        }
        var parent=new GameObject("32px lawn and wood review - entire generated stage",typeof(Grid)).transform;
        replacementRoot=parent;
        parent.SetParent(world.transform,false);
        parent.localPosition=new Vector3(0,.25f,0);
        var terrain=new GameObject("Whole-stage terrain",typeof(Tilemap),typeof(TilemapRenderer));
        terrain.transform.SetParent(parent,false);
        var map=terrain.GetComponent<Tilemap>();
        terrain.GetComponent<TilemapRenderer>().sharedMaterial=original.GetComponent<TilemapRenderer>().sharedMaterial;
        var data=world.Data;
        bool Solid(int x,int y)=>x>=0&&y>=0&&x<data.cols&&y<data.rows&&data.ground[y*data.cols+x]!=0;
        int[] dx={0,1,0,-1,1,1,-1,-1},dy={-1,0,1,0,-1,1,1,-1};
        var positions=new List<Vector3Int>();
        var cells=new List<TileBase>();
        for(int y=0;y<data.rows;y++) for(int x=0;x<data.cols;x++) {
            if(!Solid(x,y)) continue;
            int mask=0;
            for(int i=0;i<8;i++) if(Solid(x+dx[i],y+dy[i])) mask|=1<<i;
            mask=Canonical(mask);
            uint mark=unchecked((uint)x*73856093u^(uint)y*19349663u);mark^=mark>>13;
            string key=$"tile-{mask:D3}-{mark%4}";
            positions.Add(new Vector3Int(x,-y-1,0));cells.Add(TileFor(key));
            if(!Solid(x,y-1)&&(x*29+y*17)%17==3)
                PlaceSprite(parent,sprites["daisy"],new Vector2(x+.5f,-y+.25f),2,world.spriteMaterial);
            bool inside=Solid(x,y-1)&&Solid(x,y+1)&&Solid(x-1,y)&&Solid(x+1,y);
            if(inside&&mark%9<2) PlaceSprite(parent,sprites["pencil-patch-"+mark%4],new Vector2(x+.5f,-y-.25f),1,world.spriteMaterial);
            if(inside&&mark%19==2) PlaceSprite(parent,sprites["embedded-pebble-"+mark%3],new Vector2(x+.5f,-y-.25f),1,world.spriteMaterial);
            if(Solid(x,y-1)&&Solid(x,y+1)&&mark%5==2) foreach(int direction in new[]{-1,1})
                if(!Solid(x+direction,y)) PlaceSprite(parent,sprites["wall-blade-"+(direction<0?"left":"right")],
                    new Vector2(x+(direction>0?1:0),-y-.25f),2,world.spriteMaterial);
        }
        map.SetTiles(positions.ToArray(),cells.ToArray());
        // Native 32px corner overlays preserve the rounded join and the common outline.
        bool Body(int px,int py)=>px>=0&&py>=0&&px<data.cols*32&&py<data.rows*32&&Solid(px/32,py/32)&&(py%32>=8||Solid(px/32,py/32-1));
        for(int x=1;x<data.cols;x++) for(int y=0;y<data.rows;y++) foreach(int offset in new[]{0,8}) {
            int px=x*32,py=y*32+offset,count=0,sx=0,sy=0;
            foreach(int i in new[]{-1,1}) foreach(int j in new[]{-1,1})
                if(!Body(px+(i<0?-1:0),py+(j<0?-1:0))) {count++;sx=i;sy=j;}
            if(count==1) PlaceSprite(parent,sprites["inside-"+(sx<0?"left":"right")+"-"+(sy<0?"up":"down")],
                new Vector2(px/32f,-py/32f+.25f),2,world.spriteMaterial);
        }
        foreach(var p in data.platforms) {
            int count=p.w/32;
            for(int i=0;i<count;i++) {
                string key=count==1?"single":i==0?"left":i==count-1?"right":i%2==0?"middle-b":"middle-a";
                PlaceSprite(parent,sprites[key],new Vector2(p.x/32f+i+.5f,-p.y/32f-.25f),2,world.spriteMaterial);
            }
        }
        Rooms=Enumerable.Range(1,12).Select(i=>"S1-"+i.ToString("D2"))
            .Where(id=>data.rooms.Any(r=>r.name==BlockSpec.Room(id).name)).ToArray();
        Debug.Log($"BACKGROUND_CAPTURE whole-stage cells={positions.Count} dimensions={data.cols}x{data.rows}; rooms={string.Join(",",Rooms)}");
        string selection=Environment.GetEnvironmentVariable("STAGE_TAUPE_REVIEW_ROOMS");
        if(!string.IsNullOrEmpty(selection)) Rooms=Rooms.Where(id=>selection.Split(',').Contains(id)).ToArray();
        if(Rooms.Length==0) throw new InvalidDataException("No stage rooms found.");
    }

    static bool MoveUpper(ForestCombatWorld world,ForestRoom room) {
        var data=world.Data;var feet=world.arena.Player.Position;
        var choices=new List<Vector2>();
        bool Solid(int x,int y)=>x>=0&&y>=0&&x<data.cols&&y<data.rows&&data.ground[y*data.cols+x]!=0;
        bool Clear(Vector2 p) {
            int x=Mathf.FloorToInt(p.x),y=Mathf.RoundToInt(-p.y);
            return !Solid(x,y-1)&&!Solid(x,y-2)&&!Solid(x,y-3);
        }
        foreach(var p in data.platforms) {
            var position=new Vector2((p.x+p.w*.5f)/32f,-p.y/32f);
            float up=position.y-feet.y;
            if(up>=4&&up<=13&&p.x>=room.x*32&&p.x<(room.x+room.w)*32&&Clear(position))choices.Add(position);
        }
        for(int x=room.x+1;x<room.x+room.w-1;x++)
        for(int y=Mathf.Max(room.y,(int)(-feet.y)-13);y<(int)(-feet.y)-3;y++)
            if(Solid(x,y)&&!Solid(x,y-1)) {
                var position=new Vector2(x+.5f,-y);
                if(Clear(position))choices.Add(position);
            }
        if(choices.Count==0) return false;
        var target=choices.OrderBy(p=>Mathf.Abs((p.y-feet.y)-9)+Mathf.Abs(p.x-feet.x)*.15f).First();
        world.arena.Player.Position=target;Physics2D.SyncTransforms();
        using(var sim=world.arena.AcquireManualSimulation())for(int i=0;i<20;i++)sim.Step(1f/120);
        world.RefreshCamera(0,true);RefreshBackground(world);
        Debug.Log("BACKGROUND_UPPER feet="+world.arena.Player.Position+" grounded="+world.arena.Player.grounded);
        return true; // A valid upper camera location was selected; grounded is logged separately.
    }

    static void PlaceSprite(Transform parent, Sprite sprite, Vector2 position, int order, Material material)
    {
        var go = new GameObject(sprite.name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = material;
    }

    static void InstallSoilBackwalls(ForestCombatWorld world) {
        var root=world.transform.Find("Pass-through pale background walls");
        if(!root) throw new Exception("Missing original background soil supports");
        var shader=Shader.Find("Hidden/VirtualWallOutlineSoil");
        var material=new Material(shader);
        var atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
        atlas.LoadImage(File.ReadAllBytes(Path.Combine(Package,"runtime/terrain-atlas.png")));
        atlas.filterMode=FilterMode.Point;atlas.wrapMode=TextureWrapMode.Clamp;
        material.SetTexture("_MainTex",atlas);
        for(int i=0;i<4;i++) {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(Package,"runtime/decor/pencil-patch-"+i+".png")));
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Repeat;
            material.SetTexture("_Hatch"+i,texture);
        }
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
            renderer.sharedMaterial=material;renderer.gameObject.layer=28;
        }
        Debug.Log("BACKGROUND_SOIL supports="+root.childCount+"; approved core tiles and hatch patches; no outline");
    }

    static void InstallWeather(ForestCombatWorld world) {
        Sprite[] Load(string folder) {
            return Directory.GetFiles(Path.Combine(WeatherPackage,"runtime",folder),"*.png").OrderBy(p=>p).Select(path=>{
                var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;
                var s=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.one*.5f,32,0,SpriteMeshType.FullRect);s.name=Path.GetFileNameWithoutExtension(path);return s;
            }).ToArray();
        }
        var root=new GameObject("Seeded sky and paper ash - runtime candidate");root.transform.SetParent(world.transform,false);
        weather=root.AddComponent<Stage1Atmosphere>();weather.manualTime=true;weather.Initialize(world.View,Load("clouds"),Load("birds"),7312026);
    }

 static Vector4 Hex(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return new Vector4(c.r,c.g,c.b,1);}
 static Vector3 Pixel(Vector3 p)=>new Vector3(Mathf.Round(p.x*32)/32,Mathf.Round(p.y*32)/32,0);
 static void InstallBackground(ForestCombatWorld world){
  foreach(Transform child in world.transform){string n=child.name;
   if(n=="Sky follows camera"||n.StartsWith("Distant skyline")||n.StartsWith("Middle village")||n.StartsWith("Detached cloud")||n=="World fixed near scenery"||n.StartsWith("Underground ")||n.StartsWith("Water ")||n.StartsWith("Broken translucent")||n.StartsWith("Fountain ")||n=="Whole map backdrop projection")
    foreach(var renderer in child.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
  }
  layerMaterial=new Material(Shader.Find("Hidden/NativeStage1Background"));
  backgroundRoot=new GameObject("Approved separated native background").transform;backgroundRoot.SetParent(world.transform,false);
  void Add(string key,int order,int layer,List<SpriteRenderer> list){
   var sprite=LoadSprite(Path.Combine(NativePackage,"runtime"),key,Vector2.zero);
   for(int i=0;i<5;i++){
    var node=new GameObject(key+" repeat "+i,typeof(SpriteRenderer));node.layer=layer;node.transform.SetParent(backgroundRoot,false);
    var renderer=node.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=layerMaterial;renderer.sortingOrder=order;
    list.Add(renderer);BuildContinuousMesh(renderer,world.Data.rows+64);
   }
  }
  Add("near-village",-80,27,midCopies);Add("far-village",-90,26,farCopies);Add("mountains",-100,29,mountainCopies);
  InstallSoilBackwalls(world);
  var wall=world.transform.Find("Pass-through pale background walls");
  foreach(var renderer in wall.GetComponentsInChildren<MeshRenderer>(true)){
   renderer.sharedMaterial.SetVector("_SoilPaper",Hex("#E9E2D5"));renderer.sharedMaterial.SetFloat("_TextureContrast",1.5f);renderer.sharedMaterial.SetFloat("_OutlineWidth",0);
  }
  world.View.clearFlags=CameraClearFlags.SolidColor;world.View.backgroundColor=new Color32(244,241,233,255);
 }
 static void BuildContinuousMesh(SpriteRenderer source,float depth){
  float width=source.sprite.rect.width/32,height=source.sprite.rect.height/32;
  var child=new GameObject("Continuous lower background",typeof(MeshFilter),typeof(MeshRenderer));child.layer=source.gameObject.layer;child.transform.SetParent(source.transform,false);
  var mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,-depth),new Vector3(width,-depth),new Vector3(width,height),new Vector3(0,height)};
  mesh.uv=new[]{new Vector2(0,-depth/height),new Vector2(1,-depth/height),Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
  child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=source.sharedMaterial;renderer.sortingOrder=source.sortingOrder;
  extended.Add(source,renderer);source.enabled=false;
 }
 static void PositionBackground(ForestCombatWorld world){
  var feet=world.arena.Player.Position;midAnchor=new Vector3(feet.x-12,feet.y-2);farAnchor=new Vector3(feet.x-9,feet.y+1);mountainAnchor=new Vector3(feet.x-14,feet.y+1);cameraAnchor=world.View.transform.position;RefreshBackground(world);
 }
 static void Repeat(List<SpriteRenderer> copies,Vector3 anchor,float cameraX){
  int[] offsets={0,-1,1,-2,2};float width=copies[0].sprite.rect.width/32;int center=Mathf.FloorToInt((cameraX-anchor.x)/width);for(int i=0;i<copies.Count;i++){
   var source=copies[i];int index=center+offsets[i];bool mirrored=(index&1)!=0;
   source.transform.position=Pixel(anchor+Vector3.right*(index*width+(mirrored?width:0)));
   var renderer=extended[source];renderer.transform.localScale=new Vector3(mirrored?-1:1,1,1);
   var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",source.sprite.texture);renderer.SetPropertyBlock(props);
  }
 }
 static void RefreshBackground(ForestCombatWorld world){var d=world.View.transform.position-cameraAnchor;
  Repeat(midCopies,midAnchor+new Vector3(d.x*.55f,d.y*.70f),world.View.transform.position.x);Repeat(farCopies,farAnchor+new Vector3(d.x*.82f,d.y*.85f),world.View.transform.position.x);Repeat(mountainCopies,mountainAnchor+new Vector3(d.x*.92f,d.y*.92f),world.View.transform.position.x);
 }
 static void Capture(ForestCombatWorld world,ForestRoom room,string roomId){
  var camera=world.View;var oldTarget=camera.targetTexture;var oldRect=camera.rect;var oldActive=RenderTexture.active;
  var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
  try{
   camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
   if(!weather)InstallWeather(world);weather.manualTime=true;
   void Save(string label){weather.RenderAt(3);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(WeatherPackage,"preview",label+".png"),image.EncodeToPNG());}
   string label=roomId.ToLowerInvariant();weather.ashEnabled=true;Save(label+"-approved");
   if(roomId=="S1-07"&&MoveUpper(world,room))Save("s1-07-upper-approved");
   if(roomId=="S1-08"){
    string frames=Path.GetFullPath(Path.Combine(Application.dataPath,"../../paper-ash-frames"));Directory.CreateDirectory(frames);
    for(int i=0;i<48;i++){weather.RenderAt(i/8f);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(frames,"ash-"+i.ToString("D3")+".png"),image.EncodeToPNG());}
   }
   Debug.Log($"APPROVED_CAPTURE {label};seed={seed};camera={camera.transform.position};feet={world.arena.Player.Position};size={camera.orthographicSize}");
  }finally{camera.targetTexture=oldTarget;camera.rect=oldRect;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
 }
}
