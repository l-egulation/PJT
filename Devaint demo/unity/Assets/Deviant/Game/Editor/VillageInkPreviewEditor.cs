using System;
using System.Reflection;
using Deviant.Narrative.Opening;
using Deviant.Run;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Game.Editor
{
    // The preview uses a memory-only profile after the play-mode domain reload.
    [InitializeOnLoad]
    public static class VillageInkPreviewEditor
    {
        const string Pending = "Deviant.VillageInkPreview.Pending";
        static IDisposable previewProfile;
        static int bossPreviewStep;
        static double bossPreviewDeadline, bossPreviewReadyAt;
        static int directPreview;
        public static bool Switching { get; private set; }
        public static int ActiveMode { get; private set; }
        static VillageInkPreviewEditor()
        {
            SessionState.EraseInt("Deviant.VillageInkPreview.Restart");
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) { ClearPreparation(); previewProfile?.Dispose(); previewProfile = null; ActiveMode = 0; return; }
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                int piece = SessionState.GetInt(Pending, 0);
                if (piece == 0) return;
                SessionState.EraseInt(Pending);
                InstallProfile(piece); PrepareMode(piece);
            };
        }

        static void ClearPreparation()
        {
            EditorApplication.update -= PrepareBossPreview; EditorApplication.update -= PrepareDirectPreview; EditorApplication.update -= PrepareRecordPreview;
            bossPreviewStep = directPreview = 0;
        }
        static void InstallProfile(int mode)
        {
            ClearPreparation(); previewProfile?.Dispose(); previewProfile = null;
            previewProfile = Chapter1ProfileHost.UseTemporaryStore(new MemoryProfileStore(CreateProfile(mode)));
            if (Chapter1ProfileHost.Service.LoadWarning != null)
                throw new InvalidOperationException("Village ink preview profile was rejected: " + Chapter1ProfileHost.Service.LoadWarning);
            ActiveMode = mode;
        }
        static void PrepareMode(int mode)
        {
            bossPreviewDeadline = EditorApplication.timeSinceStartup + 60;
            if (mode == 6) { bossPreviewStep = 1; EditorApplication.update += PrepareBossPreview; }
            else if (mode >= 8 && mode <= 9) { directPreview = mode; EditorApplication.update += PrepareDirectPreview; }
            else if (mode >= 11 && mode <= 13) EditorApplication.update += PrepareRecordPreview;
            Debug.Log("VILLAGE_INK_REVIEW_READY: mode=" + mode + "; memory profile only. Use the scene picker to switch.");
        }
        static void SwitchInPlay(int mode)
        {
            // A live scene load avoids the repeated Mono domain unload that crashed this editor on stop/restart.
            InstallProfile(mode);
            SceneManager.LoadScene(mode == 6 || mode == 17 ? Chapter1RunHost.TitleScene : Chapter1RunHost.VillageScene);
            PrepareMode(mode);
            Debug.Log("VILLAGE_INK_LIVE_SWITCH: mode=" + mode + "; domain=" + AppDomain.CurrentDomain.Id);
        }

        // Use the production save validator: a descriptive prefix makes the GUID invalid and discards all seen-story flags.
        public static ProfileData CreateProfile(int piece)
        {
                if (piece == 17) return ProfileData.CreateNew(Guid.NewGuid().ToString());
                if (piece == 14 || piece == 15)
                {
                    var updates = new System.Collections.Generic.List<string> { "opening", "first-return", "artisan-first-run", "artisan-visit-1", "village-ink-return-1", "village-ink-reward-1", "village-executioner-note", "record-journal-written-0", "record-journal-written-1" };
                    if (piece == 15) updates.AddRange(new[] { "artisan-visit-2", "village-ink-return-2", "village-ink-reward-2", "record-journal-written-2" });
                    return new ProfileData { profileId = Guid.NewGuid().ToString(), chaptersCleared = 2, chapter1Clears = 1,
                        chapter1BossFirstClear = true, runsStarted = 1, pigment = 40, seenStories = updates.ToArray() };
                }
                if (piece == 16) return new ProfileData { profileId = Guid.NewGuid().ToString(), chaptersCleared = 1,
                    chapter1Clears = 1, chapter1BossFirstClear = true, runsStarted = 1, pigment = 40,
                    seenStories = new[] { "opening", "first-return" } };
                if (piece == 12 || piece == 13) return new ProfileData
                {
                    profileId = Guid.NewGuid().ToString(), runsStarted = piece == 13 ? 1 : 0,
                    seenStories = piece == 12 ? new[] { "opening", "first-return" }
                        : new[] { "opening", "first-return", "artisan-first-run", "record-journal-written-0" }
                };
                if (piece == 6) return new ProfileData
                {
                    profileId = Guid.NewGuid().ToString(),
                    seenStories = new[] { "opening", "first-return", Chapter1FirstRunInkGuide.GuideInkStory, Chapter1FirstRunInkGuide.GuideBagStory }
                };
                int owned = piece >= 20 ? piece - 20 : piece >= 8 ? 1 : piece == 7 ? 0 : piece;
                var seen = new System.Collections.Generic.List<string> { "opening", "first-return" };
                if (piece != 7) seen.Add("artisan-first-run");
                if (piece == 11) seen.AddRange(new[] { "artisan-visit-1", "village-ink-return-1", "village-ink-reward-1", "village-executioner-note", "record-journal-written-0", "record-journal-written-1" });
                if (piece >= 2 && piece <= 5)
                {
                    for (int k = 1; k < piece; k++) { seen.Add("artisan-visit-" + k); seen.Add("village-ink-return-" + k); seen.Add("village-ink-reward-" + k); }
                    seen.Add("village-executioner-note");
                }
                if (piece >= 20)
                {
                    for (int k = 1; k <= 5; k++) { seen.Add("artisan-visit-" + k); seen.Add("village-ink-return-" + k); seen.Add("village-ink-reward-" + k); }
                    seen.Add("village-executioner-note");
                }
                if (piece == 9) seen.Add("village-ink-return-1");
                return new ProfileData
                {
                    profileId = Guid.NewGuid().ToString(), pigment = 40,
                    chaptersCleared = owned, chapter1Clears = owned > 0 ? 1 : 0, chapter1BossFirstClear = owned > 0,
                    runsStarted = 1, runsDied = piece == 7 ? 1 : 0, seenStories = seen.ToArray()
                };
        }

        static void PrepareRecordPreview()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > bossPreviewDeadline)
            { EditorApplication.update -= PrepareRecordPreview; return; }
            var village = UnityEngine.Object.FindAnyObjectByType<StartingVillageWorld>();
            var gui = UnityEngine.Object.FindAnyObjectByType<TemporaryGameGui>();
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            if (!village || !village.Ready || !gui || !(bool)typeof(TemporaryGameGui).GetField("built", hidden).GetValue(gui)) return;
            var desk = village.Find(StartingVillageWorld.RecordDesk);
            village.World.arena.Player.Position = desk.Position + Vector2.right;
            village.World.arena.Player.velocity = Vector2.zero;
            typeof(TemporaryGameGui).GetMethod("OpenStation", hidden, null, new[] { typeof(StartingVillageWorld.Station), typeof(bool) }, null)
                .Invoke(gui, new object[] { desk, true });
            EditorApplication.update -= PrepareRecordPreview;
            Debug.Log("VILLAGE_INK_RECORD_READY: story book and notebook spread; memory profile only.");
        }

        static void PrepareDirectPreview()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > bossPreviewDeadline)
            { EditorApplication.update -= PrepareDirectPreview; return; }
            var village = UnityEngine.Object.FindAnyObjectByType<StartingVillageWorld>();
            var gui = UnityEngine.Object.FindAnyObjectByType<TemporaryGameGui>();
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            if (!village || !village.Ready || !gui || !(bool)typeof(TemporaryGameGui).GetField("built", hidden).GetValue(gui)) return;
            var type = typeof(TemporaryGameGui);
            var original = (OpeningScript)type.GetProperty("VisitData", hidden).GetValue(gui);
            var replacement = JsonUtility.FromJson<OpeningScript>(JsonUtility.ToJson(original));
            var actions = new System.Collections.Generic.List<OpeningAction> { new OpeningAction { type = "lockInput" } };
            if (directPreview == 8)
            {
                actions.Add(new OpeningAction { type = "artisan", mode = "place", x = village.World.arena.Player.Position.x + 2.3f, face = -1 });
                actions.Add(new OpeningAction { type = "bloom", piece = 1 });
                actions.Add(new OpeningAction { type = "waitAnimation" });
                actions.Add(new OpeningAction { type = "wait", seconds = .7f });
            }
            else actions.Add(new OpeningAction { type = "wait", seconds = .1f });
            actions.Add(new OpeningAction { type = "unlockInput" });
            replacement.beats = new[] { replacement.beats[0] }; replacement.beats[0].actions = actions.ToArray();
            type.GetField("visitScript", hidden).SetValue(gui, replacement);
            try { type.GetMethod("StartVisit", hidden).Invoke(gui, new object[] { 1 }); }
            finally { type.GetField("visitScript", hidden).SetValue(gui, original); }
            EditorApplication.update -= PrepareDirectPreview;
            Debug.Log("VILLAGE_INK_DIRECT_READY: mode=" + directPreview + "; actual game views, memory profile only.");
        }
        static void PrepareBossPreview()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > bossPreviewDeadline)
            { EditorApplication.update -= PrepareBossPreview; bossPreviewStep = 0; return; }
            var host = Chapter1RunHost.Instance;
            if (bossPreviewStep == 1)
            {
                if (!host) return;
                host.StartNewRun(Chapter1ProfileHost.Service.PlanRun(new[] { "sword", "blunt" })); bossPreviewStep = 2; return;
            }
            ForestCombatWorld world = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<ForestCombatWorld>())
                if (candidate.Ready && candidate.GetComponent<Chapter1RunBinding>()?.Run == host?.Flow.Current) { world = candidate; break; }
            if (!world) return;
            var boss = world.GetComponent<Chapter1BossEncounter>(); if (!boss) return;
            if (bossPreviewStep == 2)
            {
                world.arena.Player.maxHealth = world.arena.Player.health = 100000;
                foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
                boss.Boss.health = 0; bossPreviewReadyAt = EditorApplication.timeSinceStartup + 4; bossPreviewStep = 3; return;
            }
            // A sketch-weapon run opens the boss reward choice over the fallen boss (and, on a first run, its guide) and
            // holds the world, so the bottle cannot fall. The preview closes the guide and takes the first reward as a
            // player would; the bottle then falls as it does after a real choice.
            var sketch = world.GetComponent<Chapter1RunBinding>()?.Sketch;
            if (sketch && sketch.FirstRunTutorialShowing) sketch.GetComponentInChildren<Deviant.Tutorial.Unity.FirstRunTutorialWindow>(true)?.Dismiss();
            if (sketch && sketch.State != null && sketch.State.MustOpenBeforeExit)
            { if (sketch.State.PendingOffer == null) sketch.State.OpenBanked(); sketch.State.Choose(0); return; }
            var ink = boss.VillageInkPickup;
            if (!ink || !ink.Landed || EditorApplication.timeSinceStartup < bossPreviewReadyAt) return;
            world.arena.Player.Position = ink.Position + Vector2.left * 1.1f; world.arena.Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); world.RefreshCamera(0, true);
            EditorApplication.update -= PrepareBossPreview; bossPreviewStep = 0;
            Debug.Log("RED_INK_PREVIEW_READY: Press F to collect immediately, then use the exit or scene picker. Memory profile only.");
        }
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/First Red Ink Discovery")]
        public static void PlayBossDiscovery()
        {
            Review(6);
        }
        static void Play(int piece) => Review(piece);
        public static void Review(int mode)
        {
            if (Switching || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) return;
            if (EditorApplication.isPlaying)
            {
                Switching = true;
                EditorApplication.delayCall += () =>
                {
                    try { if (EditorApplication.isPlaying) SwitchInPlay(mode); else Launch(mode); }
                    finally { Switching = false; }
                };
                return;
            }
            Launch(mode);
        }
        static void Launch(int mode)
        {
            SessionState.SetInt(Pending, mode);
            if (mode == 6 || mode == 17)
            {
                EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Chapter1RunHost.TitleScene);
                var game = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (game != null) EditorWindow.GetWindow(game).Show();
                Deviant.Hub.Editor.ApprovedVillageEditor.UseFullHdGameView();
                EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            }
            else Deviant.Hub.Editor.ApprovedVillageEditor.OpenAndPlay();
        }
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/Full Play From Title")]
        public static void OpenFullPlayReview()
        {
            VillageInkReviewWindow.Open();
            EditorApplication.update -= BeginFullPlayWhenReady;
            EditorApplication.update += BeginFullPlayWhenReady;
        }
        static void BeginFullPlayWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= BeginFullPlayWhenReady;
            Review(17);
        }
        public static void OpenReview()
        {
            VillageInkReviewWindow.Open();
            Review(9);
        }
        public static void OpenFirstInkReview()
        {
            VillageInkReviewWindow.Open();
            Review(1);
        }
        public static void OpenRecordReview() { VillageInkReviewWindow.Open(); Review(12); }
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/1 Red")] public static void PlayOne() => Play(1);
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/2 Gold")] public static void PlayTwo() => Play(2);
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/3 Green")] public static void PlayThree() => Play(3);
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/4 Blue")] public static void PlayFour() => Play(4);
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/5 Purple")] public static void PlayFive() => Play(5);
    }

    public sealed class VillageInkReviewWindow : EditorWindow
    {
        Vector2 scroll;
        [MenuItem("Tools/deviant/Hub/Village Ink Preview/Scene Picker")]
        public static void Open()
        {
            var window = GetWindow<VillageInkReviewWindow>("마을 잉크 장면");
            window.minSize = new Vector2(360, 640);
            var main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.x + 16, main.y + 96, 380, 660);
            window.Show();
        }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("마을 잉크 · 장면 선택", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("재생은 유지하고 선택한 장면으로 전환합니다. 실제 저장 슬롯은 바뀌지 않습니다.\n게임 화면을 클릭한 뒤 F로 줍기 / 대화 / 확인하세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(VillageInkPreviewEditor.Switching || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying))
            {
                Choose("처음부터 전체 플레이 (테스트 저장)", 17);
                Choose("① 보스 드롭 · F 줍기부터", 6);
                Choose("② 첫 런 뒤 장인 · 마법안료 / 공방", 7);
                Choose("③ 붉은 잉크 · 마을 컷씬 전체", 1);
                Choose("첫 런 보스 클리어 · 안료 설명까지", 16);
                Choose("④ 우물에 붓기부터", 8);
                Choose("⑤ 마을 잉크 획득 화면부터", 9);
                Choose("⑦ 기록: 처음 깨어난 뒤", 12);
                Choose("⑧ 기록: 첫 런 뒤", 13);
                Choose("⑨ 기록: 붉은 잉크 사용 뒤", 11);
                Choose("새 소식 ! · 농가와 우물", 14);
                Choose("확인 / 잉크 사용 후 · ! 없음", 15);
                EditorGUILayout.Space(); EditorGUILayout.LabelField("다른 잉크 귀환", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                Choose("2 금색", 2); Choose("3 초록", 3); Choose("4 파랑", 4); Choose("5 보라", 5);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(); EditorGUILayout.LabelField("마을 채색 단계만 보기", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i <= 5; i++) Choose(i + "/5", 20 + i);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("대화 / 확인  F    ·    이동  ← / →", EditorStyles.miniLabel);
            if (EditorApplication.isPlaying && GUILayout.Button("미리보기 정지", GUILayout.Height(26))) EditorApplication.isPlaying = false;
            EditorGUILayout.EndScrollView();
        }
        static void Choose(string label, int mode)
        { if (GUILayout.Button(label, GUILayout.Height(30))) VillageInkPreviewEditor.Review(mode); }
    }
}
