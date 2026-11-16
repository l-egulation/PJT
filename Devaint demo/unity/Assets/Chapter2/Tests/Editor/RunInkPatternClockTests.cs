using System.Linq;
using Deviant.Chapter2.RoseBlue;
using NUnit.Framework;

namespace Deviant.Chapter2.Tests
{
    public sealed class RunInkPatternClockTests
    {
        [Test] public void SlowAdvancesEachCasterIndependentlyAndKeepsSpawnedHazardLifetime()
        {
            var clock = new PatternClock(new[] {
                new Cue(1, CueKind.Spawn, "rose", group: "rose"),
                new Cue(1, CueKind.Spawn, "blue", group: "blue"),
                new Cue(2, CueKind.Remove, "blue", independent: true, group: "blue"),
                new Cue(2, CueKind.Remove, "rose", independent: true, group: "rose") });
            int Caster(Cue cue) => cue.Channel == "rose" ? 1 : 2;
            var first = clock.Advance(1, .5, 1, Caster);
            Assert.That(first.Single().Channel, Is.EqualTo("blue")); Assert.That(first.Single().Kind, Is.EqualTo(CueKind.Spawn));
            var second = clock.Advance(1, .5, .1, Caster);
            Assert.That(second.Any(c => c.Channel == "rose" && c.Kind == CueKind.Spawn), Is.True);
            Assert.That(second.Any(c => c.Channel == "blue" && c.Kind == CueKind.Remove), Is.True, "slowing Blue after launch must not prolong its hazard");
            var third = clock.Advance(1, 1, 1, Caster);
            Assert.That(third.Single().Channel, Is.EqualTo("rose")); Assert.That(clock.Finished, Is.True);
        }
    }
}
