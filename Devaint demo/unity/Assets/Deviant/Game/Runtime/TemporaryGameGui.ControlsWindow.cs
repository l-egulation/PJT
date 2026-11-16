using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using Deviant.Tutorial.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The chapter 1 starting room's signpost (user request 2026-11-10: for players who skipped the opening's road). F at the
    // sign opens a window that goes through the road's moves one at a time: the four moves of its cards (run, jump, double
    // jump, dash), then its demonstrations (dash through a shot, wall climb, wall kick). Each page plays the move as a
    // MovePicture at the world's own pixel size (1:1, so the hero's ink rim sits exactly): the basic moves passing through
    // without stopping (in from the left, the move in the middle, out to the right, again), the demonstrations still on
    // their whole stretch of road, except wall moves which keep the native scale and follow the hero. The keys sit under
    // the picture, lit as the picture presses them. ←/→ turn the pages,
    // F or Esc closes it; the shared frame displays only these movement demonstrations.
    public sealed partial class TemporaryGameGui
    {
        const float WindowW = 536, WindowPictureW = 456, WindowPictureH = 132, WindowScale = 32, WindowGround = 18;
        RectTransform controlsWindow, windowKeys;
        Text windowTitle;
        FirstRunTutorialWindow replayWindow;
        MovePicture windowPicture;
        readonly List<(RectTransform cap, Image face, string code, int same, int order)> windowCaps = new List<(RectTransform, Image, string, int, int)>();
        IControlScope windowPause;
        int windowPageIndex, windowMoveSign;
        float windowLabelCheck;
        string windowSignature;
        sealed class WindowMove { public string ghost, text; public string[] keys; public StartingVillageWorld.GhostTrack track; public string[] wallClip; public float[] wallTime; }
        readonly List<WindowMove> windowMoves = new List<WindowMove>();
        public bool ControlsWindowOpen => replayWindow && replayWindow.IsOpen;
        public int ControlsWindowPage => windowPageIndex;
        public string ControlsWindowTitle => ControlsWindowOpen ? windowTitle.text : null;
        public Sprite ControlsWindowPicture => ControlsWindowOpen && windowPageIndex < windowMoves.Count ? windowPicture?.Sprite : null;
        public bool ControlsWindowRing => ControlsWindowOpen && windowPageIndex < windowMoves.Count && windowPicture != null && windowPicture.RingShown;
        public int ControlsWindowPages => windowMoves.Count;

        CombatPlayer StripPlayer => village ? village.World.arena.Player : host && host.Arena ? host.Arena.Player : null;
        WallClimbVisuals WindowClimb() => world ? world.GetComponent<Chapter1TraversalPresentation>()?.ClimbVisuals : village ? village.World.GetComponent<Chapter1TraversalPresentation>()?.ClimbVisuals : null;

        // The road's moves in the cards' order, then any demonstration the cards do not show.
        void CollectWindowMoves()
        {
            windowMoves.Clear();
            foreach (var item in ScriptedControls() ?? new OpeningControlItem[0]) windowMoves.Add(new WindowMove { ghost = item.ghost, text = item.text, keys = item.keys });
            foreach (var beat in OpeningData?.beats ?? new OpeningBeat[0])
                foreach (var action in beat.actions ?? new OpeningAction[0])
                    if (action.type == "demo" && action.mode == "play" && !windowMoves.Exists(m => m.ghost == action.ghost))
                        windowMoves.Add(new WindowMove { ghost = action.ghost, text = action.text, keys = action.keys ?? new string[0] });
            var ghosts = StartingVillageWorld.SharedGhosts;
            windowMoves.RemoveAll(m =>
            {
                m.track = System.Array.Find(ghosts.ghosts ?? new StartingVillageWorld.GhostTrack[0], g => g.id == m.ghost);
                if (m.track == null || m.track.x == null || m.track.x.Length == 0) { Debug.LogWarning("Controls window ghost missing: " + m.ghost); return true; }
                (m.wallClip, m.wallTime) = WallFramesOf(m.track);
                return false;
            });
        }

        // Ink copies of a picture shifted one sprite pixel each way: the hero's rim on the GUI (the game draws it with a
        // material the GUI does not have).
        static readonly Vector2[] Around = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };
        void MakeRim(Transform parent, List<Image> rim, int count)
        {
            rim.Clear();
            for (int i = 0; i < count; i++)
            {
                var image = Rect("Rim " + i, parent, 0, 0, 1, 1).gameObject.AddComponent<Image>();
                image.color = InkColour; image.raycastTarget = false; image.enabled = false; rim.Add(image);
            }
        }
        static void SyncRim(List<Image> rim, Image picture, float pixel)
        {
            for (int i = 0; i < rim.Count; i++)
            {
                var r = rim[i]; r.enabled = picture.enabled && picture.sprite; if (!r.enabled) continue;
                r.sprite = picture.sprite;
                var from = picture.rectTransform; var to = r.rectTransform;
                to.sizeDelta = from.sizeDelta; to.pivot = from.pivot; to.localScale = from.localScale; to.localRotation = from.localRotation;
                to.anchoredPosition = from.anchoredPosition + Around[i] * pixel;
            }
        }

        void BuildControlsWindow()
        {
            var root = new GameObject("Tutorial replay", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            replayWindow = root.AddComponent<FirstRunTutorialWindow>();
            replayWindow.Build();
            controlsWindow = replayWindow.ReplayContent;
            var ink = new Color32(243, 239, 222, 255);
            windowTitle = Label("", controlsWindow, 20, 14, 496, 32, 20, ink);
            windowTitle.font = Deviant.UI.Theme.ApprovedTheme.Font ?? windowTitle.font;
            windowTitle.alignment = TextAnchor.MiddleLeft;
            windowTitle.alignByGeometry = true;
            var area = Rect("Picture", controlsWindow, 40, 52, WindowPictureW, WindowPictureH);
            windowPicture = new MovePicture(this, area, WindowGround, WindowScale, true);
            windowKeys = Rect("Keys", controlsWindow, 0, 200, 1, 28);
        }

        void OpenControlsWindow()
        {
            if (!controlsWindow) BuildControlsWindow();
            CollectWindowMoves();
            if (windowMoves.Count == 0) return;
            HideControls();
            replayWindow.transform.SetAsLastSibling();
            windowPause?.Dispose();
            var arena = host && host.Arena ? host.Arena : village ? village.World.arena : null;
            if (arena)
            {
                windowPause = arena.Controls.OpenScope("controls window");
                windowPause.PauseSimulation(); windowPause.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
            }
            windowMoveSign = 0;
            replayWindow.ShowReplay(windowMoves.Count, ShowWindowPage, CloseControlsWindow);
        }

        void CloseControlsWindow()
        {
            windowPause?.Dispose(); windowPause = null;
            if (replayWindow && replayWindow.IsOpen) replayWindow.Dismiss();
        }

        void ShowWindowPage(int index)
        {
            windowPageIndex = index;
            if (index >= windowMoves.Count) return;
            var move = windowMoves[windowPageIndex];
            windowTitle.text = move.text;
            bool wallMove = move.wallClip != null && System.Array.Exists(move.wallClip, clip => clip != null);
            var framing = wallMove ? MovePicture.Framing.Follow
                : StaysStill(move.track, move.wallClip) ? MovePicture.Framing.Fixed : MovePicture.Framing.Wrap;
            windowPicture.Show(move.track, move.wallClip, move.wallTime, framing);
            LayoutWindowKeys();
            TickControlsWindowPicture(0);
        }

        void LayoutWindowKeys()
        {
            var move = windowMoves[windowPageIndex]; var track = move.track;
            windowSignature = KeySignature(move.keys);
            foreach (Transform child in windowKeys) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            windowCaps.Clear();
            float x = 0;
            for (int k = 0; k < move.keys.Length; k++)
            {
                string code = track.codes != null && k < track.codes.Length ? track.codes[k] : move.keys[k];
                int same = 0, order = 0;
                for (int j = 0; j < move.keys.Length; j++)
                {
                    string other = track.codes != null && j < track.codes.Length ? track.codes[j] : move.keys[j];
                    if (other == code) { same++; if (j <= k) order++; }
                }
                float kw = Keycap(windowKeys, move.keys[k], x, 0, out var cap, out var face);
                windowCaps.Add((cap, face, code, same, order)); x += kw + 4;
            }
            x = Mathf.Max(0, x - 4);
            windowKeys.sizeDelta = new Vector2(x, 28); windowKeys.anchoredPosition = new Vector2(Mathf.Round(WindowW / 2 - x / 2), windowKeys.anchoredPosition.y);
        }

        // Called every frame while the window is open (it owns the keys).
        void TickControlsWindow()
        {
            var input = Controls;
            if (input)
            {
                replayWindow.SetCloseLabel("F");
                bool fixedClose=UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame;
                if (fixedClose || input.Take(GameAction.Submit) || TakeBack()) { input.SuppressGameplay(); CloseControlsWindow(); return; }
                // The UI's navigate action: the window suppresses the gameplay moves while it is open.
                var keyboard=UnityEngine.InputSystem.Keyboard.current;
                float nx = keyboard!=null&&(keyboard.leftArrowKey.isPressed||keyboard.rightArrowKey.isPressed)?(keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0):input.UsingGamepad?input.Read(GameAction.Navigate).X:0; int sign = nx > .5f ? 1 : nx < -.5f ? -1 : 0;
                if (sign != 0 && sign != windowMoveSign) replayWindow.SelectPage(windowPageIndex + sign);
                windowMoveSign = sign;
                input.SuppressGameplay();
            }
            if (windowPageIndex >= windowMoves.Count) return;
            if (Time.unscaledTime >= windowLabelCheck) { windowLabelCheck = Time.unscaledTime + .5f; if (windowSignature != KeySignature(windowMoves[windowPageIndex].keys)) LayoutWindowKeys(); }
            TickControlsWindowPicture(Time.unscaledDeltaTime);
        }

        void TickControlsWindowPicture(float dt)
        {
            var player = StripPlayer;
            float stamina = player && player.arena && player.arena.Movement != null ? player.arena.Movement.WallStaminaDuration : 1;
            windowPicture.Tick(dt, CardBody(), WindowClimb(), village && village.HeroBody == "opening_human", player, stamina);
            float fps = StartingVillageWorld.SharedGhosts.fps > 0 ? StartingVillageWorld.SharedGhosts.fps : 60;
            LightCaps(windowCaps, windowPicture.Track, windowPicture.Frame, windowPicture.Holding, fps);
        }
    }
}
