using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Deviant.Presentation.Unity
{
    // A camera-specific render port: final transparent world -> separate RG color -> overlay HUD.
    // Never replaces targetTexture or reads last frame's color from LateUpdate.
    public sealed class WorldDistortionOutput : IDisposable
    {
        sealed class Wave { public Vector2 center; public float age,duration,pixels,radius,width; }
        readonly List<Wave> waves=new List<Wave>();
        readonly Vector4[] centers=new Vector4[8],parameters=new Vector4[8];
        readonly Camera camera;
        readonly Material material;
        readonly DistortionPass pass;
        readonly int capacity;
        readonly float maximumPixels;
        bool disposed;
        float intensity=1;
        public int ActiveCount=>waves.Count;
        public int RenderedFrames { get; private set; }
        public string LastError { get; private set; }
        public float Intensity
        {
            get=>intensity;
            set{if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>1)throw new ArgumentOutOfRangeException(nameof(value));intensity=value;}
        }
        public WorldDistortionOutput(Camera camera,Shader shader,int capacity,float maximumPixels)
        {
            if(!camera || !shader || capacity<1 || capacity>8 || !Deviant.Definitions.HitFeedbackSettings.Positive(maximumPixels))throw new ArgumentException("Explicit world camera, shader, wave budget and finite positive displacement limit required.");
            this.camera=camera;this.capacity=capacity;this.maximumPixels=maximumPixels;
            material=new Material(shader){name="Hit feedback world distortion",hideFlags=HideFlags.HideAndDontSave};
            pass=new DistortionPass(this);
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;
        }
        public bool Add(Vector2 center,float seconds,float pixels,float radius,float width)
        {
            if(disposed)throw new ObjectDisposedException(nameof(WorldDistortionOutput));
            if(!Deviant.Definitions.HitFeedbackSettings.Positive(seconds)||!Deviant.Definitions.HitFeedbackSettings.Positive(pixels)||
                !Deviant.Definitions.HitFeedbackSettings.Positive(radius)||!Deviant.Definitions.HitFeedbackSettings.Positive(width)||
                float.IsNaN(center.x)||float.IsInfinity(center.x)||float.IsNaN(center.y)||float.IsInfinity(center.y))throw new ArgumentException("Invalid distortion wave.");
            if(waves.Count>=capacity)return false;
            waves.Add(new Wave{center=center,duration=seconds,pixels=pixels,radius=radius,width=width});return true;
        }
        public void Advance(float delta)
        {
            if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
            for(int i=waves.Count-1;i>=0;i--)if((waves[i].age+=delta)>=waves[i].duration)waves.RemoveAt(i);
        }
        public void Clear()=>waves.Clear();
        void BeforeCamera(ScriptableRenderContext context,Camera rendering)
        {
            if(disposed || rendering!=camera || !camera.isActiveAndEnabled || waves.Count==0 || intensity==0)return;
            if(!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {LastError="World distortion requires the configured URP render graph.";return;}
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }
        void Prepare(int width,int height)
        {
            for(int i=0;i<waves.Count;i++)
            {
                var wave=waves[i];var uv=camera.WorldToViewportPoint(new Vector3(wave.center.x,wave.center.y,0));
                float age=Mathf.Clamp01(wave.age/wave.duration);
                centers[i]=new Vector4(uv.x,uv.y,age*wave.radius,wave.width);
                parameters[i]=new Vector4(uv.z>0?wave.pixels*(1-age)*intensity:0,0,0,0);
            }
            material.SetInt("_WaveCount",waves.Count);material.SetVectorArray("_Waves",centers);material.SetVectorArray("_WaveParams",parameters);
            // Radius/strength remain 960x540 internal pixels at arbitrary output sizes.
            material.SetVector("_InternalSize",new Vector4(540f*width/height,540,0,0));
            material.SetFloat("_MaximumPixels",maximumPixels*intensity);
        }
        sealed class DistortionPass : ScriptableRenderPass
        {
            readonly WorldDistortionOutput owner;
            public DistortionPass(WorldDistortionOutput owner)
            {this.owner=owner;renderPassEvent=RenderPassEvent.AfterRenderingTransparents;requiresIntermediateTexture=true;
                // Renderer2D 17.6 derives its color allocation from pass inputs;
                // unlike UniversalRenderer it does not inspect the flag above.
                ConfigureInput(ScriptableRenderPassInput.Color);
            }
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                if(owner.disposed || !owner.material)return;
                var resources=frame.Get<UniversalResourceData>();
                if(resources.isActiveTargetBackBuffer){owner.LastError="Renderer did not provide an intermediate transparent-world color.";return;}
                var source=resources.activeColorTexture;var desc=graph.GetTextureDesc(source);
                owner.Prepare(desc.width,desc.height);
                desc.name="HitFeedbackWorldColor";desc.clearBuffer=false;desc.msaaSamples=MSAASamples.None;
                var output=graph.CreateTexture(desc);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source,output,owner.material,0),passName:"Hit feedback transparent-world distortion");
                resources.cameraColor=output;owner.RenderedFrames++;owner.LastError=null;
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;waves.Clear();RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            if(material)UnityEngine.Object.Destroy(material);
        }
    }
}
