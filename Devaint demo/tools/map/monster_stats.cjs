'use strict';
// 스테이지별 몬스터 배치 규모: 시드마다 실제 배치기·조립기로 맵을 만들고 종류별 마리수와 체력 합을 낸다.
// 사용: node tools/map/monster_stats.cjs [시드 수=200] [--csv 폴더]
// 배치: data/map/block_rooms.json(방 원형의 적, species 글자 대응). 같은 시드는 게임과 같은 배치다.
// 체력: 1챕터 data/combat/stage1_village_monsters.json(legacyMap·maxHP), chapter1_new_monsters.json(behavior.hp),
//       clock_executioner_timing.json(maxHealth). 2챕터 unity/Assets/Chapter2/Resources/Chapter2ApprovedNormals.json(hp),
//       chapter2_elite.json(hp), chapter2_rose_blue.json(values).
// 처음 배치만 센다. 봉인 도전(4·5·6마리, 그 판 일반 전투방 종류 중 무작위)은 기댓값, 보스 패턴 소환물은 제외한다.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..');
const RS=require('./engine.js'),D=require('./rooms.js'),G=require('./generator.js');
const J=f=>JSON.parse(fs.readFileSync(path.join(root,f),'utf8'));
const all=[...D.s1,...D.s1special,...D.s2,...D.s2special],byId=id=>all.find(r=>r.id===id);
const args=process.argv.slice(2),N=+(args.find(a=>/^\d+$/.test(a))||200),csvAt=args.indexOf('--csv'),csvDir=csvAt>=0?args[csvAt+1]:null;
const sp=J('data/map/block_rooms.json').species;
const v=J('data/combat/stage1_village_monsters.json'),n1=J('data/combat/chapter1_new_monsters.json');
const legacy=Object.fromEntries(v.legacyMap.map(m=>[m.from,m.to]));
const hp1=Object.fromEntries([...v.monsters.map(m=>[m.id,{name:m.name,hp:m.maxHP}]),...n1.monsters.map(m=>[m.id,{name:m.name.replace(/ \(가칭\)$/,''),hp:m.behavior.hp}])]);
const c2=J('unity/Assets/Chapter2/Resources/Chapter2ApprovedNormals.json').monsters,el2=J('data/combat/chapter2_elite.json');
const species={
 stage1:Object.fromEntries(Object.entries(sp.s1).map(([k,id])=>{const r=legacy[id]||id;if(!hp1[r])throw new Error('1챕터 체력 없음 '+r);return [k,hp1[r]];})),
 stage2:Object.fromEntries(Object.entries(sp.s2).map(([k,id])=>{if(k==='A')return [k,{name:'A3 엘리트',hp:el2.hp}];const m=c2.find(m=>m.species===id);if(!m)throw new Error('2챕터 종 없음 '+id);return [k,{name:m.name,hp:m.hp}];})),
};
const elite={stage1:'W',stage2:'A'};
const boss1=J('data/combat/clock_executioner_timing.json').maxHealth,rb=J('data/combat/chapter2_rose_blue.json').values;
const stat=a=>{a=[...a].sort((x,y)=>x-y);return {min:a[0],avg:a.reduce((x,y)=>x+y,0)/a.length,max:a[a.length-1]};};
const fmt=s=>`${s.min} / ${s.avg.toFixed(1)} / ${s.max}`;
for(const st of ['stage1','stage2']){
 const map=species[st],keys=Object.keys(map),rows=[];let fail=0;
 for(let seed=1;seed<=N;seed++){
  const L=G[st](seed);if(!L){fail++;continue;}
  const W=RS.assemble(L,byId),count={};for(const e of W.enemies)count[e.t]=(count[e.t]||0)+1;
  const normal=keys.filter(k=>k!==elite[st]);
  const row={seed,count,n:normal.reduce((s,k)=>s+(count[k]||0),0),hp:normal.reduce((s,k)=>s+(count[k]||0)*map[k].hp,0)};
  if(st==='stage1'){const kinds=[...new Set(W.enemies.filter(e=>{const r=L.rooms.find(r=>r.key===e.owner);return r&&byId(r.ref).kind==='combat'&&e.t!==elite[st];}).map(e=>e.t))];
   row.sealHp=15*kinds.reduce((s,t)=>s+map[t].hp,0)/kinds.length;}
  rows.push(row);
 }
 console.log(`\n${st==='stage1'?'1챕터':'2챕터'} · 시드 1~${N}, 생성 실패 ${fail}`);
 console.log('| 몬스터 | 체력 | 마리수 (최소 / 평균 / 최대) |');
 for(const k of keys)console.log(`| ${map[k].name} | ${map[k].hp} | ${fmt(stat(rows.map(r=>r.count[k]||0)))} |`);
 console.log(`| 일반 합계 | | ${fmt(stat(rows.map(r=>r.n)))} · 체력 ${fmt(stat(rows.map(r=>r.hp)))} |`);
 if(st==='stage1'){console.log(`보스 시계탑 집행자 ${boss1}, 봉인 도전 15마리 체력 기댓값 ${fmt(stat(rows.map(r=>r.sealHp)))}`);}
 else console.log(`보스 Rose·Blue: 장치 ${rb.MachineHp} → Rose ${rb.RoseHp}·Blue ${rb.BlueHp}(하나 쓰러뜨리면 전환) → 합쳐진 Blue ${rb.FusedBlueHp}`);
 if(csvDir){fs.mkdirSync(csvDir,{recursive:true});
  const head=['seed',...keys.map(k=>map[k].name),'일반 마리수','일반 체력 합'].concat(st==='stage1'?['봉인 도전 체력 기댓값']:[]);
  const lines=[head.join(','),...rows.map(r=>[r.seed,...keys.map(k=>r.count[k]||0),r.n,r.hp].concat(st==='stage1'?[Math.round(r.sealHp)]:[]).join(','))];
  fs.writeFileSync(path.join(csvDir,st+'_monsters.csv'),'﻿'+lines.join('\n')+'\n');}
}
