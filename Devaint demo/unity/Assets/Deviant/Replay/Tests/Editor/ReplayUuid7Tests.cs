using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Deviant.Replay.Tests
{
    public class ReplayUuid7Tests
    {
        const long VectorTime = 1645557742000;
        [Test] public void RfcVectorUsesCanonicalByteOrderAndRoundTripsTimestamp()
        {
            var generator = new ReplayUuid7(() => DateTimeOffset.FromUnixTimeMilliseconds(VectorTime),
                () => Guid.Parse("00000000-0000-4cc3-98c4-dc0c0c07398f"));
            string id = generator.Next();
            Assert.That(id, Is.EqualTo("017f22e279b07cc398c4dc0c0c07398f"));
            Assert.That(Guid.TryParseExact(id, "N", out _), Is.True);
            Assert.That(ReplayUuid7.TryGetUtc(id, out var utc), Is.True);
            Assert.That(utc.ToUnixTimeMilliseconds(), Is.EqualTo(VectorTime));
            Assert.That(ReplayUuid7.TryGetUtc(Guid.Parse(id).ToString("D"), out utc), Is.True);
        }
        [Test] public void SameMillisecondAndClockRollbackStayStrictlyOrdered()
        {
            long now = VectorTime;
            var generator = new ReplayUuid7(() => DateTimeOffset.FromUnixTimeMilliseconds(now));
            string previous = generator.Next();
            for (int i = 0; i < 10000; i++)
            {
                if (i == 5000) now -= 1000;
                string next = generator.Next();
                Assert.That(string.CompareOrdinal(previous, next), Is.LessThan(0));
                Assert.That(ReplayUuid7.TryGetUtc(next, out var utc), Is.True);
                Assert.That(utc.ToUnixTimeMilliseconds(), Is.EqualTo(VectorTime));
                previous = next;
            }
            now = VectorTime + 1;
            string future = generator.Next();
            Assert.That(string.CompareOrdinal(previous, future), Is.LessThan(0));
            ReplayUuid7.TryGetUtc(future, out var advanced);
            Assert.That(advanced.ToUnixTimeMilliseconds(), Is.EqualTo(now));
        }
        [Test] public void CounterCarryPreservesVariantAndAdvancesTimestampAtExhaustion()
        {
            var generator = new ReplayUuid7(() => DateTimeOffset.FromUnixTimeMilliseconds(VectorTime),
                () => Guid.ParseExact("0000000000004fffbfffffffffffffff", "N"));
            string first = generator.Next(), next = generator.Next();
            Assert.That(string.CompareOrdinal(first, next), Is.LessThan(0));
            Assert.That(ReplayUuid7.TryGetUtc(next, out var utc), Is.True);
            Assert.That(utc.ToUnixTimeMilliseconds(), Is.EqualTo(VectorTime + 1));
            var carry = new ReplayUuid7(() => DateTimeOffset.FromUnixTimeMilliseconds(VectorTime),
                () => Guid.ParseExact("00000000000040008fffffffffffffff", "N"));
            carry.Next();
            Assert.That(carry.Next(), Is.EqualTo("017f22e279b070009000000000000000"));
        }
        [Test] public void ConcurrentGenerationDoesNotDuplicateOrMutateUnityRandom()
        {
            var generator = new ReplayUuid7(() => DateTimeOffset.FromUnixTimeMilliseconds(VectorTime));
            var values = new ConcurrentBag<string>();
            var state = UnityEngine.Random.state;
            Parallel.For(0, 2000, i => values.Add(generator.Next()));
            Assert.That(values.Distinct().Count(), Is.EqualTo(2000));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
        }
        [TestCase("not-a-uuid")]
        [TestCase("00000000000040008000000000000000")]
        [TestCase("017f22e279b07cc3f8c4dc0c0c07398f")]
        [TestCase("ffffffffffff7cc398c4dc0c0c07398f")]
        public void LegacyInvalidOrUnrepresentableTimesAreNotInvented(string id)
            => Assert.That(ReplayUuid7.TryGetUtc(id, out _), Is.False);
        [Test] public void BeforeEpochClockIsRejected()
        {
            var generator = new ReplayUuid7(() => DateTimeOffset.FromUnixTimeMilliseconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.Next());
        }
    }
}
