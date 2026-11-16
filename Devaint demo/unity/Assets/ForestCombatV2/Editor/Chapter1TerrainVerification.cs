using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Editor
{
    // Explicit command only: enter the saved Chapter1 scene, render and exercise
    // the real controller, then leave Play Mode without saving runtime changes.
    [InitializeOnLoad]
    public static class Chapter1TerrainVerification
    {
        const string Pending="Chapter1TerrainVerification.Pending";
        [Serializable] public sealed class ViewSpec { public string name;public int x,y; }
        [Serializable] public sealed class Views { public ViewSpec[] views; }
        [Serializable] public sealed class Check { public string name,evidence;public bool pass; }
        [Serializable] public sealed class Report
        {
            public bool complete,pass;
            public string unityVersion,scene;
            public List<Check> checks=new List<Check>();
            public List<string> errors=new List<string>();
        }
        static double deadline;
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../outputs/chapter1-rounded-terrain"));
        static Chapter1TerrainVerification()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Pending,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Wait;}
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use the project's Unity 6000.6.5f1 editor.");
            EditorSceneManager.OpenScene(MeadowDuskBuilder.Scene);
            SessionState.SetBool(Pending,true);
            EditorApplication.EnterPlaymode();
        }
        static void Wait()
        {
            var world=Object.FindFirstObjectByType<ForestCombatWorld>();
            if((!world||!world.Ready)&&EditorApplication.timeSinceStartup<deadline)return;
            EditorApplication.update-=Wait;SessionState.SetBool(Pending,false);
            var report=new Report{unityVersion=Application.unityVersion,scene=MeadowDuskBuilder.Scene};
            try{if(!world||!world.Ready)throw new Exception("Chapter1 scene initialization timed out");Verify(world,report);report.pass=true;}
            catch(Exception e){report.errors.Add(e.ToString());Debug.LogException(e);}
            report.complete=true;
            File.WriteAllText(Path.Combine(Output,"unity-verification.json"),JsonUtility.ToJson(report,true));
            Debug.Log("CHAPTER1_TERRAIN_VERIFIED pass="+report.pass+" checks="+report.checks.Count);
            EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(report.pass?0:1);
        }
        static void Require(Report r,bool pass,string name,string evidence="")
        {
            r.checks.Add(new Check{name=name,pass=pass,evidence=evidence});
            if(!pass)throw new InvalidOperationException(name+": "+evidence);
        }
        static void Verify(ForestCombatWorld world,Report report)
        {
            var arena=world.arena;var player=arena.Player;
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var enabled=renderers.ToDictionary(r=>r,r=>r.enabled);
            var enemies=arena.enemies.ToDictionary(e=>e,e=>e.aggressive);
            var camera=world.View;var oldColor=camera.backgroundColor;var oldClear=camera.clearFlags;
            try
            {
                foreach(var enemy in arena.enemies)enemy.aggressive=false;
                using(var manual=arena.AcquireManualSimulation())
                {
                    Require(report,world.roundedTerrainAtlas&&world.roundedTerrainJson,"Saved scene references rounded terrain assets");
                    Require(report,world.Data.layers.Select(l=>l.id).SequenceEqual(new[]{"forest_depth","forest_near"})&&world.backdropSprites.Length==2,"Saved scene uses the two forest-interior layers");
                    for(int i=0;i<world.Data.layers.Length;i++)
                    {
                        var sprite=world.backdropSprites[i];
                        Require(report,sprite&&sprite.texture.name==world.Data.layers[i].id&&sprite.texture.filterMode==FilterMode.Point&&sprite.texture.mipmapCount==1,"Forest backdrop source and pixel import settings",world.Data.layers[i].id);
                    }
                    var layout=JsonUtility.FromJson<RoundedTerrainLayout>(world.roundedTerrainJson.text);
                    Require(report,layout.geometrySha256==Chapter1RoundedTerrain.GeometryHash(world.Data),"Baked terrain matches current Unity ground and platforms");
                    var maps=world.GetComponentsInChildren<Tilemap>();
                    Require(report,maps.Length==2&&maps.All(m=>m.tileAnchor==new Vector3(.5f,.5f,0)),"Ground and drop-through maps share the 32px cell anchor");
                    foreach(var cell in layout.ground)
                    {
                        var at=new Vector3Int(cell.x,-cell.y-1,0);var s=world.Visuals.GetSprite(at);
                        RequireSprite(s,world.roundedTerrainAtlas,layout.sprites[cell.id]);
                    }
                    var platformMap=maps.Single(m=>m!=world.Visuals);
                    foreach(var cell in layout.platforms)RequireSprite(platformMap.GetSprite(new Vector3Int(cell.x,-cell.y-1,0)),world.roundedTerrainAtlas,layout.sprites[cell.id]);
                    Require(report,true,"Every saved cell resolves to its exact atlas rectangle and foot pivot",layout.ground.Length+" ground + "+layout.platforms.Length+" platform cells");
                    var surfaces=world.GetComponentsInChildren<ArenaSurface>();
                    Require(report,surfaces.Length==world.Data.rects.Length+world.Data.platforms.Length+2&&world.SurfaceCount==surfaces.Length,"Original collision geometry retained",surfaces.Length+" surfaces");
                    Require(report,surfaces.Count(s=>s.oneWay)==world.Data.platforms.Length&&!world.GetComponentInChildren<TilemapCollider2D>(),"Visual fillets introduce no colliders; existing one-way surfaces retained");
                    foreach(var rect in world.Data.rects)Require(report,surfaces.Any(s=>!s.oneWay&&(Vector2)s.transform.position==new Vector2(rect.x+rect.w/2f,-rect.y-rect.h/2f)&&s.GetComponent<BoxCollider2D>().size==new Vector2(rect.w,rect.h)),"Ground rectangle retained",rect.x+","+rect.y+","+rect.w+","+rect.h);
                    player.ResetPlayer();for(int i=0;i<8;i++)manual.Step(1f/120);
                    Require(report,player.grounded,"Player lands at the original Chapter1 spawn");
                    float startX=player.Position.x;
                    for(int i=0;i<48;i++)manual.Step(1f/120,new CombatInput(axis:1));
                    Require(report,player.Position.x>startX+1,"Player walks on the new grass artwork");
                    player.ResetPlayer();for(int i=0;i<8;i++)manual.Step(1f/120);
                    float jumpFloor=player.Position.y,apex=jumpFloor;
                    for(int i=0;i<37;i++){manual.Step(1f/120,new CombatInput(pressed:i==0?CombatButtons.Jump:CombatButtons.None));apex=Mathf.Max(apex,player.Position.y);}
                    Require(report,apex-jumpFloor>2.8f&&apex-jumpFloor<3.2f,"First jump keeps the existing three-unit height");
                    for(int i=0;i<40;i++){manual.Step(1f/120,new CombatInput(pressed:i==0?CombatButtons.Jump:CombatButtons.None));apex=Mathf.Max(apex,player.Position.y);}
                    Require(report,apex-jumpFloor>5.7f&&apex-jumpFloor<6.2f,"Double jump retains its original height");
                    for(int i=0;i<world.Data.platforms.Length;i++)
                    {
                        var p=world.Data.platforms[i];player.ResetPlayer();player.Position=new Vector2((p.x+p.w*.5f)/32,-p.y/32f);player.velocity=Vector2.zero;
                        Physics2D.SyncTransforms();player.grounded=player.ProbeGround();
                        Require(report,player.grounded&&player.DropThrough(),"Drop-through platform "+(i+1)+" supports feet and permits down+jump");
                        float floor=player.Position.y;for(int step=0;step<27;step++)manual.Step(1f/120);
                        Require(report,player.Position.y<floor-.5f,"Drop-through platform "+(i+1)+" clears without sticking");
                    }
                    Directory.CreateDirectory(Path.Combine(Output,"unity"));
                    for(int i=0;i<world.Data.views.Length;i++)
                    {
                        world.Warp(i);for(int step=0;step<20;step++)manual.Step(1f/120);world.RefreshCamera(0,true);
                        Require(report,player.grounded,"Area "+(i+1)+" retains a safe landing");
                        Require(report,world.BackgroundCoversBottom(out string backdropDetail),"Area "+(i+1)+" forest layers cover the camera bottom",backdropDetail);
                        Require(report,world.Data.layers.All(l=>l.repeatUnmirrored)&&world.GetComponentsInChildren<SpriteRenderer>().Where(r=>world.Data.layers.Any(l=>l.id==r.name)).All(r=>!r.flipX),"Area "+(i+1)+" forest repeats without mirrored trunks");
                        Capture(camera,Path.Combine(Output,"unity","area-"+(i+1)+"-scene.png"));
                    }
                    foreach(var r in renderers)r.enabled=r is TilemapRenderer&&r.transform.IsChildOf(world.transform);
                    camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;
                    var views=JsonUtility.FromJson<Views>(File.ReadAllText(Path.Combine(Output,"reference/views.json")));
                    foreach(var v in views.views)
                    {
                        camera.transform.position=new Vector3((v.x+480)/32f,-(v.y+270)/32f,-10);
                        Capture(camera,Path.Combine(Output,"unity",v.name+"-terrain.png"));
                    }
                    Require(report,true,"Captured ten terrain-only comparison views and eight gameplay views");
                }
            }
            finally
            {
                foreach(var pair in enabled)if(pair.Key)pair.Key.enabled=pair.Value;
                foreach(var pair in enemies)if(pair.Key)pair.Key.aggressive=pair.Value;
                camera.backgroundColor=oldColor;camera.clearFlags=oldClear;player.ResetPlayer();world.RefreshCamera(0,true);
            }
        }
        static void RequireSprite(Sprite sprite,Texture2D atlas,RoundedTerrainSprite spec)
        {
            var rect=new Rect(spec.x,atlas.height-spec.y-spec.height,spec.width,spec.height);
            if(!sprite||sprite.texture!=atlas||sprite.rect!=rect||sprite.pixelsPerUnit!=32||Vector2.Distance(sprite.pivot,new Vector2(16,spec.pivotY*spec.height))>.001f)
                throw new InvalidOperationException("Terrain sprite rectangle, PPU or pivot differs from the HTML export.");
        }
        static void Capture(Camera camera,string file)
        {
            var old=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            Texture2D image=null;
            try
            {
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=target;
                image=new Texture2D(960,540,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
            }
            finally{camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(image)Object.DestroyImmediate(image);}
        }
    }
}
