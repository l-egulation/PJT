using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class LocomotionPlaybackTests
    {
        const string Valid = "{\"schemaVersion\":1,\"forms\":[{\"form\":\"base_sword\",\"run\":1.5,\"walk\":1,\"idle\":0.5}]}";

        [Test] public void ParsesPerFormSpeedsAndRejectsOutOfRangeDuplicateOrUnknownDocuments()
        {
            Assert.That(LocomotionPlayback.TryParse(Valid, out var document, out var error), Is.True, error);
            Assert.That(document.forms[0].run, Is.EqualTo(1.5f));
            foreach (var invalid in new[]
            {
                Valid.Replace("1.5", "3.5"), Valid.Replace("1.5", "0.1"), Valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                "{\"schemaVersion\":1,\"forms\":[{\"form\":\"a\"},{\"form\":\"a\"}]}",
                "{\"schemaVersion\":1,\"forms\":[{\"form\":\"\"}]}",
            })
                Assert.That(LocomotionPlayback.TryParse(invalid, out _, out _), Is.False, invalid);
        }
        [Test] public void OnlyLoopingBodyStatesOfListedFormsAreScaledAndEverythingElsePlaysAtOne()
        {
            LocomotionPlayback.Reload();
            Assert.That(LocomotionPlayback.Scale(null, "run"), Is.EqualTo(1));
            Assert.That(LocomotionPlayback.Scale("unknown_form", "run"), Is.EqualTo(1));
            foreach (var form in new[] { "base_sword", "whip", "rapier", "dagger", "launcher", "launcher_laser", "launcher_shotgun", "launcher_grenade", "shuriken", "bomb", "hammer", "shield", "greatsword" })
                foreach (var state in new[] { "run", "walk", "idle" })
                    Assert.That(LocomotionPlayback.Scale(form, state), Is.EqualTo(.6f).Within(1e-6f), form + " " + state);
            Assert.That(LocomotionPlayback.Scale("base_sword", "dash"), Is.EqualTo(1));
            Assert.That(LocomotionPlayback.Scale("base_sword", "attack1"), Is.EqualTo(1));
        }
    }
}
