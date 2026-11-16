using System.Collections;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Game
{
    // Installs the same feature in common game, approved forest and meadow playtest scenes.
    public sealed class InventoryGameBinding : MonoBehaviour
    {
        public InventoryContent content;
        public InventoryPanelView viewPrefab;
        public InventoryRuntimeHost Runtime { get; private set; }
        InventoryPanelView view;
        BattleHudView hud;
        CanvasGroup hudVisibility;
        bool hidingHud, savedRaycasts;
        float savedAlpha;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The village owns its HUD and workbenches through TemporaryGameGui.VillageLoaded.
            // Installing the combat inventory here would overlay its legacy HUD on that GUI.
            if (scene.path == ForestCombatV2.Chapter1RunHost.VillageScene) return;
            bool chapterGui = scene.path == ForestCombatV2.Chapter1RunHost.ChapterScene || scene.path == Deviant.Chapter2.Chapter2EntryBridge.ScenePath;
            foreach (var root in scene.GetRootGameObjects()) foreach (var arena in root.GetComponentsInChildren<CombatArena>(true))
            {
                if (!arena.GetComponent<InventoryGameBinding>()) arena.gameObject.AddComponent<InventoryGameBinding>();
                if (chapterGui && !arena.GetComponent<LegacyScreenSilencer>()) arena.gameObject.AddComponent<LegacyScreenSilencer>();
            }
        }
        IEnumerator Start()
        {
            // Also cover bindings serialized in a village scene rather than installed above.
            if (gameObject.scene.path == ForestCombatV2.Chapter1RunHost.VillageScene) yield break;
            var arena = GetComponent<CombatArena>();
            while (arena && (!arena.Player || arena.CompositionProfile == CombatCompositionProfile.Unconfigured)) yield return null;
            if (!arena) yield break;
            // A random chapter composes over several frames under the loading screen; its devices (full map, boss,
            // checkpoints) appear after the player does, so bind once the scene's world answers Ready.
            while (arena && !WorldComposed()) yield return null;
            if (!arena) yield break;
            // World composition may replace comparison enemies during its own Start.
            yield return null;
            if (!content) content = Resources.Load<InventoryContent>("TemporaryInventoryContent");
            Runtime = gameObject.AddComponent<InventoryRuntimeHost>(); Runtime.Initialize(arena, content);
            bool chapterGui = gameObject.scene.path == ForestCombatV2.Chapter1RunHost.ChapterScene || gameObject.scene.path == Deviant.Chapter2.Chapter2EntryBridge.ScenePath;
            if (chapterGui && !viewPrefab)
            {
                gameObject.AddComponent<TemporaryGuiWorldOutput>();
            }
            var themedPrefab = Runtime.Theme ? Resources.Load<GameObject>("PixelInventory/InventoryView") : null;
            view = viewPrefab ? Instantiate(viewPrefab, transform) : chapterGui ? new GameObject("Temporary game GUI", typeof(RectTransform)).AddComponent<TemporaryGameGui>() :
                themedPrefab ? Instantiate(themedPrefab, transform).GetComponent<InventoryPanelView>() : new GameObject("Temporary inventory UI").AddComponent<TemporaryInventoryView>();
            view.transform.SetParent(transform, false); view.Bind(Runtime);
            foreach (var candidate in Object.FindObjectsByType<BattleHudView>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                { hud = candidate; hud.BindInventory(Runtime.Presenter.Toggle, () => Runtime.Presenter.IsOpen, Runtime.Presenter.Close, () => Runtime && (view is TemporaryGameGui || Runtime.Theme)); break; }
        }
        bool WorldComposed()
        {
            var scene = gameObject.scene;
            foreach (var world in Object.FindObjectsByType<ForestCombatV2.ForestCombatWorld>(FindObjectsSortMode.None))
                if (world.gameObject.scene == scene && world.isActiveAndEnabled && !world.Ready) return false;
            foreach (var world in Object.FindObjectsByType<Deviant.Chapter2.Chapter2World>(FindObjectsSortMode.None))
                if (world.gameObject.scene == scene && world.isActiveAndEnabled && !world.Ready) return false;
            return true;
        }
        void LateUpdate()
        {
            if (Runtime && view) view.Refresh();
            if (Runtime && hud)
            {
                bool hideLegacy = view is TemporaryGameGui || Runtime.Theme || Runtime.Presenter.IsOpen;
                if (hideLegacy && !hidingHud)
                {
                    if (!hudVisibility)
                    {
                        hudVisibility = hud.GetComponent<CanvasGroup>();
                        if (!hudVisibility) hudVisibility = hud.gameObject.AddComponent<CanvasGroup>();
                    }
                    savedAlpha = hudVisibility.alpha; savedRaycasts = hudVisibility.blocksRaycasts;
                    hudVisibility.alpha = 0; hudVisibility.blocksRaycasts = false; hidingHud = true;
                }
                else if (!hideLegacy) RestoreHud();
            }
            if (Runtime && hud && hud.bagText) hud.bagText.text = $"가방 {Runtime.Inventory.Used} / {Runtime.Inventory.Capacity}";
        }
        void RestoreHud()
        {
            if (hidingHud && hudVisibility) { hudVisibility.alpha = savedAlpha; hudVisibility.blocksRaycasts = savedRaycasts; }
            hidingHud = false;
        }
        void OnDisable() { RestoreHud(); }
        void OnDestroy()
        { RestoreHud(); if (hud) hud.BindInventory(null, null, null); if (view) Destroy(view.gameObject); }
    }

    // The play GUI binds a few frames into a chapter scene (InventoryGameBinding.Start waits for the player). Until then
    // the old IMGUI screen drew: the native world picture (VillagePixelViewport), the key line, minimap, HP bars and the
    // developer buttons. IMGUI lies over every canvas, the scene-change notebook included, so it flashed on the way
    // into a chapter (playtest 2026-11-06). From the scene's first frame until the GUI is up, keep all of it switched
    // off; TemporaryGameGui.DiscoverChapter and TemporaryGuiWorldOutput take over from there.
    [DefaultExecutionOrder(-10000)]
    public sealed class LegacyScreenSilencer : MonoBehaviour
    {
        int boundFrames; float started;
        void Awake() { started = Time.unscaledTime; Silence(); }
        void Update() => Silence();
        void LateUpdate() => Silence();
        void Silence()
        {
            var scene = gameObject.scene;
            foreach (var view in Object.FindObjectsByType<Stage1VillageExperiment.VillagePixelViewport>(FindObjectsSortMode.None))
                if (view.gameObject.scene == scene) view.ExternalPresentation = true;
            foreach (var world in Object.FindObjectsByType<ForestCombatV2.ForestCombatWorld>(FindObjectsSortMode.None))
            {
                if (world.gameObject.scene != scene) continue;
                world.ExternalPresentation = true;
                if (world.diagnostics) world.diagnostics.ExternalHud = true;
                if (world.FullMap) world.FullMap.ExternalPresentation = true;
                var go = world.gameObject;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1RunBinding run)) run.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1MaintenanceShop shop)) shop.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1MaintenanceFacility facility)) facility.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1StartingBlueprints blueprints)) blueprints.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1BossEncounter boss)) boss.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1Rewards rewards)) rewards.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.AugmentRewardChoice augments)) augments.ExternalPresentation = true;
                if (go.TryGetComponent(out ForestCombatV2.Chapter1ManualCheckpoint checkpoint)) checkpoint.ExternalPresentation = true;
                if (world.arena && world.arena.TryGetComponent(out Deviant.Combat.CombatFeelFeedback feedback)) feedback.ExternalHud = true;
            }
            foreach (var selector in Object.FindObjectsByType<ForestCombatV2.Chapter1MapSelector>(FindObjectsSortMode.None))
                if (selector.gameObject.scene == scene) selector.ExternalPresentation = true;
            // A couple of frames after the GUI exists it owns all of this; give up after 10 s in a scene without it.
            if (GetComponentInChildren<TemporaryGameGui>(true)) boundFrames++;
            if (boundFrames > 2 || Time.unscaledTime - started > 10) Destroy(this);
        }
    }
}
