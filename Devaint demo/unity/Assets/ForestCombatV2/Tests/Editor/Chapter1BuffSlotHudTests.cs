using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
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
    // 버프칸(docs/design/ui/screens.md): 캐릭터 체력 막대 아래 한 줄 6칸, 보스 체력바 아래 기절·속박 2칸.
    // 덮개는 남은 비율만큼 12시에서 반시계로 채워 시간이 흐르면 시계방향으로 지워진다.
    public sealed class Chapter1BuffSlotHudTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        sealed class Source : IBuffSlotSource
        {
            public readonly List<BuffSlot> Slots = new List<BuffSlot>();
            public void Collect(List<BuffSlot> into) => into.AddRange(Slots);
        }
        static IList Views(MonoBehaviour gui, string name) => (IList)gui.GetType().GetField(name, Hidden).GetValue(gui);
        static T Part<T>(object view, string name) => (T)view.GetType().GetField(name).GetValue(view);

        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator PlayerAndBossBuffSlotsShowInternalCooldowns()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>()); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            var player = world.arena.Player;
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            player.maxHealth = player.health = 100000; player.UseTestInput = true;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            var refresh = gui.GetType().GetMethod("Refresh");

            // 잉크를 끼지 않았으면 캐릭터칸은 없다(런 잉크 장착본이 원본).
            Assert.That(player.BuffSlotSource, Is.InstanceOf<RunInkBuffSlots>(), "chapter runs feed the slots from run inks");
            refresh.Invoke(gui, null);
            Assert.That(Views(gui, "playerBuffViews").Cast<object>().Count(v => Part<GameObject>(v, "Root").activeSelf), Is.Zero);

            // 배치 확인용 예시 장착본 11개: 한 줄 6칸 다음 줄 5칸. 일부는 준비됨, 일부는 쿨타임 중.
            var runInkSource = player.BuffSlotSource;
            float now = world.arena.Now; var source = new Source();
            var icons = new[] { BuffSlotIcon.MoveSpeed, BuffSlotIcon.Shield, BuffSlotIcon.Heal, BuffSlotIcon.Stun, BuffSlotIcon.Bind, BuffSlotIcon.Poison, BuffSlotIcon.Slow, BuffSlotIcon.BasicDamage, BuffSlotIcon.BasicSpeed, BuffSlotIcon.SkillCooldown };
            var triggers = new[] { BuffSlotTrigger.Switch, BuffSlotTrigger.Basic, BuffSlotTrigger.Dash, BuffSlotTrigger.Skill, BuffSlotTrigger.Basic, BuffSlotTrigger.Dash, BuffSlotTrigger.Skill, BuffSlotTrigger.Status, BuffSlotTrigger.Basic, BuffSlotTrigger.Status };
            var left = new[] { 0f, 5.5f, 2f, 6f, 0f, 3f, 1.2f, 4.5f, 0f, 2.5f };
            for (int i = 0; i < icons.Length; i++) source.Slots.Add(new BuffSlot(icons[i], triggers[i], now + left[i], 6));
            source.Slots.Add(new BuffSlot(BuffSlotIcon.SkillDamage, BuffSlotTrigger.Skill, now + 1, 4));
            player.BuffSlotSource = source;
            refresh.Invoke(gui, null);
            var views = Views(gui, "playerBuffViews").Cast<object>().ToArray();
            Assert.That(views.Count(v => Part<GameObject>(v, "Root").activeSelf), Is.EqualTo(11));
            var first = (RectTransform)Part<GameObject>(views[0], "Root").transform; var seventh = (RectTransform)Part<GameObject>(views[6], "Root").transform;
            Assert.That(seventh.anchoredPosition.x, Is.EqualTo(first.anchoredPosition.x), "6칸마다 다음 줄");
            // 정수배: 칸은 아이콘 원본과 같은 24이고 캔버스와 같은 배율(추가 축소 없음), 좌표는 정수.
            var canvasRoot = gui.GetComponent<Canvas>().transform;
            Assert.That(first.sizeDelta, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(first.lossyScale.x, Is.EqualTo(canvasRoot.lossyScale.x).Within(1e-5f), "버프칸은 캔버스 배율 그대로");
            Assert.That(first.lossyScale.y, Is.EqualTo(canvasRoot.lossyScale.y).Within(1e-5f));
            foreach (var v in views.Take(11)) { var p = ((RectTransform)Part<GameObject>(v, "Root").transform).anchoredPosition; Assert.That(p.x % 1 == 0 && p.y % 1 == 0, Is.True, "정수 좌표 " + p); }
            Assert.That(seventh.anchoredPosition.y, Is.LessThan(first.anchoredPosition.y));
            var shade = Part<Image>(views[1], "Shade");
            Assert.IsTrue(shade.enabled); Assert.That(shade.fillAmount, Is.EqualTo(5.5f / 6).Within(.02f));
            Assert.That(shade.fillMethod, Is.EqualTo(Image.FillMethod.Radial360)); Assert.IsFalse(shade.fillClockwise, "지워지는 쪽이 시계방향");
            Assert.That(shade.fillOrigin, Is.EqualTo((int)Image.Origin360.Top));
            Assert.AreEqual("6", Part<Text>(views[1], "Seconds").text);
            Assert.IsFalse(Part<Image>(views[0], "Shade").enabled, "준비된 칸은 밝다"); Assert.AreEqual("", Part<Text>(views[0], "Seconds").text);
            Assert.IsTrue(Part<Image>(views[0], "Badge").gameObject.activeSelf, "장착본 칸은 발동 조건 배지를 붙인다");

            // 지속 중인 칸: 쿨타임 덮개·초 대신 지속 연출(사용자 선택 D, BuffSlotGlow)이 아이콘 위에 겹친다.
            float activeAt = world.arena.Now;
            source.Slots[0] = new BuffSlot(BuffSlotIcon.MoveSpeed, BuffSlotTrigger.Switch, activeAt + 5, 6, null, activeAt + 2, 3);
            refresh.Invoke(gui, null);
            var fx = Part<RawImage>(views[0], "Fx");
            Assert.IsFalse(Part<Image>(views[0], "Shade").enabled, "지속 중에는 쿨타임 덮개를 숨긴다");
            Assert.AreEqual("", Part<Text>(views[0], "Seconds").text);
            Assert.IsTrue(fx.enabled && fx.texture, "지속 연출 층이 보인다");
            Assert.That(((RectTransform)fx.transform).sizeDelta, Is.EqualTo(new Vector2(44, 44)), "아이콘보다 사방 10 큰 연출 층");
            var glow = Part<Deviant.Presentation.Unity.BuffSlotGlow>(views[0], "Glow");
            Assert.That(glow.TriggeredAt, Is.EqualTo(activeAt).Within(1e-4f), "준비 칸이 지속을 시작하면 발동 연출");
            Assert.That(glow.Left, Is.EqualTo(2f / 3).Within(.01f));

            // 보스칸의 덮개 검증용으로 12/8초를 주입한다. 실제 게임의 보스 재부여 제한은 0초다.
            var boss = world.arena.enemies.First(e => e && !e.Dead);
            boss.isBoss = true; boss.BossControl.Configure(12, 8); boss.BossControl.Reset(); boss.maxHealth = boss.health = 100000;
            var control = new HitData { root = .6f, stun = .4f };
            float t0 = world.arena.Now;
            Assert.IsTrue(boss.Receive(1, player, 1, false, control));
            float stunEnd = boss.stunUntil, rootEnd = boss.rootUntil;
            Assert.That(stunEnd, Is.EqualTo(t0 + .4f).Within(.05f)); Assert.That(rootEnd, Is.EqualTo(t0 + .6f).Within(.05f));
            while (world.arena.Now < t0 + 1) yield return null;
            Assert.IsTrue(boss.Receive(1, player, 1, false, control), "피해는 받는다");
            Assert.That(boss.stunUntil, Is.EqualTo(stunEnd), "기절 쿨타임 중에는 부여 실패");
            Assert.That(boss.rootUntil, Is.EqualTo(rootEnd), "속박 쿨타임 중에는 부여 실패");
            while (world.arena.Now < t0 + 3) yield return null;
            var bossHud = (GameObject)gui.GetType().GetField("bossHud", Hidden).GetValue(gui);
            // 런 잉크 장착 동기화가 원본을 다시 걸 수 있으므로 캡처 직전에 예시 원본을 다시 넣는다.
            player.BuffSlotSource = source;
            refresh.Invoke(gui, null);
            bossHud.SetActive(true);
            gui.GetType().GetMethod("RefreshBuffSlots", Hidden).Invoke(gui, new object[] { player, boss, true });
            var bossViews = Views(gui, "bossBuffViews").Cast<object>().ToArray();
            Assert.That(bossViews.Count(v => Part<GameObject>(v, "Root").activeSelf), Is.EqualTo(2));
            float stunLeft = boss.BossControl.StunRemaining(world.arena.Now), rootLeft = boss.BossControl.RootRemaining(world.arena.Now);
            Assert.That(stunLeft, Is.EqualTo(9).Within(.2f)); Assert.That(rootLeft, Is.EqualTo(5).Within(.2f));
            Assert.That(Part<Image>(bossViews[0], "Shade").fillAmount, Is.EqualTo(stunLeft / 12).Within(.02f));
            Assert.That(Part<Image>(bossViews[1], "Shade").fillAmount, Is.EqualTo(rootLeft / 8).Within(.02f));
            Assert.IsFalse(Part<Image>(bossViews[0], "Badge").gameObject.activeSelf, "보스칸에는 발동 조건 배지가 없다");
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "buff-slots", 1920, 1080);

            // 8초가 지나면 속박만 다시 걸린다. 기절은 12초까지 실패한다.
            while (world.arena.Now < t0 + 8.1f) yield return null;
            boss.Receive(1, player, 1, false, control);
            Assert.That(boss.rootUntil, Is.GreaterThan(t0 + 8), "속박은 8초 뒤 다시 걸린다");
            Assert.That(boss.stunUntil, Is.EqualTo(stunEnd), "기절은 아직 쿨타임");
            while (world.arena.Now < t0 + 12.1f) yield return null;
            boss.Receive(1, player, 1, false, control);
            Assert.That(boss.stunUntil, Is.GreaterThan(t0 + 12), "기절은 12초 뒤 다시 걸린다");
            player.BuffSlotSource = runInkSource; boss.isBoss = false;
        }
    }
}
