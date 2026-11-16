using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class GroupedInkTooltipTests
    {
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        [Test] public void TooltipWrapsInsideCanvasUpdatesTextAndDoesNotInterceptPointer()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Deviant.Game.TemporaryGameGui")).First(t => t != null);
            var root = new GameObject("Tooltip test", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var gui = root.AddComponent(type);
                var stage = (RectTransform)root.transform; stage.sizeDelta = new Vector2(960,540);
                type.GetField("stage",Hidden).SetValue(gui,stage);
                var anchor = new GameObject("Icon",typeof(RectTransform)).GetComponent<RectTransform>();
                anchor.SetParent(stage,false); anchor.sizeDelta=new Vector2(24,24);anchor.anchoredPosition=new Vector2(475,-260);
                var show = type.GetMethod("ShowBuffTooltip",Hidden);
                string text = "무기 1 · 공격 슬로우 · 2칸\n이동속도 80% 감소 · 적 상태 2초\n부여 버프 6초 · 종료 후 쿨타임 1초";
                show.Invoke(gui,new object[]{anchor,text});
                var popup = (RectTransform)type.GetField("buffTooltip",Hidden).GetValue(gui);
                var label=popup.GetComponentInChildren<Text>(true);
                Assert.That(label.text,Is.EqualTo(text));
                Assert.That(label.horizontalOverflow,Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(label.color.r + label.color.g + label.color.b, Is.LessThan(1.5f), "Dark text remains readable on the light tooltip");
                Assert.That(popup.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
                var corners=new Vector3[4];popup.GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    Vector2 local=stage.InverseTransformPoint(corner);
                    Assert.That(local.x,Is.InRange(stage.rect.xMin,stage.rect.xMax));
                    Assert.That(local.y,Is.InRange(stage.rect.yMin,stage.rect.yMax));
                }
                show.Invoke(gui,new object[]{anchor,"무기 2 · 다른 효과"});
                Assert.That(label.text,Is.EqualTo("무기 2 · 다른 효과"));
                type.GetMethod("RefreshBuffTooltip",Hidden).Invoke(gui,null);
                Assert.That(popup.gameObject.activeSelf,Is.False,"No visible HUD must hide a stale tooltip");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
