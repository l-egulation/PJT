using Deviant.Stage1Monsters.Editor;
using UnityEditor;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Editor
{
    public static class Stage1FxPolishVerification
    {
        public static void Apply()
        {
            Stage1MonsterImporter.RefreshApprovedPixelsAndValidate();
            Stage1MonsterEffectImporter.ImportSelected();
            if(!EditorApplication.ExecuteMenuItem("Tools/deviant/Presentation/Build Hit Feedback Bindings"))
                throw new System.InvalidOperationException("Missing hit-feedback binding builder.");
            Debug.Log("STAGE1_FX_POLISH_APPLIED: selected frames, shared claw, gutter trajectory, code burrow.");
        }
    }
}
