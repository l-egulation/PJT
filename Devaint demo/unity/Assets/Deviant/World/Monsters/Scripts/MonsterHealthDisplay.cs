using TMPro;
using UnityEngine;
using Deviant.Combat;
using Deviant.UI;
namespace Deviant.World
{
    public class MonsterHealthDisplay : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<MonsterHealthDisplay> active = new System.Collections.Generic.List<MonsterHealthDisplay>();
        public Canvas canvas;
        public TMP_Text label;
        public PreservedGauge gauge;
        public CombatActor health;
        float anchorHeight;
        void Awake()
        {
            health = GetComponentInParent<CombatActor>();
            anchorHeight = transform.localPosition.y;
            if(label) label.gameObject.SetActive(false);
        }
        void OnEnable() { active.Add(this); }
        void OnDisable() { active.Remove(this); }
        void LateUpdate()
        {
            if (!health) return;
            canvas.enabled = !health.Dead && health.visual && health.visual.enabled;
            gauge.Set(Mathf.CeilToInt(health.health), Mathf.CeilToInt(health.maxHealth));
            float lift = 0;
            foreach(var other in active)
            {
                if (other == this || !other.health || other.health.Dead || health.ActorId >= other.health.ActorId) continue;
                float dx = Mathf.Abs(health.Position.x - other.health.Position.x);
                float dy = other.health.Position.y + other.anchorHeight - (health.Position.y + anchorHeight);
                if (dx < 2.1f && Mathf.Abs(dy) < .75f) lift = Mathf.Max(lift, dy + .75f);
            }
            transform.localPosition = new Vector3(0, anchorHeight + lift, 0);
        }
    }
}
