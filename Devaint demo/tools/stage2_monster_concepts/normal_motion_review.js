'use strict';
const D=window.MOTION_DATA, $=id=>document.getElementById(id), images=new Map(), failures=new Set(), cards=[];
let playing=true,elapsed=0,previousTime=null,rate=1,zoom=2,background='white',ready=false;
const model={
  frameAt(ds,t){let left=((t%ds.reduce((a,b)=>a+b,0))+ds.reduce((a,b)=>a+b,0))%ds.reduce((a,b)=>a+b,0);for(let i=0;i<ds.length;i++){if(left<ds[i])return i;left-=ds[i]}return ds.length-1},
  entries(monster,mode){let start=0,result=[];for(const c of monster.clips){if(mode!=='sequence'&&mode!==c.state)continue;for(let k=0;k<c.paths.length;k++){result.push({clip:c,key:k,startMs:start,endMs:start+c.durationsMs[k]});start+=c.durationsMs[k]}}return result},
  choose(entries,t){const total=entries[entries.length-1].endMs,cursor=((t%total)+total)%total;return entries.findIndex(e=>e.startMs<=cursor&&cursor<e.endMs)}
};
window.motionReviewModel=model;
for(const m of D.monsters){
  const row=document.createElement('section');row.className='row';
  const heading=document.createElement('div');heading.className='rowhead';heading.innerHTML=`<h2>${m.name}</h2><span>${m.role}</span>`;row.append(heading);
  const grid=document.createElement('div');grid.className='grid';row.append(grid);$('overview').append(row);
  const option=document.createElement('option');option.value=m.id;option.textContent=m.name;$('monster').append(option);
  for(const c of m.clips){
    const card=document.createElement('article');card.className='card';
    const header=document.createElement('div');header.className='cardhead';header.innerHTML=`<h3>${c.label}</h3>`;
    const button=document.createElement('button');button.textContent='크게 보기';button.setAttribute('aria-label',m.name+' '+c.label+' 크게 보기');button.onclick=()=>{$('monster').value=m.id;$('mode').value=c.state;elapsed=0;draw();$('focus').scrollIntoView({behavior:'smooth',block:'start'})};header.append(button);card.append(header);
    const viewport=document.createElement('div');viewport.className='viewport';const canvas=document.createElement('canvas');canvas.setAttribute('aria-label',m.name+' '+c.label);viewport.append(canvas);card.append(viewport);
    const tag=document.createElement('p');tag.className='tag';tag.textContent=c.status;card.append(tag);
    const note=document.createElement('p');note.textContent=c.note;card.append(note);
    const phase=document.createElement('p');phase.className='phase';card.append(phase);grid.append(card);cards.push({monster:m,clip:c,canvas,viewport,phase});
  }
}
function canvasSetup(canvas,w,h,scale){if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;canvas.style.width=w*scale+'px';canvas.style.height=h*scale+'px';const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,w,h);return ctx}
function imageStatus(path){return failures.has(path)?'이미지 읽기 실패: '+path:'이미지 불러오는 중…'}
function paintCard(card){const {monster:m,clip:c,canvas,viewport,phase}=card,k=model.frameAt(c.durationsMs,elapsed),im=images.get(c.paths[k]);const ctx=canvasSetup(canvas,m.crop[2]-m.crop[0],m.crop[3]-m.crop[1],zoom);viewport.style.background=background;ctx.fillStyle=background;ctx.fillRect(0,0,canvas.width,canvas.height);if(!im){phase.textContent=imageStatus(c.paths[k]);return}ctx.drawImage(im,0,0);const suffix=c.gapMs?` · 확인대기 ${c.gapMs/1000}초`:'';const position=elapsed%c.cycleMs>=c.motionMs?'확인 대기':`키 ${k+1}/${c.motionFrameEntries}`;phase.textContent=`${position} · 동작 ${c.motionMs/1000}초${suffix}`}
function focusData(){const m=D.monsters.find(m=>m.id===$('monster').value)||D.monsters[0],entries=model.entries(m,$('mode').value);return {m,entries,index:model.choose(entries,elapsed)}}
function drawFocus(){const {m,entries,index}=focusData(),e=entries[index],im=images.get(e.clip.paths[e.key]),bg=$('focus-bg').value,c=$('focus-canvas');let ctx;
  if(bg==='map'){ctx=canvasSetup(c,960,540,1);ctx.fillStyle='white';ctx.fillRect(0,0,c.width,c.height);const map=images.get(D.mapPath);if(map)ctx.drawImage(map,0,0);if(im)ctx.drawImage(im,m.crop[0]+D.canonicalToWorld[0],m.crop[1]+D.canonicalToWorld[1]);$('focus-viewport').style.background='#25252b'}
  else{ctx=canvasSetup(c,m.crop[2]-m.crop[0],m.crop[3]-m.crop[1],zoom);ctx.fillStyle=bg;ctx.fillRect(0,0,c.width,c.height);if(im)ctx.drawImage(im,0,0);$('focus-viewport').style.background=bg}
  $('scrub').max=entries.length-1;$('scrub').value=index;
  const localStart=e.clip.durationsMs.slice(0,e.key).reduce((a,b)=>a+b,0),cycle=entries[entries.length-1].endMs,localNow=((elapsed%cycle)+cycle)%cycle-e.startMs+localStart,position=localNow>=e.clip.motionMs?'확인 대기':`키 ${e.key+1}/${e.clip.motionFrameEntries}`;
  $('focus-label').textContent=`${m.name} · ${e.clip.label} · ${position} · ${e.clip.durationsMs[e.key]}ms · ${((elapsed%entries[entries.length-1].endMs)/1000).toFixed(2)} / ${(entries[entries.length-1].endMs/1000).toFixed(2)}초`;
  if(!im)$('focus-label').textContent+=' · '+imageStatus(e.clip.paths[e.key]);
  if(bg==='map'&&!images.has(D.mapPath))$('focus-label').textContent+=failures.has(D.mapPath)?' · 맵 이미지 읽기 실패 · 흰 배경으로 표시':' · 맵 불러오는 중…';
  $('focus-note').textContent=e.clip.note+' — '+e.clip.status;
}
function draw(){if(!ready)return;for(const card of cards)paintCard(card);drawFocus()}
function setPlaying(value){playing=value;$('play').textContent=playing?'전체 정지':'전체 재생';$('play').setAttribute('aria-pressed',String(playing))}
function step(direction){if(!ready)return;const f=focusData(),i=(f.index+direction+f.entries.length)%f.entries.length;setPlaying(false);elapsed=f.entries[i].startMs;draw()}
$('play').onclick=()=>setPlaying(!playing);$('restart').onclick=()=>{elapsed=0;previousTime=null;draw()};$('rate').onchange=e=>rate=+e.target.value;
$('zoom').onchange=e=>{zoom=+e.target.value;draw()};$('background').onchange=e=>{background=e.target.value;draw()};$('focus-bg').onchange=draw;
$('monster').onchange=$('mode').onchange=()=>{elapsed=0;draw()};$('previous').onclick=()=>step(-1);$('next').onclick=()=>step(1);
$('scrub').oninput=e=>{if(!ready)return;const f=focusData();setPlaying(false);elapsed=f.entries[+e.target.value].startMs;draw()};
function tick(time){if(previousTime!==null&&playing&&ready)elapsed+=Math.min(time-previousTime,250)*rate;previousTime=time;draw();requestAnimationFrame(tick)}
const paths=[...new Set([D.mapPath,D.playerPath,...D.monsters.flatMap(m=>m.clips.flatMap(c=>c.paths))])];
const b=D.playerVisibleBounds;$('player').style.width=(b[2]-b[0])*2+'px';$('player').style.height=(b[3]-b[1])*2+'px';$('player-label').textContent=`승인 플레이어 실제 그림 ${b[2]-b[0]}×${b[3]-b[1]}px · 위 그림은 정수2배 · 맵은 실제1배`;
function loadStatus(){const done=images.size+failures.size;$('load').textContent=done===paths.length&&!failures.size?'5종 · 15동작 준비 완료':`이미지 ${images.size}/${paths.length} 준비 · ${failures.size}개 실패${done<paths.length?' · 불러오는 중…':''}`;if(failures.has(D.playerPath))$('player-label').textContent='승인 플레이어 이미지 읽기 실패';draw()}
// Start rendering immediately. A missing map or one pending frame must not
// suppress every other clip; each unavailable frame has a visible status.
ready=true;draw();requestAnimationFrame(tick);
for(const path of paths){
  const im=new Image();let settled=false;
  const finish=ok=>{if(settled)return;settled=true;clearTimeout(timer);if(ok){images.set(path,im);if(path===D.playerPath)$('player').src=im.src}else failures.add(path);loadStatus()};
  const timer=setTimeout(()=>finish(false),15000);
  im.onload=()=>finish(true);im.onerror=()=>finish(false);im.src=D.embeddedImages?.[path]||path;
}
