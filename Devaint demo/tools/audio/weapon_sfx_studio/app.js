'use strict';
const $=id=>document.getElementById(id),DATA=JSON.parse($('payload').textContent),DSP=StudioDSP;
const RATE=DATA.source.rate,bytes=Uint8Array.from(atob(DATA.source.pcm),c=>c.charCodeAt(0)),pcmView=new DataView(bytes.buffer);
const SOURCE=Float32Array.from({length:bytes.length/2},(_,i)=>pcmView.getInt16(i*2,true)/32768),SOURCE_MS=SOURCE.length/RATE*1000;
const ACTIONS=new Map(DATA.actions.map(a=>[a.id,a])),images=new Map(),STORE='deviant-weapon-audio-studio-v1';
const KINDS={combo:'전체 연계',basic:'한 타',special:'특수공격'},MODES={one:'단발',repeat:'반복',spin:'회전'};
const FIELDS=['mode','at','trimA','trimB','pitch','gain','speed','pan','interval','count','duration','spinHz','depth','sweep'];
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
let project={version:1,sourceHash:DATA.source.sha256,actionId:'base_sword/combo',actions:{},crop:[0,SOURCE_MS]},selected=null;
let ac,gainNode,playing=false,looping=false,playingSources=new Set(),generation=0,timer=null,nextStart=0,startAt=0,playPeriod=0,age=0,cache=null,saveTimer;
const current=()=>ACTIONS.get(project.actionId),events=()=>project.actions[project.actionId]||(project.actions[project.actionId]=[]);
const uid=()=>globalThis.crypto?.randomUUID?.()||'e'+Math.random().toString(36).slice(2);
function message(text){$('message').textContent=text;}
function makeEvent(at){return {id:uid(),mode:'one',at:Math.round(at),trimA:project.crop[0],trimB:project.crop[1],pitch:0,gain:0,speed:1,pan:0,interval:100,count:4,duration:1000,spinHz:4,depth:.5,sweep:0};}
function validateProject(input){
  if(!input||input.version!==1||input.sourceHash!==DATA.source.sha256||!input.actions||typeof input.actions!=='object')throw Error('이 확정 소스용 작업 JSON이 아닙니다.');
  const p={version:1,sourceHash:DATA.source.sha256,actionId:ACTIONS.has(input.actionId)?input.actionId:'base_sword/combo',actions:{},crop:[0,SOURCE_MS]};
  if(Array.isArray(input.crop)&&input.crop.length===2&&input.crop.every(Number.isFinite)&&input.crop[0]>=0&&input.crop[1]<=SOURCE_MS&&input.crop[1]-input.crop[0]>=10)p.crop=input.crop.slice();
  let total=0;
  for(const action of DATA.actions){
    if(!Object.hasOwn(input.actions,action.id))continue;
    const items=input.actions[action.id];if(!Array.isArray(items)||items.length>64)throw Error('공격별 이벤트 수를 확인하세요.');
    p.actions[action.id]=items.map(e=>{DSP.validateEvent(e,SOURCE_MS);const clean={id:uid()};for(const k of FIELDS)clean[k]=e[k];return clean;});
    total+=items.length;if(total>3000)throw Error('프로젝트 이벤트 수가 너무 많습니다.');
  }
  return p;
}
function save(){clearTimeout(saveTimer);saveTimer=setTimeout(()=>{try{localStorage.setItem(STORE,JSON.stringify(project));$('saved').textContent='현재 브라우저에 저장됨 · JSON으로 백업 가능';}catch(e){$('saved').textContent='자동 저장 불가 · JSON/작업 HTML로 저장하세요.';}},200);}
function edited(){stop(false);cache=null;$('renderInfo').textContent='';save();draw();renderEvents();}
function selectedEvent(){return events().find(e=>e.id===selected);}
function selectEvent(id){selected=id;syncFields();renderEvents();draw();}
function syncFields(){const e=selectedEvent();$('eventName').textContent=e?'#'+(events().indexOf(e)+1):'없음';for(const k of FIELDS){$('e_'+k).disabled=!e;if(e)$('e_'+k).value=e[k];}$('duplicate').disabled=!e;$('delete').disabled=!e;}
function renderActions(){
  const weapon=$('weapon').value,kind=$('category').value;$('actions').replaceChildren();
  const list=DATA.actions.filter(a=>a.weapon===weapon&&(kind==='all'||a.kind===kind));
  for(const a of list){const b=document.createElement('button');b.className='action'+(a.id===project.actionId?' active':'');const label=document.createElement('span');label.textContent=a.name;const sub=document.createElement('span');sub.className='kind';sub.textContent=KINDS[a.kind]+' · '+Math.round(a.duration*1000)+'ms'+(project.actions[a.id]?.length?' · 작업 있음':'');b.append(label,sub);b.onclick=()=>switchAction(a.id);$('actions').append(b);}
  $('actionCount').textContent=list.length+'개 모션 · 총 '+DATA.actions.length+'개';
}
function switchAction(id){const fresh=!Object.hasOwn(project.actions,id);stop();project.actionId=id;age=0;if(fresh)project.actions[id]=current().markers.map(t=>makeEvent(t*1000));selected=events()[0]?.id||null;$('actionTitle').textContent=current().weaponName+' / '+current().name;$('durationText').textContent=Math.round(current().duration*1000)+' ms';$('note').textContent=current().note;cache=null;$('renderInfo').textContent='';renderActions();renderEvents();syncFields();draw();save();}
function renderEvents(){
  $('events').replaceChildren();
  for(const [i,e]of events().entries()){const row=document.createElement('div');row.className='eventrow'+(e.id===selected?' active':'');row.tabIndex=0;const text=document.createElement('span');text.className='grow';text.textContent=`${i+1}. ${MODES[e.mode]} · ${Math.round(e.at)}ms · ${e.pitch>0?'+':''}${e.pitch}반음 · ${e.gain}dB · ×${e.speed}`;const b=document.createElement('button');b.textContent='듣기';b.onclick=ev=>{ev.stopPropagation();auditionEvent(e);};row.append(text,b);row.onclick=()=>selectEvent(e.id);row.onkeydown=ev=>{if(ev.key==='Enter')selectEvent(e.id);};$('events').append(row);}
}
function frameAt(seconds,time){let t=Math.max(0,time);for(let i=0;i<seconds.length;i++){if(t<seconds[i])return i;t-=seconds[i];}return seconds.length-1;}
function sourceTime(segment,time){const c=segment.clip,d=c.seconds.reduce((a,b)=>a+b,0),hit=Math.max(0,c.impact);return time<segment.impact&&segment.impact>0?time/segment.impact*hit:hit+(time-segment.impact)/Math.max(.001,segment.duration-segment.impact)*(d-hit);}
const ctx=$('stage').getContext('2d'),tc=$('timeline').getContext('2d'),sc=$('sourceWave').getContext('2d');
function body(){
  const a=current();if(!a)return;const segment=a.segments.find(s=>age>=s.start&&age<s.start+s.duration)||a.segments[a.segments.length-1],t=clamp(age-segment.start,0,segment.duration-.000001),st=sourceTime(segment,t),c=segment.clip,i=frameAt(c.seconds,st),ref=c.frames[i],entry=DATA.frames[ref],im=images.get(ref);
  ctx.fillStyle='#1c2528';ctx.fillRect(0,0,950,380);ctx.strokeStyle='#586e61';ctx.beginPath();ctx.moveTo(40,344);ctx.lineTo(910,344);ctx.stroke();ctx.save();ctx.translate(440,342);const scale=2.2;if($('facing').checked)ctx.scale(-1,1);ctx.imageSmoothingEnabled=false;
  if(im)ctx.drawImage(im,-entry.pivot[0]*scale,-entry.pivot[1]*scale,im.width*scale,im.height*scale);
  const fx=c.effect,elapsed=st-c.fxStart;
  if($('showFx').checked&&fx&&c.fxStart>=0&&elapsed>=0&&elapsed<fx.seconds.reduce((a,b)=>a+b,0)){
    const n=frameAt(fx.seconds,elapsed),ref=fx.frames[n],image=images.get(ref),s=(fx.scale||1)*scale;
    if(image){ctx.save();ctx.translate(fx.x[n]*32*scale,-fx.y[n]*32*scale);if(fx.flipX)ctx.scale(-1,1);ctx.drawImage(image,0,0,image.width*s,image.height*s);ctx.restore();}
  }
  ctx.restore();$('frameText').textContent=(age>a.duration?'모션 끝 · 소리 잔향 / ':'')+ref.split('/').at(-1)+' · '+Math.round(st*1000)+'ms 원본';$('clock').textContent=Math.round(age*1000)+' ms';
  const flight=DATA.flight[a.weapon],post=age-segment.start-segment.impact;
  if(flight&&post>=0){const image=images.get('flight:'+a.weapon+':'+Math.floor(post*50)%flight.length);if(image){const s=1.4;ctx.save();ctx.translate(800,235);if(a.weapon==='bomb')ctx.rotate(post*Math.PI);ctx.drawImage(image,-image.width*s/2,-image.height*s/2,image.width*s,image.height*s);ctx.restore();ctx.fillStyle='#9fb8aa';ctx.font='12px sans-serif';ctx.fillText('투사체 원본 · 위치 고정',730,310);}}
  const spin=events().find(e=>e.mode==='spin');if(spin){const phase=(age-spin.at/1000)*spin.spinHz*2*Math.PI;ctx.strokeStyle='#6caa8f';ctx.beginPath();ctx.ellipse(840,65,45,20,0,0,Math.PI*2);ctx.stroke();ctx.fillStyle='#cce3c1';ctx.beginPath();ctx.arc(840+45*Math.sin(phase),65+20*Math.cos(phase),5,0,Math.PI*2);ctx.fill();ctx.font='12px sans-serif';ctx.fillText('좌우 회전감',804,110);}
}
function eventEnd(e){return e.at+(e.mode==='one'?0:e.mode==='repeat'?(e.count-1)*e.interval:e.duration)+(e.trimB-e.trimA)/e.speed;}
function timelineLength(){return Math.min(30,Math.max(current().duration,...events().map(e=>eventEnd(e)/1000),.1));}
function timeline(){
  const max=timelineLength(),w=1100,h=155;tc.fillStyle='#eaf0e6';tc.fillRect(0,0,w,h);tc.font='11px sans-serif';
  for(let t=0;t<=max;t+=max>4?.5:.1){const x=t/max*w;tc.strokeStyle='#d3ddcd';tc.beginPath();tc.moveTo(x,0);tc.lineTo(x,h);tc.stroke();tc.fillStyle='#607465';tc.fillText(Math.round(t*1000),x+3,13);}
  for(const mark of current().markers){const x=mark/max*w;tc.strokeStyle='#d48b39';tc.lineWidth=2;tc.beginPath();tc.moveTo(x,18);tc.lineTo(x,h);tc.stroke();}
  tc.lineWidth=1;
  events().forEach((e,i)=>{const x=e.at/1000/max*w,end=eventEnd(e)/1000/max*w,y=28+(i%3)*36;tc.fillStyle=e.id===selected?'#27664d':'#90b399';tc.fillRect(x,y,Math.max(12,end-x),27);tc.fillStyle=e.id===selected?'white':'#204633';tc.fillText((i+1)+' '+MODES[e.mode],x+4,y+18);});
  const x=age/max*w;tc.strokeStyle='#26352d';tc.lineWidth=2;tc.beginPath();tc.moveTo(x,0);tc.lineTo(x,h);tc.stroke();$('timelineEnd').textContent=Math.round(max*1000)+' ms';$('seek').max=Math.ceil(max*1000);$('seek').value=Math.round(age*1000);
}
function sourceWave(){const w=1100,h=100;sc.fillStyle='#edf0e7';sc.fillRect(0,0,w,h);let peak=0;for(const x of SOURCE)peak=Math.max(peak,Math.abs(x));sc.fillStyle='#829e8a';for(let i=0;i<550;i++){let p=0;for(let j=Math.floor(i*SOURCE.length/550);j<Math.floor((i+1)*SOURCE.length/550);j++)p=Math.max(p,Math.abs(SOURCE[j]));const n=p/(peak||1)*44;sc.fillRect(i*2,50-n,1.5,n*2);}const [a,b]=project.crop;sc.fillStyle='#41816335';sc.fillRect(a/SOURCE_MS*w,0,(b-a)/SOURCE_MS*w,h);sc.strokeStyle='#27664d';for(const t of [a,b]){sc.beginPath();sc.moveTo(t/SOURCE_MS*w,0);sc.lineTo(t/SOURCE_MS*w,h);sc.stroke();}$('cropA').value=Math.round(a*10)/10;$('cropB').value=Math.round(b*10)/10;}
function draw(){body();timeline();sourceWave();}
function stop(reset=false){generation++;playing=false;looping=false;clearInterval(timer);timer=null;for(const s of playingSources){try{s.stop();}catch(e){}}playingSources.clear();$('loop').classList.remove('activeLoop');if(reset)age=0;draw();}
function audio(){if(!ac){ac=new(window.AudioContext||window.webkitAudioContext)({sampleRate:RATE});gainNode=ac.createGain();gainNode.connect(ac.destination);}gainNode.gain.value=+$('master').value/100;return ac.resume();}
function mix(){if(!cache){cache=DSP.render(SOURCE,RATE,events(),current().duration);$('renderInfo').textContent=Math.round(cache.duration*1000)+'ms · '+cache.pulses+'회'+(cache.attenuationDb<-.01?' · 과출력 방지 '+cache.attenuationDb.toFixed(1)+'dB':'');}return cache;}
function schedule(m,when){const b=ac.createBuffer(2,m.left.length,RATE);b.copyToChannel(m.left,0);b.copyToChannel(m.right,1);const s=ac.createBufferSource();s.buffer=b;s.connect(gainNode);s.onended=()=>{playingSources.delete(s);s.disconnect();};playingSources.add(s);s.start(when);return s;}
async function play(loop=false){stop();const ticket=generation;try{await audio();message('소리를 준비하고 있습니다…');await new Promise(requestAnimationFrame);const m=mix();if(ticket!==generation)return;startAt=ac.currentTime+.1;playPeriod=m.duration+.35;nextStart=startAt+playPeriod;schedule(m,startAt);playing=true;looping=loop;$('loop').classList.toggle('activeLoop',loop);message('재생 중 · '+current().weaponName+' '+current().name);timer=setInterval(()=>{if(looping&&nextStart<ac.currentTime+.3){schedule(m,nextStart);nextStart+=playPeriod;}},40);}catch(e){stop();message(e.message);}}
async function auditionEvent(event){stop();const ticket=generation;try{await audio();const copy={...event,at:200};const m=DSP.render(SOURCE,RATE,[copy],.1);if(ticket!==generation)return;schedule(m,ac.currentTime+.03);message('선택한 소리만 재생');}catch(e){message(e.message);}}
function tick(){if(playing){const now=ac.currentTime-startAt;if(now>=0){age=looping?Math.min(cache.duration,now%playPeriod):Math.min(cache.duration,now);draw();if(!looping&&now>cache.duration+.03){stop();message('재생 완료');}}}requestAnimationFrame(tick);}
function addAt(at){if(events().length>=64)return message('최대 64개까지 배치할 수 있습니다.');const e=makeEvent(at);events().push(e);selected=e.id;edited();syncFields();}
$('weapon').onchange=()=>{renderActions();const first=DATA.actions.find(a=>a.weapon===$('weapon').value&&a.kind==='combo')||DATA.actions.find(a=>a.weapon===$('weapon').value);$('category').value='all';switchAction(first.id);};$('category').onchange=renderActions;
$('play').onclick=()=>play();$('loop').onclick=()=>play(true);$('stop').onclick=()=>{stop(true);message('정지');};$('seek').oninput=()=>{const ms=+$('seek').value;stop();age=ms/1000;draw();};$('master').oninput=()=>{if(gainNode)gainNode.gain.value=+$('master').value/100;};$('showFx').onchange=body;$('facing').onchange=body;
$('add').onclick=()=>addAt(age*1000);$('autoHits').onclick=()=>{project.actions[project.actionId]=current().markers.map(t=>makeEvent(t*1000));selected=events()[0]?.id;edited();syncFields();};
$('spinPreset').onclick=()=>{if(events().length>=64)return message('최대 64개까지 배치할 수 있습니다.');const e=makeEvent((current().markers[0]||0)*1000);e.mode='spin';e.interval=65;e.duration=clamp(Math.round((current().duration-e.at/1000)*1000),200,3000);e.spinHz=5;e.depth=.45;e.gain=-6;e.speed=1.2;events().push(e);selected=e.id;edited();syncFields();message('회전음 프리셋을 추가했습니다. 기존 소리가 필요 없으면 이벤트 목록에서 삭제하세요.');};
$('clear').onclick=()=>{project.actions[project.actionId]=[];selected=null;edited();syncFields();};$('duplicate').onclick=()=>{const e=selectedEvent();if(e&&events().length<64){const copy={...e,id:uid(),at:Math.min(30000,e.at+80)};events().push(copy);selected=copy.id;edited();syncFields();}};$('delete').onclick=()=>{project.actions[project.actionId]=events().filter(e=>e.id!==selected);selected=null;edited();syncFields();};
for(const key of FIELDS){const update=commit=>{const e=selectedEvent();if(!e)return;const old=e[key];e[key]=key==='mode'?$('e_'+key).value:Number($('e_'+key).value);try{DSP.validateEvent(e,SOURCE_MS);edited();}catch(err){e[key]=old;if(commit)message(err.message);}if(commit)syncFields();};$('e_'+key).oninput=()=>update(false);$('e_'+key).onchange=()=>update(true);}
function setCrop(a,b){a=clamp(a,0,SOURCE_MS-10);b=clamp(b,a+10,SOURCE_MS);project.crop=[a,b];sourceWave();save();}
$('cropA').onchange=()=>setCrop(+$('cropA').value,project.crop[1]);$('cropB').onchange=()=>setCrop(project.crop[0],+$('cropB').value);$('fullSource').onclick=()=>setCrop(0,SOURCE_MS);$('audition').onclick=()=>auditionEvent(makeEvent(200));
function point(canvas,event){const r=canvas.getBoundingClientRect();return [(event.clientX-r.left)/r.width*canvas.width,(event.clientY-r.top)/r.height*canvas.height];}
let cropDrag=null;$('sourceWave').onpointerdown=e=>{cropDrag=point($('sourceWave'),e)[0]/1100*SOURCE_MS;$('sourceWave').setPointerCapture(e.pointerId);};$('sourceWave').onpointermove=e=>{if(cropDrag===null)return;const end=clamp(point($('sourceWave'),e)[0]/1100*SOURCE_MS,0,SOURCE_MS);if(Math.abs(end-cropDrag)>=10)setCrop(Math.min(cropDrag,end),Math.max(cropDrag,end));};$('sourceWave').onpointerup=()=>{cropDrag=null;};$('sourceWave').onpointercancel=()=>{cropDrag=null;};
let drag=null;$('timeline').onpointerdown=e=>{const [x,y]=point($('timeline'),e),max=timelineLength();const item=[...events()].reverse().find(event=>{const i=events().indexOf(event),left=event.at/1000/max*1100,right=Math.max(left+12,eventEnd(event)/1000/max*1100),top=28+i%3*36;return x>=left&&x<=right&&y>=top&&y<=top+27;});stop();if(item){selectEvent(item.id);drag={x,at:item.at,max};$('timeline').setPointerCapture(e.pointerId);}else{age=clamp(x/1100*max,0,max);draw();}};$('timeline').onpointermove=e=>{if(!drag)return;const event=selectedEvent();if(!event)return;event.at=clamp(Math.round(drag.at+(point($('timeline'),e)[0]-drag.x)/1100*drag.max*1000),0,29000);cache=null;syncFields();timeline();};$('timeline').onpointerup=()=>{if(drag){drag=null;edited();}};$('timeline').onpointercancel=()=>{drag=null;};
function download(name,content,type){const blob=content instanceof Blob?content:new Blob([content],{type}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);}
$('exportWav').onclick=()=>{try{stop();message('WAV를 만들고 있습니다…');const m=mix();download('sfx_'+current().id.replaceAll('/','_')+'_edit_01_v01.wav',DSP.wav(m),'audio/wav');message('WAV 저장 · '+Math.round(m.duration*1000)+'ms / 48kHz 스테레오');}catch(e){message(e.message);}};
$('saveProject').onclick=()=>download('deviant-weapon-sfx-project.json',JSON.stringify(project,null,2),'application/json');
$('importProject').onchange=async e=>{try{const file=e.target.files[0];if(!file)return;if(file.size>4000000)throw Error('설정 파일이 너무 큽니다.');const next=validateProject(JSON.parse(await file.text()));stop();project=next;$('weapon').value=current().weapon;switchAction(project.actionId);message('설정을 불러왔습니다.');}catch(err){message(err.message);}e.target.value='';};
$('saveHtml').onclick=()=>{const copy=document.documentElement.cloneNode(true);copy.querySelector('#initial-project').textContent=JSON.stringify(project).replaceAll('<','\\u003c');download('deviant-weapon-sfx-work.html','<!doctype html>'+copy.outerHTML,'text/html');message('현재 설정을 담은 HTML 사본을 저장했습니다.');};
document.addEventListener('keydown',e=>{if(e.code==='Space'&&!['INPUT','SELECT','BUTTON'].includes(document.activeElement.tagName)){e.preventDefault();playing?stop():play();}});document.addEventListener('visibilitychange',()=>{if(document.hidden)stop();});
async function init(){
  try{
    const embedded=JSON.parse($('initial-project').textContent);if(embedded)project=validateProject(embedded);else{try{const saved=localStorage.getItem(STORE);if(saved)project=validateProject(JSON.parse(saved));}catch(e){message('저장된 작업을 읽지 못해 새 작업으로 시작합니다.');}}
    $('weapon').replaceChildren();
    for(const [id,name]of new Map(DATA.actions.map(a=>[a.weapon,a.weaponName]))){const opt=document.createElement('option');opt.value=id;opt.textContent=name;$('weapon').append(opt);}
    $('weapon').value=current().weapon;
    const resources=[...Object.entries(DATA.frames),...Object.entries(DATA.flight).flatMap(([form,list])=>list.map((src,i)=>['flight:'+form+':'+i,{src}]))];
    await Promise.all(resources.map(async([ref,entry])=>{const image=new Image();image.src=entry.src;await image.decode();images.set(ref,image);}));
    $('cropA').max=SOURCE_MS-10;$('cropB').max=SOURCE_MS;switchAction(project.actionId);$('play').disabled=false;$('loop').disabled=false;message('준비 완료 · 승인 프레임 '+images.size+'개 / 공격 '+DATA.actions.length+'개');requestAnimationFrame(tick);
  }catch(e){message('불러오기 오류: '+e.message);}
}
init();
