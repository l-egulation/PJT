'use strict';
// index.html(편집 원본)에 ../engine.js·../movecheck.js와 게임 현재 시작방 값을 넣어 한 파일짜리 시작방_편집기.html을 만든다.
// 판정 코드가 바뀌거나 data/map/block_rooms.json의 S1-START가 바뀌면 다시 실행한다: node tools/map/start-room-editor/build.cjs
const fs=require('node:fs'),path=require('node:path'),root=__dirname;
const data=JSON.parse(fs.readFileSync(path.join(root,'../../../data/map/block_rooms.json'),'utf8'));
const start=data.rooms.find(r=>r.id==='S1-START');if(!start)throw new Error('S1-START가 없습니다');
if(!start.spawn)throw new Error('S1-START에 spawn이 없습니다');
const def={spawn:{x:start.spawn.x,y:start.spawn.y},plats:start.plats.filter(p=>!p.need||!p.need.length||p.need.includes('R0')).map(p=>{const o={x:p.x,y:p.y,w:p.w};if(p.vanishing)o.vanishing=true;return o;}),enemies:(start.enemies||[]).map(e=>({t:e.t,x:e.x,y:e.y}))};
// 몬스터 추격 점프 한계: pursuit.appliesTo에 든 종만 추격·점프한다(종별 jumpHeight, 없으면 기본값).
const pursuit=JSON.parse(fs.readFileSync(path.join(root,'../../../data/combat/enemy_pursuit.json'),'utf8'));
const s1=data.species.s1,ids={T:'01_thorn-root',K:'02_broom-stalker',P:'03_gutter-spitter',G:s1.G,C:s1.C};
const jump={};for(const [t,id] of Object.entries(ids))jump[t]=pursuit.pursuit.appliesTo.includes(id)?((pursuit.species[id]||{}).jumpHeight??pursuit.pursuit.jumpHeight):0;
let html=fs.readFileSync(path.join(root,'index.html'),'utf8');
for(const file of ['engine.js','movecheck.js']){
  const tag='<script src="../'+file+'"></script>';if(!html.includes(tag))throw new Error(tag+' 없음');
  html=html.replace(tag,()=>'<script>\n'+fs.readFileSync(path.join(root,'..',file),'utf8').replace(/<\/script/gi,'<\/script')+'\n</script>');
}
const marker=/const GAME_DEFAULT=.*;\n/;if(!marker.test(html))throw new Error('GAME_DEFAULT 줄 없음');
html=html.replace(marker,()=>'const GAME_DEFAULT='+JSON.stringify(def)+';\n');
const jm=/const JUMP=.*;\n/;if(!jm.test(html))throw new Error('JUMP 줄 없음');
html=html.replace(jm,()=>'const JUMP='+JSON.stringify(jump)+';\n');
fs.writeFileSync(path.join(root,'시작방_편집기.html'),html);
console.log('시작방_편집기.html 생성 (발판 '+def.plats.length+', 몬스터 '+def.enemies.length+', 점프 '+JSON.stringify(jump)+')');
