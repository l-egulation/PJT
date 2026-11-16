using System;
using System.Collections.Generic;

namespace Deviant.World.Contracts
{
    public readonly struct LayoutMotionInput
    {
        public int Axis { get; }
        public bool Jump { get; }
        public bool Dash { get; }
        public bool Drop { get; }
        public LayoutMotionInput(int axis, bool jump = false, bool dash = false, bool drop = false)
        {
            if (axis < -1 || axis > 1 || (jump ? 1 : 0) + (dash ? 1 : 0) + (drop ? 1 : 0) > 1)
                throw new ArgumentException("A witness tick uses a valid axis and at most one movement edge.");
            Axis = axis; Jump = jump; Dash = dash; Drop = drop;
        }
    }
    public enum LayoutWitnessKind { Settle, Walk, Jump, Drop, Dash }
    public sealed class LayoutWitness
    {
        public string Id { get; }
        public LayoutPoint Start { get; }
        public int Facing { get; }
        public LayoutWitnessKind Kind { get; }
        public LayoutPoint Destination { get; }
        // Walk/Jump/Drop settle here. Dash must reach this region with its
        // actual body, then finish its uninterruptible motion and settle clear;
        // it is not a command to truncate the runtime dash at this coordinate.
        public LayoutRect FinishRegion { get; }
        public bool RequireGrounded { get; }
        public LayoutWitness(string id, LayoutPoint start, int facing, LayoutWitnessKind kind, LayoutPoint destination, LayoutRect finishRegion, bool requireGrounded = true)
        {
            if (facing != -1 && facing != 1 || !Enum.IsDefined(typeof(LayoutWitnessKind), kind))
                throw new ArgumentException("An explicit supported movement kind and signed facing are required.");
            Id = LayoutValues.Id(id); Start = start; Facing = facing; Kind = kind; Destination = destination;
            FinishRegion = finishRegion; RequireGrounded = requireGrounded;
        }
    }
    // Captured common motor rules, not an independent jump/dash simulation.
    public sealed class LayoutMotionProfile
    {
        public float Step { get; }
        public float FirstJump { get; }
        public float SecondJump { get; }
        public float RiseGravity { get; }
        public float FallGravity { get; }
        public float DashSpeed { get; }
        public float DashDuration { get; }
        public float DashRecovery { get; }
        public float DashInvulnerability { get; }
        public float DashJumpCarrySpeed { get; }
        public float DashJumpCarryDuration { get; }
        public string RulesVersion { get; }
        public string ContentHash { get; }
        public LayoutMotionProfile(float step, float firstJump, float secondJump, float riseGravity, float fallGravity,
            float dashSpeed, float dashDuration, float dashRecovery, float dashInvulnerability, float dashJumpCarrySpeed, float dashJumpCarryDuration, string rulesVersion)
        {
            var values = new[] { step, firstJump, secondJump, riseGravity, fallGravity, dashSpeed, dashDuration, dashRecovery, dashInvulnerability, dashJumpCarrySpeed, dashJumpCarryDuration };
            foreach (float value in values) { LayoutValues.Finite(value); if (value < 0) throw new ArgumentOutOfRangeException(nameof(step)); }
            if (step <= 0 || riseGravity <= 0 || fallGravity <= 0 || dashDuration <= 0 || dashInvulnerability > dashDuration)
                throw new ArgumentException("Motor proof requires the current positive step/gravity/dash duration.");
            Step = step; FirstJump = firstJump; SecondJump = secondJump; RiseGravity = riseGravity; FallGravity = fallGravity;
            DashSpeed = dashSpeed; DashDuration = dashDuration; DashRecovery = dashRecovery; DashInvulnerability = dashInvulnerability;
            DashJumpCarrySpeed = dashJumpCarrySpeed; DashJumpCarryDuration = dashJumpCarryDuration; RulesVersion = LayoutValues.Id(rulesVersion);
            ContentHash = LayoutValues.Hash(writer => { writer.Write(RulesVersion); foreach (float value in values) writer.Write(value); });
        }
    }
    public interface IChapterTraversalProof
    {
        LayoutProofResult LastResult { get; }
        // Called only after the common structural/catalog checks. No active
        // gameplay world, player, HP, timer or content policy may be mutated.
        bool TryValidate(ChapterLayout layout, LayoutBodyProfile profile, IChapterModuleCatalog catalog, out string error);
    }
}
