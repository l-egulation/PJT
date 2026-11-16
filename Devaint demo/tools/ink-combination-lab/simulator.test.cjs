'use strict';
const test=require('node:test'),assert=require('node:assert/strict');
const catalog=require('../../data/combat/ink_simulator_parts_proposal.json');
const experiment=require('../../data/combat/ink_simulator_proposal.json');
const {Simulator}=require('./simulator.js');
const id=(color,n)=>`ink2_${color}_${String(n).padStart(2,'0')}`;
function make(loadout=[[],[]],options={}){
 return new Simulator(catalog,experiment,{loadout,scenario:'single',enemyAI:false,internalCooldown:0,chainLimit:2,...options});
}
function ready(s){s.advance(5);}
test('all outgoing effects resolve before the incoming auto attack; incoming switch parts do not fire',()=>{
 const s=make([[id('red',5),id('green',4)],[id('red',4)]]);
 assert.equal(s.switchWeapon(1),true);
 const sw=s.events.findIndex(e=>e.type==='switch');
 const action=s.events.findIndex(e=>e.type==='attack'&&e.owner===1);
 assert.ok(sw>0&&action>sw);
 for(const p of [id('red',5),id('green',4)]){
  const e=s.events.findIndex(e=>e.type==='proc'&&e.part===p);
  assert.ok(e>=0&&e<sw);
  assert.equal(s.events[e].active,0);
 }
 assert.equal(s.stats.procs[id('red',4)]||0,0);
 assert.equal(s.active,1);assert.equal(s.shieldAmount(),12);
});
test('extra hits trigger hit and kill rewards, with a visible chain limit',()=>{
 const s=make([[id('red',1),id('green',1),id('green',9)],[]]);
 s.player.hp=50;s.enemies[0].hp=11;
 s.attack('basic');
 assert.equal(s.stats.kills,1);assert.ok(s.stats.procs[id('green',1)]>=2);
 assert.equal(s.stats.procs[id('green',9)],1);
 const chain=make([[id('red',1)],[]],{chainLimit:2});chain.attack('basic');
 assert.ok(chain.stats.chainLimited>0);assert.ok(chain.events.filter(e=>e.type==='hit').length<=3);
});
test('marks detonate only on a switched-weapon hit, before the new mark is added',()=>{
 const s=make([[],[id('red',7)]],{markMode:'hit'});
 s.attack('basic');assert.equal(s.enemies[0].marks.length,1);
 ready(s);s.attack('basic');assert.equal(s.stats.detonations,0);
 ready(s);s.switchWeapon(1);assert.equal(s.stats.detonations,1);
 assert.equal(s.stats.procs[id('red',7)],1);
 assert.ok(s.enemies[0].marks.every(m=>m.owner===1));
 ready(s);s.attack('basic');assert.equal(s.stats.detonations,1);
});
test('manual mark mode does not silently introduce an on-hit mark rule',()=>{
 const s=make([[],[]],{markMode:'manual'});s.attack('basic');
 assert.equal(s.enemies[0].marks.length,0);
 s.addMarks(s.enemies[0].id,2);ready(s);s.switchWeapon(1);
 assert.equal(s.stats.detonations,1);
});
test('blocked actions and misses do not grant hit rewards; one-shot buffs are consumed on a miss',()=>{
 const s=make([[id('red',4),id('blue',4)],[id('green',1)]]);
 s.player.hp=50;s.enemies[0].x=15;
 s.switchWeapon(1);assert.equal(s.stats.hits,0);assert.equal(s.player.hp,50);
 assert.equal(s.buffs.filter(b=>b.once).length,0);
 const count=s.events.length;assert.equal(s.switchWeapon(0),false);
 assert.equal(s.events.length,count);
});
test('cooldown reductions affect the intended weapon and do not bank unused cooldown',()=>{
 const s=make([[id('blue',5)],[]]);s.cooldowns[1]=5;s.switchWeapon(1);
 assert.equal(s.cooldowns[1],4);assert.equal(s.cooldowns[0],0);
 const hit=make([[id('blue',2)],[]]);hit.cooldowns[0]=2;hit.attack('basic');
 assert.ok(Math.abs(hit.cooldowns[0]-1.7)<1e-9);
});
test('range effects change actual target selection, not just the displayed number',()=>{
 const plain=make();plain.enemies[0].x=plain.player.x+2.6;plain.attack('basic');assert.equal(plain.stats.hits,0);
 const expanded=make([[id('blue',4)],[]]);expanded.enemies[0].x=expanded.player.x+2.6;
 expanded.switchWeapon(1);assert.ok(expanded.stats.hits>0);
});
test('temporary max health and shields expire; no passive damage appears while waiting',()=>{
 const s=make([[id('green',4),id('green',6)],[]]);s.switchWeapon(1);
 assert.equal(s.maxHealth(),115);s.player.hp=115;
 const damage=s.stats.damage;s.advance(7);
 assert.equal(s.maxHealth(),100);assert.equal(s.player.hp,100);assert.equal(s.shieldAmount(),0);
 assert.equal(s.stats.damage,damage);
});
test('delayed hit keeps the outgoing weapon attribution and never transfers to a respawned life',()=>{
 const s=make([[id('red',6)],[]]);s.lastTargets[0]=s.enemies[0].id;s.switchWeapon(1);s.advance(.5);
 assert.ok(s.events.some(e=>e.type==='hit'&&e.owner===0&&e.part===id('red',6)));
 const dead=make([[id('red',6)],[]]);dead.lastTargets[0]=dead.enemies[0].id;dead.switchWeapon(1);
 dead.enemies[0].hp=0;dead.enemies[0].dead=true;dead.advance(.5);
 assert.ok(!dead.events.some(e=>e.type==='hit'&&e.part===id('red',6)));
});
test('CC changes movement and attack behavior and respects the experimental boss duration scale',()=>{
 const slow=make([[id('purple',1)],[]],{enemyAI:true});slow.attack('basic');assert.equal(slow.enemySpeed(slow.enemies[0]),.8);
 const root=make([[],[id('purple',7)]],{markMode:'manual',enemyAI:true});root.addMarks(root.enemies[0].id,1);root.switchWeapon(1);
 const x=root.enemies[0].x;root.advance(.25);assert.equal(root.enemies[0].x,x);
 const boss=make([[id('purple',3)],[]],{scenario:'boss',bossCcScale:.25});boss.attack('basic');
 assert.ok(Math.abs(boss.enemies[0].status.stun.until-.05)<1e-9);
});
test('all 50 effects have executable handlers and observable results',()=>{
 for(const p of catalog.parts){
  const s=make([[p.id],[]],{scenario:'pack',markMode:'manual'});
  s.player.hp=40;s.cooldowns=[5,5];s.lastTargets[0]=s.enemies[0].id;
  if(p.effect.kind==='conditional_hit')s.enemies[0].hp=20;
  const ctx=s.context(0,s.enemies[0],{damage:10,depth:0});
  if(p.trigger==='kill'){s.enemies[0].dead=true;s.enemies[0].hp=0;}
  s.trigger(p.trigger,ctx);s.flush();s.advance(.5);
  assert.ok(s.stats.procs[p.id]>=1,p.id);
  assert.ok(s.events.some(e=>e.type==='effect'&&e.part===p.id),p.id+' must execute');
  assert.ok(!s.events.some(e=>e.type==='unsupported'),p.id);
 }
});
test('kill-granted next-attack power is consumed exactly once, including global buffs',()=>{
 const s=make([[id('red',10)],[]]);s.enemies[0].hp=1;s.attack('basic');
 ready(s);s.options.respawn=true;s.enemies[0].respawnAt=s.time;s.advance(.02);
 s.attack('basic');assert.equal(s.events.filter(e=>e.type==='attack').at(-1).damage,12.5);
 ready(s);s.attack('basic');assert.equal(s.events.filter(e=>e.type==='attack').at(-1).damage,10);
});
test('root does not make a target immune to slow, silence, or blind',()=>{
 const s=make([[id('purple',1)],[]],{ccRecovery:1});
 const e=s.enemies[0],ctx=s.context(0,e);
 s.control(e,'root',1,ctx);s.trigger('hit',ctx);
 assert.ok(s.hasStatus(e,'slow'));
 s.control(e,'silence',1,ctx);s.control(e,'blind',1,ctx);
 assert.ok(s.hasStatus(e,'silence'));assert.ok(s.hasStatus(e,'blind'));
});
test('a pending extra hit cannot resurrect a dead player through healing',()=>{
 const s=make([[id('red',6),id('green',1)],[]]);s.lastTargets[0]=s.enemies[0].id;
 s.switchWeapon(1);s.player.hp=0;s.advance(.6);assert.equal(s.player.hp,0);
});
test('directional movement bonuses and stack limits change actual player speed',()=>{
 const toward=make([[id('yellow',3)],[]]);toward.attack('basic');
 assert.equal(toward.moveSpeed({x:1,y:0}),3.75);assert.equal(toward.moveSpeed({x:-1,y:0}),3);
 const stack=make([[id('yellow',2)],[]]);for(let i=0;i<8;i++)stack.trigger('hit',stack.context(0,stack.enemies[0]));
 assert.ok(Math.abs(stack.moveSpeed()-3.6)<1e-9);
 const x=stack.player.x;stack.advance(.5,{x:1,y:0});assert.ok(Math.abs(stack.player.x-x-1.8)<1e-9);
});
test('shield caps, actual-damage lifesteal, and skill-only radius consumption are applied',()=>{
 const shield=make([[id('green',2)],[]]);for(let i=0;i<8;i++)shield.trigger('hit',shield.context(0,shield.enemies[0]));
 assert.equal(shield.shieldAmount(),15);shield.takeDamage(20);assert.equal(shield.player.hp,95);assert.equal(shield.stats.blocked,15);
 const leech=make([[id('green',3)],[]]);leech.player.hp=50;leech.enemies[0].hp=1;leech.attack('basic');assert.equal(leech.player.hp,50.05);
 const radius=make([[id('blue',6)],[]]);radius.switchWeapon(1);assert.equal(radius.modifier('radius',1,false),0);assert.equal(radius.modifier('radius',1,true),.35);
 ready(radius);radius.attack('skill');assert.equal(radius.modifier('radius',1,true),0);
});
test('CC suppresses the correct enemy actions and blind preserves an already aimed attack',()=>{
 for(const [type,blocked]of [['disarm','basic'],['silence','skill'],['stun','skill']]){
  const s=make();const e=s.enemies[0];e.intent={kind:blocked,at:0,aim:{...s.player},damage:10};
  s.control(e,type,1,s.context(0,e));s.enemyStep(e,.01);assert.equal(s.player.hp,100,type);
 }
 const s=make(),e=s.enemies[0];s.control(e,'blind',1,s.context(0,e));e.nextAttack=0;e.nextSkill=0;s.enemyStep(e,.01);assert.equal(e.intent,null);
 e.intent={kind:'skill',at:0,aim:{...s.player},damage:10};s.enemyStep(e,.01);assert.equal(s.player.hp,90);
});
test('refreshing directional bonuses updates the latest target, origin, and facing',()=>{
 const s=make([[id('yellow',3),id('yellow',6),id('yellow',8)],[]],{scenario:'pack',autoAim:false});
 s.enemies[0].x=4;s.enemies[0].y=4;s.enemies[1].x=2;s.enemies[1].y=6;
 s.trigger('hit',s.context(0,s.enemies[0]));s.trigger('hit',s.context(0,s.enemies[1]));
 assert.equal(s.moveSpeed({x:1,y:0}),3);assert.equal(s.moveSpeed({x:0,y:1}),3.75);
 s.player.facing={x:1,y:0};s.trigger('switch',s.context(0,null,{incoming:1}));
 s.player.facing={x:0,y:1};s.trigger('switch',s.context(0,null,{incoming:1}));
 assert.ok(s.moveSpeed({x:0,y:-1})>3);assert.equal(s.moveSpeed({x:-1,y:0}),3);
 s.trigger('detonate',s.context(0,s.enemies[0]));s.trigger('detonate',s.context(0,s.enemies[1]));
 const away=s.buffs.find(b=>b.direction==='away');assert.deepEqual(away.anchor,{x:2,y:6});
});
test('forward fear and silence stay inside the specified radial distance',()=>{
 const s=make([[id('purple',6)],[]],{autoAim:false});
 s.enemies[0].x=5;s.enemies[0].y=7;s.trigger('switch',s.context(0,s.enemies[0],{incoming:1}));
 assert.equal(s.hasStatus(s.enemies[0],'silence'),false);
 s.enemies[0].x=4;s.enemies[0].y=5;s.trigger('switch',s.context(0,s.enemies[0],{incoming:1}));
 assert.equal(s.hasStatus(s.enemies[0],'silence'),true);
});
test('state is bounded, reset removes buffs, and a dead player cannot trigger a switch',()=>{
 const s=make([[id('green',4)],[]]);s.player.hp=0;
 assert.equal(s.switchWeapon(1),false);assert.equal(s.active,0);
 s.reset();assert.equal(s.active,0);assert.equal(s.player.hp,100);assert.equal(s.shieldAmount(),0);
 assert.throws(()=>make([[id('red',1),'missing'],[]]));
 assert.throws(()=>make([Array(7).fill(id('red',1)),[]]));
});
