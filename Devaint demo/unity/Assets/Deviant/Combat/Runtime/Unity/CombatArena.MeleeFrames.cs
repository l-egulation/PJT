using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        MeleeFrameCatalog meleeFrames;
        MeleeFrameCatalogDocument meleeFrameSource;
        public IReadOnlyList<MeleeFrameDefinition> MeleeFrames => meleeFrames?.Actions ?? Array.Empty<MeleeFrameDefinition>();
        public MeleeFrameCatalog MeleeFrameState => meleeFrames;
        public string MeleeFrameVersion => meleeFrames?.ContentHash;
        public MeleeFrameDefinition GetMeleeFrame(string actionId, bool basic)
            => meleeFrames?.Find(new MotionActionKey(actionId, basic ? MotionActionKind.Basic : MotionActionKind.Special));
        bool InitializeMeleeFrames(out string error)
        {
            try
            {
                if (motionRegistry == null) { error = null; return true; }
                if (characterAnimations.meleeFrames == null) throw new ArgumentException("The generated 20-action melee frame source is required with the complete registry.");
                meleeFrameSource = characterAnimations.meleeFrames;
                meleeFrames = CompileMeleeFrames(meleeFrameSource, motionRegistry, appliedFirstMotion, frameSwordTiming);
                RefreshMotionBindingHash(); error = null; return true;
            }
            catch (ArgumentException exception) { error = "Invalid generated melee frame source: " + exception.Message; return false; }
        }
        MeleeFrameCatalog CompileMeleeFrames(MeleeFrameCatalogDocument source, MotionSourceRegistry registry, FixedAttackMotion first, MeleeFrameDefinition firstFrame,
            BasicMeleeCatalog basics = null, BasicMeleeDefinition firstNumeric = null, IEnumerable<DirectMeleeSpecialDefinition> direct = null)
        {
            var values = (basics ?? basicMeleeCatalog).Actions.Select(action => action.Id == FixedAttackMotion.FirstSwordAction ? firstNumeric ?? basicSwordFirst : action);
            return new MeleeFrameCatalog(source, catalog, registry, first, firstFrame, values, direct ?? directMeleeSpecials.Values);
        }
        internal void RefreshMotionBindingHash() => motionBindingHash = specialFrames != null
            ? specialFrames.BindingHash(motionRegistry, appliedFirstMotion, meleeFrames)
            : meleeFrames == null ? motionRegistry?.BindingHash(appliedFirstMotion) : meleeFrames.BindingHash(motionRegistry, appliedFirstMotion);
        public bool TryApplyMeleeFrames(IReadOnlyList<MeleeFrameDefinition> candidates, bool resetScenario, out string error)
        {
            error = null;
            if (!Player || meleeFrames == null || candidates == null || candidates.Count == 0) { error = "An initialized complete media registry and nonempty melee frame candidates are required."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var byId = meleeFrames.Actions.ToDictionary(action => action.Id, StringComparer.Ordinal);
                var selected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var candidate in candidates)
                {
                    if (candidate == null || !byId.TryGetValue(candidate.Id, out var current) || !selected.Add(candidate.Id) || candidate.NumericAction.ContentHash != current.NumericAction.ContentHash || candidate.PolicyVersion != current.PolicyVersion || candidate.Media.BindingVersion != current.Media.BindingVersion)
                        throw new ArgumentException("A melee frame candidate is stale, duplicated or belongs to another current numeric/media policy.");
                    if (candidate.Id == FixedAttackMotion.FirstSwordAction) throw new ArgumentException("The delegated first-sword source must be applied through its same canonical source adapter.");
                    candidate.CompileForStart(Player.Weapon.Speed); candidate.CompileMotion(Player.Weapon.Speed); byId[candidate.Id] = candidate;
                }
                var source = new MeleeFrameCatalogDocument { schemaVersion = MeleeFrameCatalogDocument.SchemaVersion,
                    actions = byId.Values.Where(value => value.Id != FixedAttackMotion.FirstSwordAction).OrderBy(value => value.Id, StringComparer.Ordinal).Select(value => value.ToDocument()).ToArray() };
                var next = CompileMeleeFrames(source, motionRegistry, appliedFirstMotion, frameSwordTiming);
                if (resetScenario) simulation.ValidateResetAdmission();
                meleeFrames = next; meleeFrameSource = next.ToSourceDocument(); RefreshMotionBindingHash();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest(); return true;
        }
        internal bool TryRecompileMeleeFrameNumerics(BasicMeleeCatalog basics, BasicMeleeDefinition first, IEnumerable<DirectMeleeSpecialDefinition> direct, out MeleeFrameCatalog next, out string error)
        {
            next = null; error = null; if (meleeFrames == null) return true;
            try { next = CompileMeleeFrames(meleeFrameSource, motionRegistry, appliedFirstMotion, frameSwordTiming, basics, first, direct); return true; }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        internal void PublishMeleeFrameNumerics(MeleeFrameCatalog next)
        {
            if (next == null) return; meleeFrames = next; RefreshMotionBindingHash();
        }
    }
}
