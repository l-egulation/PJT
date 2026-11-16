using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;

namespace Deviant.Narrative.Tests
{
    // Technical placeholder text and NPC IDs only; no approved story content.
    public static class DialogueFixtures
    {
        public static DialogueDefinition Definition(string suffix = "") => new DialogueDefinition("fixture-dialogue", "a", new[] {
            new DialogueLine("a", "fixture.speaker", "fixture.a" + suffix, "b", "entry"),
            new DialogueLine("b", "fixture.speaker", "fixture.b" + suffix, "c", "next"), new DialogueLine("c", "fixture.speaker", "fixture.c" + suffix) });
        public sealed class Conditions : INarrativeConditions
        {
            public string Rejected; public bool Throw;
            public bool Evaluate(string condition, Guid world, string placement) { if (Throw) throw new InvalidOperationException("condition provider failed"); return condition != Rejected; }
        }
        public sealed class Targets : IInteractionTargets
        {
            public Guid World = Guid.NewGuid(); public bool Exists = true, InCombat; public float Distance = 1;
            public NpcDefinition Npc = new NpcDefinition("fixture-npc", Definition(), "npc");
            public IReadOnlyList<NpcTarget> ReadTargets() => Exists ? new[] { Target() } : Array.Empty<NpcTarget>();
            public NpcTarget Target() => new NpcTarget(World, "fixture-placement", Npc, Distance, InCombat);
            public bool TryRead(Guid world, string placement, out NpcTarget target)
            { target = Exists && world == World && placement == "fixture-placement" ? Target() : null; return target != null; }
        }
        public sealed class Text : IDialogueText
        {
            public string Missing;
            public bool TryRead(string key, out string text) { text = key == Missing ? null : key; return text != null; }
        }
        public sealed class View : IDialogueView
        {
            public Guid Bound;
            public DialogueDisplay Display;
            public event Action<DialogueIntent> Input;
            public bool TryBind(Guid binding, out string error) { error = Bound == Guid.Empty ? null : "already bound"; if (error == null) Bound = binding; return error == null; }
            public bool TryApply(DialogueDisplay display) { if (Bound != display.BindingId) return false; Display = display; return true; }
            public void Unbind(Guid binding) { if (binding == Bound) { Bound = Guid.Empty; Display = null; } }
            public void Emit(DialogueIntent intent) { Input?.Invoke(intent); }
            public DialogueIntent Intent(DialogueIntentKind kind) => new DialogueIntent(Bound, Display.Token, kind);
        }
    }
}
