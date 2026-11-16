using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Palettes are not a reward: each weapon receives its palette at departure.
    public enum Chapter1RewardKind { InkPart, WeaponStamp, SkillStamp, Gold, ColoredInk }

    // Gold credits immediately and bursts locally; other rewards enter the stash on contact after settling.
    public sealed class Chapter1RewardPickup : MonoBehaviour
    {
        const float SettleSeconds = .35f, Reach = 1.1f, GoldFadeSeconds = 1f;
        static Sprite square;
        public Chapter1RewardKind Kind { get; private set; }
        public string BlueprintKind { get; private set; }
        // The concrete stamp this pickup grants; callers that only name a blueprint kind get one rolled here.
        public RunStamp Stamp { get; private set; }
        static readonly System.Random stampRandom = new System.Random(31337);
        public void ConfigureStamp(RunStamp stamp) { Stamp = stamp; BlueprintKind = stamp?.BlueprintKind; }
        public bool Collected { get; private set; }
        CombatArena arena;
        Camera view;
        Font font;
        RunStash stash;
        float readyAt;
        SpriteRenderer body;
        Vector2 landing;
        LootToss toss; float tossedAt;
        public LootToss Toss => toss;
        CurrencyPowderBurst powder;
        public CurrencyPowderBurst CurrencyEffect => powder;
        WeaponGrowth growth;string growthInk;
        public string ColoredInkId=>growthInk;
        string growthLabel;
        public void ConfigureGrowth(WeaponGrowth owner,string ink)
        {
            growth=owner;growthInk=ink;
            var part=owner.Data.FindPart(ink);growthLabel=part.name+" "+part.grade+"등급";
            if (body && !GroundLootArt.Apply(body, GroundLootArt.GradedInk(part.color, part.grade))
                && !GroundLootArt.Apply(body, GroundLootArt.ColoredInk(part.color)))
                body.color=part.color switch{"red"=>new Color(1,.4f,.35f),"yellow"=>new Color(1,.85f,.3f),"green"=>new Color(.35f,.85f,.55f),"blue"=>new Color(.35f,.65f,1),_=>new Color(.75f,.45f,1)};
        }
        RunSession goldRun;RunEconomy goldEconomy;
        public int GoldAmount { get; private set; }
        public void SetGold(RunSession run, int amount)
        {
            if (Collected) return;
            goldRun = run; GoldAmount = amount;
            if (Kind == Chapter1RewardKind.Gold) TryCollect();
        }
        public void SetGold(RunEconomy economy,int amount)
        {if(Collected)return;goldEconomy=economy;GoldAmount=amount;if(Kind==Chapter1RewardKind.Gold)TryCollect();}
        float GoldOpacity => Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((arena.Now - (readyAt - SettleSeconds)) / GoldFadeSeconds));

        public static string Label(Chapter1RewardKind kind, string blueprintKind) => kind switch
        {
            Chapter1RewardKind.Gold => "골드",
            Chapter1RewardKind.InkPart => "잉크 파츠",
            Chapter1RewardKind.ColoredInk => "색상 잉크",
            Chapter1RewardKind.WeaponStamp => Chapter1Rewards.BlueprintName(blueprintKind) + " 무기 스탬프",
            _ => "스킬 스탬프",
        };

        public static string StampLabel(CombatArena arena, RunStamp stamp)
        {
            if (stamp.Kind == StampKind.Skill) return Chapter1Rewards.SkillStampName(stamp.Skill) + " 스킬 스탬프";
            var form = arena && arena.catalog != null ? System.Array.Find(arena.catalog.forms, f => f.id == stamp.FormId) : null;
            return (form != null ? form.name : stamp.FormId) + " 무기 스탬프";
        }

        static Color Tint(Chapter1RewardKind kind) => kind switch
        {
            Chapter1RewardKind.Gold => new Color(1f, .78f, .22f),
            Chapter1RewardKind.InkPart => new Color(1f, .45f, .6f),
            Chapter1RewardKind.WeaponStamp => new Color(1f, .82f, .35f),
            _ => new Color(.4f, .95f, .85f),
        };

        public void Initialize(ForestCombatWorld world, RunStash runStash, Chapter1RewardKind kind, string blueprintKind, Vector2 at, Vector2? source = null)
        {
            Initialize(world.arena,world.View,world.diagnostics?world.diagnostics.uiFont:null,runStash,kind,blueprintKind,at,source);
        }
        // source: the defeated monster's feet. Items other than gold are thrown from its body and collectable once at rest.
        public void Initialize(CombatArena owner,Camera camera,Font uiFont,RunStash runStash,Chapter1RewardKind kind,string blueprintKind,Vector2 at,Vector2? source = null)
        {
            arena=owner;view=camera;font=uiFont;stash=runStash;Kind=kind;BlueprintKind=blueprintKind;
            if (kind == Chapter1RewardKind.WeaponStamp && blueprintKind != null) Stamp = Chapter1Rewards.RollWeaponStamp(blueprintKind, stampRandom);
            else if (kind == Chapter1RewardKind.SkillStamp) Stamp = Chapter1Rewards.RollSkillStamp(stampRandom);
            var floor = arena.FindFloor(at + Vector2.up * .5f, 64);
            if (floor.HasValue) at.y = floor.Value + .3f;
            landing = at; transform.position = at;
            readyAt = arena.Now + SettleSeconds;
            if (!square) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 8);
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = square; body.color = Tint(kind); body.sortingOrder = 50;
            var key = kind switch
            {
                Chapter1RewardKind.Gold => GroundLootArt.Gold,
                Chapter1RewardKind.WeaponStamp => GroundLootArt.WeaponStamp,
                Chapter1RewardKind.SkillStamp => GroundLootArt.SkillStamp,
                _ => GroundLootArt.ColoredInk("red"),
            };
            GroundLootArt.Apply(body, key);
            if (floor.HasValue)
            {
                landing.y = floor.Value + body.sprite.bounds.extents.y;
                transform.position = landing;
            }
            if (arena.spriteMaterial) body.sharedMaterial = arena.spriteMaterial;
            if (source.HasValue && kind != Chapter1RewardKind.Gold)
            {
                var settings = LootTossSettings.Current;
                tossedAt = arena.Now;
                toss = LootToss.Plan(arena, source.Value, landing, body.bounds.extents.y, LootToss.NextIndex(source.Value, tossedAt), settings);
                landing = toss.Rest; readyAt = tossedAt + toss.Duration + settings.collectDelay;
                transform.position = LootToss.Snap(toss.Launch);
            }
        }

        public bool Ready => arena && arena.Now >= readyAt;

        // Items lying on the floor no longer carry a name line; the play GUI shows HoverName in the shared speech bubble
        // over the nearest one the player walks up to (playtest 2026-11-06). Gold keeps its amount pop instead.
        static readonly System.Collections.Generic.List<Chapter1RewardPickup> live = new System.Collections.Generic.List<Chapter1RewardPickup>();
        public static System.Collections.Generic.IReadOnlyList<Chapter1RewardPickup> Live => live;
        void OnEnable() { if (!live.Contains(this)) live.Add(this); }
        void OnDisable() => live.Remove(this);
        public CombatArena Arena => arena;
        public Camera View => view;
        public string HoverName => Kind == Chapter1RewardKind.Gold || Collected ? null : growthLabel ?? (Stamp != null ? StampLabel(arena, Stamp) : Label(Kind, BlueprintKind));
        // The bubble's tail points at the top of the item's picture.
        public Vector2 HoverAnchor => body ? new Vector2(transform.position.x, body.bounds.max.y + .25f) : (Vector2)transform.position + Vector2.up * .55f;

        public bool TryCollect()
        {
            if (Collected || !arena) return false;
            if (Kind == Chapter1RewardKind.Gold)
            {
                var economy=goldEconomy??(goldRun!=null&&!goldRun.Ended?goldRun.Economy:null);if(economy==null)return false;
                // Claim before notifying balance listeners, so reentrant collection cannot pay twice.
                Collected = true;
                economy.Earn(GoldAmount);
                powder = gameObject.AddComponent<CurrencyPowderBurst>();
                powder.Initialize(arena, body, false);
                powder.Popped += PresentationSfx.PlayCurrencyPickup;
                return true;
            }
            if (!Ready || !arena.Player || arena.Player.Dead) return false;
            if(growth!=null){if(!growth.TryGrantInk(growthInk,out _,out var error)){arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>()?.Presenter.NotifyPickupFailure(error);return false;}Collected=true;Destroy(gameObject);return true;}
            switch (Kind)
            {
                case Chapter1RewardKind.InkPart: stash.AddInkParts(1); break;
                case Chapter1RewardKind.WeaponStamp:
                case Chapter1RewardKind.SkillStamp:
                    if (Stamp == null) return false;
                    stash.AddStamp(Stamp);
                    arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>()?.Presenter.NotifyAcquisition(
                        Deviant.Inventory.InventoryPresenter.AcquisitionText(StampLabel(arena, Stamp), "스탬프"));
                    break;
            }
            Collected = true;
            Destroy(gameObject);
            return true;
        }

        void Update()
        {
            if (!arena || !arena.Player || arena.Controls.Current.SimulationPaused) return;
            if (Kind == Chapter1RewardKind.Gold)
            {
                if (!Collected) TryCollect();
                if (powder && powder.Complete) Destroy(gameObject);
                return;
            }
            else if (Collected) return;
            if (toss != null) transform.position = LootToss.Snap(toss.Evaluate(arena.Now - tossedAt));
            else
            {
                float t = Mathf.Clamp01(1f - (readyAt - arena.Now) / SettleSeconds);
                transform.position = landing + Vector2.up * (4f * .8f * t * (1f - t));
            }
            if (Kind != Chapter1RewardKind.Gold && Vector2.Distance(arena.Player.Center, transform.position) <= Reach) TryCollect();
        }

        void OnGUI()
        {
            // Only the gold amount pops over its burst; every other drop is named in the hover bubble (HoverName).
            if (Kind != Chapter1RewardKind.Gold || !arena || !view || !powder || !powder.HasBurst) return;
            var point = Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(view, transform.position + Vector3.up * .55f);
            if (point.z <= 0) return;
            if (font) GUI.skin.font = font;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            var color = Tint(Kind); color.a = Mathf.Clamp01((1f - GoldOpacity) * 2);
            style.normal.textColor = color;
            GUI.Label(new Rect(point.x - 70, Screen.height - point.y - 10, 140, 20), GoldAmount + "G", style);
        }
    }
}
