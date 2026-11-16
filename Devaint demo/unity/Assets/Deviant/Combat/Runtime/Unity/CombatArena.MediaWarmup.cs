using System.Collections.Generic;
using System.Diagnostics;

namespace Deviant.Combat
{
    // Character and skill art is deferred (CombatContentLoader); only the weapons in the player's two slots are read,
    // a few milliseconds per frame, as soon as they are equipped. A form used before its warm-up finishes still reads
    // its frames on first use, so warming changes when art loads, never what is drawn.
    public partial class CombatArena
    {
        const double MediaWarmupBudgetMs = 3;
        readonly HashSet<string> warmedForms = new HashSet<string>(System.StringComparer.Ordinal);
        readonly Queue<IEnumerator<bool>> mediaWarmups = new Queue<IEnumerator<bool>>();
        readonly Stopwatch mediaWarmupClock = new Stopwatch();

        // Skill-effect catalogs that keep their own per-weapon art; the others read their sprites on first use.
        static IEnumerable<bool> SkillMedia(string form) => form switch
        {
            "whip" => WhipSkillFx.WarmSteps(),
            "dagger" => DaggerSkillFx.WarmSteps(),
            "shuriken" => ShurikenSkillFx.WarmSteps(),
            "bomb" => BombSkillFx.WarmSteps(),
            _ => null
        };

        public bool MediaWarmupPending => mediaWarmups.Count > 0;

        void WarmEquippedMedia()
        {
            if (initializing || !loadCharacterArt || !Player || catalog?.forms == null || characterAnimations == null) return;
            for (int slot = 0; slot < 2; slot++)
            {
                int index = Player.weapons[slot].FormIndex;
                if (index < 0 || index >= catalog.forms.Length) continue;
                string form = catalog.forms[index].id;
                if (string.IsNullOrEmpty(form) || !warmedForms.Add(form)) continue;
                mediaWarmups.Enqueue(characterAnimations.WarmForm(form).GetEnumerator());
                var skill = SkillMedia(form);
                if (skill != null) mediaWarmups.Enqueue(skill.GetEnumerator());
            }
            if (mediaWarmups.Count == 0) return;
            mediaWarmupClock.Restart();
            while (mediaWarmups.Count > 0 && mediaWarmupClock.Elapsed.TotalMilliseconds < MediaWarmupBudgetMs)
                if (!mediaWarmups.Peek().MoveNext()) mediaWarmups.Dequeue().Dispose();
        }
    }
}
