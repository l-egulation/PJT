using System;
using Deviant.Combat;
using Deviant.Input.Contracts;
using Deviant.Definitions;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Deviant.Game
{
    // Uses the approved tutorial window style. The merchant owns the modal control scope.
    public sealed class PortalNotebookView : MonoBehaviour
    {
        CombatPlayer player;
        Action confirmed;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform page;
        Font font;
        Text firstKey, secondKey, travelKey, description;
        Image firstKeyFace, secondKeyFace, travelKeyFace;
        Image leftHero, rightHero, leftPortal, rightPortal;
        Text closeKey;
        CharacterClip install, enter, appear;
        ApprovedTraversalArt traversal;
        ManualCheckpointData checkpoint;
        const float DemoInstall = 2f, Hold = .6f, BodyScale = 1f, Floor = 304;
        const float LeftCentre = 370, RightCentre = 590;
        float shownAt;
        static readonly Color Ink = new Color32(243, 239, 222, 255);

        public static PortalNotebookView Create(Transform owner, CombatPlayer player, Action confirmed)
        {
            var view = new GameObject("포탈 사용법 수첩", typeof(RectTransform)).AddComponent<PortalNotebookView>();
            view.transform.SetParent(owner, false);
            view.player = player;
            view.confirmed = confirmed;
            var presentation = owner.GetComponent<Chapter1TraversalPresentation>();
            view.traversal = presentation ? presentation.Art : ApprovedTraversalArt.Load();
            view.Build();
            return view;
        }

        RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        Image Picture(string name, Transform parent, float x, float y, float w, float h, Sprite sprite)
        {
            var image = Box(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        Text Words(string text, float x, float y, float w, float h, int size)
        {
            var label = Box(text, page, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size >= 20 ? 24 : 12; label.text = text; label.color = Ink;
            label.alignByGeometry = true; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        static Sprite Art(string name) => Resources.Load<Sprite>("FirstRunTutorial/Pixels/" + name);

        Text Key(float x, float y, out Image face, float height = 32, int size = 12)
        {
            var frame = Picture("Key frame", page, x, y, height, height, Art("keycap"));
            frame.type = Image.Type.Sliced;
            face = Picture("Key highlight", page, x + 3, y + 3, height - 6, height - 6, null);
            face.color = Color.clear;
            var glyph = Words("", x, y, height, height, size);
            glyph.fontSize = size;
            return glyph;
        }

        static CharacterClip Clip(CheckpointMotionData motion)
        {
            var clip = new CharacterClip { frames = motion.frames, seconds = motion.seconds };
            clip.Load();
            return clip;
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; canvas.pixelPerfect = true;
            scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            page = Box("Safe 960x540 canvas", transform, 0, 0, 960, 540);
            page.anchorMin = page.anchorMax = page.pivot = Vector2.one * .5f; page.anchoredPosition = Vector2.zero;
            var shade = Picture("수첩 뒤 음영", page, 0, 0, 960, 540, null);
            shade.color = new Color(0, 0, 0, .42f); shade.raycastTarget = true;
            font = Deviant.UI.Theme.ApprovedTheme.Font ?? Resources.Load<Font>("FirstRunTutorial/Fonts/Galmuri11");
            if (!Deviant.UI.Theme.ApprovedTheme.ConfigureFontSampling(font) && font && font.material && font.material.mainTexture)
                font.material.mainTexture.filterMode = FilterMode.Point;
            checkpoint = JsonUtility.FromJson<ManualCheckpointData>(Resources.Load<TextAsset>("ManualCheckpoint").text);
            install = Clip(checkpoint.installMotion); enter = Clip(checkpoint.enterMotion); appear = Clip(checkpoint.appearMotion);
            var panel = Picture("Approved tutorial panel", page, 212, 114, 536, 312, Art("panel"));
            panel.type = Image.Type.Sliced;
            Words("포탈", 232, 128, 496, 32, 12);
            firstKey = Key(LeftCentre - 16, 174, out firstKeyFace);
            secondKey = Key(RightCentre - 16, 174, out secondKeyFace);
            travelKey = Key(464, 174, out travelKeyFace);
            // These are the game's original sprite frames, using their native pivots and a shared preview scale.
            leftPortal = Picture("왼쪽 포탈 스프라이트", page, 0, 0, 1, 1, null);
            rightPortal = Picture("오른쪽 포탈 스프라이트", page, 0, 0, 1, 1, null);
            leftHero = Picture("왼쪽 주인공 스프라이트", page, 0, 0, 1, 1, null);
            rightHero = Picture("오른쪽 주인공 스프라이트", page, 0, 0, 1, 1, null);
            description = Words("", 232, 314, 496, 48, 12);
            description.lineSpacing = 1.35f;
            Picture("Footer divider", page, 232, 370, 496, 1, null).color = new Color(1, 1, 1, .25f);
            closeKey = Key(662, 394, out _, Deviant.UI.Theme.ApprovedTheme.ControlHintHeight, Deviant.UI.Theme.ApprovedTheme.ControlHintFontSize);
            closeKey.text = "F";
            var closeCaption = Words("닫기", 690, 394, 32, Deviant.UI.Theme.ApprovedTheme.ControlHintHeight, Deviant.UI.Theme.ApprovedTheme.ControlHintFontSize);
            closeCaption.fontSize = Deviant.UI.Theme.ApprovedTheme.ControlHintFontSize;
            var confirm = Picture("닫기 버튼", page, 658, 384, 78, 36, null);
            confirm.color = Color.clear; confirm.raycastTarget = true;
            var button = confirm.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(Confirm);
            shownAt = Time.unscaledTime;
            Tick();
        }

        public void FitToWorld()
        {
            var target = canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera ? canvas.worldCamera.targetTexture : null;
            float width = target ? target.width : Screen.width, height = target ? target.height : Screen.height;
            scaler.scaleFactor = Mathf.Max(.01f, Mathf.Min(width / 960f, height / 540f));
            canvas.scaleFactor = scaler.scaleFactor;
        }

        public void Tick()
        {
            FitToWorld();
            PreviewAt(Time.unscaledTime - shownAt);
            var input = PlayerControls.For(player);
            if (!input) return;
            input.Poll();
            firstKey.text = input.Label(GameAction.CheckpointInstall);
            secondKey.text = input.Label(GameAction.CheckpointReturn);
            travelKey.text = input.Label(GameAction.Interact);
            description.text = input.FormatPrompt("{CheckpointInstall} 1번 / {CheckpointReturn} 2번 포탈 설치\n같은 키로 현재 위치에 재설치\n가까이에서 {Interact} 왕복, 지도에서 위치 확인");
            closeKey.text = "F";
            if (Time.unscaledTime - shownAt < .35f) { input.SuppressGameplay(); return; }
            // World bindings describe the demonstration; menu confirmation stays fixed.
            var keyboard = Keyboard.current;
            bool close = input.TakeBack() | input.Take(GameAction.Submit);
            close |= keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame);
            input.SuppressGameplay();
            if (close) Confirm();
        }

        public void Confirm()
        {
            if (Time.unscaledTime - shownAt < .35f || confirmed == null) return;
            PlayerControls.For(player)?.SuppressGameplay();
            var action = confirmed; confirmed = null; action();
        }

        static void Draw(Image image, Sprite sprite, float x, int facing, Color tint)
        {
            image.enabled = sprite && tint.a > 0;
            if (!image.enabled) return;
            image.sprite = sprite; image.color = tint;
            var rect = image.rectTransform;
            rect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            float centre = x < 480 ? 350 : 610;
            float previewCentre = x < 480 ? LeftCentre : RightCentre;
            rect.anchoredPosition = new Vector2(Mathf.Round(previewCentre + (x - centre) * (BodyScale / 2f)), -Floor);
            rect.sizeDelta = sprite.rect.size * BodyScale;
            rect.localScale = new Vector3(facing, 1, 1);
        }

        // Presentation-only timeline: never installs or travels with the real player, even while simulation is paused.
        public void PreviewAt(float seconds)
        {
            float installEnd = DemoInstall + Hold;
            float travelStart = installEnd * 2;
            float trip = checkpoint.enterSeconds + checkpoint.appearSeconds + Hold;
            float time = Mathf.Repeat(Mathf.Max(0, seconds), travelStart + trip * 2);
            leftHero.enabled = rightHero.enabled = leftPortal.enabled = rightPortal.enabled = false;
            var normal = new Color32(0, 0, 0, 0);
            var lit = new Color32(242, 193, 78, 85);
            firstKeyFace.color = time < installEnd ? lit : normal;
            secondKeyFace.color = time >= installEnd && time < travelStart ? lit : normal;
            travelKeyFace.color = time >= travelStart ? lit : normal;
            var standing = traversal.portalDraw.At(traversal.portalDraw.Duration, false);
            if (time >= DemoInstall) Draw(leftPortal, standing, 366, 1, Color.white);
            if (time >= installEnd + DemoInstall) Draw(rightPortal, standing, 594, -1, Color.white);
            if (time < travelStart)
            {
                bool left = time < installEnd;
                float age = left ? time : time - installEnd;
                float source = Mathf.Clamp01(age / DemoInstall) * install.Duration;
                Draw(left ? leftHero : rightHero, install.At(source, false), left ? 350 : 610, left ? 1 : -1, Color.white);
                float drawAge = source - traversal.portalDrawStartSeconds;
                if (drawAge >= 0) Draw(left ? leftPortal : rightPortal, traversal.portalDraw.At(drawAge, false), left ? 366 : 594, left ? 1 : -1, Color.white);
                return;
            }
            // After installing on the right, travel right -> left, then left -> right.
            float travelAge = time - travelStart;
            bool fromRight = travelAge < trip;
            float ageInTrip = fromRight ? travelAge : travelAge - trip;
            if (ageInTrip < checkpoint.enterSeconds)
            {
                float progress = ageInTrip / checkpoint.enterSeconds;
                int frame = Mathf.Min(traversal.entryInk.Length - 1, Mathf.FloorToInt(progress * traversal.entryInk.Length));
                var tint = Color.Lerp(Color.white, Color.black, traversal.entryInk[frame]);
                tint.a = 1 - Mathf.InverseLerp(.72f, 1, progress);
                float origin = fromRight ? Mathf.Lerp(640, 594, progress) : Mathf.Lerp(320, 366, progress);
                Draw(fromRight ? rightHero : leftHero, enter.At(progress * enter.Duration, false), origin, fromRight ? -1 : 1, tint);
            }
            else
            {
                float progress = Mathf.Clamp01((ageInTrip - checkpoint.enterSeconds) / checkpoint.appearSeconds);
                var tint = Color.Lerp(Color.white, Color.black, 1 - Mathf.InverseLerp(.15f, .7f, progress));
                tint.a = Mathf.InverseLerp(0, .25f, progress);
                float origin = fromRight ? Mathf.Lerp(366, 320, progress) : Mathf.Lerp(594, 640, progress);
                Draw(fromRight ? leftHero : rightHero, appear.At(progress * appear.Duration, false), origin, fromRight ? -1 : 1, tint);
            }
        }

    }
}
