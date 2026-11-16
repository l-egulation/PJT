// Use render time so the flash also fades when a hit ends the fight.
export function createDamageFlash() {
  let previousHits = 0;
  let startedAt = -Infinity;
  const duration = 360;
  return (ctx, hits, now = performance.now()) => {
    if (hits < previousHits) startedAt = -Infinity;
    if (hits > previousHits) startedAt = now;
    previousHits = hits;
    const remaining = 1 - (now - startedAt) / duration;
    if (remaining <= 0) return;

    const { width, height } = ctx.canvas;
    const depth = Math.round(Math.min(width, height) * .13);
    const red = `rgba(245, 24, 40, ${.84 * remaining})`;
    const clear = 'rgba(245, 24, 40, 0)';
    ctx.save();
    for (const [x, y, dx, dy, rx, ry, w, h] of [
      [0, 0, depth, 0, 0, 0, depth, height],
      [width, 0, width - depth, 0, width - depth, 0, depth, height],
      [0, 0, 0, depth, 0, 0, width, depth],
      [0, height, 0, height - depth, 0, height - depth, width, depth],
    ]) {
      const edge = ctx.createLinearGradient(x, y, dx, dy);
      edge.addColorStop(0, red);
      edge.addColorStop(1, clear);
      ctx.fillStyle = edge;
      ctx.fillRect(rx, ry, w, h);
    }
    ctx.restore();
  };
}
