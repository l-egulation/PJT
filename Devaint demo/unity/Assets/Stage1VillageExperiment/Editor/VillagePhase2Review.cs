using System;
using System.Collections.Generic;
using System.IO;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stage1VillageExperiment.Editor
{
    // Reproducible art review. Seed override is in-memory only and never saved.
    [InitializeOnLoad]
    public static class VillagePhase2Review
    {
        const string Key="Village.Phase2Review";
        const int ReviewSeed=29053802;
        static double deadline,quiet;
        static VillagePhase2Review(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            if(!world||!world.proceduralRooms||!world.GetComponent<VillagePresentation>())
                throw new InvalidOperationException("Existing team scene with village artwork is required.");
            world.proceduralSeed=ReviewSeed;
            SessionState.SetBool(Key,true);deadline=EditorApplication.timeSinceStartup+300;quiet=0;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+300;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Phase2 review startup timed out.");
                if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(!EditorApplication.isPlaying)
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    if(quiet==0)quiet=EditorApplication.timeSinceStartup+2;
                    if(EditorApplication.timeSinceStartup>=quiet)EditorApplication.EnterPlaymode();
                    return;
                }
                var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
                var art=world?world.GetComponent<VillagePresentation>():null;
                if(!world||!world.Ready||!art||!art.Ready)return;
                if(world.Data.generationSeed!=ReviewSeed)throw new InvalidOperationException("Review seed drifted.");
                string requested=Environment.GetEnvironmentVariable("STAGE1_PHASE2_REVIEW_OUTPUT");
                string output=Path.GetFullPath(string.IsNullOrEmpty(requested)?"../outputs/village/phase2-production-20261027/terrain-review":requested);
                var report=VillageTeamMapSmoke.Check(world,art,output);
                CheckDecoration(world,art,output);
                AuthoredRooms(world,art,output);
                SceneryViews(world,art,output);
                TerrainFixture(art.GetComponent<VillageArtTransition>(),output);
                Debug.Log("VILLAGE_PHASE2_REVIEW_PASSED "+JsonUtility.ToJson(report));Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static void AuthoredRooms(ForestCombatWorld world,VillagePresentation art,string output)
        {
            var transition=art.GetComponent<VillageArtTransition>();transition.UseAuthoredRegions();
            float left=float.MaxValue,right=float.MinValue;
            foreach(var room in world.Data.rooms){left=Mathf.Min(left,room.x*32);right=Mathf.Max(right,(room.x+room.w)*32);}
            if(!transition.CrispRegions||Mathf.Abs(transition.AuthoredNormalFromPixels-Mathf.Lerp(left,right,2f/3))>.01f)
                throw new InvalidOperationException("Stage sketch influence must end at two thirds of the existing map extent.");
            using(world.arena.AcquireManualSimulation())
            {
                foreach(var room in world.Data.rooms)
                {
                    world.Warp(room.id);world.RefreshCamera(0,true);art.RefreshForCapture();
                    world.arena.Presentation.Draw(0);world.arena.RangedPresentation?.Tick();
                    Save(world.View,Path.Combine(output,"room-"+room.id.ToString("00")+"-authored.png"));
                }
            }
            File.WriteAllText(Path.Combine(output,"regions.json"),JsonUtility.ToJson(new RegionReport{
                stageLeft=left,stageRight=right,completeSketchUntil=transition.AuthoredSketchUntilPixels,
                fullyBrightFrom=transition.AuthoredNormalFromPixels,sketchAffectedFraction=2f/3,
                completeSketchFractionIsPreview=true,crisp=true},true));
        }
        [Serializable] sealed class RegionReport
        {
            public float stageLeft,stageRight,completeSketchUntil,fullyBrightFrom,sketchAffectedFraction;
            public bool completeSketchFractionIsPreview,crisp;
        }
        static void CheckDecoration(ForestCombatWorld world,VillagePresentation art,string output)
        {
            var walls=art.BackdropWallRoot;
            if(!walls||walls.GetComponentsInChildren<Collider2D>(true).Length!=0||walls.GetComponentsInChildren<Rigidbody2D>(true).Length!=0)
                throw new InvalidOperationException("Backdrop walls must have no collision or body.");
            var renderers=walls.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)throw new InvalidOperationException("Review map is missing backdrop supports.");
            float beamDepthTotal=0,columnWidthTotal=0;
            foreach(var renderer in renderers)
            {
                var placement=new MaterialPropertyBlock();renderer.GetPropertyBlock(placement);
                var beam=placement.GetVector("_BackdropBeam");var column=placement.GetVector("_BackdropColumn");
                beamDepthTotal+=beam.w-beam.y;columnWidthTotal+=column.z-column.x;
                if(renderer.sortingOrder>=-24)throw new InvalidOperationException("Backdrop wall must be behind nearby scenery and player.");
                var material=renderer.sharedMaterial;
                if(material.mainTexture!=Resources.Load<Texture2D>("Village/ReferenceGround/body")||
                    material.GetTexture("_SoilTex")!=Resources.Load<Texture2D>("Village/ReferenceGround/Bright/soil"))
                    throw new InvalidOperationException("Pass-through walls must reuse the approved terrain soil/hatch artwork.");
                if(material.GetFloat("_BackdropTiles")<.5f||
                    material.GetTexture("_BackdropLeftTex")!=Resources.Load<Texture2D>("Village/ReferenceGround/wall-left")||
                    material.GetTexture("_BackdropRightTex")!=Resources.Load<Texture2D>("Village/ReferenceGround/wall-right")||
                    material.GetTexture("_BackdropBottomTex")!=Resources.Load<Texture2D>("Village/ReferenceGround/underside"))
                    throw new InvalidOperationException("Backdrop must use the complete approved native contour tile kit.");
            }
            var props=VillageTeamScenery.Create(world.Data).props;int overlaps=0;
            for(int i=0;i<props.Length;i++)for(int j=i+1;j<props.Length;j++)
            {
                var a=props[i];var b=props[j];if(a.zone!=b.zone||a.y!=b.y||a.kind=="phase2_grass"||b.kind=="phase2_grass")continue;
                var at=Resources.Load<Texture2D>("Village/color/props/"+a.kind);var bt=Resources.Load<Texture2D>("Village/color/props/"+b.kind);
                if(Mathf.Abs(a.x-b.x)<(at.width*a.scale+bt.width*b.scale)*.5f)overlaps++;
            }
            if(overlaps==0)throw new InvalidOperationException("Scenery should form overlapping, supported groups.");
            File.WriteAllText(Path.Combine(output,"decoration.json"),"{\"backdropWalls\":"+renderers.Length+",\"colliders\":0,\"behindCharacter\":true,\"overlappingPropPairs\":"+overlaps+",\"averageBeamDepth\":"+Mathf.RoundToInt(beamDepthTotal/renderers.Length)+",\"averageColumnWidth\":"+Mathf.RoundToInt(columnWidthTotal/renderers.Length)+"}");
        }
        static void SceneryViews(ForestCombatWorld world,VillagePresentation art,string output)
        {
            var transition=art.GetComponent<VillageArtTransition>();
            var captured=new HashSet<string>();
            using(world.arena.AcquireManualSimulation())
            foreach(var prop in VillageTeamScenery.Create(world.Data).props)
            {
                if(prop.kind!="phase2_residential"&&prop.kind!="phase2_market"&&prop.kind!="phase2_plaza"&&prop.kind!="phase2_tree")continue;
                if(!captured.Add(prop.kind))continue;
                world.Warp(prop.zone);
                // An explicit review-only standing position on the existing supported
                // scenery floor. The production scene, spawn and camera are unchanged.
                world.arena.Player.Position=new Vector2((prop.x+104)/32,-prop.y/32);
                world.RefreshCamera(0,true);art.RefreshForCapture();world.arena.Presentation.Draw(0);
                world.arena.RangedPresentation?.Tick();
                foreach(var mode in new[]{VillageArtMode.Color,VillageArtMode.Sketch,VillageArtMode.Authored})
                {
                    transition.SetDisplay(mode);
                    Save(world.View,Path.Combine(output,prop.kind+"-"+mode.ToString().ToLowerInvariant()+".png"));
                }
            }
            world.Warp(0);transition.UseAuthoredRegions();
        }
        static void TerrainFixture(VillageArtTransition transition,string output)
        {
            var cells=new bool[30,16];
            for(int x=0;x<18;x++)
            {
                int top=x<4?11:x<7?9:x<11?6:x<14?8:x<16?13:10;
                for(int y=top;y<16;y++)cells[x,y]=true;
            }
            for(int x=21;x<30;x++)for(int y=6;y<16;y++)
                cells[x,y]=y<9||x<23||x>=28;
            var data=new ForestData{platforms=new[]{
                new FoliagePlatform{x=32,y=64,w=64},
                new FoliagePlatform{x=192,y=64,w=128},
                new FoliagePlatform{x=448,y=64,w=256}}};
            var owned=new List<UnityEngine.Object>();
            var root=new GameObject("Review fixture - no collision or gameplay");
            var cameraNode=new GameObject("Review fixture camera",typeof(Camera));
            try
            {
                VillageReferenceGround.Build(root.transform,cells,data,owned);
                foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                var camera=cameraNode.GetComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=540f/64;
                camera.transform.position=new Vector3(15,-540f/64,-10);camera.clearFlags=CameraClearFlags.SolidColor;
                foreach(var mode in new[]{VillageArtMode.Color,VillageArtMode.Sketch})
                {
                    transition.SetDisplay(mode);
                    camera.backgroundColor=mode==VillageArtMode.Sketch?new Color32(248,237,220,255):new Color32(181,208,206,255);
                    Save(camera,Path.Combine(output,"assembly-"+mode.ToString().ToLowerInvariant()+".png"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraNode);
                foreach(var item in owned)if(item)UnityEngine.Object.DestroyImmediate(item);
                transition.UseAuthoredRegions();
            }
        }
        static void Save(Camera camera,string path)
        {
            var target=camera.targetTexture;var rect=camera.rect;float aspect=camera.aspect;
            var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try
            {
                rt.filterMode=FilterMode.Point;camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.aspect=960f/540;
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{camera.targetTexture=target;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.Exit(code);}
    }
}
