'use strict';
const DATA=JSON.parse(document.getElementById('skill-data').textContent),$=id=>document.getElementById(id);
const KEY='lost-words-skill-sounds-v1',ctx=$('scene').getContext('2d');
let saved={};try{saved=JSON.parse(localStorage.getItem(KEY)||'{}')||{};}catch(_){}
if(!saved.actions||typeof saved.actions!=='object')saved.actions={};
// Seed only absent records from the user's explicit export; existing browser
// edits take precedence. An explicit re-review clears only that skill once.
if(DATA.importedReview&&saved.importedReviewHash!==DATA.importedReview.sha256){
  const incoming=DATA.importedReview.state;saved.starConfirmations||={};
  for(const [id,st] of Object.entries(incoming.actions))if(!saved.actions[id]){
    saved.actions[id]=JSON.parse(JSON.stringify(st));saved.starConfirmations[id]=JSON.parse(JSON.stringify(incoming.starConfirmations[id]||[]));
  }
  saved.shared||=JSON.parse(JSON.stringify(incoming.shared||{}));
  saved.importedReviewHash=DATA.importedReview.sha256;
}
saved.reopenedReviews||={};
for(const [id,revision] of Object.entries(DATA.reopenedReviews||{}))if(saved.reopenedReviews[id]!==revision){
  saved.rejectedReviews||={};saved.rejectedReviews[id]=JSON.parse(JSON.stringify(saved.actions[id]||{}));
  const a=DATA.actions.find(a=>a.id===id);saved.actions[id]={selected:0,settings:{},favorites:[],choice:null,candidateRevision:a.candidateRevision};
  saved.starConfirmations||={};saved.starConfirmations[id]=[];
  if(saved.previousReviews)delete saved.previousReviews[id];saved.reopenedReviews[id]=revision;
}
// Stars are user confirmations. Recover only records still present locally,
// once; an explicit later removal must never be resurrected on reload.
if(!saved.starConfirmations)saved.starConfirmations={};
for(const a of DATA.actions){
  let st=saved.actions[a.id];const previous=saved.previousReviews?.[a.id];
  if(!saved.starConfirmationMigration&&(!st?.favorites?.length)&&!st?.choice&&previous?.favorites?.length){
    if(!st)st=saved.actions[a.id]={selected:0,settings:{},favorites:[],choice:null,candidateRevision:a.candidateRevision};
    st.favorites=[...previous.favorites];saved.starConfirmations[a.id]=previous.favorites.map(number=>({number,candidateRevision:previous.candidateRevision||1,legacySettingsByRecipe:previous.settings||{},restoredFromPrevious:true}));
  }
  if(st?.choice&&!st.choice.candidateRevision)st.choice.candidateRevision=st.candidateRevision||1;
  if(!saved.starConfirmations[a.id])saved.starConfirmations[a.id]=(st?.favorites||[]).map(number=>({number,candidateRevision:st.candidateRevision||1,legacySettingsByRecipe:st.settings||{}}));
}
saved.starConfirmationMigration=1;
if(!saved.shared||typeof saved.shared!=='object')saved.shared={};
if(!saved.sharedRevisions)saved.sharedRevisions={};
for(const [id,group] of Object.entries(DATA.sharedGroups||{})){
  if((saved.sharedRevisions[id]||1)!==(group.revision||1))saved.shared[id]=0;
  saved.sharedRevisions[id]=group.revision||1;
}
for(const [id,group] of Object.entries(DATA.sharedGroups||{}))if(!Number.isInteger(saved.shared[id])||saved.shared[id]<0||saved.shared[id]>=group.variants.length)saved.shared[id]=0;
for(const a of DATA.actions){
  const old=saved.actions[a.id];if(old&&(old.candidateRevision||1)!==(a.candidateRevision||1)){
    if(!saved.previousReviews)saved.previousReviews={};saved.previousReviews[a.id]=old;
    const retained={};
    for(const r of a.candidates)for(const l of r.layers)if(DATA.frozenLayers?.[`${a.id}/${r.number}/${l.id}`]||old.candidateRevision===a.preserveLayerEditsFromRevision){
      const previousId=`${a.id}-v${old.candidateRevision||1}-${String(r.number).padStart(2,'0')}`,edit=old.settings?.[previousId]?.[l.id];
      if(edit){if(!retained[r.id])retained[r.id]={};retained[r.id][l.id]=JSON.parse(JSON.stringify(edit));}
    }
    saved.actions[a.id]={selected:Math.max(0,Math.min(10,old.selected||0)),settings:retained,favorites:[...(old.favorites||[])],choice:old.choice||null,candidateRevision:a.candidateRevision};
  }
}
let weapon,action,selected=0,solo=null,ac,master,cycle=null,sources=[],running=false,generation=0,ready=false,mode='once',view='skill',cursor=0;
const images=new Map(),buffers=new Map();
const status=t=>$('status').textContent=t;
function persist(){saved.weapon=weapon.id;saved.action=action?.id;try{localStorage.setItem(KEY,JSON.stringify(saved));}catch(_){}}
function state(){
  if(!saved.actions[action.id])saved.actions[action.id]={selected:0,settings:{},favorites:[],choice:null,candidateRevision:action.candidateRevision||1};
  const s=saved.actions[action.id];if(!s.settings||typeof s.settings!=='object')s.settings={};if(!Array.isArray(s.favorites))s.favorites=[];return s;
}
function linkedRecipe(a){
  const target=DATA.actions.find(x=>x.id===a.linkedAction),st=saved.actions[target.id]||{},confirmed=confirmedNumbers(target),number=st.choice?.number||(confirmed.includes((st.selected||0)+1)?st.selected+1:confirmed.at(-1)),index=Math.max(0,Math.min(10,number?number-1:st.selected||0));
  if(index===10)return {id:a.id+'-linked-current',current:true,currentKey:target.current,number:1,name:'강베기 현재 적용음 공유',layers:[],linkedAction:target.id,linkedNumber:11};
  return {...a.candidates[index],number:1,name:`강베기 ${index+1}번과 같은 소리`,linkedAction:target.id,linkedSettingsId:target.candidates[index].id,linkedNumber:index+1};
}
function recipe(){return selected===10?{number:11,current:true,name:'현재 게임 적용음',layers:[]}:action.linkedAction?linkedRecipe(action):action.candidates[selected];}
function settings(r=recipe()){
  const s=r.linkedSettingsId?(saved.actions[r.linkedAction]||(saved.actions[r.linkedAction]={settings:{},favorites:[],selected:0})):state(),key=r.linkedSettingsId||r.id;
  if(!s.settings)s.settings={};if(!s.settings[key])s.settings[key]={};
  for(const layer of r.layers){const x=s.settings[key][layer.id]||{};s.settings[key][layer.id]={offsetMs:Number.isFinite(x.offsetMs)?Math.max(-400,Math.min(400,x.offsetMs)):0,volume:Number.isFinite(x.volume)?Math.max(0,Math.min(150,x.volume)):100};}
  return s.settings[key];
}
function saveEdit(){if(state().choice?.number===selected+1&&(state().choice.candidateRevision||action.candidateRevision)===action.candidateRevision){state().choice.settings=JSON.parse(JSON.stringify(settings()));state().choice.scenario=scenarioValues();}
  const ownStar=(saved.starConfirmations[action.id]||[]).find(v=>v.number===selected+1&&v.candidateRevision===action.candidateRevision);if(ownStar){ownStar.settings=JSON.parse(JSON.stringify(settings()));ownStar.scenario=scenarioValues();}
  if(action.linkedAction&&selected!==10){const r=recipe(),target=saved.actions[action.linkedAction],targetAction=DATA.actions.find(a=>a.id===action.linkedAction);if(target?.choice?.number===r.linkedNumber&&!r.current&&(target.choice.candidateRevision||targetAction.candidateRevision)===targetAction.candidateRevision)target.choice.settings=JSON.parse(JSON.stringify(settings(r)));
    const linkedStar=(saved.starConfirmations[action.linkedAction]||[]).find(v=>v.number===r.linkedNumber&&v.candidateRevision===targetAction.candidateRevision);if(linkedStar&&!r.current)linkedStar.settings=JSON.parse(JSON.stringify(settings(r)));}
  persist();}
function confirmedNumbers(a){const st=saved.actions[a.id]||{};return [...new Set([...(st.favorites||[]),...(st.choice?[st.choice.number]:[])])].filter(n=>Number.isInteger(n)&&n>=1&&n<=11);}
function confirmationDetails(a){
  const st=saved.actions[a.id]||{};return confirmedNumbers(a).map(number=>{
    const star=(saved.starConfirmations[a.id]||[]).find(v=>v.number===number),choice=st.choice?.number===number?st.choice:null;
    const record=choice&&(!star||(choice.candidateRevision||1)>(star.candidateRevision||1))?choice:star||choice||{number,candidateRevision:st.candidateRevision||1};return {...record,number,via:record===star?'star':'choice',isCurrentRevision:(record.candidateRevision||1)===(a.candidateRevision||1),status:'user-confirmed; not applied'};
  });
}
function recordStar(number,r=recipe()){
  const st=state();st.favorites=[...new Set([...st.favorites,number])];
  const snapshot={number,candidateRevision:action.candidateRevision||1,recipeId:r.id||'current-runtime',settings:r.current?{}:JSON.parse(JSON.stringify(settings(r))),scenario:scenarioValues(),sharedChoices:{...saved.shared},layers:JSON.parse(JSON.stringify(r.layers)),audioHashes:r.current?{[r.currentKey||action.current]:DATA.audio[r.currentKey||action.current].sha256}:Object.fromEntries(r.layers.map(l=>[l.audio,DATA.audio[l.audio].sha256]))};
  saved.starConfirmations[action.id]=[...(saved.starConfirmations[action.id]||[]).filter(v=>v.number!==number),snapshot];
}
function confirmStar(number){
  const st=state(),already=confirmedNumbers(action).includes(number);
  if(already){st.favorites=st.favorites.filter(n=>n!==number);saved.starConfirmations[action.id]=(saved.starConfirmations[action.id]||[]).filter(v=>v.number!==number);if(st.choice?.number===number)st.choice=null;}
  else {
    select(number-1);recordStar(number);
  }
  persist();refresh();status(already?`${number}번 확정을 해제했습니다.`:`★ ${number}번 확정으로 저장했습니다.`);
}
function reviewRows(){return DATA.actions.map(a=>{
  const confirmed=confirmationDetails(a),revised=confirmed.some(v=>!v.isCurrentRevision);
  if(confirmed.length)return {id:a.id,weapon:a.weapon,name:a.name,basic:!!a.isBasicReview,pending:false,confirmedNumbers:confirmed.map(v=>v.number),modifiedSinceConfirmation:revised,status:`★ ${confirmed.map(v=>v.number).join('·')}번 확정${revised?' · 확정 이후 수정본 있음':''}`};
  if(a.linkedAction){const target=DATA.actions.find(x=>x.id===a.linkedAction),done=confirmedNumbers(target).length>0,modified=confirmationDetails(target).some(v=>!v.isCurrentRevision);return {id:a.id,weapon:a.weapon,name:a.name,basic:false,pending:!done,modifiedSinceConfirmation:modified,status:done?`${target.name} 확정안과 연동${modified?' · 수정본 있음':''}`:`${target.name} 확정 필요`};}
  if(a.fixedCandidate)return {id:a.id,weapon:a.weapon,name:a.name,basic:false,pending:false,status:'사용자 지정 · 1번 감기 고정'};
  return {id:a.id,weapon:a.weapon,name:a.name,basic:!!a.isBasicReview,pending:true,status:'최종 선택 필요'};
});}
function renderReviewList(){
  const rows=reviewRows(),pending=rows.filter(r=>r.pending&&!r.basic),basic=rows.filter(r=>r.pending&&r.basic);
  $('pendingTitle').textContent=`미정 목록 · 스킬 ${pending.length}개 / 기본공격 재검토 ${basic.length}개`;$('pendingList').replaceChildren();
  for(const w of DATA.weapons){const own=pending.filter(r=>r.weapon===w.id);if(!own.length)continue;const line=document.createElement('p');line.textContent=w.label+' — '+own.map(r=>r.name+(DATA.actions.find(a=>a.id===r.id).linkedAction?' (강베기 확정 필요)':'')).join(', ');$('pendingList').append(line);}
  const remaining=document.createElement('p');remaining.className='hint';remaining.textContent=basic.length?'기본공격 재검토: '+basic.map(r=>DATA.weapons.find(w=>w.id===r.weapon).label).join(', '):'기본공격 재검토 선택 완료';$('pendingList').append(remaining);
  const fixed=document.createElement('p');fixed.className='hint';fixed.textContent='확정·연동: '+(rows.filter(r=>!r.pending&&!r.basic).map(r=>r.name+' ('+r.status+')').join(', ')||'없음');$('pendingList').append(fixed);
}
function scenarioValues(){
  const st=state();if(!st.scenario)st.scenario={};const result={};
  for(const [id,spec] of Object.entries(action.scenario||{})){const n=st.scenario[id];result[id]=Number.isFinite(n)?Math.max(spec.min,Math.min(spec.max,n)):spec.default;}
  return result;
}
function timing(){const v=scenarioValues(),release=v.charge??action.impact,shift=release-action.impact;return {...v,release,shift,impactDemo:action.id==='shield_default'?(v.impactDelay||.12):release+(v.impactDelay||1),sustainEnd:release+(v.sustain||0)};}
function renderScenario(){
  $('scenarioControls').replaceChildren();$('scenarioPanel').hidden=!action||!Object.keys(action.scenario||{}).length;
  if(!action)return;
  const values=scenarioValues();
  for(const [key,spec] of Object.entries(action.scenario||{})){
    const wrap=document.createElement('div');wrap.className='row';const label=document.createElement('label');label.textContent=spec.label+' (초)';const number=document.createElement('input');number.type='number';number.min=String(spec.min);number.max=String(spec.max);number.step='.05';number.value=String(values[key]);number.setAttribute('aria-label',spec.label);
    number.onchange=()=>{const value=Number(number.value);if(!Number.isFinite(value))return;state().scenario[key]=Math.max(spec.min,Math.min(spec.max,value));const repeat=running&&mode==='loop';stop();saveEdit();renderScenario();renderEditor();render(0);if(repeat)void play('loop');};wrap.append(label,number);
    if(Number.isFinite(spec.actual)){const reset=document.createElement('button');reset.textContent=`게임 시간 ${spec.actual}초`;reset.onclick=()=>{number.value=String(spec.actual);number.onchange();};wrap.append(reset);}
    $('scenarioControls').append(wrap);
  }
}
function setWeapon(id){
  stop();weapon=DATA.weapons.find(w=>w.id===id)||DATA.weapons[0];$('weapon').value=weapon.id;
  const own=DATA.actions.filter(a=>a.weapon===weapon.id);action=own.find(a=>a.id===saved.action)||own[0]||null;
  selected=action?Math.max(0,Math.min(10,Number.isInteger(state().selected)?state().selected:0)):0;solo=null;view=action?'skill':'basic';cursor=0;refresh();persist();render(0);
}
function setAction(id){stop();action=DATA.actions.find(a=>a.id===id);selected=Math.max(0,Math.min(10,Number.isInteger(state().selected)?state().selected:0));solo=null;view='skill';cursor=0;refresh();persist();render(0);}
function refresh(){
  if(action?.fixedCandidate&&selected!==10){selected=0;state().selected=0;}
  $('skills').replaceChildren();
  for(const a of DATA.actions.filter(a=>a.weapon===weapon.id)){
    const b=document.createElement('button');b.textContent=a.name+(a.candidateRevision===DATA.revision?' · 새 시안':'')+(confirmedNumbers(a).length||a.fixedCandidate&&!a.linkedAction?' ✓':'');b.className=a.id===action?.id?'active':'';b.onclick=()=>setAction(a.id);$('skills').append(b);
  }
  renderReviewList();$('progress').textContent=`${reviewRows().filter(r=>!r.pending).length} / ${DATA.actions.length} 확정·연동`;
  $('title').textContent=weapon.label+' · '+(action?.name||'기본공격');$('summary').textContent=action?.summary||'맨손에는 별도 스킬이 없습니다. 확정된 기본공격을 들을 수 있습니다.';
  $('actionNote').textContent=action?.note||'';
  renderScenario();
  const choice=weapon.selection;$('basicLabel').textContent=choice.recipeId?`${choice.number}번 · 휘두름 ${choice.layerOffsetsMs.swing}ms / 충격 ${choice.layerOffsetsMs.impact>0?'+':''}${choice.layerOffsetsMs.impact}ms · 게임 적용본`:choice.number===11?'기존 게임 음원으로 확정':weapon.id==='shuriken'?'폭탄 2번 원음 · 게임 적용본':`${choice.number}번 · 게임 적용본`;
  if(weapon.id==='bomb'&&DATA.basicExplosionPreview)$('basicLabel').textContent+=' · 폭발은 B3 확정안 미리듣기 (게임 적용 전)';
  for(const id of ['play','loop','sequence','compare','current','choose'])$(id).disabled=!action||!ready;
  $('basic').disabled=!ready;$('candidateTitle').textContent=action?`${action.name} · ${action.fixedCandidate?'고정안':'10개 후보'} + 현재음`:'스킬 없음';
  $('sequence').textContent=action?.fixedCandidate?'고정안 → 현재음':'01 → 11 비교';
  $('current').textContent=action?.isBasicReview?'현재 기본공격 적용음':'현재 스킬 적용음';$('compare').textContent=action?.isBasicReview?'현재 기본공격 → 새 후보':'기본공격 → 선택 스킬';
  $('candidates').replaceChildren();$('editor').hidden=!action||recipe().current;
  if(!action){$('choice').textContent='';return;}
  const st=state();$('choice').textContent=reviewRows().find(r=>r.id===action.id).status;
  for(let i=0;i<11;i++){
    if(action.fixedCandidate&&i!==0&&i!==10)continue;
    const r=i===10?{name:'현재 게임 적용음',current:true}:action.linkedAction?linkedRecipe(action):action.candidates[i];
    const row=document.createElement('div');row.className='candidate'+(i===selected?' selected':'')+(r.current?' current':'')+(confirmedNumbers(action).includes(i+1)?' chosen':'');
    const b=document.createElement('button');b.className='pick';b.disabled=!ready;
    const n=document.createElement('span');n.className='num';n.textContent=String(i+1).padStart(2,'0');
    const text=document.createElement('span');text.textContent=r.name;const small=document.createElement('small');small.textContent=r.linkedAction?'강베기에서 선택한 소리와 개별 조절값을 공유합니다.':r.current?'현재 연결·배율 그대로':`${r.layers.length}개 소리 · ${r.reason}`;text.append(small);b.append(n,text);b.onclick=()=>select(i,true);
    const star=document.createElement('button');star.className='star';star.textContent=confirmedNumbers(action).includes(i+1)?'★':'☆';star.setAttribute('aria-label',`${i+1}번 확정 표시`);star.setAttribute('aria-pressed',String(confirmedNumbers(action).includes(i+1)));star.onclick=()=>confirmStar(i+1);row.append(b,star);$('candidates').append(row);
  }
  renderEditor();
}
function renderEditor(){
  $('layers').replaceChildren();if(!action||selected===10)return;
  const r=recipe(),ed=settings(r);
  for(const layer of r.layers){
    const block=document.createElement('div');block.className='layer';const row=document.createElement('div');row.className='row';
    const name=document.createElement('strong');name.textContent=layer.name;const listen=document.createElement('button');listen.textContent=solo===layer.id?'함께 듣기':'이 소리만 듣기';listen.onclick=()=>{solo=solo===layer.id?null:layer.id;renderEditor();void play('loop');};row.append(name,listen);block.append(row);
    if(layer.group){
      const group=DATA.sharedGroups[layer.group],sharedRow=document.createElement('div');sharedRow.className='row';const label=document.createElement('span');label.className='hint';label.textContent=group.label+' · '+group.scope;sharedRow.append(label);
      if(!group.fixed){const picker=document.createElement('select');picker.setAttribute('aria-label',group.label);group.variants.forEach((v,i)=>{const o=document.createElement('option');o.value=String(i);o.textContent=v.name||`${i+1}번`;picker.append(o);});picker.value=String(saved.shared[layer.group]);picker.onchange=()=>{saved.shared[layer.group]=Number(picker.value);const repeat=running&&mode==='loop';stop();saveEdit();renderEditor();if(repeat)void play('loop');status(group.label+' 변경 · 연결된 모든 스킬에 같은 소리 사용');};sharedRow.append(picker);}
      block.append(sharedRow);
    }
    for(const kind of ['offsetMs','volume']){
      const wrap=document.createElement('div');wrap.className='row';const label=document.createElement('label');label.textContent=kind==='offsetMs'?'위치 (ms)':'음량 (%)';const range=document.createElement('input'),number=document.createElement('input');
      for(const input of [range,number]){input.type=input===range?'range':'number';input.min=kind==='offsetMs'?'-400':'0';input.max=kind==='offsetMs'?'400':'150';input.step='5';input.value=String(ed[layer.id][kind]);input.setAttribute('aria-label',layer.name+' '+label.textContent);}
      const save=input=>{let value=Number(input.value);if(!Number.isFinite(value))return;value=Math.max(Number(input.min),Math.min(Number(input.max),Math.round(value/5)*5));ed[layer.id][kind]=value;range.value=String(value);number.value=String(value);saveEdit();};
      range.oninput=()=>save(range);for(const input of [range,number])input.onchange=()=>{save(input);const repeat=running&&mode==='loop';stop();if(repeat)void play('loop');};wrap.append(label,range,number);block.append(wrap);
    }
    const info=document.createElement('p');info.className='source';const events=rawEvents(plan('skill'),r).filter(e=>e.layer===layer.id);info.textContent=`재생 ${events.map(e=>Math.round(e.time*1000)).join(' / ')} ms${layer.loop?' · 지속 재생':''}${layer.fit==='charge'?' · 충전 길이에 맞춤':''}`;block.append(info);$('layers').append(block);
  }
}
function select(index,autoplay=false){stop();if(action.fixedCandidate&&index!==10)index=0;selected=index;state().selected=index;solo=null;view='skill';cursor=0;
  selectShared(index);
  persist();refresh();render(0);if(autoplay)void play('once');}
function selectShared(index){if(index<10)for(const l of recipe().layers)if(l.group&&!DATA.sharedGroups[l.group].fixed)saved.shared[l.group]=index%DATA.sharedGroups[l.group].variants.length;}
function plan(kind){
  const speed=Number($('speed').value)||1;
  if(kind==='basic'){
    let at=0;const segments=weapon.basics.map((b,i)=>{const s={start:at,duration:b.duration,impact:b.impact,clip:weapon.clips['attack'+(i+1)],effect:weapon.effects['attack'+(i+1)],name:`기본공격 ${i+1}타`,key:'attack'+(i+1)};at+=b.duration;return s;});
    const extras=weapon.id==='bomb'?[{key:DATA.basicExplosionPreview?.audio||DATA.basicExplosion,time:segments[0].impact+(action?.scenario?.impactDelay?scenarioValues().impactDelay:1),gain:DATA.basicExplosionPreview?.gain??.8}]:[];
    return {kind,speed:speed*weapon.defaultSpeed,segments,extras,duration:Math.max(at,...extras.map(e=>e.time+DATA.audio[e.key].duration))};
  }
  const t=timing();let duration=Math.max(action.previewDuration+t.shift,t.sustainEnd+.35);
  if(action.scenario?.impactDelay)duration=Math.max(duration,t.impactDemo+(action.id==='bomb_followup_special'?1.5:0)+1);
  const p={kind,speed,timing:t,markers:action.markers.map(n=>n+t.shift),segments:[{start:0,duration:action.duration+t.shift,impact:t.release,clip:action.clip,effect:action.effect,name:action.name}],duration};
  p.duration=Math.max(duration,...rawEvents(p).map(e=>(e.time+(e.duration??DATA.audio[e.key].duration/e.rate))*speed));return p;
}
function rawEvents(p,r=action?recipe():null){
  if(p.kind==='basic')return [...p.segments.map(s=>{const clip=DATA.audio[weapon.currentVariants[s.key]];return {key:weapon.currentVariants[s.key],time:s.start/p.speed,rate:clip.nominalDuration/s.duration*p.speed,gain:.8};}),...(p.extras||[]).map(e=>({...e,time:e.time/p.speed,rate:p.speed}))];
  if(r.current)return [{key:r.currentKey||action.current,time:p.timing.shift/p.speed+(r.currentKey?(action.impact-DATA.actions.find(a=>a.id===r.linkedAction).impact)/p.speed:0),rate:(action.currentRate||1)*p.speed,gain:.8},...(action.currentExtras||[]).flatMap(l=>l.times.map(time=>({key:l.audio,time:(p.timing[l.anchor]+time)/p.speed,rate:p.speed,gain:l.gain})))];
  const ed=settings(r),events=[],t=p.timing;
  for(const l of r.layers){
    const common=l.group?DATA.sharedGroups[l.group].variants[DATA.sharedGroups[l.group].fixed?0:saved.shared[l.group]]:null,key=common?.audio||l.audio,clip=DATA.audio[key];
    const anchor=l.anchor==='release'||l.anchor==='activation'?t.release:l.anchor==='impactDemo'?t.impactDemo:l.anchor==='sustainEnd'?t.sustainEnd:0;
    const length=typeof l.duration==='string'?t[l.duration]:l.duration;
    const times=l.repeatEvery?Array.from({length:Math.max(1,Math.ceil((length||0)/l.repeatEvery))},(_,i)=>i*l.repeatEvery):l.times;
    for(const time of times){const rate=l.fit==='charge'?clip.duration/t.charge*p.speed:(l.rate||1)*p.speed;
      events.push({key,layer:l.id,time:(anchor+time+ed[l.id].offsetMs/1000)/p.speed-(l.align==='onset'?0:clip.peakTime/rate),rate,
        gain:(common?.gain??l.gain)*ed[l.id].volume/100,loop:!!l.loop,fadeSeconds:(l.fadeSeconds||.035)/p.speed,duration:l.loop||l.fit?length/p.speed:clip.duration/rate});}
  }
  return events;
}
function overlapGain(events){
  const points=[];for(const e of events){const clip=DATA.audio[e.key],duration=e.duration??clip.duration/e.rate;
    if(!e.loop&&clip.peakEnvelope){const step=clip.peakEnvelopeStep/e.rate;clip.peakEnvelope.forEach((peak,i)=>{const begin=i*step,end=Math.min(duration,(i+1)*step);if(begin<end)points.push([e.time+begin,peak*e.gain],[e.time+end,-peak*e.gain]);});}
    else {const peak=clip.peak??Math.max(...clip.waveform),value=peak*e.gain;points.push([e.time,value],[e.time+duration,-value]);}
  }
  points.sort((a,b)=>a[0]-b[0]||a[1]-b[1]);let level=0,max=0;for(const [,v]of points){level+=v;max=Math.max(max,level);}return Math.min(1,.88/Math.max(.001,max));
}
function eventsFor(p,r=action?recipe():null){const events=rawEvents(p,r);if(p.kind==='basic'||r.current)return events;const safe=overlapGain(events);return events.filter(e=>!solo||solo===e.layer).map(e=>({...e,gain:e.gain*safe}));}
function stop(message='정지됨'){generation++;running=false;cycle=null;for(const s of sources){try{s.stop();}catch(_){}s.disconnect();}sources=[];$('loop').classList.remove('active');status(message);}
async function decode(key){
  if(!buffers.has(key)){const bytes=Uint8Array.from(atob(DATA.audio[key].src.split(',')[1]),c=>c.charCodeAt(0));buffers.set(key,ac.decodeAudioData(bytes.buffer).catch(e=>{buffers.delete(key);throw e;}));}
  return buffers.get(key);
}
async function schedule(kind,ticket){
  const p=plan(kind),events=eventsFor(p);let decoded;
  try{decoded=await Promise.all(events.map(e=>decode(e.key)));}catch(e){if(ticket===generation)stop('재생 실패: '+e.message);return;}
  if(ticket!==generation)return;
  const origin=ac.currentTime+.10+Math.max(.12,-Math.min(0,...events.map(e=>e.time))+.03);
  const finish=Math.max(p.duration/p.speed,...events.map((e,i)=>e.time+(e.duration??decoded[i].duration/e.rate)));
  cycle={p,origin,finish,kind};view=kind;
  for(let i=0;i<events.length;i++){const e=events[i],s=ac.createBufferSource(),gain=ac.createGain();s.buffer=decoded[i];s.playbackRate.value=e.rate;gain.gain.value=e.gain;s.connect(gain);gain.connect(master);sources.push(s);s.onended=()=>{s.disconnect();gain.disconnect();sources=sources.filter(x=>x!==s);};
    if(e.loop){s.loop=true;const at=origin+e.time,end=at+e.duration,fade=Math.min(e.fadeSeconds,e.duration/4);gain.gain.setValueAtTime(0,at);gain.gain.linearRampToValueAtTime(e.gain,at+fade);gain.gain.setValueAtTime(e.gain,end-fade);gain.gain.linearRampToValueAtTime(0,end);}
    s.start(origin+e.time);if(e.loop)s.stop(origin+e.time+e.duration);}
  status(kind==='basic'?(weapon.id==='bomb'&&DATA.basicExplosionPreview?'기본 투척은 실제 적용음 · 폭발 B3 확정안은 게임 적용 전':'확정 기본공격 · 실제 적용음'):`${action.name} · ${selected+1}번${solo?' · 개별 듣기':''}`);
}
async function play(nextMode='once'){
  if(!ready)return;stop('소리 준비 중…');const ticket=generation;mode=nextMode;
  if(!ac){ac=new AudioContext();master=ac.createGain();master.connect(ac.destination);}
  try{await ac.resume();if(ticket!==generation)return;master.gain.value=Number($('volume').value)/100;running=true;$('loop').classList.toggle('active',mode==='loop');await schedule(mode==='basic'||mode==='ab'||!action?'basic':'skill',ticket);}
  catch(e){if(ticket===generation)stop('재생 실패: '+e.message);}
}
function outputTime(){const stamp=ac.getOutputTimestamp?.();return stamp&&stamp.contextTime>0?Math.min(ac.currentTime,stamp.contextTime+Math.max(0,performance.now()-stamp.performanceTime)/1000):Math.max(0,ac.currentTime-(ac.baseLatency||0)-(ac.outputLatency||0));}
function frameAt(clip,time){let at=0;for(let i=0;i<clip.seconds.length;i++){at+=clip.seconds[i];if(time<at-1e-8)return i;}return clip.frames.length-1;}
function sourceTime(s,t){const c=s.clip,source=c.sourceImpact>=0?c.sourceImpact:c.seconds.slice(0,Math.max(0,c.contact||0)).reduce((a,b)=>a+b,0),total=c.seconds.reduce((a,b)=>a+b,0);return t<s.impact&&s.impact>0?t/s.impact*source:source+(t-s.impact)/Math.max(.001,s.duration-s.impact)*(total-source);}
function render(time=cursor){
  const p=plan(view);cursor=Math.max(0,Math.min(p.duration,time));const s=p.segments.find(s=>cursor<s.start+s.duration-1e-8)||p.segments.at(-1);const local=Math.min(s.duration,Math.max(0,cursor-s.start)),st=sourceTime(s,local),idx=frameAt(s.clip,st),ref=s.clip.frames[idx],frame=DATA.frames[ref],im=images.get(ref);
  let left=-60,right=80,top=-100,bottom=10;for(const segment of p.segments)for(const key of segment.clip.frames){const f=DATA.frames[key],u=32/f.ppu;left=Math.min(left,(f.bounds[0]-f.pivot[0])*u);right=Math.max(right,(f.bounds[2]-f.pivot[0])*u);top=Math.min(top,(f.bounds[1]-f.pivot[1])*u);bottom=Math.max(bottom,(f.bounds[3]-f.pivot[1])*u);}
  const scale=Math.min(2.7,830/(right-left),300/(bottom-top)),x=500-(left+right)*scale/2,y=355-bottom*scale;
  ctx.fillStyle='#162422';ctx.fillRect(0,0,1000,420);ctx.strokeStyle='#344a41';ctx.beginPath();ctx.moveTo(40,y);ctx.lineTo(960,y);ctx.stroke();
  if(im){ctx.save();ctx.imageSmoothingEnabled=false;const z=scale*32/frame.ppu;ctx.drawImage(im,x-frame.pivot[0]*z,y-frame.pivot[1]*z,im.width*z,im.height*z);ctx.restore();}
  const fx=s.effect,elapsed=st-s.clip.fxStart;
  if(fx&&s.clip.fxStart>=0&&elapsed>=0&&elapsed<fx.seconds.reduce((a,b)=>a+b,0)&&cursor<=s.start+s.duration){const n=frameAt(fx,elapsed),f=DATA.frames[fx.frames[n]],img=images.get(fx.frames[n]);if(img){ctx.save();ctx.translate(x+(fx.x[n]||0)*32*scale,y-(fx.y[n]||0)*32*scale);ctx.rotate(-(fx.rotation||0)*Math.PI/180);if(fx.flipX)ctx.scale(-1,1);const z=scale*32/f.ppu*(fx.scale||1);ctx.imageSmoothingEnabled=false;ctx.drawImage(img,-f.pivot[0]*z,-f.pivot[1]*z,img.width*z,img.height*z);ctx.restore();}}
  const marks=view==='basic'?p.segments.map(s=>s.start+s.impact):p.markers;
  ctx.fillStyle='#405a4e';ctx.fillRect(50,395,900,3);for(const mark of marks){ctx.fillStyle='#f1c281';ctx.fillRect(50+mark/p.duration*900-2,389,4,15);}ctx.fillStyle='#a3e1c9';ctx.beginPath();ctx.arc(50+cursor/p.duration*900,396,5,0,Math.PI*2);ctx.fill();
  const activeLayers=view==='skill'&&selected<10?rawEvents(p).filter(e=>cursor/p.speed>=e.time&&cursor/p.speed<=e.time+e.duration).map(e=>recipe().layers.find(l=>l.id===e.layer)?.name).filter(Boolean):[];
  $('phase').textContent=activeLayers.length?[...new Set(activeLayers)].join(' + '):view==='skill'&&action.id==='bomb_move_special'&&selected<10&&cursor<p.timing.sustainEnd?'비행 중 · 무음':s.name+(cursor>s.start+s.duration?' · 후속 효과 구간':'');if(view==='skill'&&action.installationEvent&&!activeLayers.length&&selected<10)$('phase').textContent=cursor<p.timing.release?'미끼 설치 준비 · 무음':'미끼 설치됨';$('frameInfo').textContent=`${ref.split('/').at(-1)} · ${idx+1}/${s.clip.frames.length}`;
  $('timeLabel').textContent=`${Math.round(cursor/p.speed*1000)} / ${Math.round(p.duration/p.speed*1000)} ms`;$('markers').textContent=(view==='skill'&&action.installationEvent?'미끼 설치 시점 ':'동작 시점 ')+marks.map(t=>Math.round(t/p.speed*1000)).join(' / ')+' ms';$('scrub').max=String(Math.round(p.duration*1000));$('scrub').value=String(Math.round(cursor*1000));
}
function tick(){
  if(running&&cycle){const t=outputTime()-cycle.origin;render(Math.max(0,t*cycle.p.speed));if(t>cycle.finish+.35){const kind=cycle.kind;cycle=null;
    if(mode==='ab'&&kind==='basic')void schedule('skill',generation);
    else if(mode==='loop')void schedule('skill',generation);
    else if(mode==='sequence'&&selected<10){selected=action.fixedCandidate?10:selected+1;state().selected=selected;selectShared(selected);solo=null;refresh();persist();void schedule('skill',generation);}
    else stop('재생 완료');}}
  requestAnimationFrame(tick);
}
function exportSelection(){saveEditSafe();return {schemaVersion:7,confirmationSemantics:'stars-and-choice-are-confirmed; multiple-stars-are-all-approved; revisions-are-preserved',kind:'skill-audio-selection',revision:DATA.revision,basicExplosionPreview:DATA.basicExplosionPreview,reviewStatus:reviewRows(),referencePolicy:DATA.referencePolicy,frozenLayers:DATA.frozenLayers,sharedChoices:saved.shared,sharedGroups:DATA.sharedGroups,offsetUnits:'milliseconds at 1x; applied to every event in that layer',scenarioScope:'audition only; charge/sustain/impact are runtime events when integrated',weapons:DATA.weapons.map(w=>({weapon:w.id,basicSelection:w.selection,currentBasic:w.currentVariants})),actions:DATA.actions.map(a=>({id:a.id,weapon:a.weapon,name:a.name,isBasicReview:!!a.isBasicReview,linkedAction:a.linkedAction,fixedCandidate:!!a.fixedCandidate,confirmedCandidates:confirmationDetails(a),confirmedComponents:a.confirmedComponents,resolvedLink:a.linkedAction?exportLink(a):undefined,...saved.actions[a.id],scenarioDefinition:a.scenario,candidates:a.candidates,current:a.current,currentRate:a.currentRate,currentExtras:a.currentExtras})),audio:Object.fromEntries(Object.entries(DATA.audio).map(([k,a])=>[k,{file:a.file,sha256:a.sha256,source:a.source,sourceSha256:a.sourceSha256,peakTime:a.peakTime,edit:a.edit}]))};}
function saveEditSafe(){if(action)saveEdit();else persist();}
function exportLink(a){const r=linkedRecipe(a);return {...r,settings:saved.actions[r.linkedAction]?.settings?.[r.linkedSettingsId]||{},currentShiftSeconds:r.current?a.impact-DATA.actions.find(x=>x.id===a.linkedAction).impact:undefined};}
$('weapon').onchange=()=>setWeapon($('weapon').value);$('play').onclick=()=>play();$('loop').onclick=()=>running&&mode==='loop'?stop():play('loop');$('basic').onclick=()=>play('basic');$('compare').onclick=()=>{solo=null;renderEditor();void play('ab');};$('current').onclick=()=>select(10,true);$('sequence').onclick=()=>{select(0);void play('sequence');};$('stop').onclick=()=>{stop();};
$('choose').onclick=()=>{const r=recipe();recordStar(selected+1,r);state().choice={number:selected+1,candidateRevision:action.candidateRevision||1,recipeId:r.id||'current-runtime',settings:r.current?{}:JSON.parse(JSON.stringify(settings())),status:'user-selected; not applied'};persist();refresh();status(`${action.name} ${selected+1}번 선택 저장됨`);};
$('reset').onclick=()=>{const repeat=running&&mode==='loop';stop();delete state().settings[recipe().id];solo=null;saveEdit();refresh();if(repeat)void play('loop');};
$('next').onclick=()=>{const index=action?DATA.actions.indexOf(action):-1;const next=Array.from({length:DATA.actions.length},(_,i)=>DATA.actions[(index+i+1)%DATA.actions.length]).find(a=>reviewRows().find(r=>r.id===a.id).pending);if(next){saved.action=next.id;setWeapon(next.weapon);}else status('모든 후보를 선택했습니다. 선택 파일을 저장해주세요.');};
$('speed').onchange=()=>{const repeat=running&&mode==='loop';stop();render(0);if(repeat)void play('loop');};$('scrub').oninput=()=>{stop('프레임 확인 중');render(Number($('scrub').value)/1000);};$('volume').oninput=()=>{$('volumeLabel').textContent=$('volume').value+'%';if(master)master.gain.setTargetAtTime(Number($('volume').value)/100,ac.currentTime,.015);};
$('export').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify(exportSelection(),null,2)],{type:'application/json'}));const link=document.createElement('a');link.href=url;link.download='skill-sound-selections.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);status('전체 스킬 선택 파일을 저장했습니다.');};
document.addEventListener('visibilitychange',()=>{if(document.hidden){stop('화면을 떠나 재생을 멈췄습니다.');}});window.addEventListener('pagehide',()=>{stop();});
for(const w of DATA.weapons){const option=document.createElement('option');option.value=w.id;option.textContent=w.label;$('weapon').append(option);}setWeapon(saved.weapon||DATA.defaultWeapon);
Promise.all(Object.entries(DATA.frames).map(([ref,f])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images.set(ref,im);resolve();};im.onerror=()=>reject(new Error(ref));im.src=f.src;}))).then(()=>{ready=true;refresh();render(0);status('준비됐습니다. 기본공격과 스킬 후보를 비교하세요.');requestAnimationFrame(tick);}).catch(e=>status('프레임을 불러오지 못했습니다: '+e.message));
