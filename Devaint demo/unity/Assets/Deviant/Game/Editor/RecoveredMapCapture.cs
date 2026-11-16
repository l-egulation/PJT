using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.Run;
using Deviant.Input.Contracts;
using Deviant.UI.Theme;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Game.Editor
{
    // Captures the displayed frame, including the native pixel presenter, IMGUI and overlay canvas.
    // This deliberately never changes camera targets or canvas render modes.
    [InitializeOnLoad]
    public static class RecoveredMapCapture
    {
        const string Active = "RecoveredMapCapture.Active";
        static readonly string Output = Path.GetFullPath(Application.isBatchMode ? "../outputs/recovered-map-batch-verification" : "../outputs/recovered-map-camera-footer");
        static int phase;
        static double readyAt;
        static double deadline;
        static InventoryRuntimeHost host;
        static Chapter2World world;
        static TemporaryGameGui gui;
        static CombatArena.ManualSimulation simulation;
        static RecoveredMapCapture() { EditorApplication.update += Tick; }

        [MenuItem("Tools/deviant/Approved UI/Capture recovered map icons")]
        public static void Run()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Unity 6000.6.5f1 required.");
            Directory.CreateDirectory(Output);
            File.Delete(Path.Combine(Output, "failure.txt"));
            File.Delete(Path.Combine(Output, "complete.txt"));
            SessionState.SetBool(Active, true);
            phase = 0;
            readyAt = 0;
            deadline = EditorApplication.timeSinceStartup + 240;
        }

        static void Tick()
        {
            if (File.Exists(Path.Combine(Output, "request.txt"))) { File.Delete(Path.Combine(Output, "request.txt")); Run(); }
            if (!SessionState.GetBool(Active, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 240;
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("HUD capture timed out at phase " + phase);
                if (!EditorApplication.isPlaying)
                {
                    EditorSceneManager.playModeStartScene = null;
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorApplication.EnterPlaymode();
                    return;
                }
                if (phase == 0)
                {
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                    Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
                    Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
                    SceneManager.LoadScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
                    phase = 1;
                    return;
                }
                if (phase == 1)
                {
                    world = Object.FindAnyObjectByType<Chapter2World>();
                    host = Object.FindAnyObjectByType<InventoryRuntimeHost>();
                    gui = Object.FindAnyObjectByType<TemporaryGameGui>();
                    if (!world || !world.Ready || !host || host.Presenter == null || !gui) return;
                    simulation = host.Arena.AcquireManualSimulation();
                    foreach (var enemy in host.Arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
                    host.Arena.Player.health = host.Arena.Player.maxHealth = 100;
                    var records = new System.Collections.Generic.List<string>();
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/boss-crimson"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon boss-crimson");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="f3ab52aad5c15434174b1e6063ff64181e6a989089285574ea0c15ca418f7cfd")throw new Exception("Recovered RGBA mismatch boss-crimson"); records.Add("boss-crimson:25x25:Point:f3ab52aad5c15434174b1e6063ff64181e6a989089285574ea0c15ca418f7cfd");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/chest"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon chest");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="a4a2331cf8e046fd93bcd7c58d7ecf228dbda507e461cdecfb429c68fab13e25")throw new Exception("Recovered RGBA mismatch chest"); records.Add("chest:25x25:Point:a4a2331cf8e046fd93bcd7c58d7ecf228dbda507e461cdecfb429c68fab13e25");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/danger-chest"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon danger-chest");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="cca0a49b85c2a86de5e6b7b7cc099a6f4847c80b01694fa2ed99d758e250305f")throw new Exception("Recovered RGBA mismatch danger-chest"); records.Add("danger-chest:25x25:Point:cca0a49b85c2a86de5e6b7b7cc099a6f4847c80b01694fa2ed99d758e250305f");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/exit"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon exit");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="e51d0696995244ac31402fd411b5672bff6ae32aeaa8d9a42e55dc449c85eaf3")throw new Exception("Recovered RGBA mismatch exit"); records.Add("exit:25x25:Point:e51d0696995244ac31402fd411b5672bff6ae32aeaa8d9a42e55dc449c85eaf3");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/player"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon player");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="a509d4cc5fda8bb7ac660e454977b7dd71feb3fc511fb6645b468d83b259a73a")throw new Exception("Recovered RGBA mismatch player"); records.Add("player:25x25:Point:a509d4cc5fda8bb7ac660e454977b7dd71feb3fc511fb6645b468d83b259a73a");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/portal-1"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon portal-1");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="fc30c4c7773063ee96359c1fb781b540d33cc07b6b807a7a3831c2be76dae97b")throw new Exception("Recovered RGBA mismatch portal-1"); records.Add("portal-1:25x25:Point:fc30c4c7773063ee96359c1fb781b540d33cc07b6b807a7a3831c2be76dae97b");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/portal-2"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon portal-2");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="4e54dcb655c3afefb43ecbef76ccc1dcec0277a3d1584cd0fc927a6b0cefe5db")throw new Exception("Recovered RGBA mismatch portal-2"); records.Add("portal-2:25x25:Point:4e54dcb655c3afefb43ecbef76ccc1dcec0277a3d1584cd0fc927a6b0cefe5db");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/recovery"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon recovery");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="00c528d6c9561db128f4b85ed448a3438782f3811582d00f6edf2e8e8caf62e7")throw new Exception("Recovered RGBA mismatch recovery"); records.Add("recovery:25x25:Point:00c528d6c9561db128f4b85ed448a3438782f3811582d00f6edf2e8e8caf62e7");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/sapling"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon sapling");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="64f094c63d12ea7a150305e4626310246de704c9eb4f84c5c2e2518ce32a4842")throw new Exception("Recovered RGBA mismatch sapling"); records.Add("sapling:25x25:Point:64f094c63d12ea7a150305e4626310246de704c9eb4f84c5c2e2518ce32a4842");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/seal"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon seal");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="12bd554dfdbb4cd1a8cca0aac2eb57494b0a1abf6626c155e46db451860e322c")throw new Exception("Recovered RGBA mismatch seal"); records.Add("seal:25x25:Point:12bd554dfdbb4cd1a8cca0aac2eb57494b0a1abf6626c155e46db451860e322c");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/spirit"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon spirit");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="5e91fa5d6d7ee80b5a9607aa0b49216c4f67e85e70a612bfabac52be3fe455f4")throw new Exception("Recovered RGBA mismatch spirit"); records.Add("spirit:25x25:Point:5e91fa5d6d7ee80b5a9607aa0b49216c4f67e85e70a612bfabac52be3fe455f4");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/trap"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon trap");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="26386f66d021805beec83dd8851741c62b57870a441fcb0d585939ff2da0e0e0")throw new Exception("Recovered RGBA mismatch trap"); records.Add("trap:25x25:Point:26386f66d021805beec83dd8851741c62b57870a441fcb0d585939ff2da0e0e0");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/unknown"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon unknown");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="86bef5d417028e9722a2cbfef0dd955962ba652f4fe3a9fa0f5be6a9a26f357c")throw new Exception("Recovered RGBA mismatch unknown"); records.Add("unknown:25x25:Point:86bef5d417028e9722a2cbfef0dd955962ba652f4fe3a9fa0f5be6a9a26f357c");}
                    {var sprite=Resources.Load<Sprite>("RecoveredMapIcons/workshop-yellow"); if(!sprite||sprite.texture.width!=25||sprite.texture.height!=25||sprite.texture.filterMode!=FilterMode.Point)throw new Exception("Invalid recovered icon workshop-yellow");
                    var bytes=sprite.texture.GetPixels32().SelectMany(p=>p.a==0?new byte[]{0,0,0,0}:new[]{p.r,p.g,p.b,p.a}).ToArray(); using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!="1ca16dd21a08adf72176fb06f496d31a223a7650975398fc7bdff3f2d3e51c0d")throw new Exception("Recovered RGBA mismatch workshop-yellow"); records.Add("workshop-yellow:25x25:Point:1ca16dd21a08adf72176fb06f496d31a223a7650975398fc7bdff3f2d3e51c0d");}
                    File.WriteAllLines(Path.Combine(Output,"runtime-icons.txt"),records);
                    var hero=host.Arena.Player;
                    ((bool[])typeof(CombatPlayer).GetField("portalInstalled",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hero))[0]=false;
                    ((bool[])typeof(CombatPlayer).GetField("portalInstalled",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hero))[1]=false;
                    var portalPositions=(Vector2[])typeof(CombatPlayer).GetField("portalPositions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hero);portalPositions[0]=hero.Position+Vector2.left*3;portalPositions[1]=hero.Position+Vector2.right*3;
                    world.FullMap.SetOpen(false);
                    SetSize(1920, 1080);
                    phase = 2;
                    readyAt = EditorApplication.timeSinceStartup + 4;
                    return;
                }
                if (EditorApplication.timeSinceStartup < readyAt) return;
                if (phase == 2 || phase == 4)
                {
                    gui.FitToWorld(); gui.Refresh(); Canvas.ForceUpdateCanvases();
                    var viewport = Object.FindAnyObjectByType<Stage1VillageExperiment.VillagePixelViewport>();
                    int expectedWidth = 1920, expectedHeight = 1080;
                    if (!Application.isBatchMode && (!viewport || !viewport.Ready || Screen.width != expectedWidth || Screen.height != expectedHeight ||
                        gui.GetComponent<Canvas>().renderMode != RenderMode.ScreenSpaceOverlay))
                        throw new InvalidOperationException("Game View compositor or HUD resolution is not ready.");
                    VerifyMarkerPlacement(phase==4,false);if(phase==4){VerifyLegendOrder();VerifyFooter();CaptureFooterReference(1920,1080);CaptureFooterReference(1280,720);}
                    var name = phase == 2 ? "combat-minimap-1920" : "full-map-1920";
                    File.WriteAllText(Path.Combine(Output, name + ".txt"),
                        "Unity=" + Application.unityVersion + "\nScreen=" + Screen.width + "x" + Screen.height +
                        "\nCanvas=" + gui.GetComponent<Canvas>().renderMode +
                        "\nPixelPresenterReady=" + (viewport && viewport.Ready) +
                        "\nWorldDisplayRect=" + (viewport ? viewport.DisplayRect.ToString() : "missing"));
                    if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(Path.Combine(Output, name + ".png"));
                    phase++; readyAt = EditorApplication.timeSinceStartup + 3;
                    return;
                }
                if (phase == 3)
                {
                    ((bool[])typeof(CombatPlayer).GetField("portalInstalled",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host.Arena.Player))[0]=true;
                    ((bool[])typeof(CombatPlayer).GetField("portalInstalled",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host.Arena.Player))[1]=true;
                    typeof(TemporaryGameGui).GetField("nextMiniMarkers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(gui,0d);
                    gui.Refresh();VerifyMarkerPlacement(false,true);
                    world.FullMap.SetOpen(true); gui.Refresh(); SetSize(1920, 1080); phase = 4; readyAt = EditorApplication.timeSinceStartup + 3;
                    return;
                }
                if (phase == 5)
                {
                    var legend=(ScrollRect)typeof(TemporaryGameGui).GetField("mapLegend",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gui);
                    legend.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();phase=6;readyAt=EditorApplication.timeSinceStartup+3;return;
                }
                if(phase==6)
                {
                    if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(Path.Combine(Output,"full-map-legend-bottom-1920.png"));phase=7;readyAt=EditorApplication.timeSinceStartup+3;return;
                }
                if (phase == 7)
                {
                    File.WriteAllText(Path.Combine(Output, "complete.txt"), "PASS: Unity 6000.6.5f1; " + (Application.isBatchMode ? "batch runtime assertions, no displayed-frame capture; " : "actual 1920x1080 Game View compositor; ") + "minimap and full map share all 14 recovered native 25px point-filtered sprites; portal endpoints installed as a display fixture; runtime RGBA hashes match source.");
                    SessionState.SetBool(Active, false); simulation?.Dispose(); simulation=null;
                    if(Environment.GetCommandLineArgs().Contains("-quitAfterHudCapture")){EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(0);}
                    return;
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Active, false);
                simulation?.Dispose(); simulation = null;
                File.WriteAllText(Path.Combine(Output, "failure.txt"), error.ToString());
                Debug.LogException(error);
                if (Environment.GetCommandLineArgs().Contains("-quitAfterHudCapture")) EditorApplication.Exit(1);
            }
        }

        static void VerifyMarkerPlacement(bool full,bool portals)
        {
            const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            var view=typeof(TemporaryGameGui).GetField(full?"fullMap":"miniMap",hidden).GetValue(gui);
            var type=view.GetType();var clip=(RectTransform)type.GetField("Clip").GetValue(view);
            void Above(Image image,float footY,string label)
            {
                float bottom=image.rectTransform.anchoredPosition.y-image.rectTransform.sizeDelta.y;
                if(bottom<footY+1)throw new Exception(label+" intersects its ground point: bottom="+bottom+" foot="+footY);
            }
            if(full)
            {
                foreach(string name in new[]{"Current player","Portal 1","Portal 2"})
                {
                    var image=clip.GetComponentsInChildren<Image>().First(i=>i.transform.parent.name==name&&i.gameObject.activeInHierarchy);
                    Above(image,0,name);
                    var point=name=="Current player"?world.FullMap.PlayerMapPoint:world.FullMap.WorldMapPoint(host.Arena.Player.PortalPosition(name=="Portal 1"?0:1));
                    var screen=world.FullMap.Viewport.ToScreen(point,clip.sizeDelta);
                    if(Vector2.Distance(((RectTransform)image.transform.parent).anchoredPosition,new Vector2(screen.x,-screen.y))>.01f)throw new Exception("Marker world coordinate moved: "+name);
                }
            }
            else
            {
                Above((Image)type.GetField("PlayerGlyph").GetValue(view),0,"Minimap player");
                if(portals)
                {
                    var viewport=(Chapter1MapViewport)typeof(TemporaryGameGui).GetField("miniViewport",hidden).GetValue(gui);
                    var images=((System.Collections.IEnumerable)type.GetField("Icons").GetValue(view)).Cast<Image>();
                    for(int slot=0;slot<2;slot++)
                    {
                        var image=images.Single(i=>i.gameObject.activeSelf&&i.name==(slot+1)+"번 포탈");
                        var point=viewport.ToScreen(world.FullMap.WorldMapPoint(host.Arena.Player.PortalPosition(slot)),clip.sizeDelta);
                        Above(image,-point.y,"Minimap portal "+slot);
                        if(Mathf.Abs(image.rectTransform.anchoredPosition.x+image.rectTransform.sizeDelta.x/2-point.x)>1)throw new Exception("Minimap portal horizontal position moved");
                    }
                }
            }
            File.AppendAllText(Path.Combine(Output,"placement-checks.txt"),(full?"Full map player + both portals above ground, exact world anchors":portals?"Minimap player + both portals above ground":"Minimap player above ground")+"\n");
        }

        static void VerifyLegendOrder()
        {
            var legend=(ScrollRect)typeof(TemporaryGameGui).GetField("mapLegend",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gui);
            string[] expected={"현재 위치","살아 있는 적","포탈 1","포탈 2","미탐험 방향","마을 출구","보스 · 엘리트","상점","잉크 충전소","봉인 도전방","상자방","정령 구출","묘목 생존","위험 상자","함정 수집"};
            var texts=legend.content.GetComponentsInChildren<Text>();float previous=-1;
            File.WriteAllLines(Path.Combine(Output,"legend-observed.txt"),texts.Select(t=>t.text));
            foreach(var label in expected){var caption=texts.Single(t=>t.text==ApprovedInkText.Display(label));float y=-caption.rectTransform.anchoredPosition.y;if(y<=previous)throw new Exception("Wrong map legend order: "+label);previous=y;}
            var split=legend.content.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Map legend sections");float at=-split.anchoredPosition.y;
            var exit=texts.Single(t=>t.text=="마을 출구").rectTransform;var boss=texts.Single(t=>t.text==ApprovedInkText.Display("보스 · 엘리트")).rectTransform;
            if(at<=-exit.anchoredPosition.y+exit.sizeDelta.y||at>=-boss.anchoredPosition.y)throw new Exception("Legend divider is not between exit and content rooms");
            if(texts.Any(t=>t.text=="카메라 범위"||t.text=="현재 화면 범위"))throw new Exception("Camera range remains inside legend");
            var hint=gui.GetComponentsInChildren<Text>().Single(t=>t.text=="현재 화면 범위");
            if(hint.transform.parent.name!="Current screen range hint"||hint.transform.IsChildOf(legend.content))throw new Exception("Camera hint is not separate from legend");
            File.WriteAllLines(Path.Combine(Output,"legend-order.txt"),expected.Concat(new[]{"Footer: 현재 화면 범위, outside legend"}));
        }

        static void SetSize(int width, int height)
        {
            if(Application.isBatchMode)return;
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(groupType, 0) });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, new[] { Enum.ToObject(kindType, 1), (object)width, height, "HUD capture " + width });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
            view.Show(); view.Focus(); view.Repaint();
        }

        static void VerifyFooter()
        {
            var footer=gui.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Figma map footer");
            if(Vector2.Distance(footer.sizeDelta,new Vector2(1620,62.195f))>.01f||Mathf.Abs(footer.localScale.x-.5f)>.001f)throw new Exception("Figma footer design scale differs");
            string[] names={"지도 이동","확대·축소","현재 위치","전체 보기"};
            float[] xs={0,191.17f,382.95f,536.84f},widths={71.352f,70.188f,34,70.891f};
            string Joined(Transform root)=>string.Concat(root.GetComponentsInChildren<Text>().Select(t=>t.text));
            for(int i=0;i<names.Length;i++)
            {
                var row=(RectTransform)footer.Find("Map hint "+names[i]);
                var cap=(RectTransform)row.Find("Figma map keycap");
                if(Mathf.Abs(row.anchoredPosition.x-xs[i])>.01f||Mathf.Abs(row.anchoredPosition.y+21)>.01f||Vector2.Distance(cap.sizeDelta,new Vector2(widths[i],41.195f))>.01f)throw new Exception("Figma keycap geometry differs: "+names[i]);
                var caption=row.Find("Map caption "+names[i]);
                if(Joined(caption)!=names[i])throw new Exception("Figma caption text differs: "+names[i]);
                foreach(var text in row.GetComponentsInChildren<Text>())foreach(char c in text.text)if(!char.IsWhiteSpace(c)&&!text.font.HasCharacter(c))throw new Exception("Missing footer glyph "+c);
                if(i>=2&&row.GetComponent<Button>()==null)throw new Exception("Clickable map hint missing: "+names[i]);
            }
            var zoom=footer.Find("Map hint 확대·축소/Map key {MapZoomIn} / {MapZoomOut}");
            // Find by direct children: action templates contain slash characters.
            zoom=footer.Find("Map hint 확대·축소").Cast<Transform>().Single(t=>t.name=="Map key {MapZoomIn} / {MapZoomOut}");
            if(Joined(zoom)!="＋ / −")throw new Exception("Default zoom glyphs differ: "+Joined(zoom));
            var input=PlayerControls.For(host.Arena.Player);string saved=input.Source.SaveBindingOverrides();
            try
            {
                var action=input.Source.GetAction(GameAction.MapZoomIn);int index=-1;
                for(int i=0;i<action.bindings.Count;i++)if(action.bindings[i].effectivePath=="<Keyboard>/equals")index=i;
                if(index<0)throw new Exception("Default map zoom key missing");
                input.Source.OverrideBinding(GameAction.MapZoomIn,index,"<Keyboard>/k");
                typeof(TemporaryGameGui).GetMethod("RefreshMapFooterPrompts",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(gui,null);
                if(Joined(zoom)!="K / −")throw new Exception("Rebound zoom label is stale: "+Joined(zoom));
            }
            finally{input.Source.LoadBindingOverrides(saved);typeof(TemporaryGameGui).GetMethod("RefreshMapFooterPrompts",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(gui,null);}
            File.WriteAllText(Path.Combine(Output,"footer-checks.txt"),"PASS: Figma native positions/sizes, four captions, all glyphs present, default +/minus display, rebound K prompt, focus/fit buttons retained.");
        }

        static void CaptureFooterReference(int width,int height)
        {
            // Isolated Unity UI render for design comparison, never a displayed Game View claim.
            var canvas=gui.GetComponent<Canvas>();var camera=world.sceneCamera;
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldDistance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var oldRect=camera.rect;var oldActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+1;
                gui.FitToWorld();foreach(var text in gui.GetComponentsInChildren<Text>())text.SetAllDirty();Canvas.ForceUpdateCanvases();
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,"footer-ui-reference-"+width+".png"),image.EncodeToPNG());
            }
            finally{canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;camera.targetTexture=oldTarget;camera.rect=oldRect;RenderTexture.active=oldActive;gui.FitToWorld();RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
    }
}
