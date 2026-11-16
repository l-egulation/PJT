using UnityEngine;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
namespace Deviant.World
{
    [DefaultExecutionOrder(100)]
    public class InkForestCamera : MonoBehaviour
    {
        public Transform followTarget;
        public InkForestWorld world;
        Vector3 smooth;
        CameraOutputBinding output;
        CameraOutputBinding.BaseSource baseline;
        public ICameraRequests Requests => output ? output.Requests : null;
        void Awake()
        {
            smooth=transform.position;
            output=CameraOutputBinding.GetOrCreate(GetComponent<Camera>());
            baseline=output.BindBaseSource("forest follow",()=>{if(world)world.PositionParallax();});
            if(!Application.isBatchMode){QualitySettings.vSyncCount=1;Application.targetFrameRate=60;}
        }
        void OnDestroy(){baseline?.Dispose();}
        void Submit(){baseline.Submit(new CameraPosition(smooth.x,smooth.y,smooth.z));output.ApplyNow();}
        public void SnapToTarget()
        {
            if (!followTarget) return;
            Vector2 foot=followTarget.position;
            smooth=new Vector3(Mathf.Max(10,foot.x-1.25f),Mathf.Max(-5.625f,world.data.FloorTop(foot.x)+2.875f,foot.y-.9375f),-10);
            Submit();
        }
        void LateUpdate(){Follow(Time.deltaTime);}
        public void Follow(float dt)
        {
            // Follow the interpolated render pose, rather than the 50Hz physics pose.
            if (!followTarget) return;
            Vector2 foot=followTarget.position;
            float x=smooth.x;
            if(foot.x>x+1.25f)x=foot.x-1.25f;
            if(foot.x<x-2.1875f)x=foot.x+2.1875f;
            x=Mathf.Max(10,x);
            float y=Mathf.Max(-5.625f,world.data.FloorTop(foot.x)+2.875f,foot.y-.9375f);
            // Horizontal dead-zone follow has no accumulating run/dash lag.
            smooth.x=x;
            smooth.y=Mathf.Lerp(smooth.y,y,1-Mathf.Exp(-7*dt));smooth.z=-10;
            Submit();
        }
    }
}
