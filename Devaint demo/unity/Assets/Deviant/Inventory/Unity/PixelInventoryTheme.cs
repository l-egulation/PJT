using System;
using UnityEngine;

namespace Deviant.Inventory.Unity
{
    [Serializable] public sealed class PixelIcon { public string id; public Sprite sprite; }
    [CreateAssetMenu(menuName = "Deviant/Pixel inventory theme")]
    public sealed class PixelInventoryTheme : ScriptableObject
    {
        public Sprite windowFrame, windowFooter, cardFrame, selectedCardFrame, slotFrame, selectedSlotFrame, buttonFrame, primaryButtonFrame, portrait, bag;
        public Sprite titleLogo, closeIcon;
        public Font regularFont, boldFont;
        public PixelIcon[] itemIcons = Array.Empty<PixelIcon>(), weaponIcons = Array.Empty<PixelIcon>();
        public bool Ready => windowFrame && cardFrame && slotFrame && selectedSlotFrame && buttonFrame && primaryButtonFrame && regularFont && boldFont;
        public Sprite Item(InkDefinition definition)
        {
            var icon = Array.Find(itemIcons, entry => entry.id == definition.Id);
            return icon != null ? icon.sprite : definition.Kind == InkKind.Form ? Weapon(definition.FormId) : null;
        }
        public Sprite Weapon(string id) => Array.Find(weaponIcons, entry => entry.id == id)?.sprite;
    }
}
