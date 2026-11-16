using System;
using System.Linq;
using System.Collections.Generic;
using Deviant.Chapter2.RoseBlue;

static class Program
{
    static int passed;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Near(double expected, double actual) { Assert(Math.Abs(expected - actual) < 1e-7, $"Expected {expected}, got {actual}"); }
    static void Test(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
    static Encounter Duo()
    {
        var e = new Encounter(RevivalAttackPolicy.PreserveSpawned); e.CompleteCutscene(e.Begin()); e.DamageBatch(Tuning.MachineHp, 0, 0);
        Assert(e.Phase == Phase.FloorTransition, "Machine did not start floor transition");
        e.CompleteCutscene(e.Generation); return e;
    }
    static Encounter Fused()
    {
        var e = Duo(); e.DamageBatch(0, Tuning.RoseHp, Tuning.BlueHp); e.Advance(.3); e.CompleteCutscene(e.Generation); return e;
    }
    static Battlefield Field()
    {
        // Test geometry only. These coordinates are not production defaults.
        return new Battlefield(new Box(0,0,30,18),new[] {
            new Platform(3,9,3,0),new Platform(21,27,3,0),
            new Platform(1,7,7,1),new Platform(12,18,7,1),new Platform(23,29,7,1),
            new Platform(5,11,11,2),new Platform(19,25,11,2)
        },42);
    }
    static int Main()
    {
        try
        {
            Test("cutscene tokens cannot complete another phase or complete twice", () => {
                var e = new Encounter(RevivalAttackPolicy.PreserveSpawned); int intro = e.Begin();
                Assert(e.EliteRemoved && !e.CanDamage(Target.Machine), "Introduction protection");
                e.Advance(100); Assert(e.Phase == Phase.Introduction, "Cutscene must explicitly finish");
                Assert(e.CompleteCutscene(intro) && !e.CompleteCutscene(intro), "Duplicate completion");
                e.DamageBatch(Tuning.MachineHp, Tuning.RoseHp, Tuning.BlueHp); Near(Tuning.RoseHp, e.RoseHp); Near(Tuning.BlueHp, e.BlueHp);
                Assert(!e.CompleteCutscene(intro), "Stale token advanced floor cutscene");
            });
            Test("simultaneous duo deaths always start absorption", () => {
                var e = Duo(); e.DamageBatch(0, Tuning.RoseHp, Tuning.BlueHp);
                Assert(e.Phase == Phase.AbsorptionPush, "Revival incorrectly won");
                e.Advance(.29); Assert(e.Phase == Phase.AbsorptionPush, "Push too short");
                e.Advance(.01); Assert(e.Phase == Phase.Absorption, "Missing cutscene handshake");
                e.Advance(100); Assert(e.Phase == Phase.Absorption, "Timed auto completion");
                e.CompleteCutscene(e.Generation); Near(Tuning.FusedBlueHp, e.BlueHp);
            });
            Test("either duo boss death transitions to fused Blue without a health floor", () => {
                foreach (bool blue in new[]{true,false}) {
                    var e=Duo();e.DamageBatch(0,blue?0:Tuning.RoseHp,blue?Tuning.BlueHp:0);
                    Assert(e.Phase==Phase.AbsorptionPush,"Duo kill did not end phase");
                    e.Advance(.3);e.CompleteCutscene(e.Generation);
                    Assert(e.Phase==Phase.FusedBlue,"Missing fused Blue");Near(Tuning.FusedBlueHp,e.BlueHp);
                }
            });
            Test("run death terminates rather than resetting the boss", () => {
                var e = Fused(); e.EndRun(); e.Advance(100);
                Assert(e.Phase == Phase.RunEnded && !e.CompleteCutscene(e.Generation), "Run resumed");
            });
            Test("homing hit damages Blue and cancels casting", () => {
                var e = Fused(); Notice n; while(e.TryRead(out n)) {}
                e.HomingArrowHitsBlue(); Near(Tuning.FusedBlueHp - Tuning.HomingBossDamage, e.BlueHp);
                bool stun = false, cancel = false;
                while(e.TryRead(out n)) { stun |= n.Kind == Signal.BlueStunned; cancel |= n.Kind == Signal.CancelUnspawnedAttacks; }
                Assert(stun && cancel, "Missing control signals");
            });
            Test("exchange projectile destruction wins over same-step center contact", () => {
                var v = new ExchangeVolley(30, true); v.Advance(2.79); v.DamageBatch(200, 150); v.Advance(.01);
                Assert(v.Result == ExchangeResult.BothDestroyed, "Center incorrectly exploded");
            });
            Test("surviving exchange accelerates while keeping independent boss damage", () => {
                var e = Duo(); e.DamageBatch(0, 0, 200); var v = e.BeginExchange(30, true);
                v.DamageBatch(100, 74); v.Advance(.27); Assert(!v.Finished, "Survivor arrived early");
                v.Advance(.011); e.ResolveExchange(); Near(Tuning.RoseHp - Tuning.ExchangeBlueImpactDamage, e.RoseHp); Near(Tuning.BlueHp - 200 + Tuning.ExchangeHeal, e.BlueHp);
                Assert(e.CanDamage(Target.Rose) && !e.CanDamage(Target.Blue), "Wrong vulnerable boss");
                e.Advance(1.99); Assert(!e.CanDamage(Target.Blue), "Other boss became vulnerable early");
                e.Advance(.01); Assert(e.CanDamage(Target.Blue), "Invulnerability never ended");
            });
            Test("exchange center explosion immediately releases both bosses", () => {
                var e = Duo(); var v = e.BeginExchange(30, true); v.Advance(2.8); e.ResolveExchange();
                Assert(v.Result == ExchangeResult.CenterExplosion && e.CanDamage(Target.Rose) && e.CanDamage(Target.Blue), "Immediate release failed");
            });
            Test("coarse simulation emits every rain event in timestamp order", () => {
                var clock = new PatternClock(Patterns.Build(Pattern.ArrowRain)); var cues = clock.Advance(10);
                var lands = cues.Where(c => c.Channel == "rain-land").ToArray();
                var blasts = cues.Where(c => c.Kind == CueKind.Explode).ToArray();
                Assert(lands.Length == 12 && blasts.Length == 12 && clock.Finished, "Skipped events");
                Near(.75, lands[0].At); Near(2.25, lands[11].At); Near(2.45, blasts[0].At); Near(3.95, blasts[11].At);
                Near(4.1, cues.Last().At);
                for(int i=1;i<cues.Count;i++) Assert(cues[i].At >= cues[i-1].At, "Order reversed");
            });
            Test("stun cancels unspawned rain but keeps existing landing and explosion", () => {
                var clock = new PatternClock(Patterns.Build(Pattern.ArrowRain)); clock.Advance(.51); clock.InterruptCaster();
                var cues = clock.Advance(10);
                Assert(cues.Count == 3 && cues.All(c => c.Group == "rain-0"), "Independent rain lifecycle lost");
            });
            Test("parrying rain removes that arrow's future explosion only", () => {
                var clock = new PatternClock(Patterns.Build(Pattern.ArrowRain)); clock.Advance(1); clock.CancelHazard("rain-0");
                var cues = clock.Advance(10);
                Assert(!cues.Any(c => c.Group == "rain-0") && cues.Count(c => c.Kind == CueKind.Explode) == 11, "Wrong rain cancelled");
            });
            Test("all bounce arrows whiten and explode on shared deadlines", () => {
                var clock = new PatternClock(Patterns.Build(Pattern.Bounce)); clock.Advance(.75); clock.InterruptCaster();
                var cues = clock.Advance(10); Assert(cues.Count == 3, "Unspawned bounce survived");
                Near(4, cues[0].At); Near(5, cues[1].At); Near(5.15, cues[2].At);
            });
            Test("trap lifetime does not erase sniper lock and delayed impact", () => {
                var cues = new PatternClock(Patterns.Build(Pattern.TrapSnipe)).Advance(4);
                Near(.7, cues.Single(c => c.Kind == CueKind.Spawn).At);
                Near(1, cues.Single(c => c.Kind == CueKind.Remove).At);
                Near(2.7, cues.Single(c => c.Kind == CueKind.LockTarget).At);
                Near(2.95, cues.Single(c => c.Kind == CueKind.Damage).At);
            });
            Test("platform rain hits at start and half-second but not at expiration", () => {
                var clock = new PatternClock(Patterns.Build(Pattern.MachineAndPlatforms));
                var cues = clock.Advance(2.5);
                var hits = cues.Where(c => c.Kind == CueKind.Damage && c.Channel == "platform-rain").ToArray();
                Assert(hits.Length == 2, "Extra tick"); Near(2, hits[0].At); Near(2.5, hits[1].At);
                Assert(clock.HasPendingAttack, "Rest must wait for the rain to end");
                var end = clock.Advance(.5);
                Assert(end.All(c => c.Kind == CueKind.Remove) && !clock.HasPendingAttack, "Rain must end without another hit");
            });
            Test("reentry requires leaving and grace; rejected entries never become delayed hits", () => {
                var contact = new ReentryContact(); int hits = 0; Func<bool> hit = () => { hits++; return true; };
                Assert(contact.Observe(true, 0, true, hit), "First touch");
                contact.Observe(true, 1, true, hit); Near(1, hits);
                contact.Observe(false, 1.1, true, hit); contact.Observe(true, 1.2, true, hit); Near(2, hits);
                contact.Observe(false, 1.3, true, hit); contact.Observe(true, 1.4, true, hit);
                contact.Observe(true, 2, true, hit); Near(2, hits);
                contact.Observe(false, 2.1, true, hit); contact.Observe(true, 2.2, true, hit); Near(3, hits);
            });
            Test("whitening and damage rejection never consume accepted-damage cooldown", () => {
                var c = new ReentryContact(); int calls = 0; Func<bool> hit = () => { calls++; return false; };
                c.Observe(true, 0, false, hit); Near(0, calls); c.Observe(false, .1, true, hit);
                c.Observe(true, .2, true, hit); Near(1, calls); c.Observe(false, .3, true, hit);
                Assert(c.Observe(true, .4, true, () => true), "Rejected hit started cooldown");
            });
            Test("bounce explosions share damage admission; separate rain explosions stack", () => {
                var ledger = new HitLedger(); int hits = 0; Func<bool> hit = () => { hits++; return true; };
                for (int i=0;i<3;i++) ledger.TryHit("bounce-pattern-1", 1, hit); Near(1, hits);
                for (int i=0;i<3;i++) ledger.TryHit("rain-pattern-1-arrow-" + i, 1, hit); Near(4, hits);
                ledger.TryHit("other", 1, () => false); Assert(ledger.TryHit("other", 1, hit), "Rejected damage consumed hit");
            });
            Test("phase-one traps reroot while phase-two zones cannot reroot", () => {
                foreach (bool once in new[] { false, true }) {
                    var c = new RootContact(once); Assert(c.Observe(true, true), "First root");
                    Assert(!c.Observe(true, true), "Continuous overlap reroot"); c.Observe(false, true);
                    Assert(c.Observe(true, true) != once, "Wrong phase reentry policy");
                }
            });
            Test("rain edge gaps and reversed sequence preserve the same geometry", () => {
                Near(1.25, HazardGeometry.RainX(0,30,0,false)); Near(28.75,HazardGeometry.RainX(0,30,11,false));
                for(int i=0;i<12;i++) Near(HazardGeometry.RainX(0,30,i,false), HazardGeometry.RainX(0,30,11-i,true));
                Near(2.2, HazardGeometry.RainX(0,30,1,false)-HazardGeometry.RainX(0,30,0,false)-Tuning.RainWidth);
            });
            Test("homing angular speed and movement speed are bounded", () => {
                var v = HazardGeometry.HomingVelocity(new Point(0,0),new Point(7,0),new Point(-10,0),.25);
                Near(7,v.Length); Near(Math.PI/6,Math.Atan2(v.Y,v.X));
            });
            Test("normal reflection stays inward with speed preserved across many walls", () => {
                var p = new Point(5,5); var v = new Point(10,0); var box = new Box(0,0,10,10);
                HazardGeometry.Bounce(ref p,ref v,box,.5,new Random(7)); Assert(v.X<0,"Outward normal reflection");
                double angle=Math.Atan2(Math.Abs(v.Y),Math.Abs(v.X))*180/Math.PI;
                Assert(angle>=30 && angle<=60,"Normal angle outside range");
                var segments=HazardGeometry.Bounce(ref p,ref v,box,100,new Random(7));
                Assert(segments.Count>20,"Skipped walls"); Near(10,v.Length);
                Assert(p.X>=-1e-8 && p.X<=10+1e-8 && p.Y>=-1e-8 && p.Y<=10+1e-8,"Escaped arena");
            });
            Test("simultaneous corner collision reflects both axes", () => {
                var p=new Point(5,5); var v=new Point(5,5);
                HazardGeometry.Bounce(ref p,ref v,new Box(0,0,10,10),1.1,new Random(1));
                Near(9.5,p.X);Near(9.5,p.Y);Near(-5,v.X);Near(-5,v.Y);
            });
            Test("phase-three picker never repeats or allows other attacks alongside homing", () => {
                var picker=new PatternPicker(42); Pattern? last=null;
                for(int i=0;i<1000;i++){var next=picker.Next(false);Assert(next!=last,"Repeated attack");last=next;}
                var overlap=picker.Next(true); Assert(overlap==null,"No attack may overlap homing");
                Assert(picker.Next(true)==null,"Second overlapping bounce");
            });
            Test("phase transition cancels every pending boss cue", () => {
                var clock=new PatternClock(Patterns.Build(Pattern.Bounce));clock.Advance(2);clock.EndPhase();
                Assert(clock.Advance(10).Count==0 && clock.Finished,"Previous phase hazard survived");
            });
            Test("revival cleanup policy must be explicitly supplied", () => {
                var e=new Encounter(RevivalAttackPolicy.ClearAllBossEffects);e.CompleteCutscene(e.Begin());
                e.DamageBatch(Tuning.MachineHp,0,0);e.CompleteCutscene(e.Generation);Notice n;while(e.TryRead(out n)){}
                e.DamageBatch(0,0,Tuning.BlueHp);bool cleared=false;while(e.TryRead(out n))cleared|=n.Kind==Signal.CancelBossEffects;
                Assert(cleared,"Explicit cleanup policy ignored");
            });
            Test("remaining exchange projectile destruction policy is explicit", () => {
                var protectedVolley=new ExchangeVolley(30,false);protectedVolley.DamageBatch(200,0);protectedVolley.DamageBatch(0,150);
                Near(100,protectedVolley.BlueOrbHp);
                var destructibleVolley=new ExchangeVolley(30,true);destructibleVolley.DamageBatch(200,0);destructibleVolley.DamageBatch(0,150);
                Assert(destructibleVolley.Result==ExchangeResult.BothDestroyed,"Second destruction rejected");
                var sameBatch=new ExchangeVolley(30,false);sameBatch.DamageBatch(200,150);
                Assert(sameBatch.Result==ExchangeResult.BothDestroyed,"One-hit simultaneous destruction rejected");
            });
            Test("all seven platforms pulse while sequence keeps one uniformly random safe platform", () => {
                var field=Field();var safe=new HashSet<int>();
                for(int i=0;i<1000;i++){
                    Assert(field.PickTraps().SequenceEqual(Enumerable.Range(0,7)),"Trap omitted a platform");
                    var sequence=field.PickSequence();Assert(sequence.Length==6&&sequence.Distinct().Count()==6,"Invalid sequence");
                    safe.Add(Enumerable.Range(0,7).Single(n=>!sequence.Contains(n)));
                }
                Assert(safe.Count==7,"Sequence excludes a safe candidate");
            });
            Test("sequence retains its exact six-blast timing",()=>{
                var clock=new PatternClock(Patterns.Build(Pattern.SequenceBlasts));var cues=clock.Advance(10);
                var blast=cues.Where(c=>c.Channel=="sequence-blast").ToArray();Near(6,blast.Length);Near(4.24,blast[0].At);Near(4.94,blast[5].At);Near(5.39,cues.Last().At);
            });
            Test("single parryable duo arrow fires with the drop at two seconds",()=>{
                var cues=Patterns.Build(Pattern.RootAndShots);var shots=cues.Where(c=>c.Channel=="duo-arrow").ToArray();Near(1,shots.Length);Near(2,shots[0].At);
                Near(20,Tuning.DuoShotDamage);Near(20,Tuning.DuoShotSpeed);Near(2,cues.Single(c=>c.Channel=="blue-return").At);
            });
            Test("exchange acceleration is independent of simulation step size",()=>{
                var coarse=new ExchangeVolley(24,true);var fine=new ExchangeVolley(24,true);coarse.Advance(2);
                for(int i=0;i<200;i++)fine.Advance(.01);Near(coarse.Distance,fine.Distance);
                Assert(!coarse.Finished,"Premature collision");coarse.Advance(.801);Assert(coarse.Result==ExchangeResult.CenterExplosion,"Missing collision");
            });
            Test("machine relocation changes platform and reaches every platform", () => {
                var field=Field();var seen=new HashSet<int>();
                for(int i=0;i<1000;i++){int old=field.MachinePlatform;int next=field.MoveMachine();Assert(next!=old,"Machine stayed in place");seen.Add(next);}
                Assert(seen.Count==7,"A platform was excluded");
            });
            Test("fall respawn does not exclude dangerous platforms", () => {
                var field=Field();var picked=new HashSet<int>();for(int i=0;i<1000;i++)picked.Add(field.PickFallRespawn());
                Assert(picked.Count==7,"Some platforms excluded from random respawn");Near(3,field.MachinePlatform);
            });
            Test("layer shots choose different layers and respect the explicit tie policy", () => {
                var field=Field();int rose,blue;bool left;
                var nearestOwners=new HashSet<bool>();
                for(int i=0;i<1000;i++){field.PickLayerShots(5,true,out rose,out left,out blue);Assert(rose!=blue&&(rose==0||blue==0),"Nearest lower layer missing");nearestOwners.Add(rose==0);}
                Assert(nearestOwners.Count==2,"Only one boss follows the player");
                field.PickLayerShots(5,false,out rose,out left,out blue);Assert(rose==1||blue==1,"Upper tie preference ignored");
                field.PickLayerShots(-5,true,out rose,out left,out blue);Assert(rose==0||blue==0,"Below-arena nearest layer");
                var damage=new HashSet<int>();for(int i=0;i<1000;i++)damage.Add(field.RoseArrowDamage());
                Assert(damage.SetEquals(new[]{9,10,11,12}),"Damage roll bounds");
            });
            Test("root zone stays within arena", () => {
                var field=Field();Near(1,field.ClampedRootCenter(-10));Near(29,field.ClampedRootCenter(40));
            });
            Test("fused Blue picks only homing, bounce, rain and fan (crossbow drop removed)", () => {
                var picker=new PatternPicker(7);var seen=new HashSet<Pattern>();for(int i=0;i<400;i++){var p=picker.Next(false);if(p.HasValue)seen.Add(p.Value);}
                Assert(seen.SetEquals(new[]{Pattern.Homing,Pattern.Bounce,Pattern.ArrowRain,Pattern.ExpandingFan}),"Fused Blue pool");
            });
            Test("revival returns Rose to swapped location", () => {
                var positions=new DuoPositions(new Point(0,0),new Point(30,0));positions.Swap();
                positions.BeginRevival();Near(0,positions.Rose.X);positions.FinishRevival();Near(30,positions.Rose.X);
                positions.BeginAbsorption();Near(30,positions.Blue.X);
            });
            Test("fan volley cadence and dive target lock", () => {
                var cues=Patterns.Build(Pattern.ExpandingFan);
                var shots=cues.Where(c=>c.Channel=="fan-arrow"&&c.Kind==CueKind.Spawn).ToArray();
                double[] times={3,3.5,3.6,4.1,4.2,4.3};
                Near(6,shots.Length);for(int i=0;i<6;i++)Near(times[i],shots[i].At);
                var dive=Patterns.Build(Pattern.RootAndShots);
                Near(1.86,dive.Single(c=>c.Channel=="root-zone"&&c.Kind==CueKind.LockTarget).At);
                Near(2,dive.Single(c=>c.Channel=="blue-return").At);
            });
            Test("phase-one attacks rest for two seconds after real damage expiration", () => {
                var r=new AttackRotation(2);r.SetPhase(Phase.Machine);var p=r.PickNext().Value;long id=r.Begin(p);r.CastingFinished();
                r.Advance(10);Assert(r.PickNext()==null,"Attacked while previous projectile lived");r.DamageEnded(id);
                Near(2,r.RestRemaining);r.Advance(1.99);Assert(r.PickNext()==null,"Attacked during rest");
                r.Advance(.01);var next=r.PickNext();Assert(next.HasValue&&next!=p,"Rest must end with a different attack available");
            });
            Test("every phase randomizes its opener and rests after each nonrepeating pattern", () => {
                foreach(var phase in new[]{Phase.Machine,Phase.Duo,Phase.FusedBlue}){
                    var openers=new HashSet<Pattern>();
                    for(int seed=0;seed<128;seed++){
                        var r=new AttackRotation(seed);r.SetPhase(phase);var first=r.PickNext().Value;openers.Add(first);
                        long id=r.Begin(first);r.CastingFinished();r.DamageEnded(id);Near(2,r.RestRemaining);
                        r.Advance(1.99);Assert(r.PickNext()==null,"Attacked before two seconds of rest");
                        r.Advance(.01);Assert(r.PickNext()!=first,"Repeated the preceding pattern");
                    }
                    Near(4,openers.Count);
                }
            });
            Test("pattern 1 fires the stun arrow and ink fireball together after the tell", () => {
                var spawns=Patterns.Build(Pattern.StunAndInk).Where(c=>c.Kind==CueKind.Spawn).ToArray();
                Near(2,spawns.Length);Near(1,spawns[0].At);Near(1,spawns[1].At);
                Assert(spawns.Any(c=>c.Channel=="stun-arrow")&&spawns.Any(c=>c.Channel=="ink-ball"),"Missing stun arrow or ink fireball");
            });
            Test("five bounce arrows spawn half a second apart before whitening", () => {
                var spawns=Patterns.Build(Pattern.Bounce).Where(c=>c.Kind==CueKind.Spawn).Select(c=>c.At).ToArray();
                Near(5,spawns.Length);for(int i=0;i<5;i++)Near(.5*(i+1),spawns[i]);Near(26,Tuning.BounceSpeed);
            });
            Test("homing has no forced follow-up and waits for its removal before rest", () => {
                foreach(bool hitBlue in new[]{false,true}){
                    var r=new AttackRotation(4);r.SetPhase(Phase.FusedBlue);long id=r.Begin(Pattern.Homing);r.CastingFinished();
                    r.Advance(10);Assert(r.PickNext()==null,"Attack overlapped a live homing arrow");
                    r.HomingRemoved(id,hitBlue);Near(2,r.RestRemaining);r.Advance(2);
                    Assert(r.PickNext()!=Pattern.Homing,"Homing repeated");
                }
                Assert(!Patterns.Build(Pattern.Homing).Any(c=>c.Channel=="bounce"),"Forced bounce remains scheduled");
            });
            Test("fused rest movement uses two units per second and three units maximum", () => {
                var move=new RestMovement();move.Begin(15,10);double x=move.Step(15,.5,1,29);Near(16,x);
                x=move.Step(x,1,1,29);Near(18,x);Near(18,move.Step(x,1,1,29));
                move.Begin(28,10);Near(29,move.Step(28,1,1,29));
            });
            Console.WriteLine($"{passed} tests passed"); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
