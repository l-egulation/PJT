using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Deviant.Playtest
{
    [DefaultExecutionOrder(900)]
    public sealed partial class GameSandbox : MonoBehaviour
    {
        public const string ScenePath = "Assets/Deviant/Playtest/Scenes/GameSandbox.unity";
        public static GameSandbox Instance { get; private set; }
        public CombatArena Arena { get; private set; }
        public bool Loading { get; private set; }
        public bool IsOpen => scope != null;
        public int Chapter { get; private set; } = 1;
        public string RoomId { get; private set; }
        public int BossPhase { get; private set; }
        public string Message { get; private set; } = "본게임 런 잉크 / 획득/골드 제한 없이 설정";
        public WeaponGrowth Growth => Arena ? Arena.MvpGrowth : null;
        public SketchEquipment Equipment => Arena ? Arena.Sketch : null;
        public RunInkCatalog InkCatalog => Equipment?.Catalog ?? CombatArena.LoadRunInkCatalog();
        public sealed class WeaponSetup
        {
            public string Form = "base_sword";
            public int Special;
            public string[] Inks = new string[3];
            public string[] Colors = Enumerable.Repeat("neutral", 3).ToArray();
            public string Basic = "1", Skill = "1", Speed = "1", Cooldown = "-1";
        }
        public readonly WeaponSetup[] Weapons = { new WeaponSetup(), new WeaponSetup { Form = "hammer" } };
        int attackLevel;
        float maxHealth = 100;
        readonly int[] seeds = { 0, 3131, 7272 };
        ForestCombatWorld forest;
        Chapter2World waterway;
        IControlScope scope;
        IDisposable profileScope;
        bool previousTestConfiguration;
        bool openAfterLoad = true;
        bool transitioning;

        public static GameSandbox Begin()
        {
            if (!DevFeatures.Enabled) return null;
            if (Instance) return Instance;
            var go = new GameObject("Game sandbox / session only");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameSandbox>();
            Instance.profileScope = Chapter1ProfileHost.UseTemporaryStore(new MemoryProfileStore());
            Instance.Travel(1, "start");
            return Instance;
        }

        public bool Travel(int chapter, string destination, int bossPhase = 0)
        {
            if (!DevFeatures.Enabled || Loading || chapter < 1 || chapter > 2) return false;
            if (bossPhase < 0 || bossPhase > 3 || bossPhase > 0 && (chapter != 2 || destination != "boss")) return false;
            if (chapter == Chapter && Arena) Capture();
            // Always rebuild the encounter: boss walls, phases, death/result scopes and projectiles cannot leak.
            Loading = true;
            StartCoroutine(LoadRoom(chapter, destination, bossPhase));
            return true;
        }

        IEnumerator LoadRoom(int chapter, string destination, int bossPhase)
        {
            Capture(); SetOpen(false); transitioning = true;
            Arena = null; forest = null; waterway = null;
            Chapter = chapter; RoomId = null; BossPhase = bossPhase;
            var host = Chapter1RunHost.Ensure();
            // A direct development run has no departure profile, so it never awards permanent progression.
            var run = host.Flow.StartNewRun(Chapter1RunHost.Now, seeds[chapter]);
            run.Growth = WeaponGrowth.CreateSandbox(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text), run.Economy);
            if (chapter == 2) { run.MarkBossDefeated(); host.Flow.TryAdvanceChapter(Chapter1RunHost.Now); }
            yield return SceneManager.LoadSceneAsync(chapter == 1 ? Chapter1RunHost.ChapterScene : Chapter2EntryBridge.ScenePath);
            float deadline = Time.realtimeSinceStartup + 40;
            while (!Arena && Time.realtimeSinceStartup < deadline)
            {
                forest = FindAnyObjectByType<ForestCombatWorld>();
                waterway = FindAnyObjectByType<Chapter2World>();
                if (chapter == 1 && forest && forest.Ready) Arena = forest.arena;
                if (chapter == 2 && waterway && waterway.Ready) Arena = waterway.arena;
                if (!Arena) yield return null;
            }
            transitioning = false;
            if (!Arena) { Loading = false; Message = "챕터 준비 실패. Unity Console을 확인하세요."; yield break; }
            Arena.MvpGrowth = run.Growth;
            Arena.EnableSketchEquipment(seeds[chapter], chapter);
            run.Sketch = Equipment.State;
            Arena.SketchEquipment = true;
            if (forest)
            {
                forest.ExternalAreaNavigation = true;
                forest.GetComponent<Chapter1StartingBlueprints>()?.ApplyUnarmedDeparture();
            }
            var keyboardInput = Arena.Player.GetComponent<CombatKeyboardInput>();
            if (keyboardInput) keyboardInput.ResetKeyEnabled = false;
            SetOpen(true);
            ApplyAll();
            if (!PlaceInRoom(destination)) Message = "이동할 방을 찾지 못했습니다: " + destination;
            else
            {
                if (BossPhase > 0)
                {
                    waterway.Boss.StartTestPhase(BossPhase);
                    Arena.Player.spawn = Arena.Player.Position;
                }
                Message = BossPhase > 0
                    ? $"2챕터 보스 / {BossPhase}페이즈 / 장비 설정 유지"
                    : $"{Chapter}챕터 / {RoomId} / 새 전투 준비됨";
            }
            Loading = false;
            if (!openAfterLoad) SetOpen(false);
        }

        void Capture()
        {
            if (!Arena || !Arena.Player || Growth == null) return;
            for (int i = 0; i < 2; i++)
            {
                var weapon = Arena.Player.weapons[i]; var setup = Weapons[i];
                setup.Form = Arena.catalog.forms[weapon.FormIndex].id; setup.Special = weapon.SpecialIndex;
                setup.Basic = Number(weapon.TestBasicDamageScale); setup.Skill = Number(weapon.TestSpecialDamageScale);
                setup.Speed = Number(weapon.TestSpeedScale); setup.Cooldown = Number(weapon.TestCooldownSeconds);
                var palette = Equipment?.Equipped(i);
                if (palette != null) { setup.Inks = palette.Slots.Select(s => s.Ink?.id).ToArray(); setup.Colors = palette.Slots.Select(s => s.Color).ToArray(); }
            }
            attackLevel = Growth.AttackLevel; maxHealth = Arena.Player.maxHealth;
        }

        void ApplyAll()
        {
            for (int i = 0; i < 2; i++)
            {
                var setup = Weapons[i];
                ConfigureWeapon(i, setup.Form, setup.Special);
                Arena.ApplyTestWeaponTuning(i, setup.Basic, setup.Skill, setup.Speed, setup.Cooldown, out _);
            }
            Growth.SetSandboxAttackLevel(attackLevel);
            Arena.Player.maxHealth = maxHealth; Arena.Player.health = maxHealth;
            hpText = Number(maxHealth); RefreshTuningText();
        }

        public bool ConfigureWeapon(int slot, string formId, int special)
        {
            if (!IsOpen || !Arena || Growth?.IsSandbox != true || Equipment == null || slot < 0 || slot > 1) return false;
            try
            {
                var weapon = Equipment.DebugConfigureWeapon(slot, formId == "unarmed" ? null : formId, special);
                Arena.Player.CancelAction(); Arena.Player.ClearInputState(); Arena.SyncSketchLoadout();
                Weapons[slot].Form = formId; Weapons[slot].Special = special;
                if (weapon != null && !ConfigurePalette(slot, Weapons[slot].Inks, Weapons[slot].Colors)) return false;
                var setup = Weapons[slot];
                Arena.ApplyTestWeaponTuning(slot, setup.Basic, setup.Skill, setup.Speed, setup.Cooldown, out _);
                Message = "무기·스킬 적용됨"; return true;
            }
            catch (InvalidOperationException e) { Message = e.Message; return false; }
        }

        public bool ConfigurePalette(int slot, string[] ids, string[] colors)
        {
            if (!IsOpen || !Arena || Growth?.IsSandbox != true || Equipment == null || slot < 0 || slot > 1) return false;
            if (ids == null || colors == null || ids.Length < 1 || ids.Length > 6 || colors.Length != ids.Length ||
                ids.Any(id => id != null && InkCatalog.Find(id) == null) ||
                colors.Any(color => color != "neutral" && !InkCatalog.inks.Any(ink => ink.color == color)))
            { Message = "현재 런 잉크와 팔레트 색상으로 1~6칸을 설정하세요."; return false; }
            try
            {
                var weapon = Equipment.Equipped(slot);
                if (weapon == null) { Message = "무기를 먼저 선택하세요."; return false; }
                // Validate the whole request before clearing any removed cells.
                for (int i = ids.Length; i < weapon.Slots.Count; i++) Equipment.DebugSetInk(weapon.Id, i, null);
                Equipment.DebugResizePalette(weapon.Id, ids.Length);
                for (int i = 0; i < ids.Length; i++)
                { Equipment.DebugSetSlotColor(weapon.Id, i, colors[i]); Equipment.DebugSetInk(weapon.Id, i, ids[i]); }
                Arena.Player.CancelAction(); Arena.Player.ClearInputState(); Arena.SyncSketchLoadout();
                Weapons[slot].Inks = (string[])ids.Clone(); Weapons[slot].Colors = (string[])colors.Clone();
                Message = "런 잉크·팔레트 색상 적용됨"; return true;
            }
            catch (InvalidOperationException e) { Message = e.Message; return false; }
        }

        public bool SetAttackLevel(int level)
        {
            if (!IsOpen || Growth == null) return false;
            try { Growth.SetSandboxAttackLevel(level); attackLevel = level; return true; }
            catch (InvalidOperationException e) { Message = e.Message; return false; }
        }

        public bool SetMaxHealth(string value)
        {
            if (!IsOpen || !Arena || !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float hp) || float.IsNaN(hp) || float.IsInfinity(hp) || hp < 1 || hp > 1000000)
            { Message = "최대 HP는 1~1,000,000 숫자를 입력하세요."; return false; }
            maxHealth = hp; Arena.Player.maxHealth = hp; Arena.Player.health = hp;
            Message = "최대 HP 적용 / 체력 회복"; return true;
        }

        public bool ApplyTuning(int slot, string basic, string skill, string speed, string cooldown)
        {
            if (!IsOpen || !Arena) return false;
            bool ok = Arena.ApplyTestWeaponTuning(slot, basic, skill, speed, cooldown, out var error);
            Message = ok ? "무기 수치 적용됨" : error;
            if (ok) Capture();
            return ok;
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            if (open)
            {
                if (!Arena) return;
                if (!Loading) Capture();
                RefreshTuningText(); hpText = Number(maxHealth);
                previousTestConfiguration = Arena.TestConfigurationOpen;
                scope = Arena.Controls.OpenScope("game sandbox");
                scope.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
                scope.PauseSimulation(); Arena.TestConfigurationOpen = true;
                Arena.Player.CancelAction(); Arena.Player.ClearInputState();
                SetPointerBlocker(true);
            }
            else
            {
                scope?.Dispose(); scope = null;
                if (Arena) { Arena.TestConfigurationOpen = previousTestConfiguration; Arena.Player.ClearInputState(); }
                SetPointerBlocker(false);
            }
        }

        public sealed class Destination
        {
            public string Id, Kind, Name;
            public Destination(string id, string kind, string name) { Id = id; Kind = kind; Name = name; }
        }
        public Destination[] Rooms => forest && forest.Ready ? forest.Data.rooms.Select(r => new Destination(r.id.ToString(), r.kind, r.name)).ToArray() :
            waterway && waterway.Ready ? waterway.Layout.rooms.Select(r => new Destination(r.id, r.kind, r.prototypeId)).ToArray() : Array.Empty<Destination>();

        bool PlaceInRoom(string destination)
        {
            var choices = Rooms;
            var room = choices.FirstOrDefault(r => r.Id == destination) ?? choices.FirstOrDefault(r => r.Kind == destination || destination == "maintenance" && (r.Kind == "facility" || r.Kind == "shop"));
            if (room == null) return false;
            if (waterway)
            {
                var source = waterway.Layout.Room(room.Id);
                var preferred = new Vector2(source.x + (room.Kind == "boss" ? 3 : source.w / 2), source.y + source.h - 2);
                var landing = FindSafeLanding(source.Bounds, preferred);
                if (!landing.HasValue) return false;
                waterway.WarpToRoom(room.Id, landing.Value);
            }
            else
            {
                var source = forest.Data.rooms.First(r => r.id.ToString() == room.Id);
                // Enter just inside the authored entry, at its actual supporting floor.
                float x = source.x + (source.entrySide == "right" ? source.w - 3 : 3);
                var at = new Vector2(x, -(source.y + source.entryY));
                var landing = FindSafeLanding(new Rect(source.x, -(source.y + source.h), source.w, source.h), at);
                if (!landing.HasValue) return false;
                Arena.Player.Position = landing.Value; Arena.Player.velocity = Vector2.zero;
                Arena.Player.jumps.Reset(); Physics2D.SyncTransforms(); Arena.Player.grounded = Arena.Player.ProbeGround();
                forest.GetComponent<Chapter1BossRoomEnclosure>()?.Hold(); forest.RefreshCamera(0, true);
            }
            RoomId = room.Id;
            Arena.Player.spawn = Arena.Player.Position;
            return true;
        }

        Vector2? FindSafeLanding(Rect bounds, Vector2 preferred)
        {
            var player = Arena.Player;
            Vector2? best = null; float score = float.PositiveInfinity;
            // A point ray at the room centre can hit the inside of a tall column. Search full-body
            // clearance and floor support on both sides, across the authored room's actual geometry.
            for (float x = bounds.xMin + 1; x <= bounds.xMax - 1; x += .5f)
                for (float y = bounds.yMax - 2; y > bounds.yMin; y -= 2)
                {
                    float? floor = Arena.FindFloor(new Vector2(x, y), bounds.height + 2);
                    if (!floor.HasValue) continue;
                    var feet = new Vector2(x, floor.Value + .025f);
                    var center = feet + new Vector2(player.offset.x * player.facing, player.offset.y);
                    if (feet.y < bounds.yMin - .01f || !bounds.Contains(feet + Vector2.up * .25f) || center.y + player.size.y / 2 > bounds.yMax - .05f) continue;
                    if (Arena.SolidOverlap(center, player.size - Vector2.one * .04f)) continue;
                    bool supported = true;
                    foreach (float side in new[] { -.4f, .4f })
                    {
                        var support = Arena.FindFloor(new Vector2(center.x + player.size.x * side, feet.y + .2f), .5f);
                        if (!support.HasValue || Mathf.Abs(support.Value - floor.Value) > .06f) { supported = false; break; }
                    }
                    if (!supported) continue;
                    float distance = (feet - preferred).sqrMagnitude;
                    if (distance < score) { score = distance; best = feet; }
                }
            return best;
        }

        public bool TravelToBossPhase(int phase)
        {
            if (Loading || phase < 1 || phase > 3) return false;
            openAfterLoad = false;
            return Travel(2, "boss", phase);
        }

        public void RestartRoom()
        {
            openAfterLoad = false;
            Travel(Chapter, BossPhase > 0 ? "boss" : RoomId ?? "start", BossPhase);
        }
        void Update()
        {
            if (transitioning || Loading) return;
            if (!Arena) { Destroy(gameObject); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) SetOpen(!IsOpen);
            else if (keyboard != null && keyboard.f5Key.wasPressedThisFrame && !IsOpen) RestartRoom();
            if (!IsOpen && Arena)
            {
                if (waterway && !string.IsNullOrEmpty(waterway.CurrentRoom)) RoomId = waterway.CurrentRoom;
                else if (forest)
                {
                    var position = Arena.Player.Position;
                    var room = Array.Find(forest.Data.rooms, r => new Rect(r.x, -(r.y + r.h), r.w, r.h).Contains(position + Vector2.up * .25f));
                    if (room != null) RoomId = room.id.ToString();
                }
            }
        }
        void OnEnable() => SceneManager.sceneLoaded += PrepareScene;
        void PrepareScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != Chapter2EntryBridge.ScenePath) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var world in root.GetComponentsInChildren<Chapter2World>(true)) world.seed = seeds[2];
        }
        void OnDisable() { SceneManager.sceneLoaded -= PrepareScene; SetOpen(false); }
        void OnDestroy() { SetOpen(false); profileScope?.Dispose(); profileScope = null; if (Instance == this) Instance = null; if (font) Destroy(font); }
        static string Number(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
