using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Deviant.Stage1Monsters
{
    public sealed class Stage1MonsterPreviewCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-stage1CaptureRoot");
            if(index<0 || index+1>=args.Length)yield break;
            string root=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(root);
            var showcase=GetComponent<Stage1MonsterShowcase>();
            showcase.previewCamera.transform.position=new Vector3(-.5f,4,-10);
            yield return null;
            var advanced=new bool[showcase.players.Length];
            for(int tick=0;tick<8;tick++)
            {
                yield return new WaitForSeconds(.05f);
                for(int i=0;i<advanced.Length;i++)advanced[i]|=showcase.players[i].Age>0;
            }
            foreach(bool didAdvance in advanced)
                if(!didAdvance)throw new InvalidOperationException("Runtime motion did not advance");
            showcase.automatic=false;showcase.paused=true;
            foreach(var player in showcase.players)player.paused=true;
            foreach(string state in new[]{"idle","walk","attack","hit","death"})
            {
                foreach(var player in showcase.players)
                {
                    if(!player.Play(state,true))player.Play("base",true);
                    player.SampleAt(player.Clip.DurationMs/1000f*(state=="death"?.28f:.52f));
                }
                yield return null;yield return null;
                string path=Path.Combine(root,state+".png");
                ScreenCapture.CaptureScreenshot(path);
                yield return null;yield return null;
                float deadline=Time.realtimeSinceStartup+10;
                while(!File.Exists(path) && Time.realtimeSinceStartup<deadline)yield return null;
                if(!File.Exists(path))throw new IOException("Native screenshot was not saved: "+path);
            }
            File.WriteAllText(Path.Combine(root,"play-mode-validation.json"),"{\"result\":\"PASS\",\"runtimeAdvanced\":true,\"capturedStates\":5,\"unity\":\""+Application.unityVersion+"\"}");
            Debug.Log("STAGE1_PLAYMODE_CAPTURE_PASS "+root);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }
    }
}
