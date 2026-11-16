using System;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Stage1VillageExperiment.Editor
{
 [InitializeOnLoad] public static class VillageReferenceGroundReview
 {
  const string Key="Village.ReferenceGroundReview.";const string Output="../outputs/village/reference-ground/";static double quiet,deadline;static bool busy;
  [Serializable] class Report{public string unity,scope;public bool colorUnchanged,physicsUnchanged,fullSpriteCanvases,transparentPadding;public int sprites,platforms,areas,surfaces;public string[] captures;}
  static VillageReferenceGroundReview(){EditorApplication.update+=Tick;}
  public static void Run(){if(Application.unityVersion!="6000.6.5f1")throw new Exception("Use Unity 6000.6.5f1");Directory.CreateDirectory(Output);SessionState.SetBool(Key+"OldOptions",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Key+"Options",(int)EditorSettings.enterPlayModeOptions);EditorSceneManager.OpenScene(Stage1VillageBuilder.ScenePath);SessionState.SetBool(Key+"Pending",true);SessionState.SetBool(Key+"Entered",false);quiet=0;deadline=EditorApplication.timeSinceStartup+240;}
  static void Tick()
  {
   if(!SessionState.GetBool(Key+"Pending",false)||busy)return;if(deadline==0)deadline=EditorApplication.timeSinceStartup+240;
   if(EditorApplication.timeSinceStartup>deadline){Finish("Reference review startup timed out");return;}
   if(EditorApplication.isCompiling||EditorApplication.isUpdating){quiet=0;return;}
   if(!EditorApplication.isPlaying){if(EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key+"Entered",false))return;if(quiet==0)quiet=EditorApplication.timeSinceStartup+3;if(EditorApplication.timeSinceStartup<quiet)return;EditorSettings.enterPlayModeOptionsEnabled=false;SessionState.SetBool(Key+"Entered",true);EditorApplication.EnterPlaymode();return;}
   var world=Object.FindAnyObjectByType<Stage1VillageWorld>();if(!world||!world.Ready||!world.arena||!world.arena.Player)return;var p=world.GetComponent<VillagePresentation>();if(!p||!p.Ready)return;
   busy=true;try{Capture(world,p);Finish(null);}catch(Exception e){Finish(e.ToString());}
  }
  static void Capture(Stage1VillageWorld world,VillagePresentation p)
  {
   var art=world.GetComponent<VillageArtTransition>();var arena=world.arena;p.animateEnvironment=false;world.showHud=false;arena.Player.UseTestInput=true;foreach(var e in arena.enemies){e.aggressive=false;e.CancelAttack();}
   var report=new Report{unity=Application.unityVersion,scope="Existing game scene with complete reference terrain sprites; full-map layout and gameplay sources unchanged. Art approval is separate.",platforms=world.Data.platforms.Length,areas=world.Data.views.Length,surfaces=world.SurfaceCount};
   using(var simulation=arena.AcquireManualSimulation())
   {
    void SetZone(int zone){arena.Player.ResetPlayer();world.Warp(zone);for(int i=0;i<18;i++)simulation.Step(1f/120,new CombatInput(0));world.RefreshCamera(0,true);p.RefreshForCapture();arena.Presentation.Draw(0);arena.RangedPresentation?.Tick();}
    SetZone(0);var colliders=world.TerrainSurfaces.Select(s=>s.GetComponent<Collider2D>().bounds).ToArray();var feet=arena.Player.Position;
    p.SetReferenceGroundEnabled(false);art.SetDisplay(VillageArtMode.Color);var color=Save(world.View,"before-color.png");art.SetDisplay(VillageArtMode.Sketch);Save(world.View,"before-sketch.png");
    p.SetReferenceGroundEnabled(true);art.SetDisplay(VillageArtMode.Color);var after=Save(world.View,"after-color.png");report.colorUnchanged=color.SequenceEqual(after);
    report.physicsUnchanged=feet==arena.Player.Position&&colliders.SequenceEqual(world.TerrainSurfaces.Select(s=>s.GetComponent<Collider2D>().bounds));
    var renderers=p.ReferenceGroundRoot.GetComponentsInChildren<SpriteRenderer>();report.sprites=renderers.Length;report.fullSpriteCanvases=renderers.All(r=>r.sprite.rect==new Rect(0,0,r.sprite.texture.width,r.sprite.texture.height));
    report.transparentPadding=true;
    foreach(var t in renderers.Select(r=>r.sprite.texture).Distinct())
    {var a=t.GetPixels32();for(int x=0;x<t.width;x++)if(a[x].a!=0||a[(t.height-1)*t.width+x].a!=0)report.transparentPadding=false;for(int y=0;y<t.height;y++)if(a[y*t.width].a!=0||a[y*t.width+t.width-1].a!=0)report.transparentPadding=false;}
    var captures=new System.Collections.Generic.List<string>();
    foreach(int zone in new[]{0,1,3,6}){SetZone(zone);art.SetDisplay(VillageArtMode.Sketch);string name="zone-"+(zone+1).ToString("00")+"-sketch.png";Save(world.View,name);captures.Add(name);}
    SetZone(0);foreach(float weight in new[]{0f,.25f,.5f,.75f,1f}){art.SetDisplay(VillageArtMode.Blend,weight);Save(world.View,"transition-"+(int)(weight*100)+".png");}art.UseAuthoredRegions();Save(world.View,"initial-game.png");report.captures=captures.ToArray();
    VillageTerrainArtReview.Capture(Path.GetFullPath(Output));
   }
   File.WriteAllText(Output+"game-review.json",JsonUtility.ToJson(report,true));
   if(!report.colorUnchanged||!report.physicsUnchanged||!report.fullSpriteCanvases||!report.transparentPadding)throw new Exception("Reference ground preservation/canvas checks failed; see game-review.json");
  }
  static Color32[] Save(Camera c,string name)
  {var old=c.targetTexture;var rect=c.rect;float aspect=c.aspect;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
   try{rt.filterMode=FilterMode.Point;c.targetTexture=rt;c.rect=new Rect(0,0,1,1);c.aspect=960f/540;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Output+name,image.EncodeToPNG());return image.GetPixels32();}
   finally{c.targetTexture=old;c.rect=rect;c.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}}
  static void Finish(string error){SessionState.SetBool(Key+"Pending",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"OldOptions",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"Options",0);if(error!=null){File.WriteAllText(Output+"error.txt",error);Debug.LogError(error);}else Debug.Log("REFERENCE_GROUND_GAME_REVIEW_PASSED");if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);else EditorApplication.isPlaying=false;busy=false;}
 }
}
