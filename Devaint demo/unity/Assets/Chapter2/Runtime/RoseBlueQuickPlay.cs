using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Chapter2
{
    // Explicit local playtest launch; normal title/new-run flow is unchanged.
    public sealed class RoseBlueQuickPlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(!Environment.GetCommandLineArgs().Contains("--rose-blue-boss-test")||FindAnyObjectByType<RoseBlueQuickPlay>())return;
            var go=new GameObject("Rose Blue direct playtest");DontDestroyOnLoad(go);go.AddComponent<RoseBlueQuickPlay>();
        }
        IEnumerator Start()
        {
            SceneManager.LoadScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
            Chapter2World world=null;
            while(!(world=FindAnyObjectByType<Chapter2World>())||!world.Ready)yield return null;
            world.WarpToRoom("B");
        }
    }
}
