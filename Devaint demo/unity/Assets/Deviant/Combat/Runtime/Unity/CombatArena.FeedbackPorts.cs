using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        public event Action<CombatCompletedStep> SimulationCompleted;
        public event Action<float> FeedbackAccentAdvanced;
        public void AdvanceFeedbackAccent(float realDelta)
        {
            bool paused=Controls.Current.SimulationPaused;
            Controls.AdvanceAccent(realDelta);
            if(FeedbackAccentAdvanced!=null)foreach(Action<float> observer in FeedbackAccentAdvanced.GetInvocationList())
                try{observer(paused?0:realDelta);}catch(Exception error){Debug.LogWarning("Feedback accent output failed: "+error.Message);}
        }
        readonly List<CombatFootSweep> footSweeps = new List<CombatFootSweep>();
        readonly List<Collider2D> feedbackSurfaces = new List<Collider2D>();
        public IReadOnlyList<CombatFootSweep> CompletedFootSweeps => footSweeps;
        public bool FootSweepOverflow {get;private set;}
        public IReadOnlyList<Collider2D> FeedbackSurfaces => feedbackSurfaces;
        public void RegisterFeedbackSurface(Collider2D surface)
        { if (surface && !feedbackSurfaces.Contains(surface)) feedbackSurfaces.Add(surface); }
        internal void RecordFootSweep(Vector2 from, Vector2 to, float width)
        { if(from==to)return;if(footSweeps.Count>=256){FootSweepOverflow=true;return;}footSweeps.Add(new CombatFootSweep(from.x, from.y, to.x, to.y, width)); }
        void BeginFeedbackStep() {footSweeps.Clear();FootSweepOverflow=false;BeginEnvironmentStep();}
        void NotifyCompletedStep(float delta)
        {
            if (!ReadModel.TryGetSession(out var session)) return;
            var value = new CombatCompletedStep(session, SimulationTickCount, Now, delta);
            if (SimulationCompleted == null) return;
            foreach (Action<CombatCompletedStep> observer in SimulationCompleted.GetInvocationList())
                try { observer(value); }
                catch (Exception error) { Debug.LogWarning("Hit feedback output failed: " + error.Message); }
        }
    }
}
