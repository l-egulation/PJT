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
    public static class NatureVerification
    {
        [Serializable] public sealed class Check {public string name;public bool pass;public string evidence;}
        [Serializable] public sealed class Report {public bool pass;public string unityVersion;public bool editorExecuted=true;public bool playModeExecuted=false;public int pngCount;public List<Check> checks=new List<Check>();}
        static Report report;static RuralNatureTerrain terrain;static string baseline,edited;
        static readonly Vector3Int editCell=new Vector3Int(-3,-25,0);
        static string ReportPath=>NatureBaker.Root+"/Reports/unity-verification.json";
        static NatureVerification(){EditorApplication.playModeStateChanged+=PlayStateChanged;}
        static void PlayStateChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("RuralNatureVerifyPlay",false))
            {
                SessionState.SetBool("RuralNatureVerifyPlay",false);
                EditorApplication.delayCall+=()=>
                {
                    try
                    {
                        report=JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));report.playModeExecuted=true;
                        var t=UnityEngine.Object.FindFirstObjectByType<RuralNatureTerrain>();
                        CheckThat(t&&t.visuals.GetUsedTilesCount()>0,"Saved tile assets render in Play mode without editor rebaking");
                        PhysicsChecks();report.pass=report.checks.All(c=>c.pass);
                        File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));SessionState.SetBool("RuralNatureFinishAfterPlay",true);EditorApplication.isPlaying=false;
                    }
                    catch(Exception e){Fail(e);}
                };
            }
            else if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("RuralNatureFinishAfterPlay",false))
            {
                SessionState.SetBool("RuralNatureFinishAfterPlay",false);
                try{report=JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));ExportAndExit();}catch(Exception e){Fail(e);}
            }
        }
        static void CheckThat(bool ok,string name,string evidence="")
        {report.checks.Add(new Check{name=name,pass=ok,evidence=evidence});File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));Debug.Log("RURAL_CHECK "+name);if(!ok)throw new InvalidOperationException(name+": "+evidence);}
        static byte Alpha(NatureComposer c,int x,int y)
        {return PixelAt(c,x,y).a;}
        static Color32 PixelAt(NatureComposer c,int x,int y)
        {var key=new Vector2Int(NatureComposer.FloorDiv(x,32),NatureComposer.FloorDiv(y,32));return c.tiles.TryGetValue(key,out var b)?b[((y%32+32)%32)*32+(x%32+32)%32]:new Color32(0,0,0,0);}
        [MenuItem("Tools/Rural Nature 32/Run Verification (Batch Only)")]
        public static void RunBatch()
        {
            report=new Report{unityVersion=Application.unityVersion};
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Use run-unity-validation.ps1. This verification creates its own review scene and exits Unity.");
                Application.runInBackground=true;
                Directory.CreateDirectory(NatureBaker.Root+"/Reports");
                File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
                var images=NatureComposer.LoadImages(NatureBaker.Root+"/Sources");
                int[,] neighbors={{0,-1},{1,-1},{1,0},{1,1},{0,1},{-1,1},{-1,0},{-1,-1}};
                for(int mask=0;mask<256;mask++)
                {
                    var g=new HashSet<Vector2Int>{Vector2Int.zero};for(int i=0;i<8;i++)if((mask&(1<<i))!=0)g.Add(new Vector2Int(neighbors[i,0],neighbors[i,1]));
                    var c=new NatureComposer(g,new HashSet<Vector2Int>(),images);c.Render();
                    if(Alpha(c,16,16)<128)throw new Exception("Missing center coverage, mask "+mask);
                    if((mask&1)==0&&Alpha(c,16,0)<128)throw new Exception("Unsupported walkable top, mask "+mask);
                    if((mask&16)==0&&Alpha(c,16,31)<128)throw new Exception("Missing flipped underside, mask "+mask);
                    if((mask&4)==0&&Alpha(c,31,16)<128)throw new Exception("Missing clockwise wall coverage, mask "+mask);
                    if((mask&64)==0&&Alpha(c,0,16)<128)throw new Exception("Missing counterclockwise wall coverage, mask "+mask);
                }
                CheckThat(true,"All 256 neighbor combinations render", "Includes isolated cells, thin pillars, diagonal contacts and inner corners; center and all four exposed edge midpoints checked.");
                var wallCells=new HashSet<Vector2Int>();for(int y=0;y<8;y++)for(int x=0;x<3;x++)wallCells.Add(new Vector2Int(x,y));
                var walls=new NatureComposer(wallCells,new HashSet<Vector2Int>(),images);walls.Render();
                var original=images["rim_top_01"];
                CheckThat(PixelAt(walls,95,80).Equals(original.pixels[18*original.width+80])&&original.pixels[18*original.width+80].a==255,"Right wall uses exact clockwise source pixels","Native upper grass strip rotated +90 degrees; no resampling.");
                CheckThat(PixelAt(walls,0,80).Equals(original.pixels[18*original.width+31])&&original.pixels[18*original.width+31].a==255,"Left wall uses exact counterclockwise source pixels","Native upper grass strip rotated -90 degrees; no resampling.");
                foreach(int length in new[]{1,2,3,18,64})
                {
                    var p=new HashSet<Vector2Int>();for(int x=-length;x<0;x++)p.Add(new Vector2Int(x,-2));
                    var c=new NatureComposer(new HashSet<Vector2Int>(),p,images);c.Render();
                    if(Alpha(c,-length*32+16,-64)<128)throw new Exception("One-way cap contact: "+length);
                }
                CheckThat(true,"One-way platforms support arbitrary lengths and negative cells","1, 2, 3, 18, 64 cells");
                terrain=NatureDemo.Build();baseline=terrain.lastBakeHash;
                CheckThat(terrain.visuals.GetUsedTilesCount()>0&&terrain.bakedTileCount>0,"Unity generated individual PNG + Tile assets",terrain.bakedTileCount+" distinct tiles");
                CheckThat(terrain.ground.GetComponent<CompositeCollider2D>()&&terrain.platforms.GetComponent<PlatformEffector2D>(),"Ground composite and one-way effector installed");
                VerifyAutomaticCycle();Finish();
            }
            catch(Exception e){Fail(e);}
        }
        static void Paint(bool add)
        {
            Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(terrain.ground,add?"Verify paint":"Verify erase");
            terrain.ground.SetTile(editCell,add?NatureBaker.LogicTile(false):null);
            Undo.FlushUndoRecordObjects();
        }
        static void VerifyAutomaticCycle()
        {
            // Hidden batch sessions do not continuously pump the edit-mode UI.
            // Let real Tilemap/Undo events populate the queue, then drive the same
            // production Tick synchronously instead of depending on UI timing.
            int count=NatureBaker.automaticBakes;
            Paint(true);CheckThat(NatureBaker.IsQueued(terrain),"Native Tilemap paint event queues automatic rebuild");
            NatureBaker.ProcessQueuedForVerification();
            CheckThat(NatureBaker.automaticBakes>count&&terrain.lastBakeHash!=baseline,"Queued painting produces correct neighbor art","Production update callback pumped synchronously in batch; not an interactive mouse test.");
            edited=terrain.lastBakeHash;
            Paint(false);CheckThat(NatureBaker.IsQueued(terrain),"Native Tilemap erase event queues automatic rebuild");
            NatureBaker.ProcessQueuedForVerification();
            CheckThat(terrain.lastBakeHash==baseline,"Erasing removes obsolete visual cells and restores exact prior output");
            Undo.PerformUndo();CheckThat(NatureBaker.IsQueued(terrain),"Native Undo event queues automatic rebuild");NatureBaker.ProcessQueuedForVerification();
            CheckThat(terrain.ground.HasTile(editCell)&&terrain.lastBakeHash==edited,"Undo restores cells and matching automatic art");
            Undo.PerformRedo();CheckThat(NatureBaker.IsQueued(terrain),"Native Redo event queues automatic rebuild");NatureBaker.ProcessQueuedForVerification();
            CheckThat(!terrain.ground.HasTile(editCell)&&terrain.lastBakeHash==baseline,"Redo restores erased cells and matching automatic art");
        }
        static void PhysicsChecks()
        {
            var scene=SceneManager.CreateScene("RuralNature_PhysicsVerification",new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var physics=scene.GetPhysicsScene2D();bool previous=NatureBaker.Busy;NatureBaker.Busy=true;
            try
            {
                var t=NatureBaker.CreateEmpty();SceneManager.MoveGameObjectToScene(t.gameObject,scene);
                for(int x=0;x<9;x++)t.ground.SetTile(new Vector3Int(x,-1,0),NatureBaker.LogicTile(false));
                for(int x=2;x<6;x++)t.platforms.SetTile(new Vector3Int(x,2,0),NatureBaker.LogicTile(true));
                foreach(var m in new[]{t.ground,t.platforms}){m.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();m.GetComponent<CompositeCollider2D>().GenerateGeometry();}
                var probe=new GameObject("Physics probe",typeof(Rigidbody2D),typeof(BoxCollider2D));SceneManager.MoveGameObjectToScene(probe,scene);
                var rb=probe.GetComponent<Rigidbody2D>();rb.freezeRotation=true;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                probe.GetComponent<BoxCollider2D>().size=new Vector2(.5f,1);
                var friction=new PhysicsMaterial2D("Verification frictionless"){friction=0,bounciness=0};probe.GetComponent<BoxCollider2D>().sharedMaterial=friction;
                Action<float,float,float> reset=(x,y,vy)=>{rb.position=new Vector2(x,y);rb.linearVelocity=new Vector2(0,vy);rb.WakeUp();Physics2D.SyncTransforms();};
                Action<int> step=n=>{for(int i=0;i<n;i++)if(!physics.Simulate(1/60f))throw new Exception("PhysicsScene2D.Simulate returned false");};
                reset(1,5,0);step(180);CheckThat(Math.Abs(rb.position.y-.5f)<.04f,"Actual Unity physics: solid ground landing",rb.position.ToString());
                reset(3.5f,5,0);step(180);CheckThat(Math.Abs(rb.position.y-3.5f)<.04f,"Actual Unity physics: one-way landing from above",rb.position.ToString());
                reset(3.5f,1.4f,10);float peak=rb.position.y;for(int i=0;i<180;i++){step(1);peak=Math.Max(peak,rb.position.y);}CheckThat(peak>4&&Math.Abs(rb.position.y-3.5f)<.04f,"Actual Unity physics: pass from below then land", "peak="+peak+" final="+rb.position);
                reset(.75f,.52f,0);for(int i=0;i<150;i++){rb.linearVelocity=new Vector2(2,rb.linearVelocity.y);step(1);}CheckThat(rb.position.x>5.5f&&Math.Abs(rb.position.y-.5f)<.04f,"Actual Unity physics: traverse merged tile seams",rb.position.ToString());
                UnityEngine.Object.DestroyImmediate(friction);
            }
            finally{if(Application.isPlaying)SceneManager.UnloadSceneAsync(scene);else EditorSceneManager.CloseScene(scene,true);NatureBaker.Busy=previous;}
        }
        static void Finish()
        {
            EditorSceneManager.SaveScene(terrain.gameObject.scene,NatureDemo.ScenePath);
            PrefabUtility.SaveAsPrefabAsset(terrain.gameObject,NatureBaker.Root+"/Prefabs/RuralNature32_Example.prefab");
            NatureBaker.Busy=true;
            EditorSceneManager.OpenScene(NatureDemo.ScenePath);
            NatureBaker.Busy=false;
            terrain=UnityEngine.Object.FindFirstObjectByType<RuralNatureTerrain>();
            CheckThat(terrain&&terrain.lastBakeHash==baseline&&terrain.visuals.GetUsedTilesCount()>0,"Saved scene reload preserves generated Tilemap references");
            foreach(var file in Directory.GetFiles(NatureBaker.Root+"/Generated/Textures","*.png"))
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\','/'));var ti=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                if(!sprite||sprite.rect.width!=32||sprite.rect.height!=32||sprite.pixelsPerUnit!=32||ti.filterMode!=FilterMode.Point||ti.mipmapEnabled||ti.textureCompression!=TextureImporterCompression.Uncompressed)throw new Exception("Bad tile import "+file);
            }
            CheckThat(true,"Individual PNG importer settings verified","32x32 / PPU32 / Point / no mipmaps / uncompressed");
            NatureDemo.CaptureAll();CheckThat(File.Exists(NatureBaker.Root+"/Reports/unity-scene-3.png"),"Three views rendered by Unity camera at 960x540");
            NatureBaker.CreateVisualPalette(terrain);
            // Drop verification-only cache entries after collecting references
            // from every saved scene, prefab and asset outside the generated cache.
            var paths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!p.StartsWith(NatureBaker.Root+"/Generated/")&&(p.EndsWith(".unity")||p.EndsWith(".prefab")||p.EndsWith(".asset"))).ToArray();
            var used=new HashSet<string>(AssetDatabase.GetDependencies(paths,true));
            foreach(var file in Directory.GetFiles(NatureBaker.Root+"/Generated/Tiles","*.asset")){string asset=file.Replace('\\','/');if(used.Contains(asset))continue;string png=NatureBaker.Root+"/Generated/Textures/"+Path.GetFileNameWithoutExtension(asset)+".png";AssetDatabase.DeleteAsset(asset);if(!used.Contains(png))AssetDatabase.DeleteAsset(png);}
            report.pngCount=Directory.GetFiles(NatureBaker.Root+"/Generated/Textures","*.png").Length;
            File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
            SessionState.SetBool("RuralNatureVerifyPlay",true);EditorApplication.isPlaying=true;
        }
        static void ExportAndExit()
        {
            AssetDatabase.Refresh();AssetDatabase.SaveAssets();
            string workspace=Path.GetFullPath(Application.dataPath+"/../../../..");
            string export=Path.Combine(workspace,"02_에셋/배경/숲/시골황혼_Unity자동타일32_v6/RuralNature32.unitypackage");
            AssetDatabase.ExportPackage(NatureBaker.Root,export,ExportPackageOptions.Recurse);
            Debug.Log("RURAL_NATURE_VERIFIED "+report.pngCount+" PNGs / "+report.checks.Count+" checks");EditorApplication.Exit(0);
        }
        static void Fail(Exception e)
        {
            Debug.LogException(e);NatureBaker.Busy=false;
            if(report!=null){report.pass=false;report.checks.Add(new Check{name="Verification exception",pass=false,evidence=e.ToString()});Directory.CreateDirectory(NatureBaker.Root+"/Reports");File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
            if(Application.isBatchMode)EditorApplication.Exit(1);
        }
    }
}
