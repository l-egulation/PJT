using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab.Tests
{
    public sealed class HangingConnectorIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Connector integration fixture").AddComponent<LightingLabComposition>();
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void AssertSockets(HangingConnectorView view)
        {
            view.SampleNow();
            Assert.That(Vector3.Distance(view.GetCurveWorldPoint(0), view.StartSocket.position), Is.LessThan(1e-5));
            Assert.That(Vector3.Distance(view.GetCurveWorldPoint(view.CurvePointCount - 1), view.EndSocket.position), Is.LessThan(1e-5));
        }
        [UnityTest] public IEnumerator ChainAndRopeReuseRenderersAndLeaveGameplayAndSocketsUntouched()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Suspension;
            Assert.That(view.Style, Is.EqualTo(HangingConnectorStyle.Chain)); Assert.That(view.AllocatedPieceCount, Is.GreaterThan(3));
            Assert.That(lab.Lantern.transform.Find("Swing pivot/Chain"), Is.Null, "The old solid bar must be removed.");
            int renderers = view.GetComponentsInChildren<Renderer>().Length, meshes = view.GetComponentsInChildren<MeshFilter>().Length;
            float health = lab.Arena.Player.health; var position = lab.Arena.Player.Position; float angle = lab.Lantern.State.Angle;
            for (int i = 0; i < 20; i++) { view.SetStyle(i % 2 == 0 ? HangingConnectorStyle.Rope : HangingConnectorStyle.Chain); AssertSockets(view); }
            Assert.That(view.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(renderers));
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(meshes));
            Assert.That(lab.Arena.Player.health, Is.EqualTo(health)); Assert.That(lab.Arena.Player.Position, Is.EqualTo(position)); Assert.That(lab.Lantern.State.Angle, Is.EqualTo(angle));
            Assert.That(view.GetComponentsInChildren<Collider2D>(), Is.Empty); Assert.That(view.GetComponentsInChildren<Rigidbody2D>(), Is.Empty);
            yield return null;
        }
        [UnityTest] public IEnumerator ActualAttackBendsTheConnectionWhilePauseAndResetKeepItInTheCombatClock()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Suspension;
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                for (int i = 0; i < 15; i++) driver.Step(.02f);
                Assert.That(lab.Lantern.State.AcceptedAttacks, Is.EqualTo(1)); Assert.That(Mathf.Abs(view.Bend), Is.GreaterThan(.001f)); AssertSockets(view);
                float bend = view.Bend, angle = lab.Lantern.State.Angle;
                lab.SetPaused(true); yield return null; yield return null; lab.SamplePresentation();
                Assert.That(view.Bend, Is.EqualTo(bend)); Assert.That(lab.Lantern.State.Angle, Is.EqualTo(angle)); AssertSockets(view);
                lab.SetPaused(false); driver.Reset(); Assert.That(view.Bend, Is.Zero); AssertSockets(view);
                Vector3 original = view.EndSocket.position; view.EndSocket.position = view.StartSocket.position;
                view.SampleNow(); Assert.That(view.GetCurveWorldPoint(view.CurvePointCount - 1), Is.EqualTo(view.StartSocket.position));
                view.EndSocket.position = original; AssertSockets(view);
            }
        }
        [UnityTest] public IEnumerator AuthoredSpritePiecesUseTheirSocketsRatherThanAssumingACenteredPivot()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Suspension;
            var texture = new Texture2D(32, 32); var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.2f, .8f), 32);
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var art = ScriptableObject.CreateInstance<HangingConnectorArt>();
            var piece = new ConnectorSpritePiece { sprite = sprite, topSocket = new Vector2(-.1f, .15f), bottomSocket = new Vector2(.1f, -.45f) };
            art.chainFace = art.chainEdge = art.rope = piece; art.spriteMaterial = material;
            try
            {
                view.SetArt(art); view.SetStyle(HangingConnectorStyle.Chain); AssertSockets(view);
                var first = view.GetPieceSprite(0); var last = view.GetPieceSprite(view.AllocatedPieceCount - 1);
                Assert.That(first.enabled && last.enabled, Is.True);
                Assert.That(Vector3.Distance(first.transform.TransformPoint(piece.topSocket), view.StartSocket.position), Is.LessThan(1e-5));
                Assert.That(Vector3.Distance(last.transform.TransformPoint(piece.bottomSocket), view.EndSocket.position), Is.LessThan(1e-5));
                view.SetStyle(HangingConnectorStyle.Rope); AssertSockets(view);
                Assert.That(Vector3.Distance(first.transform.TransformPoint(piece.topSocket), view.StartSocket.position), Is.LessThan(1e-5));
            }
            finally { view.SetArt(null); Object.Destroy(art); Object.Destroy(material); Object.Destroy(sprite); Object.Destroy(texture); }
            yield return null;
        }
    }
}
