using System;
using System.Collections.Generic;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.CombatLab
{
    // Cold Editor capture only. The code vector is keyed by the exact loaded
    // composition module; live query settings are deliberately read every time.
    public static class ExperimentRuntimeProvenanceCapture
    {
        const string HostType = "Deviant.CombatLab.LabComposition";
        const string HostAssembly = "Deviant.CombatLab.Unity";
        static readonly Dictionary<string, ExperimentCodeModule[]> codeVectors = new Dictionary<string, ExperimentCodeModule[]>(StringComparer.Ordinal);

        public static ExperimentRuntimeProvenance Capture(CombatArena arena)
        {
            if (!Application.isEditor || !Application.isPlaying || !arena || !arena.isActiveAndEnabled || !arena.Player ||
                arena.CompositionProfile != CombatCompositionProfile.Lab || arena.gameObject.scene.path != ExperimentGeometry.LabScene)
                throw new InvalidOperationException("Runtime provenance requires the initialized explicit managed Editor Lab.");
            Type host = null;
            foreach (var component in arena.GetComponents<MonoBehaviour>())
            {
                if (!component) continue;
                var type = component.GetType();
                if (type.FullName != HostType || type.Assembly.GetName().Name != HostAssembly) continue;
                if (host != null) throw new InvalidOperationException("The Lab has more than one canonical composition owner.");
                var owner = type.GetField("arena", BindingFlags.Instance | BindingFlags.Public);
                if (owner == null || !ReferenceEquals(owner.GetValue(component), arena))
                    throw new InvalidOperationException("The canonical Lab composition does not own this runtime.");
                host = type;
            }
            if (host == null) throw new InvalidOperationException("The canonical Lab composition owner is required for code provenance.");
            var modules = CodeVector(host.Assembly);
            var settings = new ExperimentQuerySettings(Physics2D.queriesHitTriggers, Physics2D.queriesStartInColliders,
                Physics2D.autoSyncTransforms, Physics2D.defaultContactOffset);
            return new ExperimentRuntimeProvenance(Application.unityVersion, Application.platform.ToString(), SystemInfo.processorType,
                IntPtr.Size * 8, modules, settings);
        }

        static ExperimentCodeModule[] CodeVector(Assembly composition)
        {
            string key = composition.FullName + "/" + RequiredMvid(composition).ToString("N");
            if (codeVectors.TryGetValue(key, out var captured)) return captured;
            var modules = new Dictionary<string, ExperimentCodeModule>(StringComparer.Ordinal);
            var assemblies = new Queue<Assembly>();
            assemblies.Enqueue(composition);
            assemblies.Enqueue(typeof(CombatArena).Assembly);
            assemblies.Enqueue(typeof(CombatInput).Assembly);
            assemblies.Enqueue(typeof(LocomotionProfile).Assembly);
            assemblies.Enqueue(typeof(ExperimentInitialState).Assembly);
            assemblies.Enqueue(typeof(ExperimentSession).Assembly);
            while (assemblies.Count != 0)
            {
                var assembly = assemblies.Dequeue(); string name = assembly.GetName().Name;
                if (modules.ContainsKey(name)) continue;
                if (!GameplayAssembly(name)) throw new InvalidOperationException("An unsupported Editor module entered the replay code vector: " + name);
                modules.Add(name, new ExperimentCodeModule(name, RequiredMvid(assembly)));
                var references = assembly.GetReferencedAssemblies();
                Array.Sort(references, (first, second) => StringComparer.Ordinal.Compare(first.Name, second.Name));
                foreach (var reference in references)
                    if (GameplayAssembly(reference.Name))
                    {
                        var dependency = Assembly.Load(reference);
                        if (dependency.GetName().Name != reference.Name) throw new InvalidOperationException("A named gameplay dependency resolved to another module.");
                        assemblies.Enqueue(dependency);
                    }
            }
            var physics = typeof(Physics2D).Assembly;
            if (physics.GetName().Name != "UnityEngine.Physics2DModule") throw new InvalidOperationException("The actual loaded Unity 2D physics module is required.");
            modules.Add(physics.GetName().Name, new ExperimentCodeModule(physics.GetName().Name, RequiredMvid(physics)));
            captured = new ExperimentCodeModule[modules.Count]; modules.Values.CopyTo(captured, 0);
            Array.Sort(captured, (first, second) => StringComparer.Ordinal.Compare(first.Name, second.Name));
            codeVectors.Add(key, captured); return captured;
        }
        static bool GameplayAssembly(string name)
        {
            if (name == "Deviant.CombatLab.Editor" || name == "Deviant.CombatLab.Contracts") return true;
            return name != null && name.StartsWith("Deviant.", StringComparison.Ordinal) &&
                !name.EndsWith(".Editor", StringComparison.Ordinal) && !name.EndsWith(".Tests", StringComparison.Ordinal);
        }
        static Guid RequiredMvid(Assembly assembly)
        {
            if (assembly.IsDynamic) throw new InvalidOperationException("Dynamic modules cannot identify a reusable Editor replay build.");
            Guid mvid = assembly.ManifestModule.ModuleVersionId;
            if (mvid == Guid.Empty) throw new InvalidOperationException("The current Editor runtime does not expose a real module identity; reusable replay is unavailable.");
            return mvid;
        }
    }
}
