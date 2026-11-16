'use strict';
// data/map/block_rooms.json(편집 원본)을 판정한 뒤 Unity Resources로 내보낸다.
// 사용: node tools/map/export_block_rooms.cjs [--check]
//   --check: 생성물이 원본·판정 결과와 일치하는지만 확인한다(파일을 쓰지 않음).
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..');
const src=path.join(root,'data/map/block_rooms.json');
const out=path.join(root,'unity/Assets/ForestCombatV2/Resources/BlockRooms.json');
const fixture=path.join(root,'unity/Assets/ForestCombatV2/Tests/Editor/BlockGeneratorParity.json');
const text=fs.readFileSync(src,'utf8');JSON.parse(text);
// 같은 시드에서 C# 배치기가 같은 결과를 내는지 Unity 시험이 비교할 기준 배치
const G=require('./generator.js');
const parity={schema:'block-generator-parity-v1',stage1:[],stage2:[]};
for(const [k,fn] of [['stage1',G.stage1],['stage2',G.stage2]])for(const seed of [1,2,3,17,42]){
 const L=fn(seed);parity[k].push({seed,attempt:L.attempt,rooms:L.rooms.map(r=>[r.key,r.ref,r.bx,r.by,!!r.mirror].join('|')),connectors:L.connectors.map(c=>[c.key,c.open.join(''),c.bx,c.by,c.bw||1].join('|')),links:L.links.map(l=>l.join('|'))});
}
const parityText=JSON.stringify(parity,null,1)+'\n';
if(process.argv.includes('--check')){
 const okRooms=fs.existsSync(out)&&fs.readFileSync(out,'utf8')===text,okFix=fs.existsSync(fixture)&&fs.readFileSync(fixture,'utf8')===parityText;
 if(!okRooms||!okFix){console.error('생성물이 원본과 다릅니다. node tools/map/export_block_rooms.cjs 로 다시 내보내세요.');process.exit(1);}
 console.log('일치');return;
}
fs.writeFileSync(out,text);fs.writeFileSync(fixture,parityText);
console.log('내보냄:',path.relative(root,out),path.relative(root,fixture));
