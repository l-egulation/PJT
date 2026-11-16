'use strict';
const DATA=JSON.parse(document.getElementById('audition-data').textContent),$=id=>document.getElementById(id),STORE='deviant-hit-three-directions-v1',COUNT=DATA.candidates.length;
const defaults={candidate:1,stars:[],weapon:'base_sword',action:'base_sword_combo',attackGain:100,hitGain:100,hitOffset:0,delay:.45,masterGain:75,hitEnabled:true,multi:true,targetCount:1,targetGap:0,hitThrottle:false,burstCount:1,burstGap:120};
let saved={...defaults},ac,master,attackBus,hitBus,limiter,token=0,nodes=[],timer=null,raf=0,visual=null;
const buffers=new Map();
try{saved={...defaults,...JSON.parse(localStorage.getItem(STORE)||'{}')};}catch{}
function persist(){try{localStorage.setItem(STORE,JSON.stringify(saved));}catch{}}
function current(){return DATA.actions.find(a=>a.id===saved.action)||DATA.actions.find(a=>a.weapon===saved.weapon);}
function candidate(n=saved.candidate){return DATA.candidates.find(c=>c.number===n);}
function label(n){const c=candidate(n);return `${c.number}번 ${c.name}`;}
function status(s){$('status').textContent=s;}
function esc(s){return String(s).replace(/[&<>"]/g,ch=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[ch]));}
function renderCards(){
  $('cards').replaceChildren();
  for(const c of DATA.candidates){const card=document.createElement('article');card.className='card'+(saved.candidate===c.number?' active':'');card.dataset.number=c.number;
    const starred=saved.stars.includes(c.number);
    card.innerHTML=`<div class="card-top"><span class="number">${c.number}번 · ${Math.round(c.duration*1000)} ms · ${esc(c.approach)}</span><button class="star ${starred?'on':''}" aria-label="${c.number}번 별표" aria-pressed="${starred}">${starred?'★':'☆'}</button></div><h3>${esc(c.name)}</h3><div class="voice">${esc(c.voice)}</div><div class="wave"></div><p>${esc(c.description)}</p><details><summary>원음 · 의도 · 한계</summary><dl><dt>원음</dt><dd>${c.sources.map(esc).join('<br>')}</dd><dt>의도</dt><dd>${esc(c.intent)}</dd><dt>한계</dt><dd>${esc(c.limits)}</dd></dl></details><div class="buttons"><button data-mode="select">선택</button><button data-mode="solo">단독 듣기</button><button data-mode="mix">공격과 듣기</button><button data-mode="currentSoloAB">망치 → 이 후보</button></div>`;
    const wave=card.querySelector('.wave'),scale=Math.max(...DATA.candidates.map(x=>Math.max(...DATA.audio[x.audio].wave)));
    for(const v of DATA.audio[c.audio].wave){const bar=document.createElement('i');bar.style.height=Math.max(2,v/scale*38)+'px';wave.append(bar);}
    card.querySelector('.star').onclick=()=>{saved.stars=starred?saved.stars.filter(n=>n!==c.number):[...saved.stars,c.number].sort((a,b)=>a-b);persist();renderCards();};
    for(const b of card.querySelectorAll('[data-mode]'))b.onclick=()=>{stop();saved.candidate=c.number;persist();refresh();if(b.dataset.mode!=='select')void start(b.dataset.mode);};
    $('cards').append(card);
  }
}
function setWeapon(id){stop();saved.weapon=id;saved.action=id+'_combo';fillActions();persist();refresh();}
function fillActions(){const entries=DATA.actions.filter(a=>a.weapon===saved.weapon).sort((a,b)=>(a.id.endsWith('_combo')?-1:0)-(b.id.endsWith('_combo')?-1:0));$('action').replaceChildren();for(const a of entries){const op=document.createElement('option');op.value=a.id;op.textContent=a.name;$('action').append(op);}if(!entries.some(a=>a.id===saved.action))saved.action=entries[0].id;$('action').value=saved.action;}
function db(p){return p<=0?'-∞ dB':((20*Math.log10(p/100)>=0?'+':'')+(20*Math.log10(p/100)).toFixed(1)+' dB');}
function updateValues(){for(const [input,lab,fmt] of [['attackGain','attackValue',v=>v+'%'],['hitGain','hitValue',v=>`${v}% (${db(v)})`],['hitOffset','offsetValue',v=>v+' ms'],['delay','delayValue',v=>v+'초'],['masterGain','masterValue',v=>v+'%'],['targetCount','countValue',v=>v+'마리'],['targetGap','gapValue',v=>v+' ms'],['burstCount','burstValue',v=>v+'회'],['burstGap','burstGapValue',v=>v+' ms']])$(lab).textContent=fmt(saved[input]);
  if(ac){master.gain.setTargetAtTime(saved.masterGain/100,ac.currentTime,.015);attackBus.gain.setTargetAtTime(saved.attackGain/100*.8,ac.currentTime,.015);hitBus.gain.setTargetAtTime(saved.hitGain/100,ac.currentTime,.015);}}
function refresh(){renderCards();$('selectedLabel').textContent=label(saved.candidate);updateValues();
  const a=current();$('actionNote').textContent=a.noDamage?'비공격 스킬: 기본 설정에서는 피격음을 넣지 않습니다. 피격음만 버튼으로 후보를 확인할 수 있습니다.':a.hitKind==='direct'?'근접 적중·연타 타이밍에 후보를 겹칩니다. 피격음 시점은 아래에서 미세 조절할 수 있습니다.':'충돌 지연을 바꾸면 피격음과 충돌·폭발음이 함께 이동합니다. 실제 적중은 게임 상황에 따라 달라집니다.';
  $('currentHitLabel').textContent=a.currentHitNote;$('delay').disabled=a.hitKind==='direct';renderTargets();drawTimeline(plan(a));drawFrame(a,0);}
function renderTargets(){const group=$('targets');group.replaceChildren();const columns=Math.min(saved.targetCount,5);group.style.gridTemplateColumns=`repeat(${columns},1fr)`;group.style.width=(saved.targetCount===1?50:saved.targetCount<=3?saved.targetCount*35:145)+'px';for(let i=0;i<saved.targetCount;i++){const el=document.createElement('div');el.className='target';el.dataset.target=i;el.setAttribute('aria-label',`몬스터 ${i+1}`);group.append(el);}}
function burstTimes(){return Array.from({length:saved.burstCount},(_,i)=>i*saved.burstGap/1000);}
function soloAction(){return {...current(),events:[],triggered:[],hits:burstTimes(),hitKind:'direct',basic:false,actionId:'solo',duration:0,segments:current().segments};}
function plan(a=current(),includeHit=saved.hitEnabled,cn=saved.candidate,hitSource='candidate'){
  const delay=Number(saved.delay),offset=Number(saved.hitOffset)/1000;
  let events=a.events.map(e=>({...e,bus:'attack'})),hits=[...a.hits],contactShift=a.hitKind==='direct'?0:delay;
  const aid=a.actionId||a.id;
  if(aid==='bomb_move_special')contactShift=3;
  if(aid==='bomb_followup_special')contactShift=delay+1.5;
  hits=hits.map(t=>t+contactShift);
  if(aid==='shield_default')hits=[.2];
  const first=hits[0]??a.impact??.2;
  for(const t of a.triggered||[]){let times=[a.impact||0];
    if(t.trigger==='explosion')times=hits;
    if(t.trigger==='hit')times=[first];
    if(t.trigger==='teleport')times=[aid==='shuriken_move_special'?first+.03:(a.impact||0)];
    if(t.trigger==='return')times=[first+1.5];
    if(t.trigger==='parry')times=[.2];
    for(const at of times)for(const e of t.events)events.push({...e,time:at+e.time,bus:'attack'});
  }
  if(a.basic&&a.weapon==='bomb')for(const at of hits)for(const e of DATA.extras.explosion_bomb)events.push({...e,time:at,bus:'attack'});
  if(aid==='launcher_basic_special')for(const e of DATA.extras.launcher_charge_impact)events.push({...e,time:first,bus:'attack'});
  if(aid==='shuriken_followup_special')hits=[first,first+.25,first+.5,first+.75];
  const multi=aid==='solo'||saved.multi;
  const contacts=(multi?hits:hits.slice(0,1)).flatMap(t=>Array.from({length:saved.targetCount},(_,target)=>({time:Math.max(0,t+offset*(aid==='solo'?0:1)+target*saved.targetGap/1000),target}))).sort((a,b)=>a.time-b.time||a.target-b.target);
  const hitTimes=contacts.map(c=>c.time);let last=-Infinity;
  const hitClip=hitSource==='current'?a.currentHit:{audio:candidate(cn).audio,gain:1};
  if(includeHit&&hitClip)for(const c of contacts){if(saved.hitThrottle&&c.time-last<.06-1e-8)continue;events.push({audio:hitClip.audio,gain:hitClip.gain,time:c.time,rate:1,bus:'hit',target:c.target});last=c.time;}
  const duration=Math.max(a.duration,...events.map(e=>e.time+DATA.audio[e.audio].duration/e.rate),...hitTimes.map(t=>t+.35))+.25;
  return {events,contacts:includeHit?contacts:[],hitTimes:includeHit?hitTimes:[],duration,action:a};
}
async function audioContext(){if(!ac){ac=new AudioContext();master=ac.createGain();attackBus=ac.createGain();hitBus=ac.createGain();limiter=ac.createDynamicsCompressor();limiter.threshold.value=-2;limiter.knee.value=3;limiter.ratio.value=12;limiter.attack.value=.002;limiter.release.value=.09;attackBus.connect(master);hitBus.connect(master);master.connect(limiter);limiter.connect(ac.destination);}await ac.resume();updateValues();}
async function decode(key){if(!buffers.has(key)){const bytes=Uint8Array.from(atob(DATA.audio[key].src.split(',')[1]),s=>s.charCodeAt(0));buffers.set(key,ac.decodeAudioData(bytes.buffer));}return buffers.get(key);}
function stop(message='정지됨'){token++;if(timer)clearTimeout(timer);timer=null;cancelAnimationFrame(raf);for(const n of nodes){try{n.stop();n.disconnect();}catch{}}nodes=[];visual=null;for(const el of $('targets').children)el.classList.remove('hit');$('cursor').style.left='0%';status(message);}
function drawTimeline(p){$('timeline').replaceChildren($('cursor'));for(const t of p.hitTimes){const mark=document.createElement('i');mark.className='marker';mark.style.left=Math.min(99,t/p.duration*100)+'%';mark.title=`피격 ${t.toFixed(3)}초`;$('timeline').append(mark);}}
function drawFrame(a,time){const s=a.segments.find(v=>time>=v.start&&time<v.start+v.duration)||a.segments[a.segments.length-1];if(!s?.clip){$('actor').removeAttribute('src');return;}
  const c=s.clip,local=Math.max(0,Math.min(time-s.start,s.duration)),total=c.seconds.reduce((a,b)=>a+b,0),pivot=c.sourceImpact>=0?c.sourceImpact:0;
  let source=local<s.impact&&s.impact>0?local/s.impact*pivot:pivot+(local-s.impact)/Math.max(.001,s.duration-s.impact)*(total-pivot);
  let i=0;while(i<c.seconds.length-1&&source>=c.seconds[i])source-=c.seconds[i++];const ref=c.frames[i];if($('actor').dataset.frame!==ref){$('actor').src=DATA.frames[ref];$('actor').dataset.frame=ref;}}
function animate(){if(!visual)return;const t=Math.max(0,ac.currentTime-visual.start);if(visual.action.actionId!=='solo')drawFrame(visual.action,t);$('cursor').style.left=Math.min(100,t/visual.duration*100)+'%';for(const el of $('targets').children)el.classList.toggle('hit',(visual.contacts||[]).some(c=>c.target===Number(el.dataset.target)&&t>=c.time&&t<c.time+.095));raf=requestAnimationFrame(animate);}
async function schedule(p,ticket,text,onend){const decoded=await Promise.all(p.events.map(e=>decode(e.audio)));if(ticket!==token)return;
  const begin=ac.currentTime+.07;nodes=[];p.events.forEach((e,i)=>{const n=ac.createBufferSource(),g=ac.createGain();n.buffer=decoded[i];n.playbackRate.value=e.rate;g.gain.value=e.gain??1;n.connect(g);g.connect(e.bus==='hit'?hitBus:attackBus);n.onended=()=>{n.disconnect();g.disconnect();};n.start(begin+e.time);nodes.push(n);});
  visual={...p,start:begin};drawTimeline(p);status(text);cancelAnimationFrame(raf);animate();timer=setTimeout(()=>{if(ticket!==token)return;for(const n of nodes)n.disconnect();nodes=[];visual=null;cancelAnimationFrame(raf);for(const el of $('targets').children)el.classList.remove('hit');onend?.();},(p.duration+.08)*1000);}
function groupText(){return `${saved.targetCount}마리 / ${saved.targetGap} ms`+(saved.burstCount>1?` · 연속 ${saved.burstCount}회 / ${saved.burstGap} ms`:'');}
async function start(mode='mix'){
  stop('소리 준비 중…');const ticket=token;
  try{await audioContext();if(ticket!==token)return;
    let queue;
    if(mode==='solo')queue=[{solo:true,n:saved.candidate}];
    else if(mode==='currentSolo')queue=[{solo:true,source:'current'}];
    else if(mode==='currentSoloAB')queue=[{solo:true,source:'current'},{solo:true,n:saved.candidate}];
    else if(mode==='soloAll')queue=[{solo:true,source:'current'},...DATA.candidates.map(c=>({solo:true,n:c.number}))];
    else if(mode==='sequence')queue=DATA.candidates.map(c=>({a:current(),n:c.number,on:true}));
    else if(mode==='refSequence')queue=[{a:current(),on:true,source:'current'},...DATA.candidates.map(c=>({a:current(),n:c.number,on:true}))];
    else if(mode==='weaponSequence')queue=DATA.actions.filter(a=>a.weapon===saved.weapon&&(!a.basic||a.id.endsWith('_combo'))).map(a=>({a,n:saved.candidate,on:saved.hitEnabled}));
    else if(mode==='ab')queue=[{a:current(),n:saved.candidate,on:false},{a:current(),n:saved.candidate,on:true}];
    else if(mode==='currentAB')queue=[{a:current(),on:true,source:'current'},{a:current(),n:saved.candidate,on:true}];
    else if(mode==='current')queue=[{a:current(),on:true,source:'current'}];
    else queue=[{a:current(),n:saved.candidate,on:saved.hitEnabled}];
    let i=0;async function next(){if(ticket!==token)return;if(i>=queue.length){savedLoop(mode,ticket);return;}const row=queue[i++];
      if(row.n){saved.candidate=row.n;persist();}
      if(row.a){saved.action=row.a.id;$('action').value=saved.action;persist();}
      refresh();const who=row.source==='current'?'기존 망치 피격음':label(row.n??saved.candidate);
      const p=row.solo?plan(soloAction(),true,row.n??saved.candidate,row.source||'candidate'):plan(row.a,row.on,row.n??saved.candidate,row.source||'candidate');
      const text=row.solo?`${i}/${queue.length} · ${who}만 · ${groupText()}`:`${i}/${queue.length} · ${row.a.name} + ${row.on?who:'피격 없음'} · ${groupText()}`;
      await schedule(p,ticket,text,()=>{timer=setTimeout(()=>void next().catch(e=>stop('재생 실패: '+e.message)),queue.length>1?450:300);});}
    await next();
  }catch(e){if(ticket===token)stop('재생 실패: '+e.message);}
}
function savedLoop(mode,ticket){if(ticket!==token)return;if($('loop').checked)timer=setTimeout(()=>void start(mode),350);else status('재생 완료');}
function selection(){return {schema:1,kind:'hit-three-directions-selection',candidate:saved.candidate,stars:saved.stars,
  confirmedCandidates:saved.stars.map(n=>({number:n,name:candidate(n).name,sha256:candidate(n).audio})),settings:{...saved},gameApplied:false};}
function exportFile(){const blob=new Blob([JSON.stringify(selection(),null,2)],{type:'application/json'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='hit-three-directions-selection.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);status('별표와 청취 설정을 선택 파일로 저장했습니다.');}
const RANGES=[['attackGain',0,150],['hitGain',0,200],['hitOffset',-200,500],['delay',.05,2],['masterGain',0,100],['targetCount',1,10],['targetGap',0,150],['burstCount',1,8],['burstGap',40,400]];
function applySettings(v){const next={...defaults,...v};next.candidate=Math.round(Math.max(1,Math.min(COUNT,Number(next.candidate)||1)));next.stars=[...new Set((Array.isArray(next.stars)?next.stars:[]).filter(n=>Number.isInteger(n)&&n>=1&&n<=COUNT))];for(const [k,min,max] of RANGES)next[k]=Math.max(min,Math.min(max,Number(next[k])||0));next.targetCount=Math.round(next.targetCount);next.burstCount=Math.round(next.burstCount);if(!DATA.weapons.some(w=>w.id===next.weapon))next.weapon=defaults.weapon;saved=next;
  for(const [k] of RANGES)$(k).value=saved[k];for(const k of ['hitEnabled','multi','hitThrottle'])$(k).checked=!!saved[k];$('weapon').value=saved.weapon;fillActions();persist();refresh();}
for(const w of DATA.weapons){const op=document.createElement('option');op.value=w.id;op.textContent=w.name;$('weapon').append(op);}
$('weapon').onchange=()=>setWeapon($('weapon').value);$('action').onchange=()=>{stop();saved.action=$('action').value;persist();refresh();};
for(const [k] of RANGES)$(k).oninput=()=>{saved[k]=Number($(k).value);persist();updateValues();if(['hitOffset','delay','targetCount','targetGap','burstCount','burstGap'].includes(k)){stop('청취 설정 변경됨 · 다시 재생해주세요');renderTargets();drawTimeline(plan());}};
for(const k of ['hitEnabled','multi','hitThrottle'])$(k).onchange=()=>{saved[k]=$(k).checked;persist();stop();refresh();};
for(const b of document.querySelectorAll('[data-count]'))b.onclick=()=>{stop();applySettings({...saved,targetCount:Number(b.dataset.count)});};
for(const b of document.querySelectorAll('[data-gap]'))b.onclick=()=>{stop();applySettings({...saved,targetGap:Number(b.dataset.gap)});};
for(const b of document.querySelectorAll('[data-burst]'))b.onclick=()=>{stop();const [n,g]=b.dataset.burst.split(',').map(Number);applySettings({...saved,burstCount:n,burstGap:g});};
for(const b of document.querySelectorAll('[data-hitgain]'))b.onclick=()=>applySettings({...saved,hitGain:Number(b.dataset.hitgain)});
for(const [id,mode] of [['play','mix'],['solo','solo'],['ab','ab'],['sequence','sequence'],['refSequence','refSequence'],['weaponSequence','weaponSequence'],['currentPlay','current'],['currentSolo','currentSolo'],['currentAB','currentAB'],['currentSoloAB','currentSoloAB'],['soloAll','soloAll']])$(id).onclick=()=>void start(mode);
$('stop').onclick=()=>stop();$('export').onclick=exportFile;
$('import').onclick=()=>$('importFile').click();$('importFile').onchange=async()=>{try{const file=$('importFile').files[0];if(!file)return;const v=JSON.parse(await file.text());if(v.kind!=='hit-three-directions-selection'||v.schema!==1)throw Error('이 피격음 화면의 선택 파일이 아닙니다.');stop();applySettings({...v.settings,candidate:v.candidate,stars:v.stars});status('선택 파일을 불러왔습니다.');}catch(e){status(e.message);}finally{$('importFile').value='';}};
$('reset').onclick=()=>{stop();applySettings({...saved,attackGain:100,hitGain:100,hitOffset:0,delay:.45,masterGain:75,hitEnabled:true,multi:true,burstCount:1,burstGap:120});};
document.addEventListener('keydown',e=>{if(e.code==='Space'&&!['INPUT','SELECT','BUTTON','SUMMARY'].includes(e.target.tagName)){e.preventDefault();visual?stop():void start();}if(e.code==='Escape')stop();});
window.addEventListener('pagehide',()=>stop());applySettings(saved);status('후보를 고른 뒤 단독 듣기 또는 공격과 듣기를 눌러주세요.');
