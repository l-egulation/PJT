using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class RangedActionWindow : EditorWindow
    {
        [SerializeField] RangedActionDraft edits;
        [SerializeField] string repository;
        [SerializeField] CombatArena runtime;
        RangedActionSourceSnapshot saved;
        RangedActionCatalog accepted;
        VisualElement fields;
        Label status, version;
        DropdownField selection;
        TextField path;
        string message;
        RangedActionGeometryPreview geometry;
        public RangedActionSourceSnapshot Saved => saved;
        public RangedActionCatalog Candidate => accepted;
        public RangedActionNumericData Numeric => edits ? edits.numeric : null;
        [MenuItem("Tools/deviant/Experiments/Ranged Action Numerics")]
        public static void Open() => GetWindow<RangedActionWindow>("Ranged numerics");
        void OnEnable() { Undo.undoRedoPerformed += UndoChanged; }
        void OnDisable() { Undo.undoRedoPerformed -= UndoChanged; }
        void OnDestroy() { if(edits)DestroyImmediate(edits); }
        void UndoChanged() { Rebuild(); Refresh(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear();var scroll=new ScrollView();rootVisualElement.Add(scroll);
            scroll.Add(Wrapped("Launcher/bomb/shuriken basics and their four ink-selected D skills · all sixteen records. Closed policies, resource/cooldown metadata, flight origins and damage categories remain unchanged."));
            path=new TextField("Source repository"){name="repository",value=repository};path.RegisterValueChangedCallback(e=>repository=e.newValue);scroll.Add(path);
            scroll.Add(Button("load","Load both canonical sources",()=>Run(()=>LoadRepository(repository))));
            var runtimeField=new ObjectField("Explicit runtime arena"){name="runtime",objectType=typeof(CombatArena),allowSceneObjects=true,value=runtime};runtimeField.RegisterValueChangedCallback(e=>runtime=e.newValue as CombatArena);scroll.Add(runtimeField);
            selection=new DropdownField("Existing action"){name="action"};selection.RegisterValueChangedCallback(e=>Run(()=>Select(e.newValue)));scroll.Add(selection);
            version=Wrapped("");version.name="version";scroll.Add(version);status=Wrapped("");status.name="status";scroll.Add(status);
            fields=new VisualElement{name="numericFields"};scroll.Add(fields);
            geometry=new RangedActionGeometryPreview();scroll.Add(geometry);
            var apply=new VisualElement();apply.style.flexDirection=FlexDirection.Row;apply.style.flexWrap=Wrap.Wrap;scroll.Add(apply);
            apply.Add(Button("applyNext","Apply complete candidate to next start",()=>Run(()=>Apply(false))));
            apply.Add(Button("applyReset","Apply complete candidate and development reset",()=>Run(()=>Apply(true))));
            apply.Add(Button("restore","Restore saved candidate to next start",()=>Run(()=>{if(saved==null)throw new InvalidOperationException("Load a source first.");ApplyCatalog(saved.Catalog,false);})));
            scroll.Add(Button("save","Save selected owning source atomically",()=>Run(SaveSource)));
            scroll.Add(Button("regenerate","Regenerate saved source and validate freshness",()=>Run(Regenerate)));
            scroll.Add(Wrapped("Save, regenerate and runtime Apply are separate. The other canonical file is checked but not part of a cross-file transaction. Shuriken basic flight and inherited bomb fields have one shared source owner. Private preview samples geometry only; actual flight/cancellation/return/missile behavior is observed in the explicitly bound runtime."));
            Rebuild();Refresh();
        }
        public void LoadRepository(string value)
        {
            var next=RangedActionSource.Load(value);saved=next;accepted=next.Catalog;repository=value;
            if(!edits){edits=CreateInstance<RangedActionDraft>();edits.hideFlags=HideFlags.HideAndDontSave;}
            Undo.RecordObject(edits,"Load ranged numeric source");edits.selectedId=accepted.Actions[0].Id;edits.numeric=accepted.Actions[0].ToNumericData();path?.SetValueWithoutNotify(repository);
            message="Both canonical files loaded; no runtime or source mutation.";Rebuild();Refresh();
        }
        public void BindRuntime(CombatArena arena){runtime=arena;rootVisualElement.Q<ObjectField>("runtime")?.SetValueWithoutNotify(arena);Refresh();}
        void Select(string id)
        {
            if(accepted==null)return;CommitDraft();var selected=accepted.Read(id)??throw new ArgumentException("Select an existing action.");
            Undo.RecordObject(edits,"Select ranged action");edits.selectedId=id;edits.numeric=selected.ToNumericData();Rebuild();Refresh();
        }
        public RangedActionCatalog CompileCandidate(){CommitDraft();return accepted;}
        void CommitDraft()
        {
            if(accepted==null||!edits||edits.numeric==null)throw new InvalidOperationException("Load the complete ranged source first.");
            accepted=accepted.Edit(edits.selectedId,edits.numeric);
        }
        public void Apply(bool reset){CommitDraft();ApplyCatalog(accepted,reset);message=reset?"Complete candidate published and existing development reset performed.":"Complete candidate published for new starts. Existing casts and released effects remain pinned.";}
        void ApplyCatalog(RangedActionCatalog candidate,bool reset)
        {
            if(!EditorApplication.isPlaying||!runtime)throw new InvalidOperationException("Select the existing common arena in Play mode.");
            if(!runtime.TryApplyRangedActions(candidate.Actions,reset,out var error))throw new InvalidOperationException(error);
        }
        public void SaveSource()
        {
            CommitDraft();saved=RangedActionSource.Save(saved,accepted,edits.selectedId);accepted=saved.Catalog;edits.numeric=accepted.Read(edits.selectedId).ToNumericData();message="Selected canonical owning file saved atomically; runtime and generated catalog unchanged.";Rebuild();Refresh();
        }
        public void Regenerate()
        {
            if(saved==null)throw new InvalidOperationException("Load and save a source first.");
            string output=BasicSwordSource.Regenerate(repository);var generated=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(repository,"unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json")));
            RangedActionSource.ValidateGenerated(repository,generated);message=output+"\nSaved ranged sources and generated catalog match the same compiler.";
        }
        void Run(Action action){try{action();}catch(Exception error)when(error is ArgumentException||error is IOException||error is InvalidOperationException||error is UnauthorizedAccessException){message=error.Message;}Refresh();}
        void Edit(Action action){Undo.IncrementCurrentGroup();Undo.RecordObject(edits,"Edit ranged numeric candidate");action();EditorUtility.SetDirty(edits);Refresh();}
        void Number(string name,string label,float value,Action<float> change,bool enabled=true)
        {
            var field=new FloatField(label){name=name,value=value};field.SetEnabled(enabled);field.RegisterValueChangedCallback(e=>Edit(()=>change(e.newValue)));fields.Add(field);
        }
        void Angles(string name,string label,float[] values,Action<float[]> change)
        {
            var field=new TextField(label){name=name,value=string.Join(", ",values??Array.Empty<float>())};
            field.RegisterValueChangedCallback(e=>Run(()=>{var parsed=e.newValue.Split(',').Select(text=>ParseNumber(text.Trim())).ToArray();Edit(()=>change(parsed));}));fields.Add(field);
        }
        void Rebuild()
        {
            if(fields==null)return;fields.Clear();if(accepted==null||!edits)return;
            selection.choices=accepted.Actions.Select(a=>a.Id).ToList();selection.SetValueWithoutNotify(edits.selectedId);
            var action=accepted.Read(edits.selectedId);var n=edits.numeric;bool missile=action.Policy==RangedActionPolicy.CasterMissile;
            fields.Add(Wrapped("Policy "+action.Policy+" · source "+(action.FormId=="launcher"&&!action.IsBasic?RangedActionSource.LauncherPath:RangedActionSource.RangedPath)+" · runtime ID "+action.RuntimeId));
            Number("activation","Release / windup seconds",n.activation,v=>{float shift=v-n.activation;n.activation=v;for(int i=0;i<n.releaseTimes.Length;i++)n.releaseTimes[i]+=shift;if(missile)n.end=n.activation+n.missile.distance/n.missile.speed+n.missile.recovery;});
            if(!missile)Number("end","Character motion end seconds",n.end,v=>n.end=v);
            Number("damage","Damage per release / tick",n.damages[0],v=>{for(int i=0;i<n.damages.Length;i++)n.damages[i]=v;});
            for(int i=1;i<n.releaseTimes.Length;i++){int index=i;Number("release-"+i,"Release "+(i+1)+" seconds",n.releaseTimes[i],v=>n.releaseTimes[index]=v);}
            if(action.Execution.Kind=="ranged")RangedFields(action,n.ranged);
            if(action.Execution.Kind=="bomb")BombFields(action,n.bomb);
            if(action.Execution.Kind=="missile")
            {
                var m=n.missile;Number("speed","Missile speed",m.speed,v=>{m.speed=v;n.end=n.activation+m.distance/m.speed+m.recovery;});Number("distance","Maximum actual flight distance",m.distance,v=>{m.distance=v;n.end=n.activation+m.distance/m.speed+m.recovery;});
                Number("blastRadius","Exit explosion radius",m.blastRadius,v=>m.blastRadius=v);Number("recovery","Post-exit recovery seconds",m.recovery,v=>{m.recovery=v;n.end=n.activation+m.distance/m.speed+m.recovery;});
                Number("manualDelay","New-D minimum actual-flight age",m.manualDelay,v=>m.manualDelay=v);Number("recoilHeight","Exit backward hop height",m.recoilHeight,v=>m.recoilHeight=v);Number("recoilDistance","Exit backward displacement",m.recoilDistance,v=>m.recoilDistance=v);
            }
        }
        void RangedFields(RangedActionDefinition action,RangedData r)
        {
            bool laser=action.Policy==RangedActionPolicy.LaserAllOnce,pellets=action.Policy==RangedActionPolicy.PiercingPellets;
            Number("distance","Range from actual muzzle center",r.distance,v=>r.distance=v);if(!laser)Number("speed","Projectile speed",r.speed,v=>r.speed=v);
            if(laser)Number("thickness","Laser thickness",r.thickness,v=>{r.thickness=v;r.radius=v/2;});else Number("radius","Collision radius",r.radius,v=>r.radius=v);
            Number("spawnHeight","Spawn height",r.spawnHeight,v=>r.spawnHeight=v);
            if(laser||pellets)Number("spawnGap","Body-front spawn gap",r.spawnGap,v=>r.spawnGap=v);else Number("spawnX","Explicit right spawn X (left mirrored)",r.spawnX,v=>r.spawnX=v);
            if(pellets){Angles("angles","Pellet angles (degrees)",r.angles,v=>r.angles=v);Number("recoilDistance","Air backward recoil distance",r.recoilDistance,v=>r.recoilDistance=v);Number("recoilHopHeight","Air recoil hop height",r.recoilHopHeight,v=>r.recoilHopHeight=v);fields.Add(Wrapped("Recoil duration is derived by the existing rise/fall gravity arc; its transport witness is read-only."));}
            if(action.Policy==RangedActionPolicy.FirstContactBlast)Number("blastRadius","First-contact explosion radius",r.blastRadius,v=>r.blastRadius=v);
            if(action.Policy==RangedActionPolicy.PiercingVisitReturn){Number("returnDelay","Arrival-relative automatic return seconds",r.returnDelay,v=>r.returnDelay=v);Number("behindGap","Behind-target body gap",r.behindGap,v=>r.behindGap=v);}
            if(action.Policy==RangedActionPolicy.PeriodicPiercing)Number("hitInterval","Per-target actual-hit interval",r.hitInterval,v=>r.hitInterval=v);
        }
        void BombFields(RangedActionDefinition action,BombData b)
        {
            bool inherited=action.Id=="bomb_basic_special", area=action.Id=="bomb_range_special", defaults=action.FormId=="bomb"&&action.Id!="bomb_basic_0";
            Number("angle","Throw angle",b.angle,v=>b.angle=v);Number("speed","Throw speed",b.speed,v=>b.speed=v,!inherited);Number("gravity","Projectile gravity",b.gravity,v=>b.gravity=v,!inherited&&!area);Number("radius","Collision radius",b.radius,v=>b.radius=v,!inherited);
            Number("blastRadius","Explosion radius",b.blastRadius,v=>b.blastRadius=v);Number("lifetime","Unlanded / flight timeout",b.lifetime,v=>b.lifetime=v,!defaults||action.Policy==RangedActionPolicy.LandingFuseBomb);
            Number("spawnHeight","Spawn height",b.spawnHeight,v=>b.spawnHeight=v,!defaults);Number("spawnGap","Body-front spawn gap",b.spawnGap,v=>b.spawnGap=v,!defaults);
            if(defaults)fields.Add(Wrapped("Disabled inherited ballistic/default fields are authored once under bomb_basic_0 and propagate to dependent actions."));
            if(action.Policy==RangedActionPolicy.LandingFuseBomb)Number("fuse","First-landing-relative fuse seconds",b.fuseSeconds,v=>b.fuseSeconds=v);
            if(action.Policy==RangedActionPolicy.ContactBombWithFragments)
            {
                Angles("fragmentAngles","Fragment world angles",b.fragmentAngles,v=>b.fragmentAngles=v);
                Number("fragmentDamage","Fragment damage",b.fragmentDamage,v=>b.fragmentDamage=v);Number("fragmentSpeed","Fragment speed",b.fragmentSpeed,v=>b.fragmentSpeed=v);Number("fragmentGravity","Fragment gravity",b.fragmentGravity,v=>b.fragmentGravity=v);
                Number("fragmentRadius","Fragment collision radius",b.fragmentRadius,v=>b.fragmentRadius=v);Number("fragmentBlastRadius","Fragment explosion radius",b.fragmentBlastRadius,v=>b.fragmentBlastRadius=v);Number("fragmentLifetime","Fragment lifetime",b.fragmentLifetime,v=>b.fragmentLifetime=v);Number("fragmentSpawnOffset","Fragment spawn offset",b.fragmentSpawnOffset,v=>b.fragmentSpawnOffset=v);
            }
        }
        void Refresh()
        {
            if(version==null)return;string error=null;RangedActionCatalog candidate=null;
            try{if(accepted!=null&&edits)candidate=accepted.Edit(edits.selectedId,edits.numeric);}catch(ArgumentException exception){error=exception.Message;}
            version.text=candidate==null?"Invalid complete candidate: "+error:"15 / 15 actions valid · selected "+candidate.Read(edits.selectedId).ContentHash+"\nPolicy/resource "+candidate.Read(edits.selectedId).SourceVersion;
            geometry?.SetCandidate(candidate?.Read(edits.selectedId));
            status.text=message??"";bool valid=candidate!=null;
            foreach(string button in new[]{"applyNext","applyReset"})rootVisualElement.Q<Button>(button)?.SetEnabled(valid&&EditorApplication.isPlaying&&runtime);
            rootVisualElement.Q<Button>("save")?.SetEnabled(valid);rootVisualElement.Q<Button>("regenerate")?.SetEnabled(saved!=null);
        }
        static float ParseNumber(string text)
        {
            if(!float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float value)||float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Enter finite comma-separated angles.");
            return value;
        }
        static Label Wrapped(string text){var label=new Label(text);label.style.whiteSpace=WhiteSpace.Normal;return label;}
        static Button Button(string name,string text,Action click)=>new Button(click){name=name,text=text};
    }
}
