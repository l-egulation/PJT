using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deviant.Stage1Monsters.Editor
{
    // Compiled C# types alone are insufficient: Unity must also bind the serialized MonoScript assets.
    public sealed class Stage1RuntimeAssetValidation : IPreprocessBuildWithReport
    {
        const string Root = "Assets/Deviant/Stage1Monsters/";
        static readonly Type[] Types = { typeof(Stage1MonsterCatalog), typeof(Stage1MonsterPlayer), typeof(Stage1MonsterEffectCatalog) };
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Validate();
        public static void ReimportAndValidate()
        {
            foreach (var type in Types)
                AssetDatabase.ImportAsset(Root + "Runtime/" + type.Name + ".cs", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in new[] {"Data/ApprovedStage1Monsters.asset", "Data/Stage1MonsterEffects.asset"})
                AssetDatabase.ImportAsset(Root + path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Validate();
        }
        public static void Validate()
        {
            foreach (var type in Types)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(Root + "Runtime/" + type.Name + ".cs");
                if (!script || script.GetClass() != type)
                    throw new BuildFailedException("Stage 1 MonoScript binding is unresolved: " + type.FullName + ". Reimport its runtime scripts and reopen the pinned Unity Editor before rebuilding.");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<Stage1MonsterCatalog>(Root + "Data/ApprovedStage1Monsters.asset");
            var effects = AssetDatabase.LoadAssetAtPath<Stage1MonsterEffectCatalog>(Root + "Data/Stage1MonsterEffects.asset");
            if (!catalog || !effects || catalog.monsters == null || catalog.monsters.Length == 0)
                throw new BuildFailedException("Stage 1 serialized motion/effect catalogs must resolve before packaging.");
            foreach (var monster in catalog.monsters)
            {
                if (!monster.prefab || !monster.prefab.GetComponent<Stage1MonsterPlayer>())
                    throw new BuildFailedException("Stage 1 prefab has no bound animation component: " + monster.id);
                foreach (var component in monster.prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!component) throw new BuildFailedException("Stage 1 prefab contains a missing script: " + monster.id);
            }
        }
    }
}
