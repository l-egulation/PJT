using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        // Full definition restoration is one atomic boundary, not a sequence of
        // temporarily mismatched numeric, media and active mode publications.
        public bool TryApplyCompleteFrameState(IReadOnlyList<BasicMeleeDefinition> basics, IReadOnlyList<DirectMeleeSpecialDefinition> direct,
            FixedAttackMotion firstMotion, MotionSourceRegistry registry, MeleeFrameDefinition firstFrame, MeleeFrameCatalogDocument meleeSource,
            IReadOnlyList<ActionTrackDefinition> finite, IReadOnlyList<AuthoredSkillDefinition> authored, SpecialFrameCatalogDocument specialSource,
            bool resetScenario, out string error)
        {
            error = null;
            if (!Player || basicMeleeCatalog == null || directMeleeSpecials == null || specialNumericFinite == null || specialNumericAuthored == null || registry == null || firstMotion == null)
            { error = "Initialized complete numeric/media/frame owners and non-null full source state are required."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                if (basics == null || basics.Count != 17 || basics.Any(value => value == null) || basics.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 17 ||
                    direct == null || direct.Count != DirectMeleeSpecialDefinition.SupportedIds.Count || finite == null || authored == null)
                    throw new ArgumentException("All original numeric definitions and their complete active mode sources are required.");
                var first = basics.Single(value => value.Id == FixedAttackMotion.FirstSwordAction);
                if (!basicMeleeCatalog.TryReplace(basics, out var nextBasics, out error)) return false;
                var nextDirect = new Dictionary<string, DirectMeleeSpecialDefinition>(StringComparer.Ordinal);
                foreach (var value in direct)
                {
                    if (value == null || !directMeleeSpecials.TryGetValue(value.Id, out var current) || current.SourceVersion != value.SourceVersion || nextDirect.ContainsKey(value.Id))
                        throw new ArgumentException("Every original direct policy must match before complete frame restoration.");
                    nextDirect.Add(value.Id, value);
                }
                var nextFinite = new Dictionary<string, ActionTrackDefinition>(StringComparer.Ordinal);
                foreach (var value in finite)
                {
                    if (value == null || value.SourceTimeMode != ActionTrackSourceTimeMode.CombatTimeFixed || !specialNumericFinite.TryGetValue(value.Id, out var current) || current.SourceVersion != value.SourceVersion || nextFinite.ContainsKey(value.Id))
                        throw new ArgumentException("Every original finite numeric policy is required exactly once before complete frame restoration.");
                    nextFinite.Add(value.Id, value);
                }
                var nextAuthored = new Dictionary<string, AuthoredSkillDefinition>(StringComparer.Ordinal);
                foreach (var value in authored)
                {
                    if (value == null || !specialNumericAuthored.TryGetValue(value.Id, out var current) || current.SourceVersion != value.SourceVersion || nextAuthored.ContainsKey(value.Id))
                        throw new ArgumentException("Every original authored numeric policy is required exactly once before complete frame restoration.");
                    nextAuthored.Add(value.Id, value);
                }
                ValidateMotionRegistry(registry);
                var sets = PrepareFirstMotion(MotionRegistryMedia.Project(characterAnimations, registry), firstMotion, registry);
                if (firstFrame != null && (firstFrame.NumericAction.ContentHash != first.ContentHash || firstFrame.SourceMotion.ContentHash != firstMotion.ContentHash))
                    throw new ArgumentException("The delegated first frame source must match its original numeric/media context.");
                var melee = new MeleeFrameCatalog(meleeSource, catalog, registry, firstMotion, firstFrame, basics, direct);
                foreach (var frame in melee.Actions) { frame.CompileForStart(Player.Weapon.Speed); frame.CompileMotion(Player.Weapon.Speed); }
                var special = new ActionSpecialFrameCatalog(specialSource, catalog, registry, firstMotion, finite, authored);
                var effectiveFinite = special.Actions.Where(value => value.Family == SpecialFrameFamily.FiniteTracks).ToDictionary(value => value.Id, value => value.Finite, StringComparer.Ordinal);
                var effectiveAuthored = special.Actions.Where(value => value.Family == SpecialFrameFamily.AuthoredSkill).ToDictionary(value => value.Id, value => value.Authored, StringComparer.Ordinal);
                string bindingHash = special.BindingHash(registry, firstMotion, melee);
                var mediaDocument = registry.ToDocument(); var meleeDocument = melee.ToSourceDocument(); var specialDocument = special.ToDocument();
                if (resetScenario) simulation.ValidateResetAdmission();
                basicMeleeCatalog = nextBasics; basicSwordFirst = first; directMeleeSpecials = nextDirect;
                motionRegistry = registry; appliedFirstMotion = firstMotion; frameSwordTiming = firstFrame;
                meleeFrames = melee; meleeFrameSource = meleeDocument; specialFrames = special;
                specialNumericFinite = nextFinite; specialNumericAuthored = nextAuthored; actionTracks = effectiveFinite; authoredSkills = effectiveAuthored;
                characterAnimations.sets = sets; characterAnimations.motionRegistry = mediaDocument; characterAnimations.meleeFrames = meleeDocument; characterAnimations.specialFrames = specialDocument;
                motionBindingHash = bindingHash;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is KeyNotFoundException)
            { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest(); return true;
        }
    }
}
