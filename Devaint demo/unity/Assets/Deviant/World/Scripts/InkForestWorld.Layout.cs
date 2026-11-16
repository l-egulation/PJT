using System;
using System.Collections.Generic;
using Deviant.World.Contracts;
using UnityEngine;

namespace Deviant.World
{
    public partial class InkForestWorld
    {
        ForestLayoutSource layoutSource;
        ForestChapterViews layoutViews;
        ChapterSession chapter;
        readonly List<int> retiringModules = new List<int>();
        bool layoutEnded;
        public event Action LayoutEnded;
        public string LayoutError { get; private set; }
        public ChapterSession Chapter => chapter;
        public bool HasValidatedLayout => chapter != null && chapter.IsActive;
        public bool TryBindLayoutProfile(LayoutBodyProfile profile, LayoutPoint existingEntry, out string error)
        {
            error = null;
            if (profile == null || profile.Motion == null) { error = "The actual initialized common motor profile is required."; return false; }
            if (layoutEnded) { error = LayoutError; return false; }
            if (chapter != null)
            {
                if (!chapter.IsActive) { error = "The forest layout ended; an explicit new world lifetime is required."; return false; }
                if (!layoutSource.SourceUnchanged(out error)) { EndInvalidLayout(error); return false; }
                if (chapter.Profile.ContentHash == profile.ContentHash) return true;
                return chapter.TryRevalidate(profile, layoutSource, out error);
            }
            ForestChapterViews pendingViews = null;
            try
            {
                var pendingSource = new ForestLayoutSource(data, chunkPrefab, profile, existingEntry);
                pendingViews = new ForestChapterViews(this, pendingSource, active, pool);
                if (!WorldAssembler.TryOpen(pendingSource, new LayoutRequest(ForestLayoutSource.LayoutId, ForestLayoutSource.LayoutVersion), profile,
                    pendingSource, pendingViews, out var pending, out error, pendingSource.CreateProof()))
                { pendingViews.Dispose(); return false; }
                layoutSource = pendingSource; layoutViews = pendingViews; chapter = pending;
                // Adopt current actual chunks only after successful source/proof
                // validation. No authored view, collider or index is recreated.
                foreach (int index in active.Keys)
                    if (!chapter.TryLoadModule(chapter.Layout.Repetition.ModuleId(index), out error)) throw new InvalidOperationException(error);
                EnsureLayoutWindow(existingEntry.X);
                return true;
            }
            catch (Exception exception)
            {
                chapter?.Dispose(); chapter = null; pendingViews?.Dispose(); layoutViews = null; layoutSource = null;
                error = "Forest layout open failed: " + exception.Message; return false;
            }
        }
        public bool TryPrepareLayoutProfile(LayoutBodyProfile profile, out ChapterSession.Revalidation transaction, out string error)
        {
            transaction = null;
            if (layoutEnded || chapter == null || !chapter.IsActive) { error = LayoutError ?? "No active validated forest layout is available."; return false; }
            if (!layoutSource.SourceUnchanged(out error)) return false;
            return chapter.TryPrepareRevalidation(profile, layoutSource, out transaction, out error);
        }
        bool EnsureLayoutWindow(float x)
        {
            if (chapter == null) return false;
            if (!chapter.IsActive) throw new InvalidOperationException("The forest layout ended; no unvalidated fallback stream is allowed.");
            if (!layoutSource.SourceUnchanged(out var error)) { EndInvalidLayout(error); throw new InvalidOperationException(error); }
            var window = planner.Plan(x);
            if (ReferenceEquals(window, CurrentWindow)) return true;
            retiringModules.Clear();
            foreach (int index in active.Keys) if (!window.Contains(index)) retiringModules.Add(index);
            foreach (int index in retiringModules)
                if (!chapter.TryUnloadModule(chapter.Layout.Repetition.ModuleId(index), out error)) throw new InvalidOperationException(error);
            foreach (var placement in window.Placements)
                if (!chapter.TryLoadModule(chapter.Layout.Repetition.ModuleId(placement.Index), out error)) throw new InvalidOperationException(error);
            CurrentWindow = window;
            return true;
        }
        internal void RecordRecycledChunk() => RecycledCount++;
        void EndInvalidLayout(string error)
        {
            if (layoutEnded) return;
            layoutEnded = true; LayoutError = error;
            List<Exception> failures = null;
            var observers = LayoutEnded;
            if (observers != null)
                foreach (Action observer in observers.GetInvocationList())
                    try { observer(); }
                    catch (Exception exception) { if (failures == null) failures = new List<Exception>(); failures.Add(exception); }
            try { chapter?.Dispose(); }
            catch (Exception exception) { if (failures == null) failures = new List<Exception>(); failures.Add(exception); }
            if (failures != null) throw new AggregateException("The forest layout ended; one or more observers failed cleanup.", failures);
        }
        void CloseWorldLayout()
        {
            chapter?.Dispose(); layoutViews?.Dispose(); chapter = null; layoutViews = null; layoutSource = null;
        }
    }
}
