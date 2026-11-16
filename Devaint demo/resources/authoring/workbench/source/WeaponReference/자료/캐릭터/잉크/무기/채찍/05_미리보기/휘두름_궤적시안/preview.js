const $ = id => document.getElementById(id);
const c = $('view').getContext('2d');
c.imageSmoothingEnabled = false;
const enemy = new Image(); enemy.src = ENEMY;
const pictures = MOTIONS.map(m => m.frames.map(f => { const im = new Image(); im.src = f.image; return im; }));
const effectPictures = WHIP_FX.map(m => m.images.map(src => { const im = new Image(); im.src=src; return im; }));
let attack = 0, time = 0, running = true, last = null, ready = false;
const starts = MOTIONS.map(m => { let t=0; return m.frames.map(f => { const start=t; t+=f.ms; return start; }); });
function frameAt(ms) {
  const m = MOTIONS[attack];
  for (let i=m.frames.length-1;i>=0;i--) if (ms>=starts[attack][i]) return i;
  return 0;
}
function draw() {
  if (!ready) return;
  const m=MOTIONS[attack], index=frameAt(time), f=m.frames[index];
  c.fillStyle='#495562'; c.fillRect(0,0,392,176);
  c.fillStyle='#75818c'; c.fillRect(12,147,368,1);
  c.drawImage(enemy,206,1);
  c.drawImage(pictures[attack][index],0,0);
  const fx=WHIP_FX[attack], fxIndex=Math.floor((time-fx.start_ms)/fx.step_ms);
  if ($('effects').checked && fxIndex>=0 && fxIndex<effectPictures[attack].length) {
    c.drawImage(effectPictures[attack][fxIndex],0,0);
  }
  if ($('tip').checked) {
    const [x,y]=f.points[f.points.length-1];
    c.strokeStyle='#fff'; c.lineWidth=1;
    c.beginPath(); c.arc(Math.round(x),Math.round(y),3.5,0,Math.PI*2); c.stroke();
  }
  $('phase').textContent=f.phase;
  $('description').textContent=f.label;
  $('counter').textContent=`동작 ${index+1} / ${m.frames.length}${fxIndex>=0&&fxIndex<effectPictures[attack].length?' · 이펙트 '+(fxIndex+1):''}`;
  $('scrub').value=time;
  $('play').textContent=running?'일시정지':'재생';
  document.querySelectorAll('[data-phase]').forEach(e => e.classList.toggle('active',e.dataset.phase===f.phase));
  [...$('frames').children].forEach((e,i)=>e.setAttribute('aria-pressed',String(i===index)));
}
function selectFrame(index) { running=false; time=starts[attack][index]; draw(); }
function selectAttack(index) {
  attack=index; time=0; running=true; last=null;
  $('horizontal').setAttribute('aria-pressed',String(index===0));
  $('downward').setAttribute('aria-pressed',String(index===1));
  $('scrub').max=MOTIONS[index].total;
  $('frames').replaceChildren();
  MOTIONS[index].frames.forEach((f,i)=>{
    const b=document.createElement('button'); b.textContent=String(i+1).padStart(2,'0');
    b.title=`${f.phase} · ${f.ms} ms · ${f.label}`;
    b.setAttribute('aria-label',`${i+1}프레임 ${f.phase}`);
    b.onclick=()=>selectFrame(i); $('frames').append(b);
  });
  draw();
}
$('horizontal').onclick=()=>selectAttack(0);
$('downward').onclick=()=>selectAttack(1);
$('play').onclick=()=>{running=!running; if(running&&time>=MOTIONS[attack].total)time=0; last=null; draw();};
$('restart').onclick=()=>{time=0;running=true;last=null;draw();};
$('scrub').oninput=e=>{running=false;time=Number(e.target.value);draw();};
$('impact').onclick=()=>{running=false;time=WHIP_FX[attack].impact_ms+10;draw();};
$('tip').onchange=draw;
$('effects').onchange=draw;
function tick(now) {
  if(ready && running && last!==null && !document.hidden) {
    time+=Math.min(80,now-last)*Number($('speed').value);
    if(time>=MOTIONS[attack].total) {
      if($('loop').checked)time%=MOTIONS[attack].total;
      else {time=MOTIONS[attack].total;running=false;}
    }
  }
  last=now; draw(); requestAnimationFrame(tick);
}
Promise.all([enemy.decode(),...pictures.flat().map(i=>i.decode()),...effectPictures.flat().map(i=>i.decode())]).then(()=>{
  ready=true; selectAttack(0); requestAnimationFrame(tick);
}).catch(err=>{$('phase').textContent='이미지를 불러오지 못했습니다.';$('description').textContent=err.message;});
