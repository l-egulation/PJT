using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1EquippedInkGuiTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetField(name, Hidden).GetValue(gui);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        static void Click(MonoBehaviour gui, Button button) { Assert.IsTrue(button.interactable, button.name); button.onClick.Invoke(); Refresh(gui); }
        static Button Slot(MonoBehaviour gui, int index) => Field<RectTransform>(gui, "growthSlots").GetComponentsInChildren<Button>().Single(b => b.name == "GrowthSlot" + index);
        static Button Owned(MonoBehaviour gui, System.Guid id) => Field<RectTransform>(gui, "growthInks").GetComponentsInChildren<Button>().Single(b => b.name == "GrowthInk" + id);
        static void AssertMounted(MonoBehaviour gui, GrowthSlot slot)
        {
            Assert.AreEqual(slot.Part.name, Field<Text>(gui, "growthDetailName").text);
            Assert.AreEqual(slot.Part.description, Field<Text>(gui, "growthDescription").text);
            Assert.AreEqual($"parts/ink-grade-{slot.Part.grade}",Field<Text>(gui,"growthDetailName").transform.Find("Grade").GetComponent<Image>().sprite.name);
            foreach (var name in new[] { "growthInsert", "growthRecolor", "growthSell", "growthDestroy" }) Assert.IsFalse(Field<Button>(gui, name).interactable, name + " cannot transact on an inspected mounted ink");
        }
        static bool Ringed(Button cell) { var ring = cell.transform.Find("Selected ring"); return ring && ring.gameObject.activeSelf; }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator ClickingMountedInksShowsTheirEffectsAndLiveSlotLevelsWithoutSellingThem()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            for(int frame=0;frame<900&&!Chapter1NativeEquipmentBookTests.WorldReady();frame++)yield return null;
            Assert.IsTrue(Chapter1NativeEquipmentBookTests.WorldReady());yield return null;yield return null;
            Chapter1NativeEquipmentBookTests.BindTestRig();
            Chapter1NativeEquipmentBookTests.CheckShop();
            yield return new ExitPlayMode();
        }
    }
}
