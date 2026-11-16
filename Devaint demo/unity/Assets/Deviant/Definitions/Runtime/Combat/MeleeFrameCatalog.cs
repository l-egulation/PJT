using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // Complete bounded family inventory. Registry owns media/default weights;
    // action-local sources own only explicit FrameTime overrides.
    public sealed class MeleeFrameCatalog
    {
        readonly Dictionary<MotionActionKey, MeleeFrameDefinition> actions;
        public IReadOnlyList<MeleeFrameDefinition> Actions { get; }
        public string ContentHash { get; }
        public const string SourcePath = "data/combat/presentation/melee_frame_bindings.json";
        public MeleeFrameCatalog(MeleeFrameCatalogDocument document, Catalog combat, MotionSourceRegistry registry, FixedAttackMotion firstMotion, MeleeFrameDefinition firstFrame,
            IEnumerable<BasicMeleeDefinition> basics, IEnumerable<DirectMeleeSpecialDefinition> direct)
        {
            if (document == null || document.schemaVersion != MeleeFrameCatalogDocument.SchemaVersion || document.actions == null || document.actions.Any(source => source == null))
                throw new ArgumentException("A complete melee frame source schema1 is required.");
            if (registry == null || firstMotion == null || combat?.forms == null) throw new ArgumentException("The current explicit combat and shared media registry are required.");
            var byId = document.actions.ToDictionary(source => source.actionId, StringComparer.Ordinal);
            var basicValues = basics?.ToArray(); var directValues = direct?.ToArray();
            if (basicValues == null || basicValues.Length != 17 || basicValues.Any(value => value == null) || basicValues.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 17 || directValues == null || directValues.Length != DirectMeleeSpecialDefinition.SupportedIds.Count || directValues.Any(value => value == null) || !directValues.Select(value => value.Id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(DirectMeleeSpecialDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("The complete 17 basic and three direct numeric definitions are required exactly once.");
            if (byId.Count != 19 || byId.ContainsKey(FixedAttackMotion.FirstSwordAction)) throw new ArgumentException("The first sword source is delegated, never duplicated in the other nineteen action records.");
            actions = new Dictionary<MotionActionKey, MeleeFrameDefinition>();
            foreach (var basic in basicValues)
            {
                int separator = basic.Id.LastIndexOf("_basic_", StringComparison.Ordinal); if (separator <= 0) throw new ArgumentException("An existing basic identity is required.");
                string formId = basic.Id.Substring(0, separator); var form = combat.forms.Single(value => value.id == formId);
                var key = new MotionActionKey(basic.Id, MotionActionKind.Basic); var media = ResolveMedia(key, registry, firstMotion, true);
                if (basic.Id == FixedAttackMotion.FirstSwordAction && firstFrame != null)
                {
                    if (firstFrame.Mode != MeleeTimingMode.FrameTime || firstFrame.Family != MeleeFrameFamily.Basic || firstFrame.FormId != formId ||
                        firstFrame.NumericAction.ContentHash != basic.ContentHash || firstFrame.SourceMotion.ContentHash != firstMotion.ContentHash ||
                        firstFrame.PolicyVersion != MeleeFrameCompiler.BasicPolicyVersion(basic.Id, formId, form.inputSpeed) || firstFrame.Media.BindingVersion != media.BindingVersion)
                        throw new ArgumentException("The delegated first frame candidate differs from the complete numeric, movement policy or shared media context.");
                    actions.Add(key, firstFrame); continue;
                }
                MeleeFrameSourceDocument source;
                if (basic.Id == FixedAttackMotion.FirstSwordAction)
                {
                    source = MeleeFrameCompiler.FixedBasic(basic, formId, form.inputSpeed, media);
                }
                else if (!byId.TryGetValue(basic.Id, out source)) throw new ArgumentException("Missing melee frame action source: " + basic.Id);
                actions.Add(key, MeleeFrameCompiler.CompileBasic(basic, formId, form.inputSpeed, media, source));
            }
            foreach (var special in directValues)
            {
                if (!byId.TryGetValue(special.Id, out var source)) throw new ArgumentException("Missing direct frame action source: " + special.Id);
                var key = new MotionActionKey(special.Id, MotionActionKind.Special);
                actions.Add(key, MeleeFrameCompiler.CompileDirect(special, ResolveMedia(key, registry, firstMotion, false), source));
            }
            if (byId.Keys.Any(id => !actions.Keys.Any(key => key.ActionId == id))) throw new ArgumentException("Unsupported action cannot acquire a melee FrameTime policy.");
            Actions = Array.AsReadOnly(actions.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
            ContentHash = MeleeFrameValues.Hash(writer => { writer.Write("melee-frame-catalog-v1"); foreach (var action in Actions) { writer.Write(action.Id); writer.Write(action.ContentHash); } });
        }
        public string BindingHash(MotionSourceRegistry registry, FixedAttackMotion firstMotion)
        {
            if (registry == null || firstMotion == null) throw new ArgumentNullException(nameof(registry));
            return MeleeFrameValues.Hash(writer => { writer.Write("melee-frame-binding-v1"); writer.Write(registry.BindingHash(firstMotion)); writer.Write(ContentHash); });
        }
        public MeleeFrameDefinition Find(MotionActionKey key) => actions.TryGetValue(key, out var result) ? result : null;
        public MeleeFrameCatalogDocument ToDocument() => new MeleeFrameCatalogDocument { schemaVersion = MeleeFrameCatalogDocument.SchemaVersion,
            actions = Actions.Where(value => value.Id != FixedAttackMotion.FirstSwordAction).Select(value => value.ToDocument()).ToArray() };
        public MeleeFrameCatalogDocument ToSourceDocument()
        {
            var document = ToDocument();
            foreach (var source in document.actions)
                if (source.mode == MeleeTimingMode.CombatTimeFixed) { source.policyVersion = null; source.mediaBindingVersion = null; }
            return document;
        }
        public static MeleeFrameMedia ResolveMedia(MotionActionKey key, MotionSourceRegistry registry, FixedAttackMotion firstMotion, bool basicCue)
        {
            var binding = registry.Find(key) ?? throw new ArgumentException("Missing typed melee motion binding: " + key);
            var motion = registry.Motion(binding.MotionId) ?? throw new ArgumentException("Missing supplied motion: " + binding.MotionId);
            bool cue = basicCue && !binding.IsFallback && motion.FxStart >= 0;
            AttackMotionTiming fixedTiming; IReadOnlyList<string> ids;
            if (motion.IsDelegated) { fixedTiming = firstMotion.Compile(0, 1); ids = firstMotion.StepIds; cue = basicCue && !binding.IsFallback && firstMotion.FxStart >= 0; }
            else { fixedTiming = motion.Compile(0, 1, basicCue); ids = motion.StepIds; }
            string identity = key + "|" + binding.RequestedForm + "|" + binding.RequestedClip + "|" + binding.MotionId + "|" + binding.IsFallback;
            return new MeleeFrameMedia(binding.MotionId, ids, fixedTiming, cue, identity);
        }
    }
}
