using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class WeaponDisplayNameIntegrationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Forms = { "base_sword", "hammer", "launcher" };
        static readonly string[] Names = { "검", "망치", "발사기" };

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator ChapterOneBagUsesEquippedWeaponNames()
        {
            Prepare();
            yield return new EnterPlayMode();
            SetMemoryProfile();
            SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);
            yield return Exercise(Chapter1MapSelector.ProceduralScene);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator ChapterTwoBagUsesTheSameEquippedWeaponNames()
        {
            Prepare();
            yield return new EnterPlayMode();
            SetMemoryProfile();
            Chapter1RunHost.Ensure().PrepareNewRun(null);
            SceneManager.LoadScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
            yield return Exercise("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
            yield return new ExitPlayMode();
        }

        static void Prepare()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void SetMemoryProfile()
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
        }

        static IEnumerator Exercise(string scene)
        {
            SketchRunBinding sketch = null;
            for (int frame = 0; frame < 900 && !(sketch && sketch.State != null && sketch.Arena && sketch.Arena.Player); frame++)
            {
                yield return null;
                sketch = Object.FindAnyObjectByType<SketchRunBinding>();
            }
            Assert.That(sketch && sketch.State != null && sketch.Arena && sketch.Arena.Player, Is.True, scene);
            yield return null; yield return null;
            var arena = sketch.Arena;
            foreach (var enemy in arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            var host = arena.GetComponent<InventoryRuntimeHost>();
            Assert.That(host, Is.Not.Null);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(g =>
                g.GetType().Name == "TemporaryGameGui" && g.GetType().GetField("host", Hidden).GetValue(g) == host);
            for (int index = 0; index < Forms.Length; index++)
            {
                var form = arena.catalog.forms.Single(f => f.id == Forms[index]);
                Assert.That(form.name, Is.EqualTo(Names[index]));
                var weapon = sketch.State.Arsenal.Acquire(form.id, form.specials[0].id, new string[] { null });
                sketch.State.Arsenal.Confirm(weapon.Id);
                sketch.State.Arsenal.Tick(10);
                int slot = index % 2;
                sketch.State.Arsenal.Equip(weapon.Id, slot);
                yield return null; yield return null;
                Assert.That(arena.catalog.forms[arena.Player.weapons[slot].FormIndex].id, Is.EqualTo(form.id));
                Assert.That(host.WeaponName(slot), Is.EqualTo(Names[index]));
                var hudWeapon = gui.GetType().GetMethod("CombatWeapon", Hidden).Invoke(gui, new object[] { slot });
                Assert.That(hudWeapon, Is.Not.Null);
                Assert.That(hudWeapon.GetType().GetField("name").GetValue(hudWeapon), Is.EqualTo(Names[index]));
                if (form.id == "launcher")
                {
                    var skill = gui.GetType().GetMethod("CombatSkill", Hidden).Invoke(gui, new[] { (object)slot, hudWeapon });
                    Assert.That(skill, Is.Not.Null);
                    Assert.That(skill.GetType().GetField("presentationName").GetValue(skill), Is.EqualTo(Names[index]));
                }
                host.Presenter.Open();
                yield return null;
                gui.GetType().GetMethod("Refresh").Invoke(gui, null);
                Canvas.ForceUpdateCanvases();
                string expected = (slot == 0 ? "A  " : "B  ") + Names[index] + " · " + form.specials[0].name;
                Assert.That(gui.GetComponentsInChildren<Text>().Any(t => t.text == expected), Is.True,
                    scene + ": the visible bag must show " + expected);
                Assert.That(gui.GetComponentsInChildren<Text>().Any(t => Forms.Select((_, i) => "기본" + Names[i]).Any(t.text.Contains)), Is.False);
                TestContext.Progress.WriteLine(scene + ": visible bag = " + expected);
                var world = Object.FindAnyObjectByType<ForestCombatWorld>();
                var chapter2 = Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>();
                Capture(gui, world ? world.View : chapter2.sceneCamera, (world ? "chapter1-" : "chapter2-") + form.id);
                host.Presenter.Close();
            }
        }

        static void Capture(MonoBehaviour gui, Camera camera, string name)
        {
            var canvas = gui.GetComponent<Canvas>();
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var target = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 1;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui, null);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/weapon-display-names/captures");
                File.WriteAllBytes("../outputs/weapon-display-names/captures/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active; camera.targetTexture = target; camera.rect = rect;
                canvas.renderMode = mode; canvas.worldCamera = oldCamera;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui, null);
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }
    }
}
