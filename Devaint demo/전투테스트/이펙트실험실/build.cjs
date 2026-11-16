// 잉크 파츠 이펙트 실험실 빌드: 파츠 20종 아이콘·설명, pixelfx 이펙트 시트, 손맛 테스트와 같은 1Stage 무대(주인공·몬스터·배경)를
// 단독 HTML 한 장(이펙트실험실.html)으로 묶는다. 실행: node build.cjs   (Node 만 필요, 이미지 가공 없음)
//   COMBAT_REPO  그림(PNG)이 있는 저장소 체크아웃. 이 워크트리는 PNG 를 추적하지 않으므로 보통 메인 체크아웃을 준다
//   FX_SHEETS    이펙트 시트 폴더(기본: 이 워크트리의 outputs/pixelfx). 각 이펙트 폴더의 export.mjs 로 먼저 뽑는다
const fs = require('fs');
const path = require('path');

const HERE = __dirname;
const SELF = path.resolve(HERE, '../..');
const REPO = process.env.COMBAT_REPO || SELF;
const SHEETS = process.env.FX_SHEETS || path.join(SELF, 'outputs/pixelfx');
const ASSETS = path.join(REPO, 'unity/Assets');
const RES = path.join(ASSETS, 'Deviant/Combat/Resources');
const VILLAGE = path.join(ASSETS, 'Stage1VillageExperiment/Resources/Village/reference-sketch');
const ICONS = path.join(ASSETS, 'Deviant/TemporaryGUI/Resources/TemporaryGUI/Pixel');
const AUDIO = path.join(ASSETS, 'Deviant/Audio/Resources/ApprovedCombatAudio');

const need = p => { if (!fs.existsSync(p)) throw new Error(`없음: ${p}\n그림이 있는 체크아웃을 COMBAT_REPO 로 지정하라`); return p; };
const b64 = (p, mime) => `data:${mime};base64,` + fs.readFileSync(need(p)).toString('base64');
const png = p => b64(p, 'image/png');
const wav = p => b64(p, 'audio/wav');
function pngSize(p) { const b = fs.readFileSync(need(p)); return [b.readUInt32BE(16), b.readUInt32BE(20)]; }
function pivot(p) {
  const m = fs.readFileSync(p + '.meta', 'utf8').match(/spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/);
  if (!m) throw new Error('pivot 없음: ' + p);
  return [+m[1], +m[2]];
}
// 고해상도 그림(망치는 PPU128)은 게임 화소(PPU32)로 줄여 그린다
function ppu(p) { const m = fs.readFileSync(p + '.meta', 'utf8').match(/spritePixelsToUnits: ([\d.]+)/); return m ? +m[1] : 32; }

// 주인공: CharacterV2 카탈로그 그대로. 무기 A = 검, 무기 B = 망치
const catalog = JSON.parse(fs.readFileSync(path.join(RES, 'CharacterV2/AnimationCatalog.json'), 'utf8'));
const frames = {};
function frame(key) {
  if (!frames[key]) {
    const p = path.join(RES, key + '.png');
    const [w, h] = pngSize(p); const [px, py] = pivot(p);
    frames[key] = { src: png(p), w, h, px, py, k: 32 / ppu(p) };
  }
  return key;
}
function form(name, wanted) {
  const set = catalog.sets.find(s => s.form === name);
  if (!set) throw new Error('형태 없음: ' + name);
  const clips = {}, effects = {};
  for (const c of set.clips) if (wanted.includes(c.name))
    clips[c.name] = { frames: c.frames.map(frame), seconds: c.seconds, impact: c.sourceImpact, fxStart: c.fxStart };
  for (const e of set.effects || []) if (clips[e.attack])
    effects[e.attack] = { frames: e.frames.map(frame), seconds: e.seconds, x: e.x, y: e.y, scale: e.scale || 1 };
  for (const w of wanted) if (!clips[w]) throw new Error(`${name}/${w} 클립 없음`);
  return { clips, effects };
}
const player = {
  base_sword: form('base_sword', ['idle', 'run', 'attack1', 'attack2']),
  hammer: form('hammer', ['idle', 'run', 'attack1', 'attack2']),
};

// 몬스터: 1Stage 마을 5종 (Stage1Monsters import.json)
const imported = JSON.parse(fs.readFileSync(path.join(ASSETS, 'Deviant/Stage1Monsters/Data/import.json'), 'utf8'));
const monsters = imported.monsters.map(m => {
  const motion = id => {
    const x = m.motions.find(k => k.id === id);
    if (!x) return null;
    return { w: x.width, h: x.height, px: x.pivot[0], py: x.pivot[1], ms: x.milliseconds, loop: x.loop,
      frames: x.frames.map(f => png(path.join(REPO, 'unity', f.path))) };
  };
  return { id: m.id, name: m.displayName, elite: m.id === '04_well-warden', idle: motion('idle') || motion('base'), hit: motion('hit'), death: motion('death') };
});

// 배경: 마을 스케치 레이어와 바닥 타일
const bgFile = n => png(path.join(VILLAGE, 'backgrounds', n));
const GROUND_ART = path.join(ASSETS, 'Stage1VillageExperiment/Resources/Village/ReferenceGround');
const bg = {
  sky: bgFile('sky_native.png'), tall: bgFile('village_tall_native.png'), mid: bgFile('village_mid_native.png'),
  clouds: ['cloud_native_0.png', 'cloud_native_1.png', 'cloud_native_2.png'].map(bgFile),
  body: png(path.join(GROUND_ART, 'body.png')),
  caps: JSON.parse(fs.readFileSync(path.join(GROUND_ART, 'library.json'), 'utf8')).entries
    .filter(e => e.name.startsWith('cap-'))
    .map(e => ({ name: e.name, src: png(path.join(GROUND_ART, e.name + '.png')), ax: e.anchorX, ay: e.anchorY, step: e.logicalWidth })),
};

const sfx = {
  sword1: wav(path.join(AUDIO, 'Weapons/base_sword_basic_0.wav')),
  sword2: wav(path.join(AUDIO, 'Weapons/base_sword_basic_1.wav')),
  heavy: wav(path.join(AUDIO, 'Weapons/greatsword_basic_2.wav')),
};

// 파츠 20종: 이름·설명·색·트리거는 Unity 가 읽는 WeaponGrowthMvp.json(이 워크트리)에서, 아이콘은 임시 GUI 그림에서
const mvp = JSON.parse(fs.readFileSync(path.join(SELF, 'unity/Assets/Deviant/Combat/Resources/WeaponGrowthMvp.json'), 'utf8').replace(/^﻿/, ''));
const parts = [];
(function walk(o) {
  if (Array.isArray(o)) return o.forEach(walk);
  if (o && typeof o === 'object') {
    if (typeof o.id === 'string' && /^ink2_/.test(o.id) && o.name) parts.push({ id: o.id, name: o.name, color: o.color, trigger: o.trigger, description: o.description, icon: png(path.join(ICONS, `ink-${o.id}.png`)) });
    Object.values(o).forEach(walk);
  }
})(mvp);
if (parts.length !== 20) throw new Error(`파츠가 20종이 아님: ${parts.length}`);

// 이펙트 시트: outputs/pixelfx/<이펙트>/<이름>.json + .png (Aseprite 형식, meta.pixelfx 에 피벗·장 수)
const fx = {};
for (const dir of fs.readdirSync(need(SHEETS))) {
  const d = path.join(SHEETS, dir);
  if (!fs.statSync(d).isDirectory()) continue;
  for (const f of fs.readdirSync(d)) if (f.endsWith('.json')) {
    const j = JSON.parse(fs.readFileSync(path.join(d, f), 'utf8')), m = j.meta.pixelfx;
    fx[f.slice(0, -5)] = { src: png(path.join(d, j.meta.image)), fw: m.frameSize[0], fh: m.frameSize[1], px: m.pivot[0], py: m.pivot[1],
      n: m.frames, cols: m.columns, fps: m.fps, loop: !!m.loop, impact: m.impactFrame ?? 0, hold: m.holdFrame ?? null, extra: m };
  }
}
const NEEDED = ['follow-cut_red_v1', 'emp-hit_red', 'charge-rise_red', 'charge-release_red', 'burst-thorn_red', 'speed-burst_yellow', 'speed-trail_yellow',
  'speed-end_yellow', 'heal_green', 'shield-up_green', 'shield_green', 'shield-break_green', 'slow_purple', 'root_purple', 'pull-rope_purple',
  ...['base', 'red', 'yellow', 'green', 'blue', 'purple'].flatMap(c => [1, 2, 3, 4, 5].map(n => `mark-burst_${c}_m${n}`)), 'mark-drop-land_base', 'mark-drop-idle_base', 'mark-drop-expire_base'];
const missing = NEEDED.filter(k => !fx[k]);
if (missing.length) throw new Error('이펙트 시트 없음: ' + missing.join(', ') + '\n전투테스트/<이펙트>/export.mjs 로 먼저 뽑아라');

const read = p => JSON.parse(fs.readFileSync(path.join(SELF, p), 'utf8'));
const data = { player, frames, monsters, bg, sfx, parts, fx, reaction: read('data/combat/enemy_hit_reaction.json'), feel: read('data/combat/presentation/hit_feedback.json') };

const template = fs.readFileSync(path.join(HERE, 'template.html'), 'utf8');
if (template.split('__ASSETS__').length !== 2) throw new Error('template 의 __ASSETS__ 자리가 하나가 아님');
const out = process.env.LAB_OUT || path.join(HERE, '이펙트실험실.html');
fs.writeFileSync(out, template.replace('__ASSETS__', () => JSON.stringify(data)), 'utf8');
console.log(`${out}  ${(fs.statSync(out).size / 1024 / 1024).toFixed(2)}MB  파츠 ${parts.length}종, 이펙트 시트 ${Object.keys(fx).length}장, 몬스터 ${monsters.length}종`);
