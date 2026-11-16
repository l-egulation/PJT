using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    // A complete replacement is prepared before publication. Existing execution
    // media and failed captures never retain references to these mutable DTOs.
    public static class MotionRegistryMedia
    {
        public static CharacterAnimationSet[] Project(CharacterAnimationCatalog media, MotionSourceRegistry registry)
        {
            if (media?.sets == null || registry == null) throw new ArgumentException("Complete motion registry and supplied media are required.");
            var sets = (CharacterAnimationSet[])media.sets.Clone();
            for (int setIndex = 0; setIndex < sets.Length; setIndex++)
            {
                var set = sets[setIndex]; if (set?.clips == null) continue;
                var clips = (CharacterClip[])set.clips.Clone();
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    var clip = clips[clipIndex]; var motion = registry.Motion(set.form + "/" + clip?.name);
                    if (motion == null || motion.IsDelegated) continue;
                    // A deferred clip stays deferred: its projection reads the same Resources frames on first use.
                    bool defer = clip.Deferred;
                    var source = defer ? null : clip.sprites;
                    if (clip.frames == null || source != null && source.Length != clip.frames.Length)
                        throw new ArgumentException("Body sprite bindings do not match supplied frames: " + motion.Id);
                    var sprites = source == null ? null : new Sprite[motion.Frames.Count];
                    bool unchanged = clip.frames.SequenceEqual(motion.Frames);
                    var nativeMuzzles = NativeMuzzles(clip);
                    var muzzleX = nativeMuzzles == null ? null : new float[motion.Frames.Count];
                    var muzzleY = nativeMuzzles == null ? null : new float[motion.Frames.Count];
                    for (int index = 0; index < motion.Frames.Count; index++)
                    {
                        int original = unchanged ? index : Array.IndexOf(clip.frames, motion.Frames[index]);
                        if (original >= 0) { if (sprites != null) sprites[index] = source[original]; }
                        else if (defer && media.motionSprites != null && media.motionSprites.ContainsKey(motion.Frames[index])) { }
                        else if (media.TryMotionSprite(motion.Frames[index], out var sprite)) { if (sprites != null) sprites[index] = sprite; }
                        else throw new ArgumentException("Known body frame has no supplied palette binding: " + motion.Frames[index]);
                        if (nativeMuzzles != null)
                        {
                            if (!nativeMuzzles.TryGetValue(motion.Frames[index], out var muzzle))
                                throw new ArgumentException("Known body frame has no native muzzle binding: " + motion.Frames[index]);
                            muzzleX[index] = muzzle.x; muzzleY[index] = muzzle.y;
                        }
                    }
                    clips[clipIndex] = new CharacterClip { name = clip.name, frames = motion.Frames.ToArray(), seconds = motion.Seconds.ToArray(),
                        stepIds = motion.StepIds.ToArray(), contact = -1, sourceImpact = motion.SourceImpact, fxStart = motion.FxStart, sprites = sprites,
                        sourceRelease=clip.sourceRelease,muzzleX=muzzleX,muzzleY=muzzleY,nativeMuzzles=nativeMuzzles };
                    if (defer) clips[clipIndex].DeferLoad();
                }
                sets[setIndex] = new CharacterAnimationSet { form = set.form, gameplayForm=set.gameplayForm,launcherMode=set.launcherMode,clips = clips, effects = set.effects,
                    iconUvRect = set.iconUvRect == null ? null : (float[])set.iconUvRect.Clone() };
            }
            return sets;
        }
        static IReadOnlyDictionary<string, Vector2> NativeMuzzles(CharacterClip clip)
        {
            if (clip.nativeMuzzles != null) return clip.nativeMuzzles;
            if (clip.muzzleX == null && clip.muzzleY == null) return null;
            if (clip.muzzleX == null || clip.muzzleY == null || clip.muzzleX.Length != clip.frames.Length || clip.muzzleY.Length != clip.frames.Length)
                throw new ArgumentException("Native muzzle bindings do not match supplied frames: " + clip.name);
            var anchors = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            for (int index = 0; index < clip.frames.Length; index++)
                anchors[clip.frames[index]] = new Vector2(clip.muzzleX[index], clip.muzzleY[index]);
            return new ReadOnlyDictionary<string, Vector2>(anchors);
        }
    }
}
