using System;
using Deviant.Combat;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Integration
{
    // Chapter composition owns this additional output. Existing hit feedback, movement and time owners are untouched.
    [DisallowMultipleComponent]
    public sealed class PlayerDeathPresentation : MonoBehaviour
    {
        const int AboveHudOrder = 110, BelowResultOrder = 90;
        readonly PlayerDeathSequence sequence = new PlayerDeathSequence();
        readonly CameraKickMixer kick = new CameraKickMixer(1, 12);
        CombatArena arena;
        CameraOutputBinding.KickSource cameraSource;
        FadeOutputBinding fade;
        FadeOutputBinding.BaseSource fadeSource;
        Canvas overlay;
        bool subscribed, resultRevealed;
        float diedAt;
        int facing;
        public bool Active => sequence.Active;
        public bool ReadyForResult => sequence.ResultReady;
        public float FadeOpacity => sequence.FadeOpacity;
        public FeedbackOffset KickPixels => kick.ReadPixels();
        public int OverlayOrder => overlay ? overlay.sortingOrder : 0;

        public static PlayerDeathPresentation Attach(CombatArena owner)
        {
            if (!owner || !owner.Player || !owner.ViewCamera) throw new ArgumentException("Initialized chapter combat required.");
            var output = owner.GetComponent<PlayerDeathPresentation>();
            if (output) return output;
            output = owner.gameObject.AddComponent<PlayerDeathPresentation>();
            output.arena = owner; output.CreateOverlay(); output.Bind(); return output;
        }
        void CreateOverlay()
        {
            var root = new GameObject("Player death fade", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            overlay = root.GetComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = AboveHudOrder; overlay.targetDisplay = arena.ViewCamera.targetDisplay;
            var image = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(root.transform, false);
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.raycastTarget = false; image.color = Color.clear;
            fade = root.AddComponent<FadeOutputBinding>(); fade.Configure(image, new FadeFrame(0, 0, 0, 0));
            fadeSource = fade.BindBaseSource("chapter player death");
        }
        void Bind()
        {
            if (subscribed || !arena || !arena.Player) return;
            cameraSource = CameraOutputBinding.GetOrCreate(arena.ViewCamera).BindKickSource(kick);
            arena.Player.Died += ObserveDeath; arena.ResetCompleted += ResetOutput;
            arena.FeedbackAccentAdvanced += Advance; arena.Deactivated += ResetOutput; subscribed = true;
        }
        public void ObserveDeath()
        {
            if (!isActiveAndEnabled || !arena || !arena.Player) return;
            if (sequence.Clearing) return;
            if (!arena.Player.Dead) { if (Active) ResetOutput(); return; }
            if (Active) return;
            diedAt = arena.Now; facing = arena.Player.facing;
            sequence.Begin(DeathDuration()); resultRevealed = false;
            overlay.sortingOrder = AboveHudOrder;
        }
        // A chapter clear blacks out with the death's fade and hold (no kick, no pose wait), then RevealResult as for a death.
        public void BeginClearBlackout()
        {
            if (!isActiveAndEnabled || !arena || Active) return;
            sequence.BeginClear(); resultRevealed = false; overlay.sortingOrder = AboveHudOrder;
        }
        float DeathDuration()
        {
            string form = arena.Presentation.CurrentForm ?? arena.Player.Form.id;
            var sets = arena.characterAnimations?.sets;
            if (sets != null)
            {
                var set = Array.Find(sets, value => value.form == form) ?? Array.Find(sets, value => value.form == "base_sword");
                var clip = set?.Find("death"); if (clip != null && clip.Duration > 0) return clip.Duration;
            }
            return .8f;
        }
        void Advance(float realDelta)
        {
            ObserveDeath(); if (!Active) return;
            sequence.Advance(realDelta, Mathf.Max(0, arena.Now - diedAt));
            var pixels = sequence.ReadKick(facing); kick.Clear();
            float size = Mathf.Sqrt(pixels.X * pixels.X + pixels.Y * pixels.Y);
            if (size > 0) kick.Add(pixels.X, pixels.Y, size, 1);
            SubmitFade();
        }
        void SubmitFade()
        {
            if (fadeSource == null || !fadeSource.IsActive) return;
            fadeSource.Submit(new FadeFrame(0, 0, 0, sequence.FadeOpacity));
            if (fade) fade.ApplyNow();
        }
        // The current result UI calls this only once its own screen is visible. Black stays behind the paper result.
        public void RevealResult()
        {
            if (!ReadyForResult || resultRevealed) return;
            resultRevealed = true; overlay.sortingOrder = BelowResultOrder;
        }
        void ResetOutput()
        {
            sequence.Reset(); kick.Clear(); resultRevealed = false;
            if (overlay) overlay.sortingOrder = AboveHudOrder;
            SubmitFade();
        }
        void Unbind()
        {
            if (!subscribed) return;
            if (arena)
            {
                if (arena.Player) arena.Player.Died -= ObserveDeath;
                arena.ResetCompleted -= ResetOutput; arena.FeedbackAccentAdvanced -= Advance; arena.Deactivated -= ResetOutput;
            }
            cameraSource?.Dispose(); cameraSource = null; subscribed = false;
        }
        void OnEnable() { if (overlay) overlay.gameObject.SetActive(true); Bind(); }
        void OnDisable() { ResetOutput(); Unbind(); if (overlay) overlay.gameObject.SetActive(false); }
        void OnDestroy()
        {
            Unbind(); fadeSource?.Dispose();
            if (overlay) { if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject); }
        }
    }
}
