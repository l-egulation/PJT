using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Kill rewards of the chapter 1 play build: gold for every kill, and stamps and colored inks only
    // from elites and bosses. Gold credits immediately while its floor visual bursts into local bouncing powder.
    // Values come from data/combat/chapter1_economy.json.
    public sealed class Chapter1Rewards : MonoBehaviour
    {
        ForestCombatWorld world;
        Chapter1RunBinding binding;
        System.Random random;
        readonly List<Chapter1RewardPickup> pickups = new List<Chapter1RewardPickup>();
        public IReadOnlyList<Chapter1RewardPickup> Pickups { get { pickups.RemoveAll(p => !p); return pickups; } }
        public RunSession Run => binding ? binding.Run : null;

        // Weapon kind of every approved form (docs/design/equipment/rules.md).
        public static string BlueprintKindOf(string formId) => formId switch
        {
            "base_sword" or "whip" or "rapier" or "dagger" => "sword",
            "hammer" or "shield" or "greatsword" => "blunt",
            "launcher" or "shuriken" or "bomb" => "ranged",
            _ => formId,
        };

        public static string BlueprintName(string kind) => kind switch
        {
            "sword" => "검",
            "blunt" => "둔기",
            "ranged" => "원거리",
            _ => kind,
        };

        // Weapon stamps turn a blueprint into one of its kind's other forms; the base form is the unstamped blueprint.
        public static string[] StampForms(string kind) => kind switch
        {
            "sword" => new[] { "whip", "rapier", "dagger" },
            "blunt" => new[] { "shield", "greatsword" },
            "ranged" => new[] { "shuriken", "bomb" },
            _ => System.Array.Empty<string>(),
        };

        // Skill stamps choose the special variant: 1 range, 2 movement, 3 follow-up (special attack ink order).
        public static string SkillStampName(int skill) => skill switch { 1 => "범위", 2 => "이동", _ => "후속타" };

        public static RunStamp RollWeaponStamp(string kind, System.Random random)
        {
            var forms = StampForms(kind);
            return forms.Length == 0 ? null : RunStamp.Weapon(kind, forms[random.Next(forms.Length)]);
        }

        public static RunStamp RollSkillStamp(System.Random random) => RunStamp.SkillStamp(1 + random.Next(3));

        // A guaranteed stamp never disappears: a weapon stamp needs a carried blueprint, otherwise it becomes a skill stamp.
        public static RunStamp RollStageStamp(bool weapon, IList<string> blueprintKinds, System.Random random)
        {
            if (weapon && blueprintKinds != null && blueprintKinds.Count > 0)
            {
                var stamp = RollWeaponStamp(blueprintKinds[random.Next(blueprintKinds.Count)], random);
                if (stamp != null) return stamp;
            }
            return RollSkillStamp(random);
        }

        public void Initialize(ForestCombatWorld owner, Chapter1RunBinding runBinding)
        {
            world = owner; binding = runBinding;
            random = new System.Random(unchecked(binding.Run.Seed * 7919 + 17));
            binding.EnemyDefeated += Defeated;
        }

        void OnDestroy() { if (binding) binding.EnemyDefeated -= Defeated; }

        InventoryRuntimeHost Bag => world && world.arena ? world.arena.GetComponent<InventoryRuntimeHost>() : null;
        bool fixedDropsSuppressed;

        // Chapter 1 inks come only from this reward table, so the bag's per-enemy fixed tables stay empty here.
        void Update()
        {
            if (fixedDropsSuppressed || !world || !world.Ready || !Bag) return;
            foreach (var enemy in world.arena.enemies)
                if (enemy && !enemy.GetComponent<InkDropSource>()) enemy.gameObject.AddComponent<InkDropSource>().tableKey = "";
            fixedDropsSuppressed = true;
        }

        // The maintenance draw uses its own exclusion without changing monster rewards.
        public string RandomInk(bool maintenanceDraw = false)
        {
            var pool = InkPool(maintenanceDraw: maintenanceDraw);
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        // Every stat and special ink, plus form inks of the chosen blueprint kinds, with equal chance.
        public List<string> InkPool(bool specialOnly = false, bool maintenanceDraw = false)
        {
            var pool = new List<string>();
            var bag = Bag;
            if (!bag) return pool;
            var kinds = ChosenBlueprintKinds();
            foreach (var entry in bag.Content.items)
            {
                if (entry.kind == InkKind.Form && !Run.Economy.AllowsFormInk(entry.formId)) continue;
                if(maintenanceDraw&&!bag.Inventory.HasRoom)continue;
                if (entry.kind == InkKind.Special) pool.Add(entry.id);
                else if (specialOnly) continue;
                else if (entry.kind == InkKind.Stat && Run?.Growth == null) pool.Add(entry.id);
                else if (entry.kind == InkKind.Form && kinds.Contains(BlueprintKindOf(entry.formId))) pool.Add(entry.id);
            }
            return pool;
        }

        // Monsters marked Chapter1NoInkReward (seal challenge pages) still pay gold but drop no ink.
        void Defeated(TestEnemy enemy)
        {
            // Chapter1NoKillReward (the key carrier) drops nothing but its own key.
            if (enemy.GetComponent<Chapter1NoKillReward>()) return;
            Grant(Chapter1EnemyRole.Of(enemy), enemy.Position, !enemy.GetComponent<Chapter1NoInkReward>());
        }

        // Weapon stamps fit only the blueprints chosen for this run; without a chosen blueprint none can drop.
        public List<string> ChosenBlueprintKinds()
        {
            var kinds = new List<string>();
            var player = world.arena.Player;
            for (int slot = 0; slot < player.weapons.Count; slot++)
            {
                if (!player.IsWeaponSlotReady(slot)) continue;
                string kind = BlueprintKindOf(world.arena.catalog.forms[player.weapons[slot].FormIndex].id);
                if (!kinds.Contains(kind)) kinds.Add(kind);
            }
            return kinds;
        }

        // Read when the boss falls: the clear is settled only at the exit, so the profile still says "not yet cleared".
        bool BossClearEndsRun { get { var boss = world ? world.GetComponent<Chapter1BossEncounter>() : null; return boss && boss.ClearEndsRun; } }

        public RewardDrop Grant(string role, Vector2 at, bool inks = true)
        {
            var economy = Run?.Economy;
            if (economy == null || Run.Ended) return default;
            var drop = RewardRoller.Roll(economy.Table(role), economy.WeaponStampShare, random);
            // The run ends at the exit of a first clear, so its boss pays neither gold nor a stamp there (user decision
            // 2026-11-09, the same reason as the missing ink altar). Pigment is paid by the profile host and stays.
            if (role == Chapter1EnemyRole.Boss && BossClearEndsRun) { drop = new RewardDrop(0, 0, 0, 0); inks = false; }
            // Sketch-weapon runs get weapons and inks only from kill-milestone, boss and shop choices; monsters drop
            // no equipment on the floor (docs/design/run/rewards.md). Gold stays.
            if (Run.Sketch != null) { drop = new RewardDrop(drop.Gold, 0, 0, 0); inks = false; }
            if (drop.Gold > 0)
            {
                var goldObject = new GameObject("Reward · Gold");
                goldObject.transform.SetParent(transform, false);
                var gold = goldObject.AddComponent<Chapter1RewardPickup>();
                gold.Initialize(world, Run.Stash, Chapter1RewardKind.Gold, null, world.arena.DropPoint(at, .9f, .75f));
                gold.SetGold(Run, Run.Perks.AdjustKillGold(drop.Gold));
                pickups.Add(gold);
            }
            if (world.arena.Sketch != null)
            {
                // Gauge and boss offers are reported once by Chapter1RunBinding.
                return new RewardDrop(drop.Gold, 0, 0, 0);
            }
            var items = new List<(Chapter1RewardKind kind, RunStamp stamp)>();
            for (int i = 0; i < (inks ? drop.Parts : 0); i++) items.Add((Chapter1RewardKind.InkPart, null));
            // Stage stamp limit: the stage's remaining room decides how many of the rolled stamps actually drop.
            var kinds = ChosenBlueprintKinds();
            int weaponStamps = 0, skillStamps = 0;
            for (int i = 0; i < drop.WeaponStamps + drop.SkillStamps && economy.TryTakeStageStamp(StampStages.Chapter1); i++)
            {
                var stamp = RollStageStamp(i < drop.WeaponStamps, kinds, random);
                bool weapon = stamp.Kind == StampKind.Weapon;
                if (weapon) weaponStamps++; else skillStamps++;
                items.Add((weapon ? Chapter1RewardKind.WeaponStamp : Chapter1RewardKind.SkillStamp, stamp));
            }
            drop = new RewardDrop(drop.Gold, drop.Parts, weaponStamps, skillStamps, drop.Inks, drop.SpecialInks);
            var dropInks = new List<string>();
            var anyInk = InkPool(); var specialInk = InkPool(true);
            for (int i = 0; i < (inks ? drop.Inks : 0) && anyInk.Count > 0; i++) dropInks.Add(anyInk[random.Next(anyInk.Count)]);
            for (int i = 0; i < (inks ? drop.SpecialInks : 0) && specialInk.Count > 0; i++) dropInks.Add(specialInk[random.Next(specialInk.Count)]);
            if(Run.Growth!=null&&inks)
            {
                var growth=Run.Growth;
                foreach(var id in growth.PlanReward(role,random)){var go=new GameObject("Reward · Colored Ink");go.transform.SetParent(transform,false);var pickup=go.AddComponent<Chapter1RewardPickup>();pickup.Initialize(world,Run.Stash,Chapter1RewardKind.ColoredInk,null,world.arena.DropPoint(at,.4f,1f),at);pickup.ConfigureGrowth(growth,id);pickups.Add(pickup);}
                // Form and special inks still supply the existing weapon route. Generic stat inks are superseded by colored parts.
                dropInks.RemoveAll(id=>System.Array.Exists(Bag.Content.items,e=>e.id==id&&e.kind==InkKind.Stat));
            }
            int total = items.Count + dropInks.Count;
            for (int i = 0; i < dropInks.Count; i++)
                Bag.SpawnDrop(dropInks[i], world.arena.DropPoint(at, (items.Count + i - (total - 1) * .5f) * .8f, 1f), at);
            for (int i = 0; i < items.Count; i++)
            {
                var go = new GameObject("Reward · " + items[i].kind);
                go.transform.SetParent(transform, false);
                var pickup = go.AddComponent<Chapter1RewardPickup>();
                pickup.Initialize(world, Run.Stash, items[i].kind, null, world.arena.DropPoint(at, (i - (total - 1) * .5f) * .8f, 1f), at);
                if (items[i].stamp != null) pickup.ConfigureStamp(items[i].stamp);
                pickups.Add(pickup);
            }
            return drop;
        }

        public bool ExternalPresentation { get; set; }
        void OnGUI()
        {
            if (ExternalPresentation) return;
            var run = Run;
            if (run?.Economy == null || binding.ShowingResult) return;
            var oldFont = GUI.skin.font;
            try
            {
                if (world.diagnostics && world.diagnostics.uiFont) GUI.skin.font = world.diagnostics.uiFont;
                var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
                style.normal.textColor = new Color(1f, .86f, .4f);
                var stash = run.Stash;
                string text = $"  골드 {run.Economy.Gold}G   잉크 파츠 {stash.InkParts}   무기 스탬프 {stash.Count(StampKind.Weapon)}   스킬 스탬프 {stash.Count(StampKind.Skill)}";
                GUI.Box(new Rect(Screen.width - 560, 8, 548, 30), text, style);
            }
            finally { GUI.skin.font = oldFont; }
        }
    }
}
