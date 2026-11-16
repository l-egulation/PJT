using System.Collections.Generic;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        static readonly Dictionary<string, Sprite> ItemIcons = new Dictionary<string, Sprite>();

        static Sprite ItemIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!ItemIcons.TryGetValue(id, out var icon))
                ItemIcons[id] = icon = Resources.Load<Sprite>("ApprovedItemIcons/" + id);
            return icon;
        }

        static string SkillIconId(int skill) => skill == 1 ? "skill_range" : skill == 2 ? "skill_move" : "skill_followup";
        static Sprite StampIcon(RunStamp stamp) => stamp == null ? null :
            ItemIcon(stamp.Kind == StampKind.Weapon ? stamp.FormId : SkillIconId(stamp.Skill));

        static Sprite LegacyStampIcon(string id) => ItemIcon(id.StartsWith("form.") ? id.Substring(5) :
            id == "special.range" ? "skill_range" : id == "special.move" ? "skill_move" :
            id == "special.follow" ? "skill_followup" : null);

        Image StampItem(Transform parent, RunStamp stamp, float x, float y, float w, float h)
        {
            var image = Rect("Stamp item", parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = StampIcon(stamp);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.enabled = image.sprite;
            return image;
        }
    }
}
