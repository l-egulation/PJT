using System;
using System.IO;
using System.Linq;
using Deviant.Chapter2;
using Deviant.Chapter2.Editor;
using Deviant.Game;
using ForestCombatV2;
using ForestCombatV2.Editor;
using Deviant.Combat;
using Deviant.Run;
using Deviant.Definitions;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MaintenanceUnityReview
{
    const string Pending="MaintenanceUnityReview.Pending";
    static IDisposable reviewProfile;
    static void InstallReviewProfile(){reviewProfile?.Dispose();reviewProfile=Chapter1ProfileHost.UseTemporaryStore(new MemoryProfileStore(new ProfileData{profileId=Guid.NewGuid().ToString(),seenStories=new[]{"opening"}}));}
    static MaintenanceUnityReview(){if(SessionState.GetBool(Pending,false)){InstallReviewProfile();began=(float)EditorApplication.timeSinceStartup;phase=0;EditorApplication.update+=Tick;}}
    public static void ValidateAssets()
    {
        foreach(var name in new[]{"npc1","npc2","stage1-shop-3","stage1-shop-4","stage1-shop-5","stage1-shop-6","stage1-desk","stage2-shop-3","stage2-shop-4","stage2-shop-5","stage2-shop-6","stage2-desk","lantern","lantern-stage1","sign-stage1","sign-stage2","stage1-reroll-crate","stage2-reroll-crate"}){
            var p="Assets/Deviant/Game/Resources/MaintenanceShops/"+name+".png";var importer=AssetImporter.GetAtPath(p) as TextureImporter;
            if(!importer||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled)throw new Exception("Maintenance point-filter import failed: "+p);
            if(!Resources.Load<Texture2D>("MaintenanceShops/"+name))throw new Exception("Missing PNG: "+name);
        }
        var data=JsonUtility.FromJson<MaintenanceRoomPresentation.Content>(Resources.Load<TextAsset>("MaintenanceShops/MaintenanceDialogue").text);
        if(data.first[6]!="얘야, 잠깐만!"||data.first.Length!=10||data.second.Length!=3)throw new Exception("Wrong dialogue content");
        var font=Resources.Load<Font>("PixelInventory/Fonts/Galmuri11-Bold");
        string glyphs=string.Concat(data.first)+string.Concat(data.second)+"마을 장인수로 정비공";
        font.RequestCharactersInTexture(glyphs,12,FontStyle.Normal);
        foreach(char ch in glyphs.Distinct())if(!char.IsWhiteSpace(ch)&&!font.GetCharacterInfo(ch,out _,12,FontStyle.Normal))throw new Exception("Missing maintenance dialogue glyph: "+ch);
        Debug.Log("MAINTENANCE_ASSETS_VALID · room display PNGs, Point, no mipmaps, adopted dialogue");
    }
    static float began;static int phase;static double talkAt,departureAt;static bool departureVerified,notebookCancellationVerified;static Deviant.Combat.CombatKeyboardInput walkingKeyboard;static bool walkingKeyboardWasEnabled;static MaintenanceRoomPresentation presentation;static CommonMaintenanceShop shop;
    static bool First=>SessionState.GetInt("MaintenanceUnityReview.Chapter",2)==1;
    static bool Interactive=>SessionState.GetBool("MaintenanceUnityReview.Interactive",false);
    public static void ProbeChapterOne(){Start(1,false);}
    [MenuItem("Tools/deviant/Maintenance/Play Chapter 1 Shop")]
    public static void OpenChapterOneShop(){Start(1,true);}
    [MenuItem("Tools/deviant/Maintenance/Play Chapter 2 Shop")]
    public static void OpenChapterTwoShop(){Start(2,true);}
    static void Start(int chapter,bool interactive)
    {
        ValidateAssets();SessionState.SetInt("MaintenanceUnityReview.Chapter",chapter);SessionState.SetBool("MaintenanceUnityReview.Interactive",interactive);SessionState.SetBool(Pending,true);
        InstallReviewProfile();
        if(chapter==1)Chapter1ProceduralVerification.Open();else Chapter2Tools.Open();
        began=(float)EditorApplication.timeSinceStartup;phase=0;talkAt=0;departureAt=0;shelfStep=0;departureVerified=notebookCancellationVerified=false;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
    }
    public static void ProbeChapterTwo()
    {
        Start(2,false);
    }
    static void Tick()
    {
        try{
            if(EditorApplication.timeSinceStartup-began>110)throw new Exception("Maintenance scene probe timed out at "+phase);
            if(!EditorApplication.isPlaying)return;
            var first=First?UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>():null;
            var second=First?null:UnityEngine.Object.FindAnyObjectByType<Chapter2World>();
            if(First?(!first||!first.Ready):(!second||!second.Ready))return;
            var host=First?first.gameObject:second.gameObject;
            var arena=First?first.arena:second.arena;var camera=First?first.View:second.sceneCamera;
            presentation=host.GetComponent<MaintenanceRoomPresentation>();shop=host.GetComponent<CommonMaintenanceShop>();
            if(!presentation||presentation.SiteCount==0||!shop)return;
            string prefix=First?"chapter1":"chapter2";
            if(phase==0){if(first){var blueprints=first.GetComponent<Chapter1StartingBlueprints>();if(blueprints&&!blueprints.Complete&&!blueprints.ApplyPreselected(new[]{"sword","blunt"}))return;}arena.Player.Position=presentation.ReviewAnchor+Vector2.left;arena.Player.velocity=Vector2.zero;if(second)second.CameraStep(0,true);phase=1;return;}
            if(phase==1&&presentation.Phase==MaintenanceRoomPresentation.Step.Dialogue){
                if(talkAt==0){talkAt=EditorApplication.timeSinceStartup;return;}if(EditorApplication.timeSinceStartup-talkAt<.8)return;
                var screen=camera.WorldToViewportPoint(arena.Player.Position);if(screen.x<.05f||screen.x>.95f||screen.y<.05f||screen.y>.95f)throw new Exception("Maintenance player outside camera frame");
                Capture(camera,prefix+"-first-visit.png");Debug.Log("MAINTENANCE_FIRST_VISIT_STARTED · "+prefix);
                if(Interactive){SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;return;}
                phase=2;return;
            }
            if(phase==2){if(presentation.Phase==MaintenanceRoomPresentation.Step.Dialogue){presentation.Advance();return;}
                var shelves=presentation.ShelfDisplay;
                if(shelves&&shelves.Active){if(ReviewShelves(arena,camera,prefix,shelves,()=>{if(first)first.RefreshCamera(0,true);else second.CameraStep(0,true);}))phase=3;return;}
                if(shop.IsOpen){Capture(camera,prefix+"-service.png");shop.Close();phase=3;return;}}
            if(phase==3){
                if(First&&!departureVerified){
                    if(presentation.Phase==MaintenanceRoomPresentation.Step.Dialogue)throw new Exception("Call-back started immediately after closing the shop");
                    if(presentation.Phase!=MaintenanceRoomPresentation.Step.AwaitDeparture)return;
                    if(presentation.Capturing||arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat|Deviant.Control.Contracts.InputChannels.Interaction))throw new Exception("Closing shop did not return control for departure");
                    if(departureAt==0){departureAt=EditorApplication.timeSinceStartup;return;}
                    if(EditorApplication.timeSinceStartup-departureAt<.65)return;
                    walkingKeyboard=arena.Player.GetComponent<Deviant.Combat.CombatKeyboardInput>();walkingKeyboardWasEnabled=walkingKeyboard&&walkingKeyboard.enabled;if(walkingKeyboardWasEnabled)walkingKeyboard.enabled=false;
                    Debug.Log("MAINTENANCE_CLOSE_WAITS_FOR_DEPARTURE");phase=6;return;
                }
                if(presentation.Phase==MaintenanceRoomPresentation.Step.PortalNotebook){Capture(camera,"chapter1-portal-notebook.png");presentation.ClosePortalNotebook();return;}
                if(presentation.Phase==MaintenanceRoomPresentation.Step.Dialogue){presentation.Advance();return;}
                if(!presentation.Seen||presentation.Capturing)return;
                if(arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat|Deviant.Control.Contracts.InputChannels.Interaction))throw new Exception("First visit leaked control lease");
                foreach(var renderer in presentation.GetComponentsInChildren<SpriteRenderer>()){
                    if(!new[]{"stage1-desk","stage2-desk","Merchant grounded feet","Merchant breathing upper body"}.Contains(renderer.name))continue;
                    if(renderer.sortingLayerID!=arena.Player.visual.sortingLayerID||renderer.sortingOrder>=arena.Player.visual.sortingOrder)throw new Exception("Shop obscures player: "+renderer.name);
                }
                var facing=presentation.GetComponentsInChildren<Transform>().First(t=>t.name=="Merchant facing player");
                float footY=facing.position.y;
                arena.Player.Position=presentation.ReviewAnchor+Vector2.left*2;presentation.SendMessage("LateUpdate");
                if(facing.localScale.x!=1)throw new Exception("Merchant does not face player on left");arena.Presentation.Draw(0);Capture(camera,prefix+"-look-left.png");
                arena.Player.Position=presentation.ReviewAnchor+Vector2.right*2;presentation.SendMessage("LateUpdate");
                if(facing.localScale.x!=-1||Mathf.Abs(facing.position.y-footY)>.001f)throw new Exception("Merchant right-facing or foot contact failed");arena.Presentation.Draw(0);Capture(camera,prefix+"-look-right.png");
                arena.Player.Position=presentation.ReviewAnchor+Vector2.right*.5f;presentation.SendMessage("LateUpdate");
                if(facing.localScale.x!=-1)throw new Exception("Merchant facing jitters at centre");
                Debug.Log("MAINTENANCE_FACING_PASS · left, right, centre deadband and grounded pivot");
                arena.Presentation.Draw(0);Capture(camera,prefix+"-front-overlap.png");
                arena.Player.Position=presentation.ReviewAnchor+Vector2.left*9;phase=4;return;
            }
            if(phase==6){
                if(presentation.Phase==MaintenanceRoomPresentation.Step.Dialogue){
                    if(presentation.CurrentText!="얘야, 잠깐만!")throw new Exception("Wrong departure call-back");
                    Debug.Log("MAINTENANCE_WALK_AWAY_CALLBACK_PASS");phase=7;return;
                }
                arena.Player.SubmitInput(new Deviant.Combat.Contracts.CombatInput(arena.Player.Position.x<presentation.ReviewAnchor.x?-1:1));return;
            }
            if(phase==7){
                if(presentation.Phase==MaintenanceRoomPresentation.Step.PortalNotebook){
                    if(!arena.Controls.Current.SimulationPaused)throw new Exception("Portal notebook must pause simulation");
                    if(!arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat|Deviant.Control.Contracts.InputChannels.Interaction|Deviant.Control.Contracts.InputChannels.Navigation))throw new Exception("Portal notebook must block gameplay");
                    var notebook=UnityEngine.Object.FindAnyObjectByType<PortalNotebookView>();
                    if(!notebook)throw new Exception("Portal notebook view is missing");
                    const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
                    var shownAt=typeof(PortalNotebookView).GetField("shownAt",hidden);
                    if(!notebookCancellationVerified)
                    {
                        shownAt.SetValue(notebook,Time.unscaledTime);notebook.Confirm();
                        if(presentation.Phase!=MaintenanceRoomPresentation.Step.PortalNotebook)throw new Exception("New notebook accepted its opening input");
                        var active=typeof(MaintenanceRoomPresentation).GetField("active",hidden);var site=active.GetValue(presentation);
                        bool taught=Chapter1ProfileHost.Service.Snapshot.HasSeen(Chapter1ManualCheckpoint.PortalTaughtStory);
                        presentation.SendMessage("OnReset");
                        if(presentation.Capturing||arena.Controls.Current.SimulationPaused||arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat|Deviant.Control.Contracts.InputChannels.Interaction|Deviant.Control.Contracts.InputChannels.Navigation))throw new Exception("Cancelled notebook leaked control");
                        if(Chapter1ProfileHost.Service.Snapshot.HasSeen(Chapter1ManualCheckpoint.PortalTaughtStory)!=taught)throw new Exception("Cancelled notebook recorded completion");
                        active.SetValue(presentation,site);typeof(MaintenanceRoomPresentation).GetMethod("AcquireTalk",hidden).Invoke(presentation,new object[]{6});
                        notebookCancellationVerified=true;Debug.Log("MAINTENANCE_PORTAL_NOTEBOOK_CANCEL_PASS");return;
                    }
                    var timing=JsonUtility.FromJson<ManualCheckpointData>(Resources.Load<TextAsset>("ManualCheckpoint").text);
                    // Installation demo/hold use the view's constants; travel samples follow current gameplay data.
                    const BindingFlags constants=BindingFlags.Static|BindingFlags.NonPublic;
                    float demo=(float)typeof(PortalNotebookView).GetField("DemoInstall",constants).GetRawConstantValue();
                    float hold=(float)typeof(PortalNotebookView).GetField("Hold",constants).GetRawConstantValue();
                    float travelStart=2*(demo+hold),trip=timing.enterSeconds+timing.appearSeconds+hold;
                    float entry=travelStart+timing.enterSeconds*.55f,exit=travelStart+timing.enterSeconds+timing.appearSeconds*.75f;
                    var previews=new[]{demo*.7f,demo+hold+demo*.7f,entry,exit,entry+trip,exit+trip};
                    var names=new[]{"install-c","install-v","enter-right","exit-left","enter-left","exit-right"};
                    for(int i=0;i<previews.Length;i++){notebook.PreviewAt(previews[i]);Capture(camera,"portal-"+names[i]+".png");}
                    if(Environment.GetEnvironmentVariable("PORTAL_NOTEBOOK_CAPTURE_ANIMATION")=="1")
                        for(int frame=0;frame<Mathf.CeilToInt((travelStart+trip*2)*10);frame++){notebook.PreviewAt(frame*.1f);Capture(camera,$"animation-{frame:D3}.png");}
                    notebook.PreviewAt(exit);Capture(camera,"chapter1-portal-notebook.png");
                    shownAt.SetValue(notebook,Time.unscaledTime-.5f);
                    var settings=InputSystem.settings;var background=settings.backgroundBehavior;var editorInput=settings.editorInputBehaviorInPlayMode;
                    settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    var keyboard=InputSystem.AddDevice<Keyboard>();string inputState="";
                    try
                    {
                        keyboard.MakeCurrent();PlayerControls.For(arena.Player).Source.SetFocused(true);
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(CombatKeyboardBindings.For(arena.Player).Interact));InputSystem.Update();
                        var actions=PlayerControls.For(arena.Player);actions.Poll();inputState=$"pressed={keyboard[CombatKeyboardBindings.For(arena.Player).Interact].isPressed}, events={actions.EventCount}, captured={actions.MenuCaptured}, focused={actions.Source.IsFocused}, context={actions.Source.Contexts}";
                        notebook.Tick();
                    }
                    finally{InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editorInput;}
                    if(presentation.Phase!=MaintenanceRoomPresentation.Step.Free||arena.Player.CheckpointBusy)throw new Exception("Notebook interaction key did not close without travelling: "+inputState+" phase="+presentation.Phase);
                    if(arena.Controls.Current.SimulationPaused)throw new Exception("Portal notebook leaked simulation pause");
                    Debug.Log("MAINTENANCE_PORTAL_NOTEBOOK_PASS");return;
                }
                if(presentation.Phase==MaintenanceRoomPresentation.Step.Dialogue){presentation.Advance();return;}
                if(!presentation.Seen)return;
                if(walkingKeyboardWasEnabled&&walkingKeyboard)walkingKeyboard.enabled=true;walkingKeyboardWasEnabled=false;departureVerified=true;phase=3;return;
            }
            if(phase==4){arena.Player.Position=presentation.ReviewAnchor+Vector2.left;phase=5;return;}
            if(phase==5){if(presentation.Capturing)throw new Exception("Revisit repeated first cutscene");Capture(camera,prefix+"-revisit.png");Debug.Log("MAINTENANCE_"+prefix.ToUpperInvariant()+"_PROBE_PASS · real shop opens/closes, visit consumed once, control returned");Finish(0);}
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    // Shelves (2026-11-14): a floor weapon's card, standing on the shelf for an ink's card, buying it with Interact, the whole
    // shelf from the artisan. Ends on the shelf so chapter 1's departure call-back starts from the shop corner.
    static int shelfStep;static double shelfAt;static string[] rerollBefore;
    static bool ReviewShelves(CombatArena arena,Camera camera,string prefix,MaintenanceShelfDisplay shelves,Action snap)
    {
        double now=EditorApplication.timeSinceStartup;var spots=shelves.Spots.ToArray();
        void Put(Vector2 at){arena.Player.Position=at;arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();snap();}
        switch(shelfStep)
        {
            case 0:
                if(spots.Length==0)return false;
                // Review a run that bought shared rerolls: two charges, so the supply crates stand at the row ends.
                var perks=shelves.Perks;
                if(perks!=null&&perks.Settings.SharedRerolls==0){
                    const BindingFlags field=BindingFlags.Instance|BindingFlags.NonPublic;
                    typeof(Deviant.Run.RunPerks).GetField("<Settings>k__BackingField",field).SetValue(perks,new Deviant.Run.RunPerkSettings(rerolls:2));
                    typeof(Deviant.Run.RunPerks).GetProperty("RerollsRemaining").SetValue(perks,2);
                    shelves.RebuildForReview();return false;
                }
                // ...and both steps of the shop display unlock: five goods a row, the stock rolled again, the room laid out
                // with the longest drawing.
                if(shelves.Sketch.State.ExtraShopItems<2){
                    shelves.Sketch.State.ExtraShopItems=2;
                    foreach(var (type,owner) in new (Type,object)[]{(typeof(Deviant.Run.SketchRunState),shelves.Sketch.State),(typeof(Deviant.Run.SketchEquipment),shelves.Sketch)})((System.Collections.IDictionary)type.GetField("shops",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner)).Clear();
                    shelves.RebuildForReview();return false;
                }
                if(spots.Count(s=>s.Kind==MaintenanceShelfDisplay.Kind.Weapon)!=5||spots.Count(s=>s.Kind==MaintenanceShelfDisplay.Kind.Ink)!=5)throw new Exception("Display unlock must lay out five goods a row");
                Debug.Log("MAINTENANCE_SHELF_EXPANDED · "+spots.Length+" places, drawing "+presentation.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name.Contains("-shop-")).name);
                Capture(camera,prefix+"-shelf-room.png");
                if(spots.Count(s=>s.Kind==MaintenanceShelfDisplay.Kind.Weapon)<3||spots.Count(s=>s.Kind==MaintenanceShelfDisplay.Kind.Ink)<3)throw new Exception("Shelves must show three weapons and three inks");
                shelves.Sketch.Economy.Earn(2000);
                Put(spots.First(s=>s.Kind==MaintenanceShelfDisplay.Kind.Weapon).Ground+Vector2.up*.05f);shelfAt=now;shelfStep=1;return false;
            case 1:
                if(now-shelfAt<.7)return false;snap();
                if(shelves.Nearest==null||shelves.Nearest.Kind!=MaintenanceShelfDisplay.Kind.Weapon)throw new Exception("Floor weapon is not in reach");
                Capture(camera,prefix+"-shelf-weapon.png");
                Put(spots.First(s=>s.Kind==MaintenanceShelfDisplay.Kind.Ink).Ground+Vector2.up*.1f);shelfAt=now;shelfStep=2;return false;
            case 2:
                if(now-shelfAt<.9)return false;snap();
                if(!arena.Player.grounded||Mathf.Abs(arena.Player.Position.y-spots.First(s=>s.Upper).Ground.y)>.1f)throw new Exception("Hero does not stand on the shelf: "+arena.Player.Position);
                if(shelves.Nearest==null||shelves.Nearest.Kind!=MaintenanceShelfDisplay.Kind.Ink)throw new Exception("Shelf ink is not in reach");
                Capture(camera,prefix+"-shelf-ink.png");
                var item=shelves.ChoiceAt(shelves.Nearest);int gold=shelves.Sketch.Economy.Gold;
                if(!shelves.TryInteract()||!item.Sold||shelves.Sketch.Economy.Gold!=gold-item.Price)throw new Exception("Interact did not buy the shelf ink");
                Debug.Log("MAINTENANCE_SHELF_BUY_PASS · "+item.Ink.name+" "+item.Price+"G");
                shelfAt=now;shelfStep=3;return false;
            case 3:
                if(now-shelfAt<.5)return false;
                Capture(camera,prefix+"-shelf-bought.png");
                if(!shelves.RerollsUnlocked){Debug.Log("MAINTENANCE_SHELF_REROLL_SKIPPED · no run perks in this scene");shelfStep=9;return true;}
                Put(spots.First(s=>s.Kind==MaintenanceShelfDisplay.Kind.RerollInks).Ground+Vector2.up*.1f);shelfAt=now;shelfStep=4;return false;
            case 4:
                if(now-shelfAt<.9)return false;snap();
                if(shelves.Nearest==null||shelves.Nearest.Kind!=MaintenanceShelfDisplay.Kind.RerollInks)throw new Exception("Ink crate is not in reach");
                Capture(camera,prefix+"-shelf-crate.png");
                rerollBefore=spots.Where(s=>s.Kind==MaintenanceShelfDisplay.Kind.Ink).Select(s=>shelves.ChoiceAt(s)?.Ink?.id).ToArray();
                if(!shelves.TryInteract()||shelves.Perks.RerollsRemaining!=1)throw new Exception("Striking the crate did not spend a shared reroll");
                shelfAt=now;shelfStep=5;return false;
            case 5: if(now-shelfAt<.1)return false;snap();Capture(camera,prefix+"-shelf-reroll-1.png");shelfStep=6;return false;
            case 6: if(now-shelfAt<.33)return false;snap();Capture(camera,prefix+"-shelf-reroll-2.png");shelfStep=7;return false;
            case 7: if(now-shelfAt<.6)return false;snap();Capture(camera,prefix+"-shelf-reroll-3.png");shelfStep=8;return false;
            case 8:
                if(now-shelfAt<1.2)return false;snap();Capture(camera,prefix+"-shelf-rerolled.png");
                var after=spots.Where(s=>s.Kind==MaintenanceShelfDisplay.Kind.Ink).Select(s=>shelves.ChoiceAt(s)?.Ink?.id).ToArray();
                Debug.Log("MAINTENANCE_SHELF_REROLL_PASS · "+string.Join(",",rerollBefore)+" -> "+string.Join(",",after));
                shelfStep=9;return true;
        }
        return true;
    }
    static void Capture(Camera camera,string file)
    {
        var dir=Environment.GetEnvironmentVariable("MAINTENANCE_CAPTURE_DIR");if(string.IsNullOrEmpty(dir))return;Directory.CreateDirectory(dir);
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
        var old=camera.targetTexture;var oldRect=camera.rect;var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        try{
            camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);
            foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+1;}
            foreach(var gui in UnityEngine.Object.FindObjectsByType<TemporaryGameGui>(FindObjectsSortMode.None))gui.FitToWorld();
            foreach(var note in UnityEngine.Object.FindObjectsByType<PortalNotebookView>(FindObjectsSortMode.None))note.FitToWorld();
            foreach(var bubble in UnityEngine.Object.FindObjectsByType<MaintenanceBubbleView>(FindObjectsSortMode.None))bubble.Fit();
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());
        }finally{
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
            RenderTexture.active=previous;camera.targetTexture=old;camera.rect=oldRect;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);
            foreach(var gui in UnityEngine.Object.FindObjectsByType<TemporaryGameGui>(FindObjectsSortMode.None))gui.FitToWorld();Canvas.ForceUpdateCanvases();
        }
    }
    static void Finish(int code){SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;EditorApplication.isPlaying=false;reviewProfile?.Dispose();reviewProfile=null;if(Application.isBatchMode)EditorApplication.Exit(code);}
}
