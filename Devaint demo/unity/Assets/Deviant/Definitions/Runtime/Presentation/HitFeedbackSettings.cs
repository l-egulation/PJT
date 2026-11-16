using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Deviant.Definitions
{
    [Serializable] public sealed class HitRecipeData
    { public string key, sourcePrefab; public float scale, rotation; }
    [Serializable] public sealed class HitFormData
    { public string form, recipe; }
    [Serializable] public sealed class HitFeedbackData
    {
        public int schemaVersion, maxActiveHits, maxInstancesPerPrefab, maxParticles, maxWaves, maxDebrisBodies, debrisPieces;
        public string status;
        public float debrisLifetime, debrisKickCooldown, debrisImpulse, debrisUpward, debrisMaxKickSpeed;
        // Corpse debris look: alpha and grey blend over the source sprite. Missing in older data means the original colour.
        public float debrisAlpha=1, debrisDesaturate;
        // Velocity a player melee sweep gives each debris piece it overlaps. Missing in older data means no reaction.
        public float debrisStrikeSpeed, debrisStrikeUpward;
        public float cameraPixels, cameraSeconds, strongCameraPixels, strongCameraSeconds, cameraMaxPixels;
        public float waveSeconds, wavePixels, waveMaxPixels, waveRadiusPixels, waveWidthPixels;
        public bool distortAllHits;
        // Draw the hit prefabs as pencil on paper (white body, graphite outline) instead of their glow colours.
        public bool paperInk;
        // Size of every hit prefab over its recipe scale (1 = recipe size). Missing in older data means 1.
        public float prefabScale=1;
        public string paperOutlineColor = "#1A1A1A";
        public string[] strongActions;
        public HitRecipeData[] recipes;
        // Recipe drawn only on critical hits; every other recipe is the random pool of ordinary hits.
        // Missing means critical hits share the ordinary pool.
        public string criticalRecipe;
        public HitFormData[] forms;
    }
    [Serializable] public sealed class HitFeedbackTimeData
    {
        public int schemaVersion;
        public string status,trigger;
        public float scale, accentSeconds;
        public void Validate()
        {
            if (schemaVersion != 2 || trigger!="player-death" || !HitFeedbackSettings.Positive(scale) || scale > 1 ||
                !HitFeedbackSettings.Positive(accentSeconds))
                throw new ArgumentException("Invalid feedback time source; only player-death schema 2 is supported.");
        }
    }
    // The Unity boundary passes a detached JSON copy. No mutable DTO is exposed.
    public sealed class HitFeedbackSettings
    {
        readonly HitFeedbackData data;
        readonly Dictionary<string, HitRecipeData> recipes;
        readonly Dictionary<string, string> forms;
        readonly HashSet<string> strong;
        readonly string[] ordinaryKeys;
        public int MaxHits => data.maxActiveHits;
        public int MaxInstances => data.maxInstancesPerPrefab;
        public int MaxParticles => data.maxParticles;
        public int MaxWaves => data.maxWaves;
        public int MaxDebris => data.maxDebrisBodies;
        public int DebrisPieces => data.debrisPieces;
        public float DebrisLifetime => data.debrisLifetime;
        public float KickCooldown => data.debrisKickCooldown;
        public float DebrisImpulse => data.debrisImpulse;
        public float DebrisUpward => data.debrisUpward;
        public float DebrisMaxKickSpeed => data.debrisMaxKickSpeed;
        public float DebrisAlpha => data.debrisAlpha;
        public float DebrisDesaturate => data.debrisDesaturate;
        public float DebrisStrikeSpeed => data.debrisStrikeSpeed;
        public float DebrisStrikeUpward => data.debrisStrikeUpward;
        public float CameraMax => data.cameraMaxPixels;
        public float WaveSeconds => data.waveSeconds;
        public float WavePixels => data.wavePixels;
        public float WaveMaxPixels => data.waveMaxPixels;
        public float WaveRadius => data.waveRadiusPixels;
        public float WaveWidth => data.waveWidthPixels;
        public float CameraPixels(bool heavy) => heavy ? data.strongCameraPixels : data.cameraPixels;
        public float CameraSeconds(bool heavy) => heavy ? data.strongCameraSeconds : data.cameraSeconds;
        public bool Strong(string action) => action != null && strong.Contains(action);
        public bool Distorts(string action) => data.distortAllHits || Strong(action);
        public bool PaperInk => data.paperInk;
        public float PrefabScale => data.prefabScale;
        public string PaperOutlineColor => data.paperOutlineColor;
        public static bool ValidOutlineColor(string value) => value != null && value.Length == 7 && value[0] == '#' &&
            uint.TryParse(value.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
        public string Recipe(string form) => form != null && forms.TryGetValue(form, out var key) ? key : null;
        public float Scale(string key) => recipes[key].scale;
        public float Rotation(string key) => recipes[key].rotation;
        public IEnumerable<string> RecipeKeys => recipes.Keys;
        public string CriticalRecipe => data.criticalRecipe;
        public IReadOnlyList<string> OrdinaryRecipeKeys => ordinaryKeys;
        // One prefab per accepted hit: the critical recipe on a critical hit, otherwise a pick from the ordinary pool.
        // The seed comes from the hit itself, so nothing consumes the game's random stream.
        public string PickRecipe(uint seed, bool critical)
        {
            if (critical && data.criticalRecipe != null) return data.criticalRecipe;
            unchecked
            {
                seed ^= seed >> 16; seed *= 0x7feb352du;
                seed ^= seed >> 15; seed *= 0x846ca68bu;
                seed ^= seed >> 16;
            }
            return ordinaryKeys[seed % (uint)ordinaryKeys.Length];
        }
        public string SourcePrefab(string key) => recipes[key].sourcePrefab;
        public static bool Positive(float f) => !float.IsNaN(f) && !float.IsInfinity(f) && f > 0;
        public HitFeedbackSettings(HitFeedbackData source)
        {
            if (source == null || source.schemaVersion != 1 || source.recipes == null || source.recipes.Length==0 || source.forms == null || source.forms.Length==0 || source.strongActions == null ||
                source.maxActiveHits < 1 || source.maxActiveHits > 1024 || source.maxInstancesPerPrefab < 1 || source.maxParticles < 1 ||
                source.maxWaves < 1 || source.maxWaves > 8 || source.maxDebrisBodies < 1 || source.maxDebrisBodies > 1024 || source.debrisPieces < 2 || source.debrisPieces > 32)
                throw new ArgumentException("Invalid hit-feedback source schema or budgets.");
            foreach (var f in new[] { source.debrisLifetime, source.debrisKickCooldown, source.debrisImpulse, source.debrisUpward, source.debrisMaxKickSpeed,
                source.cameraSeconds, source.strongCameraSeconds, source.cameraMaxPixels, source.waveSeconds, source.wavePixels, source.waveMaxPixels, source.waveRadiusPixels, source.waveWidthPixels, source.prefabScale })
                if (!Positive(f)) throw new ArgumentException("Feedback values must be finite and positive.");
            foreach (var f in new[] { source.cameraPixels, source.strongCameraPixels })
                if (float.IsNaN(f) || float.IsInfinity(f) || f < 0) throw new ArgumentException("Camera strength must be finite and nonnegative.");
            if (!Positive(source.debrisAlpha) || source.debrisAlpha > 1 || !(source.debrisDesaturate >= 0 && source.debrisDesaturate <= 1))
                throw new ArgumentException("Debris alpha must be in (0,1] and desaturation in [0,1].");
            foreach (var f in new[] { source.debrisStrikeSpeed, source.debrisStrikeUpward })
                if (float.IsNaN(f) || float.IsInfinity(f) || f < 0) throw new ArgumentException("Debris strike velocity must be finite and nonnegative.");
            if (!ValidOutlineColor(source.paperOutlineColor))
                throw new ArgumentException("Paper outline color must be #RRGGBB.");
            // Copy all scalar fields, and copy each authoring row defensively.
            data = new HitFeedbackData {
                maxActiveHits=source.maxActiveHits,maxInstancesPerPrefab=source.maxInstancesPerPrefab,maxParticles=source.maxParticles,
                maxWaves=source.maxWaves,maxDebrisBodies=source.maxDebrisBodies,debrisPieces=source.debrisPieces,
                debrisLifetime=source.debrisLifetime,debrisKickCooldown=source.debrisKickCooldown,debrisImpulse=source.debrisImpulse,debrisUpward=source.debrisUpward,debrisMaxKickSpeed=source.debrisMaxKickSpeed,
                debrisAlpha=source.debrisAlpha,debrisDesaturate=source.debrisDesaturate,debrisStrikeSpeed=source.debrisStrikeSpeed,debrisStrikeUpward=source.debrisStrikeUpward,
                cameraPixels=source.cameraPixels,cameraSeconds=source.cameraSeconds,strongCameraPixels=source.strongCameraPixels,strongCameraSeconds=source.strongCameraSeconds,
                cameraMaxPixels=source.cameraMaxPixels,waveSeconds=source.waveSeconds,wavePixels=source.wavePixels,waveMaxPixels=source.waveMaxPixels,waveRadiusPixels=source.waveRadiusPixels,waveWidthPixels=source.waveWidthPixels,
                distortAllHits=source.distortAllHits,paperInk=source.paperInk,prefabScale=source.prefabScale,
                paperOutlineColor=source.paperOutlineColor, criticalRecipe=source.criticalRecipe };
            recipes = new Dictionary<string, HitRecipeData>(StringComparer.Ordinal);
            foreach (var r in source.recipes)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.key) || string.IsNullOrWhiteSpace(r.sourcePrefab) || !Positive(r.scale) ||
                    float.IsNaN(r.rotation) || float.IsInfinity(r.rotation) || recipes.ContainsKey(r.key)) throw new ArgumentException("Invalid or duplicate feedback recipe.");
                recipes.Add(r.key, new HitRecipeData {key=r.key,sourcePrefab=r.sourcePrefab,scale=r.scale,rotation=r.rotation});
            }
            if (source.criticalRecipe != null && !recipes.ContainsKey(source.criticalRecipe)) throw new ArgumentException("Critical hit recipe must be one of the recipes.");
            ordinaryKeys = source.recipes.Select(r => r.key).Where(k => k != source.criticalRecipe).ToArray();
            if (ordinaryKeys.Length == 0) throw new ArgumentException("Ordinary hits need at least one recipe besides the critical one.");
            forms = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var f in source.forms)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.form) || f.recipe == null || !recipes.ContainsKey(f.recipe) || forms.ContainsKey(f.form)) throw new ArgumentException("Invalid form feedback binding.");
                forms.Add(f.form, f.recipe);
            }
            strong = new HashSet<string>(source.strongActions, StringComparer.Ordinal);
            if (strong.Count != source.strongActions.Length || strong.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Invalid strong action qualification.");
        }
    }
}
