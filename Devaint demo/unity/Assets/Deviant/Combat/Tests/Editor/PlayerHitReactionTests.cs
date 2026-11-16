using NUnit.Framework;
using UnityEngine;
using Deviant.Game.Diagnostics;

namespace Deviant.Combat.Tests
{
    public class PlayerHitReactionTests
    {
        [TestCase(1,0.5f,2.5f)]
        [TestCase(-1,-0.5f,2.5f)]
        public void KnockbackClearsTheAttackBoxOnEitherSide(int direction,float playerX,float attackWidth)
        {
            var policy=PlayerHitReactionSettings.Load();var body=new Bounds(new Vector3(playerX,0,0),Vector3.one);
            var attack=new Bounds(Vector3.zero,new Vector3(attackWidth,2,0));float distance=policy.Distance(body,attack,direction);
            body.center+=Vector3.right*direction*distance;
            Assert.That(direction>0?body.min.x: -body.max.x,Is.GreaterThanOrEqualTo(attackWidth/2+policy.attackBoxMargin-.001f));
        }
        [Test] public void PushDistanceIsBoundedAndHealthBarsUseGreenOrangeRed()
        {
            var policy=PlayerHitReactionSettings.Load();
            Assert.That(policy.Distance(new Bounds(Vector3.zero,Vector3.one),new Bounds(Vector3.zero,Vector3.one*100),1),Is.EqualTo(policy.maximumDistance));
            Assert.That(CombatDiagnostics.EnemyHealthColor(.8f).g,Is.GreaterThan(.7f));
            Assert.That(CombatDiagnostics.EnemyHealthColor(.5f).r,Is.GreaterThan(.9f));
            Assert.That(CombatDiagnostics.EnemyHealthColor(.2f).g,Is.LessThan(.3f));
            var node=new GameObject("Foot health anchor");
            try {var actor=node.AddComponent<CombatActor>();actor.Position=new Vector2(2,3);Assert.That(CombatDiagnostics.EnemyFootHealthAnchor(actor).y,Is.LessThan(actor.Position.y));}
            finally {Object.DestroyImmediate(node);}
        }
    }
}
