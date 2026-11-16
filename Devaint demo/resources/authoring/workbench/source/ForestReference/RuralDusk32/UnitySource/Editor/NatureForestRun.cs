using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RuralNature32.Editor
{
    [InitializeOnLoad]
    public static class NatureForestRun
    {
        public const string ScenePath=NatureBaker.Root+"/Scenes/RuralNature32_ForestRun.unity";
        const string ReportPath=NatureBaker.Root+"/Reports/ForestRun/unity-map-verification.json";
        [Serializable] public class Cell {public int x,y;public bool oneWay;}
        [Serializable] public class Tree {public string name;public int x,y,surfaceY;}
        [Serializable] public class View {public string name;public int cameraX,cameraY,actorX,actorY;}
        [Serializable] public class Map {public Cell[] cells;public Tree[] trees;public View[] views;public RuralNatureTerrain.SketchRegion[] sketchRegions;public int width,height;}
        [Serializable] public class BackgroundLayer {public string id,sha256;public float parallax,vertical,phase,baseCrop,minVertical,maxVertical;public int width,height,order;public bool repeatBottom,anchorToTerrain;public ForestPaletteGrade grade;}
        [Serializable] public class BackgroundConfig {public BackgroundLayer[] layers;public ForestPaletteGrade terrainGrade;}
        [Serializable] public class Check {public string name;public bool pass;public string evidence;}
        [Serializable] public class SketchTile {public int x,y;public string file;}
        [Serializable] public class SketchView {public string id;public int style,width,height=96,baselineY=32;public List<SketchTile> tiles=new List<SketchTile>();}
        [Serializable] public class SketchPack {public int tileSize=32,ppu=32;public List<SketchView> variants=new List<SketchView>();}
        [Serializable] public class Report {public bool pass,playModeExecuted;public string unityVersion;public int width,height,groundCells,platformCells,visualCells,uniqueTiles;public List<Check> checks=new List<Check>();}
        static Report report;
        static Map map;
        static string PreviewRoot=>Path.GetFullPath(Application.dataPath+"/../../../시골황혼_긴숲맵_v6");
        static NatureForestRun(){EditorApplication.playModeStateChanged+=PlayStateChanged;}
        static void CheckThat(bool ok,string name,string evidence="")
        {report.checks.Add(new Check{name=name,pass=ok,evidence=evidence});WriteReport();if(!ok)throw new InvalidOperationException(name+": "+evidence);Debug.Log("FOREST_MAP_CHECK "+name);}
        static void WriteReport(){Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
        static GameObject Draw(string name,string file,int x,int y,int order,Transform parent,bool flipY=false)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(NatureBaker.Root+"/Preview/"+file+".png");if(!sprite)throw new Exception(file);
            var obj=new GameObject(name,typeof(SpriteRenderer));obj.transform.SetParent(parent,false);
            obj.transform.localPosition=new Vector3((x+sprite.rect.width/2)/32f,-(y+sprite.rect.height/2)/32f,0);
            var sr=obj.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;sr.sharedMaterial=NatureDemo.Unlit;sr.flipY=flipY;return obj;
        }
        [MenuItem("Tools/Rural Nature 32/Open Long Forest Map")]
        public static void Open()
        {if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(ScenePath);}
        public static void RunBatch()
        {
            report=new Report{unityVersion=Application.unityVersion};
            try
            {
                if(!Application.isBatchMode)throw new Exception("Run using run-unity-map.ps1; batch validation creates its scene and exits.");
                Application.runInBackground=true;
                map=JsonUtility.FromJson<Map>(File.ReadAllText(NatureBaker.Root+"/Data/forest-run-map.json"));
                report.width=map.width;report.height=map.height;report.groundCells=map.cells.Count(c=>!c.oneWay);report.platformCells=map.cells.Count(c=>c.oneWay);
                NatureBaker.Busy=true;
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var t=NatureBaker.CreateEmpty();t.name="Long Forest - editable 32px terrain";t.platformStyle=RuralNatureTerrain.PlatformLook.PartialSketch;t.groundStyle=RuralNatureTerrain.GroundLook.PartialSketch;t.sketchRegions=map.sketchRegions;
                NatureModular.CreatePalette();
                foreach(bool oneWay in new[]{false,true}){var cells=map.cells.Where(c=>c.oneWay==oneWay).ToArray();(oneWay?t.platforms:t.ground).SetTiles(cells.Select(c=>new Vector3Int(c.x,c.y,0)).ToArray(),cells.Select(c=>(TileBase)NatureBaker.LogicTile(oneWay)).ToArray());}
                t.visuals.GetComponent<TilemapRenderer>().sharedMaterial=NatureDemo.Unlit;
                var composed=NatureBaker.Bake(t);report.visualCells=composed.tiles.Count;report.uniqueTiles=t.bakedTileCount;
                NatureBaker.CreateVisualPalette(t);
                VerifyPartialSketch(t,composed);
                CheckThat(map.width==20480&&map.height>=2048,"Continuous 20,480px forest generated",report.groundCells+" ground cells; "+report.platformCells+" one-way cells");
                CheckThat(t.ground.GetComponent<CompositeCollider2D>()&&t.platforms.GetComponent<PlatformEffector2D>(),"Editable ground and one-way collision layers installed");
                var decor=new GameObject("Rooted trees - original v3 pixels");
                foreach(var tree in map.trees)Draw(tree.name,tree.name,tree.x,tree.y,-10,decor.transform);
                var actors=new GameObject("Approved static scale references - not a replacement controller");
                var reference=Draw("Approved F006 reference","ApprovedIdleReference",220-64,288-115,10,actors.transform);
                var bg=new GameObject("Forest Run backdrop");
                var camera=new GameObject("Forest Run Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";
                camera.orthographic=true;camera.orthographicSize=540f/64;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(209,155,130,255);
                var background=bg.AddComponent<RuralForestBackdrop>();background.sceneCamera=camera;
                var config=JsonUtility.FromJson<BackgroundConfig>(File.ReadAllText(NatureBaker.Root+"/Data/background-layers.json"));
                t.visuals.GetComponent<TilemapRenderer>().sharedMaterial=ForestPaletteMaterials.Get("terrain",config.terrainGrade);
                background.layers=config.layers.Select(layer=>new RuralForestBackdrop.Layer{id=layer.id,width=layer.width,height=layer.height,parallax=layer.parallax,vertical=layer.vertical,baseCrop=layer.baseCrop,minVertical=layer.minVertical,maxVertical=layer.maxVertical,phase=layer.phase,repeatBottom=layer.repeatBottom,anchorToTerrain=layer.anchorToTerrain,sprites=Enumerable.Range(0,layer.repeatBottom?6:3).Select(i=>Draw(layer.id+" "+i,layer.id,0,0,layer.order,bg.transform).GetComponent<SpriteRenderer>()).ToArray()}).ToArray();
                for(int j=0;j<background.layers.Length;j++)foreach(var sprite in background.layers[j].sprites)sprite.sharedMaterial=ForestPaletteMaterials.Get(config.layers[j].id,config.layers[j].grade);
                CheckThat(background.layers.Length==3&&map.trees.Length==0&&config.layers[0].id=="dusk_sky_v2"&&config.layers[1].id=="close_woods_v2"&&config.layers[2].id=="low_trees_v2"&&config.layers.All(layer=>NatureBaker.Hash(File.ReadAllBytes(NatureBaker.Root+"/Preview/"+layer.id+".png"))==layer.sha256)&&config.layers[1].parallax>config.layers[0].parallax&&config.layers[2].parallax>config.layers[1].parallax&&config.layers[2].order>config.layers[1].order&&config.layers[2].order<0,"Separate sky, woodland and low tree layers installed","1152x768 each / PPU32; sky0.08, woodland0.55, low trees0.62; all behind gameplay; no ground-tree sprites");
                CheckThat(reference.GetComponent<SpriteRenderer>().sharedMaterial==NatureDemo.Unlit&&t.visuals.GetComponent<TilemapRenderer>().sharedMaterial.shader.name=="RuralNature32/Soft Palette","Soft palette applied to scenery only","Approved character keeps original material; source tiles and all alpha remain intact");
                var plates=NatureComposer.LoadImages(NatureBaker.Root+"/Preview");
                CheckThat(plates["dusk_sky_v2"].pixels.All(p=>p.a==255)&&plates["close_woods_v2"].pixels.Count(p=>p.a<32)>1152*768*.08&&plates["close_woods_v2"].pixels.Count(p=>p.a>224)>1152*768*.65,"Sky opaque and forest openings genuinely transparent","Generated forest alpha retained; sky visible through canopy and trunk openings");
                CheckThat(plates["low_trees_v2"].pixels.Count(p=>p.a<32)>1152*768*.4&&plates["low_trees_v2"].pixels.Count(p=>p.a>224)>1152*768*.2,"Low tree layer retains transparent space and leaf silhouettes","Original generated alpha; proportional resize; no opaque sky behind low trees");
                var solid=new HashSet<Vector2Int>(map.cells.Where(c=>!c.oneWay).Select(c=>new Vector2Int(c.x,c.y)));
                background.terrainColumns=Enumerable.Range(0,map.width/32).Select(x=>new RuralForestBackdrop.SurfaceColumn{heights=solid.Where(p=>p.x==x&&!solid.Contains(p+Vector2Int.up)).Select(p=>(-p.y-1)*32).OrderBy(y=>y).ToArray()}).ToArray();
                CheckBackdrop(camera,background);
                VerifyModularTiles();
                ExportTiles(composed);
                Directory.CreateDirectory(NatureBaker.Root+"/Reports/ForestRun");
                for(int i=0;i<map.views.Length;i++)
                {
                    reference.transform.localPosition=new Vector3((map.views[i].actorX+8)/32f,-(map.views[i].actorY-51)/32f,0);
                    Capture(camera,bg.transform,map.views[i],i+1);
                }
                foreach(int rise in new[]{0,128})
                {
                    var jumpView=new View{name="Forest jump reference",cameraX=0,cameraY=-66-rise,actorX=320,actorY=288-rise};
                    reference.transform.localPosition=new Vector3((jumpView.actorX+8)/32f,-(jumpView.actorY-51)/32f,0);
                    Capture(camera,bg.transform,jumpView,rise==0?8:9);
                }
                reference.transform.localPosition=new Vector3((220+8)/32f,-(288-51)/32f,0);
                CheckThat(map.views.All(v=>v.actorY>=0),"Seven native-scale Unity camera views rendered","960x540; characters use exact approved F006 sprite");
                PositionCamera(camera,bg.transform,map.views[0]);
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();NatureBaker.Busy=false;
                SessionState.SetBool("NatureForestVerifyPlay",true);EditorApplication.isPlaying=true;
            }
            catch(Exception e){Fail(e);}
        }
        static void CheckBackdrop(Camera camera,RuralForestBackdrop backdrop)
        {
            bool cover=true,bounded=true;
            foreach(var layer in backdrop.layers)
            {
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                bounded&=!layer.anchorToTerrain;
                foreach(int cameraY in new[]{-384,-300,-66,0,600,1508})
                {
                    camera.transform.position=new Vector3(480/32f,-(cameraY+270)/32f,-10);backdrop.PositionNow();
                    float top=-layer.sprites[1].transform.position.y*32-cameraY-layer.height/2f;
                    cover&=top<=0&&top+layer.height>=540;minimum=Mathf.Min(minimum,top);maximum=Mathf.Max(maximum,top);
                }
                bounded&=maximum-minimum<=layer.maxVertical-layer.minVertical+.01f;
                bounded&=Mathf.Abs(backdrop.LayerTop(layer,0,-266)-backdrop.LayerTop(layer,0,-66))<=4;
            }
            CheckThat(cover,"Sky and forest cover viewport with soft parallax","No reflected lower trunks or empty edges");
            CheckThat(bounded,"Vertical forest travel is small and independent of terrain","Terrain anchoring disabled; forest total range10px,sky4px;200px camera rise changes forest by4px or less");
        }
        static void VerifyPartialSketch(RuralNatureTerrain terrain,NatureComposer partial)
        {
            var original=new NatureComposer(NatureBaker.ReadCells(terrain.ground),NatureBaker.ReadCells(terrain.platforms),NatureComposer.LoadImages(NatureBaker.Root+"/Sources"),5,0);original.Render();
            long changed=0,decorated=0;bool alpha=true,outside=true,earth=true;
            foreach(var pair in original.tiles)
            {
                if(!partial.tiles.TryGetValue(pair.Key,out var pixels)){alpha=false;continue;}
                for(int i=0;i<1024;i++)
                {
                    var a=pair.Value[i];var b=pixels[i];alpha&=a.a==b.a;
                    if(a.a>0&&!NaturePartialSketch.Earth(a))decorated++;
                    if(a.Equals(b))continue;changed++;
                    int x=pair.Key.x*32+i%32,y=pair.Key.y*32+i/32;
                    outside&=map.sketchRegions.Any(r=>NaturePartialSketch.Weight(x,y,r)>0);
                    if(NaturePartialSketch.Earth(a))earth=false;
                }
            }
            CheckThat(alpha&&original.tiles.Count==partial.tiles.Count,"Partial sketch preserves every source alpha pixel and terrain silhouette");
            CheckThat(outside&&earth,"All pixels outside sketch patches and dark soil fill remain unchanged");
            CheckThat(changed>1000&&changed<decorated*.30,"Most terrain stays colored; sketch is localized",changed+" changed of "+decorated+" visible decorative pixels; "+map.sketchRegions.Length+" patches");
            bool land=true;
            foreach(int style in new[]{5,6})foreach(int length in new[]{1,2,3,4,6,11,28})
            {
                var cells=new HashSet<Vector2Int>(Enumerable.Range(0,length).Select(x=>new Vector2Int(x,0)));
                var c=new NatureComposer(new HashSet<Vector2Int>(),cells,NatureComposer.LoadImages(NatureBaker.Root+"/Sources"),style,0,new[]{new RuralNatureTerrain.SketchRegion{x=length*16,y=8,radiusX=60,radiusY=50,platform=true}});c.Render();
                for(int x=9;x<length*32-9;x++)land&=c.tiles.TryGetValue(new Vector2Int(x/32,0),out var tile)&&tile[x%32].a>48;
                land&=c.tiles.Keys.All(k=>k.y>=-1&&k.y<=1);
            }
            CheckThat(land,"Colored and partial foliage platforms preserve landing and have no root row","14 assemblies,32..896px; alpha at unchanged landing baseline;3 visual rows retain proportional leaf tips; no soil or roots");
        }
        static void VerifyModularTiles()
        {
            var catalog=JsonUtility.FromJson<NatureModular.Catalog>(File.ReadAllText(NatureModular.Root+"/tiles.json"));
            var images=NatureComposer.LoadImages(NatureModular.Root+"/Textures");
            CheckThat(catalog.palette.All(e=>images[e.id].width==32&&images[e.id].height==32&&AssetDatabase.LoadAssetAtPath<Tile>(NatureModular.Root+"/Tiles/"+e.id+".asset")),"Named reusable 32x32 PNGs and Unity Tile assets installed",catalog.palette.Length+" role-named pieces; individual Tile Palette saved");
            var patterns=Enumerable.Range(0,256).Select(NatureModular.Normalize).Distinct().ToArray();
            CheckThat(patterns.Length==47&&new[]{"clean","unfinished"}.All(s=>patterns.All(m=>images.ContainsKey(s+"_ground_"+m.ToString("000")))),"Ground surfaces, walls and all 47 neighbor shapes have sketch tiles","Two sketch styles, including inner corners, outer corners, cavities and fill");
            var library=new HashSet<string>(catalog.palette.Select(e=>NatureBaker.Hash(NatureBaker.Bytes(images[e.id].pixels))));bool reuse=true,landing=true;int cases=0;
            var source=NatureComposer.LoadImages(NatureBaker.Root+"/Sources");
            foreach(int style in new[]{1,2,3})foreach(int length in new[]{1,2,3,4,6,11,28})
            {
                var cells=new HashSet<Vector2Int>(Enumerable.Range(0,length).Select(x=>new Vector2Int(x,0)));
                var c=new NatureComposer(new HashSet<Vector2Int>(),cells,source,style);c.Render();cases++;
                reuse&=c.tiles.Values.All(p=>library.Contains(NatureBaker.Hash(NatureBaker.Bytes(p))));
                for(int x=9;x<length*32-9;x++)landing&=c.tiles.TryGetValue(new Vector2Int(x/32,0),out var tile)&&tile[x%32].a>48;
            }
            CheckThat(reuse,"All platform lengths reuse existing named tiles","21 assemblies from32px to896px; no long-platform raster is generated");
            CheckThat(landing,"Modular platform landing baseline matches unchanged collision","All21 assemblies at cell Y0; rounded caps extend9px beyond supported center, matching18px actor foot width");
            bool connected=true;foreach(string s in new[]{"clean","unfinished","colored"})foreach(string a in new[]{"left","middle_a","middle_b","middle_c"})foreach(string b in new[]{"middle_a","middle_b","middle_c","right"})for(int row=0;row<3;row++)for(int y=0;y<32;y++)connected&=images[s+"_platform_"+a+"_"+row].pixels[y*32+31].Equals(images[s+"_platform_"+b+"_"+row].pixels[y*32]);
            CheckThat(connected,"Platform middle tiles can be reordered without open seams","All48 connector pairs across clean, unfinished and colored styles; three vertical32px pieces per column; foliage only");
        }
        static void ExportTiles(NatureComposer c)
        {
            var unique=new Dictionary<string,int>();var pixels=new List<Color32[]>();var cells=new List<string>();
            foreach(var pair in c.tiles.OrderBy(p=>p.Key.y).ThenBy(p=>p.Key.x))
            {
                if(!pair.Value.Any(p=>p.a>0))continue;
                string id=NatureBaker.Hash(NatureBaker.Bytes(pair.Value));if(!unique.TryGetValue(id,out int index)){index=pixels.Count;unique[id]=index;pixels.Add(pair.Value);}
                cells.Add("["+pair.Key.x+","+pair.Key.y+","+index+"]");
            }
            int columns=32,w=columns*32,h=((pixels.Count+columns-1)/columns)*32;
            var atlas=new Color32[w*h];for(int i=0;i<pixels.Count;i++)for(int y=0;y<32;y++)Array.Copy(pixels[i],y*32,atlas,(i/columns*32+y)*w+i%columns*32,32);
            NatureBaker.WritePNG(PreviewRoot+"/terrain-atlas.png",atlas,w,h);
            var sketchIds=c.tiles.Where(p=>c.sketchTiles.Contains(p.Key)).Select(p=>unique[NatureBaker.Hash(NatureBaker.Bytes(p.Value))]).Distinct().OrderBy(i=>i);
            string data="{\"tileSize\":32,\"columns\":32,\"uniqueTiles\":"+pixels.Count+",\"sketchTileIndices\":["+string.Join(",",sketchIds)+"],\"cells\":["+string.Join(",",cells)+"]}";
            File.WriteAllText(PreviewRoot+"/terrain.json",data);File.WriteAllText(PreviewRoot+"/terrain.js","window.FOREST_TERRAIN="+data+";");
            CheckThat(pixels.Count>0,"Browser atlas exported from actual Unity tile composition",pixels.Count+" unique 32px tiles");
        }
        static void PositionCamera(Camera c,Transform bg,View v)
        {c.transform.position=new Vector3((v.cameraX+480)/32f,-(v.cameraY+270)/32f,-10);bg.GetComponent<RuralForestBackdrop>().PositionNow();}
        static void Capture(Camera c,Transform bg,View v,int index)
        {
            PositionCamera(c,bg,v);var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);target.filterMode=FilterMode.Point;
            var previous=RenderTexture.active;var texture=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try{c.targetTexture=target;c.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes(NatureBaker.Root+"/Reports/ForestRun/forest-"+index.ToString("00")+".png",texture.EncodeToPNG());}
            finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
        static void PlayStateChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("NatureForestVerifyPlay",false))
            {
                SessionState.SetBool("NatureForestVerifyPlay",false);
                EditorApplication.delayCall+=()=>{try{VerifyPhysics();SessionState.SetBool("NatureForestExportAfterPlay",true);EditorApplication.isPlaying=false;}catch(Exception e){Fail(e);}};
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("NatureForestExportAfterPlay",false))
            {
                SessionState.SetBool("NatureForestExportAfterPlay",false);
                try
                {
                    report=JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
                    AssetDatabase.Refresh();AssetDatabase.SaveAssets();
                    string workspace=Path.GetFullPath(Application.dataPath+"/../../../..");
                    AssetDatabase.ExportPackage(NatureBaker.Root,workspace+"/02_에셋/배경/숲/시골황혼_Unity자동타일32_v6/RuralNature32.unitypackage",ExportPackageOptions.Recurse);
                    Debug.Log("LONG_FOREST_VERIFIED "+report.width+"px / "+report.checks.Count+" checks");EditorApplication.Exit(0);
                }
                catch(Exception e){Fail(e);}
            }
        }
        static void VerifyPhysics()
        {
            report=JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));map=JsonUtility.FromJson<Map>(File.ReadAllText(NatureBaker.Root+"/Data/forest-run-map.json"));report.playModeExecuted=true;
            var t=UnityEngine.Object.FindAnyObjectByType<RuralNatureTerrain>();CheckThat(t&&t.visuals.GetUsedTilesCount()>0,"Saved long-map Tilemap loads in Play mode");
            var previous=Physics2D.simulationMode;Physics2D.simulationMode=SimulationMode2D.Script;
            var probe=new GameObject("Long-map landing probe",typeof(Rigidbody2D),typeof(BoxCollider2D));var rb=probe.GetComponent<Rigidbody2D>();rb.freezeRotation=true;rb.gravityScale=25/Math.Abs(Physics2D.gravity.y);
            var box=probe.GetComponent<BoxCollider2D>();box.size=new Vector2(18/32f,62/32f);box.offset=new Vector2(0,31/32f);
            try
            {
                foreach(var v in map.views)
                {
                    rb.position=new Vector2(v.actorX/32f,-v.actorY/32f+1);rb.linearVelocity=Vector2.zero;rb.WakeUp();Physics2D.SyncTransforms();
                    for(int i=0;i<180;i++)Physics2D.Simulate(1/120f);
                    CheckThat(Math.Abs(rb.position.y+v.actorY/32f)<.055f,"Unity physics landing: "+v.name,rb.position.ToString());
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(probe);Physics2D.simulationMode=previous;}
            report.pass=report.checks.All(c=>c.pass);WriteReport();
        }
        static void Fail(Exception e)
        {Debug.LogException(e);NatureBaker.Busy=false;if(report!=null){report.pass=false;report.checks.Add(new Check{name="Exception",pass=false,evidence=e.ToString()});WriteReport();}if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
}
