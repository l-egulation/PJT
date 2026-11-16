'use strict';
const DATA=JSON.parse(document.getElementById('data').textContent),$=id=>document.getElementById(id),STORE='deviant-explosion-smallbomb-v1';
const base={slot:'basic',pick:{},stars:{},memo:{},expGain:100,atkGain:100,masGain:75};
let st={...base};try{st={...base,...JSON.parse(localStorage.getItem(STORE)||'{}')};}catch{}
function save(){try{localStorage.setItem(STORE,JSON.stringify(st));}catch{}}
let ac,master,expBus,atkBus,nodes=[],timer=null,token=0;const buffers=new Map();
const slot=()=>DATA.slots.find(s=>s.id===st.slot)||DATA.slots[0];
const pick=()=>{const s=slot();return s.candidates.find(c=>c.number===st.pick[s.id])||s.candidates[0];};
function esc(t){return String(t).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));}
function wave(el,key,scale){for(const v of DATA.audio[key].wave){const i=document.createElement('i');i.style.height=Math.max(2,v/scale*32)+'px';el.append(i);}}
function render(){
  $('tabs').replaceChildren(...DATA.slots.map(s=>{const b=document.createElement('button');b.role='tab';b.textContent=s.name;b.setAttribute('aria-selected',s.id===st.slot);b.onclick=()=>{stop();st.slot=s.id;save();render();};return b;}));
  const s=slot(),p=pick(),stars=st.stars[s.id]||[];$('uses').textContent='쓰이는 곳: '+s.uses+' · 지금 파일: '+s.currentFile;
  const scale=Math.max(...[s.current,...s.candidates.map(c=>c.key)].map(k=>Math.max(...DATA.audio[k].wave)));
  const cur=document.createElement('article');cur.className='card current';
  cur.innerHTML=`<div class="top"><span class="num">지금 게임 소리 · ${DATA.audio[s.current].duration.toFixed(2)}초</span></div><h3>지금 소리 (비교 기준)</h3><div class="wave"></div><p>교체 대상인 지금 소리입니다.</p><div class="row"><button>단독 듣기</button></div>`;
  wave(cur.querySelector('.wave'),s.current,scale);cur.querySelector('button').onclick=()=>play([{solo:'current'}]);
  const cards=[cur];
  for(const c of s.candidates){const el=document.createElement('article');el.className='card'+(c.number===p.number?' active':'');const on=stars.includes(c.number);
    el.innerHTML=`<div class="top"><span class="num">${c.number}번 · ${c.duration.toFixed(2)}초</span><button class="star ${on?'on':''}" aria-pressed="${on}" aria-label="${c.number}번 별표">${on?'★':'☆'}</button></div><div class="voice">${esc(c.voice)}</div><h3>${esc(c.name)}</h3><div class="wave"></div><p>${esc(c.description)}</p><div class="src">원음 ${esc(c.source)}</div><div class="row" style="margin-top:10px"><button data-m="sel">선택</button><button data-m="solo">단독</button><button data-m="ab">지금 → 이것</button></div>`;
    wave(el.querySelector('.wave'),c.key,scale);
    el.querySelector('.star').onclick=()=>{const l=st.stars[s.id]||[];st.stars[s.id]=on?l.filter(n=>n!==c.number):[...l,c.number].sort();save();render();};
    for(const b of el.querySelectorAll('[data-m]'))b.onclick=()=>{st.pick[s.id]=c.number;save();render();if(b.dataset.m==='solo')play([{solo:c.key}]);if(b.dataset.m==='ab')play([{solo:'current'},{solo:c.key}]);};
    cards.push(el);}
  $('cards').replaceChildren(...cards);
  $('selected').textContent=`${s.name} · ${p.number}번 ${p.name}`;
  $('contexts').replaceChildren(...s.contexts.map(c=>{const d=document.createElement('div');d.className='ctx';d.innerHTML=`<strong>${esc(c.label)}</strong><div class="row" style="margin-top:6px"><button data-k="cand">이 후보</button><button data-k="cur">지금 소리</button><button data-k="ab">지금 → 이 후보</button></div>`;
    d.querySelector('[data-k=cand]').onclick=()=>play([{ctx:c,key:pick().key}]);d.querySelector('[data-k=cur]').onclick=()=>play([{ctx:c,key:s.current}]);d.querySelector('[data-k=ab]').onclick=()=>play([{ctx:c,key:s.current},{ctx:c,key:pick().key}]);return d;}));
  $('memo').value=st.memo[s.id]||'';levels();
}
function levels(){$('expVal').textContent=st.expGain+'%';$('atkVal').textContent=st.atkGain+'%';$('masVal').textContent=st.masGain+'%';$('expGain').value=st.expGain;$('atkGain').value=st.atkGain;$('masGain').value=st.masGain;
  if(ac){expBus.gain.value=st.expGain/100;atkBus.gain.value=st.atkGain/100*.8;master.gain.value=st.masGain/100;}}
async function ctx(){if(!ac){ac=new AudioContext();master=ac.createGain();expBus=ac.createGain();atkBus=ac.createGain();const lim=ac.createDynamicsCompressor();lim.threshold.value=-2;lim.knee.value=3;lim.ratio.value=12;lim.attack.value=.002;lim.release.value=.1;expBus.connect(master);atkBus.connect(master);master.connect(lim);lim.connect(ac.destination);}await ac.resume();levels();}
function decode(key){if(!buffers.has(key)){const b=Uint8Array.from(atob(DATA.audio[key].src.split(',')[1]),c=>c.charCodeAt(0));buffers.set(key,ac.decodeAudioData(b.buffer));}return buffers.get(key);}
function stop(msg='정지됨'){token++;clearTimeout(timer);for(const n of nodes){try{n.stop();}catch{}}nodes=[];$('status').textContent=msg;}
function events(item){const s=slot();if(item.solo)return[{key:item.solo==='current'?s.current:item.solo,t:0,bus:'exp'}];
  if(item.burst)return[0,.15,.3].map(t=>({key:item.key,t,bus:'exp'}));
  return[{key:item.ctx.audio,t:0,bus:'atk'},...item.ctx.explosions.map(t=>({key:item.key,t,bus:'exp'}))];}
function label(item){const s=slot();const name=k=>k===s.current||k==='current'?'지금 소리':(s.candidates.find(c=>c.key===k)?.number+'번 '+s.candidates.find(c=>c.key===k)?.name);
  if(item.solo)return name(item.solo)+' 단독';if(item.burst)return name(item.key)+' 3개 겹침';return item.ctx.label+' + '+name(item.key);}
async function play(queue){stop('준비 중…');const my=token;await ctx();let i=0;
  async function next(){if(my!==token)return;if(i>=queue.length){$('status').textContent='재생 완료';return;}const item=queue[i++];const ev=events(item);
    const bufs=await Promise.all(ev.map(e=>decode(e.key)));if(my!==token)return;const t0=ac.currentTime+.06;let end=0;
    ev.forEach((e,j)=>{const n=ac.createBufferSource();n.buffer=bufs[j];n.connect(e.bus==='exp'?expBus:atkBus);n.start(t0+e.t);nodes.push(n);end=Math.max(end,e.t+bufs[j].duration);});
    $('status').textContent=`${i}/${queue.length} · ${label(item)}`;timer=setTimeout(next,(end+.45)*1000);}
  await next();}
$('solo').onclick=()=>play([{solo:pick().key}]);$('ab').onclick=()=>play([{solo:'current'},{solo:pick().key}]);$('stop').onclick=()=>stop();
$('burst').onclick=()=>play([{burst:true,key:pick().key}]);$('all').onclick=()=>play([{solo:'current'},...slot().candidates.map(c=>({solo:c.key}))]);
for(const [id,k] of [['expGain','expGain'],['atkGain','atkGain'],['masGain','masGain']])$(id).oninput=()=>{st[k]=Number($(id).value);save();levels();};
$('memo').oninput=()=>{st.memo[slot().id]=$('memo').value;save();};
$('export').onclick=()=>{const out={schema:1,kind:'deviant-explosion-smallbomb-v1-selection',picks:Object.fromEntries(DATA.slots.map(s=>[s.id,{selected:(s.candidates.find(c=>c.number===st.pick[s.id])||s.candidates[0]).number,stars:st.stars[s.id]||[],memo:st.memo[s.id]||''}])),levels:{explosion:st.expGain,attack:st.atkGain,master:st.masGain},gameApplied:false};
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(out,null,2)],{type:'application/json'}));a.download='deviant-explosion-smallbomb-v1-selection.json';a.click();$('status').textContent='선택 파일을 저장했습니다.';};
$('import').onclick=()=>$('importFile').click();$('importFile').onchange=async()=>{try{const v=JSON.parse(await $('importFile').files[0].text());if(v.kind!=='deviant-explosion-smallbomb-v1-selection')throw Error('이 화면의 선택 파일이 아닙니다.');
  for(const [k,p] of Object.entries(v.picks||{})){st.pick[k]=p.selected;st.stars[k]=p.stars||[];st.memo[k]=p.memo||'';}Object.assign(st,{expGain:v.levels?.explosion??100,atkGain:v.levels?.attack??100,masGain:v.levels?.master??75});save();render();$('status').textContent='선택 파일을 불러왔습니다.';}catch(e){$('status').textContent=e.message;}finally{$('importFile').value='';}};
document.addEventListener('keydown',e=>{if(e.code==='Escape')stop();});render();

// Reference recording: decoded locally, its loudest event is cut out and matched to the candidate's level.
let refBuf=null;
function loudestRms(b){const d=b.getChannelData(0),w=Math.round(b.sampleRate*.4);let best=0;for(let i=0;i+w<=d.length;i+=Math.round(w/4)){let s=0;for(let j=i;j<i+w;j++)s+=d[j]*d[j];best=Math.max(best,Math.sqrt(s/w));}return best||1e-6;}
function refSegment(b){const d=b.getChannelData(0),n=Math.round(b.sampleRate*.01);let peak=0,at=0;for(let i=0;i+n<d.length;i+=n){let s=0;for(let j=i;j<i+n;j++)s+=d[j]*d[j];if(s>peak){peak=s;at=i;}}let on=at;for(let i=at;i>0;i-=n){let s=0;for(let j=i;j<i+n;j++)s+=d[j]*d[j];if(s<peak*.01){on=i;break;}}const start=Math.max(0,on-Math.round(b.sampleRate*.05)),len=Math.min(d.length-start,Math.round(b.sampleRate*1.6));const out=ac.createBuffer(1,len,b.sampleRate);out.getChannelData(0).set(d.subarray(start,start+len));return out;}
async function refPlay(withCandidate){stop('준비 중…');const my=token;await ctx();const cand=await decode(pick().key);const seg=refSegment(refBuf);const g=loudestRms(cand)/loudestRms(seg);
  const t0=ac.currentTime+.06;const a=ac.createBufferSource(),ga=ac.createGain();a.buffer=seg;ga.gain.value=g;a.connect(ga);ga.connect(expBus);a.start(t0);nodes.push(a);$('status').textContent='참고 소리 (같은 크기로 맞춤)';
  if(withCandidate){timer=setTimeout(()=>{if(my!==token)return;const b=ac.createBufferSource();b.buffer=cand;b.connect(expBus);b.start();nodes.push(b);$('status').textContent='이 후보 · '+pick().name;},(seg.duration+.45)*1000);}}
$('refLoad').onclick=()=>$('refFile').click();
$('refFile').onchange=async()=>{try{const f=$('refFile').files[0];if(!f)return;await ctx();refBuf=await ac.decodeAudioData(await f.arrayBuffer());$('refPlay').disabled=$('refAB').disabled=false;$('refInfo').textContent='불러옴: '+f.name+' · 가장 큰 소리 구간 1.6초를 후보와 같은 크기로 맞춰 재생합니다.';}catch(e){$('refInfo').textContent='불러오지 못했습니다: '+e.message;}};
$('refPlay').onclick=()=>refPlay(false);$('refAB').onclick=()=>refPlay(true);
