// 손맛 테스트 빌드: 이 저장소의 확정 에셋(1Stage 마을 스케치·Stage1Monsters·CharacterV2)을 그대로 읽어
// 단독 HTML 한 장(손맛테스트.html)으로 묶는다. 실행: node build.cjs   (Node 만 필요, 이미지 가공 없음)
const fs = require('fs');
const path = require('path');

const HERE = __dirname;
// 저장소 안(전투테스트/손맛)에서는 저장소 루트를, 바깥 사본에서는 COMBAT_REPO 를 에셋 원본으로 쓴다
const REPO = process.env.COMBAT_REPO || path.resolve(HERE, '../..');
const ASSETS = path.join(REPO, 'unity/Assets');
const RES = path.join(ASSETS, 'Deviant/Combat/Resources');
const VILLAGE = path.join(ASSETS, 'Stage1VillageExperiment/Resources/Village/reference-sketch');
const AUDIO = path.join(ASSETS, 'Deviant/Audio/Resources/ApprovedCombatAudio');

const b64 = (p, mime) => `data:${mime};base64,` + fs.readFileSync(p).toString('base64');
const png = p => b64(p, 'image/png');
const wav = p => b64(p, 'audio/wav');
function pngSize(p) { const b = fs.readFileSync(p); return [b.readUInt32BE(16), b.readUInt32BE(20)]; }
function pivot(p) {
  const m = fs.readFileSync(p + '.meta', 'utf8').match(/spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/);
  if (!m) throw new Error('pivot 없음: ' + p);
  return [+m[1], +m[2]];
}

// 주인공: Unity 가 쓰는 CharacterV2 카탈로그 그대로 (프레임별 크기·피벗, 접촉 시각, 이펙트 오프셋)
const catalog = JSON.parse(fs.readFileSync(path.join(RES, 'CharacterV2/AnimationCatalog.json'), 'utf8'));
const frames = {};
function frame(key) {
  if (!frames[key]) {
    const p = path.join(RES, key + '.png');
    const [w, h] = pngSize(p); const [px, py] = pivot(p);
    frames[key] = { src: png(p), w, h, px, py };
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
  base_sword: form('base_sword', ['idle', 'run', 'dash', 'attack1', 'attack2']),
  hammer: form('hammer', ['attack1', 'attack2']),
};

// 몬스터: Stage1Monsters import.json (1Stage 마을 확정 5종, 엘리트 = 04_well-warden)
const imported = JSON.parse(fs.readFileSync(path.join(ASSETS, 'Deviant/Stage1Monsters/Data/import.json'), 'utf8'));
const monsters = imported.monsters.map(m => {
  const motion = id => {
    const x = m.motions.find(k => k.id === id);
    if (!x) return null;
    return {
      w: x.width, h: x.height, px: x.pivot[0], py: x.pivot[1], ms: x.milliseconds, loop: x.loop,
      frames: x.frames.map(f => png(path.join(REPO, 'unity', f.path))),
    };
  };
  const idle = motion('idle') || motion('base');
  return { id: m.id, name: m.displayName, elite: m.id === '04_well-warden', idle, hit: motion('hit'), death: motion('death') };
});

// 배경: 마을 스케치 원본 레이어와 32px 지형 타일
const bgFile = n => png(path.join(VILLAGE, 'backgrounds', n));
const GROUND_ART = path.join(ASSETS, 'Stage1VillageExperiment/Resources/Village/ReferenceGround');
const bg = {
  sky: bgFile('sky_native.png'),
  tall: bgFile('village_tall_native.png'),
  mid: bgFile('village_mid_native.png'),
  clouds: ['cloud_native_0.png', 'cloud_native_1.png', 'cloud_native_2.png'].map(bgFile),
  // 바닥: 게임이 쓰는 ReferenceGround (빗금 몸통 + 돌 뚜껑, anchor 는 library.json)
  body: png(path.join(GROUND_ART, 'body.png')),
  caps: JSON.parse(fs.readFileSync(path.join(GROUND_ART, 'library.json'), 'utf8')).entries
    .filter(e => e.name.startsWith('cap-'))
    .map(e => ({ name: e.name, src: png(path.join(GROUND_ART, e.name + '.png')), ax: e.anchorX, ay: e.anchorY, step: e.logicalWidth })),
};

const sfx = {
  sword1: wav(path.join(AUDIO, 'Weapons/base_sword_basic_0.wav')),
  sword2: wav(path.join(AUDIO, 'Weapons/base_sword_basic_1.wav')),
  heavy: wav(path.join(AUDIO, 'Weapons/greatsword_basic_2.wav')),
  blast: wav(path.join(AUDIO, 'Events/sfx_combat_bomb_explosion_01_v01.wav')),
};

const read = p => JSON.parse(fs.readFileSync(path.join(REPO, p), 'utf8'));
const data = {
  player, frames, monsters, bg, sfx,
  reaction: read('data/combat/enemy_hit_reaction.json'),
  unityFeel: read('data/combat/presentation/hit_feedback.json'),
};

const template = fs.readFileSync(path.join(HERE, 'template.html'), 'utf8');
if (template.split('__ASSETS__').length !== 2) throw new Error('template 의 __ASSETS__ 자리가 하나가 아님');
const out = process.env.FEEL_OUT || path.join(HERE, '손맛테스트.html');
fs.writeFileSync(out, template.replace('__ASSETS__', () => JSON.stringify(data)), 'utf8');
console.log(`${out}  ${(fs.statSync(out).size / 1024 / 1024).toFixed(2)}MB  주인공 프레임 ${Object.keys(frames).length}장, 몬스터 ${monsters.length}종`);
