using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedSpeechTextureTests
    {
        [Test] public void ImguiColorConversionKeepsTheSharedSpriteAndItsAlphaUnchanged()
        {
            var type=Type.GetType("Deviant.UI.Theme.ApprovedTheme, Deviant.UI.Theme",true);
            var frame=(Sprite)type.GetProperty("FrameSpeech").GetValue(null);
            var source=frame.texture;var before=source.GetPixel(1000,400);
            var property=type.GetProperty("SpeechGuiTexture",BindingFlags.Static|BindingFlags.NonPublic);
            var rendered=(Texture2D)property.GetValue(null);
            Assert.That(property.GetValue(null),Is.SameAs(rendered),"the frame copy is cached rather than allocated on every repaint");
            Assert.That(((Sprite)type.GetProperty("FrameSpeech").GetValue(null)).texture,Is.SameAs(source));
            Assert.That(source.GetPixel(1000,400),Is.EqualTo(before),"uGUI keeps the unmodified imported color");
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)
            {
                Assert.That(rendered,Is.Not.SameAs(source));
                foreach(var point in new[]{new Vector2Int(1000,400),new Vector2Int(220,400),new Vector2Int(5,5)})
                {
                    var original=source.GetPixel(point.x,point.y);var actual=rendered.GetPixel(point.x,point.y);var expected=original.linear;
                    Assert.That(actual.r,Is.EqualTo(expected.r).Within(1f/255));Assert.That(actual.g,Is.EqualTo(expected.g).Within(1f/255));Assert.That(actual.b,Is.EqualTo(expected.b).Within(1f/255));
                    Assert.That(actual.a,Is.EqualTo(original.a).Within(1f/255),"the cutout alpha must survive color conversion");
                }
            }
            else Assert.That(rendered,Is.SameAs(source),"Gamma projects use the original texture directly");
        }
    }
}
