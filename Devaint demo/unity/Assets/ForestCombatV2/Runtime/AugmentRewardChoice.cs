using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Free augment choice (docs/design/progression/maintenance-augments.md). Augments are never sold (user decision
    // 2026-11-13): a boss clear whose exit leads on to a next chapter (after the boss weapon choice) and the seal reward chest
    // (docs/design/world/stage-gimmicks.md) each offer three cards, one stable and two risk cards of different groups.
    // The taken card joins the run's WeaponGrowth, so it reaches the combat through the augment layer and lasts into
    // chapter 2. The game pauses until a card is taken; there is no skip.
    public sealed class AugmentRewardChoice : MonoBehaviour
    {
        Func<CombatArena> arenaOf;
        CombatArena arena => arenaOf?.Invoke();
        Func<WeaponGrowth> growth;
        Func<bool> busy;
        IControlScope scope;
        int queued;

        public MaintenanceAugments Augments => growth?.Invoke()?.Augments;
        public AugmentDefinition[] Offer { get; private set; }
        public bool IsOpen => scope != null;
        // Choices waiting for another screen (the boss weapon choice) to close.
        public int Queued => queued;
        public event Action<AugmentDefinition> Taken;

        // busy: true while another reward screen must be finished first (the queued boss choice waits for it).
        public static AugmentRewardChoice Attach(GameObject host, Func<CombatArena> arena, Func<WeaponGrowth> growth, Func<bool> busy = null)
        {
            var choice = host.GetComponent<AugmentRewardChoice>() ?? host.AddComponent<AugmentRewardChoice>();
            choice.arenaOf = arena; choice.growth = growth; choice.busy = busy;
            return choice;
        }

        bool Ready { get { var a = arena; return a && a.Player && !a.Player.Dead && Augments != null; } }

        // Opens the choice now; false when the run has no augments or every card is already owned.
        public bool OpenChoice()
        {
            if (IsOpen || !Ready) return false;
            var offer = Augments.DrawReward();
            if (offer.Length == 0) return false;
            var next = arena.Controls.OpenScope("augment reward choice");
            try { next.BlockInput(InputChannels.Combat | InputChannels.Interaction); next.PauseSimulation(); scope = next; }
            catch { next.Dispose(); throw; }
            Offer = offer;
            return true;
        }

        // The boss clear: the choice opens once the boss weapon choice (and anything else busy) is closed.
        public void QueueChoice() { queued++; }

        void Update()
        {
            if (queued == 0 || IsOpen) return;
            if (arena && arena.Player && arena.Player.Dead) { queued = 0; return; }
            if (!Ready || busy != null && busy() || arena.Controls.Current.SimulationPaused) return;
            queued--;
            OpenChoice();
        }

        public bool TryTake(int index)
        {
            if (!IsOpen || Offer == null || index < 0 || index >= Offer.Length) return false;
            var chosen = Offer[index];
            if (!growth().GrantAugment(chosen.id)) return false;
            Offer = null; scope.Dispose(); scope = null;
            Taken?.Invoke(chosen);
            return true;
        }

        // A shared reroll (roguelite-progression.md 공용 리롤) draws another free offer that changes at least one card; the
        // run budget is spent only when such an offer exists.
        public bool TryReroll()
        {
            var perks = growth?.Invoke()?.Perks;
            if (!IsOpen || Offer == null || Augments == null || perks == null) return false;
            return perks.TryReroll(() =>
            {
                var previous = new HashSet<string>(Offer.Select(a => a.id));
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var next = Augments.DrawReward();
                    if (next.Length > 0 && next.Any(a => !previous.Contains(a.id))) { Offer = next; return true; }
                }
                return false;
            });
        }

        // A development reset or a scene change drops an open or waiting choice.
        public void Cancel() { queued = 0; Offer = null; scope?.Dispose(); scope = null; }

        void OnDestroy() => Cancel();

        // The chapter GUI shows the journal cards; without it a plain three-button choice keeps the run playable.
        public bool ExternalPresentation { get; set; }
        void OnGUI()
        {
            if (ExternalPresentation || !IsOpen || Offer == null) return;
            float w = 240, h = 160, gap = 16, x = (Screen.width - (w * Offer.Length + gap * (Offer.Length - 1))) / 2, y = (Screen.height - h) / 2;
            for (int i = 0; i < Offer.Length; i++)
                if (GUI.Button(new Rect(x + i * (w + gap), y, w, h), Offer[i].name + "\n\n" + Offer[i].gain + "\n" + Offer[i].cost)) TryTake(i);
        }
    }
}
