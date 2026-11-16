using System;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The loading screen's round window (docs/design/story/opening/opening-playable.md 「로딩」): every place builds from art that
    // is there, the hero is the run clip of whatever animation set the player is drawn with (no case per weapon), the
    // window opens to its full radius and only chapter 1 puts the ink outline on the hero.
    public sealed class ChapterLoadingScreenTests
    {
        static CharacterAnimationCatalog Catalog(params CharacterAnimationSet[] sets) => new CharacterAnimationCatalog { sets = sets };

        static CharacterAnimationSet Set(string form, string root, int first, params float[] seconds) => new CharacterAnimationSet
        {
            form = form,
            clips = new[]
            {
                new CharacterClip { name = "idle", frames = new[] { root + first.ToString("000") }, seconds = new[] { .1f } },
                new CharacterClip { name = "run", frames = seconds.Select((_, i) => root + (first + i).ToString("000")).ToArray(), seconds = seconds },
            },
        };

        [TestCase(LoadingPlace.Village, false)]
        [TestCase(LoadingPlace.Village, true)]
        [TestCase(LoadingPlace.Chapter1, false)]
        [TestCase(LoadingPlace.Chapter2, false)]
        public void EveryPlaceBuildsFromArtThatIsThere(LoadingPlace place, bool villageInColour)
        {
            var view = ChapterLoadingView.For(place, villageInColour);
            Assert.That(view.Missing, Is.Empty, string.Join(", ", view.Missing));
        }

        [Test]
        public void TheNameUnderTheWindowIsThePlace()
        {
            Assert.That(ChapterLoadingView.Label(LoadingPlace.Village), Is.EqualTo("우물 마을"));
            Assert.That(ChapterLoadingView.Label(LoadingPlace.Chapter1), Is.EqualTo("1챕터"));
            Assert.That(ChapterLoadingView.Label(LoadingPlace.Chapter2), Is.EqualTo("2챕터"));
        }

        [Test]
        public void TheHeroRunsTheRunClipOfTheSetItIsDrawnWith()
        {
            float[] timing = { .065f, .055f, .065f, .055f, .065f, .055f };
            var catalog = Catalog(Set("base_sword", "CharacterApproved11/base_sword/F", 16, timing),
                                  Set("hammer", "CharacterApproved11/hammer/F", 12, timing));
            foreach (var (form, root, first) in new[] { ("base_sword", "CharacterApproved11/base_sword/F", 16), ("hammer", "CharacterApproved11/hammer/F", 12) })
            {
                var run = LoadingHeroRun.From(catalog, form);
                Assert.That(run, Is.Not.Null, form);
                Assert.That(run.Form, Is.EqualTo(form));
                Assert.That(run.Frames.Select(f => f.name), Is.EqualTo(Enumerable.Range(first, 6).Select(i => "F" + i.ToString("000"))), form + " plays its own run frames");
                Assert.That(run.Duration, Is.EqualTo(.36f).Within(1e-4));
                Assert.That(run.At(0), Is.SameAs(run.Frames[0]));
                Assert.That(run.At(.07f), Is.SameAs(run.Frames[1]));
                Assert.That(run.At(.36f + .07f), Is.SameAs(run.Frames[1]), "the run loops");
            }
        }

        [Test]
        public void ABareHandedBodyRunsLikeAnyOtherSet()
        {
            float[] timing = Enumerable.Repeat(.06f, 12).ToArray();
            var body = new CharacterAnimationSet
            {
                form = "village_unarmed",
                clips = new[] { new CharacterClip { name = "run", frames = Enumerable.Range(1, 12).Select(i => "UnarmedBody/run/F" + i.ToString("00")).ToArray(), seconds = timing } },
            };
            var run = LoadingHeroRun.From(Catalog(body), "village_unarmed");
            Assert.That(run, Is.Not.Null);
            Assert.That(run.Frames.Length, Is.EqualTo(12));
            Assert.That(run.Duration, Is.EqualTo(.72f).Within(1e-4));
        }

        [Test]
        public void NoRunMeansNoHero()
        {
            var catalog = Catalog(Set("base_sword", "CharacterApproved11/base_sword/F", 16, .065f, .055f));
            Assert.That(LoadingHeroRun.From(catalog, "no_such_form"), Is.Null);
            Assert.That(LoadingHeroRun.From(catalog, null), Is.Null);
            Assert.That(LoadingHeroRun.From(null, "base_sword"), Is.Null);
            var idleOnly = new CharacterAnimationSet { form = "idle_only", clips = new[] { new CharacterClip { name = "idle", frames = new[] { "CharacterApproved11/base_sword/F016" }, seconds = new[] { .1f } } } };
            Assert.That(LoadingHeroRun.From(Catalog(idleOnly), "idle_only"), Is.Null);
            var broken = Set("broken", "CharacterApproved11/no_such_folder/F", 1, .1f);
            Assert.That(LoadingHeroRun.From(Catalog(broken), "broken"), Is.Null, "a missing frame draws no hero rather than a hole");
        }

        [TestCase(LoadingPlace.Chapter1, true)]
        [TestCase(LoadingPlace.Chapter2, false)]
        [TestCase(LoadingPlace.Village, false)]
        public void TheWindowOpensAroundTheRunningHero(LoadingPlace place, bool outlined)
        {
            var root = new GameObject("Loading check", typeof(RectTransform), typeof(Canvas));
            var view = ChapterLoadingView.For(place, false);
            try
            {
                var catalog = Catalog(Set("base_sword", "CharacterApproved11/base_sword/F", 16, .065f, .055f, .065f, .055f, .065f, .055f));
                var run = LoadingHeroRun.From(catalog, "base_sword");
                view.Build((RectTransform)root.transform, new Vector2(480, 240), run);
                view.Tick(0);
                Assert.That(view.WindowRadius, Is.Zero, "the window starts shut");
                view.Tick(ChapterLoadingView.OpenSeconds / 2);
                Assert.That(view.WindowRadius, Is.InRange(1, ChapterLoadingView.Radius - 1), "and opens");
                view.Tick(ChapterLoadingView.OpenSeconds + .07f);
                Assert.That(view.WindowRadius, Is.EqualTo(ChapterLoadingView.Radius));
                Assert.That(view.HeroSprite, Is.SameAs(run.At(ChapterLoadingView.OpenSeconds + .07f)));
                Assert.That(view.HeroOutlined, Is.EqualTo(outlined), "only chapter 1 inks the hero's outline");
                Assert.That(root.GetComponentsInChildren<Mask>(true), Has.Length.EqualTo(1), "one round window clips the place");
            }
            finally { view.Dispose(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void WithoutAPlayerThePlaceStillShows()
        {
            var root = new GameObject("Loading check", typeof(RectTransform), typeof(Canvas));
            var view = ChapterLoadingView.For(LoadingPlace.Chapter2);
            try
            {
                view.Build((RectTransform)root.transform, new Vector2(480, 240), null);
                view.Tick(1);
                Assert.That(view.HeroSprite, Is.Null);
                Assert.That(view.WindowRadius, Is.EqualTo(ChapterLoadingView.Radius));
            }
            finally { view.Dispose(); Object.DestroyImmediate(root); }
        }
    }
}
