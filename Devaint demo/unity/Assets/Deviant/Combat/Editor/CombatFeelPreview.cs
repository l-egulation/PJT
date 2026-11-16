using System;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Presentation.Unity;
using Deviant.Stage1Monsters.Game;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Editor
{
    [InitializeOnLoad]
    public static class CombatFeelPreview
    {
        const string Request = "CombatFeelPreview.Request";
        const string Scene = "Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity";
        static double deadline, quietUntil;
        static bool verify;
        static int phase;
        static CombatArena.ManualSimulation driver;
        static CombatArena arena;
        static TestEnemy target;
        static ForestCombatWorld world;
        static bool prepared, profiling;
        static float firstAngle;
        static int secondHitStart;
        static string Output => Path.GetFullPath("../outputs/combat-feel");
        static CombatFeelPreview() { EditorApplication.update += Tick; }
        [MenuItem("Tools/deviant/Combat Feel/Open Test")]
        public static void Open() => Queue("play");
        public static void OpenGame() => Queue("game");
        public static void Verify() => Queue("verify");
        public static void VerifyFeatures() => Queue("verify-features");
        public static void Measure() => Queue("verify-measure");
        // Monster hit picture: three ordinary basic hits then one forced critical hit, compare-<n>-a/b.png.
        public static void VerifyCompare() => Queue("verify-compare");
        // F12 wind-up hold: thorn-root and broom-stalker stroke timing at each PrepareHoldCompare value, hold.json + hold-*.png.
        public static void VerifyHold() => Queue("verify-hold");
        // Real-time editor play frame cost by phase (graphics editor, not batchmode): outputs/combat-feel/profile.json.
        public static void Profile() => Queue("verify-profile");
        static void Queue(string mode)
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            SessionState.SetString(Request, mode); Directory.CreateDirectory(Output); quietUntil = 0;
        }
        static void Tick()
        {
            var request = SessionState.GetString(Request, "");
            if (request.Length == 0) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quietUntil = 0; return; }
                    if (quietUntil == 0) { quietUntil = EditorApplication.timeSinceStartup + 3; return; }
                    if (EditorApplication.timeSinceStartup < quietUntil) return;
                    verify = request.StartsWith("verify"); phase = 0; prepared = false; target = null; profiling = false;
                    EditorSceneManager.OpenScene(Scene);
                    deadline = EditorApplication.timeSinceStartup + 120;
                    EditorApplication.EnterPlaymode(); return;
                }
                verify = request.StartsWith("verify");
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
                if (EditorApplication.timeSinceStartup > deadline && deadline != 0) throw new Exception("Combat feel scene timed out.");
                arena = Object.FindAnyObjectByType<CombatArena>();
                if (!arena || !arena.Player || !arena.FeelFeedback) return;
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
                var art = world ? world.GetComponent<VillagePresentation>() : null;
                if (!world || !world.Ready || !art || !art.Ready) return;
                if (!world.GetComponent<Stage1VillageMonsterContent>() || !arena.enemies.OfType<FieldMouseEnemy>().Any(e => e.Definition.id == "01_thorn-root"))
                    throw new Exception("The current procedural village and approved stage-one monsters are required.");
                if (request == "game") {
                    SessionState.SetString(Request, ""); EditorApplication.ExecuteMenuItem("Window/General/Game");
                    Debug.Log("CURRENT_GAME_READY Chapter1Procedural / approved stage-one monsters"); return;
                }
                if (!verify)
                {
                    PrepareTarget();
                    SessionState.SetString(Request, "");
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    Debug.Log("COMBAT_FEEL_READY Chapter1Procedural / thorn-root / F6=toggle F7=strength F8=hit-stop S=attack");
                    return;
                }
                if (request == "verify-measure") { MeasureFeel(); Finish(true); return; }
                if (request == "verify-compare") { CompareStep(); return; }
                if (request == "verify-hold") { HoldStep(); return; }
                if (request == "verify-profile")
                {
                    if (profiling) return;
                    profiling = true; PrepareTarget(); EditorApplication.ExecuteMenuItem("Window/General/Game"); deadline = EditorApplication.timeSinceStartup + 300;
                    var probe = new CombatFeelProfile();
                    probe.arena = arena; probe.prepare = PrepareTarget; probe.output = Path.Combine(Output, "profile.json");
                    probe.done = ok => { Finish(ok); if (!Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1); }; probe.Start();
                    return;
                }
                if (phase == 0)
                {
                    PrepareTarget();
                    driver = arena.AcquireManualSimulation();
                    var player = arena.Player; player.selected = 0;
                    arena.FeelFeedback.ResetFeedback();
                    // Batch camera captures can take longer than a particle's lifetime.
                    // Advance its visual clock explicitly alongside the manual arena.
                    var captureParticles = ImpactParticles();
                    var captureMain = captureParticles.main; captureMain.simulationSpeed = 0;
                    arena.Presentation.Draw(0);
                    Capture("before-hit"); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); phase = 1; return;
                }
                if (phase == 1)
                {
                    driver.Step(.02f);
                    arena.FeelFeedback.TickPresentation(0);
                    if (arena.FeelFeedback.PresentedHits <= 0) return;
                    if (arena.FeelFeedback.HitStopped) throw new Exception("The base sword basic hit keeps zero hit-stop.");
                    arena.Presentation.Draw(0);
                    CameraOutputBinding.GetOrCreate(arena.ViewCamera).ApplyNow();
                    firstAngle = arena.FeelFeedback.LastImpactAngle;
                    Capture("blade-contact");
                    AdvanceToReadableImpact(); Capture("impact");
                    if (arena.FeelFeedback.PresentedHits <= 0 || target.health >= 1000)
                        throw new Exception("No real attack hit reached the feedback presenter.");
                    if (arena.FeelFeedback.UsePixelHits)
                    {
                        // Pixel burst frames 1~3 and the sparks/shards in flight.
                        if (arena.FeelFeedback.PixelFx.BurstCount == 0) throw new Exception("The real hit did not create a pixel burst.");
                        for (int f = 1; f <= 3; f++) { driver.Step(.02f); arena.FeelFeedback.TickPresentation(.02f); target.GetComponent<Stage1VillageMonsterPresentation>().Draw(); Capture("pixel-impact-" + f); }
                        if (arena.FeelFeedback.PixelFx.LastDrawnPixels < 40) throw new Exception("Too few pixel effect pixels were drawn: " + arena.FeelFeedback.PixelFx.LastDrawnPixels);
                    }
                    else if (arena.FeelFeedback.ShowsLegacyImpact)
                    {
                        for (int i = 0; i < 9; i++) driver.Step(.02f);
                        arena.FeelFeedback.TickPresentation(.18f);
                        target.GetComponent<Stage1VillageMonsterPresentation>().Draw(); arena.Presentation.Draw(0);
                        var impactParticles = ImpactParticles();
                        if (impactParticles.particleCount != 9) throw new Exception("Expected one nine-particle burst before its flight, found " + impactParticles.particleCount);
                        var particleMain = impactParticles.main; particleMain.simulationSpeed = 1;
                        impactParticles.Simulate(.18f, false, false, true);
                        particleMain.simulationSpeed = 0;
                        arena.FeelFeedback.TickPresentation(0);
                        Capture("particles");
                        if (impactParticles.particleCount < 4) throw new Exception("Too few visible white impact particles survived their initial flight.");
                    }
                    phase = 2; return;
                }
                if (phase == 2)
                {
                    for (int i = 0; i < 25; i++) driver.Step(.02f);
                    arena.FeelFeedback.TickPresentation(.4f);
                    CameraOutputBinding.GetOrCreate(arena.ViewCamera).ApplyNow();
                    if (arena.FeelFeedback.HitStopped || arena.FeelFeedback.CurrentCameraOffset != Vector3.zero)
                        throw new Exception("Transient hit-stop or camera offset did not expire.");
                    Capture("after-hit"); PrepareTarget(); secondHitStart = arena.FeelFeedback.PresentedHits;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); phase = 3; return;
                }
                if (phase == 3)
                {
                    driver.Step(.02f); arena.FeelFeedback.TickPresentation(0);
                    if (arena.FeelFeedback.PresentedHits <= secondHitStart) return;
                    float difference = Mathf.Abs(arena.FeelFeedback.LastImpactAngle-firstAngle);
                    difference = Mathf.Min(difference,180-difference);
                    AdvanceToReadableImpact(); Capture("impact-second");
                    // The 256x144 first/second hit frames carry their own grey slash, so the basic
                    // attack Cue (ribbon, pixel tip trail or crescent sprite) must stay off for them.
                    var cue = GameObject.Find("BasicAttackEffect_base_sword_attack1");
                    if (!cue) cue = GameObject.Find("BasicAttackEffect_base_sword_attack2");
                    var ribbon=cue?cue.GetComponent<MeshFilter>():null;
                    var cueRenderer=cue?cue.GetComponent<SpriteRenderer>():null;
                    if((cueRenderer&&cueRenderer.enabled&&cueRenderer.sprite)||(ribbon&&ribbon.sharedMesh&&ribbon.sharedMesh.vertexCount>0))
                        throw new Exception("The base sword hits draw their slash in the body frames; a basic attack Cue was still rendered: "+cue.name);
                    if(arena.FeelFeedback.UsePixelEffects&&arena.FeelFeedback.PixelFx.LastTrailSamples>0)
                        throw new Exception("The base sword hits draw their slash in the body frames; the pixel blade tip trail was still drawn.");
                    string cuePath="slash-in-body-frames";
                    File.WriteAllText(Path.Combine(Output, "verification.json"),
                        "{\"pass\":true,\"realAttack\":true,\"hitCount\":" + arena.FeelFeedback.PresentedHits + ",\"targetHealth\":" + target.health.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        ",\"angleDifference\":" + difference.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"swordSprite\":\"" + cuePath + "\",\"scene\":\"Chapter1Procedural\",\"monster\":\"01_thorn-root\",\"basicHitStop\":false,\"pixelTrails\":" + (arena.FeelFeedback.UsePixelEffects ? "true" : "false") + ",\"pixelHits\":" + (arena.FeelFeedback.UsePixelHits ? "true" : "false") + "}");
                    if(request=="verify-features") {
                        for(int i=0;i<40;i++)driver.Step(.02f);
                        PrepareTarget();arena.FeelFeedback.ResetFeedback();
                        driver.Step(.02f,new CombatInput(0,CombatButtons.Special));phase=4;return;
                    }
                    Finish(true);
                }
                if(phase==4) {
                    driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);
                    if(arena.FeelFeedback.ActiveDamageNumberCount==0)return;
                    if(!arena.FeelFeedback.LastDamageWasHeavy||(arena.FeelFeedback.UsePixelEffects?arena.FeelFeedback.PixelFx.ArcCount==0&&arena.FeelFeedback.PixelFx.LastDrawnPixels<40:arena.FeelFeedback.ActiveCleaveCount==0))throw new Exception("The real heavy cleave did not show its trail and heavy damage.");
                    if(!arena.FeelFeedback.HitStopped)throw new Exception("The heavy cleave must start its tuned hit-stop.");
                    Capture("heavy-cleave-contact");
                    for(int i=0;i<3;i++){driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);}
                    Capture("heavy-cleave");
                    for(int i=0;i<40;i++){driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);}
                    PrepareTarget();target.health=1;arena.FeelFeedback.ResetFeedback();driver.Step(.02f,new CombatInput(0,CombatButtons.Basic));phase=5;return;
                }
                if(phase==5) {
                    driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);
                    if(!target.Dead)return;
                    if(arena.FeelFeedback.ActiveDeathPopCount!=1||!arena.FeelFeedback.OwnsDeathVisual(target.visual))throw new Exception("The finishing hit did not replace the native dissolve with a pop.");
                    for(int i=0;i<6;i++){driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);}
                    target.GetComponent<Stage1VillageMonsterPresentation>().Draw();Capture("death-pop");
                    // The practice target already proved this side of the room is reachable.
                    var attacker=world.SpawnMonster("02_broom-stalker",arena.Player.Position+Vector2.left*1.2f,true,1);
                    phase=6;return;
                }
                if(phase==6) {
                    driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);
                    if(!arena.FeelFeedback.PlayerHurtVignetteActive)return;
                    if(arena.LastPlayerKnockDistance<.9f)throw new Exception("Player knockback did not use the new contact-box exit policy.");
                    Capture("player-hurt");
                    File.WriteAllText(Path.Combine(Output,"features.json"),"{\"pass\":true,\"realHeavyCleave\":true,\"numericDamage\":true,\"deathPop\":true,\"playerRedEdges\":true,\"knockDistance\":"+arena.LastPlayerKnockDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"swordSlashInBodyFrames\":true}");
                    for(int i=0;i<50;i++)driver.Step(.02f);
                    PrepareTarget();arena.Player.selected=1;
                    driver.Step(.02f,new CombatInput(0,CombatButtons.Basic));phase=7;return;
                }
                if(phase==7) {
                    driver.Step(.02f);arena.Presentation.Draw(0);
                    // The current chapter start equips the basic hammer in slot 2 instead of the whip.
                    if(arena.Player.Form.id=="hammer"&&arena.FeelFeedback.UsePixelEffects) {
                        arena.FeelFeedback.TickPresentation(.02f);
                        if(arena.FeelFeedback.PixelFx.ArcCount==0)return;
                        // Wait for the delayed arc (it starts 90 ms before contact), then capture its sweep and the impact.
                        for(int i=0;i<40;i++){arena.FeelFeedback.PixelFx.Refresh();if(arena.FeelFeedback.PixelFx.LastDrawnPixels>40)break;driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);arena.Presentation.Draw(0);}
                        for(int i=0;i<4;i++){Capture("hammer-arc-"+i);driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);arena.Presentation.Draw(0);}
                        File.WriteAllText(Path.Combine(Output,"hammer.json"),"{\"pass\":true,\"hammerArc\":true,\"hitStopped\":"+(arena.FeelFeedback.HitStopped?"true":"false")+"}");
                        Finish(true);return;
                    }
                    var node=GameObject.Find("BasicAttackEffect_whip_attack1");var mesh=node?node.GetComponent<MeshFilter>():null;
                    if(!mesh||mesh.sharedMesh.vertexCount<12)return;
                    Capture("whip-curve");
                    File.WriteAllText(Path.Combine(Output,"whip.json"),"{\"pass\":true,\"nativeWhipCurve\":true}");
                    for(int i=0;i<50;i++)driver.Step(.02f);
                    PrepareTarget();arena.Player.selected=1;arena.FeelFeedback.ResetFeedback();
                    driver.Step(.02f,new CombatInput(0,CombatButtons.Special));phase=8;return;
                }
                if(phase==8) {
                    driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);arena.Presentation.Draw(0);
                    var node=GameObject.Find("Whip skill wrapping sweep");var mesh=node?node.GetComponent<MeshFilter>():null;
                    if(!mesh||mesh.sharedMesh.vertexCount<12)return;
                    for(int i=0;i<5;i++){driver.Step(.02f);arena.FeelFeedback.TickPresentation(.02f);arena.Presentation.Draw(0);}
                    Capture("whip-skill");
                    File.WriteAllText(Path.Combine(Output,"whip-skill.json"),"{\"pass\":true,\"wrappingSweep\":true}");
                    Finish(true);
                }
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "verification-error.txt"), error.ToString()); Debug.LogException(error); Finish(false);
            }
        }
        // Real-scene numbers for the tuned feel: enemy push, player step, visual lift and lean per attack.
        static void MeasureFeel()
        {
            PrepareTarget(); driver = arena.AcquireManualSimulation(); arena.FeelFeedback.ResetFeedback();
            var sb = new System.Text.StringBuilder("{\"motionLoaded\":" + (arena.FeelMotion != null ? "true" : "false"));
            var clock = new System.Diagnostics.Stopwatch(); double fxTotal = 0, fxMax = 0, tickMax = 0; int fxFrames = 0;
            void Swing(string label, int slot, bool special)
            {
                for (int i = 0; i < 30; i++) driver.Step(.02f);
                PrepareTarget(); arena.Player.selected = slot; arena.FeelFeedback.ResetFeedback();
                double swingTick = 0; float tx = target.Position.x, px = arena.Player.Position.x, maxPush = 0, maxStep = 0, maxLift = 0, maxLean = 0; int hits = arena.FeelFeedback.PresentedHits; bool stopped = false;
                driver.Step(.02f, new CombatInput(0, special ? CombatButtons.Special : CombatButtons.Basic));
                for (int i = 0; i < 45; i++)
                {
                    driver.Step(.02f);
                    clock.Restart(); arena.FeelFeedback.TickPresentation(.02f); double clockTick = clock.Elapsed.TotalMilliseconds; tickMax = Math.Max(tickMax, clockTick); swingTick = Math.Max(swingTick, clockTick);
                    target.GetComponent<Stage1VillageMonsterPresentation>().Draw(); arena.FeelFeedback.EnemyMotion.Refresh();
                    clock.Restart(); arena.FeelFeedback.PixelFx?.Refresh(); double fx = clock.Elapsed.TotalMilliseconds;
                    fxTotal += fx; fxMax = Math.Max(fxMax, fx); fxFrames++;
                    stopped |= arena.FeelFeedback.HitStopped;
                    maxPush = Mathf.Max(maxPush, Mathf.Abs(target.Position.x - tx)); maxStep = Mathf.Max(maxStep, Mathf.Abs(arena.Player.Position.x - px));
                    maxLift = Mathf.Max(maxLift, target.visual.transform.localPosition.y);
                    float z = target.visual.transform.localEulerAngles.z; maxLean = Mathf.Max(maxLean, Mathf.Min(z, 360 - z));
                }
                sb.Append(",\"" + label + "\":{\"form\":\"" + arena.Player.Form.id + "\",\"hits\":" + (arena.FeelFeedback.PresentedHits - hits) +
                    ",\"enemyPush\":" + maxPush.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"playerStep\":" + maxStep.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"visualLift\":" + maxLift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"leanDegrees\":" + maxLean.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ",\"hitStop\":" + (stopped ? "true" : "false") + ",\"tickMs\":" + swingTick.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "}");
            }
            // Each attack twice: the first pass includes one-time costs (first load / JIT).
            Swing("swordBasic", 0, false); Swing("hammerBasic", 1, false); Swing("swordSpecial", 0, true);
            Swing("swordBasicAgain", 0, false); Swing("hammerBasicAgain", 1, false); Swing("swordSpecialAgain", 0, true);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.Append(",\"pixelFxMs\":{\"avg\":" + (fxTotal / Math.Max(1, fxFrames)).ToString("0.###", inv) + ",\"max\":" + fxMax.ToString("0.###", inv) +
                ",\"tickMax\":" + tickMax.ToString("0.###", inv) + "}");
            sb.Append("}"); File.WriteAllText(Path.Combine(Output, "measure.json"), sb.ToString()); Debug.Log("FEEL_MEASURE " + sb);
        }
        static int holdIndex, holdStage, holdWait, holdRetry;
        static bool holdBroom;
        static FieldMouseEnemy holdActor;
        static string holdLog;
        static void HoldStep()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (holdStage == 0)
            {
                PrepareTarget(); driver = arena.AcquireManualSimulation(); target.gameObject.SetActive(false);
                holdIndex = 1; holdBroom = false; holdLog = ""; holdStage = 1; return;
            }
            if (holdStage == 1)
            {
                // A fresh, already-aggressive monster each round (as the player-hurt shot does): a monster that has
                // returned home only re-engages under the chase AI's stricter conditions.
                for (int i = 0; i < 40; i++) driver.Step(.02f);
                PrepareHoldCompare.Set(holdIndex);
                // Every round's stroke lands on the player; keep it alive and targetable for the next one.
                arena.Player.health = arena.Player.maxHealth;
                if (holdActor) holdActor.gameObject.SetActive(false);
                holdActor = world.SpawnMonster(holdBroom ? "02_broom-stalker" : "01_thorn-root", arena.Player.Position + Vector2.left * (holdBroom ? 1.2f : 1.7f), true, 1);
                holdWait = 0; holdStage = 2; return;
            }
            var actor = holdActor;
            var presentation = actor.GetComponent<Stage1VillageMonsterPresentation>();
            var stroke = actor.GetComponent<Stage1VillageMonsterEffects>().EffectRenderer;
            for (int i = 0; i < 10 && !stroke.enabled; i++) { driver.Step(.01f); presentation.Draw(); }
            var attack = actor.GetComponent<Stage1VillageMonsterAttack>();
            string entry = "{\"monster\":\"" + actor.Definition.id + "\",\"extra\":" + attack.HoldExtra.ToString("0.###", inv);
            if (!stroke.enabled)
            {
                if (holdWait == 60 && holdRetry < 2) { holdRetry++; holdStage = 1; return; }
                if (++holdWait <= 60) return;
                // Whether the chase AI starts an attack at all is not what this measures: record and move on.
                holdLog += (holdLog.Length > 0 ? "," : "") + entry + ",\"skipped\":\"no attack started: " + actor.Brain.State + "\"}";
            }
            else
            {
                float strokeAt = actor.SpeciesAttackAge, expected = attack.Spec.damageStart + attack.HoldExtra;
                if (Mathf.Abs(strokeAt - expected) > .025f) throw new Exception(actor.Definition.id + " stroke at " + strokeAt + "s, expected " + expected + "s");
                // Capture 0.15 s into the stroke, where it is widest, with the attack box in viewport fractions
                // (x0,y0,x1,y1, y up) so the picture can be checked against the hit box.
                for (int i = 0; i < 15; i++) { driver.Step(.01f); presentation.Draw(); }
                var box = actor.AttackBounds; var view = arena.ViewCamera;
                Vector3 lo = view.WorldToViewportPoint(box.min), hi = view.WorldToViewportPoint(box.max);
                Capture("hold-" + (holdBroom ? "broom" : "thorn") + "-" + attack.HoldExtra.ToString("0.0", inv));
                holdLog += (holdLog.Length > 0 ? "," : "") + entry + ",\"strokeAt\":" + strokeAt.ToString("0.###", inv) + ",\"prepare\":" + actor.Definition.prepare.ToString("0.###", inv) +
                    ",\"attackBox\":[" + lo.x.ToString("0.####", inv) + "," + lo.y.ToString("0.####", inv) + "," + hi.x.ToString("0.####", inv) + "," + hi.y.ToString("0.####", inv) + "]}";
            }
            actor.aggressive = false; actor.CancelAttack(); holdRetry = 0;
            if (++holdIndex <= 4) { holdStage = 1; return; }
            if (!holdBroom) { holdBroom = true; holdIndex = 1; holdStage = 1; return; }
            PrepareHoldCompare.Set(0); holdStage = 0;
            File.WriteAllText(Path.Combine(Output, "hold.json"), "{\"pass\":true,\"results\":[" + holdLog + "]}");
            Finish(true);
        }
        static int compareVariant, compareStage, compareStart;
        static string compareLog;
        // One editor tick between stepping and capturing lets HitFeedbackBridge.LateUpdate sample its prefab.
        static void CompareStep()
        {
            var feel = arena.FeelFeedback;
            if (compareStage == 0)
            {
                PrepareTarget(); driver = arena.AcquireManualSimulation(); compareVariant = 0; compareLog = ""; compareStage = 1; return;
            }
            if (compareStage == 1)
            {
                for (int i = 0; i < 30; i++) driver.Step(.02f);
                arena.PlayerCritChance = compareVariant == 3 ? 1 : 0; PrepareTarget(); compareStart = feel.PresentedHits;
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); compareStage = 2; return;
            }
            if (compareStage == 2)
            {
                driver.Step(.02f); feel.TickPresentation(.02f);
                if (feel.PresentedHits <= compareStart) return;
                for (int i = 0; i < 2; i++) { driver.Step(.02f); feel.TickPresentation(.02f); }
                arena.Presentation.Draw(0); compareStage = 3; return;
            }
            target.GetComponent<Stage1VillageMonsterPresentation>().Draw();
            if (compareStage == 3)
            {
                Capture("compare-" + compareVariant + "-a");
                compareLog += (compareLog.Length > 0 ? "," : "") + "{\"hit\":" + compareVariant + ",\"critical\":" + (arena.LastHitWasCritical ? "true" : "false") + ",\"prefab\":\"" + BridgeValue("LastHitVfxKey") + "\",\"prefabHits\":\"" + BridgeValue("StartedHits") + "\",\"prefabError\":\"" + BridgeValue("LastError") + "\",\"pixelHits\":" + (feel.UsePixelHits ? "true" : "false") + ",\"pixelBursts\":" + feel.PixelFx.BurstCount + "}";
                for (int i = 0; i < 3; i++) { driver.Step(.02f); feel.TickPresentation(.02f); }
                arena.Presentation.Draw(0); compareStage = 4; return;
            }
            Capture("compare-" + compareVariant + "-b");
            if (++compareVariant < 4) { compareStage = 1; return; }
            arena.PlayerCritChance = 0; compareStage = 0;
            File.WriteAllText(Path.Combine(Output, "compare.json"), "{\"pass\":true,\"hits\":[" + compareLog + "]}");
            Finish(true);
        }
        // The hit-feedback bridge lives in the Integration assembly; read its state by name for the capture log only.
        static string BridgeValue(string property)
        {
            var bridge = arena.GetComponent("HitFeedbackBridge");
            return bridge ? (bridge.GetType().GetProperty(property)?.GetValue(bridge)?.ToString() ?? "").Replace("\"", "'") : "no bridge";
        }
        static void AdvanceToReadableImpact()
        {
            for (int i = 0; i < 2; i++) { driver.Step(.02f); arena.FeelFeedback.TickPresentation(.02f); }
            arena.Presentation.Draw(0); CameraOutputBinding.GetOrCreate(arena.ViewCamera).ApplyNow();
        }
        static ParticleSystem ImpactParticles() => arena.FeelFeedback.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "Combat feel ink impacts");
        static void PrepareTarget()
        {
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            if (!prepared) {
                var offers = world.GetComponent<Chapter1StartingBlueprints>();
                if (offers && !offers.Complete) for (int i = 0; i < 3 && !offers.Complete; i++) {
                    arena.Player.Position = offers.OfferPosition(i); Physics2D.SyncTransforms();
                    if (!offers.TryChoose(i)) throw new Exception("Could not prepare the existing weapon offer for the test.");
                }
                world.Warp(1);
                arena.Player.Reconfigure(1,Array.FindIndex(arena.catalog.forms,f=>f.id=="whip"),0); prepared = true;
            }
            var player = arena.Player;
            player.selected = 0;
            player.SyncFacing(-1);
            if (!target) target = world.SpawnMonster("01_thorn-root", player.Position + Vector2.left * 1.7f, false, 1);
            foreach (var other in arena.enemies) if (other && other != target &&
                Mathf.Abs(other.Position.x-player.Position.x)<5 && Mathf.Abs(other.Position.y-player.Position.y)<3)
                other.gameObject.SetActive(false);
            target.health = target.maxHealth = verify?1000:60;
            target.Position = player.Position + Vector2.left * 1.7f;
            target.velocity = Vector2.zero;
            target.spawn = target.Position; target.SyncFacing(1);
            Physics2D.SyncTransforms();
        }
        static void Capture(string name)
        {
            world.GetComponent<VillagePresentation>().RefreshForCapture();
            arena.FeelFeedback.EnemyMotion?.Refresh(); arena.FeelFeedback.PixelFx?.WaitForBursts(); arena.FeelFeedback.PixelFx?.Refresh();
            var camera = arena.ViewCamera; var previous = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; camera.rect = rect; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image);
            }
        }
        static void Finish(bool success)
        {
            driver?.Dispose(); driver = null; SessionState.SetString(Request, "");
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
