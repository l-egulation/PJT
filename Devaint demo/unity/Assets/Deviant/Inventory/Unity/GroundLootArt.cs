using UnityEngine;

namespace Deviant.Inventory.Unity
{
    // Native pixel art at 16 PPU keeps one outline weight, including the smaller 12px coin.
    public static class GroundLootArt
    {
        public const string Gold = "gold-flat-small", Pigment = "pigment-palette-flat";
        public const string WeaponStamp = "weapon-stamp-refined", SkillStamp = "skill-stamp-refined";
        public static string ColoredInk(string color) => "ink-refined-" + color;
        public static string GradedInk(string color, int grade) => $"ink-{color}-grade-{grade}";
        public static Sprite Load(string id) => Resources.Load<Sprite>("LootPickups/" + id);

        public static bool Apply(SpriteRenderer visual, string id)
        {
            var sprite = Load(id);
            if (!sprite) return false;
            visual.sprite = sprite;
            visual.color = Color.white;
            visual.transform.localScale = Vector3.one;
            return true;
        }

        public static string InkKey(InkDefinition definition) => definition.Kind switch
        {
            InkKind.Form => WeaponStamp,
            InkKind.Special => SkillStamp,
            _ => ColoredInk(definition.Value == 3 ? "yellow" : definition.Value == 4 ? "blue" : "red"),
        };
    }
}
