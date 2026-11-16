using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.Run;
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
    public static class LiveHudCapture
    {
        const string Active = "LiveHudCapture.Active";
        static readonly string Output = Path.GetFullPath("../outputs/live-hud-mr");
        static int phase;
        static double readyAt;
        static double deadline;
        static InventoryRuntimeHost host;
        static Chapter2World world;
        static TemporaryGameGui gui;
        static CombatArena.ManualSimulation simulation;
        static LiveHudCapture() { EditorApplication.update += Tick; }

        [MenuItem("Tools/deviant/Approved UI/Capture displayed combat HUD")]
        public static void Run()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Displayed HUD capture requires a non-batch Game View.");
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
                    int expectedWidth = phase == 2 ? 1920 : 1280, expectedHeight = phase == 2 ? 1080 : 720;
                    if (!viewport || !viewport.Ready || Screen.width != expectedWidth || Screen.height != expectedHeight ||
                        gui.GetComponent<Canvas>().renderMode != RenderMode.ScreenSpaceOverlay)
                        throw new InvalidOperationException("Game View compositor or HUD resolution is not ready.");
                    var name = phase == 2 ? "actual-play-1920" : "actual-play-1280";
                    File.WriteAllText(Path.Combine(Output, name + ".txt"),
                        "Unity=" + Application.unityVersion + "\nScreen=" + Screen.width + "x" + Screen.height +
                        "\nCanvas=" + gui.GetComponent<Canvas>().renderMode +
                        "\nPixelPresenterReady=" + (viewport && viewport.Ready) +
                        "\nWorldDisplayRect=" + (viewport ? viewport.DisplayRect.ToString() : "missing"));
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output, name + ".png"));
                    phase++; readyAt = EditorApplication.timeSinceStartup + 3;
                    return;
                }
                if (phase == 3)
                {
                    SetSize(1280, 720); phase = 4; readyAt = EditorApplication.timeSinceStartup + 3;
                    return;
                }
                if (phase == 5)
                {
                    var buttons = gui.GetComponentsInChildren<Button>();
                    var bag = buttons.Single(b => b.name == "Bag");
                    var pause = buttons.Single(b => b.name == "Pause");
                    bag.onClick.Invoke();
                    if (!host.Presenter.IsOpen) throw new Exception("Bag button did not open inventory");
                    host.Presenter.Close(); gui.Refresh();
                    pause.onClick.Invoke();
                    if (!host.Presenter.Paused) throw new Exception("Pause button did not pause gameplay");
                    pause.onClick.Invoke();
                    if (host.Presenter.Paused) throw new Exception("Pause button did not resume gameplay");
                    var minimap = buttons.Single(b => b.name == "Minimap");
                    minimap.onClick.Invoke();
                    if (!world.FullMap.IsOpen) throw new Exception("Minimap button did not open full map");
                    world.FullMap.SetOpen(false); gui.Refresh();
                    var images = gui.GetComponentsInChildren<Image>();
                    foreach (var image in images.Where(i => i.name == "Minimap plate" || i == bag.GetComponent<Image>() || i == pause.GetComponent<Image>()))
                    {
                        if (image.sprite.texture.filterMode != FilterMode.Point) throw new Exception("HUD pixel texture must use point filtering");
                        File.WriteAllBytes(Path.Combine(Output, image.name + "-native-pixels.png"), image.sprite.texture.EncodeToPNG());
                        // Hashes of the approved native RGBA pixels, in Unity's bottom-up order.
                        // Kept inline so this verification also works on clean checkouts without local PNG drafts.
                        var expected = image.name == "Bag" ? "57c598fd61da767978585fe1a27f1085f1e0a521708e61cabedbfdc16a8b34aa" :
                            image.name == "Pause" ? "753bb6f7a1da38f91ad3ee30da560105ee18c4b85233f1201fdfc6ac40591ed2" :
                            "ed73345e3535a862a0330ef91bb1553c305cca08ba9bfeae37eab41e6a2d5448";
                        var rgba = image.sprite.texture.GetPixels32().SelectMany(p => new[] { p.r, p.g, p.b, p.a }).ToArray();
                        using (var sha = SHA256.Create())
                            if (BitConverter.ToString(sha.ComputeHash(rgba)).Replace("-", "").ToLowerInvariant() != expected)
                                throw new Exception("Runtime HUD pixels differ from approved PNG: " + image.name);
                    }
                    File.WriteAllText(Path.Combine(Output, "complete.txt"), "PASS: displayed 1920 and 1280 Game View; bag opens; pause/resume; minimap opens; point-filtered pixel textures; all three runtime textures exactly match approved PNG pixels.");
                    SessionState.SetBool(Active, false);
                    simulation?.Dispose(); simulation = null;
                    if (Environment.GetCommandLineArgs().Contains("-quitAfterHudCapture"))
                    {
                        EditorApplication.ExitPlaymode();
                        EditorApplication.delayCall += () => EditorApplication.Exit(0);
                    }
                    // Leave the real scene open for visual inspection.
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

        static void SetSize(int width, int height)
        {
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
    }
}
