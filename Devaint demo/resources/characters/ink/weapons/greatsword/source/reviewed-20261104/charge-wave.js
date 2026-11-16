(function (root) {
  'use strict';

  const ground = 687;
  const ink = '#1b1616';
  const shade = '#302a2b';
  const rim = '#423b3c';
  const stages = [
    { x: 1320, start: 0,   grow: 60, width: 185, height: 116 },
    { x: 1455, start: 50,  grow: 65, width: 228, height: 218 },
    { x: 1620, start: 110, grow: 75, width: 294, height: 375 },
    { x: 1820, start: 170, grow: 90, width: 384, height: 560 }
  ];
  const peak = Math.max(...stages.map(s => s.start + s.grow));
  const hold = 1000;
  const fade = 520;
  const end = peak + hold + fade;
  const clamp = x => Math.max(0, Math.min(1, x));
  const expand = x => 1 - (1 - clamp(x)) ** 3;
  const snap = x => Math.round(x / 3) * 3;

  // Each dark mass has a wide, irregular head and a short taper back to contact.
  const lobes = [
    { start: .08, reach: .76, lift: .54, depth: .35, lag: .07 },
    { start: 0,   reach: .68, lift: .92, depth: .54, lag: 0 },
    { start: .17, reach: .91, lift: .66, depth: .37, lag: .13 },
    { start: .18, reach: 1,   lift: .24, depth: .29, lag: .22 }
  ];
  const outline = [[0,0],[.20,-.21],[.34,-.37],[.47,-.36],[.51,-.51],
                   [.68,-.55],[.78,-.46],[.91,-.55],[1,-.34],[1.04,-.10],
                   [.97,.08],[.93,.20],[.86,.21],[.81,.38],[.65,.45],
                   [.48,.38],[.39,.26],[.21,.23]];
  const face = [[.09,.02],[.40,-.26],[.62,-.47],[.72,-.39],[.92,-.24],
                [.93,-.11],[.76,-.06],[.62,.14],[.35,.17]];
  const highlight = [[.32,.03],[.61,-.29],[.82,-.27],[.70,-.13],[.57,-.03]];

  function geometry(time) {
    const shapes = [];
    const add = (color, points) => shapes.push({ color, points: points.map(p => p.map(snap)) });
    for (const [index, stage] of stages.entries()) {
      if (time <= stage.start) continue;
      const age = clamp((time - stage.start) / stage.grow);
      const grow = expand(age);
      const w = stage.width * grow;
      const h = stage.height * grow;
      const x = stage.x - w * .22;

      add(ink, [[x, ground],[x+w*.26,ground-h*.10],[x+w*.59,ground-h*.11],
                [x+w*.90,ground-h*.06],[x+w,ground-3],[x+w*.79,ground],
                [x+w*.42,ground-3]]);
      for (const [lobeIndex, lobe] of lobes.entries()) {
        const open = expand((age - lobe.lag) / (1 - lobe.lag));
        if (open === 0) continue;
        const reach = w * lobe.reach * (.58 + .42 * open);
        const lift = h * lobe.lift * open;
        const depth = h * lobe.depth * open;
        const base = x + w * lobe.start;
        const bend = ((index + lobeIndex) % 3 - 1) * .11;
        const place = ([u,v]) => [base + u * reach, Math.min(ground, ground - u * lift + (v + bend * u * u) * depth)];
        const silhouette = outline.map(([u,v], point) => {
          const chip = ((point * 7 + index * 3 + lobeIndex * 5) % 11 - 5) * .012;
          return place([u, v + chip * Math.min(1, u * 3)]);
        });
        add(ink, silhouette);
        add(shade, face.map(place));
        add(rim, highlight.map(place));
      }

    }
    return shapes;
  }

  const debris = [
    { delay: 6,  offset: .38, vx: 760, vy: 1030, lift: .55, size: 34, angle: -.86 },
    { delay: 12, offset: .62, vx: 1190, vy: 620, lift: .24, size: 35, angle: -.35 },
    { delay: 17, offset: .40, vx: 930, vy: 1480, lift: .69, size: 30, angle: -1.06 },
    { delay: 24, offset: .74, vx: 1350, vy: 340, lift: .08, size: 24, angle: -.28 },
    { delay: 34, offset: .60, vx: 1040, vy: 950, lift: .45, size: 25, angle: -.66 }
  ];

  function impacts(time) {
    const shapes = [];
    const add = (color, alpha, points) => shapes.push({color, alpha, points: points.map(p => p.map(snap))});
    for (const [index, stage] of stages.entries()) {
      const age = time - stage.start;
      if (age < 0 || age >= 300) continue;
      if (age < 64) {
        const travel = expand((age + 4) / 64);
        const x = stage.x + stage.width * travel * .66;
        const length = stage.width * (.34 + travel * .38);
        const strength = 1 - age / 64;
        add(ink, strength, [[x-length*.68,ground],[x-length*.12,ground-12],
                            [x+length,ground-6],[x+length*.52,ground]]);
        add('#c2b5ab', strength * .95, [[x-length*.45,ground-4],[x+length*.23,ground-13],
                                       [x+length*.8,ground-7],[x+length*.22,ground-5]]);
        add(rim, strength, [[x-length*.18,ground-17],[x+length*.28,ground-27],
                            [x+length*.77,ground-23],[x+length*.32,ground-19]]);
      }

      for (const [j, chip] of debris.entries()) {
        const elapsed = age - chip.delay;
        const life = 195 + j * 12;
        if (elapsed < 0 || elapsed >= life) continue;
        const seconds = elapsed / 1000;
        const scale = .79 + index * .13;
        const x = stage.x + stage.width * chip.offset + chip.vx * scale * seconds;
        const launchLift = stage.height * chip.lift * expand(elapsed / 90);
        const y = ground - 10 - launchLift - chip.vy * scale * seconds + 2600 * seconds ** 2;
        if (y >= ground) continue;
        const opacity = 1 - clamp((elapsed - life * .54) / (life * .46));
        const size = chip.size * scale;
        const angle = chip.angle + (j % 2 ? 1 : -1) * seconds * 2.8;
        const cos = Math.cos(angle), sin = Math.sin(angle);
        const place = ([u,v]) => [x + size * (u*cos - v*sin), Math.min(ground, y + size * (u*sin + v*cos))];
        if (elapsed < 65) {
          add(shade, opacity * (1 - elapsed / 65) * .8,
              [[-2.3,0],[-.25,-.10],[.19,.05],[-.35,.12]].map(place));
        }
        add(ink, opacity, [[-.64,.04],[-.30,-.20],[.39,-.14],[.64,-.01],[.22,.20]].map(place));
        add(rim, opacity, [[-.31,-.10],[.38,-.11],[.52,-.02],[-.01,.02]].map(place));
      }
    }
    return shapes;
  }

  const crest = geometry(peak);
  function state(time) {
    if (time < 0 || time >= end) return null;
    return {
      shapes: time >= peak ? crest : geometry(time),
      opacity: 1 - clamp((time - peak - hold) / fade),
      accents: impacts(time)
    };
  }

  let overlay;
  function paint(context, shapes) {
    for (const { color, points, alpha = 1 } of shapes) {
      context.globalAlpha = alpha;
      context.fillStyle = color;
      context.beginPath();
      context.moveTo(...points[0]);
      for (const point of points.slice(1)) context.lineTo(...point);
      context.closePath();
      context.fill();
    }
    context.globalAlpha = 1;
  }

  function draw(context, time) {
    const sample = state(time);
    if (!sample) return;
    if (sample.shapes !== crest) {
      paint(context, sample.shapes);
    } else {
      if (!overlay) {
        overlay = document.createElement('canvas');
        overlay.width = context.canvas.width;
        overlay.height = context.canvas.height;
        paint(overlay.getContext('2d'), crest);
      }
      context.globalAlpha = sample.opacity;
      context.drawImage(overlay, 0, 0);
    }
    paint(context, sample.accents);
  }

  const api = { state, draw, ground, peak, hold, fade, end };
  if (typeof module === 'undefined') root.Shockwave = api;
  else module.exports = api;
})(globalThis);
