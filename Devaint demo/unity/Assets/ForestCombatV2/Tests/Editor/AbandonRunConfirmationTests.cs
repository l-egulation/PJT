using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Deviant.Combat;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class AbandonRunConfirmationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        Component gui;
        Type guiType;
        InventoryPresenter presenter;
        GameObject arenaNode, hostNode;
        object Call(string method, params object[] args) => guiType.GetMethod(method, Hidden | BindingFlags.Public).Invoke(gui, args);
        T Get<T>(string name) => (T)guiType.GetField(name, Hidden).GetValue(gui);
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arenaNode = new GameObject("Confirmation controls");
            var arena = arenaNode.AddComponent<CombatArena>(); arena.enabled = false;
            hostNode = new GameObject("Isolated inventory session");
            var host = hostNode.AddComponent<InventoryRuntimeHost>(); host.enabled = false;
            presenter = new InventoryPresenter(new InkInventory(new[] { "sword", "sword" }), arena.Controls);
            typeof(InventoryRuntimeHost).GetProperty("Arena").SetValue(host, arena);
            typeof(InventoryRuntimeHost).GetProperty("Presenter").SetValue(host, presenter);
            guiType = Type.GetType("Deviant.Game.TemporaryGameGui, Assembly-CSharp", true);
            gui = new GameObject("Production pause menu", typeof(RectTransform)).AddComponent(guiType);
            guiType.GetField("host", Hidden).SetValue(gui, host);
            Call("BuildRoot"); Call("InitJournal"); Call("BuildMenu");
            presenter.TogglePause(); Call("RefreshMenu");
            Get<GameObject>("menu").SetActive(true);
            var dim = Get<GameObject>("dim"); dim.SetActive(true); dim.GetComponent<Image>().color = new Color32(36, 39, 43, 244);
            EventSystem.current.SetSelectedGameObject(Get<Button>("menuSecondary").gameObject);
        }
        [Test] public void SoundSliderAndTitleTogglePersistWithoutRebuildingDuringDrag()
        {
            float master=Deviant.Music.GameAudioSettings.Master;
            const string titleKey="deviant.title.glitch-sound";bool hadTitle=PlayerPrefs.HasKey(titleKey);int title=PlayerPrefs.GetInt(titleKey,1);
            try
            {
                Call("OpenApprovedSettings");Call("RequestSettingsTab",1);
                var body=Get<RectTransform>("settingsBody");
                var slider=body.GetComponentsInChildren<Slider>()[0];
                slider.value=3;
                Assert.That(Deviant.Music.GameAudioSettings.Master,Is.EqualTo(.3f).Within(.001f));
                Assert.That(PlayerPrefs.GetFloat(Deviant.Music.GameAudioSettings.MasterKey),Is.EqualTo(.3f).Within(.001f));
                slider.value=7;
                Assert.That(Deviant.Music.GameAudioSettings.Master,Is.EqualTo(.7f).Within(.001f));
                Assert.That(body.GetComponentsInChildren<Slider>()[0],Is.SameAs(slider),"dragging does not replace the slider or lose pointer capture");
                var arrows=body.GetComponentsInChildren<Button>();
                Array.Find(arrows,b=>b.name=="Title sound next").onClick.Invoke();
                Assert.That(PlayerPrefs.GetInt(titleKey),Is.EqualTo(title==0?1:0));
                Call("CloseApprovedSettings");Call("OpenApprovedSettings");Call("RequestSettingsTab",1);
                Assert.That(PlayerPrefs.GetInt(titleKey),Is.EqualTo(title==0?1:0),"reopening settings preserves title effects");
            }
            finally
            {
                Deviant.Music.GameAudioSettings.SetMaster(master);
                if(hadTitle)PlayerPrefs.SetInt(titleKey,title);else PlayerPrefs.DeleteKey(titleKey);PlayerPrefs.Save();
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (gui) Object.DestroyImmediate(gui.gameObject);
            if (hostNode) Object.DestroyImmediate(hostNode);
            presenter?.Dispose();
            if (arenaNode) Object.DestroyImmediate(arenaNode);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator DefaultCancelCoversTheParentAndReturnsFocusWithoutUnpausing()
        {
            int leaves = 0;
            Call("OpenAbandonRunQuestion", (Action)(() => leaves++));
            var cancel = Get<Button>("abandonRunCancel"); var confirm = Get<Button>("abandonRunConfirm");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(cancel.gameObject));
            Assert.That(Get<Button>("menuSecondary").IsInteractable(), Is.False);
            Call("RefreshMenu");
            Assert.That(Get<Button>("menuSecondary").IsInteractable(), Is.False, "A parent refresh cannot uncover the menu.");
            Capture("unity-confirm-cancel");
            EventSystem.current.SetSelectedGameObject(confirm.gameObject);
            Call("RefreshAbandonRunQuestion"); Capture("unity-confirm-leave");
            EventSystem.current.SetSelectedGameObject(cancel.gameObject);
            ExecuteEvents.Execute(cancel.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Null);
            Assert.That(leaves, Is.Zero);
            Assert.That(presenter.Paused, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Get<Button>("menuSecondary").gameObject));
            Assert.That(Get<Button>("menuSecondary").IsInteractable(), Is.True);
            Capture("unity-cancel-restored-pause");
            yield return null;
        }
        [UnityTest] public IEnumerator ExplicitLeaveRunsOnceAndAnOldButtonCannotConfirmANewDialog()
        {
            int leaves = 0;
            Call("OpenAbandonRunQuestion", (Action)(() => leaves++));
            var oldConfirm = Get<Button>("abandonRunConfirm");
            oldConfirm.onClick.Invoke(); oldConfirm.onClick.Invoke();
            Assert.That(leaves, Is.EqualTo(1));
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Null);
            Call("OpenAbandonRunQuestion", (Action)(() => leaves += 10));
            oldConfirm.onClick.Invoke();
            Assert.That(leaves, Is.EqualTo(1), "A deferred old button must not use the new dialog's callback.");
            Get<Button>("abandonRunCancel").onClick.Invoke();
            Assert.That(leaves, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator EscapeClosesOnlyTheConfirmationEvenIfBackIsDeliveredTwice()
        {
            int leaves = 0;
            Call("OpenAbandonRunQuestion", (Action)(() => leaves++));
            Call("Back"); Call("Back");
            Assert.That(leaves, Is.Zero); Assert.That(presenter.Paused, Is.True);
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Null);
            yield return null;
            Call("Back");
            Assert.That(presenter.Paused, Is.False, "A later, separate Back can still resume normally.");
        }
        [UnityTest] public IEnumerator ProductionVillageButtonOpensTheQuestionBeforeAnyNavigation()
        {
            Get<Button>("menuSecondary").onClick.Invoke();
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Not.Null);
            Assert.That(presenter.Paused, Is.True);
            Get<Button>("abandonRunCancel").onClick.Invoke();
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator VillageTitleButtonAsksFirstAndCancelOrEscapeKeepsVillagePaused()
        {
            guiType.GetField("hubPaused", Hidden).SetValue(gui, true);
            Call("RefreshHubMenu");
            Call("VillageMenuSecondary");
            var question = Get<GameObject>("abandonRunQuestion");
            Assert.That(question, Is.Not.Null);
            Assert.That(question.name, Is.EqualTo("타이틀 복귀 확인"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Get<Button>("abandonRunCancel").gameObject));
            Assert.That(Get<Button>("menuSecondary").IsInteractable(), Is.False);
            Capture("unity-title-confirm-cancel");
            EventSystem.current.SetSelectedGameObject(Get<Button>("abandonRunConfirm").gameObject);
            Call("RefreshAbandonRunQuestion"); Capture("unity-title-confirm-leave");
            Get<Button>("abandonRunCancel").onClick.Invoke();
            Assert.That(Get<bool>("hubPaused"), Is.True);
            Assert.That(Get<Button>("menuSecondary").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Get<Button>("menuSecondary").gameObject));
            Call("VillageMenuSecondary"); Call("Back"); Call("Back");
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Null);
            Assert.That(Get<bool>("hubPaused"), Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator TitleReturnRequiresExplicitConfirmationAndRunsOnlyOnce()
        {
            guiType.GetField("hubPaused", Hidden).SetValue(gui, true);
            int leaves = 0;
            Call("OpenTitleReturnQuestion", (Action)(() => leaves++));
            Assert.That(leaves, Is.Zero);
            var confirm = Get<Button>("abandonRunConfirm");
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            Assert.That(leaves, Is.EqualTo(1));
            Assert.That(Get<GameObject>("abandonRunQuestion"), Is.Null);
            yield return null;
        }
        void PrepareShortcutReset()
        {
            var input = gui.gameObject.AddComponent<Deviant.Input.Unity.LocalInput>();
            guiType.GetField("guiInput", Hidden).SetValue(gui, input);
            Call("OpenShortcuts");
            Get<GameObject>("shortcutPanel").SetActive(true);
        }
        [UnityTest] public IEnumerator ShortcutResetButtonAsksFirstAndBackKeepsBindingsAndPanel()
        {
            PrepareShortcutReset();
            var input = Get<Deviant.Input.Unity.LocalInput>("guiInput");
            var jump = input.Source.GetAction(Deviant.Input.Contracts.GameAction.Jump);
            int index = 0;
            while (!Deviant.Input.Unity.KeyboardShortcutSettings.IsKeyboard(jump.bindings[index])) index++;
            input.Source.OverrideBinding(Deviant.Input.Contracts.GameAction.Jump, index, "<Keyboard>/j");
            var reset = Array.Find(Get<GameObject>("shortcutPanel").GetComponentsInChildren<Button>(), b => b.GetComponentInChildren<Text>()?.text == "기본 키로 복원");
            EventSystem.current.SetSelectedGameObject(reset.gameObject);
            reset.onClick.Invoke();
            Assert.That(Get<GameObject>("shortcutResetQuestion"), Is.Not.Null);
            Assert.That(reset.IsInteractable(), Is.False);
            Assert.That(jump.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/j"));
            Capture("unity-key-reset-confirm");
            Call("Back"); Call("Back");
            Assert.That(Get<GameObject>("shortcutResetQuestion"), Is.Null);
            Assert.That(Get<bool>("shortcutPanelOpen"), Is.True);
            Assert.That(reset.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(reset.gameObject));
            Assert.That(jump.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/j"));
            yield return null;
        }
        [UnityTest] public IEnumerator GamepadCanChooseAndConfirmResetAndDiscardWithoutAKeyboard()
        {
            PrepareShortcutReset();
            var background=InputSystem.settings.backgroundBehavior;
            var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad=InputSystem.AddDevice<Gamepad>();int restores=0;
            try
            {
                Call("OpenShortcutResetQuestion",(Action)(()=>restores++));yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));InputSystem.Update();Call("TickShortcutResetQuestion");
                Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("Confirm shortcut reset"));
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();Call("TickShortcutResetQuestion");Call("TickShortcutResetQuestion");
                Assert.That(restores,Is.EqualTo(1));Assert.That(Get<GameObject>("shortcutResetQuestion"),Is.Null);
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();yield return null;
                Call("CloseShortcuts");PrepareDisplaySettings();Get<Button[]>("settingsTabs")[0].onClick.Invoke();
                int width=Get<int>("pendingWidth");SetPendingDisplay(width+64,Get<int>("pendingHeight"),Get<FullScreenMode>("pendingMode"));
                Get<Button[]>("settingsTabs")[1].onClick.Invoke();yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));InputSystem.Update();Call("TickSettingsDraftQuestion");
                Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("Discard display settings"));
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();Call("TickSettingsDraftQuestion");
                Assert.That(Get<GameObject>("settingsDraftQuestion"),Is.Null);Assert.That(Get<int>("settingsTab"),Is.EqualTo(1));
                Assert.That(Get<int>("pendingWidth"),Is.EqualTo(width));
            }
            finally
            {
                InputSystem.RemoveDevice(pad);InputSystem.settings.backgroundBehavior=background;
                InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            }
        }
        [UnityTest] public IEnumerator DisplayArrowsStepInBothDirectionsWithoutChangingTheActualDisplay()
        {
            PrepareDisplaySettings();
            int screenWidth=Screen.width,screenHeight=Screen.height;
            SetPendingDisplay(1280,720,FullScreenMode.Windowed);Call("RefreshApprovedSettings");
            Action<string> click=name=>Array.Find(Get<GameObject>("approvedSettings").GetComponentsInChildren<Button>(),b=>b.name==name).onClick.Invoke();
            click("Resolution next");Assert.That(Get<int>("pendingWidth"),Is.EqualTo(1600));
            click("Resolution previous");Assert.That(Get<int>("pendingWidth"),Is.EqualTo(1280));
            click("Resolution previous");Assert.That(Get<int>("pendingWidth"),Is.EqualTo(960));
            click("Window mode previous");Assert.That(Get<FullScreenMode>("pendingMode"),Is.EqualTo(FullScreenMode.FullScreenWindow));
            click("Window mode next");Assert.That(Get<FullScreenMode>("pendingMode"),Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(Screen.width,Is.EqualTo(screenWidth));Assert.That(Screen.height,Is.EqualTo(screenHeight));
            yield return null;
        }
        [UnityTest] public IEnumerator ControlKeysAreInlineAndCancelOrTabChangeEndsOnlyTheRebinding()
        {
            PrepareShortcutReset();
            var panel=Get<GameObject>("shortcutPanel");var settings=Get<GameObject>("approvedSettings");
            Assert.That(Get<int>("settingsTab"),Is.EqualTo(2));
            Assert.That(panel.transform.IsChildOf(settings.transform),Is.True);
            var labels=Array.ConvertAll(panel.GetComponentsInChildren<Text>(),t=>t.text);
            foreach(var heading in new[]{"이동","전투","탐험","메뉴","지도"})Assert.That(labels,Does.Contain(heading));
            var jump=Array.Find(panel.GetComponentsInChildren<Button>(),b=>b.name.StartsWith("Binding Jump "));
            var input=Get<Deviant.Input.Unity.LocalInput>("guiInput");
            jump.onClick.Invoke();Assert.That(input.Rebinding,Is.True);
            Call("Back");Assert.That(input.Rebinding,Is.False);Assert.That(panel.activeInHierarchy,Is.True);
            yield return null;
            jump.onClick.Invoke();Get<Button[]>("settingsTabs")[0].onClick.Invoke();
            Assert.That(input.Rebinding,Is.False);Assert.That(Get<int>("settingsTab"),Is.Zero);
            Assert.That(settings.activeInHierarchy,Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator ShortcutResetKeyboardFCancelsAndGConfirmsOnlyOnce()
        {
            PrepareShortcutReset();
            var previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            int restores = 0;
            try
            {
                Call("OpenShortcutResetQuestion", (Action)(() => restores++));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
                Assert.That(keyboard.fKey.isPressed, Is.True, "Synthetic keyboard events must reach the Play mode device.");
                Call("TickShortcutResetQuestion");
                Assert.That(Get<GameObject>("shortcutResetQuestion"), Is.Not.Null, "The F that opens a fixed-key dialog must not cancel it in that frame.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
                Call("TickShortcutResetQuestion");
                Assert.That(Get<GameObject>("shortcutResetQuestion"), Is.Null);
                Assert.That(restores, Is.Zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Call("OpenShortcutResetQuestion", (Action)(() => restores++));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); InputSystem.Update();
                Call("TickShortcutResetQuestion"); Call("TickShortcutResetQuestion");
                Assert.That(Get<GameObject>("shortcutResetQuestion"), Is.Null);
                Assert.That(restores, Is.EqualTo(1));
                Call("CloseShortcuts"); Get<GameObject>("shortcutPanel").SetActive(false);
                Call("OpenApprovedSettings");
                Get<Button[]>("settingsTabs")[0].onClick.Invoke();
                int changedWidth = Get<int>("pendingWidth") + 64;
                SetPendingDisplay(changedWidth, Get<int>("pendingHeight"), Get<FullScreenMode>("pendingMode"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
                Get<Button[]>("settingsTabs")[1].onClick.Invoke();
                Call("TickApprovedSettings");
                Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Not.Null, "The tab-opening F must not dismiss the unapplied-settings dialog.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
                Call("TickApprovedSettings");
                Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Null);
                Assert.That(Get<int>("pendingWidth"), Is.EqualTo(changedWidth));
                Assert.That(Get<int>("settingsTab"), Is.Zero);
                Get<Button[]>("settingsTabs")[1].onClick.Invoke();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); InputSystem.Update();
                Call("TickApprovedSettings");
                Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Null);
                Assert.That(Get<int>("settingsTab"), Is.EqualTo(1));
                Assert.That(Get<int>("pendingWidth"), Is.EqualTo(Get<int>("acceptedDisplayWidth")));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
            yield return null;
        }
        void PrepareDisplaySettings()
        {
            var input = gui.gameObject.AddComponent<Deviant.Input.Unity.LocalInput>();
            guiType.GetField("guiInput", Hidden).SetValue(gui, input);
            Call("OpenApprovedSettings");
        }
        void SetPendingDisplay(int width, int height, FullScreenMode mode)
        {
            guiType.GetField("pendingWidth", Hidden).SetValue(gui, width);
            guiType.GetField("pendingHeight", Hidden).SetValue(gui, height);
            guiType.GetField("pendingMode", Hidden).SetValue(gui, mode);
        }
        [UnityTest] public IEnumerator UnappliedDisplayContinueKeepsValuesAndBlocksTheParent()
        {
            PrepareDisplaySettings();
            int changedWidth = Get<int>("pendingWidth") + 64;
            SetPendingDisplay(changedWidth, Get<int>("pendingHeight"), Get<FullScreenMode>("pendingMode"));
            Get<Button[]>("settingsTabs")[1].onClick.Invoke();
            Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Not.Null);
            Assert.That(Get<int>("settingsTab"), Is.Zero);
            Assert.That(Get<Button[]>("settingsTabs")[1].IsInteractable(), Is.False);
            Capture("unity-unapplied-settings-confirm");
            Call("Back"); Call("Back");
            Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Null);
            Assert.That(Get<GameObject>("approvedSettings").activeInHierarchy, Is.True);
            Assert.That(Get<int>("pendingWidth"), Is.EqualTo(changedWidth));
            Assert.That(Get<int>("settingsTab"), Is.Zero);
            Assert.That(Get<Button[]>("settingsTabs")[0].IsInteractable(), Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator UnappliedDisplayDiscardResetsPendingValuesAndOpensTheRequestedTab()
        {
            PrepareDisplaySettings();
            int width = Get<int>("pendingWidth"), height = Get<int>("pendingHeight");
            var mode = Get<FullScreenMode>("pendingMode");
            int screenWidth = Screen.width, screenHeight = Screen.height;
            var screenMode = Screen.fullScreenMode;
            SetPendingDisplay(width + 64, height + 36, mode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Get<Button[]>("settingsTabs")[2].onClick.Invoke();
            var discard = Array.Find(Get<GameObject>("settingsDraftQuestion").GetComponentsInChildren<Button>(), b => b.name == "Discard display settings");
            discard.onClick.Invoke(); discard.onClick.Invoke();
            Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Null);
            Assert.That(Get<int>("settingsTab"), Is.EqualTo(2));
            Assert.That(Get<int>("pendingWidth"), Is.EqualTo(width));
            Assert.That(Get<int>("pendingHeight"), Is.EqualTo(height));
            Assert.That(Get<FullScreenMode>("pendingMode"), Is.EqualTo(mode));
            Assert.That(Screen.width, Is.EqualTo(screenWidth));
            Assert.That(Screen.height, Is.EqualTo(screenHeight));
            Assert.That(Screen.fullScreenMode, Is.EqualTo(screenMode));
            yield return null;
        }
        [UnityTest] public IEnumerator UnchangedOrAcceptedDisplayDoesNotAskBeforeSwitchingTabs()
        {
            PrepareDisplaySettings();
            Get<Button[]>("settingsTabs")[1].onClick.Invoke();
            Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Null);
            Assert.That(Get<int>("settingsTab"), Is.EqualTo(1));
            Call("RequestSettingsTab", 0);
            SetPendingDisplay(Get<int>("pendingWidth") + 64, Get<int>("pendingHeight"), Get<FullScreenMode>("pendingMode"));
            Call("KeepDisplay");
            Get<Button[]>("settingsTabs")[2].onClick.Invoke();
            Assert.That(Get<GameObject>("settingsDraftQuestion"), Is.Null);
            Assert.That(Get<int>("settingsTab"), Is.EqualTo(2));
            yield return null;
        }
        void Capture(string name)
        {
            if (Environment.GetEnvironmentVariable("DEVIANT_UI_REVIEW_CAPTURE") != "1") return;
            var output = Path.GetFullPath(Environment.GetEnvironmentVariable("DEVIANT_UI_REVIEW_OUTPUT") ?? "../outputs/ui-approval-step1-20261114"); Directory.CreateDirectory(output);
            var camera = new GameObject("UI capture camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(65, 64, 58, 255);
            var canvas = gui.GetComponent<Canvas>(); var previousMode = canvas.renderMode;
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active; var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.aspect = 16f / 9;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                guiType.GetMethod("FitToWorld").Invoke(gui, null);
                foreach (var text in gui.GetComponentsInChildren<Text>()) text.SetAllDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = null; camera.targetTexture = null; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(camera.gameObject);
                guiType.GetMethod("FitToWorld").Invoke(gui, null);
            }
        }
    }
}
