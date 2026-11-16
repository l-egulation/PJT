using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class WorldNoticeReadabilityTests
    {
        [Test] public void DescriptionKeepsDarkBackingBeforeTheBottomFade()
        {
            var type=typeof(Chapter1SealChallenge).Assembly.GetType("ForestCombatV2.ApprovedWorldNotice");
            var shade=(Texture2D)type.GetProperty("Shade",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            // Completion copy and the lower boss notice both need the dark backing beneath the full text line.
            foreach(float offset in new[]{0f,50f})
            {
                float descriptionCentre=84+offset+20,shadeHeight=166+offset;
                Assert.That(shade.GetPixelBilinear(.5f,1-descriptionCentre/shadeHeight).a,Is.GreaterThanOrEqualTo(.9f),
                    "The description must not sit in the weak part of the top gradient");
            }
            Assert.That(shade.GetPixel(0,0).a,Is.LessThan(.01f),"the bottom edge still fades naturally into the world");
        }
    }
}
