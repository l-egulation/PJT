'use strict';
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const catalog=read('data/combat/ink_simulator_parts_proposal.json'),builds=read('data/combat/ink_simulator_builds_proposal.json');
const colors={red:['빨강','공격'],yellow:['노랑','이동 속도'],green:['초록','생명·보호막·회복'],blue:['파랑','범위·쿨타임'],purple:['보라','CC 부여']};
const common=[
 '파츠는 트리거 + 효과로 구성한다. 빨강=공격, 노랑=이동 속도, 초록=생명·보호막·회복, 파랑=범위·쿨타임, 보라=CC 부여.',
 '초기 트리거는 몬스터 적중 시 / 스위칭 시 / 표식을 터트릴 시 / 몬스터 처치 시다.',
 '제품 장착 기준: 무기에 팔레트를 먼저 장착하고 지정 색상 칸에만 잉크를 넣는다. 기본 효과는 장착 무기 자신에게만 적용하며, 다른 무기 강화는 개별 파츠에 명시한 예외다. 자유 50종 모드는 비교용으로 유지하며, 별도 MVP 정비소 모드의 20종에는 팔레트·색상·귀속·강화를 적용한다.',
 '스위칭은 A의 관련 효과 모두 발동 → B로 전환 → B 자동 공격 순서다. 스위칭 파츠의 발동 원본은 A이며 적용 대상은 개별 설명을 따른다.',
 'MVP 표식은 원타격 적중당 1개를 쌓고 추가타는 부여하지 않는다. 최대 5개·8초 유지하며 무기 전환 후 적중하면 쌓인 표식을 전량 소비한다. 소비한 표식 수에 비례한 기본 격발 피해를 주고 각 격발 잉크 효과는 격발당 1회 발동한다. MVP 기본 격발 피해는 격발 무기의 기본 콤보 타격 피해 평균 × 소비 표식 수 × 0.15를 시험 기준으로 한다. 이 피해는 추가타로서 잉크의 적중·처치 트리거를 발동하고 새 표식은 부여하지 않는다. 원타격으로 표식을 추가할 때 해당 몬스터의 전체 표식 유지시간을 8초로 갱신한다. 격발 타격은 기존 표식을 먼저 격발한 뒤 새 무기의 표식 1개를 남긴다.',
 '추가타도 적중·처치 트리거를 발동한다. 추가타·격발·처치의 연쇄 제한은 미정이다. 지속 피해는 없다. 지연 단발 공격은 시간이 지난 뒤 한 번 공격한다.',
 '모든 숫자·퍼센트·시간·거리·누적 한도는 제안값이다. 내부 쿨타임·중복 유형·동일 효과 중첩·CC 저항과 보스 적용은 미정이다. 미정을 쿨타임 0 또는 무제한 발동으로 해석하지 않는다.',
 '색상별 적중 3·스위칭 3·격발 2·처치 2종 배분은 이번 초안의 다양성을 위한 제안이며 필수 콘텐츠 배분 규칙이 아니다.',
 '범위 피해 공격은 빨강, 사거리·반경을 늘리는 효과는 파랑으로 제안한다. 복합 효과의 최종 색상 판단은 검토가 필요하다.'
];
function generate(worksheet=false){
 const lines=[worksheet?'# 잉크별 설계 작성지 — 신규 50종':'# 색상 잉크 파츠 50종 설계 초안','',
 '색상별 10종의 후보 중 20종을 MVP 우선 콘텐츠로 채택했다. 개별 효과 수치·세부 제약은 검토 초안이며 제품 구현 완료를 뜻하지 않는다.','',
 '편집 원본: [ink_simulator_parts_proposal.json](../../../../data/combat/ink_simulator_parts_proposal.json). 현재 제품 기준은 [잉크 구성 규칙](../../../design/equipment/inks/rules.md#효과-부여와-공격-부여)를 따른다.','',
 worksheet?'이 문서는 직접 편집한다. 효과·수치를 확정 변경하면 JSON에도 반영하고 카탈로그를 재생성한다. `--worksheet`는 수동 편집을 덮어쓰므로 편집 후 실행하지 않는다.':'이 문서는 `node tools/ink-combination-lab/generate-simulator.cjs`로 생성한다. 직접 작성할 내용은 [잉크별 설계 작성지](../../../design/equipment/inks/combination-worksheet.md)를 사용한다.','',
 '## 공통 기준','',...common.map(x=>'- '+x),''];
 const selected=new Set(catalog.mvp?.partIds||[]);
 if(selected.size){lines.push('## MVP 우선 채택 20종','','색상별 4종이며 각 색상에서 적중·스위칭·격발·처치 1종씩 채택한다. 콘텐츠 종류와 트리거의 선정이며, 아래 수치·지속시간·제약은 검토 초안이다. 나머지 30종은 후속 후보로 보존한다.','','| 색상 | 파츠 | 트리거 | 효과 초안 |','|---|---|---|---|');
 for(const p of catalog.parts.filter(p=>selected.has(p.id)))lines.push(`| ${colors[p.color][0]} | ${p.name} | ${catalog.triggers[p.trigger]} | ${p.effect.description} |`);
 lines.push('','표식 부여·누적·소비, 내부 쿨타임·중첩·연쇄, CC 저항·보스 적용은 미정이다. MVP 정비소·드롭·Unity 연결 범위와 실행 검증은 개발 문서를 따른다.','');}
 for(const [color,[name,role]]of Object.entries(colors)){
  lines.push(`## ${name} — ${role} 10종`,'');
  for(const p of catalog.parts.filter(p=>p.color===color)){
   lines.push(`### ${p.name} — \`${p.id}\``,'',`색상: ${name} · 트리거: **${catalog.triggers[p.trigger]}**`,'',
    '**1. 운용 목표**','',`- 목표: ${p.goal}`,`- 활용 상황: ${p.situation}`,`- 선택 이유: ${p.reason}`,`- 연계 의도: ${p.combo}`,'',
    '**2. 발동 조건**','',`- 트리거: ${catalog.triggers[p.trigger]}`,`- 발동 원본: ${p.source==='outgoing_weapon'?'전환 전 A무기':'적중·처치·격발 이벤트의 무기. 파생 공격 귀속 세부 판정은 미정'}`,
    ...(p.requires?['- 전제: 기본 격발 피해 계수와 세부 판정 결정 필요']:[]),'',
    '**3. 보상 효과**','',`- 적용 대상: ${p.effect.target}`,`- 무기 귀속: ${p.effect.weaponScope==='incoming_weapon'?'들어오는 B무기 · 명시적 예외':p.effect.weaponScope==='both_weapons'?'두 무기 · 명시적 예외':'장착 무기 자신'}`, ...(p.effect.crossWeaponException?[`- 예외 표기: ${p.effect.crossWeaponException}`]:[]),`- 효과 초안: ${p.effect.description}`,'',
    '**4. 대가·제약**','',`- 대가 초안: ${p.cost}`,`- 운용상 약점·제한: ${p.limits}`,'- 내부 쿨타임·중복 유형: 미정','',
    '**추가로 정할 내용**','',...p.openQuestions.map(q=>'- '+q),
    ...(worksheet?['- 내 수정안: **작성 필요**',selected.has(p.id)?'- MVP 범위: **우선 채택** · 효과 수치·세부 제약은 검토 필요':'- MVP 범위: **후속 후보** · 이번 20종에 포함하지 않음']:[]),'');
  }
 }
 const names=new Map(catalog.parts.map(p=>[p.id,p.name]));
 lines.push('## 조합 예시','','성능을 실행·탐색하지 않은 설계 예시다. 획득 가능한 팔레트나 최적 조합을 뜻하지 않는다. 원본은 [ink_simulator_builds_proposal.json](../../../../data/combat/ink_simulator_builds_proposal.json)이다.','','| 방향 | 무기 A | 무기 B | 전제 |','|---|---|---|---|');
 for(const b of builds.builds)lines.push(`| ${b.name} | ${b.build[0].map(id=>names.get(id)).join(' · ')} | ${b.build[1].map(id=>names.get(id)).join(' · ')} | ${b.requires||'없음'} |`);
 lines.push('','## 구현 범위','','50종 파츠의 구조화 수치 `effect.params`를 [독립 브라우저 시뮬레이터](../../tools/ink-simulator.md)에 연결했다. 피해·회복·보호막·이동 속도·범위·쿨타임·CC와 4종 트리거를 간이 모델로 계산한다. `ink_simulator_proposal.json`의 표식·연쇄·중첩·CC 설정은 미정 규칙의 실험값이며 제품 규칙을 확정하지 않는다. 이 50종을 Unity 전투로 내보내지 않는다. 기존 Unity 잉크 데이터와 실행 코드는 변경하지 않는다.','',
 '표식 기본 피해 계수·처리 순서·연쇄 제한, 공통 효과 중첩, 스위칭 취소, CC 저항·보스 적용, 미지원 무기 범위 해석, 실제 수치는 제품 설계로는 미정이다. 브라우저 간이 무기·판정·AI는 Unity 전투·드롭·저장·경제를 재현하지 않는다. 엔진·브라우저 검사는 제품 밸런스·재미나 Unity 동작 검증을 대신하지 않는다.','');
 lines.splice(1,0,'','[카탈로그 개요](README.md)','','> 적용 범위: 현재 코드·생성기·시험 도구의 구현 자료이며 제품 성장 기획이 아니다. 새 기준은 [무기 도면·런 잉크 도감](../../../design/equipment/rules.md)이다. 아래 고정 파츠·등급·팔레트 수치를 새 잉크 레벨·보상에 승계하지 않는다.');
 return lines.join('\n');
}
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function generatePreview(){
 const cards=catalog.parts.map(p=>`<article data-color="${p.color}" data-trigger="${p.trigger}"><header><span>${esc(colors[p.color][0])} · ${esc(catalog.triggers[p.trigger])}</span><h2>${esc(p.name)}</h2><code>${p.id}</code></header><p class="effect">${esc(p.effect.description)}</p><dl>${[['운용 목표',p.goal],['활용 상황',p.situation],['발동 원본',p.source==='outgoing_weapon'?'전환 전 A무기':'이벤트의 무기'],['적용 대상',p.effect.target],['제약',p.limits],['연계',p.combo]].map(([k,v])=>`<dt>${k}</dt><dd>${esc(v)}</dd>`).join('')}</dl>${p.requires?'<p class="pending">표식 기본 피해 계수·처리 순서 미정</p>':''}<details><summary>검토할 내용</summary><p>${esc(p.cost)}</p><ul>${p.openQuestions.map(q=>`<li>${esc(q)}</li>`).join('')}<li>내부 쿨타임·중복 유형 미정</li></ul></details></article>`).join('\n');
 return `<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>잉크 파츠 설계 · 50종</title><style>
 :root{font-family:"Malgun Gothic",sans-serif;color:#edf0f4;background:#111820;color-scheme:dark}*{box-sizing:border-box}body{margin:0}main{max-width:1280px;margin:auto;padding:32px 22px}h1{margin:0 0 12px;font-size:28px}.intro{color:#bbc7d3;line-height:1.8}.notice{border:1px solid #6b5830;background:#2c281e;padding:16px;border-radius:12px;line-height:1.7}.flow{padding:16px;background:#1c2734;border-radius:12px;line-height:1.8}.controls{display:flex;gap:12px;flex-wrap:wrap;padding:20px 0}label{display:grid;gap:7px;font-size:13px;color:#bbc7d3}select,input{font:inherit;padding:11px;border:1px solid #526276;background:#18222d;border-radius:8px;color:#edf0f4}input{min-width:240px}#count{color:#bbc7d3}#grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}article{border:1px solid #374656;border-top:4px solid var(--c);border-radius:12px;background:#1a242f;padding:20px;min-width:0}article[data-color=red]{--c:#ff8b7b}article[data-color=yellow]{--c:#f7d76d}article[data-color=green]{--c:#78d9ae}article[data-color=blue]{--c:#7ac6ff}article[data-color=purple]{--c:#c3a1ff}header span{color:var(--c);font-size:13px}h2{font-size:21px;margin:10px 0 5px}code{font-size:11px;color:#99a9bb}.effect{line-height:1.8;border-bottom:1px solid #374656;padding-bottom:16px}dl{font-size:13px;line-height:1.8}dt{color:#99a9bb;margin-top:12px}dd{margin:0}.pending{font-size:13px;color:#f7d76d}details{border-top:1px solid #374656;padding-top:14px;font-size:13px;line-height:1.8}summary{cursor:pointer}article[hidden]{display:none}footer{color:#99a9bb;line-height:1.8;padding:24px 0}@media(max-width:960px){#grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:620px){main{padding:22px 14px}#grid{grid-template-columns:1fr}h1{font-size:25px}input{min-width:0;width:100%}.controls label:last-child{width:100%}}
 </style><main><h1>잉크 파츠 설계 · 50종</h1><p class="intro">색상별 10종 · 파츠 = 트리거 + 효과<br>빨강 공격 / 노랑 이동 속도 / 초록 생명·보호막·회복 / 파랑 범위·쿨타임 / 보라 CC</p><p class="notice"><strong>설계 초안 열람 화면입니다.</strong> 전투 시뮬레이션은 연결하지 않았습니다. 모든 수치는 제안값이며 표식 부여·연쇄 제한·중복·CC 저항은 미정입니다.</p><p class="flow">스위칭: <strong>A의 효과 모두 발동 → B로 전환 → B 자동 공격</strong><br>표식: 부여·누적 → 무기 전환 후 적중 시 격발<br>추가타도 적중·처치 트리거 발동 / 지속 피해 없음</p><div class="controls"><label>색상<select id="color"><option value="all">모든 색상</option>${Object.entries(colors).map(([id,[name,role]])=>`<option value="${id}">${name} · ${role}</option>`).join('')}</select></label><label>트리거<select id="trigger"><option value="all">모든 트리거</option>${Object.entries(catalog.triggers).map(([id,name])=>`<option value="${id}">${name}</option>`).join('')}</select></label><label>이름·효과·목표 검색<input type="search" id="query" placeholder="예: 보호막, 사거리, 스위칭"></label></div><p id="count" aria-live="polite">50 / 50종</p><section id="grid">${cards}</section><p id="empty" hidden>조건에 맞는 파츠가 없습니다.</p><footer>모든 스위칭 파츠는 A에서 발동하며 플레이어·B 등에 적용할지는 각 설명을 따릅니다.<br>표식 격발 파츠에는 원타격 적중의 표식 공급을 사용하며 격발 피해·소비 규칙은 미정입니다. 실제 게임 구현 및 밸런스 검증을 뜻하지 않습니다.</footer></main><script>
 const controls=['color','trigger','query'].map(id=>document.getElementById(id)),cards=[...document.querySelectorAll('article')];function filter(){const [color,trigger,query]=controls.map(el=>el.value);let count=0;for(const card of cards){const show=(color==='all'||card.dataset.color===color)&&(trigger==='all'||card.dataset.trigger===trigger)&&card.textContent.toLowerCase().includes(query.trim().toLowerCase());card.hidden=!show;if(show)count++;}document.getElementById('count').textContent=count+' / 50종';document.getElementById('empty').hidden=count>0;}controls.forEach(el=>el.addEventListener('input',filter));
 </script></html>\n`;
}
function generateSimulator(){
 const experiment=read('data/combat/ink_simulator_proposal.json');
 const values={CATALOG:JSON.stringify(catalog).replace(/</g,'\\u003c'),EXPERIMENT:JSON.stringify(experiment).replace(/</g,'\\u003c'),BUILDS:JSON.stringify(builds).replace(/</g,'\\u003c')};
 for(const [key,file]of Object.entries({GROWTH:'growth.js',GROWTHAPP:'growth-app.js',STYLE:'simulator.css',ENGINE:'simulator.js',APP:'simulator-app.js'}))values[key]=fs.readFileSync(path.join(__dirname,file),'utf8');
 values.TUNING=JSON.stringify(read('data/combat/weapon_growth_mvp_tuning_draft.json')).replace(/</g,'\\u003c');values.ECONOMY=JSON.stringify(read('data/combat/chapter1_economy.json')).replace(/</g,'\\u003c');
 return fs.readFileSync(path.join(__dirname,'simulator.html'),'utf8').replace(/\{\{(\w+)\}\}/g,(_,key)=>{if(!(key in values))throw Error('Unknown template token: '+key);return values[key];});
}
if(require.main===module){
 const check=process.argv.includes('--check'),outputs=[['docs/development/data/catalogs/ink-simulator-part-catalog.md',generate()]];
 if(process.argv.includes('--worksheet'))throw new Error('설계 작성지는 새 발동 조건×효과 명세의 수동 편집 문서입니다. --worksheet로 덮어쓸 수 없습니다.');
 if(process.argv.includes('--preview'))outputs.push(['outputs/ink-combination-lab/ink-parts.html',generateSimulator()],['outputs/ink-combination-lab/encyclopedia.html',generatePreview()]);
 for(const [file,content]of outputs){const target=path.join(root,file);if(check){if(!fs.existsSync(target)||fs.readFileSync(target,'utf8')!==content){console.error('원본 불일치: '+file);process.exitCode=1;}}else{fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,content);}}
 if(!process.exitCode)console.log(`50종 ${check?'원본 일치':'생성'}: ${outputs.length}개 파일`);
}
module.exports={generate,generatePreview,generateSimulator};
