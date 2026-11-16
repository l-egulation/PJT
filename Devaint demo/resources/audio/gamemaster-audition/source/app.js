'use strict';
const ROOT = JSON.parse(document.getElementById('audition-data').textContent);
const $ = id => document.getElementById(id);
const KEY = 'lost-words-all-weapons-audition-v2';
let savedRoot = {};
try { const value=JSON.parse(localStorage.getItem(KEY)||'{}');if(value&&typeof value==='object')savedRoot=value; } catch (_) {}
if(!savedRoot.weapons||typeof savedRoot.weapons!=='object')savedRoot.weapons={};
for(const weapon of ROOT.weapons){
  if(!savedRoot.weapons[weapon.id])savedRoot.weapons[weapon.id]={};
  if(savedRoot.weapons[weapon.id].candidateRevision!==weapon.candidateRevision)savedRoot.weapons[weapon.id]={candidateRevision:weapon.candidateRevision};
  if(weapon.choice&&!savedRoot.weapons[weapon.id].choice){
    savedRoot.weapons[weapon.id].choice=weapon.choice;
    savedRoot.weapons[weapon.id].selected=weapon.choice.number-1;
  }
}
let DATA, selected=0, favorites=new Set(), offsets=[], layerOffsets=[], choice=null;
const COUNT = 11;
const images = new Map();
const audioCache = new Map();
let ac, master, buffers = [], sources = [], generation = 0, running = false, mode = 'once', cycle = null, currentTime = 0, ready = false;
let viewport={scale:2.6,x:475,y:382};
const ctx = $('scene').getContext('2d');
const status = (message, error = false) => { $('status').textContent = message; $('status').classList.toggle('error', error); };
function persist() {
  if(!DATA)return;
  savedRoot.active=DATA.id;
  if(choice?.number===selected+1){choice.offsetMs=DATA.sounds[selected].current?0:offsets[selected];choice.layerOffsetsMs=DATA.sounds[selected].layers?{...layerOffsets[selected]}:undefined;}
  savedRoot.weapons[DATA.id]={selected,favorites:[...favorites],offsets,layerOffsets,choice,candidateRevision:DATA.candidateRevision};
  try { localStorage.setItem(KEY, JSON.stringify(savedRoot)); } catch (_) {}
}
function hydrate(weapon){
  const candidates=(weapon.reviewCandidates||weapon.candidates).map((key,i)=>typeof key==='string'?({...ROOT.audio[key],number:i+1}):
    ({...key,variants:Object.fromEntries(Object.entries(key.variants).map(([hit,id])=>[hit,ROOT.audio[id]])),
      layers:key.layers?Object.fromEntries(Object.entries(key.layers).map(([hit,stems])=>[hit,Object.fromEntries(Object.entries(stems).map(([name,id])=>[name,ROOT.audio[id]]))])):undefined}));
  return {...weapon,frames:ROOT.frames,sounds:[...candidates,
    {number:11,name:`현재 적용 중 · ${weapon.label} 기본공격`,current:true,
      variants:Object.fromEntries(Object.entries(weapon.currentVariants).map(([key,id])=>[key,ROOT.audio[id]]))}]};
}
function renderProgress(){
  const finished=ROOT.weapons.filter(w=>savedRoot.weapons[w.id]?.choice).length;
  $('progress').textContent=`${finished} / ${ROOT.weapons.length}종 선택 완료`;
  $('weaponTabs').replaceChildren();
  for(const weapon of ROOT.weapons){
    const picked=savedRoot.weapons[weapon.id]?.choice;
    const button=document.createElement('button');
    button.textContent=`${weapon.label}${picked?' · '+String(picked.number).padStart(2,'0')+' ✓':''}`;
    button.classList.toggle('active',weapon.id===DATA.id);
    button.setAttribute('aria-pressed',String(weapon.id===DATA.id));button.onclick=()=>activateWeapon(weapon.id);
    $('weaponTabs').append(button);
  }
  $('chosen').textContent=choice?`선택 완료 · ${String(choice.number).padStart(2,'0')}번${choice.number===11?' (현재 적용음)':''}`:'아직 선택하지 않았습니다.';
}
function activateWeapon(id){
  if(DATA)persist();stop();
  DATA=hydrate(ROOT.weapons.find(w=>w.id===id)||ROOT.weapons[0]);
  const saved=savedRoot.weapons[DATA.id]||{};
  selected=Number.isInteger(saved.selected)&&saved.selected>=0&&saved.selected<COUNT?saved.selected:0;
  favorites=new Set((Array.isArray(saved.favorites)?saved.favorites:[]).filter(n=>Number.isInteger(n)&&n>=1&&n<=COUNT));
  offsets=Array.from({length:COUNT},(_,i)=>Number.isFinite(saved.offsets?.[i])?Math.max(-200,Math.min(200,saved.offsets[i])):0);
  layerOffsets=Array.from({length:COUNT},(_,i)=>Object.fromEntries(['swing','impact'].map(name=>[name,Number.isFinite(saved.layerOffsets?.[i]?.[name])?Math.max(-400,Math.min(400,saved.layerOffsets[i][name])):0])));
  $('layerListen').value='both';
  choice=saved.choice||null;buffers=[];currentTime=0;
  $('weapon').value=DATA.id;
  $('motion').replaceChildren();
  const option=(value,text)=>{const o=document.createElement('option');o.value=value;o.textContent=text;return o;};
  $('motion').append(option('combo',`기본공격 전체 · ${DATA.basics.length}타`));
  DATA.basics.forEach((_,i)=>$('motion').append(option('attack'+(i+1),`기본공격 ${i+1}타`)));
  $('speed').replaceChildren();
  const speeds=[...new Set([.5,.8,1,1.4,1.5,2,DATA.defaultSpeed])].sort((a,b)=>a-b);
  for(const speed of speeds)$('speed').append(option(String(speed),`${speed}×${speed===DATA.defaultSpeed?' · 현재 게임 기준':''}`));
  $('speed').value=String(DATA.defaultSpeed);
  $('normalize').disabled=!!DATA.reviewCandidates;$('normalize').checked=!DATA.reviewCandidates;
  $('weaponHint').textContent=DATA.hint;
  $('candidateHeading').textContent=DATA.label+' · '+DATA.eventLabel+' 후보';
  $('canvasLabel').textContent=DATA.label+' 기본공격';
  let left=-80,right=100,top=-110,bottom=10;
  for(const clip of Object.values(DATA.clips))for(const ref of clip.frames){
    const f=ROOT.frames[ref],u=32/f.ppu;
    left=Math.min(left,(f.bounds[0]-f.pivot[0])*u);right=Math.max(right,(f.bounds[2]-f.pivot[0])*u);
    top=Math.min(top,(f.bounds[1]-f.pivot[1])*u);bottom=Math.max(bottom,(f.bounds[3]-f.pivot[1])*u);
  }
  const scale=Math.min(2.6,780/(right-left),300/(bottom-top));
  viewport={scale,x:500-(left+right)*scale/2,y:375-bottom*scale};
  buildCandidates();updateSelection();render(0);renderProgress();
  status(`${DATA.label} · 새 후보 10개와 현재 적용음을 비교하세요.`);
}
function plan() {
  const names = $('motion').value === 'combo' ? DATA.basics.map((_,i)=>'attack'+(i+1)) : [$('motion').value];
  let at = 0;
  const segments = names.map(name => {
    const basic = DATA.basics[Number(name.replace('attack',''))-1];
    const segment = {name, start: at, duration: basic.duration, impact: basic.impact, active: basic.active};
    at += basic.duration;
    return segment;
  });
  return {segments, duration: at, speed: Number($('speed').value)};
}
function frameAt(clip, time) {
  let elapsed = 0;
  for (let i = 0; i < clip.seconds.length; i++) {
    elapsed += clip.seconds[i];
    if (time < elapsed - 1e-8) return i;
  }
  return clip.seconds.length - 1;
}
function mapSourceTime(age,impact,end,sourceImpact,sourceDuration){
  return age<impact&&impact>0?age/impact*sourceImpact:sourceImpact+(age-impact)/Math.max(.001,end-impact)*(sourceDuration-sourceImpact);
}
function render(time = currentTime, idle = false) {
  const p = plan();
  currentTime = Math.max(0, Math.min(p.duration, time));
  let segment = p.segments.find(s => currentTime < s.start + s.duration - 1e-8) || p.segments.at(-1);
  let clip = DATA.clips[segment.name], local = currentTime - segment.start;
  const sourceImpact=clip.sourceImpact>=0?clip.sourceImpact:clip.seconds.slice(0,Math.max(0,clip.contact||0)).reduce((a,b)=>a+b,0);
  let sourceTime=mapSourceTime(local,segment.impact,segment.duration,sourceImpact,clip.seconds.reduce((a,b)=>a+b,0));
  if (idle) { clip = DATA.clips.idle; sourceTime = (performance.now() / 1000) % clip.seconds.reduce((a,b) => a+b, 0); }
  const index = frameAt(clip, sourceTime), ref = clip.frames[index], frame = DATA.frames[ref], image = images.get(ref);
  ctx.fillStyle = '#182322'; ctx.fillRect(0, 0, 1000, 440);
  ctx.strokeStyle = '#22312d'; ctx.lineWidth = 1;
  for (let x = 20; x < 1000; x += 40) {ctx.beginPath();ctx.moveTo(x,65);ctx.lineTo(x,383);ctx.stroke();}
  for (let y = 63; y < 384; y += 40) {ctx.beginPath();ctx.moveTo(20,y);ctx.lineTo(980,y);ctx.stroke();}
  ctx.fillStyle = '#0e1916';ctx.beginPath();ctx.ellipse(viewport.x,viewport.y,65,8,0,0,Math.PI*2);ctx.fill();
  ctx.strokeStyle = '#607b6b';ctx.beginPath();ctx.moveTo(50,viewport.y);ctx.lineTo(950,viewport.y);ctx.stroke();
  if (image) {
    ctx.save();ctx.translate(viewport.x,viewport.y);if ($('flip').checked)ctx.scale(-1,1);ctx.imageSmoothingEnabled=false;
    const scale = viewport.scale*32/frame.ppu;ctx.drawImage(image,-frame.pivot[0]*scale,-frame.pivot[1]*scale,image.width*scale,image.height*scale);ctx.restore();
  }
  const fx=DATA.effects[segment.name];
  if(!idle&&$('showFx').checked&&fx&&clip.fxStart>=0){
    const elapsed=sourceTime-clip.fxStart;
    if(elapsed>=0&&elapsed<fx.seconds.reduce((a,b)=>a+b,0)){
      const n=frameAt(fx,elapsed),f=ROOT.frames[fx.frames[n]],im=images.get(fx.frames[n]);
      if(im){
        ctx.save();ctx.translate(viewport.x,viewport.y);if($('flip').checked)ctx.scale(-1,1);
        ctx.translate((fx.x[n]||0)*32*viewport.scale,-(fx.y[n]||0)*32*viewport.scale);
        ctx.rotate(-(fx.rotation||0)*Math.PI/180);if(fx.flipX)ctx.scale(-1,1);ctx.imageSmoothingEnabled=false;
        const scale=viewport.scale*32/f.ppu*(fx.scale||1);
        ctx.drawImage(im,-f.pivot[0]*scale,-f.pivot[1]*scale,im.width*scale,im.height*scale);ctx.restore();
      }
    }
  }
  const contact = !idle && local >= segment.impact && local < segment.impact+segment.active;
  const ranged=['launcher','bomb','shuriken'].includes(DATA.id);
  $('phase').textContent = idle ? '대기' : `${segment.name.replace('attack','')}타 · ${contact ? (ranged?'발사 · 투척':'타격 구간') : local < segment.impact ? '준비 · 공격' : '회수'}`;
  $('phase').style.color = contact ? '#efbf75' : '#9bdfcc';
  $('frameInfo').textContent = `${ref.split('/').at(-1)} · ${index+1}/${clip.frames.length} 프레임`;
  const y = 412, x0 = 70, width = 860;
  ctx.fillStyle='#3d5148';ctx.fillRect(x0,y,width,3);
  for (const s of p.segments) {
    const x = x0 + (s.start+s.impact)/p.duration*width;
    ctx.fillStyle = '#efbf75';ctx.fillRect(x-2,y-6,4,15);
  }
  ctx.fillStyle='#9bdfcc';ctx.beginPath();ctx.arc(x0+currentTime/p.duration*width,y+1,5,0,Math.PI*2);ctx.fill();
  $('scrub').max = Math.round(p.duration*1000);$('scrub').value = Math.round(currentTime*1000);
  $('timeLabel').textContent = `${Math.round(currentTime*1000)} / ${Math.round(p.duration*1000)} ms`;
  $('hitTimes').textContent = (ranged?'발사·투척 시점 ':'타격 시점 ')+p.segments.map(s => Math.round((s.start+s.impact)/p.speed*1000)).join(' / ')+' ms';
}
function drawWave() {
  const candidate = DATA.sounds[selected];
  const variantKey = $('motion').value === 'combo' ? 'attack1' : $('motion').value;
  const sound = candidate.variants ? candidate.variants[variantKey] : candidate, c = $('wave').getContext('2d');
  c.clearRect(0,0,800,64);c.fillStyle='#7cc8b1';
  const max = Math.max(...sound.waveform, .01), step=800/sound.waveform.length;
  sound.waveform.forEach((v,i) => {const h = Math.max(1,v/max*48);c.fillRect(i*step,32-h/2,Math.max(1,step-1),h);});
  c.fillStyle='#efbf75';c.fillRect(sound.peakTime/sound.duration*800,0,2,64);
  $('soundInfo').textContent = `${candidate.current ? '현재 '+variantKey.replace('attack','')+'타 원음 · ' : ''}${Math.round(sound.duration*1000)} ms · 피크 ${Math.round(sound.peakTime*1000)} ms`;
}
function updateSelection() {
  const sound = DATA.sounds[selected];
  $('selectedTitle').textContent = `${String(sound.number).padStart(2,'0')} · ${sound.name}`;
  $('offset').value=offsets[selected];$('offsetLabel').textContent=`${offsets[selected]>0?'+':''}${offsets[selected]} ms`;
  $('offset').disabled=!!sound.current;$('resetOffset').disabled=!!sound.current;
  if(sound.current)$('offsetLabel').textContent='게임 기준';
  $('alignmentNote').textContent=sound.current?'현재 적용음 · 게임 시작 시점·재생 배율 유지':sound.authored?'타별 편집 시안 · 2타 바닥 충격 포함':sound.fixedMix?'단일 음원 · 설계 음량 유지':'소리 피크를 타격에 정렬';
  $('layerEditor').hidden=!sound.layers;
  if(sound.layers){
    for(const name of ['swing','impact']){
      const ms=layerOffsets[selected][name];
      $(name+'Offset').value=String(ms);$(name+'OffsetNumber').value=String(ms);
      $(name+'Position').textContent=`2타 시작 후 ${Math.round((sound.layers.attack2[name].accentSeconds*1000+ms)/Number($('speed').value)+offsets[selected])} ms에 가장 강하게`;
    }
  }
  document.querySelectorAll('.candidate').forEach((row, i) => {
    row.classList.toggle('selected', i===selected);row.querySelector('.pick').setAttribute('aria-pressed', String(i===selected));
    row.classList.toggle('chosen',choice?.number===i+1);
    row.querySelector('.star').setAttribute('aria-pressed', String(favorites.has(i+1)));
    row.querySelector('.star').textContent=favorites.has(i+1)?'★':'☆';
  });
  $('favorites').textContent = favorites.size ? '표시한 후보 · '+[...favorites].sort((a,b)=>a-b).map(n=>String(n).padStart(2,'0')).join(', ') : '표시한 후보가 없습니다.';
  drawWave();persist();renderProgress();
}
async function prepareAudio() {
  if (!ac) {ac = new AudioContext();master=ac.createGain();master.connect(ac.destination);}
  await ac.resume();master.gain.value=Number($('volume').value)/100;
  return Promise.all(DATA.sounds.map(async sound => {
    const clips={...(sound.variants || {default:sound})};
    for(const [hit,stems] of Object.entries(sound.layers||{}))for(const [name,clip] of Object.entries(stems))clips[hit+':'+name]=clip;
    const entries=await Promise.all(Object.entries(clips).map(async ([key,s])=>{
      if(!audioCache.has(s.sha256)){
        const bytes=Uint8Array.from(atob(s.src.split(',')[1]),ch=>ch.charCodeAt(0));
        audioCache.set(s.sha256,ac.decodeAudioData(bytes.buffer).catch(error=>{audioCache.delete(s.sha256);throw error;}));
      }
      return [key,await audioCache.get(s.sha256)];
    }));
    return Object.fromEntries(entries);
  }));
}
function clearAudio() {
  for (const source of sources) {try {source.stop();} catch (_) {} source.disconnect();}
  sources=[];
}
function stop(message = '정지됨') {
  generation++;running=false;cycle=null;clearAudio();
  $('loop').classList.remove('active');$('sequence').classList.remove('active');
  $('loop').setAttribute('aria-pressed','false');$('sequence').setAttribute('aria-pressed','false');
  status(message);
}
function playbackEvents(p, sound, offsetMs) {
  return p.segments.flatMap(s=>{
    if(sound.layers?.[s.name])return Object.entries(sound.layers[s.name])
      .filter(([name])=>$('layerListen').value==='both'||$('layerListen').value===name)
      .map(([name,clip])=>({time:(s.start+layerOffsets[selected][name]/1000)/p.speed+offsetMs/1000,
        rate:clip.nominalDuration/s.duration*p.speed,clip,key:s.name+':'+name}));
    const clip=sound.variants ? sound.variants[s.name] : sound;
    const rate=sound.current||sound.authored ? clip.nominalDuration/s.duration*p.speed : 1;
    const time=sound.current ? s.start/p.speed : sound.authored ? s.start/p.speed+offsetMs/1000 : (s.start+s.impact)/p.speed-clip.peakTime+offsetMs/1000;
    return [{time,rate,clip,key:sound.variants?s.name:'default'}];
  });
}
function scheduleCycle() {
  const p=plan(), sound=DATA.sounds[selected];
  const events=playbackEvents(p,sound,offsets[selected]);
  const lead=Math.max(.18,-Math.min(...events.map(e=>e.time))+.04), origin=ac.currentTime+.06+lead;
  const finish=Math.max(p.duration/p.speed,...events.map(e=>e.time+buffers[selected][e.key].duration/e.rate));
  cycle={origin,finish,p,selected};
  for (const event of events) {
    const source=ac.createBufferSource(), gain=ac.createGain();source.buffer=buffers[selected][event.key];
    source.playbackRate.value=event.rate;
    gain.gain.value=sound.fixedMix?1:$('normalize').checked?event.clip.comparisonGain:(event.clip.runtimeGain || 1);
    source.connect(gain);gain.connect(master);sources.push(source);
    source.onended=()=>{source.disconnect();gain.disconnect();sources=sources.filter(s=>s!==source);};
    source.start(origin+event.time);
  }
  status(`${mode==='sequence'?'순서대로 비교':mode==='loop'?'반복 재생':'재생 중'} · 후보 ${sound.number} · ${$('motion').selectedOptions[0].textContent}`);
}
async function play(nextMode = 'once') {
  if (!ready) return;
  stop('소리를 준비하고 있습니다…');const ticket=generation;mode=nextMode;
  try {
    const decoded=await prepareAudio();if(ticket!==generation)return;buffers=decoded;
    running=true;
    for(const key of ['loop','sequence']) {$(key).classList.toggle('active',mode===key);$(key).setAttribute('aria-pressed',String(mode===key));}
    scheduleCycle();
  } catch(error) {if(ticket===generation){stop();status('음원을 재생하지 못했습니다. 재생 버튼을 다시 눌러주세요. '+error.message,true);}}
}
function select(index, autoplay = true) {
  const nextMode=running&&mode==='loop'?'loop':'once';
  stop();selected=(index+COUNT)%COUNT;currentTime=0;updateSelection();render(0);if(autoplay)void play(nextMode);
}
function audibleTime() {
  // currentTime is the render clock, ahead of the sound reaching the output device.
  const stamp=ac.getOutputTimestamp?.();
  if(stamp&&stamp.contextTime>0&&Number.isFinite(stamp.performanceTime))
    return Math.min(ac.currentTime,stamp.contextTime+Math.max(0,performance.now()-stamp.performanceTime)/1000);
  return Math.max(0,ac.currentTime-(ac.baseLatency||0)-(ac.outputLatency||0));
}
function tick() {
  if(running&&cycle) {
    const t=audibleTime()-cycle.origin;
    render(Math.max(0,Math.min(cycle.p.duration,t*cycle.p.speed)),t<0 || t>cycle.p.duration/cycle.p.speed);
    if(t>cycle.finish+.4) {
      if(mode==='loop')scheduleCycle();
      else if(mode==='sequence'&&selected<COUNT-1){selected++;updateSelection();scheduleCycle();}
      else stop(mode==='sequence'?`후보 ${COUNT}개 비교가 끝났습니다.`:'재생 완료');
    }
  }
  requestAnimationFrame(tick);
}
function buildCandidates(){
$('candidates').replaceChildren();
DATA.sounds.forEach((sound,i)=>{
  const row=document.createElement('div');row.className='candidate'+(sound.current?' current':'');
  const pick=document.createElement('button');pick.className='pick';pick.disabled=!ready;
  pick.setAttribute('aria-label',`후보 ${sound.number} ${sound.name} 모션과 재생`);
  const number=document.createElement('span');number.className='number';number.textContent=String(sound.number).padStart(2,'0');
  const text=document.createElement('span');
  const name=document.createElement('span');name.className='filename';name.textContent=sound.name;
  const duration=document.createElement('span');duration.className='duration';duration.textContent=sound.current?`게임에 연결된 ${DATA.basics.length}타 음원 · 비교 기준`:sound.authored?'1타 휘두름 · 2타 휘두름 + 바닥 충격':`${Math.round(sound.duration*1000)} ms · 모션과 재생`;
  text.append(name,duration);pick.append(number,text);pick.onclick=()=>select(i);
  const star=document.createElement('button');star.className='star';star.setAttribute('aria-label',`후보 ${sound.number} 마음에 듦 표시`);
  star.onclick=()=>{favorites.has(sound.number)?favorites.delete(sound.number):favorites.add(sound.number);updateSelection();};
  row.append(pick,star);$('candidates').append(row);
});
}
$('weapon').onchange=()=>activateWeapon($('weapon').value);
$('nextWeapon').onclick=()=>activateWeapon(ROOT.weapons[(ROOT.weapons.findIndex(w=>w.id===DATA.id)+1)%ROOT.weapons.length].id);
$('confirmChoice').onclick=()=>{
  const sound=DATA.sounds[selected];
  choice={number:sound.number,file:sound.file||sound.recipeId||'current-runtime',recipeId:sound.recipeId,sha256:sound.sha256,archiveEntry:sound.archiveEntry,
    offsetMs:sound.current?0:offsets[selected],status:'user-selected; not integrated'};
  persist();updateSelection();status(`${DATA.label} ${sound.number}번을 선택했습니다. 다른 무기도 이어서 고를 수 있습니다.`);
};
$('currentSound').onclick=()=>select(DATA.sounds.findIndex(s=>s.current));
$('play').onclick=()=>play();$('loop').onclick=()=>running&&mode==='loop'?stop():play('loop');
$('sequence').onclick=()=>{if(running&&mode==='sequence'){stop();return;}select(0,false);void play('sequence');};
$('stop').onclick=()=>{stop();render(0);};
for(const id of ['motion','speed','normalize'])$(id).onchange=()=>{const repeat=running&&mode==='loop';stop();updateSelection();render(0);if(repeat)void play('loop');};
$('flip').onchange=()=>render();
$('showFx').onchange=()=>render();
$('scrub').oninput=()=>{stop('프레임 확인 중 · 재생을 누르면 처음부터 시작합니다.');render(Number($('scrub').value)/1000);};
$('offset').oninput=()=>{offsets[selected]=Number($('offset').value);updateSelection();};
$('offset').onchange=()=>{const repeat=running&&mode==='loop';stop('소리 시점 저장됨');if(repeat)void play('loop');};
$('resetOffset').onclick=()=>{offsets[selected]=0;updateSelection();$('offset').onchange();};
function restartLayerPreview(){
  const repeat=running&&mode==='loop';stop('2타 소리 위치 저장됨');updateSelection();render(0);
  if(repeat)void play('loop');
}
for(const name of ['swing','impact'])for(const suffix of ['Offset','OffsetNumber']){
  const save=()=>{
    const value=Number($(name+suffix).value);if(!Number.isFinite(value))return;
    layerOffsets[selected][name]=Math.max(-400,Math.min(400,Math.round(value/5)*5));updateSelection();
  };
  if(suffix==='Offset')$(name+suffix).oninput=save;
  $(name+suffix).onchange=()=>{save();restartLayerPreview();};
}
$('layerListen').onchange=()=>{
  $('motion').value='attack2';restartLayerPreview();
};
$('resetLayers').onclick=()=>{layerOffsets[selected]={swing:0,impact:0};$('layerListen').value='both';restartLayerPreview();};
$('playLayers').onclick=()=>{$('motion').value='attack2';updateSelection();void play('loop');};
$('volume').oninput=()=>{$('volumeLabel').textContent=$('volume').value+'%';if(master)master.gain.setTargetAtTime(Number($('volume').value)/100,ac.currentTime,.015);};
function exportSelection(){
  persist();
  return {schemaVersion:3,status:'audition selections; not integrated',layerOffsetUnits:'milliseconds at 1x, attack2 only; negative=earlier',
    alignment:'Candidates: 8 ms energy peak aligned to impact. Current: attack start and runtime playback rate.',
    weapons:ROOT.weapons.map(w=>({weapon:w.id,label:w.label,...savedRoot.weapons[w.id],
      candidates:hydrate(w).sounds.map(s=>({number:s.number,current:!!s.current,authored:!!s.authored,recipeId:s.recipeId,file:s.file,sha256:s.sha256,archiveEntry:s.archiveEntry,
      layers:s.layers?Object.fromEntries(Object.entries(s.layers).map(([hit,stems])=>[hit,Object.fromEntries(Object.entries(stems).map(([name,v])=>[name,{file:v.file,sha256:v.sha256,nominalDuration:v.nominalDuration,accentSeconds:v.accentSeconds}]))])):undefined,
      variants:s.variants?Object.fromEntries(Object.entries(s.variants).map(([key,v])=>[key,{file:v.file,sha256:v.sha256,nominalDuration:v.nominalDuration}])):undefined}))}))};
}
$('export').onclick=()=>{
  const data=exportSelection();
  const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));
  const link=document.createElement('a');link.href=url;link.download='all-weapons-audition-selection.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  status('모든 무기의 선택과 시점을 JSON 파일로 저장했습니다.');
};
document.addEventListener('keydown',event=>{
  if(event.target.closest('input,select,textarea,button')||event.altKey||event.ctrlKey||event.metaKey)return;
  if(event.code==='Space'){event.preventDefault();running?stop():void play();}
  if(event.code==='ArrowLeft'||event.code==='ArrowRight'){event.preventDefault();select(selected+(event.code==='ArrowRight'?1:-1));}
});
document.addEventListener('visibilitychange',()=>{if(document.hidden)stop('다른 화면으로 이동하여 재생을 멈췄습니다.');});
window.addEventListener('pagehide',()=>stop());
for(const weapon of ROOT.weapons){const option=document.createElement('option');option.value=weapon.id;option.textContent=weapon.label;$('weapon').append(option);}
activateWeapon(ROOT.weapons.some(w=>w.id===savedRoot.active)?savedRoot.active:ROOT.defaultWeapon);
Promise.all(Object.entries(ROOT.frames).map(([ref,frame])=>new Promise((resolve,reject)=>{
  const image=new Image();image.onload=()=>{images.set(ref,image);resolve();};image.onerror=()=>reject(new Error(ref));image.src=frame.src;
}))).then(()=>{
  ready=true;for(const id of ['play','loop','sequence','currentSound'])$(id).disabled=false;
  document.querySelectorAll('.pick').forEach(button=>button.disabled=false);
  render(0);status('준비됐습니다. 무기와 후보를 고르면 모션과 소리가 함께 재생됩니다.');requestAnimationFrame(tick);
}).catch(error=>status('공격 프레임을 불러오지 못했습니다: '+error.message,true));
