import test from 'node:test';
import assert from 'node:assert/strict';
import {rules,platforms,nearestLayer,fallIndex,trapState,rainTicks,chooseTraps} from '../monsters/stage-02/boss-siblings-lineup-20261103/phase1-playtest/rules.mjs';
test('machine uses the supplied phase-one dimensions and HP',()=>{
 assert.equal(rules.deviceHP,1500);assert.equal(rules.deviceW,64);assert.equal(rules.deviceH,96);
 assert.equal(platforms.length,7);assert.deepEqual([0,1,2].map(l=>platforms.filter(p=>p.layer===l).length),[2,3,2]);
});
test('Rose selects the nearest layer, including below all platforms',()=>{
 assert.equal(nearestLayer(900),0);assert.equal(nearestLayer(280),1);assert.equal(nearestLayer(120),2);
});
test('all seven respawn bins are selectable without safety filtering',()=>{
 assert.deepEqual(Array.from({length:7},(_,i)=>fallIndex(()=>i/7+.001)),[0,1,2,3,4,5,6]);
});
test('trap changes at 1.5 and 3.5, not on sniper impact',()=>{
 assert.equal(trapState(1.499),'warning');assert.equal(trapState(1.5),'active');assert.equal(trapState(3.5),'off');assert.equal(rules.rootSeconds,2);
});
test('rain deals exactly two scheduled ticks; no third tick at end',()=>{
 assert.deepEqual(rainTicks(1.9,3),[2,2.5]);assert.deepEqual(rainTicks(2,2.49),[]);assert.deepEqual(rainTicks(2.49,2.5),[2.5]);assert.deepEqual(rainTicks(2.5,3),[]);
});
test('five distinct traps leave the occupied/reachable nearest platform safe',()=>{
 for(const p of platforms){const plan=chooseTraps({x:p.x+p.w/2,y:p.y},()=>.37);assert.equal(new Set(plan.selected).size,5);assert.ok(!plan.selected.includes(plan.safe));assert.equal(platforms[plan.safe],p);}
});
