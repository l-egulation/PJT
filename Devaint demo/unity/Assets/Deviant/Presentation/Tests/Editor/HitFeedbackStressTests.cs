using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public sealed class HitFeedbackStressTests
    {
        [Serializable]sealed class Profile
        {
            public string unityVersion,environment;
            public int frames,hits,peakActive,peakParticleCapacity,poolInstances;
            public long managedBytes;
            public bool managedAllocationCounterAvailable;
            public double totalMilliseconds,medianStepMilliseconds,p95StepMilliseconds;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator SustainedBurstKeepsCueScopesParticlesAndPoolWithinSeparateBudgets()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Bounded feedback stress fixture");
            var settings=new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(Resources.Load<TextAsset>("HitFeedback/HitFeedback").text));
            var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");
            var profile=new Profile{unityVersion=Application.unityVersion,environment="Editor manual media sampling, no combat/logging/GPU frame-time claim",frames=600};
            var durations=new double[profile.frames];long sequence=0;var execution=Guid.NewGuid();
            using(var factory=new PrefabCueFactory(root.transform,bindings,settings.MaxInstances,settings.MaxParticles))using(var session=new HitFeedbackSession(settings.MaxHits))
            {
                var keys=bindings.prefabs.Select(b=>b.key).ToArray();
                long probeBefore=GC.GetAllocatedBytesForCurrentThread();var probe=new byte[2048];GC.KeepAlive(probe);
                profile.managedAllocationCounterAvailable=GC.GetAllocatedBytesForCurrentThread()-probeBefore>=2048;
                long allocation=GC.GetAllocatedBytesForCurrentThread();var overall=Stopwatch.StartNew();
                for(int frame=0;frame<profile.frames;frame++)
                {
                    long started=Stopwatch.GetTimestamp();float now=frame/120f;
                    for(int i=0;i<12;i++)
                    {
                        var key=keys[(frame+i)%keys.Length];var definition=factory.Definition(key);
                        session.Consume(++sequence,execution,i*.1f,0,now,now,definition,factory.Capture(key,0,settings.Scale(key),unchecked((uint)sequence)));
                    }
                    session.Sample(now);profile.peakActive=Math.Max(profile.peakActive,session.ActiveCount);profile.peakParticleCapacity=Math.Max(profile.peakParticleCapacity,factory.ParticleCapacityInUse);
                    Assert.That(session.ActiveCount,Is.LessThanOrEqualTo(settings.MaxHits));Assert.That(factory.ParticleCapacityInUse,Is.LessThanOrEqualTo(settings.MaxParticles));
                    Assert.That(root.transform.childCount,Is.LessThanOrEqualTo(keys.Length*settings.MaxInstances));
                    durations[frame]=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
                }
                overall.Stop();profile.totalMilliseconds=overall.Elapsed.TotalMilliseconds;profile.managedBytes=profile.managedAllocationCounterAvailable?GC.GetAllocatedBytesForCurrentThread()-allocation:-1;
                profile.hits=(int)sequence;profile.poolInstances=root.transform.childCount;Array.Sort(durations);profile.medianStepMilliseconds=durations[durations.Length/2];profile.p95StepMilliseconds=durations[(int)(durations.Length*.95)];
                session.Sample(profile.frames/120f+bindings.prefabs.Max(b=>b.duration)+1);
                Assert.That(session.ActiveCount,Is.Zero);Assert.That(factory.ActiveCount,Is.Zero);Assert.That(factory.ParticleCapacityInUse,Is.Zero);
            }
            yield return null;Assert.That(root.transform.childCount,Is.Zero,"Factory disposal returns all native instances, not only active handles.");Object.Destroy(root);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../profiling"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"hit-feedback-editor-sampling.json"),JsonUtility.ToJson(profile,true));
        }
    }
}
