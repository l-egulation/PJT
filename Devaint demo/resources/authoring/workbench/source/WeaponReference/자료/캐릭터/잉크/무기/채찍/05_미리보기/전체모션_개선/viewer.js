'use strict';
const $=id=>document.getElementById(id);
const state={id:'대기',t:0,running:true,last:null,global:0,all:false,step:0};
const tour=['대기','걷기','달리기','점프','낙하착지','연속','대시','죽음_2'];
const movies=new Map(WHIP_ALL.movies.map(m=>[m.id,m]));
const loaded=[];
function load(src){const im=new Image();im.src=src;loaded.push(im);return im;}
const images=new Map(WHIP_ALL.movies.map(m=>[m.id,m.frames.map(f=>load(f.image))]));
const fx=WHIP_ALL.effects.map(m=>m.frames.map(load));
const enemy=load(WHIP_ALL.enemy), main=$('view').getContext('2d');
const off=document.createElement('canvas');off.width=392;off.height=176;
const ctx=off.getContext('2d');ctx.imageSmoothingEnabled=false;main.imageSmoothingEnabled=false;
const tabs=new Map(),cards=new Map();let frameButtons=[];
function frameIndex(m,t){let sum=0;for(let i=0;i<m.frames.length;i++){sum+=m.frames[i].ms;if(t<sum)return i;}return m.frames.length-1;}
function frameTime(m,i){return m.frames.slice(0,i).reduce((sum,f)=>sum+f.ms,0);}
function drawScene(g,m,t,width,height){
  const i=frameIndex(m,t);g.save();g.setTransform(1,0,0,1,0,0);g.fillStyle='#495562';g.fillRect(0,0,width,height);
  if($('flip').checked){g.translate(width,0);g.scale(-1,1);}
  if($('guide').checked){g.fillStyle='#7f8b98';g.fillRect(0,147,width,1);}
  if(m.attack && $('enemy').checked)g.drawImage(enemy,206,1);
  g.drawImage(images.get(m.id)[i],0,0);
  if(m.attack && $('fx').checked){for(const layer of m.effects){const def=WHIP_ALL.effects[layer.track];const n=Math.floor((t-layer.offset-def.start_ms)/def.step_ms);if(n>=0&&n<def.frame_count)g.drawImage(fx[layer.track][n],0,0);}}
  g.restore();return i;
}
function refreshFrames(){
  const m=movies.get(state.id);$('frames').replaceChildren();frameButtons=[];
  m.frames.forEach((f,i)=>{
    const b=document.createElement('button');b.className='frame';b.setAttribute('aria-label',`${i+1}번 프레임 선택`);
    const c=document.createElement('canvas');c.width=158;c.height=100;const g=c.getContext('2d');g.imageSmoothingEnabled=false;g.fillStyle='#495562';g.fillRect(0,0,158,100);
    // A shared character crop keeps pixel scale consistent in every thumbnail.
    g.drawImage(images.get(m.id)[i],48,67,158,100,0,0,158,100);
    const label=document.createElement('span');label.textContent=String(i+1).padStart(2,'0');const detail=document.createElement('small');detail.textContent=`${f.phase} · ${f.ms}ms`;
    b.append(c,label,detail);b.onclick=()=>seekFrame(i);$('frames').append(b);frameButtons.push(b);
  });
}
function select(id,keepTour=false){
  if(!movies.has(id))return;if(!keepTour){state.all=false;state.step=0;}
  state.id=id;state.t=0;state.running=true;$('all').setAttribute('aria-pressed',String(state.all));
  const m=movies.get(id);$('scrub').max=m.frames.length-1;$('title').textContent=m.title;$('note').textContent=m.note;
  for(const [key,b] of tabs)b.setAttribute('aria-pressed',String(key===id));
  refreshFrames();render();
}
function seekFrame(i){const m=movies.get(state.id);i=Math.max(0,Math.min(m.frames.length-1,i));state.t=frameTime(m,i);state.running=false;render();}
function render(){
  const m=movies.get(state.id),i=drawScene(ctx,m,Math.min(state.t,m.total-1),392,176);
  main.drawImage(off,0,0,1176,528);
  $('scrub').value=i;$('status').textContent=`${i+1} / ${m.frames.length} 프레임 · ${m.frames[i].phase}`;
  $('time').textContent=`${Math.floor(Math.min(state.t,m.total))}ms / ${m.total}ms`;
  $('play').textContent=state.running?'일시정지':'재생';frameButtons.forEach((b,j)=>b.classList.toggle('current',i===j));
  for(const [id,g] of cards){const movie=movies.get(id),t=state.global%(movie.total+400);drawScene(g,movie,Math.min(t,movie.total-1),392,176);}
}
function tick(now){
  const dt=state.last===null?0:Math.min(80,now-state.last)*Number($('speed').value);state.last=now;
  if(state.running){state.global+=dt;state.t+=dt;const m=movies.get(state.id);const hold=state.all?(state.id==='죽음_2'?700:150):m.loop?0:400;
    if(state.t>=m.total+hold){
      if(state.all){
        if(state.step<tour.length-1){state.step++;select(tour[state.step],true);}
        else if($('repeat').checked){state.step=0;select(tour[0],true);}
        else{state.running=false;state.t=m.total-1;}
      }else if($('repeat').checked){state.t=0;}
      else{state.t=m.total-1;state.running=false;}
    }
  }
  render();requestAnimationFrame(tick);
}
for(const m of WHIP_ALL.movies){
  const button=document.createElement('button');button.textContent=m.title;button.setAttribute('aria-pressed','false');button.onclick=()=>select(m.id);$('tabs').append(button);tabs.set(m.id,button);
  const card=document.createElement('button');card.className='card';const title=document.createElement('span');title.className='title';title.textContent=m.title;
  const canvas=document.createElement('canvas');canvas.width=392;canvas.height=176;const g=canvas.getContext('2d');g.imageSmoothingEnabled=false;
  card.append(title,canvas);card.onclick=()=>select(m.id);$('grid').append(card);cards.set(m.id,g);
}
$('play').onclick=()=>{const m=movies.get(state.id);if(state.t>=m.total-1)state.t=0;state.running=!state.running;render();};
$('restart').onclick=()=>select(state.id);
$('all').onclick=()=>{state.all=!state.all;state.step=0;if(state.all)select(tour[0],true);else $('all').setAttribute('aria-pressed','false');};
$('scrub').oninput=e=>seekFrame(Number(e.target.value));
$('prev').onclick=()=>seekFrame(frameIndex(movies.get(state.id),state.t)-1);
$('next').onclick=()=>seekFrame(frameIndex(movies.get(state.id),state.t)+1);
for(const id of ['fx','enemy','guide','flip'])$(id).onchange=render;
document.addEventListener('keydown',e=>{
  if(/INPUT|SELECT|TEXTAREA/.test(e.target.tagName))return;
  if(e.code==='Space'){e.preventDefault();$('play').click();}
  else if(e.code==='ArrowLeft'){e.preventDefault();$('prev').click();}
  else if(e.code==='ArrowRight'){e.preventDefault();$('next').click();}
  else if(e.key==='1')select('공격1');else if(e.key==='2')select('공격2');else if(e.key.toLowerCase()==='c')select('연속');
});
Promise.all(loaded.map(im=>im.decode())).then(()=>{if(location.hash==='#all'){state.all=true;select('대기',true);}else select('대기');requestAnimationFrame(tick);}).catch(error=>{$('error').textContent='이미지를 불러오지 못했습니다. 페이지를 새로고침해 주세요.';console.error(error);});
