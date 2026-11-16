using System;
using UnityEditor;
using UnityEngine;

namespace Deviant.Presentation.Editor
{
    // Opening a scene in a fresh checkout otherwise plays without hit VFX, because the bindings that
    // HitFeedbackBridge loads are git-ignored generated assets. Interactive editors build them once.
    [InitializeOnLoad]
    public static class HitFeedbackBindingsGuard
    {
        static HitFeedbackBindingsGuard()
        {
            // Batch tests and builds own their asset state; GameBuilder builds bindings explicitly.
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () => EnsureBuilt();
        }

        public static bool EnsureBuilt()
        {
            try
            {
                if (HitFeedbackAssetBuilder.BindingsExist) return true;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
                HitFeedbackAssetBuilder.Build();
                Debug.Log("Built missing or stale hit feedback bindings (Presentation/Resources/HitFeedback/Generated).");
                return HitFeedbackAssetBuilder.BindingsExist;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hit feedback bindings could not be built; hit VFX stay off until Tools > deviant > Presentation > Build Hit Feedback Bindings succeeds. " + exception.Message);
                return false;
            }
        }
    }
}
