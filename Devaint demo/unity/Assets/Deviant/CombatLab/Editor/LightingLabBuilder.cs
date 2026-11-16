using System;
using System.IO;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deviant.CombatLab
{
    public static class LightingLabBuilder
    {
        const string Root = "Assets/Deviant/Presentation/Resources/Lighting";
        public const string ScenePath = "Assets/Deviant/CombatLab/Scenes/LightingLab.unity";
        [MenuItem("Tools/deviant/Experiments/Lighting/Open Lab")]
        public static void OpenLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(); EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
        // Generates Unity binding assets only. No production images, normal-map bytes,
        // combat data, project package versions or build scene list are changed.
        public static void Build()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Lighting authoring requires Unity 6000.6.5f1.");
            var json = Resources.Load<TextAsset>("Lighting/Lab");
            if (!json) throw new InvalidOperationException("Run python3 tools/lighting/export_lighting.py first.");
            var catalog = new LightingCatalog(JsonUtility.FromJson<LightingCatalogData>(json.text));
            Directory.CreateDirectory(Root); Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)); AssetDatabase.Refresh();
            var bindings = AssetDatabase.LoadAssetAtPath<LightingBindings>(Root + "/Bindings.asset");
            if (!bindings) { bindings = ScriptableObject.CreateInstance<LightingBindings>(); AssetDatabase.CreateAsset(bindings, Root + "/Bindings.asset"); }
            bindings.litOutlineShader = Resources.Load<Shader>("Lighting/LitInkOutline");
            bindings.connectorShader = Resources.Load<Shader>("Lighting/Connector");
            bindings.windowBeamShader = Resources.Load<Shader>("Lighting/WindowBeam");
            bindings.litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            bindings.unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!bindings.litOutlineShader || !bindings.litShader || !bindings.unlitShader || !bindings.connectorShader || !bindings.windowBeamShader) throw new InvalidOperationException("Lighting shaders are missing.");
            bindings.shadowTemplate = BuildShadow();
            bindings.lanternTemplate = BuildEmitter(catalog.Emitter("lantern"));
            bindings.windowTemplate = BuildEmitter(catalog.Emitter("window"));
            EditorUtility.SetDirty(bindings); AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Lighting Lab").AddComponent<LightingLabComposition>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            Debug.Log("Lighting Lab bindings built for URP 17.6: public cached shadow provider and baked normal-map light settings.");
        }
        static GameObject BuildShadow()
        {
            var root = new GameObject("Cached sprite shadow");
            try
            {
                var source = root.AddComponent<SpriteShadowSource>();
                source.frames = new[] { new SpriteShadowFrame { vertices = LightingLabComposition.Box(.5f, .5f), edges = LightingLabComposition.Loop(4) } };
                var caster = root.AddComponent<ShadowCaster2D>();
                caster.castingOption = ShadowCaster2D.ShadowCastingOptions.CastShadow;
                caster.alphaCutoff = .1f;
                // Editor auto-selects our public provider by its priority. Check the
                // serialized authoring result so Player instances cannot silently fall
                // back to the default square (runtime AddComponent has no selection).
                var serialized = new SerializedObject(caster);
                var provider = serialized.FindProperty("m_ShadowShape2DProvider");
                if (!(provider.managedReferenceValue is CachedSpriteShadowProvider)) throw new InvalidOperationException("URP did not select the cached shadow provider.");
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/ShadowTemplate.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static LightEmitterView BuildEmitter(LightEmitterDefinition definition)
        {
            var root = new GameObject(definition.Id + " light");
            try
            {
                var output = root.AddComponent<Light2D>();
                output.lightType = Light2D.LightType.Point; output.blendStyleIndex = 0;
                var serialized = new SerializedObject(output);
                serialized.FindProperty("m_NormalMapQuality").intValue = (int)Light2D.NormalMapQuality.Accurate;
                serialized.FindProperty("m_NormalMapDistance").floatValue = definition.NormalDistance;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var view = root.AddComponent<LightEmitterView>(); view.Bind(definition, 1);
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + definition.Id + ".prefab").GetComponent<LightEmitterView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
