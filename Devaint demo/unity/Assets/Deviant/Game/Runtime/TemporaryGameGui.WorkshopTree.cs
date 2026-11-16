using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Input.Contracts;
using Deviant.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Deviant.UI.Theme;
using ForestCombatV2;

namespace Deviant.Game
{
    // The artisan workshop's left page: branch tabs over the unlock tree of WorkshopTreeLayout
    // (docs/development/presentation/ui/temporary-gui.md, 장인의 공방). One node per group of tiers ("공격력 I" =
    // attack 1-4) with its bought/total count inside and its name below, one row per tier line, lines from each
    // prerequisite node. The selection is the selected node's next tier (hubSelected), which the right page details.
    public sealed partial class TemporaryGameGui
    {
        Button[] benchTabs = Array.Empty<Button>();
        string benchBranch, benchBuiltBranch;
        WorkshopTreeLayout benchTree;
        readonly Dictionary<string, Button> benchNodes = new Dictionary<string, Button>();
        const float BenchNode = 32, BenchRing = 40, BenchMaxColumn = 116, BenchMaxRow = 84, BenchLabelH = 16, BenchCaptionH = 26, BenchLine = 2, BenchTabH = 32;
        static readonly Color BenchSurface = new Color32(36, 39, 43, 255), BenchOnGold = new Color32(36, 35, 29, 255);
        static bool BenchHas(string id) => Profile.Snapshot.Has(id);

        void BuildBenchTabs()
        {
            var tabs = WorkshopTreeLayout.Branches(Profile.Catalog);
            const float gap = 4; float w = Mathf.Floor((ColumnW - gap * (tabs.Count - 1)) / tabs.Count);
            benchTabs = new Button[tabs.Count];
            for (int i = 0; i < tabs.Count; i++)
            {
                string id = tabs[i].id;
                var tab = Action(tabs[i].name, workbench.transform, LeftX + i * (w + gap), TopY - 4, w, BenchTabH, () => SetBenchBranch(id, true));
                tab.name = "Branch " + (id.Length > 0 ? id : "all");
                tab.GetComponentInChildren<Text>().fontSize = 15;
                benchTabs[i] = tab;
            }
        }

        // Branch focus previews that page; Down enters its next purchase without an extra confirmation.
        void SetBenchBranch(string branch, bool focus)
        {
            if (branch == benchBranch) { benchFocusPending |= focus; return; }
            benchBranch = branch; benchDirty = true; hubMessage = "";
            var tree = WorkshopTreeLayout.For(Profile.Catalog, branch);
            var own = tree.Nodes.Where(n => !n.Foreign).OrderBy(n => n.Row).ThenBy(n => n.Column).ToArray();
            var pick = own.FirstOrDefault(n => Profile.StatusOf(n.Current(BenchHas).id).CanBuy) ?? own.FirstOrDefault(n => !n.Complete(BenchHas)) ?? own.FirstOrDefault();
            if (pick != null) hubSelected = pick.Current(BenchHas).id;
            benchFocusPending = focus;
            PresentationSfx.PlaySelected("sfx_ui_tab");
        }
        bool benchFocusPending, benchNodeFocusPending;
        bool benchTabsFocused = true;

        void StepBenchBranch(int delta)
        {
            var tabs = WorkshopTreeLayout.Branches(Profile.Catalog);
            int at = Mathf.Max(0, tabs.ToList().FindIndex(b => b.id == benchBranch));
            SetBenchBranch(tabs[(at + delta + tabs.Count) % tabs.Count].id, true);
        }

        // Q/E and the shoulders switch branches while the workshop is open; LB is also the exploration bag key.
        bool TickBenchTabInput()
        {
            if (hubOpen != workbench || benchTabs.Length < 2) return false;
            if (LivePreviousTabPressed) { if (Gamepad.current?.leftShoulder.wasPressedThisFrame == true) Controls?.Take(GameAction.Inventory); StepBenchBranch(-1); return true; }
            if (LiveNextTabPressed) { StepBenchBranch(1); return true; }
            return false;
        }

        // The node whose tiers include the selection.
        WorkshopTreeLayout.Node BenchSelectedNode => benchTree?.Nodes.FirstOrDefault(n => n.Contains(hubSelected ?? ""));
        Button BenchSelectedButton => BenchSelectedNode is { } node && benchNodes.TryGetValue(node.Id, out var b) ? b : null;

        void RefreshBenchTree()
        {
            var catalog = Profile.Catalog;
            var focused = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            int focusedTab = Array.FindIndex(benchTabs, b => b && b.gameObject == focused);
            if (focusedTab >= 0 && !benchFocusPending && !benchNodeFocusPending)
            {
                benchTabsFocused = true;
                var branch = WorkshopTreeLayout.Branches(catalog)[focusedTab].id;
                if (branch != benchBranch) SetBenchBranch(branch, false);
            }
            else if (focused && (focused.transform.IsChildOf(benchItems) || focused == benchBuy.gameObject))
                benchTabsFocused = false;
            if (benchBranch == null || WorkshopTreeLayout.Branches(catalog).All(b => b.id != benchBranch))
                benchBranch = WorkshopTreeLayout.BranchOf(catalog, catalog.Find(hubSelected) ?? catalog.Unlocks[0]);
            if ((benchDirty || benchBuiltBranch != benchBranch || benchTree == null) &&
                focused && focused.transform.parent == benchItems && !benchFocusPending)
                benchNodeFocusPending = true;
            if (benchDirty || benchBuiltBranch != benchBranch || benchTree == null) BuildBenchTree();
            // Keyboard/pad focus on a node is the selection: the focused node's next tier.
            if (focused && focused.transform.parent == benchItems && !benchFocusPending && !benchNodeFocusPending)
            {
                var node = benchTree.Nodes.FirstOrDefault(n => benchNodes.TryGetValue(n.Id, out var b) && b.gameObject == focused);
                if (node != null && !node.Contains(hubSelected ?? "")) { hubSelected = node.Current(BenchHas).id; hubMessage = ""; }
            }
            if (benchFocusPending) { benchFocusPending = false; FocusBenchTab(); }
            if (benchNodeFocusPending && EventSystem.current && BenchSelectedButton is { } target)
            { benchNodeFocusPending = false; benchTabsFocused = false; EventSystem.current.SetSelectedGameObject(target.gameObject); }
            PaintBenchTree();
            SetBenchContentFocus();
        }

        static string BenchNodeName(WorkshopTreeLayout.Node node) => "Node " + node.Id + (node.Foreign ? " (선행)" : "");

        void BuildBenchTree()
        {
            benchDirty = false; benchBuiltBranch = benchBranch; benchNodes.Clear();
            // Retire the old page before rebuilding so this frame's updates address the new nodes.
            for (int i = benchItems.childCount - 1; i >= 0; i--)
            { var old = benchItems.GetChild(i); old.gameObject.SetActive(false); old.SetParent(null, false); Destroy(old.gameObject); }
            var tree = benchTree = WorkshopTreeLayout.For(Profile.Catalog, benchBranch);
            float viewH = ((RectTransform)benchItems.parent).rect.height; if (viewH <= 0) viewH = 326;
            benchItems.sizeDelta = new Vector2(ColumnW, viewH); benchItems.anchoredPosition = Vector2.zero;
            if (tree.Nodes.Count == 0) return;
            float colW = Mathf.Min(BenchMaxColumn, Mathf.Floor(ColumnW / tree.Columns)), rowH = Mathf.Min(BenchMaxRow, Mathf.Floor(viewH / tree.Rows.Count));
            float gridX = Mathf.Round((ColumnW - colW * tree.Columns) / 2);
            // A foreign row carries its label (which branch the prerequisite comes from) above its nodes.
            // Leave room for the focus ring outside the node, inside the viewport mask.
            float Top(int row) => rowH * row + (tree.Rows[row].Foreign ? BenchLabelH + 2 : (BenchRing - BenchNode) / 2 + 1);
            Vector2 Centre(WorkshopTreeLayout.Node n) => new Vector2(gridX + colW * (n.Column + .5f), Top(n.Row) + BenchNode / 2);
            // Lines first so the nodes cover their ends. A line leaves the prerequisite to the right, turns in the gap
            // after it and runs along the target's row.
            var lines = Rect("Tree lines", benchItems, 0, 0, ColumnW, viewH);
            foreach (var edge in tree.Edges)
            {
                var a = Centre(edge.From); var b = Centre(edge.To); float turn = a.x + colW / 2, half = BenchLine / 2;
                var seg = Rect("Line " + edge.From.Id + ">" + edge.To.Id, lines, 0, 0, ColumnW, viewH);
                if (Mathf.Approximately(a.y, b.y)) Fill(seg, a.x, a.y - half, b.x - a.x, BenchLine, Color.white);
                else
                {
                    Fill(seg, a.x, a.y - half, turn - a.x + half, BenchLine, Color.white);
                    Fill(seg, turn - half, Mathf.Min(a.y, b.y) - half, BenchLine, Mathf.Abs(b.y - a.y) + BenchLine, Color.white);
                    Fill(seg, turn - half, b.y - half, b.x - turn + half, BenchLine, Color.white);
                }
            }
            for (int r = 0; r < tree.Rows.Count; r++)
            {
                var row = tree.Rows[r]; if (!row.Foreign) continue;
                var first = tree.Nodes.Where(n => n.Row == r).OrderBy(n => n.Column).FirstOrDefault(); if (first == null) continue;
                var label = Words(BenchForeignLabel(row), benchItems, gridX + colW * first.Column + (colW - BenchNode) / 2, rowH * r, ColumnW, BenchLabelH, 13, false, JMuted);
                label.name = "Row " + r;
            }
            foreach (var node in tree.Nodes)
            {
                var c = Centre(node); string id = node.Id; var levels = node.Levels;
                var face = Rounded(BenchNodeName(node), benchItems, c.x - BenchNode / 2, c.y - BenchNode / 2, BenchNode, BenchNode, 7, 2, BenchSurface, ApprovedTheme.Muted, true);
                var ring = Rounded("Focus", face.transform, -(BenchRing - BenchNode) / 2, -(BenchRing - BenchNode) / 2, BenchRing, BenchRing, 9, 2, Color.clear, ApprovedTheme.Paper);
                ring.enabled = false;
                var count = Words("", face.transform, 0, 0, BenchNode, BenchNode, 12, true, JInk, TextAnchor.MiddleCenter); count.name = "Count";
                var dot = Glyph(Disc(8), face.transform, (BenchNode - 8) / 2, (BenchNode - 8) / 2, 8, 8, ApprovedTheme.Paper); dot.name = "Single"; dot.enabled = levels.Count <= 1;
                Glyph(LockGlyph, face.transform, BenchNode - 8, BenchNode - 9, 9, 11, ApprovedTheme.Muted).name = "Lock";
                // Measure the name gap from the outer selection ring, not the inner node.
                float captionY = BenchNode + (BenchRing - BenchNode) / 2 + 6;
                var caption = Words(node.Name, face.transform, -(colW - 4 - BenchNode) / 2, captionY, colW - 4, BenchCaptionH, 12, false, JMuted, TextAnchor.UpperCenter);
                caption.name = "Caption"; caption.horizontalOverflow = HorizontalWrapMode.Wrap; caption.lineSpacing = .9f;
                caption.rectTransform.sizeDelta = new Vector2(colW - 4, Mathf.Min(BenchCaptionH, Mathf.Max(14, Mathf.Ceil(caption.preferredHeight))));
                var button = face.gameObject.AddComponent<WorkshopNodeButton>(); button.targetGraphic = face; button.transition = Selectable.Transition.None;
                string branch = WorkshopTreeLayout.BranchOf(Profile.Catalog, levels[0]);
                if (node.Foreign)
                {
                    // A prerequisite stays reachable by arrows; F or click opens its own branch.
                    button.Submit=()=>button.onClick.Invoke();
                    button.onClick.AddListener(() => { SetBenchBranch(branch, false); hubSelected = node.Current(BenchHas).id; benchNodeFocusPending = true; RefreshHub(); });
                }
                else
                {
                    // A click also takes focus, since focus is the selection (RefreshBenchTree).
                    button.onClick.AddListener(() =>
                    {
                        if (!node.Contains(hubSelected ?? "")) PresentationSfx.PlaySelected("sfx_ui_select");
                        benchTabsFocused = false;
                        hubSelected = node.Current(BenchHas).id; hubMessage = "";
                        if (EventSystem.current && button) EventSystem.current.SetSelectedGameObject(button.gameObject);
                        RefreshHub();
                    });
                    button.Submit = () =>
                    {
                        if(benchTabsFocused)return;
                        hubSelected=node.Current(BenchHas).id;
                        if(Profile.StatusOf(hubSelected).CanBuy)BuySelected();
                        RefreshHub();
                    };
                }
                benchNodes[id] = button;
            }
        }

        string BenchForeignLabel(WorkshopTreeLayout.Row row)
        {
            var names = row.ForeignBranches.Select(id => WorkshopTreeLayout.Branches(Profile.Catalog).FirstOrDefault(b => b.id == id)?.name).Where(n => n != null).ToArray();
            return names.Length == 1 ? names[0] + " 선행" : "다른 줄기 선행";
        }

        // Node faces follow the next tier: complete gold, buyable gold edge, open-but-short paper edge, locked muted
        // with a lock; a node of several tiers shows bought/total inside. The selected node carries a paper ring and a
        // paper caption. A line is gold once its prerequisite node is complete.
        void PaintBenchTree()
        {
            var tabs = WorkshopTreeLayout.Branches(Profile.Catalog);
            for (int i = 0; i < benchTabs.Length && i < tabs.Count; i++)
            {
                bool active = tabs[i].id == benchBranch;
                var visual = benchTabs[i].GetComponent<ApprovedActionVisual>();
                if (visual) { visual.PersistentSelection = active; visual.NormalColor = active ? ApprovedTheme.Paper : ApprovedTheme.Muted; }
                else benchTabs[i].GetComponentInChildren<Text>().color = active ? ApprovedTheme.Paper : ApprovedTheme.Muted;
            }
            if (benchTree == null) return;
            var lines = benchItems.Find("Tree lines");
            foreach (var edge in benchTree.Edges)
            {
                var seg = lines ? lines.Find("Line " + edge.From.Id + ">" + edge.To.Id) : null; if (!seg) continue;
                var color = edge.From.Complete(BenchHas) ? new Color(ApprovedTheme.Gold.r, ApprovedTheme.Gold.g, ApprovedTheme.Gold.b, .9f) : new Color(ApprovedTheme.Muted.r, ApprovedTheme.Muted.g, ApprovedTheme.Muted.b, .35f);
                foreach (var part in seg.GetComponentsInChildren<Image>()) part.color = color;
            }
            foreach (var node in benchTree.Nodes)
            {
                var face = benchItems.Find(BenchNodeName(node))?.GetComponent<Image>(); if (!face) continue;
                var status = Profile.StatusOf(node.Current(BenchHas).id);
                bool owned = node.Complete(BenchHas), locked = Locked(status.State), ready = status.CanBuy, chosen = !benchTabsFocused && node.Contains(hubSelected ?? "");
                Color fill = owned ? ApprovedTheme.Gold : BenchSurface;
                Color edge = owned || ready ? ApprovedTheme.Gold : locked || node.Foreign ? new Color(ApprovedTheme.Muted.r, ApprovedTheme.Muted.g, ApprovedTheme.Muted.b, .45f) : new Color(ApprovedTheme.Paper.r, ApprovedTheme.Paper.g, ApprovedTheme.Paper.b, .75f);
                Color ink = owned ? BenchOnGold : ready ? ApprovedTheme.Gold : locked || node.Foreign ? new Color(ApprovedTheme.Muted.r, ApprovedTheme.Muted.g, ApprovedTheme.Muted.b, .7f) : ApprovedTheme.Paper;
                face.sprite = RoundedSprite(7, 2, fill, edge);
                var count = face.transform.Find("Count").GetComponent<Text>();
                count.text = node.Levels.Count > 1 ? node.Owned(BenchHas) + "/" + node.Levels.Count : ""; count.color = ink;
                face.transform.Find("Single").GetComponent<Image>().color = ink;
                var lockIcon = face.transform.Find("Lock").GetComponent<Image>(); lockIcon.enabled = locked && !owned; lockIcon.color = ink;
                face.transform.Find("Focus").GetComponent<Image>().enabled = chosen;
                face.transform.Find("Caption").GetComponent<Text>().color = chosen ? new Color32(217,203,132,255) : ApprovedTheme.Muted;
            }
        }

        // Keep every authored node in place; arrow neighbors include diagonal steps and foreign prerequisites.
        void NavigateBenchTree(bool ready)
        {
            Button selected = BenchSelectedButton ?? benchNodes.Values.FirstOrDefault();
            var tabs = WorkshopTreeLayout.Branches(Profile.Catalog);
            int active = Mathf.Max(0, tabs.ToList().FindIndex(b => b.id == benchBranch));
            Button activeTab = active < benchTabs.Length ? benchTabs[active] : null;
            if (benchTree != null)
            {
                Button At(WorkshopTreeLayout.Node n)=>n!=null&&benchNodes.TryGetValue(n.Id,out var button)?button:null;
                foreach(var n in benchTree.Nodes)
                {
                    var right=At(benchTree.Neighbor(n,1,0));
                    benchNodes[n.Id].navigation=new Navigation {
                        mode=Navigation.Mode.Explicit,
                        selectOnLeft=At(benchTree.Neighbor(n,-1,0)),selectOnRight=right?right:ready?benchBuy:null,
                        selectOnUp=At(benchTree.Neighbor(n,0,-1))??activeTab,selectOnDown=At(benchTree.Neighbor(n,0,1))};
                }
            }
            for (int i = 0; i < benchTabs.Length; i++)
                benchTabs[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit, selectOnLeft = i > 0 ? benchTabs[i - 1] : null,
                    selectOnRight = i + 1 < benchTabs.Length ? benchTabs[i + 1] : null, selectOnDown = selected,
                };
            var back = selected ? selected : activeTab;
            benchBuy.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = back, selectOnUp = back };
            benchClose.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = back, selectOnLeft = benchTabs.Length > 0 ? benchTabs[benchTabs.Length - 1] : back };
        }

        void FocusBenchTab()
        {
            benchTabsFocused = true;
            var branches = WorkshopTreeLayout.Branches(Profile.Catalog);
            int index = Mathf.Max(0, branches.ToList().FindIndex(b => b.id == benchBranch));
            if (EventSystem.current && index < benchTabs.Length)
                EventSystem.current.SetSelectedGameObject(benchTabs[index].gameObject);
        }

        void SetBenchContentFocus()
        {
            void Fade(GameObject content)
            {
                if (!content) return;
                var group = content.GetComponent<CanvasGroup>();
                // Unity's missing-component object must use its overloaded null check, not CLR ??.
                if (!group) group = content.AddComponent<CanvasGroup>();
                group.alpha = benchTabsFocused ? .3f : 1;
                // Leave uGUI keyboard navigation available so Down can enter the node tree.
                group.blocksRaycasts = !benchTabsFocused;
            }
            Fade(benchItems.parent.gameObject);
            Fade(benchDetails.parent.gameObject);
            Fade(benchName.gameObject);
            Fade(benchBuy.gameObject);
            Fade(benchMessage.gameObject);
        }

        // The next tier of the node holding this unlock: what the right page details and the buy button takes.
        static string BenchNodeCurrent(string unlockId)
        {
            var levels = WorkshopTreeLayout.Levels(Profile.Catalog, Profile.Catalog.Find(unlockId));
            return (levels.FirstOrDefault(l => !BenchHas(l.id)) ?? levels.LastOrDefault())?.id ?? unlockId;
        }
    }
    // Pointer click selects a node; menu submit unlocks that already-focused node exactly once.
    public sealed class WorkshopNodeButton : Button
    {
        public Action Submit;
        public override void OnSubmit(BaseEventData eventData)
        {
            if(IsActive()&&IsInteractable())Submit?.Invoke();
        }
    }

}
