'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { create, receive, expire, dismiss, resolveBurst } = require('./shield_decoy_retaliation.cjs');
const { retaliation } = require('../../data/combat/shield_decoy_retaliation.json');
const melee = require('../../data/combat/melee_weapon_design.json');
const skill = melee.forms.find(f => f.id === 'shield').specials.find(s => s.id === 'shield_follow_up');
const make = () => create(skill.independent_effect.hp, retaliation);
test('selected tuning gives four ten-damage attackers one full wave without breaking', () => {
  let state = make();
  assert.equal(state.health, 60);
  for (let i=0; i<4; i++) { const hit = receive(state, 10); state = hit.state; assert.equal(hit.burst, null); }
  assert.equal(state.health, 20);
  assert.equal(skill.independent_effect.duration, 3);
  assert.equal(skill.independent_effect.taunt_radius, 5);
  assert.equal(skill.cooldown, 10);
});
test('destruction releases fixed twenty exactly once; overkill does not increase it', () => {
  const damaged = receive(make(), 10).state;
  const lethal = receive(damaged, 1000);
  assert.equal(lethal.healthLost, 50);
  assert.equal(lethal.burst.damage, 20);
  assert.equal(receive(lethal.state, 1000).burst, null);
  assert.equal(expire(lethal.state).burst, null);
});
test('unhit or partially damaged decoy expires with the same fixed burst', () => {
  const fresh = expire(make());
  assert.equal(fresh.burst.damage, 20);
  assert.equal(expire(fresh.state).burst, null);
  assert.equal(expire(receive(make(), 30).state).burst.damage, 20);
});
test('replacement never detonates and cannot later expire or take damage', () => {
  const removed = dismiss(receive(make(), 30).state);
  assert.equal(removed.burst, null);
  assert.equal(expire(removed.state).burst, null);
  assert.equal(receive(removed.state, 1000).burst, null);
});
test('one hit per living enemy within three units; bosses can receive damage', () => {
  const burst = expire(make()).burst;
  assert.equal(burst.radiusUnits, 3);
  const targets = [
    { id: 1, enemy: true, x: 1, y: 0 },
    { id: 1, enemy: true, x: 1, y: 0 },
    { id: 2, enemy: true, boss: true, x: 3, y: 0 },
    { id: 3, enemy: true, x: 3.01, y: 0 },
    { id: 4, enemy: false, x: 0, y: 0 },
    { id: 5, enemy: true, dead: true, x: 0, y: 0 }
  ];
  assert.deepEqual(resolveBurst(burst, targets, { x: 0, y: 0 }), [
    { id: 1, damage: 20 }, { id: 2, damage: 20 }
  ]);
});
test('invalid damage cannot corrupt the preview state', () => {
  const state = make();
  for (const value of [-1, NaN, Infinity]) assert.throws(() => receive(state, value));
  assert.equal(state.health, 60);
});
