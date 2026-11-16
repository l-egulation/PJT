using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Chapter2
{
    // Opt-in development-player check; normal launches keep the existing title flow.
    public sealed class Chapter2PlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Application.isEditor||!Debug.isDebugBuild||!Environment.GetCommandLineArgs().Contains("-chapter2-smoke"))return;
            var go=new GameObject("Chapter2 development smoke");DontDestroyOnLoad(go);go.AddComponent<Chapter2PlayerSmoke>();
        }
        IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync(Chapter2EntryBridge.ScenePath);float deadline=Time.realtimeSinceStartup+45;Chapter2World world=null;
            do{yield return null;world=FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            if(!world||!world.Ready){Debug.LogError("CHAPTER2_PLAYER_SMOKE_FAILED startup");Application.Quit(2);yield break;}
            world.WarpToRoom("M");bool shop=world.TryOpenMaintenance();world.CloseMaintenance();
            bool valid=C2Bank.Validate(world.Layout).Count==0&&world.Layout.rooms.Length==16&&world.Actors.Count>120&&shop;
            Debug.Log((valid?"CHAPTER2_PLAYER_SMOKE_PASS ":"CHAPTER2_PLAYER_SMOKE_FAILED ")+world.Layout.id+" variant="+world.Layout.variant+" rooms="+world.Layout.rooms.Length+" actors="+world.Actors.Count);
            for(int i=0;i<10;i++)yield return null;Application.Quit(valid?0:3);
        }
    }
}
