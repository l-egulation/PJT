'use strict';
// 색상 잉크 파츠 개편안의 원본을 검사하고 카탈로그 문서를 생성한다. --check는 생성물 일치만 확인한다.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../..'),source='data/combat/ink_color_parts_proposal.json',output='docs/development/data/catalogs/ink-color-parts.md';
const catalog=JSON.parse(fs.readFileSync(path.join(root,source),'utf8'));
const percentKeys=new Set(['rechargeReduction','ratio','amount','damageTaken','windupReduction','attackSpeed','moveSpeed','switchWindupReduction','threshold','ratioPerState','amountPerKill','cap','amountPerStack']);
const paramNames={rechargeReduction:'충전 회복 단축',amountPerKill:'처치당',cap:'최대',amountPerStack:'중첩당',maxStacks:'최대 중첩',resetSeconds:'초기화',ratio:'비율',amount:'효과량',attackSpeed:'공속',moveSpeed:'이속',switchWindupReduction:'스위칭 준비 단축',damageTaken:'받는 피해',windupReduction:'준비 단축',threshold:'체력 기준',ratioPerState:'상태당 비율',duration:'지속',interval:'간격',seconds:'감소',radius:'반경',length:'길이',distance:'이동 거리',count:'대상 수',target:'대상',stacksBySlotLevel:'슬롯 +0~+5 최대 중첩'};
const targets={incoming_weapon:'바꿔 든 무기',equipped_weapon:'장착 무기',both_weapons:'두 무기'};
const ruleSources={user:'사용자 결정',draft:'초안',existing:'기존'};
// 표식은 쌓인 순서대로 이 색을 띠고 역순으로 터진다. markIndex는 1(빨강)~7(보라)이다.
const markColors=['빨강','주황','노랑','초록','파랑','남색','보라'];
// 색상별 파츠 수. 처음 31종(노랑은 끊어 치기를 더해 7종)에 2026-11-05 추가 12종, 같은 날 사용자가 고른 11종을 더했다.
const colorCounts={red:10,yellow:12,green:10,blue:10,purple:11},total=Object.values(colorCounts).reduce((a,b)=>a+b,0);

function verify(){
 const triggers=new Set(catalog.triggers.map(t=>t.id)),colors=catalog.colorRoles.map(c=>c.color);
 assert.equal(catalog.parts.length,total);
 assert.equal(new Set(catalog.parts.map(p=>p.id)).size,total,'중복 ID');
 assert.equal(new Set(catalog.parts.map(p=>p.name)).size,total,'중복 이름');
 for(const color of colors)assert.equal(catalog.parts.filter(p=>p.color===color).length,colorCounts[color],`${color} ${colorCounts[color]}종`);
 for(const r of catalog.rules)assert.ok(ruleSources[r.source]&&r.text,'규칙 출처');
 for(const p of catalog.parts){
  assert.ok(triggers.has(p.trigger),p.id+' 트리거');
  assert.ok(colors.includes(p.color),p.id+' 색상');
  assert.ok(p.cooldownSeconds>=0&&typeof p.duplicate==='boolean',p.id+' 쿨타임·중복');
  // 회복은 처치·적중 누적(N회마다) 또는 표식 터트리기(쿨타임 10초 이상)로만, 보호막은 자체 쿨타임 15초 이상.
  if(p.kind==='heal')assert.ok(['hit','kill'].includes(p.trigger)&&p.every>1||p.trigger==='mark_pop'&&p.cooldownSeconds>=10,p.id+' 누적 회복');
  if(p.kind==='shield'||p.kind==='hit_guard_shield')assert.ok(p.cooldownSeconds>=15,p.id+' 보호막 쿨타임');
  assert.equal(p.trigger==='mark_pop',Number.isInteger(p.markIndex)&&p.markIndex>=1&&p.markIndex<=7,p.id+' 표식 순번');
 }
}
const value=(k,v)=>Array.isArray(v)?v.join('/'):k==='target'?targets[v]||v:percentKeys.has(k)&&(v<=1||k==='ratio')?`${Math.round(v*1000)/10}%`:['duration','interval','seconds','resetSeconds'].includes(k)?v+'초':v;
const tuning=JSON.parse(fs.readFileSync(path.join(root,'data/combat/ink_color_parts_tuning.json'),'utf8'));
const params=p=>Object.entries(p.params).map(([k,v])=>`${paramNames[k]||k} ${value(k,v)}`).join(' · ');

function generate(){
 verify();
 const trig=Object.fromEntries(catalog.triggers.map(t=>[t.id,t.name]));
 const lines=[`# 색상 잉크 파츠 개편안 ${total}종`,'',
  '색상마다 고유한 방식으로 피해에 기여하도록 다시 짠 색상 잉크 파츠다. 색상 역할과 아래 `사용자 결정` 규칙은 확정 방향이다. 파츠별 등급·+0~+5 수치·쿨타임은 승인된 재설계 기준이다. 실전 밸런스 체감은 플레이 검증 대상이다.','',
  `편집 원본: [\`ink_color_parts_proposal.json\`](../../../../${source}). 이 문서는 \`node tools/ink-combination-lab/generate-color-parts.cjs\`로 생성하고 \`--check\`로 일치를 검사한다. 현재 제품 기준은 [잉크 구성 규칙](../../../design/equipment/inks/rules.md#효과-부여와-공격-부여)이다.`,'',
  `**구현 범위**: Unity 런의 색상 잉크는 이 ${total}종을 사용한다. 파츠별 고정 내부 등급·슬롯 강화 대상은 [\`ink_color_parts_tuning.json\`](../../../../data/combat/ink_color_parts_tuning.json)의 확정 테이블이고, 가방과 정비소는 파츠별 문양과 등급 문장을 사용한다. 전투 이펙트는 기존 그림을 임시로 쓰고, 전기장만 반경에 맞춘 전용 번개 장판 반복 시트를 쓴다. 브라우저 시뮬레이터는 [이전 50종](ink-simulator-part-catalog.md)으로 남아 있다.`,'',
  '## 색상 역할','','| 색 | 역할 | 피해에 기여하는 방식 |','|---|---|---|',
  ...catalog.colorRoles.map(c=>`| ${c.name} | ${c.role} | ${c.summary} |`),'',
  '## 트리거','','| 트리거 | 기준 |','|---|---|',...catalog.triggers.map(t=>`| ${t.name} | ${t.description} |`),'',
  '## 공통 규칙','','`사용자 결정`은 확정 방향, `초안`은 사용자 결정을 구현하려고 제안한 시험 규칙, `기존`은 개편 설계의 기존 규칙이다.','',
  ...catalog.rules.map(r=>`- \`${ruleSources[r.source]}\` ${r.text}`),'',
  '## 용어','',`- **${catalog.terms.markBurst}**: 표식을 쌓은 무기가 아닌 무기로 맞혀, 쌓인 표식을 역순으로 하나씩 모두 터트리는 것`,`- **기준 피해**: ${catalog.terms.baseReference}`,
  ...[catalog.terms.bind,catalog.terms.stun].map(t=>'- '+t.replace(/^([^:]+):/,'**$1**:')),'',
  ...(catalog.statusVisuals?.length?['## 몬스터 상태 표시','',...catalog.statusVisuals.map(s=>'- '+s),'']:[])];
 for(const c of catalog.colorRoles){
  lines.push(`## ${c.name} — ${c.role}`,'','| ID · 이름 | 트리거 | 효과 | 쿨타임 · 중복 | 제약 |','|---|---|---|---|---|');
  for(const p of catalog.parts.filter(p=>p.color===c.color))
   lines.push(`| \`${p.id}\` **${p.name}** | ${p.trigger==='mark_pop'?`${markColors[p.markIndex-1]}(${p.markIndex}번째) 표식이 터질 때`:trig[p.trigger]}${p.every?` ${p.every}회마다`:''}${p.condition==='first_hit_per_life'?' (몬스터당 첫 적중)':p.condition==='special'?' (특수 스킬 원타격)':''} | ${p.description}${params(p)?`<br><small>${params(p)}</small>`:''} | ${p.cooldownSeconds}초 · ${p.duplicate?'중복 가능':'무기당 1'} | ${p.limits} |`);
  lines.push('');
 }
 lines.push('## 고정 등급과 슬롯 강화 수치','','아래 수치는 +0 → +1 → +2 → +3 → +4 → +5 순서다. 범위 증폭은 여의봉을 통합했으며, 기존 여의봉 ID는 범위 증폭으로 읽는다.','','| 파츠 | 등급 | 강화 수치 |','|---|---|---|');
 for(const row of tuning.partTuning){const p=catalog.parts.find(p=>p.id===row.id);lines.push(`| ${p.name} | ${row.fixedGrade} | ${Object.entries(row.levelTable).map(([k,vs])=>`${row.statLabels[k]}: ${vs.map(v=>`${Number((v*(row.statUnits[k]==='%'?100:1)).toFixed(3))}${row.statUnits[k]}`).join(' → ')}`).join('<br>')} |`);}
 lines.push('');
 lines.push('## 남은 결정','',...catalog.openQuestions.map(q=>'- '+q),'');
 lines.splice(1,0,'','[카탈로그 개요](README.md)','','> 적용 범위: 현재 코드·생성기·시험 도구의 구현 자료이며 제품 성장 기획이 아니다. 새 기준은 [무기 도면·런 잉크 도감](../../../design/equipment/rules.md)이다. 아래 고정 파츠·등급·팔레트 수치를 새 잉크 레벨·보상에 승계하지 않는다.');
 return lines.join('\n');
}

if(require.main===module){
 const content=generate(),target=path.join(root,output);
 if(process.argv.includes('--check')){if(!fs.existsSync(target)||fs.readFileSync(target,'utf8')!==content){console.error('원본 불일치: '+output);process.exitCode=1;}}
 else fs.writeFileSync(target,content);
 if(!process.exitCode)console.log(`색상 잉크 파츠 ${total}종 ${process.argv.includes('--check')?'원본 일치':'생성'}: ${output}`);
}
module.exports={generate};
