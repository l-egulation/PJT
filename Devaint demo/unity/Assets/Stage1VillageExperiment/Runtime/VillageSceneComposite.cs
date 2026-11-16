using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stage1VillageExperiment
{
    // Blend completed scenes, not individual transparent sprites. Different endpoint
    // silhouettes must reveal their own backgrounds before the single interpolation.
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class VillageSceneComposite : MonoBehaviour
    {
        static readonly int EndpointId = Shader.PropertyToID("_VillageEndpointOverride");
        static readonly int SketchId = Shader.PropertyToID("_SketchTex");
        static readonly int ModeId = Shader.PropertyToID("_VillageArtMode");
        static readonly int WeightId = Shader.PropertyToID("_VillageArtWeight");
        static readonly int BoundaryId = Shader.PropertyToID("_VillageBoundaryPixels");
        static readonly int RangeId = Shader.PropertyToID("_VillageAuthoredRange");
        static readonly int OriginId = Shader.PropertyToID("_VillageViewportOrigin");
        static readonly int RightId = Shader.PropertyToID("_VillageViewportRight");
        static readonly int UpId = Shader.PropertyToID("_VillageViewportUp");

        Camera sourceCamera, sketchCamera;
        VillageArtTransition transition;
        Material composite;
        RenderTexture sketchTexture, colorTexture;
        bool compositePending, renderingEndpoint;
        Vector2 viewportOrigin, viewportRight, viewportUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void ResetEndpoint() { Shader.SetGlobalFloat(EndpointId, -1); }

        public static void Attach(Camera camera, VillageArtTransition art)
        {
            if (!camera || !art) throw new InvalidOperationException("Village scene compositing requires its camera and art transition.");
            var effect = camera.GetComponent<VillageSceneComposite>();
            if (!effect) effect = camera.gameObject.AddComponent<VillageSceneComposite>();
            effect.transition = art;
            effect.EnsureResources();
        }

        void Awake()
        {
            useGUILayout = false;
            sourceCamera = GetComponent<Camera>();
            ResetEndpoint();
        }

        void OnEnable()
        {
            ResetEndpoint();
            RenderPipelineManager.beginCameraRendering+=BeginPipelineCamera;
            RenderPipelineManager.endCameraRendering+=EndPipelineCamera;
        }
        void BeginPipelineCamera(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=sourceCamera||renderingEndpoint)return;
            PrepareEndpoint();
        }
        void EndPipelineCamera(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=sourceCamera||renderingEndpoint)return;
            try
            {
                if(!compositePending)return;
                var target=sourceCamera.targetTexture;
                if(!target)throw new InvalidOperationException("Village endpoint compositing requires the native or capture camera target.");
                EnsureSketchTarget(target);
                if(!colorTexture)
                {
                    var descriptor=sketchTexture.descriptor;descriptor.depthBufferBits=0;
                    colorTexture=new RenderTexture(descriptor){name="Village completed color endpoint",filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};colorTexture.Create();
                }
                context.Submit();
                bool oldSrgb=GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear&&colorTexture.sRGB;
                    Graphics.Blit(target,colorTexture);
                    sketchCamera.CopyFrom(sourceCamera);
                    sketchCamera.transform.SetPositionAndRotation(sourceCamera.transform.position,sourceCamera.transform.rotation);
                    sketchCamera.enabled=false;sketchCamera.targetTexture=sketchTexture;sketchCamera.rect=new Rect(0,0,1,1);
                    sketchCamera.worldToCameraMatrix=sourceCamera.worldToCameraMatrix;sketchCamera.projectionMatrix=sourceCamera.projectionMatrix;
                    renderingEndpoint=true;Shader.SetGlobalFloat(EndpointId,1);
                    // Rendering inside an existing SRP context must use this in-context
                    // API. SubmitRenderRequest starts another frame/context recursively.
                    UniversalRenderPipeline.RenderSingleCamera(context,sketchCamera);
                    context.Submit();
                    ResetEndpoint();composite.SetTexture(SketchId,sketchTexture);
                    GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear&&target.sRGB;
                    Graphics.Blit(colorTexture,target,composite,0);
                }
                finally{GL.sRGBWrite=oldSrgb;}
            }
            finally
            {
                renderingEndpoint=false;compositePending=false;ResetEndpoint();
                if(sketchCamera)sketchCamera.targetTexture=null;
            }
        }

        void EnsureResources()
        {
            if (!composite)
            {
                var shader = Resources.Load<Shader>("Village/VillageSceneComposite");
                if (!shader || !shader.isSupported)
                    throw new InvalidOperationException("The village endpoint composite shader is required.");
                composite = new Material(shader) { name = "Village completed-scene blend", hideFlags = HideFlags.HideAndDontSave };
            }
            if (!sketchCamera)
            {
                // A bare camera, never a cloned GameObject: no compositor, camera
                // controller, simulation scripts or audio listener can run recursively.
                var node = new GameObject("Village sketch endpoint camera") { hideFlags = HideFlags.HideAndDontSave };
                sketchCamera = node.AddComponent<Camera>();
                sketchCamera.enabled = false;
            }
        }

        void PrepareEndpoint()
        {
            compositePending = false;
            ResetEndpoint();
            if (!transition || !transition.isActiveAndEnabled) return;
            float height = sourceCamera.orthographicSize * 2;
            Vector3 right = sourceCamera.transform.right * (height * sourceCamera.aspect);
            Vector3 up = sourceCamera.transform.up * height;
            Vector3 origin = sourceCamera.transform.position - (right + up) * .5f;
            viewportOrigin = new Vector2(origin.x * 32, -origin.y * 32);
            viewportRight = new Vector2(right.x * 32, -right.y * 32);
            viewportUp = new Vector2(up.x * 32, -up.y * 32);
            if (!NeedsComposite()) return;
            EnsureResources();
            composite.SetFloat(ModeId, (float)transition.Mode);
            composite.SetFloat(WeightId, transition.Weight);
            composite.SetFloat(BoundaryId, transition.BoundaryPixels);
            composite.SetVector(RangeId, new Vector4(transition.AuthoredSketchUntilPixels, transition.AuthoredNormalFromPixels, 0, 0));
            composite.SetVector(OriginId, new Vector4(viewportOrigin.x, viewportOrigin.y, 0, 0));
            composite.SetVector(RightId, new Vector4(viewportRight.x, viewportRight.y, 0, 0));
            composite.SetVector(UpId, new Vector4(viewportUp.x, viewportUp.y, 0, 0));
            compositePending = true;
            Shader.SetGlobalFloat(EndpointId, 0);
        }

        bool NeedsComposite()
        {
            // Crisp authored regions choose each registered object/material directly.
            // Completing and crossfading two scenes would reintroduce double outlines.
            if(transition.CrispRegions&&(transition.Mode==VillageArtMode.Authored||transition.Mode==VillageArtMode.Region))return false;
            if (transition.Mode == VillageArtMode.Color || transition.Mode == VillageArtMode.Sketch) return false;
            if (transition.Mode == VillageArtMode.Blend) return transition.Weight > 0 && transition.Weight < 1;
            float left = viewportOrigin.x + Mathf.Min(0, viewportRight.x) + Mathf.Min(0, viewportUp.x);
            float right = viewportOrigin.x + Mathf.Max(0, viewportRight.x) + Mathf.Max(0, viewportUp.x);
            if (transition.Mode == VillageArtMode.Authored)
                return right > transition.AuthoredSketchUntilPixels && left < transition.AuthoredNormalFromPixels;
            // The review curve stays within +/-40px, around its 180px-wide band.
            return right > transition.BoundaryPixels - 130 && left < transition.BoundaryPixels + 130;
        }

        void EnsureSketchTarget(RenderTexture source)
        {
            if (sketchTexture && (sketchTexture.width != source.width || sketchTexture.height != source.height ||
                sketchTexture.graphicsFormat != source.graphicsFormat || sketchTexture.antiAliasing != source.antiAliasing))
                ReleaseSketchTarget();
            if (!sketchTexture)
            {
                var descriptor = source.descriptor;
                descriptor.depthBufferBits = 24;
                descriptor.bindMS = false;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.useDynamicScale = false;
                descriptor.memoryless = RenderTextureMemoryless.None;
                sketchTexture = new RenderTexture(descriptor)
                {
                    name = "Village completed sketch endpoint",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            if (!sketchTexture.IsCreated() && !sketchTexture.Create())
                throw new InvalidOperationException("Could not allocate the village sketch endpoint target.");
        }


        void ReleaseSketchTarget()
        {
            if (!sketchTexture) return;
            sketchTexture.Release();
            DisposeOwned(sketchTexture);
            sketchTexture = null;
            if(colorTexture){colorTexture.Release();DisposeOwned(colorTexture);colorTexture=null;}
        }

        static void DisposeOwned(UnityEngine.Object owned)
        {
            if (!owned) return;
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }

        void ReleaseResources()
        {
            compositePending = false;
            ResetEndpoint();
            if (sketchCamera)
            {
                sketchCamera.enabled = false;
                sketchCamera.targetTexture = null;
                DisposeOwned(sketchCamera.gameObject);
                sketchCamera = null;
            }
            ReleaseSketchTarget();
            DisposeOwned(composite);
            composite = null;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeginPipelineCamera;
            RenderPipelineManager.endCameraRendering-=EndPipelineCamera;
            ReleaseResources();
        }
        void OnDestroy() { ReleaseResources(); }
    }
}
