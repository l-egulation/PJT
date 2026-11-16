import assert from 'node:assert/strict';
import { Encounter, RULES, circleRect, overlap } from './model.mjs';

// Behavioral contract tests: run `node tests.mjs` from this directory.
// These use the same simulation as the playable preview; no browser mocks.
const results=[];
function test(name,run){try{run();results.push({name,pass:true});}catch(error){results.push({name,pass:false,error:error.message});}}
function near(actual,expected,tolerance=.00001){assert.ok(Math.abs(actual-expected)<=tolerance,`${actual} != ${expected} (±${tolerance})`);}
function game(options={}){const e=new Encounter(options);e.enterFight();e.nextIn=999;e.player.x=RULES.right-15;return e;}
function advance(e,seconds,input={},step=.01){let remaining=seconds;while(remaining>1e-8){const dt=Math.min(step,remaining);e.update(dt,input);remaining-=dt;}}
function directPattern(e,seconds,step=.01){let remaining=seconds;while(remaining>1e-8){const dt=Math.min(step,remaining);e.updatePattern(dt);remaining-=dt;}}

test('arena, fist, mixer and player use the declared game-unit scale',()=>{
  near((RULES.right-RULES.left)/RULES.ppu,30); // open arena = fixed camera width
  near(RULES.fist.width/RULES.ppu,26);near(RULES.fist.height/RULES.ppu,6);
  near(RULES.mixer.radius/RULES.ppu,6);near(RULES.mixer.distance/RULES.ppu,15);
  near(RULES.run/RULES.ppu,7.5);near(RULES.dashSpeed/RULES.ppu,22.5);
  const e=game(),b=e.playerBox();near(b.w/RULES.ppu,.9375);near(b.h/RULES.ppu,2);
});

test('intro gives 4.8 seconds without consuming fight time',()=>{
  const e=new Encounter();advance(e,4.79);assert.equal(e.status,'intro');near(e.elapsed,0);
  advance(e,.02);assert.equal(e.status,'fight');assert.ok(e.elapsed<=.01+.00001);
});

test('intro cues fire once and combat input stays locked until bars clear',()=>{
  const e=new Encounter();advance(e,4.79,{attack:true,jump:true,move:1});
  assert.equal(e.player.x,260);assert.equal(e.player.y,464);assert.equal(e.elapsed,0);
  const events=e.drain();assert.equal(events.filter(v=>v.type==='intro-impact').length,1);assert.equal(events.filter(v=>v.type==='intro-roar').length,1);
  advance(e,.03);assert.equal(e.status,'fight');assert.ok(e.elapsed<.04);
});
test('240-second execution sets player health to zero',()=>{
  const e=game();e.elapsed=239.99;advance(e,.02);assert.equal(e.status,'defeat');assert.equal(e.player.hp,0);
});

test('long fist has the exact warning / active / hold / retract / punish / wait order',()=>{
  const e=game();e.forcePattern('fist','left');
  for(const [time,stage] of [[.99,'warn'],[1.01,'strike'],[1.31,'hold'],[1.61,'retract'],[2.11,'recovery'],[4.11,'wait']]){
    const before=e.pattern.age;directPattern(e,time-before);assert.equal(e.pattern.stage,stage);
  }
  directPattern(e,6.11-e.pattern.age);assert.equal(e.pattern,null);
});

test('fist geometry leaves four game units on the opposite wall',()=>{
  for(const side of ['left','right']){const e=game();e.forcePattern('fist',side);e.pattern.age=1.4;const g=e.fistGeometry();
    near(g.w,26*RULES.ppu);near(g.h,6*RULES.ppu);
    near(side==='left'?RULES.right-(g.x+g.w):g.x-RULES.left,4*RULES.ppu);
  }
});

test('only extending fist deals damage, once, and a crossed endpoint cannot miss',()=>{
  for(const side of ['left','right']){
    const e=game();e.forcePattern('fist',side);e.player.x=side==='left'?600:350;
    e.pattern.age=1.29;e.update(.02);assert.equal(e.player.hp,75);assert.equal(e.pattern.stage,'hold');
    e.hitstop=0;e.player.invuln=0;advance(e,.6);assert.equal(e.player.hp,75);
    for(const age of [.8,1.4,1.8]){const h=game();h.forcePattern('fist',side);h.player.x=side==='left'?600:350;h.pattern.age=age;h.update(.01);assert.equal(h.player.hp,100);}
  }
});

test('normal hits respect invulnerability; dash blocks damage for only its invulnerable phase',()=>{
  const e=game();assert.equal(e.damagePlayer(25),true);assert.equal(e.damagePlayer(25),false);
  e.player.invuln=0;e.player.dash=.2;e.player.dashAge=.1;assert.equal(e.damagePlayer(25),false);
  e.player.dashAge=.16;assert.equal(e.damagePlayer(25),true);
});

test('fist knockback covers three game units when not stopped by an arena wall',()=>{
  const e=game();e.player.x=400;e.damagePlayer(25,{direction:1});e.hitstop=0;
  advance(e,.2);near(e.player.x-400,3*RULES.ppu);
});

test('a player near center can reach the advertised fist safe edge during its warning',()=>{
  for(const side of ['left','right']){const e=game();e.forcePattern('fist',side);e.player.x=480;const move=side==='left'?1:-1;
    e.update(.01,{move,dash:true});advance(e,1.35,{move});assert.equal(e.player.hp,100);
  }
});

test('one player swing damages shared body/fist HP only once',()=>{
  const e=game();e.player.x=350;e.player.face=1;e.player.attack=.2;e.player.hit=false;
  assert.ok(e.bossHurtboxes().filter(b=>overlap(b,e.swingBox())).length>=2,'fixture must overlap two boss parts');
  e.update(.01);assert.equal(e.boss.hp,RULES.bossHp-RULES.attackDamage);
  e.hitstop=0;advance(e,.1);assert.equal(e.boss.hp,RULES.bossHp-RULES.attackDamage);
});

test('body and fists have no contact damage; chains and ceiling cannot be attacked',()=>{
  const e=game();e.player.x=480;e.player.y=400;advance(e,.1);assert.equal(e.player.hp,100);
  e.player.y=160;e.player.attack=.2;e.player.hit=false;e.update(.01);assert.equal(e.boss.hp,RULES.bossHp);
});

test('mixer body is untargetable during exit/return, while expanded mixer can be hit',()=>{
  const e=game();e.forcePattern('mixer','left');
  for(const age of [.5,6]){e.pattern.age=age;assert.equal(e.bossHurtboxes().length,0);}
  e.pattern.age=2;assert.ok(e.bossHurtboxes().length>0);
  e.pattern.age=6.6;assert.ok(e.bossHurtboxes().length>=3);
});

test('mixer moves fifteen units in 4.5 seconds and leaves a three-unit safe edge',()=>{
  for(const side of ['left','right']){const e=game();e.forcePattern('mixer',side);e.pattern.age=1;const start=e.mixerGeometry();
    e.pattern.age=5.5-1e-7;const end=e.mixerGeometry();near(Math.abs(start.x-end.x),15*RULES.ppu,.001);
    near(side==='left'?RULES.right-(end.x+end.r):end.x-end.r-RULES.left,3*RULES.ppu,.001);
    e.pattern.age=5.5;assert.equal(e.mixerGeometry(),null);
  }
  assert.equal(circleRect({x:0,y:0,r:10},{x:11,y:0,w:3,h:3}),false);
  assert.equal(circleRect({x:0,y:0,r:10},{x:9,y:0,w:3,h:3}),true);
});

test('demo teaches escape toward the persistent mixer safe edge',()=>{
  const e=game();e.player.x=480;e.forcePattern('mixer','left');assert.equal(e.demoInput().move,1);
  e.forcePattern('mixer','right');assert.equal(e.demoInput().move,-1);
});

test('mixer alternates 7/8 damage every .25 sec, ignores ordinary invulnerability, never knocks back',()=>{
  const e=game();e.forcePattern('mixer','left');e.pattern.age=1;e.player.x=300;e.player.invuln=5;
  e.updatePattern(.001);assert.equal(e.player.hp,93);assert.equal(e.player.knock,0);
  directPattern(e,.24);assert.equal(e.player.hp,93);
  directPattern(e,.02);assert.equal(e.player.hp,85);assert.equal(e.player.knock,0);near(e.player.invuln,5);
  const h=game();h.forcePattern('mixer','left');h.pattern.age=1;h.player.x=300;h.player.dash=.2;h.player.dashAge=.01;
  h.updatePattern(.001);assert.equal(h.player.hp,100);
});

test('needle warning is a full second, followed by .3-second fall and .5-second harmless fade',()=>{
  const e=game();e.forcePattern('needle','left');e.player.x=RULES.right-15;
  advance(e,.99);assert.equal(e.events.some(v=>v.type==='needle-drop'),false,'a new needle was advanced twice on its spawn tick');
  advance(e,.03);assert.equal(e.events.some(v=>v.type==='needle-drop'),true);
  const n={id:999,x:e.player.x,age:1.31,speed:1,hit:false,landed:true};e.needles=[n];e.player.invuln=0;
  e.updateNeedles(.1);assert.equal(e.player.hp,100,'landed needle must be harmless');
  e.updateNeedles(.4);assert.equal(e.needles.length,0);
});

test('needle pattern announces 13 fixed locations, at .3-second intervals, ending at 8.9 seconds',()=>{
  const e=game();e.player.x=300;e.forcePattern('needle');const planned=[...e.pattern.positions];
  assert.equal(planned.length,13);near(planned[0],300);e.player.x=800;e.player.invuln=999;
  advance(e,3.61);assert.deepEqual(e.pattern.positions,planned);
  const warnings=e.events.filter(v=>v.type==='needle-warn');assert.equal(warnings.length,13);
  for(let i=1;i<warnings.length;i++)near(warnings[i].time-warnings[i-1].time,.3,.015);
  assert.ok(new Set(planned).size>=10,'warnings should not keep selecting the same cells');
  e.pattern.age=4.8;e.updatePattern(.01);assert.equal(e.pattern.stage,'strike');
  e.pattern.age=4.91;e.updatePattern(.01);assert.equal(e.pattern.stage,'recovery');
  e.pattern.age=6.91;e.updatePattern(.01);assert.equal(e.pattern.stage,'wait');
  e.pattern.age=8.89;e.updatePattern(.02);assert.equal(e.pattern,null);
});

test('stun cancels ordinary pattern but preserves already announced needles; mixer rejects stun',()=>{
  const e=game();e.forcePattern('needle');advance(e,.5);const pending=e.needles.length;assert.ok(pending>0);
  assert.equal(e.stun(),true);assert.equal(e.pattern,null);assert.equal(e.needles.length,pending);
  advance(e,.6);assert.ok(e.events.some(v=>v.type==='needle-drop'));
  e.forcePattern('mixer');const p=e.pattern;assert.equal(e.stun(),false);assert.equal(e.pattern,p);
});

test('random selection never repeats last pattern and is deterministic from its seed',()=>{
  const a=game({seed:711}),b=game({seed:711});let prior=null;
  for(let i=0;i<100;i++){a.startPattern();b.startPattern();assert.notEqual(a.pattern.type,prior);assert.equal(a.pattern.type,b.pattern.type);assert.equal(a.pattern.side,b.pattern.side);prior=a.pattern.type;}
});

test('enrage snapshots speed on the next pattern; current recovery/wait keeps its old speed',()=>{
  const e=game();e.forcePattern('fist','left');e.pattern.age=3;e.elapsed=119.9;
  advance(e,.2);assert.equal(e.enraged,true);near(e.pattern.speed,1);near(e.pattern.age,3.2);
  e.startPattern('fist','left');near(e.pattern.speed,1.25);advance(e,.2);near(e.pattern.age,.25);
  near(e.elapsed,120.3);
});

test('enraged mixer scales repeated-hit interval from .25 to .2 seconds',()=>{
  const e=game();e.enraged=true;e.forcePattern('mixer','left');e.pattern.age=1;e.player.x=300;
  e.updatePattern(.001);assert.equal(e.player.hp,93);directPattern(e,.19);assert.equal(e.player.hp,93);
  directPattern(e,.02);assert.equal(e.player.hp,85);
});

test('pause freezes all combat time and retry resets RNG, health, hazards and pattern history',()=>{
  const e=game({seed:91});e.forcePattern('needle');advance(e,.5);e.paused=true;const before=JSON.stringify(e.snapshot()),clock=e.time;
  advance(e,3);assert.equal(JSON.stringify(e.snapshot()),before);near(e.time,clock);
  e.reset();const fresh=new Encounter({seed:91});assert.deepEqual(e.snapshot(),fresh.snapshot());
  assert.equal(e.events.length,0);assert.equal(e.log.length,0);assert.equal(e.player.dashCd,0);
  e.enterFight();fresh.enterFight();e.startPattern();fresh.startPattern();assert.equal(e.pattern.type,fresh.pattern.type);
});

test('boss kill wins a same-frame timeout or mutual kill and clears pending hazards',()=>{
  for(const mutual of [false,true]){const e=game();e.elapsed=239.995;e.boss.hp=RULES.attackDamage;
    e.player.x=350;e.player.attack=.2;if(mutual)e.player.hp=0;
    e.needles=[{id:1,x:800,age:.5,speed:1,hit:false,landed:false}];e.update(.01);
    assert.equal(e.status,'victory');assert.equal(e.needles.length,0);assert.equal(e.pattern,null);
    assert.equal(e.events.filter(v=>v.type==='victory').length,1);assert.equal(e.events.filter(v=>v.type==='defeat').length,0);
  }
  const e=game();e.elapsed=239.995;e.update(.01);assert.equal(e.status,'defeat');assert.equal(e.events.at(-1).timeout,true);
});

test('ending a fight prevents further damage and duplicate terminal events',()=>{
  const e=game();e.boss.hp=0;e.update(.01);const hp=e.player.hp,events=e.events.length;
  assert.equal(e.damagePlayer(25),false);e.damageBoss(999);advance(e,2);
  assert.equal(e.player.hp,hp);assert.equal(e.events.length,events);assert.ok(e.endAge>1.9);
});

test('automatic fist side avoids trapping either wall while explicit authored sides remain exact',()=>{
  const centerSides=new Set();
  for(let seed=1;seed<=40;seed++){
    for(const [x,expected] of [[111,'right'],[454,'right'],[506,'left'],[849,'left']]){
      const e=game({seed});e.player.x=x;e.startPattern('fist');assert.equal(e.pattern.side,expected);
      e.startPattern('fist',expected==='right'?'left':'right');assert.notEqual(e.pattern.side,expected);
    }
    const e=game({seed:seed*7919});e.player.x=480;e.startPattern('fist');centerSides.add(e.pattern.side);
  }
  assert.deepEqual([...centerSides].sort(),['left','right']);
});

test('returning small fists move continuously and hurtboxes use their exact rendered offsets',()=>{
  for(const side of ['left','right']){
    const e=game();e.forcePattern('fist',side);let previous=null;
    for(let i=0;i<=46;i++){
      e.pattern.age=2.1+i*.01;const offset=e.sideOffset(side),boxes=e.bossHurtboxes(),box=boxes[side==='left'?1:2];
      near(box.x,(side==='left'?320:554)+offset.x);near(box.y,293+offset.y);
      if(previous!==null){assert.ok(Math.abs(offset.x)<=Math.abs(previous)+1e-7);assert.ok(Math.abs(offset.x-previous)<11);}
      previous=offset.x;
      const other=e.sideOffset(side==='left'?'right':'left');near(other.x,0);
    }
    near(previous,0);
  }
});

test('practice repeats its selected attack while normal selection remains independent',()=>{
  for(const type of ['fist','needle','mixer']){
    const e=game({practice:type});e.nextIn=0;e.player.invuln=999;
    // Mixer bypasses normal invulnerability, so keep its safe edge between ticks.
    for(let i=0;i<4200;i++){
      if(e.pattern?.type==='mixer')e.player.x=e.pattern.side==='left'?849:111;
      e.update(.01);
    }
    assert.equal(e.status,'fight');assert.ok(e.log.length>=3);assert.ok(e.log.every(p=>p.type===type));
    e.reset('play',type);assert.equal(e.practice,type);assert.equal(e.mode,'play');assert.equal(e.status,'intro');
  }
});

test('ordinary attacks can win through the shared-health combat path',()=>{
  const e=game();e.player.x=350;e.player.face=1;
  for(let i=0;i<3000&&e.status==='fight';i++)e.update(.01,{attack:true});
  assert.equal(e.status,'victory');assert.equal(e.boss.hp,0);assert.equal(e.stats.hits,40);
  assert.equal(e.events.filter(v=>v.type==='victory').length,1);
});

test('four ordinary fist hits cause defeat, then reset removes terminal and pending state',()=>{
  const e=game();
  for(let i=0;i<4;i++){
    e.player.x=400;e.forcePattern('fist','left');e.pattern.age=1.2;e.update(.01);
    if(i<3){advance(e,.85);assert.equal(e.status,'fight');}
  }
  assert.equal(e.status,'defeat');assert.equal(e.player.hp,0);assert.equal(e.stats.taken,100);
  assert.equal(e.needles.length,0);assert.equal(e.pattern,null);assert.equal(e.events.filter(v=>v.type==='defeat').length,1);
  e.reset('play');assert.equal(e.status,'intro');assert.equal(e.player.hp,100);assert.equal(e.boss.hp,RULES.bossHp);
  assert.equal(e.endAge,0);assert.equal(e.events.length,0);assert.equal(e.hitstop,0);assert.equal(e.paused,false);
});

for(const r of results)console.log(`${r.pass?'PASS':'FAIL'} ${r.name}${r.error?'\n     '+r.error:''}`);
const failed=results.filter(r=>!r.pass);console.log(`\n${results.length-failed.length}/${results.length} behavioral checks passed.`);
if(failed.length)process.exitCode=1;
