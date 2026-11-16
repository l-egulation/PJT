'use strict';
// 방 원형 전체(모든 출입구 조합 × 반전)와 배치기 시드를 판정한다. 사용: node tools/map/validate.cjs [시드 수]
const M=require('./movecheck.js'),RS=require('./engine.js'),D=require('./rooms.js'),G=require('./generator.js');
const all=[...D.s1,...D.s1special,...D.s2,...D.s2special],byId=id=>all.find(r=>r.id===id);
let checks=0,bad=0;
for(const [stage,list] of [['s1',D.s1],['s1',D.s1special],['s2',D.s2],['s2',D.s2special]])for(const room of list)
 for(const mirror of [false,true])for(const c of RS.combos(room.slots)){checks++;const r=RS.checkRoom(room,c,mirror,M,stage);if(r.issues.length){bad++;if(bad<20)console.log(room.id,mirror?'반전':'',c.join('+'),r.issues.map(i=>i.rule+' '+i.msg).join(' / '));}}
console.log(`원형 판정 ${checks}건, 실패 ${bad}건`);
const N=+(process.argv[2]||20);let fail=0;
for(const st of ['stage1','stage2'])for(let s=1;s<=N;s++){
 const L=G[st](s);if(!L){fail++;console.log(st,s,'생성 실패');continue;}
 const W=RS.assemble(L,byId);if(W.issues.length){fail++;console.log(st,s,W.issues.slice(0,3));continue;}
 const k=RS.checkLayout(W,M,L.start,{wallClimb:true});if(!k.rooms.every(r=>r.go&&r.back)||k.enemiesReached!==k.enemies){fail++;console.log(st,s,'왕복 실패');}
}
console.log(`배치 시드 ${N}×2, 실패 ${fail}건`);
process.exitCode=bad||fail?1:0;
