using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable] public sealed class WallClimbVisuals
    {
        public float contactX;
        public float bodyScale = 1, ledgeEndScale = 1;
        // The ledge climb is drawn as authored: ledgeCorner is the wall corner in the source canvas (px from the top-left) and
        // every ledge frame is drawn at one size, bodyScale / ledgePixelsPerUnit units per source pixel.
        public float[] ledgeCorner; public float ledgePixelsPerUnit;
        public bool LedgeAnchored => ledgeCorner != null && ledgeCorner.Length == 2 && ledgePixelsPerUnit > 0;
        public CharacterClip inkStart, inkClimb, inkLedge, humanStart, humanClimb, humanLedge;
        readonly Dictionary<Sprite, float> feet = new Dictionary<Sprite, float>();
        public void Load()
        {
            foreach (var clip in new[] { inkStart, inkClimb, inkLedge, humanStart, humanClimb, humanLedge })
            {
                clip.Load();
                foreach (var sprite in clip.sprites)
                {
                    if (!sprite || feet.ContainsKey(sprite)) continue;
                    var rect = sprite.rect; var texture = sprite.texture; var pixels = texture.GetPixels32();
                    int bottom = (int)rect.yMax;
                    for (int y = (int)rect.y; y < bottom; y++)
                        for (int x = (int)rect.x; x < rect.xMax; x++)
                            if (pixels[y * texture.width + x].a >= 128) { bottom = y; break; }
                    feet[sprite] = (bottom - rect.y - sprite.pivot.y) / sprite.pixelsPerUnit;
                }
            }
        }
        public float FootOffset(Sprite sprite) => feet.TryGetValue(sprite, out float foot) ? foot : 0;
    }
    [Serializable] public sealed class ApprovedTraversalArt
    {
        public CharacterClip wallContact, wallKick, portalDraw, portalMelt;
        public CharacterAnimationSet[] jumps;
        public float portalDrawStartSeconds;
        public float[] entryInk;
        public static ApprovedTraversalArt Load()
        {
            var asset = Resources.Load<TextAsset>("ApprovedTraversal/Catalog");
            if (!asset) throw new InvalidOperationException("Restore the approved wall/jump and portal packages first.");
            var art = JsonUtility.FromJson<ApprovedTraversalArt>(asset.text);
            art.wallContact.Load(); art.wallKick.Load(); art.portalDraw.Load(); art.portalMelt.Load();
            foreach (var set in art.jumps) foreach (var clip in set.clips) clip.Load();
            return art;
        }
    }

    // Presentation only: physics, input buffering, jump charges and combat clocks
    // stay owned by CombatPlayer. Portal presentation runs after this component.
    [DefaultExecutionOrder(1050)]
    public sealed class Chapter1TraversalPresentation : MonoBehaviour
    {
        CombatArena arena;
        CombatPlayer player;
        CharacterAnimationSet[] appliedSets;
        float contactStarted;
        bool wasClimbing, altered;
        bool ownsScale;
        Vector3 appliedScale;
        int contactFacing;
        WallClimbVisuals climbVisuals;
        public ApprovedTraversalArt Art { get; private set; }
        // The climb-v4 wall art (start, climb loop, ledge), shared with the opening's guide ghost so it climbs as the hero does.
        public WallClimbVisuals ClimbVisuals => climbVisuals;
        public string CurrentMotion { get; private set; }

        public void Initialize(ForestCombatWorld owner) => Initialize(owner.arena);
        // Chapter 2 builds its own arena without a ForestCombatWorld; it climbs in the same approved wall art.
        public void Initialize(CombatArena owner)
        {
            arena = owner; player = arena.Player; Art = ApprovedTraversalArt.Load();
            var climbAsset = Resources.Load<TextAsset>("ApprovedTraversal/ClimbV4");
            if (climbAsset) { climbVisuals = JsonUtility.FromJson<WallClimbVisuals>(climbAsset.text); climbVisuals.Load(); }
            ApplyJumpClips();
        }
        void ApplyJumpClips()
        {
            var sets = arena.characterAnimations?.sets;
            if (sets == null || ReferenceEquals(sets, appliedSets)) return;
            foreach (var set in sets)
            {
                var approved = Array.Find(Art.jumps, s => s.form == set.form);
                if (approved == null) continue;
                for (int i = 0; i < set.clips.Length; i++)
                {
                    var replacement = approved.Find(set.clips[i].name);
                    if (replacement != null) set.clips[i] = replacement;
                }
            }
            appliedSets = sets;
        }
        void LateUpdate() => RefreshPresentation();
        public void RefreshPresentation()
        {
            if (!player || !player.visual || Art == null) return;
            ApplyJumpClips();
            CurrentMotion = null;
            RestoreScale();
            if (altered) player.visual.transform.localPosition = Vector3.zero;
            altered = false;
            bool available = !player.Dead && !player.CheckpointBusy && !player.currentCast && !player.Dashing;
            // A body with wall clips of its own (the human hero before the ink) climbs in them; every other look uses
            // the approved ink wall art.
            var body = BodySet();
            var contact = body?.Find("wall_contact") ?? Art.wallContact;
            var kick = body?.Find("wall_kick") ?? Art.wallKick;
            if (available && player.WallClimbing)
            {
                if (!wasClimbing || contactFacing != player.facing)
                { contactStarted = arena.Now; contactFacing = player.facing; }
                CurrentMotion = "wall_contact";
                float age = arena.Now - contactStarted;
                if (climbVisuals != null)
                {
                    bool human = player.AppearanceOverride == "opening_human";
                    var start = human ? climbVisuals.humanStart : climbVisuals.inkStart;
                    var climb = human ? climbVisuals.humanClimb : climbVisuals.inkClimb;
                    // The motor supplies the ascent; frame anchors remove source-sheet translation.
                    player.visual.sprite = player.CurrentInput.ClimbAxis > 0
                        ? (age < start.Duration ? start.At(age, false) : climb.At(age - start.Duration, true))
                        : start.At(start.Duration, false);
                }
                else player.visual.sprite = contact.At(age, true);
                player.visual.flipX = player.facing < 0;
                // Keep the authored hand line on the collider edge as each frame's pivot and PPU change.
                float contactOffset = -.4375f;
                if (climbVisuals != null && climbVisuals.contactX > 0)
                {
                    var sprite = player.visual.sprite;
                    float handReach = (climbVisuals.contactX - sprite.pivot.x) / sprite.pixelsPerUnit * climbVisuals.bodyScale;
                    contactOffset = player.offset.x + player.size.x / 2 - handReach;
                }
                player.visual.transform.localPosition = new Vector3(contactOffset * player.facing, 0, 0);
                altered = true;
                if (climbVisuals != null) ApplySize(climbVisuals.bodyScale);
            }
            else if (available && player.WallJumpInFlight && arena.Now - player.WallJumpStartedAt < kick.Duration)
            {
                CurrentMotion = "wall_kick";
                player.visual.sprite = kick.At(arena.Now - player.WallJumpStartedAt, false);
                // Source kick is authored leaving a right wall, including its initial turn.
                player.visual.flipX = player.WallJumpHeading > 0;
                player.visual.transform.localPosition = Vector3.zero;
            }
            else if (available && climbVisuals != null && climbVisuals.LedgeAnchored && player.WallLedgeClimbing)
            {
                // The motor holds the hero at the wall; the climb plays where it was drawn, its wall corner on the real corner,
                // so the hands stay on the edge and the body rises and steps over exactly as in the art.
                CurrentMotion = "wall_ledge";
                var clip = player.AppearanceOverride == "opening_human" ? climbVisuals.humanLedge : climbVisuals.inkLedge;
                var sprite = clip.At(arena.Now - player.WallLedgeStartedAt, false);
                player.visual.sprite = sprite;
                int side = player.WallLedgeSide < 0 ? -1 : 1;
                player.visual.flipX = side < 0;
                float k = climbVisuals.bodyScale / climbVisuals.ledgePixelsPerUnit;
                var corner = player.WallLedgeCorner;
                float cornerX = climbVisuals.ledgeCorner[0], cornerY = sprite.rect.height - climbVisuals.ledgeCorner[1];
                var visual = player.visual.transform;
                visual.position = new Vector3(corner.x - side * (cornerX - sprite.pivot.x) * k, corner.y - (cornerY - sprite.pivot.y) * k, visual.position.z);
                appliedScale = new Vector3(k * sprite.pixelsPerUnit, k * sprite.pixelsPerUnit, 1);
                visual.localScale = appliedScale;
                ownsScale = altered = true;
            }
            else if (available && climbVisuals != null && arena.Now >= player.WallLedgeStartedAt &&
                arena.Now - player.WallLedgeStartedAt < climbVisuals.inkLedge.Duration)
            {
                CurrentMotion = "wall_ledge";
                var clip = player.AppearanceOverride == "opening_human" ? climbVisuals.humanLedge : climbVisuals.inkLedge;
                float age = arena.Now - player.WallLedgeStartedAt;
                player.visual.sprite = clip.At(age, false);
                player.visual.flipX = player.facing < 0;
                float blend = Mathf.SmoothStep(0, 1, age / (clip.Duration - clip.seconds[clip.seconds.Length - 1]));
                ApplySize(Mathf.Lerp(climbVisuals.bodyScale, climbVisuals.ledgeEndScale, blend), player.grounded ? blend : 0);
            }
            wasClimbing = available && player.WallClimbing;
        }
        void ApplySize(float scale, float plant = 0)
        {
            var visual = player.visual.transform;
            float foot = climbVisuals.FootOffset(player.visual.sprite);
            var at = visual.localPosition;
            // Enlarge about the authored feet; only the grounded ledge recovery plants them at the motor's floor.
            at.y += (1 - scale - plant) * foot;
            visual.localPosition = at;
            appliedScale = new Vector3(scale, scale, 1);
            visual.localScale = appliedScale;
            ownsScale = altered = true;
        }
        void RestoreScale()
        {
            // The native presenter or jump stretch may already own this frame. Do not erase their scale.
            if (ownsScale && player && player.visual && player.visual.transform.localScale == appliedScale)
                player.visual.transform.localScale = Vector3.one;
            ownsScale = false;
        }
        CharacterAnimationSet BodySet()
        {
            string id = player.AppearanceOverride;
            if (string.IsNullOrEmpty(id)) return null;
            var sets = arena.characterAnimations?.sets;
            return sets == null ? null : Array.Find(sets, s => s.form == id);
        }
        void OnDisable()
        {
            RestoreScale();
            if (altered && player && player.visual) player.visual.transform.localPosition = Vector3.zero;
            altered = wasClimbing = false;
        }
    }
}
