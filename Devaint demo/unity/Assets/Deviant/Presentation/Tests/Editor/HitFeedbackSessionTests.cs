using System;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class HitFeedbackSessionTests
    {
        [Test]public void DeathTimeSourceOnlyAcceptsTheExplicitPlayerDeathPolicy()
        {
            var data=new HitFeedbackTimeData{schemaVersion=2,trigger="player-death",scale=.35f,accentSeconds=.15f};
            Assert.DoesNotThrow(()=>data.Validate());
            data.trigger="monster-death";Assert.Throws<ArgumentException>(()=>data.Validate());
            data.trigger="player-death";data.schemaVersion=1;Assert.Throws<ArgumentException>(()=>data.Validate());
            data.schemaVersion=2;data.scale=float.NaN;Assert.Throws<ArgumentException>(()=>data.Validate());
        }
        sealed class Factory:ICueViewFactory
        {
            public int opened,closed;
            sealed class View:ICueView
            {readonly Factory owner;bool closed;public View(Factory owner){this.owner=owner;}public void Sample(float t){}public void Dispose(){if(closed)return;closed=true;owner.closed++;}}
            public IDisposable Begin(ICueDefinition definition,CueAnchor anchor,Action<CueViewResult> result)
            {opened++;result(CueViewResult.Ready(new View(this)));return null;}
        }
        [Test]public void SameExecutionCanHitMultipleTargetsWithoutRetainingCompletedHistory()
        {
            var definition=new PrefabCueDefinition("hit","prefab",.1f,"v1");var factory=new Factory();var execution=Guid.NewGuid();
            using(var session=new HitFeedbackSession(2))
            {
                Assert.That(session.Consume(1,execution,1,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Started));
                Assert.That(session.Consume(2,execution,2,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Started));
                Assert.That(session.Consume(2,execution,2,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Duplicate));
                Assert.That(session.Consume(3,execution,3,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Suppressed));
                session.Sample(.2f);Assert.That(session.ActiveCount,Is.Zero);Assert.That(factory.closed,Is.EqualTo(2));
                Assert.That(session.Consume(2,execution,2,2,0,.2f,definition,factory),Is.EqualTo(HitFeedbackResult.Duplicate));
                Assert.That(session.Consume(4,execution,2,2,0,.2f,definition,factory),Is.EqualTo(HitFeedbackResult.Expired));
            }
        }
        [Test]public void DisablingMediaPreservesAdmissionAndNeverReplaysOldEvents()
        {
            var definition=new PrefabCueDefinition("hit","prefab",.1f,"v1");var factory=new Factory();var execution=Guid.NewGuid();
            using(var session=new HitFeedbackSession(1))
            {
                Assert.That(session.Consume(1,execution,1,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Started));
                session.ClearActive();Assert.That(factory.closed,Is.EqualTo(1));Assert.That(session.ActiveCount,Is.Zero);
                Assert.That(session.Consume(1,execution,1,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Duplicate));
                Assert.That(session.Consume(2,execution,1,2,0,0,definition,factory,false),Is.EqualTo(HitFeedbackResult.AcceptedWithoutMedia));
                Assert.That(factory.opened,Is.EqualTo(1));Assert.That(session.Started,Is.EqualTo(1));
                Assert.That(session.Consume(2,execution,1,2,0,0,definition,factory),Is.EqualTo(HitFeedbackResult.Duplicate));
                Assert.That(session.Consume(3,execution,1,2,0,.2f,definition,factory,false),Is.EqualTo(HitFeedbackResult.Expired));
                Assert.That(session.Consume(4,execution,1,2,.2f,.2f,definition,factory),Is.EqualTo(HitFeedbackResult.Started));
                Assert.That(factory.opened,Is.EqualTo(2));
            }
        }
        [Test]public void SettingsAreValidatedAndSourceMutationDoesNotChangePublishedCatalog()
        {
            var data=UnityEngine.JsonUtility.FromJson<HitFeedbackData>(UnityEngine.Resources.Load<UnityEngine.TextAsset>("HitFeedback/HitFeedback").text);
            var settings=new HitFeedbackSettings(data);float scale=settings.Scale("hit_slash");data.recipes[0].scale=123;data.forms[0].recipe="hit_blunt";
            Assert.That(settings.Scale("hit_slash"),Is.EqualTo(scale));Assert.That(settings.Recipe("base_sword"),Is.EqualTo("hit_slash"));
            float maximum=settings.WaveMaxPixels;data.waveMaxPixels=123;
            Assert.That(settings.WaveMaxPixels,Is.EqualTo(maximum));
            foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
            {data.waveMaxPixels=invalid;Assert.Throws<ArgumentException>(()=>new HitFeedbackSettings(data));}
            data.waveMaxPixels=maximum;
            data.recipes[0].scale=float.NaN;Assert.Throws<ArgumentException>(()=>new HitFeedbackSettings(data));
        }
        [Test]public void PaperStyleValidatesHexColorsAndCapturesSettings()
        {
            var data=UnityEngine.JsonUtility.FromJson<HitFeedbackData>(UnityEngine.Resources.Load<UnityEngine.TextAsset>("HitFeedback/HitFeedback").text);
            data.paperOutlineColor="#A03F12";
            var settings=new HitFeedbackSettings(data);
            data.paperOutlineColor="#123456";
            Assert.That(settings.PaperOutlineColor,Is.EqualTo("#A03F12"));
            foreach(string invalid in new[]{null,"","red","123456","#12345","#12345678","#GG0000","# 12345"})
            {data.paperOutlineColor=invalid;Assert.Throws<ArgumentException>(()=>new HitFeedbackSettings(data),invalid);}
            data.paperOutlineColor="#00ff80";Assert.DoesNotThrow(()=>new HitFeedbackSettings(data));
        }
        [Test]public void AllHitDistortionIsIndependentOfStrongCameraQualificationAndCopiesItsSource()
        {
            var data=UnityEngine.JsonUtility.FromJson<HitFeedbackData>(UnityEngine.Resources.Load<UnityEngine.TextAsset>("HitFeedback/HitFeedback").text);
            data.distortAllHits=false;data.strongActions=new[]{"fixture-strong"};
            var qualified=new HitFeedbackSettings(data);
            Assert.That(qualified.Distorts("fixture-normal"),Is.False);Assert.That(qualified.Distorts(null),Is.False);
            Assert.That(qualified.Distorts("fixture-strong"),Is.True);
            data.distortAllHits=true;var all=new HitFeedbackSettings(data);
            Assert.That(all.Distorts("fixture-normal"),Is.True);Assert.That(all.Distorts("fixture-special"),Is.True);
            Assert.That(all.Strong("fixture-normal"),Is.False);Assert.That(all.Strong("fixture-strong"),Is.True);
            Assert.That(all.CameraPixels(false),Is.EqualTo(qualified.CameraPixels(false)));
            Assert.That(all.CameraSeconds(false),Is.EqualTo(qualified.CameraSeconds(false)));
            data.distortAllHits=false;Assert.That(all.Distorts("fixture-normal"),Is.True,"Published settings must not follow mutable authoring data.");
        }
    }
}
