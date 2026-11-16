using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // The hero's look in the village (data/story/hero_bodies.json, compiled to Hub/Resources/HubBodies.json): the village
    // is always walked bare-handed, and the opening shows the hero as a human until the helper's ink revives it. The sets
    // are added to the arena's animation catalog under their body id and drawn through CombatPlayer.AppearanceOverride,
    // so locomotion picks their clips as it does a weapon form's.
    public sealed partial class StartingVillageWorld
    {
        [Serializable] sealed class BodyFile { public int schemaVersion; public string village, opening; public BodyEntry[] bodies; }
        [Serializable] sealed class BodyEntry { public string id, root; public bool plainAfterimage; public int[] dashPoses; public BodyClip[] clips; }
        [Serializable] sealed class BodyClip { public string name; public string[] frames; public float[] seconds; }

        BodyFile bodyFile;
        public string VillageBody => bodyFile?.village;
        public string OpeningBody => bodyFile?.opening;
        public string HeroBody => World.arena.Player ? World.arena.Player.AppearanceOverride : null;

        // Adds the body sets to the catalog once and dresses the hero in the village body.
        void BuildHeroBodies()
        {
            var text = Resources.Load<TextAsset>("HubBodies");
            if (!text) { Debug.LogError("HubBodies resource is missing; run tools/assets/build_hub_bindings.py."); return; }
            bodyFile = JsonUtility.FromJson<BodyFile>(text.text);
            InstallBodies(World.arena, bodyFile);
            SetHeroBody(VillageBody);
        }

        public static void DressUnarmed(CombatArena arena)
        {
            var text = Resources.Load<TextAsset>("HubBodies");
            if (!text) throw new System.InvalidOperationException("HubBodies resource is missing.");
            var bodies = JsonUtility.FromJson<BodyFile>(text.text);
            InstallBodies(arena, bodies);
            int form = Array.FindIndex(arena.catalog.forms, value => value.id == "unarmed");
            if (form < 0) throw new InvalidOperationException("The generated unarmed combat form is missing.");
            var skill = arena.catalog.forms[form].specials[0];
            var configuration = new Deviant.Equipment.Contracts.WeaponConfiguration(
                new Deviant.Equipment.Contracts.WeaponSelection(form, 0), skill.capacity, skill.cooldown);
            var result = arena.Player.weapons.Reconfigure(0, configuration, arena.Now);
            if (result.Status != Deviant.Equipment.Contracts.EquipmentCommandStatus.Succeeded)
                throw new InvalidOperationException(result.Error);
            arena.Player.CancelAction(); arena.Player.ClearInputState(); arena.Player.selected = 0;
            arena.Player.weapons[0].StatSlots.Clear();
            arena.Player.weapons[0].ExperimentalChargeStamp = false;
            arena.Player.AppearanceOverride = null;
            arena.Player.SetWeaponSlotReady(0, true);
            arena.Player.SetWeaponSlotReady(1, false);
        }

        static void InstallBodies(CombatArena arena, BodyFile bodyFile)
        {
            var catalog = arena.characterAnimations;
            if (catalog == null) return;
            var sets = new List<CharacterAnimationSet>(catalog.sets ?? new CharacterAnimationSet[0]);
            foreach (var body in bodyFile.bodies)
            {
                if (sets.Exists(set => set.form == body.id)) continue;
                var clips = Array.ConvertAll(body.clips, clip => new CharacterClip
                {
                    name = clip.name, seconds = clip.seconds,
                    frames = Array.ConvertAll(clip.frames, frame => body.root + "/" + frame),
                });
                foreach (var clip in clips) clip.Load();
                sets.Add(new CharacterAnimationSet { form = body.id, gameplayForm = body.id, clips = clips, plainAfterimage = body.plainAfterimage,
                    dashPoses = body.dashPoses != null && body.dashPoses.Length > 0 ? body.dashPoses : null });
            }
            catalog.sets = sets.ToArray();
        }

        // Null draws the weapon form again (the runs); any other id is a body from HubBodies.
        public void SetHeroBody(string id)
        {
            if (World.arena.Player) World.arena.Player.AppearanceOverride = id;
        }

        CharacterAnimationSet HeroSet()
        {
            string form = HeroBody ?? "base_sword";
            CharacterAnimationSet found = null;
            foreach (var set in World.arena.characterAnimations?.sets ?? new CharacterAnimationSet[0]) if (set.form == form) found = set;
            return found;
        }
    }
}
