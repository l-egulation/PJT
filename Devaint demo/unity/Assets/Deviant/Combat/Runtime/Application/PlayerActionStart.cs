using System;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Live reads retain the existing admission/callback boundaries. Native cast
    // creation alone assigns currentCast and emits its Started receipt.
    public interface IPlayerActionStartHost
    {
        int Axis { get; }
        void SyncFacing(int direction);
        void BeginBasicCombo(int index);
        FormData Form { get; }
        int SpecialIndex { get; }
        float WeaponSpeed { get; }
        BasicMeleeDefinition BasicMelee(string formId, int index);
        RangedActionDefinition RangedBasic(string formId, int index);
        RangedActionDefinition RangedSpecial(string id);
        SkillData Special { get; }
        ActionTrackDefinition ActionTracks(string id);
        DirectMeleeSpecialDefinition DirectSpecial(string id);
        ChargedMeleeDefinition ChargedMelee(string id);
        AuthoredSkillDefinition AuthoredSpecial(string id);
        bool ShurikenReturnPending();
        bool ProbeGround();
        void RecordUnavailableSpecial();
        void ClearCombo();
        void ClearJumpBuffer();
        void ClearBufferedJump();
        Guid CreateBasic(SkillData data, CombatCommandKey command, float startAt, int index, BasicMeleeDefinition definition, MeleeActionDefinition timeline, RangedActionDefinition ranged);
        Guid CreateSpecial(SkillData source, CombatCommandKey command, float startAt, string version, MeleeActionDefinition timeline,
            DirectMeleeSpecialDefinition direct, ChargedMeleeDefinition charged, ActionTrackDefinition tracks, AuthoredSkillDefinition authored, RangedActionDefinition ranged);
    }
}
