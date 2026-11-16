using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LocalTerrainPerformanceCheck
{
    const string Pending="LocalTerrainPerformanceCheck.Pending";
    static double readyAt;
    static double quietUntil;
    static LocalTerrainPerformanceCheck(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        EditorSceneManager.OpenScene(Chapter1MapSelector.Ver2Scene);
        SessionState.SetBool(Pending,true);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(!EditorApplication.isPlaying){
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){quietUntil=0;return;}
            if(quietUntil==0){quietUntil=EditorApplication.timeSinceStartup+8;return;}
            if(EditorApplication.timeSinceStartup<quietUntil)return;
            EditorApplication.EnterPlaymode();return;
        }
        var world=UnityEngine.Object.FindFirstObjectByType<ForestCombatWorld>();
        if(!world||!world.Ready)return;
        if(readyAt==0){readyAt=EditorApplication.timeSinceStartup;return;}
        if(EditorApplication.timeSinceStartup-readyAt<1)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var enemies=world.arena.enemies.OfType<FieldMouseEnemy>().ToArray();
            var truth=new System.Text.StringBuilder();
            Physics2D.SyncTransforms();
            // Include the complete scene, long paths, walls and repeated cliff probes.
            Action queries=()=>{foreach(var enemy in enemies)foreach(float dx in new[]{0f,.25f,-.25f,1f,-1f,8f,-8f,32f}){
                var goal=enemy.Position+Vector2.right*dx;
                truth.Append(enemy.FullSupport(enemy.Position,goal)?'1':'0');
                truth.Append(enemy.FlatPath(goal)?'1':'0');
            }};
            queries();string expected=truth.ToString();
            var indices=enemies.Select(e=>e.SurfaceIndex).ToArray();
            if(indices.Any(index=>index==null))throw new InvalidOperationException("Scene enemies have no terrain index.");
            foreach(var enemy in enemies)enemy.SurfaceIndex=null;
            truth.Clear();queries();
            if(truth.ToString()!=expected)throw new InvalidOperationException("Indexed and original path decisions differ.");
            for(int i=0;i<enemies.Length;i++)enemies[i].SurfaceIndex=indices[i];
            truth.Clear();long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            for(int i=0;i<12;i++)queries();timer.Stop();
            long queryAllocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            double queryMs=timer.Elapsed.TotalMilliseconds;
            world.arena.Player.UseTestInput=true;
            using(var manual=world.arena.AcquireManualSimulation())
            {
                allocated=GC.GetAllocatedBytesForCurrentThread();timer.Restart();
                for(int i=0;i<120;i++)manual.Step(1f/120);
                timer.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            }
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-terrainPerfLabel");
            string label=at>=0?args[at+1]:"current";
            string output=Path.GetFullPath("../outputs/terrain-performance");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,label+".json"),JsonUtility.ToJson(new Report{
                unity=Application.unityVersion,enemies=enemies.Length,surfaces=world.SurfaceCount,
                queries=12*enemies.Length*16,queryMs=queryMs,queryAllocatedBytes=queryAllocated,
                stepMs=timer.Elapsed.TotalMilliseconds,stepAllocatedBytes=allocated,results=expected},true));
            UnityEngine.Debug.Log("TERRAIN_PERF "+label+" queryMs="+queryMs+" stepMs="+timer.Elapsed.TotalMilliseconds);
            EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
        catch(Exception error){UnityEngine.Debug.LogException(error);EditorApplication.Exit(1);}
    }
    [Serializable] class Report{public string unity,results;public int enemies,surfaces,queries;public double queryMs,stepMs;public long queryAllocatedBytes,stepAllocatedBytes;}
}
