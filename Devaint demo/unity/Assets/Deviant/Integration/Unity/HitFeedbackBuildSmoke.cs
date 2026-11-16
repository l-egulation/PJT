using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Integration
{
    // Inert in ordinary play. Only an explicitly launched local verification
    // player creates this diagnostic owner and supplies its report directory.
    public sealed class HitFeedbackBuildSmoke:MonoBehaviour
    {
        [Serializable]sealed class Result
        {
            public bool pass;
            public string unityVersion,scene,graphics;
            public long hitCues;
            public int debris;
            public int distortionWaves,distortionFrames;
            public bool playerDeathSlow;
            public List<string> errors=new List<string>();
        }
        Result result;
        string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--verify-feedback-build");
            if(index<0||index+1>=args.Length)return;
            var owner=new GameObject("Explicit feedback build verification").AddComponent<HitFeedbackBuildSmoke>();owner.folder=Path.GetFullPath(args[index+1]);
            DontDestroyOnLoad(owner.gameObject);
        }
        void Start()
        {
            result=new Result{unityVersion=Application.unityVersion,scene=SceneManager.GetActiveScene().path,graphics=SystemInfo.graphicsDeviceType.ToString()};
            Directory.CreateDirectory(folder);Application.logMessageReceived+=OnLog;StartCoroutine(Guarded());
        }
        void OnLog(string message,string stack,LogType type)
        {if((type==LogType.Error||type==LogType.Exception)&&result.errors.Count<16)result.errors.Add(message+"\n"+stack);}
        IEnumerator Guarded()
        {
            var run=Run();
            while(true)
            {
                object current=null;bool more;
                try{more=run.MoveNext();if(more)current=run.Current;}
                catch(Exception error){result.errors.Add(error.ToString());Finish();yield break;}
                if(!more)break;yield return current;
            }
            Finish();
        }
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        IEnumerator Run()
        {
            const string stage="Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity";
            if(SceneManager.GetActiveScene().path=="Assets/ForestCombatV2/Scenes/Chapter1Title.unity")
            {
                Require(Application.CanStreamedLevelBeLoaded(stage),"The current chapter stage is missing from the build.");
                yield return SceneManager.LoadSceneAsync(stage);
            }
            result.scene=SceneManager.GetActiveScene().path;
            CombatArena arena=null;double until=Time.realtimeSinceStartupAsDouble+30;
            while(!arena||!arena.Player)
            {
                Require(Time.realtimeSinceStartupAsDouble<until,"Build did not initialize a playable combat arena.");arena=FindAnyObjectByType<CombatArena>();yield return null;
            }
            yield return null;
            var bridge=arena.GetComponent<HitFeedbackBridge>();Require(bridge&&bridge.Ready,"Build did not load its feedback bindings.");
            Require(arena.Player.visual&&arena.Player.visual.sprite,"Build did not load approved character art.");
            Require(arena.enemies.Count>0,"Build has no playable encounter targets.");
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();var player=arena.Player;player.UseTestInput=true;
                // Explicit build verification supplies a diagnostic loadout;
                // ordinary title/blueprint interactions are never modified.
                player.SetWeaponSlotReady(0,true);player.SetWeaponSlotReady(1,true);
                foreach(var enemy in arena.enemies)enemy.aggressive=false;
                var target=arena.enemies[0];target.Position=player.Position+Vector2.right*1.8f;target.velocity=Vector2.zero;target.health=1;target.shield=0;target.guardUntil=0;
                player.SyncFacing(1);Physics2D.SyncTransforms();driver.Step(.01f,new CombatInput(0,CombatButtons.Basic));
                for(int i=0;i<300&&bridge.ActiveDebris==0;i++)
                {
                    driver.Step(.01f);arena.Presentation.Draw(0);arena.FeelFeedback?.TickPresentation(.01f);target.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);bridge.Sample();
                }
                result.hitCues=bridge.StartedHits;result.debris=bridge.ActiveDebris;
                Require(result.hitCues>0,"Packaged native attacks did not produce hit VFX.");
                Require(result.debris>0,"Packaged approved death did not produce its body fragments: "+bridge.LastError);
                result.distortionWaves=bridge.Distortion?.ActiveCount??0;
                Require(result.distortionWaves>0,"Packaged native attacks did not request distortion.");
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Capture(arena.ViewCamera);result.distortionFrames=bridge.Distortion.RenderedFrames;
                    Require(result.distortionFrames>0,"Packaged distortion did not render: "+bridge.Distortion.LastError);
                }
                player.health=0;player.Die();result.playerDeathSlow=Mathf.Abs(arena.Controls.Current.SimulationScale-.35f)<.00001f;
                Require(result.playerDeathSlow,"Packaged player-death slow policy did not apply.");arena.AdvanceFeedbackAccent(.2f);
                Require(Mathf.Abs(arena.Controls.Current.SimulationScale-1)<.00001f,"Packaged player-death slow did not release its time request.");
                driver.Reset();player.UseTestInput=false;
            }
            result.pass=result.errors.Count==0;
        }
        void Capture(Camera camera)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"player.png"),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
        }
        void Finish()
        {
            result.pass=result.pass&&result.errors.Count==0;File.WriteAllText(Path.Combine(folder,"build-smoke.json"),JsonUtility.ToJson(result,true));
            Debug.Log("HIT_FEEDBACK_BUILD_SMOKE "+(result.pass?"PASS":"FAIL"));Application.Quit(result.pass?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
