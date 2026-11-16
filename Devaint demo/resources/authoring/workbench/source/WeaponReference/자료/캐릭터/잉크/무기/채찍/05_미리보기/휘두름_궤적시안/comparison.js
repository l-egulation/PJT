const $=id=>document.getElementById(id);
const contexts=[$('left'),$('right')].map(c=>{const g=c.getContext('2d');g.imageSmoothingEnabled=false;return g;});
const image=src=>{const i=new Image();i.src=src;return i;};
const enemy=image(ENEMY);
const previous=PREVIOUS.frames.map(f=>({body:image(f.image),fx:image(f.effect)}));
const current=MOTIONS.map(m=>m.frames.map(f=>image(f.image)));
const effects=WHIP_FX.map(f=>f.images.map(image));
const combo=PREVIOUS.combo;
const windows=[[-160,120],[-240,250],[-combo.impact_ms[0],Math.max(PREVIOUS.duration-PREVIOUS.impact_ms[0],combo.total_ms-combo.impact_ms[0])]];
let hit=2,relative=windows[2][0],running=true,ready=false,last=null,pause=0;
function currentIndex(t,h){let n=0;for(;n<MOTIONS[h].frames.length-1;n++){if(t<MOTIONS[h].frames[n].ms)break;t-=MOTIONS[h].frames[n].ms;}return n;}
function background(g){g.fillStyle='#495562';g.fillRect(0,0,392,176);g.fillStyle='#75818c';g.fillRect(12,147,368,1);g.drawImage(enemy,206,1);}
function drawEffect(g,t,h){const e=Math.floor((t-WHIP_FX[h].start_ms)/WHIP_FX[h].step_ms);if(e>=0&&e<effects[h].length)g.drawImage(effects[h][e],0,0);}
function renderCurrent(g,t,fx,h){const n=currentIndex(t,h);background(g);g.drawImage(current[h][n],0,0);if(fx)drawEffect(g,t,h);return {hit:h,index:n};}
function renderCombo(g,t,fx){
  const h=t<combo.join_ms?0:1,local=h===0?t:t-combo.second_offset_ms;
  const info=renderCurrent(g,local,false,h);
  if(fx){drawEffect(g,t,0);drawEffect(g,t-combo.second_offset_ms,1);}
  return info;
}
function renderRight(g,fx){return hit===2?renderCombo(g,combo.impact_ms[0]+relative,fx):renderCurrent(g,WHIP_FX[hit].impact_ms+relative,fx,hit);}
function updateLabels(){
  const same=$('mode').value==='effects',chain=hit===2;
  $('left-title').textContent=same?'이번 동작 · 이펙트 없음':'이전 · 기존 채찍 공격';
  $('right-title').textContent=same?'이번 동작 · 이펙트 있음':'이번 · 새 궤적과 잔상';
  $('left-desc').textContent=same?'캐릭터와 채찍만 재생':'타격 모양으로 바로 뻗기 + 기존 A 이펙트';
  $('right-desc').textContent=same?'흰색·회색 잔상과 손 방울':'접힘이 끝까지 이동 + 끝을 따라 남는 잔상';
  $('fx-label').hidden=same;
  $('intro').textContent=chain
    ? (same?'가로 공격 → 내려치기 연속 동작을 이펙트 전후로 비교합니다.':'가로 공격 → 내려치기를 연속으로 재생합니다. 왼쪽은 기존, 오른쪽은 이번 버전이며 첫 타격 순간을 맞췄습니다.')
    : (same?'동일한 채찍 동작을 이펙트 전후로 비교합니다.':'왼쪽은 기존, 오른쪽은 이번 버전입니다. 선택한 공격의 타격 순간을 맞춰 재생합니다.');
  $('impact').textContent=chain?'1타 타격 순간':'타격 순간';
  $('impact2').hidden=!chain;
}
function draw(){
  if(!ready)return;
  const mode=$('mode').value,fx=$('fx').checked;
  const info=renderRight(contexts[1],mode==='effects'||fx),m=MOTIONS[info.hit],f=m.frames[info.index];
  $('right-status').textContent=`이번 · ${info.hit+1}타 ${f.phase} · 동작 ${info.index+1} / ${m.frames.length}`;
  if(mode==='effects'){
    renderRight(contexts[0],false);$('left-status').textContent=`같은 ${info.hit+1}타 동작 · 이펙트 없음`;
  }else{
    const oldTime=PREVIOUS.impact_ms[hit===2?0:hit]+relative;
    let i=PREVIOUS.frames.findIndex(f=>oldTime<f.time+f.ms);if(i<0)i=previous.length-1;
    background(contexts[0]);contexts[0].drawImage(previous[i].body,0,0);
    if(fx)contexts[0].drawImage(previous[i].fx,0,0);
    $('left-status').textContent=`이전 · 원본 ${i+1} / ${previous.length} · ${PREVIOUS.frames[i].pose}`;
  }
  $('scrub').value=relative;
  const prefix=hit===2?'1타 타격':'타격';
  $('readout').textContent=relative<0?`${prefix} ${Math.round(-relative)}ms 전`:relative>0?`${prefix} ${Math.round(relative)}ms 후`:`${prefix} 순간`;
  $('play').textContent=running?'일시정지':'재생';
}
function selectHit(n){
  hit=n;relative=windows[n][0];pause=0;last=null;running=true;
  $('scrub').min=windows[n][0];$('scrub').max=windows[n][1];
  ['hit1','hit2','combo'].forEach((id,i)=>$(id).setAttribute('aria-pressed',String(i===n)));
  updateLabels();draw();
}
$('hit1').onclick=()=>selectHit(0);$('hit2').onclick=()=>selectHit(1);$('combo').onclick=()=>selectHit(2);
$('mode').onchange=()=>{updateLabels();draw();};
$('play').onclick=()=>{running=!running;last=null;draw();};
$('restart').onclick=()=>selectHit(hit);
$('impact').onclick=()=>{relative=0;pause=0;running=false;draw();};
$('impact2').onclick=()=>{relative=combo.impact_ms[1]-combo.impact_ms[0];pause=0;running=false;draw();};
$('scrub').oninput=e=>{relative=Number(e.target.value);pause=0;running=false;draw();};
$('fx').onchange=draw;
function tick(now){
  if(ready&&running&&last!==null&&!document.hidden){
    const dt=Math.min(80,now-last)*Number($('speed').value);
    if(relative>=windows[hit][1]){pause+=dt;if(!$('loop').checked)running=false;else if(pause>=220){relative=windows[hit][0];pause=0;}}
    else relative=Math.min(windows[hit][1],relative+dt);
  }
  last=now;draw();requestAnimationFrame(tick);
}
Promise.all([enemy,...previous.flatMap(f=>[f.body,f.fx]),...current.flat(),...effects.flat()].map(im=>im.decode())).then(()=>{ready=true;selectHit(2);requestAnimationFrame(tick);}).catch(e=>{$('intro').textContent='이미지를 불러오지 못했습니다: '+e.message;});
