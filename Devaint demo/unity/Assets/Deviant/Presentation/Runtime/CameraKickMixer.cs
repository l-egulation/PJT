using System;
using System.Collections.Generic;

namespace Deviant.Presentation
{
    public readonly struct FeedbackOffset
    {
        public float X { get; }
        public float Y { get; }
        public FeedbackOffset(float x,float y) { X=x;Y=y; }
    }
    public sealed class CameraKickMixer
    {
        sealed class Kick { public float x,y,pixels,duration,age; }
        readonly List<Kick> kicks=new List<Kick>();
        readonly int capacity;
        readonly float maximum;
        float intensity=1;
        public float Intensity
        {
            get=>intensity;
            set { if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>1)throw new ArgumentOutOfRangeException(nameof(value));intensity=value; }
        }
        public int ActiveCount=>kicks.Count;
        public CameraKickMixer(int capacity,float maxPixels)
        { if(capacity<1 || !Deviant.Definitions.HitFeedbackSettings.Positive(maxPixels))throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;maximum=maxPixels; }
        public bool Add(float x,float y,float pixels,float seconds)
        {
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||!Deviant.Definitions.HitFeedbackSettings.Positive(seconds)||
                float.IsNaN(pixels)||float.IsInfinity(pixels)||pixels<0)throw new ArgumentOutOfRangeException(nameof(pixels));
            double length=Math.Sqrt((double)x*x+(double)y*y);
            if(length<.000001 || pixels==0 || kicks.Count>=capacity)return false;
            kicks.Add(new Kick{x=(float)(x/length),y=(float)(y/length),pixels=pixels,duration=seconds});return true;
        }
        public void Advance(float accentDelta)
        {
            if(float.IsNaN(accentDelta)||float.IsInfinity(accentDelta)||accentDelta<0)throw new ArgumentOutOfRangeException(nameof(accentDelta));
            for(int i=kicks.Count-1;i>=0;i--)if((kicks[i].age+=accentDelta)>=kicks[i].duration)kicks.RemoveAt(i);
        }
        public FeedbackOffset ReadPixels()
        {
            float x=0,y=0;
            foreach(var k in kicks){float envelope=1-k.age/k.duration;envelope*=envelope;x+=k.x*k.pixels*envelope;y+=k.y*k.pixels*envelope;}
            float length=(float)Math.Sqrt(x*x+y*y),scale=(length>maximum?maximum/length:1)*intensity;
            return new FeedbackOffset(x*scale,y*scale);
        }
        public void Clear()=>kicks.Clear();
    }
}
