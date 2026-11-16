// Full Rose and Blue boss flow review: each step is its own page in one iframe; pages report their end with
// postMessage and the player's X and HP are carried into the next step. The absorption cutscene has two branches
// (Blue down first or Rose down first); the battle picks one by who falls first and both can be opened directly.
// The flow bar (pause, seek bar with cut ticks, cut list) drives the same-origin iframe through its own controls.
const $=s=>document.querySelector(s),view=$('#view');
const PHASE1='../../../boss-siblings-lineup-20261103/phase1-playtest/',PHASE2='../../../boss-phase2-cutscene-20261106/preview/';
const BRANCH={'blue-first':'블루가 먼저 쓰러짐','rose-first':'로즈가 먼저 쓰러짐'};
const STEPS=[
 {id:'intro',name:'입장 컷씬',url:()=>'../intro/?embedded'},
 {id:'phase1',name:'1페이즈 전투',url:()=>`${PHASE1}?embedded&handoff`},
 {id:'shift',name:'1→2 전환 컷씬',url:c=>`../phase-shift/?embedded&x=${c.x??416}&y=${c.y??236}&hp=${c.hp??100}`+(c.mx!=null?`&mx=${c.mx}&my=${c.my}`:'')},
 {id:'phase2',name:'2페이즈 전투',url:c=>`${PHASE2}?embedded&battle&playerX=${c.x??480}&hp=${c.hp??100}`},
 {id:'blue-first',name:'흡수 컷씬: 블루가 먼저 쓰러짐',url:()=>`${PHASE2}?embedded&scene=blue-first`},
 {id:'rose-first',name:'흡수 컷씬: 로즈가 먼저 쓰러짐',url:()=>`${PHASE2}?embedded&scene=rose-first`}];
let index=0,carry={},branch=null,pending=null;
// Cut lists per cutscene step, read from the same timeline files the cutscene pages play.
const CUTS={};
async function loadCuts(){
 const get=u=>fetch(new URL(u,location.href)).then(r=>r.json());
 const [intro,shift,absorb]=await Promise.all([get('../intro/timeline.json'),get('../phase-shift/timeline.json'),get(PHASE2+'timeline.json')]);
 CUTS.intro=intro;CUTS.shift=shift;CUTS['blue-first']=absorb.branches['blue-first'];CUTS['rose-first']=absorb.branches['rose-first'];}
// The step whose cuts are on screen: inside the phase 2 page the battle turns into a branch cutscene.
const activeId=()=>STEPS[index].id==='phase2'&&branch?branch:STEPS[index].id;
function inner(){const d=view.contentDocument;if(!d||!d.querySelector)return null;
 const seek=d.querySelector('#seek'),play=d.querySelector('#play')||d.querySelector('#pause'),cues=d.querySelector('#cues');
 const cutscene=!!(seek&&!seek.disabled&&cues&&!cues.hidden&&CUTS[activeId()]);return {d,seek,play,cutscene};}
function seekInner(t){const s=inner();if(!s?.cutscene)return false;const C=CUTS[activeId()];
 // Stop just short of the end so a drag to the right edge does not trigger the automatic hand-off to the next step.
 s.seek.value=Math.min(t,C.duration-.02);s.seek.dispatchEvent(new Event('input',{bubbles:true}));return true;}
function playing(){const s=inner();return !!s?.play&&s.play.textContent.trim()==='일시정지';}
function setPlaying(on){const s=inner();if(s?.play&&playing()!==on)s.play.click();}
function mark(id){document.querySelectorAll('#cutlist .head').forEach(b=>b.classList.toggle('active',b.dataset.step===id));}
function go(i,data={}){index=i;carry={...carry,...data};const s=STEPS[i];$('#end').hidden=true;branch=BRANCH[s.id]?s.id:null;
 view.src=new URL(s.url(carry),location.href).href;$('#status').textContent=s.name;$('#ko').hidden=s.id!=='phase2';mark(s.id);buildTicks();}
// Jump to one cut: load its step when needed, then seek once the page's player is ready.
function jump(stepId,time){const i=STEPS.findIndex(s=>s.id===stepId);
 if(activeId()===stepId&&seekInner(time))return;
 pending={stepId,time,since:performance.now()};go(i);}
view.addEventListener('load',()=>view.focus());
window.addEventListener('message',e=>{if(e.origin!==location.origin||e.source!==view.contentWindow)return;const d=e.data||{},s=STEPS[index];
 if(s.id==='intro'&&d.type==='cutscene-done')go(1,{x:d.x,y:d.y});
 else if(s.id==='phase1'&&d.type==='phase1-ready')view.contentWindow.postMessage({type:'boss-preview-start',spawn:{x:carry.x??224,y:carry.y??380}},location.origin);
 else if(s.id==='phase1'&&d.type==='phase1-state')$('#status').textContent=`1페이즈 전투  ${d.text}`;
 else if(s.id==='phase1'&&d.type==='phase1-break')go(2,{x:d.x,y:d.y,hp:d.hp,mx:d.mx,my:d.my});
 else if(s.id==='shift'&&d.type==='cutscene-done')go(3,{x:d.x,hp:d.hp,y:445});
 // The battle switches to the absorption cutscene inside the same page; show which branch it took.
 else if(d.type==='absorb-start'){branch=d.branch;$('#ko').hidden=true;$('#status').textContent=`흡수 컷씬: ${BRANCH[d.branch]}`;mark(d.branch);buildTicks();}
 else if(d.type==='cutscene-done'&&branch){const other=branch==='blue-first'?'rose-first':'blue-first';
  $('#end-text').textContent=`흡수 컷씬 끝 (${BRANCH[branch]})`;$('#other').textContent=`${BRANCH[other]} 갈래 보기`;$('#other').onclick=()=>go(STEPS.findIndex(x=>x.id===other));
  $('#end').hidden=false;$('#status').textContent=`흡수 컷씬 끝: ${BRANCH[branch]}`;}});
function buildCutList(){
 $('#cutlist').replaceChildren(...STEPS.map((s,i)=>{const row=document.createElement('div');row.className='group';
  const head=document.createElement('button');head.className='head';head.dataset.step=s.id;head.textContent=s.name;head.onclick=()=>go(i);row.append(head);
  for(const c of CUTS[s.id]?.cues??[]){const b=document.createElement('button');b.className='cut';b.dataset.step=s.id;b.dataset.time=c.time;
   b.innerHTML=`<small>${c.time.toFixed(2)}</small>`;b.append(c.name);b.onclick=()=>jump(s.id,c.time);row.append(b);}
  return row;}));}
function buildTicks(){const C=CUTS[activeId()];$('#ticks').replaceChildren(...(C?.cues??[]).map(c=>{const b=document.createElement('button');
 b.title=`${c.time.toFixed(2)}s  ${c.name}`;b.style.left=`${c.time/C.duration*100}%`;b.dataset.time=c.time;b.onclick=()=>jump(activeId(),c.time);return b;}));}
function cutsNow(){const C=CUTS[activeId()],s=inner();if(!C||!s?.cutscene)return null;const t=Number(s.seek.value);return {C,t};}
function stepCut(dir){const now=cutsNow();if(!now)return;const {C,t}=now;
 const k=C.cues.findLastIndex(c=>t>=c.time-.001);const target=dir<0?(t-C.cues[Math.max(0,k)].time>.4?k:k-1):k+1;
 if(target>=0&&target<C.cues.length)seekInner(C.cues[target].time);}
// Mirror the iframe's player state onto the flow bar every frame.
function tick(){
 const s=inner(),id=activeId(),C=CUTS[id];
 // stage2QA is published after each page has wired its controls, so the seek is only sent once it can be heard.
 if(pending&&id===pending.stepId&&s?.cutscene&&view.contentWindow?.stage2QA&&Math.abs(Number(s.seek.max)-C.duration)<.01){seekInner(pending.time);pending=null;}
 if(pending&&performance.now()-pending.since>8000)pending=null;
 const on=!!s?.cutscene;$('#fseek').disabled=!on;$('#prev-cut').disabled=$('#next-cut').disabled=!on;
 $('#toggle').disabled=!s?.play;$('#toggle').textContent=playing()?'일시정지':'재생';
 if(on){const t=Number(s.seek.value);$('#fseek').max=C.duration;if(document.activeElement!==$('#fseek'))$('#fseek').value=t;
  $('#fclock').textContent=`${t.toFixed(2)} / ${C.duration.toFixed(2)}s`;
  const cur=C.cues.filter(c=>t>=c.time-.001).at(-1);
  document.querySelectorAll('#ticks button').forEach(b=>b.classList.toggle('active',Number(b.dataset.time)===cur?.time));
  document.querySelectorAll('#cutlist .cut').forEach(b=>b.classList.toggle('active',b.dataset.step===id&&Number(b.dataset.time)===cur?.time));}
 else{$('#fclock').textContent=STEPS[index].id.startsWith('phase')&&!branch?'전투 중':'—';document.querySelectorAll('#cutlist .cut.active,#ticks .active').forEach(b=>b.classList.remove('active'));}
 }
function frame(){tick();requestAnimationFrame(frame);}
// QA hook: run one mirror step by hand (a hidden browser tab pauses requestAnimationFrame).
window.stage2Flow={tick,jump,get step(){return activeId();}};
// Name tag comparison: the bubbles read the same setting (localStorage) in every step and in the standalone pages.
const TAG_KEY='stage2-speech-tags';try{$('#tags').checked=localStorage.getItem(TAG_KEY)!=='0';}catch{}
$('#tags').onchange=e=>{try{localStorage.setItem(TAG_KEY,e.target.checked?'1':'0');}catch{}const s=view.contentWindow?.stage2Speech;if(s)s.tags=e.target.checked;};
view.addEventListener('load',()=>{const s=view.contentWindow?.stage2Speech;if(s)s.tags=$('#tags').checked;});
$('#fseek').oninput=e=>seekInner(Number(e.target.value));
$('#toggle').onclick=()=>{const s=inner();if(s?.play)s.play.click();};
$('#prev-cut').onclick=()=>stepCut(-1);$('#next-cut').onclick=()=>stepCut(1);
// Keys on the flow page itself (the iframe keeps its own keys while it has focus).
document.addEventListener('keydown',e=>{if(/INPUT|TEXTAREA|SELECT|BUTTON/.test(e.target.tagName)&&e.target.id!=='fseek')return;
 if(e.key===' '){e.preventDefault();$('#toggle').click();}else if(e.key==='['){stepCut(-1);}else if(e.key===']'){stepCut(1);}});
// Force who falls first in the phase 2 battle, so either branch can be reached without a full fight.
document.querySelectorAll('[data-first]').forEach(b=>b.onclick=()=>view.contentWindow.document.querySelector(`[data-ko=${b.dataset.first}]`)?.click());
$('#restart').onclick=$('#again').onclick=()=>{carry={};go(0);};
$('#next').onclick=()=>{if(index<3)go(index+1);};
loadCuts().catch(e=>console.error(e)).finally(()=>{buildCutList();go(0);requestAnimationFrame(frame);});
