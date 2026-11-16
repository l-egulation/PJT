using System;
using Deviant.Control.Contracts;
using Deviant.Equipment.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // A new scene owns a new offer. Combat-only reset deliberately keeps it.
    public sealed class Chapter1StartingBlueprints : MonoBehaviour
    {
        static readonly string[] Forms = { "base_sword", "hammer", "launcher" };
        // Stable blueprint ids of data/combat/roguelite_progression.json, in the same order as Forms.
        public static readonly string[] BlueprintIds = { "sword", "blunt", "ranged" };
        static readonly string[] Names = { "검", "망치", "발사기" };
        readonly bool[] chosen = new bool[3];
        readonly Vector2[] positions = new Vector2[3];
        readonly Transform[] offers = new Transform[3];
        readonly Sprite[] icons = new Sprite[3];
        readonly int[] formIndices = new int[3];
        ForestCombatWorld world;
        bool initialized;
        // The starting room's only exit stays shut until two blueprints are equipped (user decision 2026-10-29).
        BoxCollider2D exitLock;
        Vector2 exitDoor;
        public int SelectedCount { get; private set; }
        // Raised once per weapon slot when a starting blueprint is equipped (slot, weapon instance id).
        public event Action<int, Guid> WeaponEquipped;
        // The village departure already chose both blueprints; the start room then offers nothing.
        public bool Preselected { get; private set; }
        public bool Complete => unarmedDeparture || SelectedCount == 2;
        bool unarmedDeparture;
        public bool ExitLocked => exitLock && exitLock.enabled;
        public Vector2 ExitDoor => exitDoor;
        public bool PlayerAtLockedExit => ExitLocked && world.arena.Player &&
            Mathf.Abs(world.arena.Player.Position.x - exitDoor.x) <= 4 && Mathf.Abs(world.arena.Player.Position.y - exitDoor.y) <= 8;
        public int RemainingCount => Complete ? 0 : 3 - SelectedCount;
        public bool IsOffered(int index) => initialized && index >= 0 && index < 3 && !Complete && !chosen[index];
        public Vector2 OfferPosition(int index) => positions[index];

        public void Initialize(ForestCombatWorld owner)
        {
            if (initialized) throw new InvalidOperationException("Starting blueprints already belong to a run.");
            if (!owner || !owner.Prepared || !owner.proceduralRooms || owner.Data.rooms == null || owner.Data.rooms.Length == 0 || owner.Data.rooms[0].kind != "start")
                throw new InvalidOperationException("Starting blueprints require the initialized random-stage starting room.");
            world = owner;
            for (int i = 0; i < 3; i++)
            {
                formIndices[i] = Array.FindIndex(world.arena.catalog.forms, form => form.id == Forms[i]);
                if (formIndices[i] < 0) throw new InvalidOperationException("Missing starting weapon: " + Forms[i]);
                positions[i] = world.arena.Player.spawn + Vector2.right * (3 + i * 4);
                // The start can be a ledge above the room's floor (S1-START spawn); a stand past the ledge's end rests
                // on whatever lies below, down to the starting room's floor (inner height 20).
                var floor = world.arena.FindFloor(positions[i] + Vector2.up * 2, 24);
                if (!floor.HasValue) throw new InvalidOperationException("Starting blueprint has no supporting floor.");
                positions[i].y = floor.Value;
            }
            for (int i = 0; i < 3; i++) CreateOffer(i);
            world.arena.Player.SetWeaponSlotReady(0, false);
            world.arena.Player.SetWeaponSlotReady(1, false);
            // Dummy training reuses this room as an open practice floor, so only real runs lock it.
            if (!world.dummyTrainingOnly) CreateExitLock(world.Data.rooms[0]);
            initialized = true;
        }

        void CreateExitLock(ForestRoom room)
        {
            if (room.exitSide != "right") throw new InvalidOperationException("The starting room exit lock expects a right-hand exit.");
            exitDoor = new Vector2(room.x + room.w, -(room.y + room.exitY));
            // Same wall as the boss entry: one collider spanning the doorway up to the room ceiling.
            var wall = new GameObject("Starting room exit lock"); wall.transform.SetParent(transform, false);
            wall.transform.position = new Vector3(exitDoor.x - .125f, exitDoor.y + 7.5f);
            wall.AddComponent<Deviant.Combat.ArenaSurface>(); exitLock = wall.AddComponent<BoxCollider2D>(); exitLock.size = new Vector2(.25f, 15);
            Physics2D.SyncTransforms();
        }

        void CreateOffer(int index)
        {
            var root = new GameObject("Starting blueprint · " + Forms[index]);
            root.transform.SetParent(transform, false);
            offers[index] = root.transform;
            Paper("Outline", new Color32(78, 62, 43, 255), new Vector2(1.8f, 2.2f), 15);
            Paper("Parchment", new Color32(235, 220, 181, 255), new Vector2(1.65f, 2.05f), 16);
            var sprite = world.diagnostics.GetWeaponIcon(Forms[index]);
            var sets = world.arena.characterAnimations?.sets;
            var set = sets == null ? null : Array.Find(sets, item => item != null && item.form == Forms[index]);
            var crop = set?.iconUvRect;
            if (sprite)
            {
                var rect = crop != null && crop.Length == 4
                    ? new Rect(crop[0] * sprite.texture.width, crop[1] * sprite.texture.height, crop[2] * sprite.texture.width, crop[3] * sprite.texture.height)
                    : sprite.rect;
                icons[index] = Sprite.Create(sprite.texture, rect, Vector2.one * .5f, 32, 0, SpriteMeshType.FullRect);
                var icon = new GameObject("Weapon drawing").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(root.transform, false); icon.sprite = icons[index];
                float scale = 1.25f / Mathf.Max(icon.sprite.bounds.size.x, icon.sprite.bounds.size.y);
                icon.transform.localScale = Vector3.one * scale;
                icon.sharedMaterial = world.arena.spriteMaterial; icon.sortingOrder = 17;
            }
            void Paper(string name, Color color, Vector2 size, int order)
            {
                var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false); renderer.sprite = world.arena.TechnicalSprite;
                renderer.color = color; renderer.transform.localScale = size;
                renderer.sharedMaterial = world.arena.spriteMaterial; renderer.sortingOrder = order;
            }
            AnimateOffer(index);
        }

        bool InteractionAllowed => initialized && !Complete && world && world.Ready && world.arena.Player &&
            !world.arena.Player.Dead && !world.arena.Player.InputBlocked &&
            !world.arena.Controls.Current.SimulationPaused && !world.arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0;

        bool InReach(int index)
        {
            var point = world.arena.Player.Position;
            return Mathf.Abs(point.x - positions[index].x) <= 1.7f && Mathf.Abs(point.y - positions[index].y) <= 2.25f;
        }

        public bool TryChoose(int index)
        {
            if (!InteractionAllowed || !IsOffered(index) || !InReach(index)) return false;
            var player = world.arena.Player;
            if (player.currentCast || player.Dashing) return false;
            return Equip(index);
        }

        // Equips the blueprints chosen at the village departure table, in slot order, without the start-room offers.
        public bool ApplyPreselected(System.Collections.Generic.IReadOnlyList<string> blueprintIds)
        {
            if (!initialized || SelectedCount != 0 || blueprintIds == null || blueprintIds.Count != 2) return false;
            int first = Array.IndexOf(BlueprintIds, blueprintIds[0]), second = Array.IndexOf(BlueprintIds, blueprintIds[1]);
            if (first < 0 || second < 0 || first == second) return false;
            Preselected = true;
            return Equip(first) && Equip(second);
        }

        // The new village flow grants no weapons and must not recreate the old offer/exit requirement.
        public bool ApplyUnarmedDeparture()
        {
            if (!initialized || SelectedCount != 0) return false;
            unarmedDeparture = true; Preselected = true;
            foreach (var offer in offers) if (offer) offer.gameObject.SetActive(false);
            if (exitLock) { exitLock.enabled = false; Physics2D.SyncTransforms(); }
            world.arena.Player.SetWeaponSlotReady(0, false);
            world.arena.Player.SetWeaponSlotReady(1, false);
            StartingVillageWorld.DressUnarmed(world.arena);
            return true;
        }

        bool Equip(int index)
        {
            if (!IsOffered(index)) return false;
            var player = world.arena.Player;
            int slot = SelectedCount;
            var form = world.arena.catalog.forms[formIndices[index]];
            var skill = form.specials[0];
            var configuration = new WeaponConfiguration(new WeaponSelection(formIndices[index], 0), skill.capacity, skill.cooldown);
            var result = player.weapons.Reconfigure(slot, configuration, world.arena.Now);
            if (result.Status != EquipmentCommandStatus.Succeeded) return false;
            var weapon = player.weapons[slot];
            weapon.StatSlots.Clear(); weapon.TestBasicDamageScale = weapon.TestSpecialDamageScale = weapon.TestSpeedScale = 1;
            weapon.TestCooldownSeconds = -1;
            var speed = world.TestEnvironment?.Current?.basicSpeedTrials;
            if (speed == null && world.enablePlaytestBasicSpeeds)
            {
                var settings = Resources.Load<TextAsset>("PlaytestEnvironment");
                if (settings) speed = JsonUtility.FromJson<Deviant.Combat.PlaytestEnvironment.Settings>(settings.text).basicSpeedTrials;
            }
            weapon.TestFormSpeedScale = speed == null ? 1 : Array.Find(speed, trial => trial.form == Forms[index])?.scale ?? 1;
            weapon.Cooldown.Reset(skill.capacity, skill.cooldown);
            player.CancelAction();
            player.ClearInputState();
            player.SetWeaponSlotReady(slot, true);
            // A run always starts holding weapon 1; equipping the second blueprint must not leave slot 2 active.
            player.selected = 0;
            chosen[index] = true;
            SelectedCount++;
            PresentationSfx.PlaySelected("sfx_ui_confirm");
            offers[index].gameObject.SetActive(false);
            if (Complete)
            {
                foreach (var offer in offers) if (offer) offer.gameObject.SetActive(false);
                if (exitLock) { exitLock.enabled = false; Physics2D.SyncTransforms(); }
            }
            world.arena.Record(Names[index] + " 도면 획득 / 슬롯 " + (slot + 1));
            WeaponEquipped?.Invoke(slot, weapon.InstanceId);
            return true;
        }

        public int NearestAvailableOffer => InteractionAllowed ? NearestOffer() : -1;
        int NearestOffer()
        {
            int nearest = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
            {
                if (!IsOffered(i) || !InReach(i)) continue;
                float next = Mathf.Abs(world.arena.Player.Position.x - positions[i].x);
                if (next < distance) { nearest = i; distance = next; }
            }
            return nearest;
        }
        void AnimateOffer(int index)
        {
            offers[index].position = positions[index] + Vector2.up * (1.6f + Mathf.Sin(Time.unscaledTime * 2 + index) * .12f);
        }
        void Update()
        {
            if (!initialized || Complete) return;
            for (int i = 0; i < 3; i++) if (IsOffered(i)) AnimateOffer(i);
            if (InteractionAllowed)
                Deviant.Combat.PlayerControls.TryPerform(world.arena.Player, Deviant.Input.Contracts.GameAction.Interact, () => TryChoose(NearestOffer()));
        }
        public bool ExternalPresentation { get; set; }
        // Second line of the selection banner; it names the locked exit when the player walks up to it.
        public string ExitGuide => PlayerAtLockedExit ? "도면 2개를 골라야 출구가 열립니다" :
            ExitLocked ? "서로 다른 도면 2개를 고르면 출구가 열립니다" : "서로 다른 도면 2개를 선택하세요";

        void OnGUI()
        {
            if (ExternalPresentation) return;
            if (!initialized || Complete || !world || world.FullMap && world.FullMap.IsOpen) return;
            var oldFont = GUI.skin.font; int oldDepth = GUI.depth;
            try
            {
                if (world.diagnostics.uiFont) GUI.skin.font = world.diagnostics.uiFont;
                GUI.depth = -25;
                var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                int nearest = InteractionAllowed ? NearestOffer() : -1;
                for (int i = 0; i < 3; i++)
                {
                    if (!IsOffered(i)) continue;
                    var point = Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(world.View, offers[i].position);
                    if (point.z <= 0 || point.x < -100 || point.x > Screen.width + 100 || point.y < 0 || point.y > Screen.height) continue;
                    float width = Mathf.Clamp(Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(world.View, offers[i].position + Vector3.right * 3.7f).x - point.x, 92, 160);
                    GUI.Box(new Rect(point.x - width / 2, Screen.height - point.y + 44, width, 48),
                        Names[i] + " 도면\n" + (nearest == i ? Deviant.Combat.PlayerControls.Prompt(world.arena.Player, "[{Interact}] 선택 / 슬롯 ") + (SelectedCount + 1) : "가까이 가서 선택"), style);
                }
                GUI.Box(new Rect((Screen.width - 430) / 2f, 82, 430, 52),
                    "시작 도면 선택 / " + SelectedCount + " / 2\n" + ExitGuide, style);
            }
            finally { GUI.skin.font = oldFont; GUI.depth = oldDepth; }
        }
        void OnDestroy()
        {
            foreach (var icon in icons) if (icon) Destroy(icon);
        }
    }
}
