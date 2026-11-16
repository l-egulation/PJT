using System.Collections.Generic;
using Deviant.Control.Contracts;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // First-run notebooks on the play GUI (docs/design/story/tutorials/first-run-guide.md, user decision 2026-11-09). On the run the
    // first-run ink guide teaches (the profile's first run): the combat notebook once the departure palette card has gone
    // (the guide's monster waits for it), and the palette notebook once the guide ink is picked up (the bag prompt waits
    // for it). Each pauses the world. The GUI owns the scope and the view; Chapter1FirstRunInkGuide owns when each is due.
    public sealed partial class TemporaryGameGui
    {
        FirstRunNotebookView firstRunNotebook;
        IControlScope firstRunNotebookScope;
        public bool FirstRunNotebookShowing => firstRunNotebook;
        IControlScope departureCardScope;
        bool departureCardDismissed;
        public bool DepartureCardLocked => departureCardScope != null;
        // The run the first-run guide teaches (the profile's first run, with a GUI to show its notebooks).
        bool FirstRunGuideRun => InkGuide && InkGuide.Active && InkGuide.NotebookWanted;

        // On that run the departure palette card stays until the player closes it with interact or advance, like the
        // notebooks, and the hero cannot move or attack under it; the world keeps running so the hero still breathes
        // (user 2026-11-09: a frozen screen read as a hang). The combat notebook follows it. Other runs keep the timed card.
        void LockDepartureCard(bool held)
        {
            held = held && host && host.Arena;
            if (held && departureCardScope == null)
            {
                departureCardScope = host.Arena.Controls.OpenScope("first-run departure card");
                departureCardScope.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
            }
            else if (!held && departureCardScope != null) { departureCardScope.Dispose(); departureCardScope = null; Controls?.SuppressGameplay(); }
        }

        public void DismissDepartureCard() { departureCardDismissed = true; LockDepartureCard(false); }

        // Closing input for the held card, after the same short grace the notebooks use.
        void RefreshDepartureCardInput()
        {
            var input = Controls; if (!input) return;
            bool close = input.Take(Deviant.Input.Contracts.GameAction.Interact) | input.Take(Deviant.Input.Contracts.GameAction.Advance);
            input.SuppressGameplay();
            if (close && Time.unscaledTime - runPaletteAt >= .35f) DismissDepartureCard();
        }

        void RefreshFirstRunNotebook()
        {
            if (SketchBinding) return;
            var guide = InkGuide;
            if (departureCardScope != null) { RefreshDepartureCardInput(); return; }
            if (firstRunNotebook)
            {
                // The guide finished it (closed here, a test, or the guide stopped): tear down and give the world back.
                if (!guide || !guide.NotebookOpen) CloseFirstRunNotebook();
                else firstRunNotebook.Tick();
                return;
            }
            if (!guide || !guide.Active || !shop || shop.Growth == null) return;
            guide.NotebookWanted = true;
            var due = guide.NotebookDue;
            if (due == Chapter1FirstRunInkGuide.Notebook.None) return;
            // The combat notebook follows the departure palette card (which holds the world on this run); neither opens
            // over another screen or the pause menu.
            if (due == Chapter1FirstRunInkGuide.Notebook.Combat && (runPaletteShownFor == null || RunPaletteShowing)) return;
            if (Presenter.IsOpen || Presenter.Paused || shop.IsOpen || Altar && Altar.IsOpen || augments && augments.IsOpen || map && map.IsOpen) return;
            var arena = host ? host.Arena : null;
            if (!arena || !arena.Player || arena.Player.Dead) return;
            var palettes = new List<(string name, IList<string> colours)>();
            for (int w = 0; w < 2; w++)
            {
                var palette = shop.Growth.Equipped(shop.GrowthWeapon(w));
                var colours = new List<string>();
                if (palette != null) foreach (var slot in palette.Slots) colours.Add(slot.Color);
                string name = runPaletteNames != null && runPaletteNames[w] && !string.IsNullOrEmpty(runPaletteNames[w].text) ? runPaletteNames[w].text : host.WeaponName(w);
                palettes.Add((name, colours));
            }
            // The combat page swings each weapon's own character clips (the set the arena draws the hero with).
            var heroSets = new List<Deviant.Combat.CharacterAnimationSet>();
            for (int w = 0; w < 2; w++)
            {
                Deviant.Combat.CharacterAnimationSet set = null;
                var sets = arena.characterAnimations?.sets;
                if (sets != null && w < arena.Player.weapons.Count && arena.Player.IsWeaponSlotReady(w))
                {
                    string form = arena.catalog.forms[arena.Player.weapons[w].FormIndex].id;
                    foreach (var candidate in sets) if (candidate != null && candidate.form == form) set = candidate;
                }
                heroSets.Add(set);
            }
            // The palette page fades the picked-up guide part (the bag's emblem drop) into the slot the bag will ring.
            FirstRunNotebookView.GuideInk? guideInk = null;
            if (due == Chapter1FirstRunInkGuide.Notebook.Palette && guide.GuidePart != null && guide.TargetSlot(out int inkWeapon, out int inkSlot))
                guideInk = new FirstRunNotebookView.GuideInk { part = DropArt(guide.GuidePart), weapon = inkWeapon, slot = inkSlot };
            guide.MarkNotebookOpen(due);
            if (!guide.NotebookOpen) return;
            firstRunNotebookScope = arena.Controls.OpenScope("first-run notebook");
            firstRunNotebookScope.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
            firstRunNotebookScope.PauseSimulation();
            firstRunNotebook = FirstRunNotebookView.Create(transform, arena.Player, palettes, colour => DropArt(null, colour) ?? CombatSprite("drop-" + colour), heroSets, guideInk,
                due == Chapter1FirstRunInkGuide.Notebook.Combat, FinishFirstRunNotebook);
        }

        void FinishFirstRunNotebook()
        {
            var guide = InkGuide;
            if (guide) guide.FinishNotebook();
            CloseFirstRunNotebook();
        }

        void CloseFirstRunNotebook()
        {
            if (firstRunNotebook) { firstRunNotebook.gameObject.SetActive(false); Destroy(firstRunNotebook.gameObject); }
            firstRunNotebook = null;
            firstRunNotebookScope?.Dispose(); firstRunNotebookScope = null;
            Controls?.SuppressGameplay();
        }
    }
}
