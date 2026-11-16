using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed class TrainingDummy : TestEnemy
    {
        public readonly TrainingDamageMeter Meter = new TrainingDamageMeter();
        public override void Step(float dt) { } // Fixed target, no AI or forced movement.
        public override void ResetEnemy() { base.ResetEnemy(); Meter.Clear(); if(visual)visual.color=new Color32(181,142,70,255); }
        public void Restore() { ResetEnemy(); Physics2D.SyncTransforms(); }
    }
}
