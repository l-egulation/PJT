using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Unity
{
    [Serializable] public sealed class HitPrefabBinding
    {
        public string key, version;
        public GameObject prefab;
        public float duration;
    }
    // One factory per output session. Catalog replacement requires disposing this owner.
    public sealed class PrefabCueFactory : IDisposable
    {
        sealed class Entry
        {
            public GameObject prefab;
            public PrefabCueDefinition definition;
            public int cost, count, active;
            public readonly Stack<GameObject> idle = new Stack<GameObject>();
        }
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly HashSet<View> active = new HashSet<View>();
        readonly Transform root;
        readonly int maxInstances, maxParticles;
        int particles;
        bool disposed;
        public int ActiveCount => active.Count;
        public int ParticleCapacityInUse => particles;
        // Art-review comparison: draw the prefab with runtime paper/graphite copies of its materials.
        // The prefab and its material assets are never written.
        public bool PaperInk { get; set; }
        Color paperOutlineColor = new Color32(26,26,26,255);
        public Color PaperOutlineColor
        {
            get => paperOutlineColor;
            set
            {
                foreach(float channel in new[]{value.r,value.g,value.b,value.a})
                    if(float.IsNaN(channel)||float.IsInfinity(channel)||channel<0||channel>1)
                        throw new ArgumentOutOfRangeException(nameof(value));
                if(value.a!=1)throw new ArgumentException("Paper outlines require an opaque color.");
                paperOutlineColor=value;RefreshPaperMaterials();
            }
        }
        void ApplyPaperStyle(Material material)
        {
            material.SetColor("_OutlineColor",paperOutlineColor);
        }
        void RefreshPaperMaterials()
        {
            if(disposed)throw new ObjectDisposedException(nameof(PrefabCueFactory));
            foreach(var material in paperMaterials.Values)ApplyPaperStyle(material);
        }
        Shader paperShader;
        readonly Dictionary<Material, Material> paperMaterials = new Dictionary<Material, Material>();
        Material Paper(Material source)
        {
            if (!paperShader) paperShader = Resources.Load<Shader>("HitFeedbackPaperInk");
            if (!paperShader) return source;
            if (!paperMaterials.TryGetValue(source, out var paper))
            {
                paper = new Material(paperShader) { name = source.name + " (paper ink)", hideFlags = HideFlags.DontSave };
                paper.mainTexture = source.mainTexture; paper.mainTextureScale = source.mainTextureScale; paper.mainTextureOffset = source.mainTextureOffset;
                if (source.HasProperty("_Color")) paper.SetColor("_Color", source.GetColor("_Color"));
                ApplyPaperStyle(paper);
                paperMaterials.Add(source, paper);
            }
            return paper;
        }
        public PrefabCueFactory(Transform root, HitFeedbackBindings bindings, int maxInstances, int maxParticles)
        {
            if (!root || !bindings || bindings.prefabs == null || maxInstances <= 0 || maxParticles <= 0) throw new ArgumentException("Explicit prefab output and positive budgets required.");
            this.root = root; this.maxInstances = maxInstances; this.maxParticles = maxParticles;
            foreach (var binding in bindings.prefabs)
            {
                if (binding == null || !binding.prefab || entries.ContainsKey(binding.key)) throw new ArgumentException("Missing or duplicate prefab binding.");
                Validate(binding.prefab);
                var entry = new Entry {prefab=binding.prefab, definition=new PrefabCueDefinition(binding.key,binding.key,binding.duration,binding.version)};
                entry.cost = binding.prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.main.maxParticles);
                entries.Add(binding.key,entry);
            }
        }
        public PrefabCueDefinition Definition(string key) => key != null && entries.TryGetValue(key,out var entry) ? entry.definition : null;
        public ICueViewFactory Capture(string key, float angle, float scale, uint seed, int sortingOrder = 60)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PrefabCueFactory));
            if (!entries.TryGetValue(key,out var entry) || !HitFeedbackSettings.Positive(scale) || float.IsNaN(angle) || float.IsInfinity(angle)) throw new ArgumentException("Invalid captured prefab binding.");
            return new Captured(this,entry,angle,scale,seed,sortingOrder);
        }
        public static void Validate(GameObject prefab)
        {
            var systems=prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length==0) throw new ArgumentException("Prefab has no ParticleSystem.");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (!component || !(component is Transform || component is ParticleSystem || component is ParticleSystemRenderer))
                    throw new ArgumentException("Unsupported prefab component; automatic scripts, audio, animation and physics are not accepted.");
            foreach (var ps in systems)
            {
                var main=ps.main;
                if (main.loop || main.stopAction != ParticleSystemStopAction.None || main.playOnAwake || main.simulationSpeed != 1)
                    throw new ArgumentException("Prefab must be a non-looping, externally sampled particle variant.");
                var sub=ps.subEmitters;
                for(int i=0;i<sub.subEmittersCount;i++)
                    if(sub.GetSubEmitterSystem(i)&&!sub.GetSubEmitterSystem(i).transform.IsChildOf(prefab.transform))
                        throw new ArgumentException("External subemitters cannot be owned by this prefab lease.");
                foreach(var renderer in ps.GetComponents<ParticleSystemRenderer>())
                    foreach(var material in renderer.sharedMaterials)
                        if (!material || !material.shader || material.shader.name != "Deviant/HitFeedback/Particle")
                            throw new ArgumentException("Prefab requires the explicit time-independent hit-feedback particle shader.");
            }
        }
        sealed class Captured : ICueViewFactory
        {
            readonly PrefabCueFactory owner;
            readonly Entry entry;
            readonly float angle,scale;
            readonly uint seed;
            readonly int order;
            public Captured(PrefabCueFactory owner,Entry entry,float angle,float scale,uint seed,int order)
            { this.owner=owner;this.entry=entry;this.angle=angle;this.scale=scale;this.seed=seed;this.order=order; }
            public IDisposable Begin(ICueDefinition definition,CueAnchor anchor,Action<CueViewResult> resolved)
            {
                if (owner.disposed || !owner.root || definition.ContentHash != entry.definition.ContentHash)
                { resolved(CueViewResult.Failure("Captured prefab output is unavailable or version-mismatched."));return null; }
                if (entry.active >= owner.maxInstances || owner.particles+entry.cost>owner.maxParticles)
                { resolved(CueViewResult.Failure("Prefab output budget reached."));return null; }
                GameObject go=null;
                while(entry.idle.Count>0 && !go)go=entry.idle.Pop();
                if(!go) { go=Object.Instantiate(entry.prefab,owner.root);entry.count++; }
                go.SetActive(false);go.name="Hit feedback · "+entry.definition.Id;
                go.transform.SetPositionAndRotation(new Vector3(anchor.X,anchor.Y,0),Quaternion.Euler(0,0,angle));
                go.transform.localScale=Vector3.one*scale;
                var view=new View(owner,entry,go,seed,order);
                entry.active++;owner.particles+=entry.cost;owner.active.Add(view);
                resolved(CueViewResult.Ready(view));return null;
            }
        }
        sealed class View : ICueView
        {
            readonly PrefabCueFactory owner;
            readonly Entry entry;
            readonly GameObject instance;
            readonly ParticleSystem[] systems,roots;
            readonly ParticleSystemRenderer[] renderers;
            readonly Material[][] originals;
            float previous=float.NaN;
            bool returned,paper;
            public View(PrefabCueFactory owner,Entry entry,GameObject instance,uint seed,int order)
            {
                this.owner=owner;this.entry=entry;this.instance=instance;
                systems=instance.GetComponentsInChildren<ParticleSystem>(true);
                roots=systems.Where(ps=>!systems.Any(other=>other!=ps && ps.transform.IsChildOf(other.transform))).ToArray();
                renderers=instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
                for(int i=0;i<systems.Length;i++)
                { systems[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);systems[i].useAutoRandomSeed=false;systems[i].randomSeed=seed+(uint)i+1; }
                foreach(var renderer in renderers) { renderer.sortingOrder=order;renderer.enabled=false; }
                originals=renderers.Select(renderer=>renderer.sharedMaterials).ToArray();
            }
            void UsePaper(bool value)
            {
                if(paper==value)return;paper=value;
                for(int i=0;i<renderers.Length;i++)
                    renderers[i].sharedMaterials=value?originals[i].Select(owner.Paper).ToArray():originals[i];
            }
            public void Sample(float elapsed)
            {
                if(returned || owner.disposed || !owner.root || !instance)throw new InvalidOperationException("Prefab lease is no longer available.");
                if(float.IsNaN(elapsed)||float.IsInfinity(elapsed))throw new ArgumentOutOfRangeException(nameof(elapsed));
                if(systems.Any(ps=>!ps)||renderers.Any(renderer=>!renderer))throw new InvalidOperationException("Particle output was destroyed.");
                elapsed=Mathf.Max(0,elapsed);
                UsePaper(owner.PaperInk);
                if(elapsed==previous)return;
                bool restart=float.IsNaN(previous)||elapsed<previous;
                instance.SetActive(true);
                foreach(var ps in roots) { ps.Simulate(restart?elapsed:elapsed-previous,true,restart,false);ps.Pause(true); }
                foreach(var renderer in renderers) { if(!renderer)throw new InvalidOperationException("Particle renderer was destroyed.");renderer.enabled=true; }
                previous=elapsed;
            }
            public void Dispose()
            {
                if(returned)return;returned=true;
                if(instance)
                {
                    foreach(var ps in systems)if(ps)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    foreach(var renderer in renderers)if(renderer)renderer.enabled=false;
                    if(renderers.All(renderer=>renderer))UsePaper(false);
                    instance.SetActive(false);
                    if(!owner.disposed && owner.root)entry.idle.Push(instance);else Object.Destroy(instance);
                }
                entry.active--;owner.particles-=entry.cost;owner.active.Remove(this);
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var view in active.ToArray())view.Dispose();
            foreach(var entry in entries.Values)while(entry.idle.Count>0){var go=entry.idle.Pop();if(go)Object.Destroy(go);}
            foreach(var material in paperMaterials.Values)if(material)Object.Destroy(material);
            paperMaterials.Clear();
        }
    }
}
