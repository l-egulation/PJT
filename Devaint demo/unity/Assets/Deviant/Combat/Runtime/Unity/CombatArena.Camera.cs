using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        CameraOutputBinding cameraOutput;
        public ICameraRequests CameraRequests => cameraOutput ? cameraOutput.Requests : null;
        void InitializeCameraOutput() => cameraOutput = CameraOutputBinding.GetOrCreate(view);
    }
}
