using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;

namespace Deviant.Presentation
{
    public enum HitFeedbackResult { Started, Duplicate, Suppressed, MissingBinding, Expired, InvalidContext, AcceptedWithoutMedia }
    public sealed class HitFeedbackSession : IDisposable
    {
        sealed class Clock:ICueClock {public float now,started;public float ReadElapsed()=>now-started;}
        sealed class Active {public ICueScope scope;public ICueHandle handle;public Clock clock;}
        sealed class Reject:ICueViewFactory
        {public IDisposable Begin(ICueDefinition definition,CueAnchor anchor,Action<CueViewResult> resolved){resolved(CueViewResult.Failure("A captured hit factory is required."));return null;}}
        readonly CuePlayer player=new CuePlayer(new Reject());
        readonly List<Active> active=new List<Active>();
        readonly int maximum;
        long latest;
        public int ActiveCount=>active.Count;
        public long Started {get;private set;}
        public long Suppressed {get;private set;}
        public string LastError {get;private set;}
        public HitFeedbackSession(int maximum){if(maximum<1)throw new ArgumentOutOfRangeException(nameof(maximum));this.maximum=maximum;}
        public HitFeedbackResult Consume(long sequence,Guid execution,float x,float y,float at,float now,PrefabCueDefinition definition,ICueViewFactory factory,bool playMedia=true)
        {
            if(sequence<=latest)return HitFeedbackResult.Duplicate;latest=sequence;
            if(definition==null||factory==null)return HitFeedbackResult.MissingBinding;
            if(float.IsNaN(at)||float.IsInfinity(at)||float.IsNaN(now)||float.IsInfinity(now)||execution==Guid.Empty)return HitFeedbackResult.InvalidContext;
            if(now-at>=definition.Duration)return HitFeedbackResult.Expired;
            if(!playMedia)return HitFeedbackResult.AcceptedWithoutMedia;
            if(active.Count>=maximum){Suppressed++;return HitFeedbackResult.Suppressed;}
            var clock=new Clock{now=now,started=at};var scope=player.OpenScope("accepted hit",factory);
            try
            {
                var handle=scope.Play(definition,new CueOccurrence(execution,definition.Id),new CueAnchor(x,y,1),clock);
                if(handle.Status==CueStatus.Failed){LastError=handle.Error;scope.Dispose();return HitFeedbackResult.MissingBinding;}
                active.Add(new Active{scope=scope,handle=handle,clock=clock});Started++;return HitFeedbackResult.Started;
            }
            catch{scope.Dispose();throw;}
        }
        public void Sample(float now)
        {
            foreach(var value in active)value.clock.now=now;
            player.Tick();
            for(int i=active.Count-1;i>=0;i--)
            {
                var value=active[i];var status=value.handle.Status;
                if(status!=CueStatus.Completed&&status!=CueStatus.Cancelled&&status!=CueStatus.Failed)continue;
                if(value.handle.Error!=null)LastError=value.handle.Error;
                value.scope.Dispose();active.RemoveAt(i);
            }
        }
        public void ClearActive(){player.Reset();active.Clear();LastError=null;}
        public void Reset(){ClearActive();latest=0;}
        public void Dispose(){player.Dispose();active.Clear();}
    }
}
