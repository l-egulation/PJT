using System;
using System.Collections.Generic;
using UnityEngine;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    // Samples the reviewed Canvas frames on the arena clock. No hit/movement writes.
    public sealed class ShieldSkillPresentation : IDisposable
    {
        [Serializable] sealed class Clip { public string name; public int count, width, height, columns; public float fps, ppu; public float[] pivot; }
        [Serializable] sealed class Catalog { public string format; public Clip[] clips; }
        sealed class Effect { public string clip; public SpriteRenderer view; public CombatActor follow; public float at; public int life; }
        readonly CombatArena arena;
        readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        readonly List<Effect> effects = new List<Effect>();
        readonly Dictionary<ArenaProjectile, LineRenderer[]> reflections = new Dictionary<ArenaProjectile, LineRenderer[]>();
        readonly Dictionary<int, Sprite> body = new Dictionary<int, Sprite>();
        GameObject root;
        Material trailMaterial;
        SpriteRenderer guardBack, guardFront;
        Guid seenAction;
        float guardUntil, guardStarted, guardBroken = -1, previousShield, lastNow;
        int playerLife;
        bool basicEmitted;
        public int ActiveEffects => effects.Count;
        public bool DisplaysGuard(CombatActor actor) => actor == arena.Player && arena.loadCharacterArt && clips.Count > 0 && arena.Now < actor.guardUntil && !actor.Dead;
        public ShieldSkillPresentation(CombatArena value)
        {
            arena = value;
            var text = Resources.Load<TextAsset>("ShieldReviewed/clips");
            if (text)
            {
                var data = JsonUtility.FromJson<Catalog>(text.text);
                if (data.format != "shield-reviewed-v1") throw new InvalidOperationException("Invalid reviewed shield art.");
                foreach (var clip in data.clips) clips.Add(clip.name, clip);
            }
            arena.ResetCompleted += Reset;
            arena.Deactivated += Reset;
        }
        Sprite[] Frames(string name)
        {
            if (frames.TryGetValue(name, out var found)) return found;
            if (!clips.TryGetValue(name, out var clip)) return Array.Empty<Sprite>();
            var texture = Resources.Load<Texture2D>("ShieldReviewed/" + name);
            if (!texture) throw new InvalidOperationException("Missing shield sheet: " + name);
            found = new Sprite[clip.count];
            for (int i = 0; i < found.Length; i++)
                found[i] = Sprite.Create(texture, new Rect((i % clip.columns) * clip.width,
                    texture.height - (i / clip.columns + 1) * clip.height, clip.width, clip.height),
                    new Vector2(clip.pivot[0], clip.pivot[1]), clip.ppu);
            frames.Add(name, found); return found;
        }
        Sprite Sample(string name, float age, bool loop = false)
        {
            var set = Frames(name); if (set.Length == 0 || age < 0) return null;
            int index = Mathf.FloorToInt(age * clips[name].fps);
            return loop ? set[index % set.Length] : index < set.Length ? set[index] : null;
        }
        SpriteRenderer View(string name, int order)
        {
            if (!root) { root = new GameObject("Reviewed shield effects"); root.transform.SetParent(arena.transform, false); }
            var node = new GameObject(name); node.transform.SetParent(root.transform, false);
            var view = node.AddComponent<SpriteRenderer>(); view.sortingOrder = order;
            if (arena.spriteMaterial) view.sharedMaterial = arena.spriteMaterial;
            if (arena.Player.visual) view.sortingLayerID = arena.Player.visual.sortingLayerID;
            return view;
        }
        int Order => arena.Player.visual ? arena.Player.visual.sortingOrder : 10;
        void Spawn(string clip, Vector2 position, int direction, CombatActor follow = null, float? at = null)
        {
            if (!clips.ContainsKey(clip)) return;
            var view = View(clip, Order + 2); view.transform.position = position; view.flipX = direction < 0;
            effects.Add(new Effect { clip = clip, view = view, follow = follow, at = at ?? arena.Now, life = follow ? follow.LifeVersion : 0 });
        }
        public void Parry(CombatActor owner, int direction) => Spawn("parry-success", owner.Position, direction, owner);
        public void Burst(Vector2 position, int direction) => Spawn("burst", position, direction);
        public void Tick()
        {
            var player = arena.Player;
            if (!player || !arena.loadCharacterArt || clips.Count == 0) return;
            if (arena.Now < lastNow || playerLife != player.LifeVersion) { Reset(); playerLife = player.LifeVersion; }
            lastNow = arena.Now;
            var cast = player.currentCast;
            if (cast && cast.Definition.Id.StartsWith("shield_", StringComparison.Ordinal) && !cast.PresentationCancelled && !player.Dead)
            {
                if (seenAction != cast.ExecutionId)
                {
                    seenAction = cast.ExecutionId; basicEmitted = false;
                    if (cast.Definition.Id == "shield_default") Spawn("parry-empty", player.Position, cast.FeelFacing, player, arena.Now - cast.Age);
                    if (cast.Definition.Id == "shield_movement") Spawn("rush", player.Position, cast.FeelFacing, player, arena.Now - cast.Age);
                }
                float age = cast.Age;
                if (cast.isBasic && cast.ResourceResult?.Status == ActionResourceStatus.Succeeded && !basicEmitted)
                { basicEmitted = true; Spawn("basic", player.Position, cast.FeelFacing, null, arena.Now - Mathf.Max(0, age - cast.ImpactTime)); }
                DrawBody(player, cast, age);
            }
            else seenAction = Guid.Empty;
            DrawGuard(player);
            foreach (var d in arena.decoys)
            {
                if (!d || d.Dead || !d.GetComponent<ShieldDecoyRuntime>() || !d.visual) continue;
                d.visual.sprite = Sample("decoy", 0); d.visual.color = new Color(1, 1, 1, .93f);
                d.visual.transform.localScale = Vector3.one; d.visual.transform.localPosition = Vector3.zero; d.visual.flipX = player.facing < 0;
            }
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i]; var sprite = Sample(e.clip, arena.Now - e.at);
                if (!sprite || e.follow && (e.follow.Dead || e.follow.LifeVersion != e.life))
                { if (e.view) UnityEngine.Object.Destroy(e.view.gameObject); effects.RemoveAt(i); continue; }
                e.view.sprite = sprite;
                e.view.enabled = !arena.FeelFeedback || arena.FeelFeedback.EffectsEnabled;
                if (e.follow) e.view.transform.position = e.follow.Position;
                if (e.clip == "parry-empty" || e.clip == "rush")
                    e.view.enabled &= cast && !cast.PresentationCancelled &&
                        cast.Definition.Id == (e.clip == "rush" ? "shield_movement" : "shield_default");
            }
            DrawReflections();
        }
        void DrawBody(CombatActor player, CombatCast cast, float age)
        {
            if (!player.visual || cast.isBasic) return;
            string id = cast.Definition.Id; int frame;
            if (id == "shield_default") frame = !cast.ShieldParrySucceeded && age < cast.Definition.Effect.ParryWindow ? 49 : age < .36f ? 50 : age < .40f ? 47 : 51;
            else if (id == "shield_range") frame = age < .1f ? 47 : age < .25f ? 49 : age < .34f ? 50 : 47;
            else if (id == "shield_movement") frame = age < .05f ? 46 : age < .1f ? 47 : age < .15f ? 48 : age < .45f ? 49 : age < .6f ? 50 : 51;
            else frame = age < .046f ? 46 : age < .128f ? 47 : age < .229f ? 48 : age < .321f ? 49 : age < .422f ? 50 : 51;
            if (!body.TryGetValue(frame, out var sprite)) body[frame] = sprite = Resources.Load<Sprite>("CharacterApproved11/shield/F" + frame.ToString("000"));
            if (sprite) { player.visual.sprite = sprite; player.visual.transform.localPosition = Vector3.zero; player.visual.transform.localScale = Vector3.one; player.visual.flipX = cast.FeelFacing < 0; }
        }
        void DrawGuard(CombatActor player)
        {
            bool active = arena.Now < player.guardUntil && !player.Dead;
            if (!guardBack) { guardBack = View("Guard back", Order - 1); guardFront = View("Guard front", Order + 1); }
            guardBack.enabled = guardFront.enabled = false;
            if (!active) { guardUntil = 0; previousShield = 0; return; }
            if (guardUntil != player.guardUntil)
            { guardUntil = player.guardUntil; guardStarted = arena.Now; guardBroken = -1; previousShield = player.shield; Spawn("guard-summon", player.Position, player.facing, player); }
            if (player.shield < previousShield)
            {
                if (player.shield <= 0 && guardBroken < 0) guardBroken = arena.Now;
                else Spawn("guard-hit", player.Position, player.facing, player);
            }
            previousShield = player.shield;
            int hp = Mathf.Clamp(Mathf.CeilToInt(player.shield / 6) * 6, 6, 30);
            string name = guardBroken >= 0 ? "guard-break" : "guard-" + hp;
            float age = guardBroken >= 0 ? arena.Now - guardBroken : arena.Now - guardStarted;
            guardBack.sprite = Sample(name + "-back", age, guardBroken < 0);
            guardFront.sprite = Sample(name + "-front", age, guardBroken < 0);
            bool show = !arena.FeelFeedback || arena.FeelFeedback.EffectsEnabled;
            foreach (var v in new[] { guardBack, guardFront })
            { v.enabled = show && v.sprite; v.transform.position = player.Position; v.flipX = player.facing < 0; v.color = new Color(1, 1, 1, Mathf.Clamp01((guardUntil - arena.Now) / .13f)); }
        }
        void DrawReflections()
        {
            var expired = new List<ArenaProjectile>();
            foreach (var pair in reflections) if (!pair.Key || !arena.projectiles.Contains(pair.Key)) expired.Add(pair.Key);
            foreach (var key in expired)
            {
                foreach (var line in reflections[key]) if (line) UnityEngine.Object.Destroy(line.gameObject);
                reflections.Remove(key);
            }
            foreach (var shot in arena.projectiles)
            {
                if (!shot || !shot.Execution.Reflected || shot.source != arena.Player) continue;
                if (!reflections.TryGetValue(shot, out var lines))
                {
                    if (!trailMaterial) trailMaterial = new Material(Shader.Find("Sprites/Default"));
                    lines = new LineRenderer[2];
                    for (int i = 0; i < 2; i++)
                    {
                        var node = new GameObject("White reflected wake"); node.transform.SetParent(shot.transform, false);
                        var line = node.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 8;
                        line.sharedMaterial = trailMaterial; line.startWidth = i == 0 ? .22f : .07f; line.endWidth = 0;
                        line.startColor = new Color(1, 1, .98f, i == 0 ? .20f : .95f); line.endColor = new Color(.9f, .97f, 1, 0);
                        line.sortingOrder = Order + 3; line.sortingLayerID = arena.Player.visual.sortingLayerID; lines[i] = line;
                    }
                    reflections.Add(shot, lines);
                }
                foreach (var line in lines) for (int i = 0; i < 8; i++) line.SetPosition(i, shot.transform.position - (Vector3)shot.velocity.normalized * (i * .17f));
            }
        }
        public void Reset()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null; guardBack = guardFront = null; effects.Clear();
            foreach (var lines in reflections.Values) foreach (var line in lines) if (line) UnityEngine.Object.Destroy(line.gameObject);
            reflections.Clear();
            guardUntil = previousShield = 0; guardBroken = -1; seenAction = Guid.Empty; lastNow = arena.Now;
        }
        public void Dispose()
        {
            Reset(); arena.ResetCompleted -= Reset; arena.Deactivated -= Reset;
            foreach (var set in frames.Values) foreach (var sprite in set) if (sprite) UnityEngine.Object.Destroy(sprite);
            frames.Clear(); if (trailMaterial) UnityEngine.Object.Destroy(trailMaterial);
        }
    }
}
