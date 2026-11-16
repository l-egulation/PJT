using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        ActionSpecialFrameCatalog specialFrames;
        Dictionary<string, ActionTrackDefinition> specialNumericFinite;
        Dictionary<string, AuthoredSkillDefinition> specialNumericAuthored;
        public ActionSpecialFrameCatalog SpecialFrames => specialFrames;
        public ActionSpecialFrameDefinition GetSpecialFrame(string id) => specialFrames?.Find(id);
        public ActionTrackDefinition GetNumericActionTracks(string id)
            => specialNumericFinite != null && id != null && specialNumericFinite.TryGetValue(id, out var value) ? value : GetActionTracks(id);
        public AuthoredSkillDefinition GetNumericAuthoredSkill(string id)
            => specialNumericAuthored != null && id != null && specialNumericAuthored.TryGetValue(id, out var value) ? value : GetAuthoredSkill(id);
        bool InitializeSpecialFrames(SpecialFrameCatalogDocument source, out string error)
        {
            try
            {
                if (MotionRegistry == null) { error = null; return true; } // Explicit isolated media fixture, not the product generated-content path.
                if (source == null) throw new ArgumentException("The complete generated special frame source is required.");
                var finite = new Dictionary<string, ActionTrackDefinition>(actionTracks, StringComparer.Ordinal);
                var authored = new Dictionary<string, AuthoredSkillDefinition>(authoredSkills, StringComparer.Ordinal);
                var next = CompileSpecialFrames(source, MotionRegistry, finite, authored);
                PublishSpecialFrames(next, finite, authored); error = null; return true;
            }
            catch (ArgumentException exception) { error = "Invalid generated special frame source: " + exception.Message; return false; }
        }
        ActionSpecialFrameCatalog CompileSpecialFrames(SpecialFrameCatalogDocument source, MotionSourceRegistry registry,
            IReadOnlyDictionary<string, ActionTrackDefinition> finite, IReadOnlyDictionary<string, AuthoredSkillDefinition> authored)
        {
            if (!TryGetBasicSwordMotion(out var first, out var error)) throw new ArgumentException(error);
            return new ActionSpecialFrameCatalog(source, catalog, registry, first, finite.Values, authored.Values);
        }
        void PublishSpecialFrames(ActionSpecialFrameCatalog next, Dictionary<string, ActionTrackDefinition> finite, Dictionary<string, AuthoredSkillDefinition> authored)
        {
            // Prepare all replacement dictionaries and source values before the
            // first owner field changes. Running casts retain their own objects.
            var effectiveFinite = next.Actions.Where(value => value.Family == SpecialFrameFamily.FiniteTracks).ToDictionary(value => value.Id, value => value.Finite, StringComparer.Ordinal);
            var effectiveAuthored = next.Actions.Where(value => value.Family == SpecialFrameFamily.AuthoredSkill).ToDictionary(value => value.Id, value => value.Authored, StringComparer.Ordinal);
            var document = next.ToDocument();
            if (!TryGetBasicSwordMotion(out var first, out var error)) throw new ArgumentException(error);
            string bindingHash = next.BindingHash(MotionRegistry, first, MeleeFrameState);
            specialNumericFinite = finite; specialNumericAuthored = authored; actionTracks = effectiveFinite; authoredSkills = effectiveAuthored;
            characterAnimations.specialFrames = document; specialFrames = next;
            motionBindingHash = bindingHash;
        }
        public void ValidateSpecialFrames(ActionSpecialFrameCatalog candidate)
        {
            if (candidate == null || specialFrames == null) throw new ArgumentException("An initialized complete special frame catalog is required.");
            ValidateMotionRegistry(MotionRegistry);
            var current = CompileSpecialFrames(candidate.ToDocument(), MotionRegistry, specialNumericFinite, specialNumericAuthored);
            if (current.ContentHash != candidate.ContentHash) throw new ArgumentException("Special frame candidate numeric/policy/media sources changed. Reload before applying.");
        }
        public bool TryApplySpecialFrames(IReadOnlyList<ActionSpecialFrameDefinition> candidates, bool resetScenario, out string error)
        {
            error = null;
            if (!Player || specialFrames == null) { error = "Special frame sources are not initialized."; return false; }
            if (candidates == null || candidates.Count == 0 || candidates.Count > specialFrames.Actions.Count) { error = "A nonempty unique subset of complete special frame candidates is required."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var document = specialFrames.ToSourceDocument(); var selected = new HashSet<MotionActionKey>();
                foreach (var candidate in candidates)
                {
                    if (candidate == null || !selected.Add(candidate.Key) || specialFrames.Find(candidate.Key) == null) throw new ArgumentException("Special frame candidates require distinct supported original typed IDs.");
                    int index = Array.FindIndex(document.actions, value => value.actionId == candidate.Id);
                    document.actions[index] = candidate.ToDocument();
                }
                ValidateMotionRegistry(MotionRegistry);
                var next = CompileSpecialFrames(document, MotionRegistry, specialNumericFinite, specialNumericAuthored);
                foreach (var candidate in candidates)
                    if (next.Find(candidate.Key).ContentHash != candidate.ContentHash || next.Find(candidate.Key).SourceHash != candidate.SourceHash)
                        throw new ArgumentException("A special frame candidate no longer matches current original numeric/media/policy values.");
                if (resetScenario) simulation.ValidateResetAdmission();
                PublishSpecialFrames(next, specialNumericFinite, specialNumericAuthored);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest(); return true;
        }
        bool TryApplySpecialFiniteNumeric(IReadOnlyList<ActionTrackDefinition> candidates, bool resetScenario, out string error)
        {
            error = null;
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var finite = new Dictionary<string, ActionTrackDefinition>(specialNumericFinite, StringComparer.Ordinal);
                var selected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var candidate in candidates)
                {
                    if (candidate == null || !selected.Add(candidate.Id) || !finite.TryGetValue(candidate.Id, out var previous) || previous.SourceVersion != candidate.SourceVersion || candidate.SourceTimeMode != ActionTrackSourceTimeMode.CombatTimeFixed)
                        throw new ArgumentException("The original complete unique finite numeric subset is required.");
                    if (specialFrames.Find(candidate.Id).UsesFrameTime) SpecialFrameTimingGuards.RequireFiniteNumericEdit(previous, candidate);
                    finite[candidate.Id] = candidate;
                }
                var next = CompileSpecialFrames(specialFrames.ToSourceDocument(), MotionRegistry, finite, specialNumericAuthored);
                ValidateMotionRegistry(MotionRegistry); if (resetScenario) simulation.ValidateResetAdmission(); PublishSpecialFrames(next, finite, specialNumericAuthored);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest(); return true;
        }
        bool TryApplySpecialAuthoredNumeric(IReadOnlyList<AuthoredSkillDefinition> candidates, bool resetScenario, out string error)
        {
            error = null;
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var authored = new Dictionary<string, AuthoredSkillDefinition>(specialNumericAuthored, StringComparer.Ordinal);
                var selected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var candidate in candidates)
                {
                    if (candidate == null || !selected.Add(candidate.Id) || !authored.TryGetValue(candidate.Id, out var previous) || previous.SourceVersion != candidate.SourceVersion)
                        throw new ArgumentException("The original complete unique authored numeric subset is required.");
                    if (specialFrames.Find(candidate.Id).UsesFrameTime) SpecialFrameTimingGuards.RequireAuthoredNumericEdit(previous, candidate);
                    authored[candidate.Id] = candidate;
                }
                var next = CompileSpecialFrames(specialFrames.ToSourceDocument(), MotionRegistry, specialNumericFinite, authored);
                ValidateMotionRegistry(MotionRegistry); if (resetScenario) simulation.ValidateResetAdmission(); PublishSpecialFrames(next, specialNumericFinite, authored);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest(); return true;
        }
    }
}
