using UnityEngine;
using UnityEngine.Rendering;

namespace Stage1VillageExperiment
{
    /// <summary>
    /// Presents the 960x540 world through one native-resolution, point-filtered buffer.
    /// Camera motion/projection ownership remains with Stage1VillageWorld. IMGUI HUDs
    /// draw afterwards at their usual depth; this component does not add a UI package.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9000)]
    public sealed class VillagePixelViewport : MonoBehaviour
    {
        public const int LogicalWidth = 960;
        public const int LogicalHeight = 540;

        public Stage1VillageWorld world;
        public Camera sourceCamera;
        [Tooltip("Keep disabled for batch verification, whose harness owns camera capture targets.")]
        public bool enableInBatchMode;
        public bool ExternalPresentation { get; set; }

        public RenderTexture NativeTexture => nativeTexture;
        public Camera OutputCamera => outputCamera;
        public bool Ready => attachedCamera && nativeTexture && attachedCamera.targetTexture == nativeTexture && outputCamera && outputCamera.enabled;
        public Rect DisplayRect => CalculateDisplayRect(Screen.width, Screen.height);

        Camera attachedCamera, outputCamera;
        RenderTexture nativeTexture, previousTarget;
        Deviant.Presentation.Unity.CameraDisplayProjection displayProjection;
        Rect previousRect;
        float previousAspect;
        bool previousHdr, previousMsaa;
        Deviant.Game.Diagnostics.CombatDiagnostics diagnostics;
        System.Func<Vector3,Vector3> previousProjection;

        void Awake() { useGUILayout = false; }

        void OnEnable()
        {
            // Batch screenshots intentionally use their own target size and camera rect.
            // A batch player smoke test still exercises simulation without this presenter.
            if (Application.isBatchMode && !enableInBatchMode) { enabled = false; return; }
            Camera.onPreCull += BeforeBuiltInCamera;
            RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
        }

        void LateUpdate()
        {
            if (!world) world = GetComponent<Stage1VillageWorld>();
            Camera requested = sourceCamera ? sourceCamera : world ? world.View : null;
            if (attachedCamera && attachedCamera != requested) Detach();
            if (!attachedCamera && requested && (!world || world.Ready)) Attach(requested);
            if (Ready)
            {
                // Recreate after graphics-device loss without changing the camera contract.
                if (!nativeTexture.IsCreated()) nativeTexture.Create();
                PrepareNativeCamera(attachedCamera);
                outputCamera.depth = attachedCamera.depth + 1;
                outputCamera.targetDisplay = attachedCamera.targetDisplay;
            }
        }

        void Attach(Camera camera)
        {
            // Respect an explicit external target. A capture harness can replace our target
            // temporarily; all callbacks below leave that replacement completely alone.
            if (camera.targetTexture) return;
            previousTarget = camera.targetTexture;
            previousRect = camera.rect;
            previousAspect = camera.aspect;
            previousHdr = camera.allowHDR;
            previousMsaa = camera.allowMSAA;
            nativeTexture = new RenderTexture(LogicalWidth, LogicalHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = "Village native 960x540",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                useDynamicScale = false,
                hideFlags = HideFlags.DontSave
            };
            if (!nativeTexture.Create())
            {
                Destroy(nativeTexture); nativeTexture = null;
                Debug.LogWarning("Village native viewport could not allocate its render target; using direct camera output.", this);
                enabled = false;
                return;
            }
            attachedCamera = camera;
            var team=GetComponent<ForestCombatV2.ForestCombatWorld>();
            if(team&&team.diagnostics)
            {
                diagnostics=team.diagnostics;previousProjection=diagnostics.WorldPointToScreen;
                diagnostics.WorldPointToScreen=ProjectWorldPoint;
            }
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = nativeTexture;
            displayProjection = camera.GetComponent<Deviant.Presentation.Unity.CameraDisplayProjection>();
            if (!displayProjection) displayProjection = camera.gameObject.AddComponent<Deviant.Presentation.Unity.CameraDisplayProjection>();
            displayProjection.PresentedTexture = nativeTexture;
            displayProjection.DisplayRect = () => DisplayRect;
            PrepareNativeCamera(camera);
            // A camera whose targetTexture is set only renders that texture. With no
            // camera targeting the display, a standalone SRP player may never produce
            // the backbuffer/IMGUI frame at all, even while the native RT is healthy.
            // This empty camera provides a real display frame; OnGUI below presents
            // the point-filtered RT, followed by the ordinary gameplay HUD.
            var output = new GameObject("Village pixel screen output");
            output.transform.SetParent(transform, false);
            outputCamera = output.AddComponent<Camera>();
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = Color.black;
            outputCamera.cullingMask = 0;
            outputCamera.depth = camera.depth + 1;
            outputCamera.targetDisplay = camera.targetDisplay;
            outputCamera.allowHDR = false;
            outputCamera.allowMSAA = false;
            outputCamera.useOcclusionCulling = false;
            outputCamera.rect = new Rect(0, 0, 1, 1);
        }

        void BeforeBuiltInCamera(Camera camera) { PrepareNativeCamera(camera); }
        void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) { PrepareNativeCamera(camera); }

        void PrepareNativeCamera(Camera camera)
        {
            if (!camera || camera != attachedCamera || camera.targetTexture != nativeTexture || !nativeTexture) return;
            // World.RefreshCamera computes a window letterbox every frame. Our buffer is
            // already 16:9; letterboxing belongs to presentation, never inside this buffer.
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = LogicalWidth / (float)LogicalHeight;
        }

        public static Rect CalculateDisplayRect(int width, int height)
        {
            if (width <= 0 || height <= 0) return new Rect();
            float fit = Mathf.Min(width / (float)LogicalWidth, height / (float)LogicalHeight);
            // Whole-number enlargement keeps all native pixels equally sized. On unusually
            // small windows, fit the complete frame rather than cropping playable terrain.
            float scale = fit >= 1 ? Mathf.Floor(fit) : fit;
            float w = Mathf.Round(LogicalWidth * scale), h = Mathf.Round(LogicalHeight * scale);
            return new Rect(Mathf.Floor((width - w) * .5f), Mathf.Floor((height - h) * .5f), w, h);
        }

        public Vector3 ProjectWorldPoint(Vector3 world)
            =>Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(attachedCamera,world);

        void OnGUI()
        {
            if (ExternalPresentation || !Ready || Event.current.type != EventType.Repaint) return;
            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;
            try
            {
                GUI.depth = 10000; // Larger depth is behind the normal depth-0 HUD.
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
                GUI.color = Color.white;
                GUI.DrawTexture(DisplayRect, nativeTexture, ScaleMode.StretchToFill, false);
            }
            finally { GUI.depth = oldDepth; GUI.color = oldColor; GUI.matrix = oldMatrix; }
        }

        void OnDisable()
        {
            Camera.onPreCull -= BeforeBuiltInCamera;
            RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
            Detach();
        }

        void OnDestroy() { Detach(); }

        void Detach()
        {
            if (displayProjection)
            {
                displayProjection.PresentedTexture = null;
                displayProjection.DisplayRect = null;
                displayProjection = null;
            }
            if(diagnostics&&diagnostics.WorldPointToScreen==ProjectWorldPoint)diagnostics.WorldPointToScreen=previousProjection;
            diagnostics=null;previousProjection=null;
            if (outputCamera)
            {
                outputCamera.enabled = false;
                Destroy(outputCamera.gameObject);
                outputCamera = null;
            }
            if (attachedCamera)
            {
                // Never stomp a capture target installed by another owner.
                if (attachedCamera.targetTexture == nativeTexture)
                {
                    attachedCamera.targetTexture = previousTarget;
                    attachedCamera.rect = previousRect;
                    attachedCamera.aspect = previousAspect;
                }
                attachedCamera.allowHDR = previousHdr;
                attachedCamera.allowMSAA = previousMsaa;
            }
            attachedCamera = null;
            previousTarget = null;
            if (!nativeTexture) return;
            nativeTexture.Release();
            Destroy(nativeTexture);
            nativeTexture = null;
        }
    }
}
