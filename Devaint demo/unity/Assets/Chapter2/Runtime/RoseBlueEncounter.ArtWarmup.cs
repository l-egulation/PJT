using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Deviant.Chapter2
{
    // Boss motion and VFX sheets are no longer cut when chapter 2 is built (they held about 116 MB of texture data
    // from the chapter's first frame, 2026-11-16). They are cut on first use, and ahead of the fight once the player
    // comes within WarmDistance of the battlefield, a few milliseconds per step.
    public sealed partial class RoseBlueEncounter
    {
        public const float WarmDistance = 64;
        const double WarmBudgetMs = 3;
        IEnumerator<bool> artWarmup;
        readonly Stopwatch artWarmupClock = new Stopwatch();
        public bool ArtWarmed { get; private set; }

        public void WarmArtNear(Vector2 player)
        {
            if (ArtWarmed || !Stage) return;
            if (artWarmup == null)
            {
                if (!Started && Vector2.Distance(player, Stage.Origin) > WarmDistance) return;
                artWarmup = ArtWarmupSteps().GetEnumerator();
            }
            artWarmupClock.Restart();
            while (artWarmupClock.Elapsed.TotalMilliseconds < WarmBudgetMs)
                if (!artWarmup.MoveNext()) { artWarmup.Dispose(); artWarmup = null; ArtWarmed = true; return; }
        }

        IEnumerable<bool> ArtWarmupSteps()
        {
            LoadBossMotions();
            foreach (var id in new List<string>(bossMotions.Keys)) { BossMotionEntry(id); yield return true; }
            foreach (var name in new List<string>(vfxSheets.Keys)) { _ = Vfx(name); yield return true; }
        }
    }
}
