using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1InventorySkinTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui,string name)=>(T)gui.GetType().GetField(name,Hidden).GetValue(gui);
        static void Refresh(MonoBehaviour gui)=>gui.GetType().GetMethod("Refresh").Invoke(gui,null);
        static void CheckSlotLevel(Button cell, string expected)
        {
            var badge = (RectTransform)cell.transform.Find("Slot level");
            Assert.IsTrue(badge && badge.gameObject.activeInHierarchy);
            Assert.AreEqual(Vector2.one, badge.anchorMax);
            Assert.AreEqual(expected, badge.GetComponentInChildren<Text>().text);
        }

        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator SuppliedBookShowsOneFivePlaceBagCurrentPaletteAndScrollableEffects()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            for(int frame=0;frame<900&&!Chapter1NativeEquipmentBookTests.WorldReady();frame++)yield return null;
            Assert.IsTrue(Chapter1NativeEquipmentBookTests.WorldReady());yield return null;yield return null;
            Chapter1NativeEquipmentBookTests.BindTestRig();
            Chapter1NativeEquipmentBookTests.CheckInventory();
            yield return new ExitPlayMode();
        }
    }
}
