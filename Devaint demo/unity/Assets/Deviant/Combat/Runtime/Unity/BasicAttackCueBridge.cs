using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    // Every eligible basic attack supplies its start-bound media and timing.
    sealed class BasicAttackCueBridge : IDisposable
    {
        sealed class Entry { public ICueScope scope; public ICueHandle handle; }
        readonly CharacterPresentationBinding binding;
        readonly SpriteCueFactory factory;
        readonly CuePlayer player;
        readonly Dictionary<Guid, Entry> executions = new Dictionary<Guid, Entry>();
        readonly List<Guid> completed = new List<Guid>();
        string compilationError;
        public int ActiveCount => player.ActiveCount;
        public ICueHandle LastHandle { get; private set; }
        public string LastError => compilationError ?? LastHandle?.Error;
        public BasicAttackCueBridge(CharacterPresentationBinding binding)
        { this.binding = binding; factory = new SpriteCueFactory(binding.Material, 18); player = new CuePlayer(factory); }
        public void Emit(AttackMotionSnapshot media, in ActorReadSnapshot actor)
        {
            if (media.Form == "shield") return; // Approved shield contact is sampled by ShieldSkillPresentation.
            Guid execution = media.ExecutionId;
            if (executions.ContainsKey(execution)) return;
            var entry = new Entry(); executions.Add(execution, entry); compilationError = null;
            try
            {
                var effect = binding.UseFeelSwordTrail && CombatFeelArt.CanReplace(media) ? CombatFeelArt.Sword(media) : media.Effect;
                var definition = new PresentationCueDefinition("BasicAttackEffect_" + media.Form + "_" + media.State,
                    effect.frames, effect.seconds, effect.x, effect.y, effect.flipX);
                // Rapier thrusts stay on the blade while the basic lunge advances or turns.
                Func<CueAnchor> follow = media.Form == "rapier" ? (Func<CueAnchor>)(() =>
                {
                    if (!binding.TryRead(out var current)) throw new ObjectDisposedException(nameof(CharacterPresentationBinding));
                    return new CueAnchor(current.Kinematics.X, current.Kinematics.Y, current.Kinematics.Facing);
                }) : null;
                factory.Publish(definition, effect.sprites ?? new UnityEngine.Sprite[definition.Frames.Count], effect.scale, effect.rotation, follow);
                bool ribbon=binding.UseContinuousSwordTrail&&CombatFeelArt.CanReplace(media)&&SwordRibbonView.Supports(media);
                ICueViewFactory view=ribbon?new SwordRibbonFactory(binding,media,definition):null;
                if(binding.FeelFeedback&&binding.FeelFeedback.UseWhipTrail&&WhipTrailView.Supports(media))view=new WhipTrailFactory(binding,media,definition);
                entry.scope = player.OpenScope("basic attack " + execution,view);
                entry.handle = entry.scope.Play(definition, new CueOccurrence(execution, definition.Id),
                    new CueAnchor(actor.Kinematics.X, actor.Kinematics.Y, actor.Kinematics.Facing), new AttackClock(binding, media.Timing, media.StartedAt));
                LastHandle = entry.handle;
            }
            catch (Exception exception) { entry.scope?.Dispose(); compilationError = exception.Message; }
        }
        public void Tick(Guid currentExecution)
        {
            player.Tick();
            foreach (var pair in executions)
                if (pair.Key != currentExecution && (pair.Value.handle == null || Terminal(pair.Value.handle.Status))) completed.Add(pair.Key);
            for (int i = 0; i < completed.Count; i++)
            {
                var entry = executions[completed[i]]; entry.scope?.Dispose(); executions.Remove(completed[i]);
            }
            completed.Clear();
        }
        public void Reset()
        { player.Reset(); executions.Clear(); completed.Clear(); LastHandle = null; compilationError = null; }
        public void Dispose() { player.Dispose(); factory.Dispose(); executions.Clear(); completed.Clear(); }
        static bool Terminal(CueStatus status) => status == CueStatus.Completed || status == CueStatus.Cancelled || status == CueStatus.Failed;
        sealed class AttackClock : ICueClock
        {
            readonly CharacterPresentationBinding binding;
            readonly AttackMotionTiming timing;
            readonly float started;
            public AttackClock(CharacterPresentationBinding binding, AttackMotionTiming timing, float started)
            {
                this.binding = binding; this.timing = timing; this.started = started;
            }
            public float ReadElapsed()
            {
                if (!binding.TryRead(out var actor)) throw new ObjectDisposedException(nameof(CharacterPresentationBinding));
                return timing.CueElapsed(Math.Max(0, actor.Now - started));
            }
        }
    }
}
