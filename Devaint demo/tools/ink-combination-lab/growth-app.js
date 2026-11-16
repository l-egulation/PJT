(function(){
'use strict';
const $=id=>document.getElementById(id),data=id=>JSON.parse($(id).textContent);
const catalog=data('ink-data'),tuning=data('tuning-data'),economy=data('economy-data');
const g=new InkGrowth.Growth(catalog,tuning,economy),names={red:'빨강',yellow:'노랑',green:'초록',blue:'파랑',purple:'보라',colorless:'무색'};
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let enabled=false,owner=0,slot=0,firstColor='red',secondColor='blue',pendingPalettes=0,note='시작 팔레트·골드 없이 시작합니다. 보상 시험 또는 시험 재료로 확인하세요.';
function pattern(count){if(count===3)return Array(3).fill(firstColor).map(c=>Math.random()<tuning.palettes.colorlessChancePerSlot?'colorless':c);if(count===4){if(firstColor===secondColor)throw Error('4칸 팔레트는 서로 다른 두 색을 선택하세요.');return[firstColor,firstColor,secondColor,secondColor].map(c=>Math.random()<tuning.palettes.colorlessChancePerSlot?'colorless':c);}return Array.from({length:5},()=>g.choose(Object.entries(tuning.palettes.colorWeights),Math.random));}
function apply(){if(enabled)inkLab.setGrowth(g.combatConfig());}
function act(fn,sync=false){try{const result=fn();note=typeof result==='string'?result:'완료했습니다.';if(sync)apply();}catch(e){note=e.message;}render();}
function choices(selected){return Object.entries(names).filter(([k])=>k!=='colorless').map(([id,name])=>`<option value="${id}" ${id===selected?'selected':''}>${name}</option>`).join('');}
function render(){
 const pal=g.equipped[owner]?g.palette(owner):null;
 if(pal&&slot>=pal.slots.length)slot=0;
 $('growth-panel').innerHTML=`<div class="section-head"><h2>MVP 정비소 · 20종</h2><button id="g-mode" aria-pressed="${enabled}">${enabled?'자유 실험으로 전환':'MVP 모드 시작'}</button></div><p class="help">3칸 단색 · 4칸 두 색 2+2 · 5칸 무작위 / 최초 무기 귀속 / 빈칸 무료 · 교체 유료 / 슬롯 최대 +5<br>현재 세션만 유지합니다. 전투 초기화는 재고·골드를 유지합니다. 3·4칸은 기본 색을 고른 뒤 각 칸이 10% 확률로 무색으로 바뀝니다.</p><div class="row"><strong id="g-balance">${g.gold}G</strong><span>뽑기 ${g.price('inkDraw')}G · 잉크 판매 ${g.price('inkDraw')*.2}G/개</span><button id="g-draw" ${!enabled?'disabled':''}>잉크 뽑기</button><button id="g-attack-up" ${!enabled?'disabled':''}>공통 공격 +${g.attackLevel} → 강화 ${g.price('statUpgrade')}G</button></div><p id="g-message" role="status">${esc(note)}</p>
 <details ${enabled?'open':''}><summary>보상·재료 시험 도구</summary><p class="help">실제 런 진행을 대신하는 시험 지급입니다. 팔레트 보상은 구성 확률이 미정인 색상에 대해 아래 선택값을 사용합니다.</p><div class="row"><button data-reward="normal">일반 보상 시험</button><button data-reward="elite">엘리트 보상 시험</button><button data-reward="boss">보스 보상 시험</button><button id="g-materials">시험 재료: 2000G + MVP 각 1개</button></div></details>
 <div class="growth-grid"><section><h3>팔레트 획득</h3><div class="row"><label>첫 색<select id="g-first">${choices(firstColor)}</select></label><label>둘째 색<select id="g-second">${choices(secondColor)}</select></label></div><p class="help">구매 ${g.price('palette')}G · 미수령 팔레트 보상 ${pendingPalettes}개</p><div class="row">${[3,4,5].map(n=>`<button data-buy-palette="${n}" ${!enabled?'disabled':''}>${n}칸 구매</button><button data-claim-palette="${n}" ${!enabled||!pendingPalettes?'disabled':''}>${n}칸 보상 수령·시험</button>`).join('')}</div><p class="help">상점의 칸 수·재고 갱신과 미정 색상 확률을 자동 확정하지 않도록 구성을 직접 골라 시험합니다.</p><div id="g-palettes">${g.palettes.map(p=>`<p>${p.slots.map(s=>names[s.color]).join(' / ')} · ${p.boundTo===null?'미귀속':(p.boundTo?'B':'A')+' 귀속'} <button data-bind="${p.uid}" ${!enabled?'disabled':''}>${g.equipped[owner]&&g.equipped[owner]!==p.uid?'교체 · 기존 팔레트·잉크 소멸':(owner?'B':'A')+'에 장착'}</button></p>`).join('')||'<p>보유 팔레트 없음</p>'}</div></section>
 <section><h3>잉크 장착·슬롯 강화</h3><div class="row"><label>대상 무기<select id="g-owner"><option value="0" ${owner===0?'selected':''}>A</option><option value="1" ${owner===1?'selected':''}>B</option></select></label><label>슬롯<select id="g-slot">${pal?pal.slots.map((s,i)=>`<option value="${i}" ${i===slot?'selected':''}>${i+1} · ${names[s.color]} · +${s.level}</option>`).join(''):'<option value="0">팔레트 필요</option>'}</select></label><button id="g-upgrade" ${!enabled||!pal?'disabled':''}>슬롯 강화 ${g.price('slotUpgrade')}G</button></div><div id="g-slots">${pal?pal.slots.map((s,i)=>`<p>${i+1}. ${names[s.color]} +${s.level} · ${s.partId?esc(g.parts.get(s.partId).name):'빈칸'}</p>`).join(''):'팔레트를 먼저 장착하세요.'}</div><p class="help">장착 잉크를 무료로 해제·판매하는 동작은 제공하지 않습니다. 색상 변경은 점유 칸에 새 잉크와 동시에 1회·60G입니다.</p><div id="g-inks">${g.inks.map(i=>{const p=g.parts.get(i.partId),r=g.rows.get(i.partId);return`<div class="growth-ink"><span>${esc(p.name)} · ${names[p.color]} · ${r.fixedGrade}등급</span><p class="help">${esc(p.effect.description)}</p><div class="row"><button data-insert="${i.uid}" ${!enabled||!pal?'disabled':''}>선택 칸에 ${pal?.slots[slot]?.partId?'교체 '+(80+r.fixedGrade*20)+'G':'무료 장착'}</button><button data-recolor="${i.uid}" ${!enabled||!pal?'disabled':''}>색상 변경+교체</button><button data-sell="${i.uid}" ${!enabled?'disabled':''}>판매 ${g.price('inkDraw')*.2}G</button></div></div>`;}).join('')||'<p>보유 잉크 없음</p>'}</div></section></div>`;
 for(const el of document.querySelectorAll('.library,.settings,#weapons,#apply-example,#example')){el.hidden=enabled;}
 $('example').closest('label').hidden=enabled;
 const oldHelp=document.querySelector('.loadout-panel>.help');if(oldHelp)oldHelp.hidden=enabled;
 $('g-mode').onclick=()=>{enabled=!enabled;inkLab.setGrowth(enabled?g.combatConfig():null);note=enabled?'MVP 규칙을 전투에 적용했습니다.':'자유 실험 모드입니다.';render();};
 $('g-first').onchange=e=>{firstColor=e.target.value;};$('g-second').onchange=e=>{secondColor=e.target.value;};
 $('g-owner').onchange=e=>{owner=Number(e.target.value);slot=0;render();};$('g-slot').onchange=e=>{slot=Number(e.target.value);render();};
 $('g-draw').onclick=()=>act(()=>{const ink=g.draw();return g.parts.get(ink.partId).name+' 획득';});
 $('g-attack-up').onclick=()=>act(()=>g.upgradeAttack(),true);$('g-upgrade').onclick=()=>act(()=>g.upgrade(owner,slot),true);
 $('g-materials').onclick=()=>act(()=>{g.gold+=2000;for(const row of tuning.partTuning)g.grantInk(row.id);return '시험 재료 지급: 2000G + MVP 20개';});
 document.querySelectorAll('[data-reward]').forEach(b=>b.onclick=()=>act(()=>{const r=g.reward(b.dataset.reward);if(r.paletteDue)pendingPalettes++;return `${r.gold}G · 잉크 ${r.inks.length}개 · 팔레트 보상 ${r.paletteDue?1:0}개`; }));
 document.querySelectorAll('[data-buy-palette]').forEach(b=>b.onclick=()=>act(()=>g.buyPalette(pattern(Number(b.dataset.buyPalette)))));
 document.querySelectorAll('[data-claim-palette]').forEach(b=>b.onclick=()=>act(()=>{if(pendingPalettes<1)throw Error('미수령 보상이 없습니다.');g.grantPalette(pattern(Number(b.dataset.claimPalette)));pendingPalettes--;}));
 document.querySelectorAll('[data-bind]').forEach(b=>b.onclick=()=>act(()=>g.bind(b.dataset.bind,owner),true));
 document.querySelectorAll('[data-insert]').forEach(b=>b.onclick=()=>act(()=>g.insert(owner,slot,b.dataset.insert),true));
 document.querySelectorAll('[data-recolor]').forEach(b=>b.onclick=()=>act(()=>{const ink=g.ink(b.dataset.recolor);g.recolor(owner,slot,g.parts.get(ink.partId).color,ink.uid);},true));
 document.querySelectorAll('[data-sell]').forEach(b=>b.onclick=()=>act(()=>`잉크 판매 +${g.sell(b.dataset.sell)}G`));
}
window.inkGrowthLab={get state(){return g;},get enabled(){return enabled;},render};render();
})();
