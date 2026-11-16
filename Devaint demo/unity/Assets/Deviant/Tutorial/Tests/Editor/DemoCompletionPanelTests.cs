using System.Collections;
using Deviant.Tutorial.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deviant.Tutorial.Tests
{
    public sealed class DemoCompletionPanelTests
    {
        GameObject owner;
        DemoCompletionPanel Create(System.Action village, System.Action title)
        {
            owner = new GameObject("Demo under test", typeof(RectTransform));
            return DemoCompletionPanel.Create(owner.transform, village, title);
        }
        [TearDown] public void Cleanup() { if (owner) Object.DestroyImmediate(owner); }

        [Test] public void CompletionPanelKeepsSharedOutlineFontSmoothForOtherScreens()
        {
            var approved=Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk");
            Assert.That(approved,Is.Not.Null);
            approved.material.mainTexture.filterMode=FilterMode.Point;
            var panel=Create(()=>{},()=>{});
            foreach(var text in panel.GetComponentsInChildren<Text>())
                Assert.That(text.font,Is.SameAs(approved));
            Assert.That(approved.material.mainTexture.filterMode,Is.EqualTo(FilterMode.Bilinear));
            approved.RequestCharactersInTexture("최종 클리어 가방 제작 장비 설정",28);
            Assert.That(approved.material.mainTexture.filterMode,Is.EqualTo(FilterMode.Bilinear));
        }

        [UnityTest] public IEnumerator OpeningInputIsIgnoredAndDestinationIsSubmittedOnlyOnce()
        {
            int village = 0, title = 0;
            var panel = Create(() => village++, () => title++);
            panel.VillageButton.onClick.Invoke(); panel.TitleButton.onClick.Invoke();
            Assert.That(village + title, Is.Zero);
            yield return new WaitForSecondsRealtime(.4f);
            panel.TitleButton.onClick.Invoke(); panel.VillageButton.onClick.Invoke(); panel.TitleButton.onClick.Invoke();
            Assert.That(title, Is.EqualTo(1)); Assert.That(village, Is.Zero);
            Assert.That(panel.VillageButton.interactable, Is.False);
            Assert.That(panel.TitleButton.interactable, Is.False);
        }

        [UnityTest] public IEnumerator HiddenPanelCannotNavigateAndCanBeReopened()
        {
            int village = 0;
            var panel = Create(() => village++, () => Assert.Fail("Wrong destination"));
            panel.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(.4f);
            panel.VillageButton.onClick.Invoke(); Assert.That(village, Is.Zero);
            panel.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(.4f);
            panel.VillageButton.onClick.Invoke(); Assert.That(village, Is.EqualTo(1));
        }

        [Test] public void RetainsRunSummaryAndExplicitControllerNavigation()
        {
            var panel = Create(() => {}, () => {});
            panel.SetSummary("플레이 시간 12:34 / 처치 42 / 마법안료 +10");
            Assert.That(System.Array.Exists(panel.GetComponentsInChildren<Text>(), t => t.text.Contains("12:34") && t.text.Contains("+10")), Is.True);
            Assert.That(panel.VillageButton.navigation.selectOnRight, Is.SameAs(panel.TitleButton));
            Assert.That(panel.TitleButton.navigation.selectOnLeft, Is.SameAs(panel.VillageButton));
        }
    }
}
