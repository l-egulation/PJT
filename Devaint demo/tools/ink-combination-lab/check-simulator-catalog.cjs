'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const read = p => JSON.parse(fs.readFileSync(path.join(root, p), 'utf8'));
const catalog = read('data/combat/ink_simulator_parts_proposal.json');
assert.equal(catalog.schema, 2, 'new trigger/effect schema required');
assert.equal(catalog.parts.length, 50, '50 new parts required');
assert.equal(new Set(catalog.parts.map(p => p.id)).size, 50, 'unique IDs required');
assert.equal(new Set(catalog.parts.map(p => p.name)).size, 50, 'unique names required');
const roles = {red:'attack', yellow:'move_speed', green:'life', blue:'range_cooldown', purple:'crowd_control'};
const triggers = ['hit', 'switch', 'detonate', 'kill'];
for (const [color, role] of Object.entries(roles)) {
  const parts = catalog.parts.filter(p => p.color === color);
  assert.equal(parts.length, 10, `${color}: 10 parts required`);
  for (const [trigger, count] of [['hit',3], ['switch',3], ['detonate',2], ['kill',2]])
    assert.equal(parts.filter(p => p.trigger === trigger).length, count, `${color}/${trigger}`);
  for (const p of parts) assert.equal(p.effect.role, role, `${p.id}: color role mismatch`);
}
for (const p of catalog.parts) {
  assert.match(p.id, /^ink2_(red|yellow|green|blue|purple)_\d{2}$/);
  assert.ok(triggers.includes(p.trigger), p.id);
  for (const key of ['name','goal','situation','reason','effect','cost','limits','combo']) assert.ok(p[key], `${p.id}: ${key}`);
  assert.ok(p.effect.description && p.effect.target && p.effect.kind, `${p.id}: incomplete effect`);
  assert.ok(['equipped_weapon','incoming_weapon','both_weapons'].includes(p.effect.weaponScope),p.id+' missing ownership');
  if(p.effect.weaponScope!=='equipped_weapon')assert.ok(p.effect.crossWeaponException,p.id+' needs explicit exception');
  assert.ok(p.effect.params&&Object.keys(p.effect.params).length, `${p.id}: structured simulator parameters required`);
  for(const value of Object.values(p.effect.params))if(typeof value==='number')assert.ok(Number.isFinite(value)&&value>=0,`${p.id}: invalid number`);
  assert.ok(!/지속 피해|화상/.test(p.effect.description), `${p.id}: damage over time excluded`);
  if (p.trigger === 'switch') assert.equal(p.source, 'outgoing_weapon');
  if (p.trigger === 'detonate') assert.equal(p.requires, 'mark_system_pending');
}
assert.equal(catalog.rules.extraHitsTriggerHitAndKill, true);
assert.equal(catalog.rules.damageOverTime, false);
assert.deepEqual(catalog.rules.switchOrder, ['outgoing_effects','switch_weapon','incoming_auto_attack']);
assert.equal(catalog.rules.markSource,'primary_hit');assert.equal(catalog.rules.marksPerHit,1);assert.equal(catalog.rules.extraHitsApplyMarks,false);assert.equal(catalog.rules.markConsumption,'all');assert.equal(catalog.rules.markCap,5);assert.equal(catalog.rules.markDuration,8);assert.equal(catalog.rules.inkEffectProcsPerDetonation,1);assert.equal(catalog.rules.markIntrinsicDamage.coefficientPerMark,null);
const ids = new Set(catalog.parts.map(p => p.id));
const mvp=catalog.mvp;assert.equal(mvp.status,'selected_for_mvp_trial');assert.equal(mvp.tuningStatus,'not_approved');assert.equal(mvp.partIds.length,20);assert.equal(new Set(mvp.partIds).size,20);
for(const id of mvp.partIds)assert.ok(ids.has(id),'Unknown MVP part: '+id);
for(const color of Object.keys(roles)){const chosen=catalog.parts.filter(p=>p.color===color&&mvp.partIds.includes(p.id));assert.equal(chosen.length,4,color+' MVP count');assert.deepEqual(chosen.map(p=>p.trigger).sort(),[...triggers].sort(),color+' MVP triggers');}
for (const build of read('data/combat/ink_simulator_builds_proposal.json').builds)
  for (const side of build.build) for (const id of side) assert.ok(ids.has(id), `stale build reference: ${id}`);
const {generatePreview} = require('./generate-simulator.cjs');
const html = generatePreview();
const cards = [...html.matchAll(/<article data-color="(\w+)" data-trigger="(\w+)">([\s\S]*?)<\/article>/g)].map(m=>({dataset:{color:m[1],trigger:m[2]},textContent:m[3].replace(/<[^>]*>/g,''),hidden:false}));
assert.equal(cards.length,50,'preview must render all parts');
const elements={color:{value:'all'},trigger:{value:'all'},query:{value:''},count:{textContent:''},empty:{hidden:true}};
for(const key of ['color','trigger','query'])elements[key].addEventListener=()=>{};
const vm=require('node:vm'),context=vm.createContext({document:{getElementById:id=>elements[id],querySelectorAll:()=>cards}});
vm.runInContext(html.match(/<script>([\s\S]*?)<\/script>/)[1],context);
function filter(color,trigger,query,count){
 elements.color.value=color;elements.trigger.value=trigger;elements.query.value=query;
 vm.runInContext('filter()',context);
 assert.equal(cards.filter(c=>!c.hidden).length,count,`${color}/${trigger}/${query}`);
 assert.equal(elements.empty.hidden,count>0);
}
filter('all','all','',50);filter('red','hit','',3);filter('blue','switch','',3);
filter('all','detonate','',10);filter('all','all','없는파츠검색어',0);
filter('all','all',catalog.parts[0].name,1);filter('all','all','',50);
console.log('PASS: 50 parts, color roles, four triggers, switch order, no DOT, build references, and preview filtering');
