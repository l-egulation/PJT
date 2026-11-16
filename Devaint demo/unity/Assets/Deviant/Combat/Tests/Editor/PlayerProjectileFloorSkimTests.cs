using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    // 큰 관통 수리검(반지름 1, 발사 높이 1)은 원 아래 끝이 바닥과 딱 맞닿아, 위치에 따라 던지자마자 바닥에 맞아 사라졌다(2026-11-11).
    // 원 아래 절반에만 걸친 바닥은 통과하고 벽에만 막힌다.
    public class PlayerProjectileFloorSkimTests
    {
        [Test] public void BigPierceShurikenSkimsTheFloorButStopsAtWalls()
        {
            Assert.That(PlayerProjectile.SkimsFloor("shuriken_special", 195, 1, 194), Is.True, "floor exactly under the circle (stage 2 case)");
            Assert.That(PlayerProjectile.SkimsFloor("shuriken_special", 195, 1, 196), Is.False, "a wall rising above the centre still stops it");
            Assert.That(PlayerProjectile.SkimsFloor("shuriken", 195, .2f, 194), Is.False, "other shots keep their terrain rules");
        }
    }
}
