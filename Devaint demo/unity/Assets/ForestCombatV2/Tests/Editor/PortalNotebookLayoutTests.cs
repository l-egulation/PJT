using System;
using System.Linq;
using UnityEngine.InputSystem;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace ForestCombatV2.Tests {
 public sealed class PortalNotebookLayoutTests {
  [UnityEngine.TestTools.UnityTearDown] public System.Collections.IEnumerator Cleanup(){if(Application.isPlaying)yield return new UnityEngine.TestTools.ExitPlayMode();Chapter1ProfileHost.UseStore(null);}
  [UnityEngine.TestTools.UnityTest]
  public System.Collections.IEnumerator WorldRebindingDoesNotChangeNotebookCloseKeys() {
   UnityEditor.EditorSettings.enterPlayModeOptionsEnabled=false;
   UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=null;
   UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
   yield return new UnityEngine.TestTools.EnterPlayMode();
   yield return ExerciseInput();
   yield return new UnityEngine.TestTools.ExitPlayMode();Chapter1ProfileHost.UseStore(null);
  }
  static System.Collections.IEnumerator ExerciseInput(){
   Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore(new Deviant.Run.ProfileData{profileId=Guid.NewGuid().ToString(),seenStories=new[]{"opening","first-return"}}));
   ForestCombatWorld village=null;
   for(int f=0;f<600;f++){yield return null;village=Object.FindAnyObjectByType<ForestCombatWorld>();if(village&&village.Ready)break;}
   Assert.That(village&&village.Ready,Is.True);
   var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Deviant.Game.PortalNotebookView")).First(t=>t!=null);
   var player=village.arena.Player;var input=Deviant.Combat.PlayerControls.For(player);
   for(int f=0;f<120&&(input==null||input.Source==null);f++){yield return null;input=Deviant.Combat.PlayerControls.For(player);}
   Assert.That(input&&input.Source!=null,Is.True,"player input initialized");
   var action=input.Source.GetAction(Deviant.Input.Contracts.GameAction.Interact);
   Assert.That(action,Is.Not.Null,"world interact action exists");
   int binding=Enumerable.Range(0,action.bindings.Count).First(i=>action.bindings[i].effectivePath=="<Keyboard>/f");
   string old=action.bindings[binding].overridePath;
   var settings=UnityEngine.InputSystem.InputSystem.settings;var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;
   settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
   settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
   var root=new GameObject("Fixed portal menu input");int closed=0;
   try {
    action.ApplyBindingOverride(binding,"<Keyboard>/k");input.Source.SetFocused(true);keyboard.MakeCurrent();
    var view=(Component)type.GetMethod("Create").Invoke(null,new object[]{root.transform,player,(Action)(()=>closed++)});
    var tick=type.GetMethod("Tick");tick.Invoke(view,null);
    var label=(Text)type.GetField("closeKey",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
    Assert.That(label.text,Is.EqualTo("F"),"world remapping must not relabel the menu close key");
    type.GetField("shownAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,Time.unscaledTime-1);
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.K));UnityEngine.InputSystem.InputSystem.Update();tick.Invoke(view,null);
    Assert.That(closed,Is.Zero,"world interact must not dismiss the guide");
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());UnityEngine.InputSystem.InputSystem.Update();
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));UnityEngine.InputSystem.InputSystem.Update();tick.Invoke(view,null);
    Assert.That(closed,Is.EqualTo(1),"fixed F closes once");
   } finally {Object.DestroyImmediate(root);if(old==null)action.RemoveBindingOverride(binding);else action.ApplyBindingOverride(binding,old);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;}
  }


  [Test] public void OpeningNotebookDoesNotChangeSharedFontSampling() {
   var font=Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk");Assert.That(font,Is.Not.Null);
   font.RequestCharactersInTexture("포탈",12);var texture=font.material.mainTexture;var old=texture.filterMode;
   var root=new GameObject("Portal font test");
   try {
    texture.filterMode=FilterMode.Bilinear;
    var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Deviant.Game.PortalNotebookView")).First(t=>t!=null);
    type.GetMethod("Create").Invoke(null,new object[]{root.transform,null,(Action)(()=>{})});
    Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Bilinear),"opening a guide must not pixelate the shared menu font");
   }finally{Object.DestroyImmediate(root);texture.filterMode=old;}
  }

  [TestCase(800,600)] [TestCase(1280,800)] [TestCase(2560,1080)]
  public void NotebookFitsTheSameSafeViewportAsOtherMenus(int width,int height) {
   var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Deviant.Game.PortalNotebookView")).First(t=>t!=null);
   var root=new GameObject("Portal layout test");var cameraObject=new GameObject("Camera");
   var camera=cameraObject.AddComponent<Camera>();var rt=new RenderTexture(width,height,24);
   try {
    var view=(Component)type.GetMethod("Create").Invoke(null,new object[]{root.transform,null,(Action)(()=>{})});
    var canvas=view.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;camera.targetTexture=rt;
    type.GetMethod("FitToWorld").Invoke(view,null);Canvas.ForceUpdateCanvases();
    Assert.That(canvas.scaleFactor,Is.EqualTo(Mathf.Min(width/960f,height/540f)).Within(.001f),"guide must scale continuously like approved menus");
   } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(rt);}
  }
 }
}
