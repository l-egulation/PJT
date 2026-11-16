using System;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable] public sealed class ApprovedThrownCatalog { public ApprovedThrownDefinition[] projectiles; }
    [Serializable] public sealed class ApprovedThrownDefinition : CharacterClip
    {
        public string form;
        public float spin;
    }

    // Native sprite art only: no collider, hit event, lifetime, or ballistic changes.
    public sealed class ApprovedThrownVisual : MonoBehaviour
    {
        CombatArena arena;
        ApprovedThrownDefinition definition;
        SpriteRenderer visual;
        float started;

        public static bool TryAttach(Transform owner,CombatArena arena,string form)
        {
            if(!arena || !arena.loadCharacterArt)return false;
            var text=Resources.Load<TextAsset>("CharacterV2/ApprovedThrownProjectiles");
            if(!text)return false;
            var catalog=JsonUtility.FromJson<ApprovedThrownCatalog>(text.text);
            var entry=Array.Find(catalog.projectiles,d=>d.form==form);
            if(entry==null)return false;
            entry.Load();
            var child=new GameObject("ApprovedThrown_"+form);
            child.transform.SetParent(owner,false);
            // The parent's scale belongs to the existing placeholder/collision setup.
            // Compensate on this child so PPU32 artwork retains its native dimensions.
            var scale=owner.localScale;
            child.transform.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1);
            var renderer=child.AddComponent<SpriteRenderer>();renderer.sortingOrder=26;renderer.color=Color.white;
            if(arena.spriteMaterial)renderer.sharedMaterial=arena.spriteMaterial;
            var component=child.AddComponent<ApprovedThrownVisual>();
            component.arena=arena;component.definition=entry;component.visual=renderer;component.started=arena.Now;component.Sample();
            return true;
        }
        public void Sample()
        {
            if(!arena || definition==null || !visual)return;
            float age=Mathf.Max(0,arena.Now-started);
            visual.sprite=definition.At(age,true);
            transform.localRotation=Quaternion.Euler(0,0,definition.spin*age);
        }
        void LateUpdate(){Sample();}
    }
}
