(function(){
'use strict';
const $=id=>document.getElementById(id),read=id=>JSON.parse($(id).textContent);
const catalog=read('ink-data'),experiment=read('experiment-data'),builds=read('build-data');
const parts=new Map(catalog.parts.map(p=>[p.id,p])),colors={red:'#fa8c84',yellow:'#e5cb74',green:'#81d3ad',blue:'#85bce8',purple:'#c5a2ec'};
const colorNames={red:'빨강 · 공격',yellow:'노랑 · 이동 속도',green:'초록 · 생명·보호막·회복',blue:'파랑 · 범위·쿨타임',purple:'보라 · CC'};
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const storageKey='deviant-ink-simulator-v2-50';
const defaultLoadout=[['ink2_red_01','ink2_red_04','ink2_green_04','ink2_blue_05','ink2_yellow_04','ink2_purple_04'],['ink2_red_07','ink2_green_07','ink2_blue_07','ink2_yellow_07','ink2_purple_07','ink2_blue_02']];
let growthConfig=null;
let options={...experiment.defaults,profiles:[...experiment.defaults.profiles]},loadout=defaultLoadout.map(v=>[...v]),editOwner=0,sim,paused=false,last=0,renderAt=0,keys=new Set(),pad={x:0,y:0};
const numeric={marksPerHit:[1,5],markCap:[1,20],markDuration:[1,30],detonationDamage:[0,100],chainLimit:[0,6],internalCooldown:[0,5],bossCcScale:[0,1],ccRecovery:[0,5]};
const flags=['enemyAI','respawn','marksOnExtra','allowDuplicates','autoAim'];
const selectors={markMode:['hit','manual'],markConsume:['all','one'],modifierPolicy:['strongest','sum']};
function validate(saved){
 if(!saved||!Array.isArray(saved.loadout)||saved.loadout.length!==2||saved.loadout.some(ids=>!Array.isArray(ids)||ids.length>6||ids.some(id=>!parts.has(id))))throw Error('이전 파츠 목록');
 const o=saved.options;if(!o||!experiment.scenarios[o.scenario]||!Array.isArray(o.profiles)||o.profiles.length!==2||o.profiles.some(id=>!experiment.profiles.some(p=>p.id===id)))throw Error('무기 설정');
 for(const [key,[min,max]]of Object.entries(numeric))if(!Number.isFinite(o[key])||o[key]<min||o[key]>max)throw Error(key);
 for(const key of flags)if(typeof o[key]!=='boolean')throw Error(key);
 for(const [key,values]of Object.entries(selectors))if(!values.includes(o[key]))throw Error(key);
 if(!o.allowDuplicates&&saved.loadout.some(ids=>new Set(ids).size!==ids.length))throw Error('중복 설정');
 return saved;
}
let restoreWarning='';try{const raw=localStorage.getItem(storageKey);if(raw){const saved=validate(JSON.parse(raw));loadout=saved.loadout;options={...options,...saved.options};}}catch{restoreWarning='호환되지 않는 저장 설정을 제외하고 기본 조합을 불러왔습니다.';}
function save(){try{localStorage.setItem(storageKey,JSON.stringify({options,loadout}));}catch{}}
function message(text){$('message').textContent=text;}
function reset(text='전투 상태를 초기화했습니다. 파츠 구성은 유지됩니다.'){
 sim=new InkSimulator.Simulator(catalog,experiment,{...options,loadout,growthConfig});last=0;renderAt=0;keys.clear();pad={x:0,y:0};
 renderWeapons();renderLibrary();update();draw();save();message(text);
}
function renderWeapons(){
 $('weapons').innerHTML=[0,1].map(owner=>{
  const profile=experiment.profiles.find(w=>w.id===options.profiles[owner]);
  return `<section class="weapon" data-owner="${owner}"><div class="weapon-top"><strong>무기 ${owner?'B':'A'} · ${loadout[owner].length}/6</strong><span>${sim.active===owner?'현재 사용 중':'대기 중'}</span></div><label>시험용 무기 프리셋<select data-profile="${owner}">${experiment.profiles.map(w=>`<option value="${w.id}" ${w.id===profile.id?'selected':''}>${esc(w.name)}</option>`).join('')}</select></label><p class="profile-help">기본 ${profile.basicDamage} / 스킬 ${profile.skillDamage} · 사거리 ${profile.basicReach} / ${profile.skillReach}<br>스킬 쿨타임 ${profile.cooldown}초 · 반경 ${profile.basicRadius} / ${profile.skillRadius}</p><div class="slots">${Array.from({length:6},(_,slot)=>{const p=parts.get(loadout[owner][slot]);return p?`<button style="--ink:${colors[p.color]}" data-remove="${owner}:${slot}" aria-label="${owner?'B':'A'} ${esc(p.name)} 해제">${esc(p.name)} ×</button>`:'<span class="empty-slot">＋ 빈 칸</span>';}).join('')}</div></section>`;
 }).join('');
 $('edit-a').setAttribute('aria-pressed',String(editOwner===0));$('edit-b').setAttribute('aria-pressed',String(editOwner===1));
}
function renderLibrary(){
 const color=$('color').value,trigger=$('trigger').value,query=$('query').value.trim().toLowerCase();
 const found=catalog.parts.filter(p=>(color==='all'||p.color===color)&&(trigger==='all'||p.trigger===trigger)&&[p.name,p.goal,p.effect.description,p.combo].join(' ').toLowerCase().includes(query));
 $('count').textContent=found.length+' / 50종';$('empty').hidden=found.length>0;
 $('part-grid').innerHTML=found.map(p=>{
  const count=loadout[editOwner].filter(id=>id===p.id).length,badges=loadout.map((ids,owner)=>{const n=ids.filter(id=>id===p.id).length;return n?`${owner?'B':'A'} ×${n}`:'';}).filter(Boolean).join(' · ');
  return `<article class="part" style="--ink:${colors[p.color]}"><span class="part-tag">${esc(colorNames[p.color])} · ${esc(catalog.triggers[p.trigger])}</span><h3>${esc(p.name)}</h3><span class="part-badges">${badges||'미장착'}</span><p>${esc(p.effect.description)}</p><p class="goal">${esc(p.goal)}</p><details><summary>대상·제약·연계</summary><p>대상: ${esc(p.effect.target)}</p><p>${esc(p.limits)}</p><p>연계: ${esc(p.combo)}</p><p>내부 쿨타임·중복·CC는 위 실험 설정을 따릅니다.</p></details><button data-equip="${p.id}" aria-pressed="${count>0}">${editOwner?'B':'A'}에 ${count&&!options.allowDuplicates?'장착 해제':'장착'}${count&&options.allowDuplicates?' · 추가 실험':''}</button></article>`;
 }).join('');
}
function equip(id){
 const ids=loadout[editOwner],p=parts.get(id);if(!p)return;
 if(!options.allowDuplicates&&ids.includes(id)){loadout[editOwner]=ids.filter(v=>v!==id);reset(p.name+' 해제');return;}
 if(ids.length>=6){message('팔레트가 가득 찼습니다. 장착 칸을 눌러 먼저 해제하세요.');return;}
 ids.push(id);reset(p.name+' → 무기 '+(editOwner?'B':'A')+' 장착');
}
function perform(kind,owner){
 if(paused){message('일시정지를 해제한 뒤 공격하세요.');return;}
 const before=sim.active;
 if(sim.attack(kind,owner)){if(before!==sim.active)renderWeapons();message((before!==owner?'떠나는 무기의 효과 → 교대 → ':'')+(owner?'B':'A')+' '+(kind==='skill'?'스킬':'기본 공격'));}
 else message(sim.player.hp<=0?'플레이어가 쓰러졌습니다. 전투를 초기화하세요.':sim.cooldowns[owner]>0&&kind==='skill'?'스킬 쿨타임 중입니다.':'공격 동작이 끝난 뒤 다시 입력하세요.');
 update();draw();
}
function switchAttack(){if(paused){message('일시정지를 해제하세요.');return;}const to=1-sim.active;if(sim.switchWeapon(to)){renderWeapons();message((to?'A':'B')+' 효과 발동 → '+(to?'B':'A')+' 전환 → 자동 기본 공격');}else message('공격 중이거나 플레이어가 쓰러졌습니다.');update();draw();}
function movement(){return{x:(keys.has('ArrowRight')?1:0)-(keys.has('ArrowLeft')?1:0)+pad.x,y:(keys.has('ArrowDown')?1:0)-(keys.has('ArrowUp')?1:0)+pad.y};}
const n=(v,d=1)=>Number(v).toFixed(d),ownerName=v=>v===0?'A':'B';
function logLine(e){
 const name=e.part?parts.get(e.part)?.name||e.part:'';
 if(e.type==='proc')return `${ownerName(e.owner)} · ${name} [${catalog.triggers[e.trigger]}]`;
 if(e.type==='switch')return `${ownerName(e.owner)} → ${ownerName(e.incoming)} 전환 완료`;
 if(e.type==='attack')return `${ownerName(e.owner)} ${e.kind==='skill'?'스킬':'기본'} 시작 · 피해 ${n(e.damage)}`;
 if(e.type==='detonate')return `${ownerName(e.owner)} · 표식 ${e.count}개 격발`;
 if(e.type==='kill')return `${ownerName(e.owner)} · 몬스터 ${e.targetId+1} 처치`;
 if(e.type==='hit')return `${ownerName(e.owner)} · ${e.extra?'추가타':'원타격'} ${n(e.damage)} 피해`;
 if(e.type==='received')return `피격 ${n(e.amount)} · 보호막 흡수 ${n(e.absorbed)}`;
 return null;
}
function update(){
 const moving=movement(),dir=(moving.x||moving.y)?{x:moving.x/Math.hypot(moving.x,moving.y),y:moving.y/Math.hypot(moving.x,moving.y)}:null;
 const items=[['체력',n(sim.player.hp)+' / '+n(sim.maxHealth())],['보호막',n(sim.shieldAmount())],['누적 피해',n(sim.stats.damage)],['추가타 피해',n(sim.stats.extraDamage)],['처치 / 격발',sim.stats.kills+' / '+sim.stats.detonations],['이동 속도',n(sim.moveSpeed(dir),2)],['시간 / 평균 DPS',n(sim.time)+'s / '+n(sim.time?sim.stats.damage/sim.time:0)],['안전 제한 횟수',sim.stats.chainLimited]];
 $('stats').innerHTML=items.map(([label,value])=>`<div class="stat"><span>${label}</span><strong>${value}</strong></div>`).join('');
 $('active-status').innerHTML=`현재 무기 <strong>${ownerName(sim.active)}</strong><br>A 스킬 ${n(sim.cooldowns[0])}s · B 스킬 ${n(sim.cooldowns[1])}s<br>회복 ${n(sim.stats.healed)} · 보호 ${n(sim.stats.blocked)} · 체력 피해 ${n(sim.stats.received)}<br>공격 강화 +${n(sim.modifier('power')*100,0)}% · 사거리 +${n(sim.modifier('reach')*100,0)}%<br>기본 반경 +${n(sim.modifier('radius')*100,0)}% · 스킬 반경 +${n(sim.modifier('radius',sim.active,true)*100,0)}%`;
 const busy=!sim.canAct()||paused;
 for(const [id,owner,kind]of [['attack-a',0,'basic'],['attack-b',1,'basic'],['skill-a',0,'skill'],['skill-b',1,'skill']])$(id).disabled=busy||(kind==='skill'&&sim.cooldowns[owner]>1e-9);
 $('switch').disabled=busy;$('switch').textContent=ownerName(sim.active)+' → '+ownerName(1-sim.active)+' 교대 공격';
 $('manual-mark').disabled=sim.alive().length===0||sim.player.hp<=0;
 $('receive').disabled=sim.player.hp<=0;
 $('event-log').innerHTML=sim.events.map(e=>({e,text:logLine(e)})).filter(x=>x.text).slice(-35).reverse().map(({e,text})=>`<li><time>${n(e.time,2)}s</time> ${esc(text)}</li>`).join('');
}
const canvas=$('arena'),c=canvas.getContext('2d');
let W=1000,H=556,scale=1,fitWorld=false,camera={x:0,y:0,width:18,height:10};
const point=p=>({x:(p.x-camera.x)*scale,y:(p.y-camera.y)*scale});
function viewport(){
 W=Math.max(280,Math.round(canvas.getBoundingClientRect().width));H=Math.round(W*.556);
 const ratio=window.devicePixelRatio||1;
 if(canvas.width!==Math.round(W*ratio)||canvas.height!==Math.round(H*ratio)){canvas.width=Math.round(W*ratio);canvas.height=Math.round(H*ratio);}
 c.setTransform(ratio,0,0,ratio,0,0);
 const width=fitWorld?experiment.model.worldWidth:W<600?8:12,height=H/W*width;
 camera={x:fitWorld?0:Math.max(0,Math.min(experiment.model.worldWidth-width,sim.player.x-width*.25)),y:fitWorld?0:Math.max(0,Math.min(experiment.model.worldHeight-height,sim.player.y-height*.5)),width,height};
 scale=W/width;
}
function text(s,x,y,size=12,color='#b5c5d6',align='center'){c.fillStyle=color;c.font=`${size}px "Malgun Gothic",sans-serif`;c.textAlign=align;c.fillText(s,x,y);}
function circle(x,y,r,color,width=1,fill=false){c.beginPath();c.arc(x,y,r,0,Math.PI*2);if(fill){c.fillStyle=color;c.fill();}else{c.strokeStyle=color;c.lineWidth=width;c.stroke();}}
function line(a,b,color,width=2){c.strokeStyle=color;c.lineWidth=width;c.beginPath();c.moveTo(a.x,a.y);c.lineTo(b.x,b.y);c.stroke();}
const statusNames={slow:'둔화',stun:'기절',root:'속박',disarm:'무장해제',fear:'공포',silence:'침묵',blind:'실명'};
function draw(){
 viewport();
 c.clearRect(0,0,W,H);c.fillStyle='#101923';c.fillRect(0,0,W,H);
 c.strokeStyle='#1c2c3c';c.lineWidth=1;for(let x=0;x<W;x+=scale){c.beginPath();c.moveTo(x,0);c.lineTo(x,H);c.stroke();}for(let y=0;y<H;y+=scale){c.beginPath();c.moveTo(0,y);c.lineTo(W,y);c.stroke();}
 text(W<600?(sim.options.enemyAI?'AI 켜짐':'허수아비'):(sim.options.enemyAI?'간이 몬스터 AI · 조준 원을 피하세요':'허수아비 모드 · 이동·공격 OFF'),12,20,11,'#8298af','left');text(paused?'일시정지':sim.player.hp<=0?'쓰러짐 · 초기화 필요':W<600?'표적 클릭으로 조준':sim.alive().length?'몬스터를 클릭해 조준하세요':'모든 몬스터 처치 · 초기화 또는 재등장 설정',W-12,20,11,'#b6c7d7','right');
 const p=point(sim.player),face=sim.player.facing,profile=sim.profiles[sim.active],reach=profile.basicReach*(1+sim.modifier('reach'))*scale,radius=profile.basicRadius*(1+sim.modifier('radius'))*scale;
 c.save();c.translate(p.x,p.y);c.rotate(Math.atan2(face.y,face.x));c.fillStyle='#8ad0e909';c.strokeStyle='#8ad0e930';c.setLineDash([4,6]);c.fillRect(0,-radius,reach,radius*2);c.strokeRect(0,-radius,reach,radius*2);c.restore();c.setLineDash([]);
 for(const e of sim.enemies){
  const t=point(e);if(e.dead){text('처치',t.x,t.y,13,'#526477');continue;}
  if(e.intent){const target=point(e.intent.aim);circle(target.x,target.y,experiment.model.enemyHitRadius*scale,'#f2ab70',2);line(t,target,'#b7714d55',1);}
  c.fillStyle='#8d7255';c.fillRect(t.x-3,t.y-5,6,35);line({x:t.x-17,y:t.y-2},{x:t.x+17,y:t.y-2},'#ad906b',5);circle(t.x,t.y-16,11,'#b99b70',1,true);line({x:t.x-5,y:t.y-18},{x:t.x+5,y:t.y-14},'#433e3b',2);line({x:t.x-5,y:t.y-14},{x:t.x+5,y:t.y-18},'#433e3b',2);
  if(sim.selectedTarget===e.id)circle(t.x,t.y,29,'#ebce8a',2);
  const states=Object.keys(e.status).filter(k=>sim.hasStatus(e,k));if(states.length){circle(t.x,t.y,24,'#c5a2ec',2);text(states.map(k=>statusNames[k]).join(' · '),t.x,t.y+49,11,'#d4b7f6');}
  c.fillStyle='#344052';c.fillRect(t.x-18,t.y-39,36,4);c.fillStyle=e.boss?'#d3a577':'#83bcb0';c.fillRect(t.x-18,t.y-39,36*e.hp/e.maxHp,4);text(n(e.hp,0),t.x,t.y-44,10,'#ced8e4');
  e.marks.forEach((m,i)=>circle(t.x+(i-(e.marks.length-1)/2)*8,t.y-65,3,m.owner===0?'#86d6ef':'#edc878',1,true));text((e.boss?'보스 ':'표적 ')+(e.id+1),t.x,t.y+32,11,'#869db2');
 }
 if(sim.shieldAmount()>0)circle(p.x,p.y,23,'#81d3ad',3);
 circle(p.x,p.y,15,sim.active===0?'#86d6ef':'#edc878',1,true);line(p,{x:p.x+face.x*29,y:p.y+face.y*29},'#edf2f7',4);text(ownerName(sim.active),p.x,p.y+5,13,'#122031');
 c.fillStyle='#344052';c.fillRect(p.x-30,p.y-34,60,5);c.fillStyle='#81d3ad';c.fillRect(p.x-30,p.y-34,60*Math.max(0,sim.player.hp/sim.maxHealth()),5);
 const recent=sim.events.filter(e=>sim.time-e.time<.65&&sim.time>=e.time);
 for(const e of recent){
  const age=sim.time-e.time;c.globalAlpha=Math.max(0,1-age/.65);
  if(e.type==='attack'){const o=point(e.origin);c.save();c.translate(o.x,o.y);c.rotate(Math.atan2(e.facing.y,e.facing.x));c.fillStyle=e.owner===0?'#86d6ef28':'#edc87828';c.strokeStyle=e.owner===0?'#86d6ef':'#edc878';c.lineWidth=2;c.fillRect(0,-e.radius*scale,e.reach*scale,e.radius*2*scale);c.strokeRect(0,-e.radius*scale,e.reach*scale,e.radius*2*scale);c.restore();}
  if(['hit','detonate','cc','displace'].includes(e.type)){const t=point(e),color=e.part?colors[parts.get(e.part)?.color]||'#d9dfe7':e.type==='detonate'?'#d4b4fa':'#d9dfe7';circle(t.x,t.y,12+age*36,color,e.type==='detonate'?3:1);if(e.type==='hit')text((e.extra?'+':'')+n(e.damage),t.x+10,t.y-14-age*55,14,color);if(e.type==='detonate')text('격발 '+e.count,t.x,t.y-75-age*25,14,color);}
  if(e.type==='heal'&&e.amount>0)text('회복 +'+n(e.amount),p.x,p.y-48-age*45,13,'#81d3ad');
  if(e.type==='received')text('피격 −'+n(e.amount),p.x,p.y+45+age*30,14,'#fa8c84');
  if(e.type==='switch')circle(p.x,p.y,20+age*55,e.incoming===0?'#86d6ef':'#edc878',2);
 }c.globalAlpha=1;
}
function initializeControls(){
 $('scenario').innerHTML=Object.entries(experiment.scenarios).map(([id,s])=>`<option value="${id}">${esc(s.name)}</option>`).join('');$('scenario').value=options.scenario;
 for(const [id,label]of Object.entries(colorNames))$('color').insertAdjacentHTML('beforeend',`<option value="${id}">${label}</option>`);
 for(const [id,label]of Object.entries(catalog.triggers))$('trigger').insertAdjacentHTML('beforeend',`<option value="${id}">${label}</option>`);
 $('example').innerHTML=builds.builds.map((b,i)=>`<option value="${i}">${esc(b.name)}</option>`).join('');
 const changed=()=>{save();reset('실험 설정 변경 · 전투 초기화');};
 $('scenario').addEventListener('change',()=>{options.scenario=$('scenario').value;changed();});
 for(const id of flags){$(id).checked=options[id];$(id).addEventListener('change',()=>{options[id]=$(id).checked;if(id==='allowDuplicates'&&!options[id])loadout=loadout.map(ids=>[...new Set(ids)]);changed();});}
 for(const [id,[min,max]]of Object.entries(numeric)){$(id).value=options[id];$(id).addEventListener('change',()=>{let value=Number($(id).value);if(!Number.isFinite(value))value=experiment.defaults[id];value=Math.max(min,Math.min(max,value));if(['chainLimit','markCap','marksPerHit','detonationDamage'].includes(id))value=Math.round(value);options[id]=value;$(id).value=value;changed();});}
 for(const id of Object.keys(selectors)){$(id).value=options[id];$(id).addEventListener('change',()=>{options[id]=$(id).value;changed();});}
 for(const id of ['color','trigger','query'])$(id).addEventListener('input',renderLibrary);
 for(const owner of [0,1])$('edit-'+(owner?'b':'a')).onclick=()=>{editOwner=owner;renderWeapons();renderLibrary();};
 $('weapons').addEventListener('change',e=>{if(e.target.matches('[data-profile]')){options.profiles[Number(e.target.dataset.profile)]=e.target.value;changed();}});
 $('weapons').addEventListener('click',e=>{const b=e.target.closest('[data-remove]');if(!b)return;const [owner,slot]=b.dataset.remove.split(':').map(Number);loadout[owner].splice(slot,1);editOwner=owner;reset('파츠 해제 · 전투 초기화');});
 $('part-grid').addEventListener('click',e=>{const b=e.target.closest('[data-equip]');if(b)equip(b.dataset.equip);});
 $('apply-example').onclick=()=>{const example=builds.builds[Number($('example').value)];loadout=example.build.map(ids=>[...ids]);reset(example.name+' 적용'+(example.requires?' · '+example.requires:''));};
 $('reset').onclick=()=>reset();$('pause').onclick=()=>{paused=!paused;keys.clear();pad={x:0,y:0};$('pause').setAttribute('aria-pressed',String(paused));$('pause').textContent=paused?'계속하기':'일시정지';update();draw();};
 for(const [id,kind,owner]of [['attack-a','basic',0],['attack-b','basic',1],['skill-a','skill',0],['skill-b','skill',1]])$(id).onclick=()=>perform(kind,owner);
 $('switch').onclick=switchAttack;
 $('fit-view').onclick=()=>{fitWorld=!fitWorld;$('fit-view').setAttribute('aria-pressed',String(fitWorld));$('fit-view').textContent=fitWorld?'근접 보기':'전체 보기';draw();};
 $('manual-mark').onclick=()=>{const t=sim.target();if(t){sim.addMarks(t.id,1);message(ownerName(sim.active)+' 무기로 표식 1개 수동 부여 · 실험');update();draw();}};
 $('receive').onclick=()=>{sim.takeDamage(20);message('시험 피격 20 · 보호막이 먼저 흡수합니다.');update();draw();};
 canvas.addEventListener('pointerdown',e=>{const r=canvas.getBoundingClientRect(),pos={x:camera.x+(e.clientX-r.left)/r.width*camera.width,y:camera.y+(e.clientY-r.top)/r.height*camera.height};const target=sim.nearest(pos,null,1);if(target){sim.selectedTarget=target.id;sim.aim();message('표적 '+(target.id+1)+' 선택');draw();}});
 document.querySelectorAll('[data-move]').forEach(button=>{button.addEventListener('pointerdown',e=>{e.preventDefault();button.setPointerCapture(e.pointerId);const [x,y]=button.dataset.move.split(',').map(Number);pad={x,y};});for(const event of ['pointerup','pointercancel','lostpointercapture'])button.addEventListener(event,()=>{pad={x:0,y:0};});});
 window.addEventListener('keydown',e=>{if(/^(INPUT|SELECT|TEXTAREA)$/.test(e.target.tagName)||e.ctrlKey||e.metaKey||e.altKey)return;if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key)){keys.add(e.key);e.preventDefault();return;}if(e.repeat)return;const action={q:['basic',0],w:['basic',1],e:['skill',0],r:['skill',1]}[e.key.toLowerCase()];if(action){e.preventDefault();perform(...action);}if(e.code==='Space'){e.preventDefault();switchAttack();}});
 window.addEventListener('keyup',e=>keys.delete(e.key));window.addEventListener('blur',()=>{keys.clear();pad={x:0,y:0};last=0;});
 document.addEventListener('visibilitychange',()=>{keys.clear();pad={x:0,y:0};last=0;});
}
function frame(now){
 const dt=last?Math.min(.08,(now-last)/1000):0;last=now;
 if(!paused&&!document.hidden&&sim.player.hp>0)sim.advance(dt,movement());
 if(now-renderAt>100){update();renderAt=now;}draw();requestAnimationFrame(frame);
}
initializeControls();reset(restoreWarning||'A로 표식을 쌓은 뒤 B로 교대해 격발과 회복·CC를 확인하세요.');
window.inkLab={get sim(){return sim;},setGrowth(config){growthConfig=config;reset(config?'MVP 팔레트 구성 적용 · 전투 초기화':'자유 실험 모드');},get growthConfig(){return growthConfig;},get options(){return{...options};},get loadout(){return loadout.map(ids=>[...ids]);},reset};
requestAnimationFrame(frame);
})();
