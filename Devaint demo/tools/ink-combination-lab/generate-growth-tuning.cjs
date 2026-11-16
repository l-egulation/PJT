'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../..'),read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const policy=read('data/combat/weapon_growth_mvp_proposal.json'),draft=read('data/combat/weapon_growth_mvp_tuning_draft.json'),catalog=read('data/combat/ink_simulator_parts_proposal.json'),economy=read('data/combat/chapter1_economy.json');
const parts=new Map(catalog.parts.map(p=>[p.id,p]));
const colors={red:'빨강',yellow:'노랑',green:'초록',blue:'파랑',purple:'보라'};
const families={damage:'공격',healing:'회복',shield:'보호막',move_speed:'이속',range:'범위',cooldown:'쿨타임 회수',crowd_control:'CC'};
const pct=n=>Math.round(n*10000)/100+'%';
function verify(){
 assert.equal(draft.status,'accepted_test_baseline_implementation_pending');
 assert.deepEqual(draft.partTuning.map(p=>p.id).sort(),[...catalog.mvp.partIds].sort());
 assert.equal(new Set(draft.partTuning.map(p=>p.id)).size,20);
 for(const row of Object.values(draft.gradeWeights)){assert.equal(row.length,4);assert.ok(row.every(n=>n>=0&&n<=1));assert.ok(Math.abs(row.reduce((a,b)=>a+b,0)-1)<1e-9);}
 for(const pool of [draft.palettes.slotWeights,draft.palettes.colorWeights])assert.ok(Math.abs(Object.values(pool).reduce((a,b)=>a+b,0)-1)<1e-9);
 assert.ok(draft.palettes.slotWeights[3]>draft.palettes.slotWeights[4]&&draft.palettes.slotWeights[4]>draft.palettes.slotWeights[5]);
 for(const values of Object.values(draft.upgradeMultipliers)){assert.equal(values.length,6);assert.equal(values[0],1);assert.ok(values.every((v,i)=>Number.isFinite(v)&&v>=1&&(!i||v>=values[i-1])));}
 for(const p of draft.partTuning){assert.ok(Number.isInteger(p.fixedGrade)&&p.fixedGrade>=1&&p.fixedGrade<=4);assert.equal(typeof p.duplicateEquip,'boolean');assert.ok(p.internalCooldownSeconds>=0);for(const u of p.upgradeTargets){assert.equal(typeof p.baseParams[u.parameter],'number');assert.ok(draft.upgradeMultipliers[u.family]);}}
 for(let grade=1;grade<=4;grade++)assert.ok(draft.partTuning.some(p=>p.fixedGrade===grade));
 assert.equal(policy.palette.activation.minimumFilledSlots,1);assert.equal(policy.palette.activation.craftCostModel,'no_separate_initial_crafting');
 assert.equal(policy.inkQuality.fixedGradeByPart,null);assert.equal(policy.markSystem.intrinsicDamage.coefficientPerMark,null);
 assert.equal(draft.rewards.normal.inkCount,policy.inkAcquisition.normalEnemy.countOnSuccess);
 assert.equal(policy.inkAcquisition.sale.priceBasis,'current_ink_draw_price');assert.equal(draft.costs.inkSale.saleIncrementsDrawPrice,false);for(const price of [40,60,80,100])assert.equal(price*draft.costs.inkSale.drawPriceFraction*5,price);
 assert.equal(policy.slotUpgrade.maxLevel,5);assert.equal(draft.mark.coefficientAffectedByPaletteSlotLevel,false);
}
function parameterText(kind,key,value){
 if(key==='ratio'||key==='amount'&&['move_speed','next_attack_power','next_attack_reach','area_radius_buff','slow','max_health_heal','lifesteal'].includes(kind))return pct(value);
 if(key==='duration')return value+'초';if(key==='seconds')return value+'초';if(key==='distance'||key==='radius'||key==='range'||key==='width')return value+'unit';return String(value);
}
function describe(row,level){
 const p=parts.get(row.id),v={...row.baseParams};for(const u of row.upgradeTargets)v[u.parameter]*=draft.upgradeMultipliers[u.family][level];
 const k=p.effect.kind,a=v.amount,r=v.ratio;
 const n=x=>Math.round(x*1000)/1000;
 if(k==='extra_hit')return '적중 피해 '+pct(r)+' 추가타';
 if(k==='next_attack_power')return '다음 공격 피해 +'+pct(a);
 if(k==='detonation_extra_hit')return '기본 공격 기준 '+pct(r)+' 추가타';
 if(k==='move_speed')return '이속 +'+pct(a)+' / '+v.duration+'초';
 if(k==='lifesteal')return '실제 피해 '+pct(r)+' 흡혈';
 if(k==='shield')return '보호막 '+n(a)+' / '+v.duration+'초';
 if(k==='heal')return '체력 '+n(a)+' 회복';
 if(k==='max_health_heal')return '최대 체력 '+pct(r)+' 회복';
 if(k==='cooldown_reduce')return '남은 쿨타임 −'+n(v.seconds)+'초';
 if(k==='next_attack_reach')return '다음 공격 사거리 +'+pct(a);
 if(k==='area_radius_buff')return '공격 반경 +'+pct(a)+' / '+v.duration+'초';
 if(k==='slow')return '둔화 '+pct(a)+' / '+v.duration+'초';
 if(k==='pull')return '끌기 '+n(v.distance)+'unit / 반경 '+v.radius;
 if(k==='root')return '속박 '+n(v.duration)+'초';
 throw Error('Missing display handler: '+k);
}
function generate(){
 const lines=['# MVP 무기 성장 수치 초안','','**사용자가 채택한 테스트 기준이다.** 확정된 시스템 방향과 구분하며, 이 파일의 수치를 브라우저·Unity·드롭·상점에 자동 적용하지 않는다. 실제 플레이 밸런스나 Unity 검증을 완료한 값이 아니다.','','편집 원본: [`weapon_growth_mvp_tuning_draft.json`](../../../../data/combat/weapon_growth_mvp_tuning_draft.json). 확정된 방향은 [`weapon_growth_mvp_proposal.json`](../../../../data/combat/weapon_growth_mvp_proposal.json), 파츠 이름·효과는 [MVP 목록](ink-simulator-part-catalog.md#mvp-우선-채택-20종)을 따른다. `node tools/ink-combination-lab/generate-growth-tuning.cjs`로 재생성하고 `--check`로 검사한다.','','**현재 Unity 적용 범위**: 아래 파츠별 표는 이전 MVP 20종의 브라우저 시뮬레이터 기준이다. Unity 런의 색상 잉크는 [개편안 53종](ink-color-parts.md)과 [`ink_color_parts_tuning.json`](../../../../data/combat/ink_color_parts_tuning.json)을 쓰며, 획득처 확률은 이 문서의 원본을 공유한다. 현행 강화는 계열 배율 대신 파츠별 명시적 6단계 테이블을 쓰며 가격은 `chapter1_economy.json`의 슬롯별 50/100/180/350/600G다. 아래 이전 MVP의 가격·강화 배율표는 현행 색상 잉크에 적용하지 않는다.','','## 확정 방향과 제안 수치의 구분','','확정: MVP 20종, 시작 팔레트 없음, 첫 엘리트 팔레트 1개, 팔레트 3~5칸·칸별 무작위 색, 회당 슬롯 1개·런당 횟수 제한(1차 시험값) 유료 색상 변경, 장착·활성화 후 빈칸 추가 무료, 최소 잉크 1개로 제작, 드롭·상점 구매로 팔레트 획득, 최초 장착 무기에 귀속·다른 무기로 이전 불가, 별도 최초 제작비 없음, 슬롯 +0..+5와 파츠별 강화 배율, 파츠당 고정 등급 하나, 획득처별 등급 확률, 원타격 표식 1개·최대 7중첩(빨·주·노·초·파·남·보 순서)·8초·전체 갱신·다른 무기 적중 시 역순으로 전량 터트리기·신규 표식 1개, 일반 드롭 성공 시 잉크 1개다.','','아래 수치는 테스트 기준으로 채택했다. 팔레트 최초 제작비는 폐기하고 슬롯 수별 색상 구성을 적용한다. 잉크 판매가 기준 등 미정 항목은 별도로 표시한다. 확정 방향 원본의 실제 수치 필드는 런타임 이행 전까지 비워 둔다.','','## 획득량·확률','','| 획득처 | 색상 잉크 | 팔레트 |','|---|---|---|'];
 for(const [key,label]of [['normal','일반'],['elite','엘리트'],['boss','보스']]){const r=draft.rewards[key];lines.push(`| ${label} | ${pct(r.inkChance)} / 성공 시 ${r.inkCount}개 | ${pct(r.paletteChance)} / 성공 시 ${r.paletteCount}개 |`);}
 lines.push('','첫 엘리트는 일반 팔레트 추첨을 대신해 1개를 확정 지급한다. 확정 1개에 50% 보상을 더해 2개를 주지 않는다. 일반/엘리트/보스의 잉크는 신규 MVP 색상 파츠를 기준으로 한 제안이며 기존 구형 잉크 지급과의 이중 지급을 구현하지 않는다.','','| 획득처 | 1등급 | 2등급 | 3등급 | 4등급 |','|---|---|---|---|---|');
 for(const [key,label]of [['normal','일반'],['elite','엘리트'],['boss','보스'],['maintenance','정비소 뽑기']])lines.push(`| ${label} | ${draft.gradeWeights[key].map(pct).join(' | ')} |`);
 lines.push('','각 파츠는 표에 배정된 고정 등급으로만 등장한다. 먼저 획득처별 등급을 추첨하고, 그 등급의 파츠 중 장착 팔레트 색상은 가중치 3, 다른 색은 1로 추첨하는 안이다. 동일 색 칸 수만큼 가중치를 반복 곱하지 않는다. 무색 칸은 특정 색 가중치를 만들지 않으며, 팔레트가 없으면 모든 후보의 기본 가중치 1을 사용한다. 최종 색상 확률은 등급 풀의 파츠 구성에도 영향을 받는다. 중복 제외 등으로 비어 있는 등급은 제외하고 남은 등급의 확률을 재정규화한다. 중복 장착 불가 파츠도 재드롭한다.','','## 팔레트 구성','','| 칸 수 | 확률 |','|---|---|');
 for(const [count,w]of Object.entries(draft.palettes.slotWeights))lines.push(`| ${count}칸 | ${pct(w)} |`);
 lines.push('','3칸: 같은 색 3개. 4칸: 서로 다른 두 색 2개씩. 5칸: 무작위이며 기존 시험 가중치(각 색 18%, 무색 10%)를 적용한다. 3·4칸은 다섯 기본 색을 균등 추첨한 기본 구성을 만들고 각 칸이 독립적으로 10% 확률로 무색이 된다. 슬롯 수가 적어도 집중 구성을 선택할 가치가 있도록 한다.','','## 20종의 등급·기본 수치·강화 결과','','+0과 +5는 같은 파츠의 동일한 내부 등급에서 슬롯 강화만 바꾼 결과다. +5가 새로운 잉크 등급이라는 뜻은 아니다. 지속시간·반경·내부 쿨타임은 강화 대상에 명시된 항목이 아니면 유지한다. 중복 허용은 장착 기준이며 드롭 횟수 정책을 뜻하지 않는다.','','| 색상 | 파츠 | 트리거 | 고정 등급 | +0 | +5 | 중복 장착 | 내부 쿨타임 |','|---|---|---|---|---|---|---|');
 for(const row of draft.partTuning){const p=parts.get(row.id);lines.push(`| ${colors[p.color]} | ${p.name} | ${catalog.triggers[p.trigger]} | ${row.fixedGrade} | ${describe(row,0)} | ${describe(row,5)} | ${row.duplicateEquip?'가능':'무기당 1개'} | ${row.internalCooldownSeconds}초${row.cooldownScope==='instance_and_target'?'·대상별':''} |`);}
 lines.push('','각 쿨타임은 파츠 인스턴스별이다. 대상별 표시가 있는 파츠는 같은 인스턴스라도 대상마다 따로 검사한다. 중복 가능 파츠는 인스턴스별로 발동·쿨타임을 관리한다. 다른 무기 강화는 기존 파츠의 예외 표기를 따르고 나머지는 장착 무기 자신에게만 적용한다.','','## 슬롯 강화 배율','','| 효과 계열 | +0 | +1 | +2 | +3 | +4 | +5 |','|---|---|---|---|---|---|---|');
 for(const [family,values]of Object.entries(draft.upgradeMultipliers))lines.push(`| ${families[family]} | ${values.map(v=>'×'+v.toFixed(2)).join(' | ')} |`);
 lines.push('','공격은 추가타 피해 비율·다음 공격 증가율, 회복은 회복량·흡혈 비율, 보호막은 보호막량, 이속은 증가율, 범위는 사거리·반경 증가율, 쿨타임은 회수 초, CC는 해당 파츠의 둔화량·끌기 거리·속박시간을 강화한다. CC의 반경과 다른 지속시간은 표의 강화 대상이 아니면 바꾸지 않는다. 파츠를 교체하면 같은 슬롯 레벨을 새 파츠의 계열 배율에 적용한다.','','## 제작·교체·성장 비용','','| 항목 | 제안 비용 |','|---|---|','| 팔레트 획득 | 드롭 또는 상점 구매. 별도 최초 제작비 없음 |','| 장착 / 활성화 후 빈칸 추가 | 무료 |','| 같은 색 잉크 교체 | 무료. 인벤토리(Tab)에서 전투 중에도 언제든 반복 교체할 수 있고, 빠진 잉크는 보유 목록으로 돌아간다 |',`| 팔레트 색상 변경 | 런당 ${draft.costs.colorChangesPerRun}회(두 팔레트 공용), 회당 ${draft.costs.colorChangeGold}G, 필요한 잉크 교체 포함. 별도 부분 교체비 중복 청구 없음. 바뀐 칸의 기존 잉크는 보유 목록으로 돌아감 |`,'| 새 팔레트 재제작 | 추가 비용 없음. 기존 팔레트·잉크 소멸 및 새 팔레트 +0 |');
 lines.push('| 색상 잉크 판매 | 현재 뽑기 비용의 20% 수령: 40/60/80G → 개당 8/12/16G. 판매 5개 = 현재 뽑기 1회. 판매는 가격을 올리지 않고 성공 뽑기만 가격 증가. 장착 잉크 직접 판매 범위 미정 |');
 const labels={palette:'팔레트 구매',inkDraw:'정비소 잉크 뽑기',statUpgrade:'캐릭터 스탯 강화',slotUpgrade:'팔레트 슬롯 강화'};
 for(const p of economy.prices)if(labels[p.id])lines.push(`| ${labels[p.id]} | ${p.start}G부터 성공 횟수마다 +${p.step}G — 기존 시험 가격 유지안 |`);
 lines.push('','기존 가격 트랙은 런 전체의 정비소에서 성공 횟수를 공유한다. 슬롯 +5까지 한 칸에만 연속 투자하면 50+75+100+125+150=500G이며, 다른 슬롯에서 이미 강화한 횟수가 있으면 더 비싸다. 색상 변경 가격에 교체를 포함하는 정책은 별도 제안이다. 빈칸 색상 변경의 새 잉크 필요 여부와 무색 칸 변경 범위는 아직 결정하지 않는다.','','캐릭터 공통 공격 성능은 레벨당 기준값의 +5%, 최대 +10을 제안한다. 매 단계 곱하지 않고 +10에서 합계 +50%다. 기본·스킬·잉크 및 격발의 무기 기본 공격 기준값에 적용하며 고정 회복·이속·CC까지 일괄 증폭하지 않는다. 캐릭터 최대 +10과 팔레트 슬롯 최대 +5를 구분한다.','','## 표식·연쇄·공통 안전 제안','',`기본 격발 피해 = 격발 무기 기본 공격 기준값 × 소비 표식 수 × ${draft.mark.coefficientPerConsumedMark}. 7개면 기준값의 105%다. 기본 공격 기준값이 20이면 3개 격발 9, 7개 격발 21 피해이며, 이는 계산 예시다. 슬롯 강화는 이 공통 계수를 높이지 않고 장착 격발 파츠의 추가 효과만 강화한다. 콤보 무기의 기준값은 기본 콤보 각 타격 피해의 산술평균이다.`,
 '',`추가 제안: 추가타 연쇄 깊이 ${draft.proposedSafety.chainDepth}, 이속 증가 합계 최대 ${pct(draft.proposedSafety.movementSpeedBonusCap)}, 흡혈 합계 최대 ${pct(draft.proposedSafety.lifeStealFractionCap)}, 강제 제어 후 회복 ${draft.proposedSafety.hardControlRecoverySeconds}초, 보스 CC 지속·변위 ${draft.proposedSafety.bossControlDurationAndDisplacementScale}배, 같은 원행동의 대상별 누적 강제 변위 최대 ${draft.proposedSafety.combinedDisplacementCapPerTargetPerSourceAction}unit. 모두 테스트 기준이며 포화 시 추가 중복의 효율이 낮아질 수 있다.`,
 '','## 효과 합성·연쇄 처리 제안','','다음 공격 피해 증가가 동시에 준비되면 같은 공격의 증가율끼리 합산하고 공격 시작 시 함께 소비하는 안이다. 예를 들어 +30%와 +25%는 ×1.55다. 캐릭터 공통 공격 성장 배율과는 별도로 곱한다. 중복 장착한 가벼운 발끝은 슬롯별 수치·지속시간을 독립 관리하고 증가율을 더하되 이속 합계 50% 상한을 적용하는 안이다.','','연쇄 깊이는 원타격 0, 그 피해에서 생성한 추가 피해 1, 다시 생성한 추가 피해 2로 제안한다. 기본 격발 피해는 원타격에서 생성한 추가 피해로 시작한다. 깊이 상한에서는 새로운 추가 피해 생성만 막고 이미 확정된 적중·처치의 회복·자원 효과 등은 처리한다. 파츠 내부 쿨타임은 파생 이벤트를 큐에 넣기 전에 예약해 슬롯 순서에 따른 재귀 차이를 막는 안이다. 테스트 기준이며 구현 완료로 간주하지 않는다.','','기존 명세에 따라 새 팔레트 재제작 시 기존 팔레트·내부 잉크는 소멸하고 슬롯 강화는 초기화한다. 아래 미정인 단순 탈착·이전·교환과 유료 재제작의 소멸을 구분한다.','','## 경제 비교와 남은 결정','','기존 시험 골드는 일반 6~10G, 엘리트 200~280G, 보스 600~720G다. 첫 엘리트의 팔레트는 별도 제작비 없이 사용할 수 있다. 첫 슬롯 강화 50G + 첫 뽑기 40G = 90G다. 상점에서 팔레트를 구매하면 구매비가 필요하다. 전체 런 평균 획득량·소비량·체감은 실게임에서 검증하지 않았다.','',...draft.pendingDecisions.map(s=>'- '+s),'',...draft.notes.map(s=>'- '+s),'');
 lines.splice(1,0,'','[카탈로그 개요](README.md)','','> 적용 범위: 현재 코드·생성기·시험 도구의 구현 자료이며 제품 성장 기획이 아니다. 새 기준은 [무기 도면·런 잉크 도감](../../../design/equipment/rules.md)이다. 아래 고정 파츠·등급·팔레트 수치를 새 잉크 레벨·보상에 승계하지 않는다.');
 return lines.join('\n');
}
verify();const output=path.join(root,'docs/development/data/catalogs/weapon-growth-mvp-tuning.md'),content=generate();
if(process.argv.includes('--check')){assert.equal(fs.readFileSync(output,'utf8'),content,'Tuning document stale');console.log('PASS: tuning draft probabilities, 20 fixed grades, upgrade tables, policy separation, and generated document');}
else{fs.writeFileSync(output,content);console.log('Generated pending-review MVP tuning document');}
