using System;
using System.Collections.Generic;
using System.Linq;
namespace Deviant.Run
{
    [Serializable] public sealed class GrowthValues { public float rechargeReduction, ratio, amount, damageTaken, windupReduction, duration, distance, radius, seconds, interval, attackSpeed, moveSpeed, switchWindupReduction, threshold, amountPerKill, cap, amountPerStack, maxStacks, resetSeconds, ratioPerState, count; }
    // One upgradable value as the catalog words it, pre-formatted for slot levels +0..+5 by export_weapon_growth.py.
    [Serializable] public sealed class GrowthStat { public string label; public string[] values; }
    [Serializable] public sealed class GrowthPart
    {
        public string id, name, description, color, trigger, kind, scope, condition, icon; public int grade, every, markIndex; public bool duplicate, targetCooldown; public float cooldown, eliteCooldown; public GrowthValues[] levels; public GrowthStat[] stats;
        // "비율 32%" for a slot at +4; empty when the data carries no upgradable value.
        public string StatsAt(int level)=>stats==null?"":string.Join(" · ",stats.Where(s=>s.values!=null&&s.values.Length>0).Select(s=>s.label+" "+s.values[Math.Max(0,Math.Min(level,s.values.Length-1))]));
    }
    [Serializable] public sealed class GrowthGrades { public string source; public float[] weights; }
    [Serializable] public sealed class GrowthColor { public string color; public float weight; }
    [Serializable] public sealed class GrowthReward { public string source; public float inkChance, paletteChance; public int inkCount, paletteCount; }
    [Serializable] public sealed class GrowthData
    {
        public int schema,colorChangeGold,colorChangesPerRun,earlyOwnedColorInks,attackMaxLevel,chainDepth,inkCapacity,markCap;
        public float colorlessChancePerSlot,saleFraction,attackPerLevel,markCoefficient,markDuration,markPopInterval,movementSpeedBonusCap,lifeStealFractionCap,hardControlRecoverySeconds,bossControlDurationAndDisplacementScale,combinedDisplacementCapPerTargetPerSourceAction,shieldCap;
        public string concentratedColorSelection,basicReferencePolicy;
        public GrowthPart FindPart(string id) => Array.Find(parts, p => p.id == (id == "ink3_blue_03" ? "ink3_blue_01" : id));
        public GrowthPart[] parts; public GrowthGrades[] gradeSources; public GrowthReward[] rewards; public float[] slotWeights; public GrowthColor[] colorWeights;
        public void Validate()
        {
            if(schema!=1||parts==null||parts.Length!=53)throw new ArgumentException("MVP 데이터 오류");
            if(markCap<1||markCap>7||!(markDuration>0)||!(markPopInterval>0))throw new ArgumentException("MVP 표식 데이터 오류");
            if(inkCapacity<1)throw new ArgumentException("MVP 가방 용량 오류");
            if(float.IsNaN(shieldCap)||float.IsInfinity(shieldCap)||shieldCap<=0)throw new ArgumentException("MVP 보호막 상한 오류");
            var ids=new HashSet<string>();
            foreach(var p in parts)
            {
                if(p==null||string.IsNullOrEmpty(p.id)||!ids.Add(p.id)||p.levels==null||p.levels.Length!=6||p.levels.Any(v=>v==null))throw new ArgumentException("MVP 파츠/레벨 데이터 오류: "+p?.id);
                bool supported=p.kind switch
                {
                    "extra_hit" or "focus_stack_damage" or "execute_damage" or "mark_spread" or "poison_burst" or "underline_mark" => p.trigger=="hit",
                    "stun" => p.trigger=="hit"||p.trigger=="dash",
                    "bind" or "poison" => p.trigger=="hit"||p.trigger=="mark_pop",
                    "chain_lightning" => p.trigger=="mark_pop",
                    "mark_chain_spread" => p.trigger=="mark_burst",
                    "move_charge" => p.trigger=="move",
                    "hit_guard_shield" => p.trigger=="damaged",
                    "next_attack_power" or "switch_slide" or "next_attack_geometry" => p.trigger=="switch",
                    "shield" => p.trigger=="switch"||p.trigger=="kill",
                    "burst_extra_hit" or "cc_count_hit" => p.trigger=="mark_burst",
                    "kill_area_hit" or "kill_stack_attack_power" or "moving_attack_power" => p.trigger=="kill",
                    "electric_field" => p.trigger=="move"||p.trigger=="mark_burst",
                    "attack_and_move_speed" => p.trigger=="passive"||p.trigger=="switch",
                    "dash_hit" => p.trigger=="dash",
                    "all_geometry" or "shielded_attack_power" or "high_health_attack_power" or "attack_reach" or "stunned_dot" or "mark_damage_bonus" or "risky_attack_power" or "dash_followup" or "extra_skill_charge" or "controlled_damage" => p.trigger=="passive",
                    "homing_counter" or "stored_retaliation" => p.trigger=="damaged",
                    "heal" => p.trigger=="hit"||p.trigger=="kill"||p.trigger=="mark_pop",
                    "area_radius" => p.trigger=="passive"||p.trigger=="kill",
                    "cooldown_reduce" => p.trigger=="hit"||p.trigger=="switch"||p.trigger=="kill"||p.trigger=="mark_burst"||p.trigger=="mark_pop",
                    "area_hit" => p.trigger=="bind_end",
                    "hit_recovery_cancel" => p.trigger=="hit",
                    _ => false
                };
                if(!supported)throw new ArgumentException($"MVP 지원하지 않는 효과/발동: {p.id} ({p.kind}/{p.trigger})");
                if((p.trigger=="mark_pop")!=(p.markIndex>=1&&p.markIndex<=markCap))throw new ArgumentException($"MVP 표식 순번 오류: {p.id} ({p.markIndex})");
                if(p.scope!="equipped_weapon"&&p.scope!="incoming_weapon"&&p.scope!="both_weapons")throw new ArgumentException($"MVP 지원하지 않는 적용 대상: {p.id} ({p.scope})");
                if(!string.IsNullOrEmpty(p.condition)&&p.condition!="first_hit_per_life"&&p.condition!="special")throw new ArgumentException($"MVP 지원하지 않는 조건: {p.id} ({p.condition})");
            }
        }
    }
    public sealed class GrowthInk { public Guid Id {get;} public GrowthPart Part {get;} public GrowthInk(GrowthPart part):this(part,Guid.NewGuid()){} internal GrowthInk(GrowthPart part,Guid id){Part=part;Id=id;} }
    // One ink on a maintenance stall: the part, its fixed price and whether this run already bought it.
    public sealed class InkOffer { public GrowthPart Part {get;} public int Price {get;} public bool Sold {get;internal set;} public InkOffer(GrowthPart part,int price){Part=part;Price=price;} }
    public sealed class GrowthSlot { public Guid InstanceId {get;internal set;}=Guid.NewGuid(); public string Color {get;internal set;} public GrowthPart Part {get;internal set;} public int Level {get;internal set;} }
    public sealed class GrowthPalette
    {
        public Guid Id {get;}=Guid.NewGuid(); public Guid BoundWeapon {get;internal set;} public bool ColorChanged {get;internal set;}
        public IReadOnlyList<GrowthSlot> Slots {get;internal set;}
        internal GrowthPalette(string[] colors){Slots=Array.AsReadOnly(colors.Select(c=>new GrowthSlot{Color=c}).ToArray());}
    }
    // Owns colored inks only; form/stat/special legacy bag items remain in their existing inventory.
    public sealed partial class WeaponGrowth
    {
        public SketchEquipment Sketch { get; set; }
        public GrowthData Data {get;} public RunEconomy Economy {get;}
        public RunPerks Perks { get; set; }
        readonly List<GrowthInk> inks=new List<GrowthInk>();readonly List<GrowthPalette> palettes=new List<GrowthPalette>();
        readonly Dictionary<Guid,GrowthPalette> equipped=new Dictionary<Guid,GrowthPalette>();
        public IReadOnlyList<GrowthInk> Inks=>inks.AsReadOnly();public IReadOnlyList<GrowthPalette> Palettes=>palettes.AsReadOnly();
        public int AttackLevel {get;private set;}
        // Recovery bonus (destroyed_ink_gold_20) from the run's departure snapshot; 0 when not unlocked.
        public int DestroyedInkGoldBonus {get;set;}
        // Permanent attack upgrade (0.05 per village tier) from the departure snapshot, added to the maintenance levels.
        public float PermanentAttackBonus {get;set;}
        // One payout per destroyed ink instance, however the destruction is reported.
        readonly HashSet<Guid> destroyReceipts=new HashSet<Guid>();
        public int DestroyRewardsPaid=>destroyReceipts.Count;
        public int Revision {get;private set;}
        public event Action Changed;
        public event Action<GrowthInk> Acquired;
        public Func<int> ExternalUsage {get;set;}
        // Village ink unlocks: parts a profile run has not unlocked yet. Drops, the maintenance stall and the boss altar skip
        // them; direct grants (development sets, tests) still work. Empty for training and runs without a village plan.
        readonly HashSet<string> lockedParts=new HashSet<string>();
        public IReadOnlyCollection<string> LockedParts=>lockedParts;
        public void LockParts(IEnumerable<string> ids){lockedParts.Clear();if(ids!=null)foreach(var id in ids)lockedParts.Add(id);}
        public bool Offered(GrowthPart part)=>part!=null&&!lockedParts.Contains(part.id);
        // The run's shared bag: the data's base bag, raised once at departure by the village bag unlock (bag_slots_*).
        int inkCapacity;
        public int InkCapacity {get=>inkCapacity;set{if(value<Data.inkCapacity)throw new ArgumentOutOfRangeException(nameof(value),"가방은 기본 칸보다 줄일 수 없습니다.");inkCapacity=value;Publish();}}
        string BagFullMessage=>$"가방이 가득 찼습니다. ({InkCapacity}/{InkCapacity})";
        public bool HasInkRoom=>inks.Count+Math.Max(0,ExternalUsage?.Invoke()??0)<InkCapacity;
        public WeaponGrowth(GrowthData data,RunEconomy economy){Data=data??throw new ArgumentNullException(nameof(data));Economy=economy??throw new ArgumentNullException(nameof(economy));data.Validate();inkCapacity=data.inkCapacity;}
        void Publish(){Revision++;Changed?.Invoke();}
        static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public bool TryGrantInk(string id,out GrowthInk ink,out string error)
        {ink=null;error=null;var part=Data.FindPart(id);if(part==null){error="MVP 잉크 없음";return false;}if(!HasInkRoom){error=BagFullMessage;return false;}ink=new GrowthInk(part);inks.Add(ink);Publish();Acquired?.Invoke(ink);return true;}
        // Debug replacement does not create bag items, consume gold, or bypass colour/duplicate compatibility.
        public void DebugSetPart(Guid weapon, int index, string id, int? level = null)
        {
            var slot = Slot(weapon, index);
            Require(!level.HasValue || level.Value >= 0 && level.Value <= 5, "강화 단계는 +0부터 +5까지입니다.");
            GrowthPart part = null;
            if (id != null)
            {
                part = Data.FindPart(id);
                Require(part != null, "Unknown ink part");
                Check(weapon, index, new GrowthInk(part), slot.Color);
            }
            if (slot.Part == part)
            {
                if (level.HasValue && slot.Level != level.Value) { slot.Level = level.Value; Publish(); }
                return;
            }
            slot.Part = part; slot.InstanceId = Guid.NewGuid(); if(level.HasValue)slot.Level=level.Value; Publish();
        }
        public void DebugSetSlotColor(Guid weapon, int index, string color)
        {
            var slot = Slot(weapon, index);
            Require(Data.colorWeights.Any(w => w.color == color) || color == "colorless" && index == 0, "이 칸에서 사용할 수 없는 색상입니다.");
            Require(slot.Part == null || color == "colorless" || slot.Part.color == color, "장착 파츠와 색상이 맞지 않습니다. 파츠를 먼저 해제하세요.");
            if (slot.Color == color) return;
            slot.Color = color; Publish();
        }
        public void DebugResizePalette(Guid weapon, int count)
        {
            Require(count >= 3 && count <= 5, "팔레트 칸 수는 3~5칸입니다.");
            var palette = Equipped(weapon);
            if (palette == null) { Bind(GrantPalette(Enumerable.Repeat(Data.colorWeights[0].color,count).ToArray()).Id,weapon); return; }
            if (palette.Slots.Count == count) return;
            Require(!palette.Slots.Skip(count).Any(s => s.Part != null || s.Level > 0), "제거될 칸의 파츠와 강화 단계를 먼저 비우세요.");
            var cells = palette.Slots.Take(count).ToList();
            string color = palette.Slots[palette.Slots.Count-1].Color;
            while (cells.Count < count) cells.Add(new GrowthSlot { Color = color });
            palette.Slots = Array.AsReadOnly(cells.ToArray()); Publish();
        }
        public GrowthInk GrantInk(string id){Require(TryGrantInk(id,out var ink,out var error),error);return ink;}
        public GrowthPalette GrantPalette(string[] colors)
        {
            Require(colors!=null&&colors.Length>=3&&colors.Length<=5,"팔레트 칸 수 오류");
            Require(colors.All(c=>Data.colorWeights.Any(w=>w.color==c)),"팔레트 색상 오류");
            // Departure slots roll independently; accept every valid color combination.
            var p=new GrowthPalette(colors);palettes.Add(p);Publish();return p;
        }
        // Sketch-weapon bridge: makes a combat weapon's palette exactly the given inks (one per slot, null = empty),
        // all colourless at +0, without bag items or colour rules. An empty list removes the weapon's palette.
        // The sketch arsenal owns the inks; this only lets the existing ink effects run for them.
        public void MirrorPalette(Guid weapon, IReadOnlyList<string> inks)
        {
            Require(weapon != Guid.Empty, "무기 오류");
            var old = Equipped(weapon);
            if (inks == null || inks.Count == 0)
            {
                if (old == null) return;
                equipped.Remove(weapon); palettes.Remove(old); Publish(); return;
            }
            var p = new GrowthPalette(Enumerable.Repeat("colorless", inks.Count).ToArray());
            for (int i = 0; i < inks.Count; i++) p.Slots[i].Part = inks[i] == null ? null : Data.FindPart(inks[i]);
            if (old != null) palettes.Remove(old);
            p.BoundWeapon = weapon; palettes.Add(p); equipped[weapon] = p; Publish();
        }
        public GrowthSlot[] SnapshotSlots(Guid weapon)=>Equipped(weapon)?.Slots.Select(s=>new GrowthSlot{InstanceId=s.InstanceId,Color=s.Color,Part=s.Part,Level=s.Level}).ToArray()??Array.Empty<GrowthSlot>();
        public GrowthPalette Equipped(Guid weapon)=>equipped.TryGetValue(weapon,out var p)?p:null;
        public void Bind(Guid palette,Guid weapon)
        {
            var p=palettes.Find(p=>p.Id==palette);Require(p!=null&&weapon!=Guid.Empty,"팔레트·무기 오류");Require(p.BoundWeapon==Guid.Empty||p.BoundWeapon==weapon,"최초 무기에 귀속된 팔레트입니다.");var old=Equipped(weapon);if(old!=null&&old!=p)palettes.Remove(old);p.BoundWeapon=weapon;equipped[weapon]=p;Publish();
        }
        GrowthSlot Slot(Guid weapon,int index){var p=Equipped(weapon);Require(p!=null,"팔레트를 먼저 장착하세요.");Require(index>=0&&index<p.Slots.Count,"슬롯 오류");return p.Slots[index];}
        GrowthInk Ink(Guid id){var i=inks.Find(i=>i.Id==id);Require(i!=null,"보유 잉크 없음");return i;}
        void Check(Guid weapon,int index,GrowthInk ink,string color){Require(color=="colorless"||ink.Part.color==color,"색상이 맞지 않습니다.");Require(ink.Part.duplicate||!Equipped(weapon).Slots.Where((s,i)=>i!=index).Any(s=>s.Part==ink.Part),"중복 장착 불가 파츠입니다.");}
        // A slot carries the inserted ink's id, so a replaced or detached part keeps its identity.
        // Replacement is free and repeatable: the replaced part returns to the owned inks (user decision 2026-11-03).
        public void Insert(Guid weapon,int index,Guid inkId)
        {var slot=Slot(weapon,index);var ink=Ink(inkId);Check(weapon,index,ink,slot.Color);if(slot.Part!=null)inks.Add(new GrowthInk(slot.Part,slot.InstanceId));slot.InstanceId=ink.Id;slot.Part=ink.Part;inks.Remove(ink);Publish();}
        // Unequips a mounted part back to the bag with its identity, free and at any time (user decision 2026-11-05).
        // The slot keeps its colour and upgrade level and becomes empty; a full bag refuses instead of losing the ink.
        public GrowthInk Detach(Guid weapon,int index)
        {var slot=Slot(weapon,index);Require(slot.Part!=null,"빈 칸입니다.");Require(HasInkRoom,BagFullMessage);var ink=new GrowthInk(slot.Part,slot.InstanceId);inks.Add(ink);slot.Part=null;slot.InstanceId=Guid.NewGuid();Publish();return ink;}
        // Explicit destruction is the only path that pays the recovery bonus; replacement, sale, recolor detach,
        // floor despawn and run cleanup never call this.
        int RewardDestroyed(Guid inkId){if(DestroyedInkGoldBonus<=0||!destroyReceipts.Add(inkId))return 0;Economy.Earn(DestroyedInkGoldBonus);return DestroyedInkGoldBonus;}
        // Explicitly destroys an owned, unmounted color ink; mounted parts cannot be destroyed directly.
        public int Destroy(Guid inkId){var ink=Ink(inkId);inks.Remove(ink);int paid=RewardDestroyed(ink.Id);Publish();return paid;}
        // Fixed sale price (economy inkShop.saleGold); it no longer follows a draw price (user decision 2026-11-09).
        public int SalePrice=>Economy.InkSaleGold;
        public int Sell(Guid inkId){var ink=Ink(inkId);int value=SalePrice;inks.Remove(ink);Economy.Earn(value);Publish();return value;}
        public int UpgradeCost(Guid weapon,int index) => Economy.SlotUpgradeCost(Slot(weapon,index).Level);
        public void Upgrade(Guid weapon,int index)
        {
            var slot=Slot(weapon,index);
            Require(slot.Level<5,"최대 +5입니다.");
            Require(Economy.TrySpend(Economy.SlotUpgradeCost(slot.Level)),"골드 부족");
            slot.Level++;
            Publish();
        }
        public void UpgradeAttack(){Require(AttackLevel<Data.attackMaxLevel,"최대 공격 강화입니다.");Require(Economy.TryPurchase(PriceTracks.StatUpgrade),"골드 부족");AttackLevel++;Publish();}
        public float AttackMultiplier=>1+AttackLevel*Data.attackPerLevel+PermanentAttackBonus;
        // Kill-stacked attack ("기세를 몰아") lives for the whole run, keyed by the mounted ink instance.
        readonly Dictionary<Guid,int> killStacks=new Dictionary<Guid,int>();
        public int KillStacks(Guid ink)=>killStacks.TryGetValue(ink,out var n)?n:0;
        public void AddKillStack(Guid ink)=>killStacks[ink]=KillStacks(ink)+1;
        // "N회마다" 파츠(전투 식량·티끌 모아 태산)의 누적 횟수. 장착 잉크 인스턴스별로 런 내내 센다.
        readonly Dictionary<string,int> triggerCounts=new Dictionary<string,int>();
        public int CountTrigger(Guid ink,string trigger){string key=ink+":"+trigger;int n=(triggerCounts.TryGetValue(key,out var c)?c:0)+1;triggerCounts[key]=n;return n;}
        // Slot colour changes share one run-wide allowance (data colorChangesPerRun, trial 2) across both palettes.
        public int ColorChangesUsed {get;private set;}
        public int ColorChangesLeft=>Math.Max(0,Data.colorChangesPerRun-ColorChangesUsed);
        public void Recolor(Guid weapon,int index,string color,Guid inkId)
        {var p=Equipped(weapon);var s=Slot(weapon,index);Require(ColorChangesLeft>0,$"색 변경은 런당 {Data.colorChangesPerRun}회입니다.");Require(s.Part!=null,"빈칸 색상 변경은 미정입니다.");Require(color!="colorless"&&color!=s.Color&&Data.colorWeights.Any(w=>w.color==color),"새 색상을 선택하세요.");var ink=Ink(inkId);Check(weapon,index,ink,color);Require(Economy.TrySpend(Data.colorChangeGold),"골드 부족");
         // The recolored slot's old part is detached, not destroyed: it returns to the owned inks with its identity.
         inks.Add(new GrowthInk(s.Part,s.InstanceId));s.InstanceId=ink.Id;s.Color=color;s.Part=ink.Part;inks.Remove(ink);p.ColorChanged=true;ColorChangesUsed++;Publish();}
        static int Roll(IReadOnlyList<float> weights,Random random){double n=random.NextDouble()*weights.Sum();for(int i=0;i<weights.Count;i++){n-=weights[i];if(n<0)return i;}return weights.Count-1;}
        // Colour inks that have appeared this run from drops. The boss altar and the maintenance stall, where the player
        // chooses, are outside this count.
        public int ColorInksRolled {get;private set;}
        // Early colour limit: until earlyOwnedColorInks inks have appeared, only colours on the two palettes right now
        // (colourless excluded) can appear; after that the five colours are equally likely. Colour is drawn first, then
        // the grade by the source's weights among the grades that colour has, then a part of that colour and grade
        // (user decision 2026-10-31, recommended options).
        public GrowthPart ChoosePart(string source,Random random)
        {
            var weights=Array.Find(Data.gradeSources,s=>s.source==source)?.weights;Require(weights!=null,"획득처 오류");
            var offered=Data.parts.Where(Offered).ToArray();
            var colors=Data.colorWeights.Select(w=>w.color).Where(c=>c!="colorless"&&offered.Any(p=>p.color==c)).ToArray();
            if(ColorInksRolled<Data.earlyOwnedColorInks)
            {var owned=equipped.Values.SelectMany(p=>p.Slots).Select(s=>s.Color).ToHashSet();var allowed=colors.Where(owned.Contains).ToArray();if(allowed.Length>0)colors=allowed;}
            string color=colors[random.Next(colors.Length)];
            var adjusted=weights.Select((w,i)=>offered.Any(p=>p.color==color&&p.grade==i+1)?w:0).ToArray();
            int grade=adjusted.Sum()>0?Roll(adjusted,random)+1:offered.First(p=>p.color==color).grade;
            var pool=offered.Where(p=>p.color==color&&p.grade==grade).ToArray();
            ColorInksRolled++;
            return pool[random.Next(pool.Length)];
        }
        // Boss altar: the player names a colour and gets up to three different candidates of it, drawn with the boss grade
        // weights; unique (non-duplicate) parts the run already owns are left out. Altar inks are outside the early colour
        // count. Candidate grade odds and the fate of unpicked candidates follow the user decision of 2026-10-31.
        public string[] AltarCandidates(string color,Random random)
        {
            Require(color!="colorless"&&Data.parts.Any(p=>p.color==color&&Offered(p)),"제단 색상 오류");
            var weights=Array.Find(Data.gradeSources,s=>s.source=="boss")?.weights;Require(weights!=null,"획득처 오류");
            var owned=inks.Select(i=>i.Part.id).Concat(palettes.SelectMany(p=>p.Slots).Where(s=>s.Part!=null).Select(s=>s.Part.id)).ToHashSet();
            var pool=Data.parts.Where(p=>p.color==color&&Offered(p)&&(p.duplicate||!owned.Contains(p.id))).ToList();var result=new List<string>();
            while(result.Count<3&&pool.Count>0){int i=Roll(pool.Select(p=>weights[p.grade-1]+1e-4f).ToArray(),random);result.Add(pool[i].id);pool.RemoveAt(i);}
            return result.ToArray();
        }
        public GrowthInk RollInk(string source,Random random){Require(HasInkRoom,BagFullMessage);return GrantInk(ChoosePart(source,random).id);}
        // Maintenance ink stall (replaces the random draw, user decision 2026-11-09). A facility's display is rolled the first
        // time it is asked for and kept for the run: offerCount (2~3) different parts of the stall grades (3~4), drawn
        // evenly from parts the run may be offered, leaving out unique parts it already owns. Every colour can appear;
        // like the altar, the stall ignores and does not advance the early colour count. Prices are fixed per grade.
        readonly Dictionary<string,InkOffer[]> stalls=new Dictionary<string,InkOffer[]>();
        public IReadOnlyList<InkOffer> InkStock(string facility,Random random)
        {
            Require(!string.IsNullOrEmpty(facility),"정비소 오류");
            if(stalls.TryGetValue(facility,out var stock))return stock;
            var shop=Economy.InkShop;Require(shop?.grades!=null&&shop.offerCount!=null,"잉크 판매대 데이터 오류");
            var owned=inks.Select(i=>i.Part.id).Concat(palettes.SelectMany(p=>p.Slots).Where(s=>s.Part!=null).Select(s=>s.Part.id)).ToHashSet();
            var pool=Data.parts.Where(p=>Array.IndexOf(shop.grades,p.grade)>=0&&Offered(p)&&(p.duplicate||!owned.Contains(p.id))).ToList();
            int count=random.Next(Math.Max(0,shop.offerCount.min),Math.Max(shop.offerCount.min,shop.offerCount.max)+1)+(Perks?.Settings.ExtraShopItems??0);
            var offers=new List<InkOffer>();
            while(offers.Count<count&&pool.Count>0){int i=random.Next(pool.Count);offers.Add(new InkOffer(pool[i],Economy.InkShopPrice(pool[i].grade)));pool.RemoveAt(i);}
            stock=offers.ToArray();stalls[facility]=stock;return stock;
        }
        public IReadOnlyList<InkOffer> StockOf(string facility)=>facility!=null&&stalls.TryGetValue(facility,out var stock)?stock:Array.Empty<InkOffer>();
        public bool RerollInkStock(string facility, Random random)
        {
            if (Perks == null || random == null) return false;
            return Perks.TryReroll(() =>
            {
                if (!stalls.TryGetValue(facility, out var stock) || stock.All(o => o.Sold)) return false;
                var sold = stock.Where(o => o.Sold).Select(o => o.Part.id).ToHashSet();
                var old = stock.Where(o => !o.Sold).Select(o => o.Part.id).ToHashSet();
                var pool = Data.parts.Where(p => Array.IndexOf(Economy.InkShop.grades, p.grade) >= 0 && Offered(p) && !sold.Contains(p.id) &&
                    (p.duplicate || !inks.Any(i => i.Part.id == p.id) && !palettes.Any(x => x.Slots.Any(s => s.Part?.id == p.id)))).ToList();
                var fresh = pool.Where(p => !old.Contains(p.id)).ToList();
                if (fresh.Count == 0) return false;
                var selected = new List<GrowthPart> { fresh[random.Next(fresh.Count)] };
                pool.RemoveAll(p => p.id == selected[0].id);
                while (selected.Count < old.Count && pool.Count > 0) { int n = random.Next(pool.Count); selected.Add(pool[n]); pool.RemoveAt(n); }
                if (selected.Count != old.Count) return false;
                int index = 0;
                for (int i = 0; i < stock.Length; i++) if (!stock[i].Sold)
                { var part = selected[index++]; stock[i] = new InkOffer(part, Economy.InkShopPrice(part.grade)); }
                Publish(); return true;
            });
        }
        public bool RerollAltar(string color, string[] previous, Random random, out string[] next)
        {
            string[] result = null;
            bool ok = Perks != null && previous != null && Perks.TryReroll(() =>
            {
                var candidates = AltarCandidates(color, random);
                var old = previous.ToHashSet();
                if (old.SetEquals(candidates))
                {
                    var alternative = Data.parts.FirstOrDefault(p => p.color == color && Offered(p) && !old.Contains(p.id) &&
                        (p.duplicate || !inks.Any(i => i.Part.id == p.id) && !palettes.Any(x => x.Slots.Any(s => s.Part?.id == p.id))));
                    if (alternative == null || candidates.Length == 0) return false;
                    candidates[0] = alternative.id;
                }
                result = candidates; return true;
            });
            next = result; return ok;
        }
        // A full bag or short gold refuses before any gold is taken; a bought offer stays on the shelf as sold.
        public GrowthInk BuyStockInk(string facility,int index)
        {
            var stock=StockOf(facility);Require(index>=0&&index<stock.Count,"판매대 상품이 없습니다.");var offer=stock[index];
            Require(!offer.Sold,"이미 판매된 잉크입니다.");Require(HasInkRoom,BagFullMessage);Require(Economy.TrySpend(offer.Price),"골드 부족");
            var ink=GrantInk(offer.Part.id);offer.Sold=true;Publish();return ink;
        }
        // Kills drop color inks only. Palettes are fixed at departure and never dropped, sold or rebuilt.
        public string[] PlanReward(string source,Random random)
        {var r=Array.Find(Data.rewards,r=>r.source==source);Require(r!=null,"보상 종류 오류");var inks=new List<string>();if(random.NextDouble()<r.inkChance)for(int i=0;i<r.inkCount;i++)inks.Add(ChoosePart(source,random).id);return inks.ToArray();}
        // Start palette: the system picks every color; colorless only comes from the first-slot wildcard unlock.
        // Every slot rolls its own color by the color weights, whatever the slot count (user decision 2026-11-03/04).
        public string[] RollStartColors(int count,bool wildcard,Random random)
        {
            Require(count>=3&&count<=5,"팔레트 칸 수 오류");
            var weighted=Data.colorWeights.Where(w=>w.color!="colorless").ToArray();var colors=weighted.Select(w=>w.color).ToArray();
            var result=Enumerable.Range(0,count).Select(_=>colors[Roll(weighted.Select(w=>w.weight).ToArray(),random)]).ToArray();
            if(wildcard)result[0]="colorless";return result;
        }
        // Grants and binds one weapon's departure palette; a weapon keeps a single palette for the whole run.
        public GrowthPalette EquipStartPalette(Guid weapon,int count,bool wildcard,Random random)
        {Require(weapon!=Guid.Empty&&Equipped(weapon)==null,"이미 출발 팔레트가 있습니다.");var p=GrantPalette(RollStartColors(count,wildcard,random));Bind(p.Id,weapon);return p;}
    }
}
