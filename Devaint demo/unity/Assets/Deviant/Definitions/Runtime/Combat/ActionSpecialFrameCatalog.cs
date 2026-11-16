using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;

namespace Deviant.Definitions
{
    public sealed class ActionSpecialFrameCatalog
    {
        public const string SourcePath = "data/combat/presentation/special_frame_bindings.json";
        readonly Dictionary<MotionActionKey, ActionSpecialFrameDefinition> actions;
        public IReadOnlyList<ActionSpecialFrameDefinition> Actions { get; }
        public string ContentHash { get; }
        public ActionSpecialFrameCatalog(SpecialFrameCatalogDocument document, Catalog combat, MotionSourceRegistry registry, FixedAttackMotion firstMotion,
            IEnumerable<ActionTrackDefinition> finite, IEnumerable<AuthoredSkillDefinition> authored)
        {
            if (document == null || document.schemaVersion != SpecialFrameCatalogDocument.SchemaVersion || document.actions == null || document.actions.Any(value => value == null))
                throw new ArgumentException("The complete bounded special frame schema1 requires all four finite and nineteen authored records.");
            if (combat?.forms == null || registry == null || firstMotion == null) throw new ArgumentException("Explicit current numeric inventory and structural media sources are required.");
            var numericFinite = finite?.ToArray(); var numericAuthored = authored?.ToArray();
            RequireIds(numericFinite?.Select(value => value?.Id), ActionTrackDefinition.SupportedIds, "finite");
            RequireIds(numericAuthored?.Select(value => value?.Id), AuthoredSkillDefinition.SupportedIds, "authored");
            var sources = new Dictionary<MotionActionKey, SpecialFrameSourceDocument>();
            foreach (var source in document.actions)
            {
                var key = new MotionActionKey(source.actionId, source.kind, source.variant);
                if (source.kind != MotionActionKind.Special || source.variant != -1 || sources.ContainsKey(key)) throw new ArgumentException("Special frame sources require distinct complete typed existing special keys.");
                sources.Add(key, source);
            }
            actions = new Dictionary<MotionActionKey, ActionSpecialFrameDefinition>();
            foreach (var numeric in numericFinite)
            {
                var key = new MotionActionKey(numeric.Id, MotionActionKind.Special); string form = FormFor(combat, numeric.Id);
                if (!sources.TryGetValue(key, out var source)) throw new ArgumentException("Missing finite special frame source: " + numeric.Id);
                var media = MeleeFrameCatalog.ResolveMedia(key, registry, firstMotion, false);
                actions.Add(key, ActionSpecialFrameCompiler.CompileFinite(numeric, form, media, source));
            }
            foreach (var numeric in numericAuthored)
            {
                var key = new MotionActionKey(numeric.Id, MotionActionKind.Special);
                if (FormFor(combat, numeric.Id) != numeric.FormId || !sources.TryGetValue(key, out var source)) throw new ArgumentException("Missing or foreign authored frame source: " + numeric.Id);
                var media = MeleeFrameCatalog.ResolveMedia(key, registry, firstMotion, false);
                actions.Add(key, ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
            }
            if (sources.Keys.Any(key => !actions.ContainsKey(key))) throw new ArgumentException("Unsupported policies cannot acquire a special FrameTime source.");
            Actions = Array.AsReadOnly(actions.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
            ContentHash = SpecialFrameValues.Hash(writer => {
                writer.Write("special-frame-catalog-v1"); writer.Write(Actions.Count);
                foreach (var action in Actions) { writer.Write(action.Id); writer.Write(action.SourceHash); writer.Write(action.ContentHash); }
            });
        }
        public ActionSpecialFrameDefinition Find(MotionActionKey key) => actions.TryGetValue(key, out var result) ? result : null;
        public ActionSpecialFrameDefinition Find(string id) => Find(new MotionActionKey(id, MotionActionKind.Special));
        public SpecialFrameCatalogDocument ToDocument() => new SpecialFrameCatalogDocument { schemaVersion = SpecialFrameCatalogDocument.SchemaVersion,
            actions = Actions.Select(value => value.ToDocument()).ToArray() };
        public SpecialFrameCatalogDocument ToSourceDocument()
        {
            var source = ToDocument();
            foreach (var action in source.actions) if (action.mode == MeleeTimingMode.CombatTimeFixed)
            { action.policyVersion = null; action.mediaBindingVersion = null; action.timeline = null; }
            return source;
        }
        public string BindingHash(MotionSourceRegistry registry, FixedAttackMotion firstMotion, MeleeFrameCatalog melee)
        {
            if (registry == null || firstMotion == null || melee == null) throw new ArgumentException("Complete captured registry/first/melee sources are required for whole special binding identity.");
            return SpecialFrameValues.Hash(writer => { writer.Write("special-frame-binding-v1"); writer.Write(melee.BindingHash(registry, firstMotion)); writer.Write(ContentHash); });
        }
        public static SpecialFrameCatalogDocument FixedSources(Catalog combat, MotionSourceRegistry registry, FixedAttackMotion firstMotion,
            IEnumerable<ActionTrackDefinition> finite, IEnumerable<AuthoredSkillDefinition> authored)
        {
            var records = new List<SpecialFrameSourceDocument>();
            foreach (var numeric in finite)
            {
                var media = MeleeFrameCatalog.ResolveMedia(new MotionActionKey(numeric.Id, MotionActionKind.Special), registry, firstMotion, false);
                records.Add(ActionSpecialFrameCompiler.FixedFinite(numeric, FormFor(combat, numeric.Id), media));
            }
            foreach (var numeric in authored)
            {
                var media = MeleeFrameCatalog.ResolveMedia(new MotionActionKey(numeric.Id, MotionActionKind.Special), registry, firstMotion, false);
                records.Add(ActionSpecialFrameCompiler.FixedAuthored(numeric, media));
            }
            return new SpecialFrameCatalogDocument { schemaVersion = SpecialFrameCatalogDocument.SchemaVersion, actions = records.OrderBy(value => value.actionId, StringComparer.Ordinal).ToArray() };
        }
        static string FormFor(Catalog combat, string id)
        {
            var forms = combat.forms.Where(form => form?.specials != null && form.specials.Count(skill => skill?.id == id) == 1).ToArray();
            if (forms.Length != 1 || combat.forms.Sum(form => form?.specials?.Count(skill => skill?.id == id) ?? 0) != 1)
                throw new ArgumentException("Each special numeric identity must occur exactly once in its existing form.");
            return forms[0].id;
        }
        static void RequireIds(IEnumerable<string> actual, IReadOnlyList<string> expected, string family)
        {
            var ids = actual?.ToArray();
            if (ids == null || ids.Any(string.IsNullOrEmpty) || ids.Length != expected.Count || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                !ids.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("The complete unique original " + family + " numeric definition inventory is required.");
        }
    }
}
