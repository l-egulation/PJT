using UnityEngine;

namespace Deviant.Chapter2
{
    // Landing-driven framing, matching the approved waterway preview. Normal and double jumps keep the exact frame.
    // Leaving that jump band (falls, drop-throughs, wall-jump shafts) tracks the player ahead of the movement
    // so the terrain the player is heading into stays on screen.
    public sealed class Chapter2CameraFraming
    {
        // Camera center above the feet while standing.
        public const float GroundOffset=3.7f;
        // The held frame covers feet from HoldBelow under the last floor to HoldAbove over it (double jump apex is 6).
        public const float HoldBelow=.5f,HoldAbove=6.5f;
        const float LeadSeconds=.12f,FallLeadMax=4f,RiseLeadMax=2.5f;
        public float GoalY { get; private set; }
        public bool FreezeVerticalVelocity { get; private set; }
        // True while the frame leads a vertical move outside the jump band; the camera then follows tightly.
        public bool Tracking { get; private set; }
        float floorY;bool initialized,wasGrounded;
        public float Step(float feetY,bool grounded,float currentY,bool snap=false)=>Step(feetY,0,grounded,currentY,snap);
        public float Step(float feetY,float velocityY,bool grounded,float currentY,bool snap=false)
        {
            FreezeVerticalVelocity=false;
            if(snap||!initialized){initialized=true;floorY=feetY;GoalY=feetY+GroundOffset;wasGrounded=grounded;Tracking=false;return GoalY;}
            if(!grounded&&wasGrounded){GoalY=currentY;FreezeVerticalVelocity=true;Tracking=false;}
            if(grounded){
                if(Mathf.Abs(feetY-floorY)>=.25f||!wasGrounded&&Tracking){floorY=feetY;GoalY=feetY+GroundOffset;}
                Tracking=false;
            }else{
                // The band is measured from the frame itself, so a tracked move keeps following without a new landing.
                float frameFloor=GoalY-GroundOffset;
                if(feetY<frameFloor-HoldBelow){GoalY=Mathf.Min(GoalY,FallingLimit(feetY)-Mathf.Min(FallLeadMax,Mathf.Max(0,-velocityY)*LeadSeconds));Tracking=true;}
                else if(feetY>frameFloor+HoldAbove){GoalY=Mathf.Max(GoalY,RisingLimit(feetY)+Mathf.Min(RiseLeadMax,Mathf.Max(0,velocityY)*LeadSeconds));Tracking=true;}
            }
            wasGrounded=grounded;return GoalY;
        }
        // Highest camera center that still shows the jump band below the feet.
        public static float FallingLimit(float feetY)=>feetY+GroundOffset+HoldBelow;
        // Lowest camera center that still shows the jump band above the feet.
        public static float RisingLimit(float feetY)=>feetY+GroundOffset-HoldAbove;
    }
}
