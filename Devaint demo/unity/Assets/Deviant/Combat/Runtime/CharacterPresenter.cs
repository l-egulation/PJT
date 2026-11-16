using System;
using UnityEngine;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;

namespace Deviant.Combat
{
    // Presentation only. These sprites never create hitboxes, move actors, or change combat time.
    public sealed class CharacterPresenter : IDisposable
    {
        public string CurrentClip { get; private set; }
        public string CurrentForm { get; private set; }
        public int ActiveEffects => cues?.ActiveCount ?? 0;
        public ICueHandle LastEffectHandle => cues?.LastHandle;
        public string LastEffectError => cues?.LastError;
        CharacterLocomotionExecution locomotion;
        float previousRunTime = -1;
        readonly ApprovedDashPresentation dashArt=new ApprovedDashPresentation();
        readonly JumpMotionPresentation jumpArt=new JumpMotionPresentation();
        public int ActiveJumpAfterimages => jumpArt.ActiveAfterimages;
        BasicAttackCueBridge cues;
        readonly CharacterPresentationBinding binding;
        public string CurrentAttackMotionVersion => binding.Attack?.Timing.ContentHash;
        public string CurrentMotionBindingVersion => binding.Attack?.RegistryVersion;
        public string LastAttackMotionError => binding.Error;
        public string CurrentBodyClipName => binding.Attack?.BodyClipName ?? CurrentClip;
        internal bool TryGetLaserReleaseFraction(Guid execution, out float fraction)
        {
            var attack = binding.Attack;
            fraction = attack != null && attack.ExecutionId == execution ? attack.LaserReleaseFraction : -1;
            return fraction >= 0;
        }
        internal CharacterPresenter(CharacterPresentationBinding binding) { this.binding = binding; }
        void EnsureCues() { if (cues == null) cues = new BasicAttackCueBridge(binding); }
        public void Reset()
        {
            previousRunTime = -1;
            cues?.Reset(); dashArt.Reset(); jumpArt.Reset(); locomotion.Reset(); binding.ResetCapture();
        }
        public void Dispose() { cues?.Dispose(); cues = null; dashArt.Dispose(); jumpArt.Dispose(); binding.ResetCapture(); }
        internal void ReleaseBinding() { Dispose(); binding.Dispose(); }
        public void Draw(float dt)
        {
            EnsureCues();
            try
            {
                if (binding.TryRead(out var actor)) DrawCharacter(in actor, dt);
                else { dashArt.Reset(); jumpArt.Reset(); }
            }
            finally
            {
                Guid current = binding.TryRead(out var actor) && actor.Presentation.HasAction ? actor.Action.ExecutionId : Guid.Empty;
                cues.Tick(current);
            }
        }
        void DrawCharacter(in ActorReadSnapshot actor, float dt)
        {
            var visual = binding.Output; if (!visual) { dashArt.Reset(); jumpArt.Reset(); return; }
            var action = actor.Action; var facts = actor.Presentation;
            var attackMotion = binding.Attack;
            bool captured = facts.HasAction && action.ExecutionId == binding.CapturedExecution;
            if (!binding.HasCatalog && !captured) return;
            bool dashing = actor.Now < actor.Dash.EndsAt;
            CharacterAnimationSet set = null;
            if (!captured || actor.Vitals.Dead || dashing || facts.Approaching) set = binding.FindSet(facts.AppearanceId ?? actor.ActiveWeaponContentId);
            if (set == null && !captured) return;
            CurrentForm = captured ? attackMotion?.Form ?? binding.CapturedForm : set?.form;
            var durations = new LocomotionDurations(set, attackMotion, captured);
            var input = new CharacterLocomotionInput(CurrentForm, actor.Now, dt, actor.Vitals.Dead, dashing, facts.Approaching,
                facts.HasAction, facts.IsBasicAction, facts.BasicIndex, actor.Kinematics.Grounded, actor.Kinematics.VelocityY, facts.Axis);
            string state=locomotion.Advance(in input,ref durations);
            CurrentClip=captured && state==binding.CapturedState ? attackMotion?.BodyClipName ?? state : state;
            var clip=set?.Find(state) ?? set?.Find("attack1") ?? set?.Find("idle");
            // 매듭 접근 재시전(다시 D): 대시 그림 대신 채찍에 끌려가는 전용 그림. 끌려가는 동안 이전 땡기기는 놓는다.
            if (facts.Approaching)
            {
                binding.CancelWhipHold();
                if (action.ContentId == "whip_movement") clip = set?.Find("knot_pull") ?? clip;
            }
            float whipOffset = 0;
            if (state == "run" && actor.Kinematics.Grounded && clip != null)
            {
                float age = locomotion.Elapsed, duration = clip.Duration;
                float prior = previousRunTime < 0 || age < previousRunTime ? 0 : previousRunTime;
                if (duration > 0 && (CrossedContact(prior, age, duration, .120f)
                    || CrossedContact(prior, age, duration, .305f))) binding.Footstep();
                previousRunTime = age;
            }
            else previousRunTime = -1;
            float dashDuration = facts.Approaching ? facts.ApproachVisualDuration : facts.DashDuration;
            var dashSprite=dashArt.Pose(in actor,set,state);
            if(dashSprite){binding.ApplyNativeSprite(dashSprite);CurrentClip=dashing?"dash":"dash_recovery";}
            else if(!actor.Vitals.Dead && binding.TryShurikenArrival(CurrentForm,actor.Now,out var arriveSprite,out var arriveName))
            { binding.ApplyNativeSprite(arriveSprite);CurrentClip=arriveName; }
            else if(!actor.Vitals.Dead && binding.TryBombMissileEmerge(CurrentForm,actor.Now,out var emergeSprite,out var emergeName))
            { binding.ApplyNativeSprite(emergeSprite);CurrentClip=emergeName; }
            else if(!facts.HasAction && binding.TryRecovery(CurrentForm,actor.Now,state,out var returnSprite,out var returnName))
            { binding.ApplyNativeSprite(returnSprite);CurrentClip=returnName; }
            else if(!facts.HasAction && binding.TryWhipHold(CurrentForm,actor.Now,state,out var whipSprite,out var whipName,out whipOffset))
            { binding.ApplyNativeSprite(whipSprite);CurrentClip=whipName; }
            else if (captured && state == binding.CapturedState) binding.ApplyNativeSprite(
                attackMotion?.AtPresentation(action.Elapsed, facts.ImpactTime, facts.MotionDuration, facts.DynamicBodyMapping,
                    action.Phase == CombatActionReadPhase.Charging));
            else if (clip != null) binding.ApplyNativeSprite(facts.HasAction && state.StartsWith("attack")
                // A held charge shot keeps the aim pose just before the shot until it is released (approved 2026-11-06).
                ? clip.Attack(action.Phase == CombatActionReadPhase.Charging ? Mathf.Min(action.Elapsed, facts.ImpactTime * .999f) : action.Elapsed, facts.ImpactTime, facts.MotionDuration)
                : clip.At(state == "dash" ? locomotion.Elapsed / dashDuration * clip.Duration : locomotion.Elapsed * LocomotionPlayback.Scale(CurrentForm, state), state == "run" || state == "idle" || state == "fall_loop"));
            // Select the aerial drawing before main's stretch and echo pass, so both use the same sprite.
            if (!dashing && !dashSprite && !facts.HasAction && !facts.Approaching &&
                binding.TryAirJump(CurrentForm, actor.Now, out var airSprite))
            { binding.ApplyNativeSprite(airSprite); CurrentClip = "air_jump"; }
            visual.flipX = actor.Kinematics.Facing < 0;
            // 채찍 땡기기 떨림: ApplyNativeSprite 가 위치를 0 으로 되돌린 뒤 가로로만 1화소 민다(그림·판정과 무관).
            if (whipOffset != 0) visual.transform.localPosition = new Vector3(whipOffset, 0, 0);
            visual.color = actor.Vitals.Dead ? Color.gray : actor.Statuses.HiddenAt(actor.Now) ? new Color(1, 1, 1, .35f) : Color.white;
            // 숨죽이기(단검 기본 특수, 2026-11-07 확정 b): 몸이 잠깐 먹빛 실루엣이 됐다가 걷히며 은신의 옅은 몸으로 넘어간다.
            if (captured && CurrentClip == "attack_hold_breath")
            {
                float ink = DaggerSkillFx.BodyInk(action.Elapsed);
                if (ink > 0) visual.color = Color.Lerp(visual.color, new Color(27 / 255f, 22 / 255f, 22 / 255f, 1), ink);
            }
            // 폭탄 3연속 차기(2026-11-10): 띄운 폭탄 셋이 차일 때까지 몸 앞에 떠 있다(그림에는 없고 코드가 그린다).
            BombTripleKickBombs.Draw(visual, captured && CurrentClip == "attack_triple_kick" && !dashing, action.Elapsed, actor.Kinematics.Facing);
            dashArt.Draw(in actor,visual);
            // Wall traversal owns authored body poses; discard ordinary jump scale and echoes immediately.
            if (facts.WallTraversal) jumpArt.Reset();
            else jumpArt.Draw(in actor, visual, dashing || dashSprite || facts.HasAction || facts.Approaching);
            if (captured && state == binding.CapturedState)
            {
                if (attackMotion?.Effect != null && attackMotion.Timing.FxStart >= 0 && attackMotion.Timing.SourceTime(action.Elapsed) >= attackMotion.Timing.FxStart)
                    cues.Emit(attackMotion, in actor);
            }
        }
        static bool CrossedContact(float before, float now, float duration, float contact)
        {
            float offset = contact * duration / .360f;
            return Mathf.FloorToInt((now - offset) / duration) > Mathf.FloorToInt((before - offset) / duration);
        }

        struct LocomotionDurations : ICharacterLocomotionDurations
        {
            readonly CharacterAnimationSet set;
            readonly AttackMotionSnapshot attack;
            readonly bool captured;
            public LocomotionDurations(CharacterAnimationSet set,AttackMotionSnapshot attack,bool captured)
            { this.set=set; this.attack=attack; this.captured=captured; }
            public float LandingDuration => captured ? attack?.LandingDuration ?? 0 : set?.Find("land")!=null ? set.Find("land").Duration : 0;
            public float JumpStartDuration => set?.Find("jump_start")!=null ? set.Find("jump_start").Duration : 0;
            public float FallStartDuration => set.Find("fall_start")!=null ? set.Find("fall_start").Duration : 0;
        }
    }
}
