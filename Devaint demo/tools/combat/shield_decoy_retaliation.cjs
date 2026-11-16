// Browser preview rules. Unity integration remains explicitly separate in the source definition.
(function (root) {
  'use strict';
  function finite(value, name, positive = false) {
    if (!Number.isFinite(value) || (positive ? value <= 0 : value < 0)) throw new Error('Invalid ' + name);
    return value;
  }
  function create(maxHealth, rules) {
    finite(maxHealth, 'maxHealth', true);
    finite(rules.fixedDamage, 'fixedDamage', true);
    finite(rules.radiusUnits, 'radiusUnits', true);
    return { maxHealth, health: maxHealth, active: true,
      rules: Object.freeze({ ...rules }) };
  }
  function receive(state, damage) {
    finite(damage, 'damage');
    if (!state.active) return { state, healthLost: 0, burst: null };
    const healthLost = Math.min(state.health, damage);
    const health = state.health - healthLost;
    const next = { ...state, health };
    if (health === 0) return { ...release(next), healthLost };
    return { state: next, healthLost, burst: null };
  }
  function release(state) {
    if (!state.active) return { state, healthLost: 0, burst: null };
    return { state: { ...state, active: false }, healthLost: 0,
      burst: Object.freeze({ damage: state.rules.fixedDamage,
        radiusUnits: state.rules.radiusUnits, hitsPerTarget: 1 }) };
  }
  function expire(state) {
    return release(state);
  }
  function dismiss(state) {
    return { state: { ...state, active: false }, healthLost: 0, burst: null };
  }
  function resolveBurst(burst, targets, origin) {
    if (!burst) return [];
    const seen = new Set(), hits = [];
    for (const target of targets) {
      if (!target.enemy || target.dead || seen.has(target.id)) continue;
      if (Math.hypot(target.x - origin.x, target.y - origin.y) > burst.radiusUnits) continue;
      seen.add(target.id);
      hits.push({ id: target.id, damage: burst.damage });
    }
    return hits;
  }
  const api = Object.freeze({ create, receive, expire, dismiss, resolveBurst });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.DecoyRetaliation = api;
})(globalThis);
