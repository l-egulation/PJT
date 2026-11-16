using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Run.Tests
{
    public sealed class WorkshopTreeLayoutTests
    {
        static ProgressionCatalog Catalog() => new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json")));

        [Test] public void BranchTabsFollowTheDesignOrderAndHoldEveryUnlockOnce()
        {
            var catalog = Catalog();
            var tabs = WorkshopTreeLayout.Branches(catalog);
            Assert.That(tabs.Select(b => b.name), Is.EqualTo(new[] { "전투", "기동", "경제", "선택", "잉크 연구" }));
            var own = tabs.SelectMany(b => WorkshopTreeLayout.For(catalog, b.id).Nodes.Where(n => !n.Foreign)).SelectMany(n => n.Levels).Select(u => u.id).ToArray();
            Assert.That(own, Is.EquivalentTo(catalog.Unlocks.Select(u => u.id)), "each unlock is a level of one node in exactly one tab");
        }

        [Test] public void TiersFormNodesOfFourAndSingleUnlocksStandAlone()
        {
            var catalog = Catalog();
            var combat = WorkshopTreeLayout.For(catalog, "combat");
            var attack1 = combat.Find("attack_bonus_3");
            Assert.That(attack1.Name, Is.EqualTo("공격력 I"));
            Assert.That(attack1.Levels.Select(u => u.id), Is.EqualTo(new[] { "attack_bonus_1", "attack_bonus_2", "attack_bonus_3", "attack_bonus_4" }));
            Assert.That(combat.Find("attack_bonus_8").Name, Is.EqualTo("공격력 II"));
            Assert.That(combat.Find("recovery_uses_2").Levels.Count, Is.EqualTo(2));
            Assert.That(combat.Find("revive_1").Name, Is.EqualTo("부활"));
            var choice = WorkshopTreeLayout.For(catalog, "choice");
            Assert.That(choice.Find("shared_rerolls_5").Name, Is.EqualTo("공용 리롤 II"), "a remainder starts the next numeral");
            Assert.That(choice.Find("shared_rerolls_5").Levels.Count, Is.EqualTo(1));
            Assert.That(choice.Find("augment_bundle_burst").Levels.Count, Is.EqualTo(1), "bundles are separate contents, not tiers");
            var has = new System.Collections.Generic.HashSet<string> { "attack_bonus_1", "attack_bonus_2" };
            Assert.That(attack1.Owned(has.Contains), Is.EqualTo(2));
            Assert.That(attack1.Current(has.Contains).id, Is.EqualTo("attack_bonus_3"));
            Assert.That(attack1.Complete(has.Contains), Is.False);
        }

        [Test] public void EveryLinkLeavesACompleteNodeFromALeftColumn()
        {
            var catalog = Catalog();
            foreach (var tab in WorkshopTreeLayout.Branches(catalog))
            {
                var tree = WorkshopTreeLayout.For(catalog, tab.id);
                foreach (var node in tree.Nodes.Where(n => !n.Foreign))
                {
                    // Inside a node a tier follows the one before; a link from another node enters at the first tier
                    // and needs that node's last tier.
                    for (int i = 1; i < node.Levels.Count; i++) Assert.That(node.Levels[i].prerequisites, Is.EqualTo(new[] { node.Levels[i - 1].id }));
                    foreach (var id in node.Levels[0].prerequisites)
                    {
                        var edge = tree.Edges.Single(e => e.To == node && e.From.Contains(id));
                        Assert.That(edge.From.Last.id, Is.EqualTo(id), node.Name + " waits on the whole " + edge.From.Name);
                        Assert.That(edge.From.Column, Is.LessThan(node.Column), node.Name + " stands right of " + edge.From.Name);
                    }
                }
                Assert.That(tree.Nodes.GroupBy(n => (n.Row, n.Column)).All(g => g.Count() == 1), tab.name + ": one node per cell");
            }
        }

        [Test] public void CombatDrawsOneRowPerLineAndCrossLinksShareTheColumnStep()
        {
            var catalog = Catalog();
            var tree = WorkshopTreeLayout.For(catalog, "combat");
            Assert.That(tree.Rows.Select(r => r.First.id), Is.EqualTo(new[] { "attack_bonus_1", "max_health_bonus_1", "recovery_uses_1", "revive_1" }));
            Assert.That(tree.Columns, Is.EqualTo(2));
            Assert.That(tree.Nodes.Count, Is.EqualTo(6));
            var attack2 = tree.Find("attack_bonus_5");
            Assert.That((attack2.Row, attack2.Column), Is.EqualTo((0, 1)));
            Assert.That(tree.Edges.Where(e => e.To == attack2).Select(e => e.From.Name), Is.EquivalentTo(new[] { "공격력 I", "최대 체력 I" }));
            Assert.That(tree.Find("recovery_uses_1").Column, Is.EqualTo(1), "the recovery stand follows the whole health node");
        }

        [Test] public void AnotherBranchPrerequisiteIsAForeignNodeBelowTheBranchLines()
        {
            var catalog = Catalog();
            var tree = WorkshopTreeLayout.For(catalog, "mobility");
            var foreign = tree.Find("attack_bonus_4");
            Assert.That(foreign.Foreign, Is.True);
            Assert.That(foreign.Name, Is.EqualTo("공격력 I"));
            Assert.That(tree.Rows[foreign.Row].Foreign, Is.True);
            Assert.That(tree.Rows[foreign.Row].ForeignBranches, Is.EqualTo(new[] { "combat" }));
            Assert.That(foreign.Row, Is.GreaterThan(tree.Nodes.Where(n => !n.Foreign).Max(n => n.Row)));
            Assert.That(tree.Edges.Any(e => e.From == foreign && e.To.Id == "extra_dash_1"));
            Assert.That(tree.Find("extra_dash_1").Column, Is.EqualTo(1));
        }

        [Test] public void WithoutNodeDataEveryUnlockIsItsOwnNode()
        {
            var data = JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json"));
            foreach (var u in data.unlocks) u.node = null;
            var catalog = new ProgressionCatalog(data);
            var tree = WorkshopTreeLayout.For(catalog, "combat");
            Assert.That(tree.Nodes.All(n => n.Levels.Count == 1));
            Assert.That(tree.Find("attack_bonus_2").Name, Is.EqualTo(catalog.Find("attack_bonus_2").name));
        }

        [Test] public void WithoutBranchDataEveryUnlockStandsInOneTree()
        {
            var data = JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json"));
            data.branches = null;
            var catalog = new ProgressionCatalog(data);
            var tabs = WorkshopTreeLayout.Branches(catalog);
            Assert.That(tabs.Count, Is.EqualTo(1));
            var tree = WorkshopTreeLayout.For(catalog, tabs[0].id);
            Assert.That(tree.Nodes.Sum(n => n.Levels.Count), Is.EqualTo(catalog.Unlocks.Count));
            Assert.That(tree.Nodes.Any(n => n.Foreign), Is.False);
        }

        [Test] public void ArrowsReachAllAuthoredNodesIncludingDiagonalAndForeignRows()
        {
            var catalog=Catalog();
            foreach(var branch in Branches())
            {
                var tree=WorkshopTreeLayout.For(catalog,branch);
                var seen=new System.Collections.Generic.HashSet<WorkshopTreeLayout.Node>();
                var pending=new System.Collections.Generic.Queue<WorkshopTreeLayout.Node>();pending.Enqueue(tree.Nodes.First(n=>!n.Foreign));
                while(pending.Count>0)
                {
                    var node=pending.Dequeue();if(!seen.Add(node))continue;
                    foreach(var direction in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                    {var next=tree.Neighbor(node,direction.Item1,direction.Item2);if(next!=null)pending.Enqueue(next);}
                }
                Assert.That(seen,Is.EquivalentTo(tree.Nodes),branch+" has no keyboard-isolated nodes");
            }
            var economy=WorkshopTreeLayout.For(catalog,"economy");
            Assert.That(economy.Neighbor(economy.Find("starting_gold_50"),1,0).Id,Is.EqualTo("kill_gold_1"));
            var mobility=WorkshopTreeLayout.For(catalog,"mobility");
            Assert.That(mobility.Neighbor(mobility.Find("extra_dash_1"),0,1).Foreign,Is.True);
            string[] Branches()=>WorkshopTreeLayout.Branches(catalog).Select(b=>b.id).ToArray();
        }

        [Test] public void CatalogRejectsAnUnknownBranch()
        {
            var data = JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json"));
            data.unlocks[0].branch = "nowhere";
            Assert.That(ProgressionCatalog.Validate(data), Has.Some.Contains("unknown branch"));
        }
    }
}
