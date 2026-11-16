using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class RapierThrustPlacementTests
    {
        static CharacterEffect Effect(int hit)
        {
            var catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var effect = catalog.sets.Single(s => s.form == "rapier").Effect("attack" + hit);
            effect.Load(); return effect;
        }
        static PresentationCueDefinition Definition(CharacterEffect effect, string id)
            => new PresentationCueDefinition(id, effect.frames, effect.seconds, effect.x, effect.y, effect.flipX);

        [TestCase(1, 15f, 178f, 66f)]
        [TestCase(2, -6f, 180f, 111f)]
        [TestCase(3, 0f, 198f, 93.5f)]
        public void ThrustRingFollowsTheBladeLineAndMirrorsDuringAdvance(int hit, float angle, float tipX, float tipY)
        {
            var effect = Effect(hit);
            Assert.That(effect.rotation, Is.EqualTo(angle));
            var definition = Definition(effect, "RapierPlacementTest");
            var anchor = new CueAnchor(4, 2, 1);
            ICueView view = null;
            using (var factory = new SpriteCueFactory())
            {
                factory.Publish(definition, effect.sprites, effect.scale, effect.rotation, () => anchor);
                factory.Begin(definition, new CueAnchor(0, 0, 1), r => view = r.View);
                try
                {
                    view.Sample(.08f);
                    var renderer = GameObject.Find(definition.Id).GetComponent<SpriteRenderer>();
                    var ring = renderer.transform.TransformPoint(new Vector3(202f / 64, 65f / 64, 0));
                    var tip = new Vector3(4 + (tipX - 72) / 35, 2 + (128 - tipY) / 35, 0);
                    var direction = Quaternion.Euler(0, 0, angle) * Vector3.right;
                    var delta = ring - tip;
                    Assert.That(Vector3.Cross(direction, delta).magnitude, Is.LessThan(.08f), "ring must lie on the current blade axis");
                    Assert.That(Vector3.Dot(direction, delta), Is.InRange(.15f, .35f), "ring sits just ahead of the blade tip");
                    // Publishing a future placement must leave this active view's angle unchanged.
                    factory.Publish(definition, effect.sprites, 1, 80);
                    anchor = new CueAnchor(4.65f, 3, -1); view.Sample(.10f);
                    Assert.That(renderer.transform.position.x, Is.EqualTo(anchor.X - effect.x[0]).Within(.00001f));
                    Assert.That(renderer.transform.position.y, Is.EqualTo(anchor.Y + effect.y[0]).Within(.00001f));
                    Assert.That(Mathf.DeltaAngle(renderer.transform.eulerAngles.z, -angle), Is.EqualTo(0).Within(.00001f));
                    Assert.That(renderer.transform.localScale.x, Is.EqualTo(-effect.scale));
                }
                finally { view?.Dispose(); }
            }
        }

        [Test]
        public void RenderThreeContactPosesWithTheirActualUnityEffects()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for the contact sheet");
            var nodes = new List<GameObject>(); var views = new List<ICueView>();
            var factories = new List<SpriteCueFactory>();
            RenderTexture target = null; Texture2D image = null;
            try
            {
                var cameraNode = new GameObject("Rapier placement capture"); nodes.Add(cameraNode);
                var camera = cameraNode.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 3;
                camera.transform.position = new Vector3(50007, 1.5f, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.32f, .32f, .32f); camera.cullingMask = 1 << 31;
                target = new RenderTexture(1536, 384, 24); target.Create(); camera.targetTexture = target;
                int[] poses = { 3, 11, 17 };
                for (int i = 0; i < poses.Length; i++)
                {
                    var body = new GameObject("Contact " + (i + 1)); body.layer = 31; nodes.Add(body);
                    body.transform.position = new Vector3(50000 + i * 7, 0, 0);
                    body.AddComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("CharacterBasicOutline/rapier/" + poses[i].ToString("00"));
                    var effect = Effect(i + 1); var definition = Definition(effect, "ContactEffect" + i);
                    var factory = new SpriteCueFactory(); factories.Add(factory);
                    factory.Publish(definition, effect.sprites, effect.scale, effect.rotation);
                    factory.Begin(definition, new CueAnchor(body.transform.position.x, 0, 1), r => views.Add(r.View));
                    views[views.Count - 1].Sample(.08f); GameObject.Find(definition.Id).layer = 31;
                }
                camera.Render(); var previous = RenderTexture.active;
                try { RenderTexture.active = target; image = new Texture2D(target.width, target.height); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); }
                finally { RenderTexture.active = previous; }
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/rapier-effect-alignment/unity-contact-poses.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                foreach (var view in views) view.Dispose(); foreach (var factory in factories) factory.Dispose();
                foreach (var node in nodes) Object.DestroyImmediate(node);
                if (target) { target.Release(); Object.DestroyImmediate(target); } if (image) Object.DestroyImmediate(image);
            }
        }
    }
}
