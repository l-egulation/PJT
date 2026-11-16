using System;
using Deviant.Combat;
using Deviant.Game.Diagnostics;
using Deviant.Integration;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;

namespace Deviant.CombatLab
{
    public sealed class LabComposition : MonoBehaviour
    {
        public CombatArena arena;
        public CombatDiagnostics diagnostics;
        public Sprite idleSprite;
        public Material spriteMaterial;
        Camera view;
        CameraOutputBinding cameraOutput;
        CameraOutputBinding.BaseSource cameraFollow;
        void Start()
        {
            try
            {
                if (!arena || !diagnostics) throw new InvalidOperationException("LabComposition requires its arena and technical diagnostics.");
                var content = CombatContentLoader.Load(arena.characterAnimations, arena.loadCharacterArt);
                view = new GameObject("CombatV2Camera").AddComponent<Camera>(); view.tag = "MainCamera";
                view.orthographic = true; view.orthographicSize = 6; view.backgroundColor = Color.white; view.clearFlags = CameraClearFlags.SolidColor;
                content.Profile = CombatCompositionProfile.Lab;
                content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
                content.Camera = view; content.SpriteMaterial = spriteMaterial; content.PlayerSprite = idleSprite;
                content.PlayerSpawn = new Vector2(-4, 0); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
                content.PlayerSortingOrder = 10;
                int bomb = content.Catalog?.forms == null ? -1 : Array.FindIndex(content.Catalog.forms, form => form.id == "bomb");
                content.SecondaryFormIndex = bomb >= 0 ? bomb : 1;
                content.Enemies = new[] {
                    new CombatEnemySpawn("MeleeTester", 0, new Vector2(3, 0), new Vector2(2.65f, 1.9f), new Vector2(.18f, .95f), 100, false, false, 10),
                    new CombatEnemySpawn("RangedTester", 1, new Vector2(9, 0), new Vector2(1.3f, 1.2f), new Vector2(.45f, 1.25f), 60, false, false, 10),
                    new CombatEnemySpawn("BossDummy", 2, new Vector2(31, 0), new Vector2(5.3f, 3.8f), new Vector2(.36f, 1.9f), 1000, true, false, 10)
                };
                content.PrepareEnvironment = CreateEnvironment;
                content.BindPlayerInput = player => player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
                if (!arena.Initialize(content)) { enabled = false; return; }
                diagnostics.arena = arena;
                cameraOutput = CameraOutputBinding.GetOrCreate(view);
                cameraFollow = cameraOutput.BindBaseSource("combat arena follow");
                arena.Record("V2 sandbox: A/S basic 1/2 / Q/W special 1/2 / arrow keys / Space / Shift / F5 reset");
                BeginCommandLineCapture();
            }
            catch (InvalidOperationException error)
            {
                Debug.LogError(error.Message);
                if (arena) arena.enabled = false;
                enabled = false;
            }
        }
        void CreateEnvironment(CombatArena combat)
        {
            Surface("Ground", new Vector2(0, -.5f), new Vector2(40, 1), false);
            Surface("FarGround", new Vector2(35, -.5f), new Vector2(22, 1), false);
            Surface("Wall", new Vector2(13, 1.5f), new Vector2(.4f, 3), false);
            Surface("OneWay", new Vector2(-8, 2.7f), new Vector2(5, .25f), true);
            Surface("HighPlatform", new Vector2(-13, 4), new Vector2(3, .4f), false);
            Surface("LeftBoundary", new Vector2(-20.5f, 5), new Vector2(1, 12), false);
            Surface("RightBoundary", new Vector2(46.5f, 5), new Vector2(1, 12), false);
            var pit = new GameObject("ExplicitPitRecoveryVolume"); pit.transform.position = new Vector3(13, -6, 0);
            var collider = pit.AddComponent<BoxCollider2D>(); collider.size = new Vector2(70, 3); collider.isTrigger = true;
            pit.AddComponent<ArenaPit>(); combat.pits.Add(collider);
        }
        void Surface(string label, Vector2 position, Vector2 dimensions, bool oneWay)
        {
            var root = new GameObject(label); root.transform.position = position;
            root.AddComponent<ArenaSurface>().oneWay = oneWay;
            var collider=root.AddComponent<BoxCollider2D>();collider.size = dimensions;arena.RegisterFeedbackSurface(collider);
            var child = new GameObject("Visual"); child.transform.SetParent(root.transform, false); child.transform.localScale = dimensions;
            var visual = child.AddComponent<SpriteRenderer>(); visual.sprite = arena.TechnicalSprite; visual.sortingOrder = 10;
            visual.color = oneWay ? new Color(.3f, .6f, .7f) : new Color(.25f, .3f, .4f);
            if (spriteMaterial) visual.sharedMaterial = spriteMaterial;
        }
        void LateUpdate() => Follow();
        public void Follow()
        {
            if (!arena || !arena.Player || !view || cameraFollow == null) return;
            float half = view.orthographicSize * view.aspect;
            cameraFollow.Submit(new CameraPosition(Mathf.Clamp(arena.Player.Position.x + 2, -20 + half, 46 - half), Mathf.Max(4, arena.Player.Position.y + 2), -10));
            cameraOutput.ApplyNow();
        }
        void OnDestroy() { cameraFollow?.Dispose(); }
        void BeginCommandLineCapture()
        {
            string[] args=System.Environment.GetCommandLineArgs();
            int modeArgument=System.Array.IndexOf(args,"-combatV2LauncherMode"),launcherMode=-1;
            if(modeArgument>=0 && modeArgument+1<args.Length) int.TryParse(args[modeArgument+1],out launcherMode);
            int formArgument=System.Array.IndexOf(args,"-combatV2Form"),hitArgument=System.Array.IndexOf(args,"-combatV2Hit"),facingArgument=System.Array.IndexOf(args,"-combatV2Facing");
            string captureForm=formArgument>=0 && formArgument+1<args.Length ? args[formArgument+1] : null;
            int captureHit=1,captureFacing=1;
            if(hitArgument>=0 && hitArgument+1<args.Length) int.TryParse(args[hitArgument+1],out captureHit);
            if(facingArgument>=0 && facingArgument+1<args.Length) int.TryParse(args[facingArgument+1],out captureFacing);
            int feedbackArgument=System.Array.IndexOf(args,"-combatV2Feedback");
            for(int i=0;i<args.Length-1;i++) if(args[i]=="-combatV2Capture")
            {
                if(feedbackArgument>=0 && feedbackArgument+1<args.Length) StartCoroutine(CaptureFeedback(args[i+1],args[feedbackArgument+1]));
                else StartCoroutine(CapturePreview(args[i+1],System.Array.IndexOf(args,"-combatV2Whip")>=0,System.Array.IndexOf(args,"-combatV2Bomb")>=0,launcherMode,System.Array.IndexOf(args,"-combatV2Equipment")>=0,captureForm,captureHit,captureFacing));
            }
        }
        System.Collections.IEnumerator FeedbackAdvance(float seconds)
        {
            float until=arena.Now+seconds;
            while(arena.Now<until) yield return new WaitForFixedUpdate();
        }
        System.Collections.IEnumerator CaptureFeedback(string path,string scenario)
        {
            arena.Player.UseTestInput=true; arena.Player.selected=0; diagnostics.showPanel=false; arena.showBoxes=false;
            string form=scenario=="shotgun" ? "launcher" : scenario=="sword" ? "base_sword" : scenario;
            int mode=scenario=="sword" ? 1 : 2;
            arena.Player.Reconfigure(0,System.Array.FindIndex(arena.catalog.forms,f=>f.id==form),mode);
            arena.enemies[0].Position=arena.Player.Position+Vector2.right*(scenario=="sword" ? 4 : 5); Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            if(scenario=="shotgun")
            {
                arena.Player.QueueTestInput(jump:true); yield return FeedbackAdvance(.08f);
                arena.Player.QueueTestInput(basic:true); yield return FeedbackAdvance(.26f);
            }
            else
            {
                arena.Player.QueueTestInput(special:true);
                yield return FeedbackAdvance(scenario=="sword" ? .32f : scenario=="rapier" ? .12f : .2f);
                if(scenario=="whip") { arena.Player.QueueTestInput(special:true); yield return FeedbackAdvance(.06f); }
            }
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1); Application.Quit();
        }
        System.Collections.IEnumerator CapturePreview(string path,bool whip,bool bomb,int launcherMode=-1,bool equipment=false,string form=null,int hit=1,int facing=1)
        {
            arena.Player.UseTestInput=true; arena.Player.selected=launcherMode>=0 ? 0 : whip || bomb ? 1 : 0; diagnostics.showPanel=equipment; arena.showBoxes=false;
            if(whip) arena.Player.Reconfigure(1,1,0);
            if(launcherMode>=0) arena.Player.Reconfigure(0,System.Array.FindIndex(arena.catalog.forms,f=>f.id=="launcher"),Mathf.Clamp(launcherMode,0,3));
            if(form!=null)
            {
                int index=System.Array.FindIndex(arena.catalog.forms,f=>f.id==form);
                if(index>=0) { arena.Player.selected=0; arena.Player.Reconfigure(0,index,0); }
                arena.Player.SyncFacing(facing<0 ? -1 : 1);
                arena.enemies[0].Position=arena.Player.Position+Vector2.right*arena.Player.facing*1.8f; Physics2D.SyncTransforms();
            }
            if(bomb || launcherMode==3) { arena.enemies[0].Position=arena.Player.Position+Vector2.right*2.5f; Physics2D.SyncTransforms(); }
            yield return new WaitForSecondsRealtime(.5f);
            arena.Player.weapons[1-arena.Player.selected].Cooldown.Spend(arena.Now);
            arena.Player.axis=bomb || launcherMode>=0 || equipment || form!=null ? 0 : 1;
            if(!equipment) arena.Player.QueueTestInput(basic:true);
            if(form!=null && !equipment)
            {
                hit=Mathf.Clamp(hit,1,arena.Player.Form.basics.Length);
                yield return new WaitForFixedUpdate();
                for(int next=1;next<hit;next++)
                {
                    arena.Player.QueueTestInput(basic:true);
                    float timeout=arena.Now+3;
                    while(arena.Player.BasicIndex<next && arena.Now<timeout) yield return new WaitForFixedUpdate();
                }
                float frameTimeout=arena.Now+3;
                while(arena.Player.currentCast && arena.Player.currentCast.Age<arena.Player.currentCast.ImpactTime+.02f && arena.Now<frameTimeout) yield return new WaitForFixedUpdate();
            }
            float until=arena.Now+(launcherMode==1 || launcherMode==2 ? .20f : bomb || launcherMode==3 ? .29f : .24f);
            if(form==null || equipment) while(arena.Now<until) yield return new WaitForFixedUpdate();
            arena.Player.axis=0;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1);
            Application.Quit();
        }
    }
}
