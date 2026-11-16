using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Run;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class SketchRewardChoiceScreenTests
    {
        [TestCase("앞으로 빠르게 돌진하며 길목의 적을 찌른다.",8,"앞으로 빠르게\n돌진하며 길목의\n적을 찌른다.")]
        [TestCase("<color=#abcdef>체력을 3 회복합니다.</color>",6,"<color=#abcdef>체력을 3\n회복합니다.</color>")]
        [TestCase("효과\n\n대가",8,"효과\n\n대가")]
        [TestCase("글자",2,"글자")]
        [TestCase(null,2,"")]
        public void DescriptionWrapPreservesKoreanWordsAndColorMarkup(string source,int width,string expected)
        {
            Assert.That(SketchRewardChoiceScreen.WrapWords(source,width,value=>value.Length),Is.EqualTo(expected));
        }

        static SketchRunState Run() => new SketchRunState(new Dictionary<string, string[]> {
            ["base_sword"] = new[] { "base_sword_default", "base_sword_range" }, ["dagger"] = new[] { "dagger_default" } },
            new[] { "green-test", "red-test" }, id => id.StartsWith("green") ? "green" : "red", 42,
            // Screen checks use mixed offers; the run-opening rewards are covered by SketchRunStateTests.
            new SketchRewardRules { OpeningRequirements = new float[0], OpeningOffers = false, DoubleOfferEvery = 0 });
        static SketchRewardChoiceScreen.Item[] Items(int n) => Enumerable.Range(0, n).Select(i => new SketchRewardChoiceScreen.Item { Name = "후보 " + i }).ToArray();

        [Test] public void BindingPausesForComparisonAndResumesImmediatelyAfterAcquisition()
        {
            var host = new GameObject("reward binding fixture"); host.SetActive(false);
            try
            {
                var arena = host.AddComponent<CombatArena>();
                var run = new RunFlow(() => 42).StartNewRun(0); run.Sketch = Run();
                var binding = SketchRunBinding.Attach(host, arena, run, 1);
                run.Sketch.RecordKill("elite", 200); run.Sketch.OpenBanked();
                var offer = run.Sketch.PendingOffer;
                binding.ToggleRewardBag();
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                Assert.That(binding.TryChooseReward(0), Is.False, "comparison must not choose behind the bag");
                binding.ToggleRewardBag();
                Assert.That(run.Sketch.PendingOffer, Is.SameAs(offer));
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                Assert.That(binding.TryChooseReward(0), Is.True);
                Assert.That(binding.TryChooseReward(0), Is.False);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False, "choosing grants the item and closes the reward without a receipt");
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                Assert.That(binding.RewardModalShown, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test] public void BankingARewardDoesNotDisplayATopNotice()
        {
            var host=new GameObject("quiet reward banking fixture");host.SetActive(false);
            try
            {
                var arena=host.AddComponent<CombatArena>();var run=new RunFlow(()=>42).StartNewRun(0);run.Sketch=Run();
                var binding=SketchRunBinding.Attach(host,arena,run,1);binding.ReportKill("elite",200);
                Assert.That(run.Sketch.Banked,Is.GreaterThan(0));Assert.That(binding.Notice,Is.Null);
            }
            finally{Object.DestroyImmediate(host);}
        }
        [Test] public void BankedRewardEntryPointUsesTheSameOfferStateAsTheGKeyPath()
        {
            var host = new GameObject("banked reward entry fixture"); host.SetActive(false);
            try
            {
                var arena = host.AddComponent<CombatArena>();
                var run = new RunFlow(() => 42).StartNewRun(0); run.Sketch = Run();
                var binding = SketchRunBinding.Attach(host, arena, run, 1);
                run.Sketch.RecordKill("elite", 200);

                Assert.That(binding.TryOpenBankedReward(), Is.True);
                Assert.That(binding.State.PendingOffer, Is.Not.Null);
                Assert.That(binding.RewardModalShown, Is.True);
                Assert.That(binding.TryOpenBankedReward(), Is.False, "an already-open offer cannot be opened twice");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test] public void CompareBagKeepsOfferOrderAndHighlightedCandidate()
        {
            var run = Run(); run.RecordKill("elite", 200); Assert.That(run.OpenBanked(), Is.True);
            var offer = run.PendingOffer; var items = Items(offer.Length); var screen = new SketchRewardChoiceScreen();
            screen.Offer(offer, items, run.Banked, false); screen.Move(2);
            // Re-presenting after a comparison bag does not re-roll or reset focus.
            screen.Offer(offer, items, run.Banked, false);
            Assert.That(screen.Selected, Is.EqualTo(2)); Assert.That(screen.Items, Is.SameAs(items));
            Assert.That(run.PendingOffer, Is.SameAs(offer)); Assert.That(run.OffersOpened, Is.EqualTo(1));
        }

        [Test] public void SharedComparisonBagOwnsItsBackdropAndClosingPreservesThePendingOffer()
        {
            var host = new GameObject("shared reward comparison fixture"); host.SetActive(false);
            try
            {
                var arena = host.AddComponent<CombatArena>();
                var run = new RunFlow(() => 42).StartNewRun(0); run.Sketch = Run();
                var binding = SketchRunBinding.Attach(host, arena, run, 1);
                using var bag = new Deviant.Inventory.InventoryPresenter(new Deviant.Inventory.InkInventory(new[] { "sword" }), arena.Controls);
                binding.SharedBagOpen = () => bag.IsOpen;
                binding.SharedBagVisibility = shown => { if (shown) bag.Open(); else bag.Close(); };
                int backdropQueries = 0;
                binding.SharedRewardBackdrop = () => { backdropQueries++; return bag.IsOpen; };
                run.Sketch.RecordKill("elite", 200); run.Sketch.OpenBanked(); var offer = run.Sketch.PendingOffer;
                for (int i = 0; i < 3; i++)
                {
                    binding.ToggleRewardBag();
                    Assert.That(bag.IsOpen, Is.True); Assert.That(binding.ShowingRewardChoice, Is.False);
                    // A shared uGUI bag must return before making any IMGUI call (which is invalid outside OnGUI).
                    typeof(SketchRunBinding).GetMethod("DrawRewardScreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(binding, null);
                    Assert.That(backdropQueries, Is.EqualTo(i + 1), "the bag owns the only backdrop");
                    Assert.That(binding.TryChooseReward(0), Is.False);
                    binding.ToggleRewardBag();
                    Assert.That(binding.ShowingRewardChoice, Is.True); Assert.That(run.Sketch.PendingOffer, Is.SameAs(offer));
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                }
                Assert.That(binding.TryChooseReward(0), Is.True);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test] public void SubmitChoosesOneRealRewardAndRejectsRepeatedSubmit()
        {
            var run = Run(); run.RecordKill("elite", 200); run.OpenBanked(); var offer = run.PendingOffer;
            int at = System.Array.FindIndex(offer, c => !c.IsInk), calls = 0;
            var screen = new SketchRewardChoiceScreen();
            screen.Offer(offer, Items(3), run.Banked, false); screen.Move(at);
            screen.Chosen = i => { calls++; run.Choose(i); };
            screen.Activate(); screen.Activate();
            Assert.That(calls, Is.EqualTo(1)); Assert.That(run.PendingOffer, Is.Null);
            Assert.That(run.Arsenal.Weapons.Count, Is.EqualTo(1)); Assert.That(run.WeaponsPicked, Is.EqualTo(1));
            Assert.That(run.Arsenal.StateOf(run.Arsenal.Weapons.First()), Is.EqualTo(SketchWeaponState.Unconfirmed));
        }
        [Test] public void InkIsRegisteredBeforePostPickActionsAndPostPickDoesNotGrantAgain()
        {
            var run = Run(); run.RecordKill("elite", 200); run.OpenBanked(); var offer = run.PendingOffer;
            int at = System.Array.FindIndex(offer, c => c.IsInk); var ink = offer[at].Ink;
            var screen = new SketchRewardChoiceScreen(); screen.Offer(offer, Items(3), 0, false);
            screen.Chosen = i => run.Choose(i); screen.Move(at); screen.Activate();
            Assert.That(run.Arsenal.Registered(ink), Is.True);
            int configured = 0; screen.Picked(ink, new SketchRewardChoiceScreen.Item { Name = ink, IsInk = true });
            screen.Configured = () => configured++; screen.Move(1); screen.Activate(); screen.Activate();
            Assert.That(configured, Is.EqualTo(1)); Assert.That(run.Arsenal.Codex.Count, Is.EqualTo(1));
            Assert.That(run.Arsenal.RemainingInkUses(ink), Is.EqualTo(10), "post-pick actions never grant twice");
        }
        [Test] public void AQueuedBossOfferIsNotConsumedByPostPickContinue()
        {
            var run = Run(); run.RecordKill("elite", 200); run.OpenBanked(); run.OpenBossOffer();
            var current = run.PendingOffer; run.Choose(0); var boss = run.PendingOffer;
            Assert.That(boss, Is.Not.Null); Assert.That(boss, Is.Not.SameAs(current)); Assert.That(run.PendingIsBoss, Is.True);
            var screen = new SketchRewardChoiceScreen(); screen.Picked(new object(), new SketchRewardChoiceScreen.Item { Name = "획득" });
            int continued = 0; screen.Continued = () => continued++; screen.Activate();
            Assert.That(continued, Is.EqualTo(1)); Assert.That(run.PendingOffer, Is.SameAs(boss));
            screen.Offer(boss, Items(3), 0, true); Assert.That(screen.Acquired, Is.False); Assert.That(screen.Selected, Is.Zero);
        }
        [Test] public void SelectedCardSubmitsDirectlyWithoutAnActionRow()
        {
            var screen=new SketchRewardChoiceScreen();var key=new object();int chosen=-1,calls=0;
            screen.Chosen=i=>{chosen=i;calls++;};screen.Offer(key,Items(3),1,false);
            screen.Move(2);screen.Activate();screen.Activate();
            Assert.That(chosen,Is.EqualTo(2));Assert.That(calls,Is.EqualTo(1));
        }
        [Test] public void NewOfferResetsScrollButSameOfferPreservesSelection()
        {
            var screen=new SketchRewardChoiceScreen();var key=new object();screen.Offer(key,Items(3),1,false);
            screen.Move(2);screen.Offer(key,screen.Items,1,false);Assert.That(screen.Selected,Is.EqualTo(2));
            screen.Offer(new object(),Items(3),0,true);Assert.That(screen.Selected,Is.Zero);Assert.That(screen.ScrollOffset,Is.Zero);
        }
        [Test] public void DescriptionScrollStaysWithSelectedCardAndClampsAtBothEnds()
        {
            var screen=new SketchRewardChoiceScreen();screen.Offer(new object(),Items(3),1,false);
            // Layout supplies each candidate's actual overflow; simulate different long descriptions.
            var limits=(float[])typeof(SketchRewardChoiceScreen).GetField("scrollMax",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(screen);
            limits[0]=120;limits[1]=60;
            screen.Scroll(40);Assert.That(screen.ScrollOffset,Is.EqualTo(40));
            screen.Move(1);Assert.That(screen.ScrollOffset,Is.Zero);
            screen.Scroll(1000);Assert.That(screen.ScrollOffset,Is.EqualTo(60));
            screen.Move(-1);Assert.That(screen.ScrollOffset,Is.EqualTo(40));
            screen.Scroll(-1000);Assert.That(screen.ScrollOffset,Is.Zero);
            Assert.That(screen.Selected,Is.Zero,"vertical scrolling keeps the selected reward");
        }
        [Test] public void OptionalRerollDoesNotOverlapRewardCards()
        {
            for(int i=0;i<3;i++)Assert.IsFalse(SketchRewardChoiceScreen.Card(i).Overlaps(SketchRewardChoiceScreen.RerollButton));
            Assert.That(SketchRewardChoiceScreen.RerollButton.xMin,Is.GreaterThanOrEqualTo(650));
            Assert.That(SketchRewardChoiceScreen.RerollButton.yMax,Is.LessThan(540));
        }
        [TestCase(960,540)] [TestCase(1920,1080)] [TestCase(701,499)] [TestCase(2222,922)]
        public void NativeLayoutAndPointerAgreeInsideTheLetterboxedViewport(int width, int height)
        {
            var viewport = SketchRewardChoiceScreen.Viewport(width, height);
            Assert.That(viewport.width / viewport.height, Is.EqualTo(16f/9).Within(.001));
            Assert.That(viewport.xMin, Is.GreaterThanOrEqualTo(-.01f)); Assert.That(viewport.yMin, Is.GreaterThanOrEqualTo(-.01f));
            Assert.That(viewport.xMax, Is.LessThanOrEqualTo(width + .01)); Assert.That(viewport.yMax, Is.LessThanOrEqualTo(height + .01));
            var screen = new SketchRewardChoiceScreen(); screen.Offer(new object(), Items(3), 0, false);
            var native = SketchRewardChoiceScreen.Card(2).center; var position = viewport.position + native * (viewport.width / 960);
            screen.Pointer(new Vector2(position.x, height - position.y), width, height);
            Assert.That(screen.Selected, Is.EqualTo(2));
            screen.Move(-1); screen.Pointer(new Vector2(position.x, height - position.y), width, height);
            Assert.That(screen.Selected, Is.EqualTo(1), "a stationary mouse must not override keyboard focus");
        }
    }
}
