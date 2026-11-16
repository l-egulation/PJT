using System;
using System.Linq;
using Deviant.Run;
using Deviant.Definitions;
using Deviant.Combat;
using NUnit.Framework;
using UnityEngine;
namespace ForestCombatV2.Tests
{
 public sealed class WeaponGrowthMvpTests
 {
  static WeaponGrowth Make(){var data=JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text);var economy=JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text);return new WeaponGrowth(data,new RunEconomy(economy));}
  // Village ink unlocks: locked parts never appear in drops, the maintenance stall or altar candidates, while direct
  // grants (development sets) still work.
  [Test]public void LockedPartsStayOutOfDropsTheStallAndTheAltar()
  {
   var g=Make();var locked=new[]{"ink3_red_02","ink3_red_09","ink3_blue_09"};g.LockParts(locked);var r=new System.Random(7);
   for(int i=0;i<600;i++)CollectionAssert.DoesNotContain(locked,g.ChoosePart(i%2==0?"elite":"normal",r).id);
   for(int i=0;i<200;i++)foreach(var offer in g.InkStock("facility"+i,r))CollectionAssert.DoesNotContain(locked,offer.Part.id);
   for(int i=0;i<200;i++){foreach(var id in g.AltarCandidates("red",r))CollectionAssert.DoesNotContain(locked,id);foreach(var id in g.AltarCandidates("blue",r))CollectionAssert.DoesNotContain(locked,id);}
   Assert.IsNotNull(g.GrantInk("ink3_red_02"),"a direct grant still works");
   g.LockParts(null);bool seen=false;for(int i=0;i<2000&&!seen;i++)seen=g.ChoosePart("elite",r).id=="ink3_blue_09";Assert.IsTrue(seen,"unlocked again, the part drops");
  }
  // The village bag unlock raises the run's shared bag at departure; it never drops below the data's base bag.
  [Test]public void TheRunBagTakesTheBagUnlockAndNeverShrinksBelowTheBase()
  {
   var g=Make();Assert.AreEqual(5,g.InkCapacity);for(int i=0;i<5;i++)g.GrantInk("ink3_red_01");Assert.IsFalse(g.HasInkRoom);
   g.InkCapacity=7;Assert.IsTrue(g.HasInkRoom);g.GrantInk("ink3_red_01");g.GrantInk("ink3_red_01");Assert.IsFalse(g.TryGrantInk("ink3_red_01",out _,out var error));
   StringAssert.Contains("7/7",error);
   Assert.Throws<ArgumentOutOfRangeException>(()=>g.InkCapacity=4);
  }
  [Test]public void FullColorBagRejectsGrantsAndStallPurchasesWithoutCharging()
  {
   var g=Make();Assert.AreEqual(5,g.Data.inkCapacity);g.Economy.Earn(1000);for(int i=0;i<5;i++)g.GrantInk("ink3_red_01");
   int gold=g.Economy.Gold,rolled=g.ColorInksRolled;var offer=g.InkStock("facility",new System.Random(9))[0];
   Assert.IsFalse(g.TryGrantInk("ink3_red_03",out var rejected,out _));Assert.IsNull(rejected);
   Assert.Throws<InvalidOperationException>(()=>g.BuyStockInk("facility",0));Assert.AreEqual(5,g.Inks.Count);Assert.IsFalse(offer.Sold);
   Assert.AreEqual(gold,g.Economy.Gold);Assert.AreEqual(rolled,g.ColorInksRolled);
   var weapon=Guid.NewGuid();g.Bind(g.GrantPalette(new[]{"red","red","red"}).Id,weapon);g.Insert(weapon,0,g.Inks[0].Id);
   Assert.IsTrue(g.HasInkRoom);Assert.AreSame(offer.Part,g.BuyStockInk("facility",0).Part);Assert.AreEqual(5,g.Inks.Count);Assert.AreEqual(gold-offer.Price,g.Economy.Gold);
   Assert.AreEqual(rolled,g.ColorInksRolled,"stall purchases are outside the early colour count");
  }
  // Ink stall (user decision 2026-11-09): every facility shows 2~3 different grade 3~4 inks at fixed prices (grade 3 =
  // attack upgrades 1~4 in total, grade 4 = 1~5), keeps that display for the run and marks bought inks as sold.
  [Test]public void InkStallShowsTwoOrThreeDifferentHighGradeInksAtFixedPrices()
  {
   var g=Make();var r=new System.Random(5);var unique=g.Data.parts.First(p=>!p.duplicate&&p.grade>=3);g.GrantInk(unique.id);
   Assert.AreEqual(420,g.Economy.InkShopPrice(3));Assert.AreEqual(600,g.Economy.InkShopPrice(4));
   var counts=new System.Collections.Generic.HashSet<int>();var grades=new System.Collections.Generic.HashSet<int>();var colours=new System.Collections.Generic.HashSet<string>();
   for(int i=0;i<300;i++)
   {
    var stock=g.InkStock("facility"+i,r);counts.Add(stock.Count);
    Assert.That(stock.Count,Is.InRange(2,3));Assert.AreEqual(stock.Count,stock.Select(o=>o.Part.id).Distinct().Count(),"one display never repeats a part");
    foreach(var o in stock){Assert.That(o.Part.grade,Is.InRange(3,4));Assert.AreEqual(g.Economy.InkShopPrice(o.Part.grade),o.Price);Assert.AreNotEqual(unique.id,o.Part.id,"owned unique parts are left out");Assert.IsFalse(o.Sold);grades.Add(o.Part.grade);colours.Add(o.Part.color);}
    Assert.AreSame(stock,g.InkStock("facility"+i,r),"a facility keeps its display");
   }
   CollectionAssert.AreEquivalent(new[]{2,3},counts);CollectionAssert.AreEquivalent(new[]{3,4},grades);CollectionAssert.AreEquivalent(new[]{"red","yellow","green","blue","purple"},colours);
   Assert.AreEqual(0,g.ColorInksRolled,"rolling displays does not use the early colour count");
   var shelf=g.InkStock("shop",r);var first=shelf[0];g.Economy.Earn(first.Price-1);int before=g.Inks.Count;
   Assert.Throws<InvalidOperationException>(()=>g.BuyStockInk("shop",0));Assert.AreEqual(first.Price-1,g.Economy.Gold);Assert.AreEqual(before,g.Inks.Count);Assert.IsFalse(first.Sold);
   g.Economy.Earn(1);g.BuyStockInk("shop",0);Assert.IsTrue(first.Sold);Assert.AreEqual(0,g.Economy.Gold);Assert.AreEqual(before+1,g.Inks.Count);
   g.Economy.Earn(10000);Assert.Throws<InvalidOperationException>(()=>g.BuyStockInk("shop",0),"a sold ink stays sold");Assert.AreEqual(10000,g.Economy.Gold);
   Assert.Throws<InvalidOperationException>(()=>g.BuyStockInk("unknown",0));
  }
  [TestCase("kind")][TestCase("trigger")][TestCase("scope")][TestCase("condition")]
  public void LoadingRejectsUnknownEffectRulesWithThePartId(string field)
  {
   var g=Make();var part=g.Data.parts[0];typeof(GrowthPart).GetField(field).SetValue(part,"unknown_rule");
   var error=Assert.Throws<ArgumentException>(()=>new WeaponGrowth(g.Data,g.Economy));
   StringAssert.Contains(part.id,error.Message);StringAssert.Contains("unknown_rule",error.Message);
  }
  [TestCase(0f)][TestCase(-1f)][TestCase(float.NaN)][TestCase(float.PositiveInfinity)]
  public void LoadingRejectsInvalidCurrentShieldCaps(float cap)
  {var g=Make();g.Data.shieldCap=cap;Assert.Throws<ArgumentException>(()=>new WeaponGrowth(g.Data,g.Economy));}
  [Test]public void DetachReturnsTheMountedInkKeepsTheSlotAndRefusesAFullBag()
  {
   var g=Make();var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Economy.Earn(1000);
   var ink=g.GrantInk("ink3_red_01");g.Insert(w,0,ink.Id);g.Upgrade(w,0);int gold=g.Economy.Gold;
   var back=g.Detach(w,0);Assert.AreEqual(ink.Id,back.Id,"the same ink instance comes back");Assert.IsNull(p.Slots[0].Part);Assert.AreEqual(1,p.Slots[0].Level,"the upgrade stays on the slot");
   Assert.AreEqual(gold,g.Economy.Gold,"free");Assert.IsTrue(g.Inks.Contains(back));Assert.Throws<InvalidOperationException>(()=>g.Detach(w,0),"an empty slot");
   g.Insert(w,0,back.Id);while(g.HasInkRoom)g.GrantInk("ink3_red_03");
   Assert.Throws<InvalidOperationException>(()=>g.Detach(w,0),"a full bag refuses");Assert.AreEqual("ink3_red_01",p.Slots[0].Part.id,"and the ink stays mounted");
  }
  [Test]public void SlotReadoutShowsTheValuesTheSlotLevelActuallyUses()
  {
   var g=Make();Assert.That(g.Data.parts.All(p=>p.stats!=null&&p.stats.Length>0&&p.stats.All(v=>v.values.Length==6)),Is.True);
   var red=g.Data.FindPart("ink3_red_01");Assert.AreEqual("추가타 20%",red.StatsAt(0));Assert.AreEqual("추가타 40%",red.StatsAt(4));
   Assert.AreEqual(.4f,red.levels[4].ratio,1e-5f);
   Assert.That(g.Data.FindPart("ink3_yellow_07").StatsAt(5),Does.Contain("35%"));
   Assert.That(g.Data.FindPart("ink3_blue_09").StatsAt(5),Does.Contain("25%"));
   Assert.That(g.Data.FindPart("ink3_green_04").StatsAt(0),Does.Contain("1HP").And.Not.Contain("100%"));
  }
  [Test]public void SlotPricesAreIndependentAcrossBothWeaponsAndCapWithoutCharging()
  {
   var g=Make();var a=Guid.NewGuid();var b=Guid.NewGuid();
   g.Bind(g.GrantPalette(new[]{"red","red","red"}).Id,a);g.Bind(g.GrantPalette(new[]{"blue","blue","blue"}).Id,b);
   g.Economy.Earn(3000);var expected=new[]{50,100,180,350,600};
   for(int i=0;i<5;i++){Assert.AreEqual(expected[i],g.UpgradeCost(a,0));g.Upgrade(a,0);Assert.AreEqual(50,g.UpgradeCost(a,1));Assert.AreEqual(50,g.UpgradeCost(b,0));}
   Assert.AreEqual(1720,g.Economy.Gold);Assert.Throws<InvalidOperationException>(()=>g.Upgrade(a,0));Assert.AreEqual(1720,g.Economy.Gold);
   g.Upgrade(b,0);Assert.AreEqual(1670,g.Economy.Gold);Assert.AreEqual(100,g.UpgradeCost(b,0));
   g.Economy.DebugSetGold(99);Assert.Throws<InvalidOperationException>(()=>g.Upgrade(b,0));Assert.AreEqual(99,g.Economy.Gold);Assert.AreEqual(1,g.Equipped(b).Slots[0].Level);
  }
  [Test]public void MergedReachIdResolvesWithoutAppearingInDropPool()
  {
   var g=Make();Assert.AreEqual(53,g.Data.parts.Length);Assert.IsFalse(g.Data.parts.Any(p=>p.id=="ink3_blue_03"));
   Assert.AreEqual("ink3_blue_01",g.GrantInk("ink3_blue_03").Part.id);
  }
  [Test]public void ChapterTwoUsesItsOwnGoldRangesWhileChapterOneStaysUnchanged()
  {
   var g=Make();var rng=new System.Random(17);
   foreach(var role in new[]{"normal","elite"})
   {
    var table=g.Economy.Chapter2Table(role);int min=role=="normal"?13:450,max=role=="normal"?16:550;
    Assert.AreEqual(min,table.gold.min);Assert.AreEqual(max,table.gold.max);bool low=false,high=false;
    for(int i=0;i<5000;i++){int gold=RewardRoller.Roll(table,.5f,rng).Gold;Assert.That(gold,Is.InRange(min,max));low|=gold==min;high|=gold==max;}Assert.IsTrue(low&&high);
   }
   Assert.AreEqual(6,g.Economy.Table("normal").gold.min);Assert.AreEqual(10,g.Economy.Table("normal").gold.max);
   Assert.AreEqual(200,g.Economy.Table("elite").gold.min);Assert.AreEqual(280,g.Economy.Table("elite").gold.max);
  }

  [Test]public void SalesPayTheFixedSalePrice(){var g=Make();Assert.AreEqual(8,g.SalePrice);for(int i=0;i<5;i++)g.Sell(g.GrantInk("ink3_red_03").Id);Assert.AreEqual(40,g.Economy.Gold);g.Economy.Earn(1000);g.InkStock("facility",new System.Random(1));g.BuyStockInk("facility",0);Assert.AreEqual(8,g.SalePrice,"buying from the stall never changes the sale price");}
  [Test]public void BindingAndColorsRejectWithoutConsumption(){var g=Make();var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);Assert.Throws<InvalidOperationException>(()=>g.Bind(p.Id,Guid.NewGuid()));var blue=g.GrantInk("ink3_blue_02");Assert.Throws<InvalidOperationException>(()=>g.Insert(w,0,blue.Id));Assert.AreEqual(1,g.Inks.Count);Assert.IsNull(p.Slots[0].Part);}
  [Test]public void FreeReplacementKeepsSlotUpgradeAndReturnsOldInk(){var g=Make();var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Economy.Earn(1000);var first=g.GrantInk("ink3_red_01");g.Insert(w,0,first.Id);g.Upgrade(w,0);var second=g.GrantInk("ink3_red_03");g.Insert(w,0,second.Id);Assert.AreEqual(1,p.Slots[0].Level);Assert.AreEqual(950,g.Economy.Gold,"only the slot upgrade costs gold");Assert.AreEqual(1,g.Inks.Count);Assert.IsTrue(g.Inks.Any(i=>i.Id==first.Id&&i.Part.id=="ink3_red_01"),"the replaced ink returns with its identity");g.Insert(w,0,first.Id);Assert.AreEqual("ink3_red_01",p.Slots[0].Part.id,"swapping back is repeatable");Assert.IsTrue(g.Inks.Any(i=>i.Id==second.Id),"the other ink comes back");Assert.AreEqual(950,g.Economy.Gold);}
  [Test]public void DuplicateEquipIsIndependentOfRepeatDrop(){var g=Make();var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Insert(w,0,g.GrantInk("ink3_red_03").Id);var second=g.GrantInk("ink3_red_03");Assert.Throws<InvalidOperationException>(()=>g.Insert(w,1,second.Id));Assert.AreEqual(1,g.Inks.Count);}
  [Test]public void NewPaletteReplacementIsFreeDestroysOldAndStartsAtZero(){var g=Make();var w=Guid.NewGuid();var old=g.GrantPalette(new[]{"red","red","red"});g.Bind(old.Id,w);g.Economy.Earn(100);g.Upgrade(w,0);var next=g.GrantPalette(new[]{"blue","blue","colorless"});g.Bind(next.Id,w);Assert.AreEqual(50,g.Economy.Gold);Assert.AreEqual(1,g.Palettes.Count);Assert.AreEqual(0,next.Slots[0].Level);}
  [Test]public void ProjectileGeometryScalingPreservesSourceAndExtendsBlastRadius(){var original=new RangedProjectileDefinition(new RangedData{type="ball",distance=10,radius=1,blastRadius=2});var scaled=original.WithGeometry(1.4f,1.35f);Assert.That(scaled.Distance,Is.EqualTo(14).Within(.001));Assert.That(scaled.BlastRadius,Is.EqualTo(2.7).Within(.001));Assert.AreEqual(2,original.BlastRadius);var bomb=new BombProjectileDefinition(new BombData{radius=.5f,blastRadius=2,lifetime=3});Assert.That(bomb.WithRadius(1.35f).BlastRadius,Is.EqualTo(2.7).Within(.001));}
  [Test]public void AttackSnapshotKeepsPartAndLevelWhenSlotChanges(){var g=Make();var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Insert(w,0,g.GrantInk("ink3_red_01").Id);var snap=g.SnapshotSlots(w);g.Economy.Earn(1000);g.Upgrade(w,0);g.Insert(w,0,g.GrantInk("ink3_red_03").Id);Assert.AreEqual("ink3_red_01",snap[0].Part.id);Assert.AreEqual(0,snap[0].Level);Assert.AreNotEqual(p.Slots[0].InstanceId,snap[0].InstanceId);}
  [Test]public void DeparturePalettesUseThePlannedSlotsAndOnlyTheUnlockedFirstWildcard(){for(int n=3;n<=5;n++)for(int seed=0;seed<40;seed++){var g=Make();var w1=Guid.NewGuid();var w2=Guid.NewGuid();var a=g.EquipStartPalette(w1,n,true,new System.Random(seed));var b=g.EquipStartPalette(w2,n,false,new System.Random(seed+1000));Assert.AreEqual(n,a.Slots.Count);Assert.AreEqual(n,b.Slots.Count);Assert.AreEqual("colorless",a.Slots[0].Color);for(int i=1;i<n;i++)Assert.AreNotEqual("colorless",a.Slots[i].Color);for(int i=0;i<n;i++)Assert.AreNotEqual("colorless",b.Slots[i].Color);Assert.AreSame(a,g.Equipped(w1));Assert.AreSame(b,g.Equipped(w2));Assert.Throws<InvalidOperationException>(()=>g.EquipStartPalette(w1,n,false,new System.Random(1)),"one palette per weapon");}}
  // Every rolled start palette is accepted: the old 3-slot single-colour / 4-slot two-by-two check refused almost all of them.
  [Test]public void EveryRolledStartPaletteCanBeEquipped(){var g=Make();for(int n=3;n<=5;n++)for(int seed=0;seed<60;seed++){var p=g.EquipStartPalette(Guid.NewGuid(),n,seed%2==0,new System.Random(seed));Assert.AreEqual(n,p.Slots.Count);}}
  [Test]public void DeparturePalettesRollEachSlotColourInsteadOfFixedPatterns(){var g=Make();for(int n=3;n<=4;n++){int mixed=0,unpaired=0;for(int seed=0;seed<40;seed++){var c=g.RollStartColors(n,false,new System.Random(seed));Assert.AreEqual(n,c.Length);Assert.That(c,Has.None.EqualTo("colorless"));if(c.Distinct().Count()>1)mixed++;if(n==4&&!(c[0]==c[1]&&c[2]==c[3]))unpaired++;}Assert.Greater(mixed,30,n+"-slot start colours vary per slot");if(n==4)Assert.Greater(unpaired,20,"4-slot start colours are not the old two-by-two pattern");}}
  [Test]public void ExplicitDestroyPaysTheRecoveryBonusOncePerInk(){var g=Make();g.DestroyedInkGoldBonus=20;var one=g.GrantInk("ink3_red_01");Assert.AreEqual(20,g.Destroy(one.Id));Assert.AreEqual(20,g.Economy.Gold);Assert.Throws<InvalidOperationException>(()=>g.Destroy(one.Id),"a destroyed ink cannot be destroyed again");Assert.AreEqual(20,g.Economy.Gold);for(int i=0;i<3;i++)g.Destroy(g.GrantInk("ink3_blue_02").Id);Assert.AreEqual(80,g.Economy.Gold,"three different inks pay 60");Assert.AreEqual(4,g.DestroyRewardsPaid);}
  // The first five colour inks of a run, from any source, use only the colours on the two palettes; then all five.
  [Test]public void EarlyColourInksUseOnlyThePalettesColoursThenAllFive(){var g=Make();var r=new System.Random(11);var w1=Guid.NewGuid();var w2=Guid.NewGuid();g.Bind(g.GrantPalette(new[]{"red","red","red"}).Id,w1);g.Bind(g.GrantPalette(new[]{"colorless","blue","blue"}).Id,w2);Assert.AreEqual(5,g.Data.earlyOwnedColorInks);g.Economy.Earn(10000);var early=new System.Collections.Generic.List<string>();early.Add(g.ChoosePart("normal",r).color);early.AddRange(g.PlanReward("elite",r).Take(4-0).Select(id=>Array.Find(g.Data.parts,x=>x.id==id).color));while(g.ColorInksRolled<5)early.Add(g.ChoosePart("normal",r).color);early=early.Take(5).ToList();Assert.AreEqual(5,early.Count);Assert.That(early.All(c=>c=="red"||c=="blue"),Is.True,"drops share the five and stay on red/blue (colourless is not a colour): "+string.Join(",",early));var later=Enumerable.Range(0,500).Select(_=>g.ChoosePart("elite",r).color).ToArray();CollectionAssert.AreEquivalent(new[]{"red","yellow","green","blue","purple"},later.Distinct().ToArray(),"from the sixth ink every colour can appear");foreach(var c in new[]{"red","yellow","green","blue","purple"})Assert.That(later.Count(x=>x==c),Is.InRange(60,140),"colours are equally likely: "+c);}
  [Test]public void TheEarlyLimitFollowsThePalettesCurrentColours(){var g=Make();var r=new System.Random(5);var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Economy.Earn(1000);g.Insert(w,0,g.GrantInk("ink3_red_01").Id);Assert.AreEqual("red",g.ChoosePart("normal",r).color);g.Recolor(w,0,"blue",g.GrantInk("ink3_blue_02").Id);var seen=Enumerable.Range(0,3).Select(_=>g.ChoosePart("elite",r).color).ToArray();Assert.That(seen.All(c=>c=="red"||c=="blue"),Is.True,"the recoloured blue joins the allowed colours: "+string.Join(",",seen));Assert.AreEqual(4,g.ColorInksRolled);}
  [Test]public void EarlyGradesFollowTheSourceAmongTheColoursGrades(){var g=Make();var r=new System.Random(2);var w=Guid.NewGuid();g.Bind(g.GrantPalette(new[]{"purple","purple","purple"}).Id,w);var part=g.ChoosePart("boss",r);Assert.AreEqual("purple",part.color);var grades=g.Data.parts.Where(x=>x.color=="purple").Select(x=>x.grade).ToArray();CollectionAssert.Contains(grades,part.grade,"only a grade that purple has");}
  [Test]public void AltarOffersUpToThreeDifferentCandidatesOfTheColourWithoutOwnedUniqueInks(){var g=Make();var r=new System.Random(4);var red=g.Data.parts.Where(x=>x.color=="red").ToArray();var unique=red.FirstOrDefault(x=>!x.duplicate);if(unique!=null)g.GrantInk(unique.id);for(int n=0;n<50;n++){var c=g.AltarCandidates("red",r);Assert.That(c.Length,Is.EqualTo(Math.Min(3,red.Count(x=>x.duplicate||x!=unique))));Assert.That(c.Distinct().Count(),Is.EqualTo(c.Length),"different candidates");Assert.That(c.All(id=>red.Any(x=>x.id==id)),Is.True,"all of the chosen colour");if(unique!=null)CollectionAssert.DoesNotContain(c,unique.id,"an owned unique ink is not offered");}Assert.AreEqual(0,g.ColorInksRolled,"altar candidates are outside the early colour count");Assert.Throws<InvalidOperationException>(()=>g.AltarCandidates("colorless",r));}
  [Test]public void TheBossDropsNoColourInksBecauseTheAltarReplacesThem(){var g=Make();var r=new System.Random(9);for(int i=0;i<50;i++)Assert.That(g.PlanReward("boss",r),Is.Empty);}
  [Test]public void RecolorHasARunWideAllowanceSharedByBothPalettes(){var g=Make();var w1=Guid.NewGuid();var w2=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});var q=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w1);g.Bind(q.Id,w2);g.Economy.Earn(1000);g.Insert(w1,0,g.GrantInk("ink3_red_01").Id);g.Insert(w1,1,g.GrantInk("ink3_red_03").Id);g.Insert(w2,0,g.GrantInk("ink3_red_01").Id);Assert.AreEqual(2,g.Data.colorChangesPerRun);Assert.AreEqual(100,g.Data.colorChangeGold);g.Recolor(w1,0,"blue",g.GrantInk("ink3_blue_02").Id);g.Recolor(w2,0,"blue",g.GrantInk("ink3_blue_02").Id);Assert.AreEqual(800,g.Economy.Gold,"two changes at 100G each");Assert.AreEqual(0,g.ColorChangesLeft);var spare=g.GrantInk("ink3_blue_02");Assert.Throws<InvalidOperationException>(()=>g.Recolor(w1,1,"blue",spare.Id),"a third change in the run is refused");Assert.AreEqual(800,g.Economy.Gold,"a refused change costs nothing");Assert.AreEqual("red",p.Slots[1].Color);}
  [Test]public void OnlyExplicitDestroyPaysTheRecoveryBonus(){var g=Make();g.DestroyedInkGoldBonus=20;var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Economy.Earn(1000);var first=g.GrantInk("ink3_red_01");g.Insert(w,0,first.Id);Assert.AreEqual(1000,g.Economy.Gold,"filling an empty slot destroys nothing");g.Insert(w,0,g.GrantInk("ink3_red_03").Id);Assert.AreEqual(1000,g.Economy.Gold,"a free replacement returns the old part instead of destroying it");Assert.IsTrue(g.Inks.Any(i=>i.Id==first.Id),"the replaced part is owned again");int gold=g.Economy.Gold;g.Sell(g.GrantInk("ink3_red_01").Id);Assert.AreEqual(gold+g.SalePrice,g.Economy.Gold,"a sale pays only its price");gold=g.Economy.Gold;var mounted=p.Slots[0].InstanceId;g.Recolor(w,0,"blue",g.GrantInk("ink3_blue_02").Id);Assert.AreEqual(gold-g.Data.colorChangeGold,g.Economy.Gold,"recolor detaches without a reward");Assert.IsTrue(g.Inks.Any(i=>i.Id==mounted&&i.Part.id=="ink3_red_03"),"the detached part returns with its identity");}
  [Test]public void WithoutTheUnlockDestroyingPaysNothing(){var g=Make();g.Destroy(g.GrantInk("ink3_red_01").Id);Assert.AreEqual(0,g.Economy.Gold);Assert.AreEqual(0,g.DestroyRewardsPaid);}
  [Test]public void KillRewardsNeverContainPalettes(){var g=Make();var r=new System.Random(3);for(int i=0;i<200;i++)foreach(var source in new[]{"normal","elite","boss"})foreach(var id in g.PlanReward(source,r))Assert.IsNotNull(Array.Find(g.Data.parts,p=>p.id==id));}
  [Test]public void SlotFiveIsMaximumAndAttackLevelUsesSharedPrice(){var g=Make();var w=Guid.NewGuid();var p=g.GrantPalette(new[]{"red","red","red"});g.Bind(p.Id,w);g.Economy.Earn(2000);for(int i=0;i<5;i++)g.Upgrade(w,0);Assert.Throws<InvalidOperationException>(()=>g.Upgrade(w,0));Assert.AreEqual(720,g.Economy.Gold);g.UpgradeAttack();Assert.That(g.AttackMultiplier,Is.EqualTo(1.05f).Within(.0001));}
 }
}
