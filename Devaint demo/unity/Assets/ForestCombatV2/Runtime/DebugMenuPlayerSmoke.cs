using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestCombatV2
{
    // Explicit local player verification only, following the project's existing build-smoke pattern.
    public sealed class DebugMenuPlayerSmoke : MonoBehaviour
    {
        [Serializable] sealed class Result { public bool pass, debugIncluded; public int seed; public string scene, detail; }
        string output;
        bool layout;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-debugMenuLayoutSmoke");bool layout=i>=0;
            if(!layout)i=Array.IndexOf(args,"-debugMenuBossSmoke");
            if(i<0||i+1>=args.Length)return;
            var go=new GameObject("Explicit console player verification");DontDestroyOnLoad(go);
            var smoke=go.AddComponent<DebugMenuPlayerSmoke>();smoke.output=args[i+1];smoke.layout=layout;
        }
        IEnumerator Start()
        {
            yield return null;
            yield return SceneManager.LoadSceneAsync(Chapter1MapSelector.ProceduralScene);
            ForestCombatWorld world=null;
            float deadline=Time.realtimeSinceStartup+90;
            do { yield return null;world=FindFirstObjectByType<ForestCombatWorld>(); }
            while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            if(!world||!world.Ready){Finish(null,false,"Stage did not become ready");yield break;}
            foreach(var selector in FindObjectsByType<Chapter1MapSelector>())Destroy(selector.gameObject);
            yield return null;
            if(layout){yield return CaptureLayouts(world);yield break;}
            try { Verify(world);Finish(world,true,"Console boss command moved inside the room without a legacy selector; run, loadout, health and enemies preserved."); }
            catch(Exception e){Finish(world,false,e.ToString());}
        }
        IEnumerator CaptureLayouts(ForestCombatWorld world)
        {
            string error=null;DebugGameMenu menu=null;
            string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),"dense-captures");
            try
            {
                menu=world.GetComponent<DebugGameMenu>();Need(menu&&menu.Open(),"Console did not open");
                PrepareLoadout(world,menu);
                if(world.arena.Sketch!=null)
                {
                    var ink=world.arena.Sketch.Catalog.inks[0];menu.SelectWeapon(0);menu.SelectInkSlot(0);
                    Need(menu.TryApplyPaletteColor(ink.color,out var colorError),colorError);
                    Need(menu.TryApplyRunInk(ink.id,out var inkError),inkError);
                }
                else
                {
                    var growth=world.GetComponent<Chapter1RunBinding>().Run.Growth;
                    var color=growth.Equipped(world.arena.Player.weapons[0].InstanceId).Slots[0].Color;
                    Need(menu.TryApplyInkPart(growth.Data.parts.First(p=>color=="colorless"||p.color==color).id,3,out var reason),reason);
                }
                Directory.CreateDirectory(folder);
            }
            catch(Exception e){error=e.ToString();}
            if(error!=null){Finish(world,false,error);yield break;}
            string[] names={"root","equipment","choices","ink-details","currency","unlocks","travel","monsters"};
            for(int page=0;page<names.Length;page++)
            {
                try
                {
                    switch(page)
                    {
                        case 1:menu.SelectCategory(2);break;
                        case 2:SelectCaptureItem(menu,"무기 형태:");Need(menu.ExecuteSelected(),"Choice list did not open");break;
                        case 3:menu.Back();menu.SelectCategory(2);menu.MoveSelection(-1);break;
                        case 4:menu.SelectCategory(1);break;
                        case 5:menu.SelectCategory(3);break;
                        case 6:menu.SelectCategory(0);break;
                        case 7:menu.SelectCategory(4);break;
                    }
                }
                catch(Exception e){error=e.ToString();}
                if(error!=null){Finish(world,false,error);yield break;}
                yield return null;yield return new WaitForEndOfFrame();
                Texture2D capture=null;
                try { capture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,names[page]+".png"),capture.EncodeToPNG()); }
                catch(Exception e){error=e.ToString();}
                finally { if(capture)Destroy(capture); }
                if(error!=null){Finish(world,false,error);yield break;}
            }
            Finish(world,true,"Captured console layouts in "+folder);
        }
        static void SelectCaptureItem(DebugGameMenu menu,string prefix)
        {
            for(int i=0;i<256;i++){if(menu.SelectedLabel!=null&&menu.SelectedLabel.StartsWith(prefix))return;menu.MoveSelection(1);}
            throw new InvalidOperationException("Menu item missing: "+prefix);
        }
        static void PrepareLoadout(ForestCombatWorld world,DebugGameMenu menu)
        {
            if(world.arena.Sketch==null)
            {
                var offers=world.GetComponent<Chapter1StartingBlueprints>();
                Need(offers&&offers.ApplyPreselected(new[]{"sword","blunt"}),"Starting loadout unavailable");
                return;
            }
            int sword=Array.FindIndex(world.arena.catalog.forms,form=>form.id=="base_sword");
            int hammer=Array.FindIndex(world.arena.catalog.forms,form=>form.id=="hammer");
            Need(sword>=0&&hammer>=0,"Console weapon forms missing");
            Need(menu.TryApplyEquipment(0,sword,0,out var firstError),firstError);
            Need(menu.TryApplyEquipment(1,hammer,0,out var secondError),secondError);
        }
        static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static void Verify(ForestCombatWorld world)
        {
            Need(!Application.isEditor&&DevFeatures.IncludedInPlayer&&DevFeatures.Enabled,"This must be a player with the console included");
            Need(!FindFirstObjectByType<Chapter1MapSelector>(),"Legacy selector must be absent");
            var menu=world.GetComponent<DebugGameMenu>();Need(menu&&menu.Open(),"Console did not open");
            PrepareLoadout(world,menu);
            var player=world.arena.Player;var run=world.GetComponent<Chapter1RunBinding>().Run;
            int seed=world.Data.generationSeed,count=world.arena.enemies.Count;
            float health=player.health;var identities=player.weapons.Select(w=>w.InstanceId).ToArray();
            var selections=player.weapons.Select(w=>w.FormIndex+":"+w.SpecialIndex).ToArray();
            var boss=world.GetComponent<ClockExecutionerEncounter>();Need(boss&&boss.Boss,"Boss missing");float bossHealth=boss.Boss.health;
            menu.SelectCategory(0);
            SelectCaptureItem(menu,"현재 런 보스방으로 이동");
            Need(menu.SelectedLabel=="현재 런 보스방으로 이동","Boss button is not selectable: "+menu.SelectedLabel);
            using(var paused=world.arena.Controls.OpenScope("player smoke paused background UI"))
            {
                paused.PauseSimulation();paused.BlockInput(Deviant.Control.Contracts.InputChannels.Combat|Deviant.Control.Contracts.InputChannels.Interaction);
                Need(menu.ExecuteSelected(),"Boss button did not dispatch");
            var room=world.Data.rooms[Chapter1DebugTravel.BossIndex(world)];
            Need(Vector2.Distance(player.Position,new Vector2(room.x+3,-(room.y+room.entryY)+.02f))<.03f,"No boss-room arrival: "+menu.Status+" position="+player.Position);
            Need(world.GetComponent<Chapter1BossEncounter>().PlayerInBossRoom,"Outside boss room");
            Need(world.GetComponent<Chapter1RunBinding>().Run==run&&world.Data.generationSeed==seed,"Run/seed changed");
            Need(player.weapons.Select(w=>w.InstanceId).SequenceEqual(identities)&&player.weapons.Select(w=>w.FormIndex+":"+w.SpecialIndex).SequenceEqual(selections),"Loadout changed");
            Need(player.health==health&&boss.Boss.health==bossHealth&&world.arena.enemies.Count==count,"Health or enemy state changed");
                Need(world.arena.Controls.Current.SimulationPaused,"Console/background pause was lost");
            }
            menu.Close();
        }
        void Finish(ForestCombatWorld world,bool pass,string detail)
        {
            var path=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,JsonUtility.ToJson(new Result{pass=pass,debugIncluded=DevFeatures.IncludedInPlayer,seed=world?world.Data.generationSeed:0,scene=SceneManager.GetActiveScene().path,detail=detail},true));
            Debug.Log("DEBUG_MENU_PLAYER_SMOKE "+(pass?"PASS ":"FAIL ")+detail);Application.Quit(pass?0:1);
        }
    }
}
