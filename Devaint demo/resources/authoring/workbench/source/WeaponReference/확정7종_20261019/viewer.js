const $=id=>document.getElementById(id),cache={},fxCache={},cards={};
let t=0,last=null,playing=false,ready=false,selectedHit=0,manual=true,actors={};
const sum=s=>s.reduce((a,b)=>a+b.ms,0);
function visibleWeapons(){return D.weapons.filter(w=>$('weapon').value==='all'||w.id===$('weapon').value);}
function pick(seq,time){let start=0;for(let i=0;i<seq.length;i++){if(time<start+seq[i].ms)return {step:seq[i],i};start+=seq[i].ms;}return {step:seq.at(-1),i:seq.length-1};}
function actor(w){return actors[w.id]||(actors[w.id]={lastHit:0,queued:0,queuedAt:0,active:false,current:null,history:[]});}
function queueFull(w){return w.manualInput.maxQueued!==null&&actor(w).queued>=w.manualInput.maxQueued;}
function nextInput(w){const a=actor(w);return (a.lastHit+a.queued)%w.hits.length+1;}
function beginAction(w,a,started){a.lastHit=a.lastHit%w.hits.length+1;a.current={number:a.lastHit,started};a.active=true;a.history.push(a.current);}
function updateManual(){
 for(const w of D.weapons){const a=actors[w.id];if(!a)continue;
  while(a.active){const h=w.hits[a.current.number-1],fullEnd=a.current.started+h.end-h.start,chainEnd=a.current.started+w.manualInput.chainAt[a.current.number-1],end=a.queued?Math.max(chainEnd,a.queuedAt):fullEnd;if(t<end)break;if(a.queued){a.queued--;beginAction(w,a,end);a.queuedAt=end;}else a.active=false;}
  a.history=a.history.filter(inst=>t-inst.started<w.hits[inst.number-1].duration+100);
 }
}
function startManual(weaponId){
 if(weaponId&&$('weapon').value!==weaponId){$('weapon').value=weaponId;$('weapon').onchange();}
 const wasRunning=playing,fresh=!manual;
 if(fresh){manual=true;selectedHit=0;t=0;actors={};}
 $('motion').value='attack';updateManual();
 for(const w of visibleWeapons()){const a=actor(w);if(a.active){if(!queueFull(w)){if(!a.queued)a.queuedAt=t;a.queued++;}}else beginAction(w,a,t);}
 updateManual();playing=true;if(!wasRunning||fresh)last=null;controls();draw();
}
function hitDuration(){return Math.max(1,...visibleWeapons().map(w=>w.hits[selectedHit-1]?.duration||0));}
function current(){
 if(manual){const a=actor(visibleWeapons()[0]),inst=a.current,h=inst?visibleWeapons()[0].hits[inst.number-1]:null;return {key:'attack',local:inst?Math.min(t-inst.started,h.duration):0,duration:h?.duration||1};}
 if(selectedHit)return {key:'attack',local:Math.min(t,hitDuration()),duration:hitDuration()};
 const key=$('motion').value;if(key!=='all')return {key,local:t,duration:Math.max(...visibleWeapons().map(w=>sum(w.sequences[key])))};
 let time=t%sum(D.stages);for(const stage of D.stages){if(time<stage.ms)return {key:stage.key,local:time,duration:stage.ms};time-=stage.ms;}return {key:'idle',local:0,duration:640};
}
function stateFor(w,key,local){
 if(manual){const inst=actor(w).current;if(!inst)return {seq:w.sequences.idle,time:0,fxTime:-1,missing:false};const h=w.hits[inst.number-1],age=t-inst.started;if(age>=h.duration)return {seq:w.sequences.idle,time:0,fxTime:-1,missing:false};return {seq:h.steps,time:Math.min(age,h.end-h.start-1),fxTime:h.start+age,missing:false};}
 if(selectedHit){const hit=w.hits[selectedHit-1];if(!hit)return {seq:w.sequences.idle,time:0,fxTime:-1,missing:true};return {seq:hit.steps,time:Math.min(local,hit.end-hit.start-1),fxTime:hit.start+local,missing:false};}
 const seq=w.sequences[key],n=sum(seq),loop=$('motion').value!=='all'||['idle','walk','run'].includes(key),time=loop?local%n:Math.min(local,n-1);return {seq,time,fxTime:time,missing:false};
}
function activeEffects(w,time,key){
 if(key!=='attack'||!$('effects').checked)return [];
 if(manual){const records=[];for(const inst of actor(w).history){const fxTime=w.hits[inst.number-1].start+t-inst.started;w.fx.forEach((e,j)=>{if(e.hit===inst.number&&fxTime>=e.start&&fxTime<e.end)records.push({e,j,fxTime});});}return records;}
 return w.fx.map((e,j)=>({e,j,fxTime:time})).filter(({e})=>(!selectedHit||e.hit===selectedHit)&&time>=e.start&&time<e.end);
}
function controls(){
 document.querySelectorAll('[data-hit]').forEach(b=>{const n=+b.dataset.hit;b.disabled=n>0&&!visibleWeapons().some(w=>w.hits[n-1]);b.setAttribute('aria-pressed',String(!manual&&n===selectedHit));});
 const ws=visibleWeapons();$('attack').textContent='공격하기 · '+(ws.length===1?(queueFull(ws[0])?((actor(ws[0]).lastHit%ws[0].hits.length+1)+'타 예약됨'):'다음 '+nextInput(ws[0])+'타'):'다음 타격');$('attack').setAttribute('aria-pressed',String(manual));$('play').textContent=playing?'일시정지':'재생';$('seek').disabled=manual;
}
function draw(){
 if(!ready)return;const {key,local,duration}=current(),progress=manual||selectedHit?local:local%duration;
 if(manual){const ws=visibleWeapons(),a=actor(ws[0]);$('stage').textContent=ws.length===1?(a.active?a.current.number+'타 재생 중':'클릭 입력 대기')+(queueFull(ws[0])?' · 다음 타격 예약됨':' · 다음 클릭 '+nextInput(ws[0])+'타')+(a.queued?' · 예약 '+a.queued+'회':''):'직접 공격 · 화면이나 공격하기 버튼을 클릭하세요';}
 else $('stage').textContent=(selectedHit?selectedHit+'타만 · 한 번 재생':D.names[key])+' · '+(playing?'재생 중':'정지')+' · '+Math.floor(progress)+' / '+duration+'ms';
 $('seek').value=Math.round(progress/duration*1000);
 for(const w of D.weapons){const {canvas,ctx,status}=cards[w.id],state=stateFor(w,key,local),{step,i}=pick(state.seq,state.time);
  ctx.imageSmoothingEnabled=false;ctx.fillStyle='#fff';ctx.fillRect(0,0,canvas.width,canvas.height);
  if($('background').value==='checker')for(let y=0;y<canvas.height;y+=24)for(let x=0;x<canvas.width;x+=24){ctx.fillStyle=(x/24+y/24)%2?'#e0e7ec':'#f1f5f7';ctx.fillRect(x,y,24,24);}
  if($('guide').checked){ctx.strokeStyle='#beced8';ctx.beginPath();ctx.moveTo(0,D.ground[1]*3+.5);ctx.lineTo(canvas.width,D.ground[1]*3+.5);ctx.stroke();}
  const p=w.pivots[step.frame]||w.pivot,o=w.offsets[step.frame]||[0,0],im=cache[w.id][step.frame];ctx.drawImage(im,(D.ground[0]-p[0]+o[0])*3,(D.ground[1]-p[1]+o[1])*3,im.width*3,im.height*3);
  if(!state.missing)for(const {e,j,fxTime} of activeEffects(w,state.fxTime,key)){ctx.save();ctx.globalAlpha=Math.max(0,Math.min(e.fadeIn?Math.min(1,(fxTime-e.start)/e.fadeIn):1,e.fadeOut?Math.min(1,(e.end-fxTime)/e.fadeOut):1));const x=(D.ground[0]+e.x)*3,y=(D.ground[1]+e.y)*3;if(e.flip){ctx.translate(x+e.width*3,y);ctx.scale(-1,1);ctx.drawImage(fxCache[w.id][j],0,0,e.width*3,e.height*3);}else ctx.drawImage(fxCache[w.id][j],x,y,e.width*3,e.height*3);ctx.restore();}
  if(manual){const a=actor(w);status.textContent=(a.active?a.current.number+'타':'대기')+(queueFull(w)?' · 다음 타격 예약됨':' · 다음 '+nextInput(w)+'타')+(a.queued?' · 예약 '+a.queued+'회':'');}
  else status.textContent=state.missing?selectedHit+'타 없음 · 대기':(selectedHit?selectedHit+'타':D.names[key])+' · '+(i+1)+'/'+state.seq.length+'프레임';
 }controls();
}
function startHit(n){if(n&&!visibleWeapons().some(w=>w.hits[n-1]))return;manual=false;actors={};selectedHit=n;$('motion').value='attack';t=0;last=null;playing=true;controls();draw();}
for(const w of D.weapons){const article=document.createElement('article');article.innerHTML='<header><h2>'+w.name+'</h2><small>'+w.hits.length+'연타 · '+sum(w.sequences.attack)+'ms</small></header><canvas></canvas><footer><span></span><a href="index.html?weapon='+w.id+'">이 무기 크게 ↗</a></footer>';const canvas=article.querySelector('canvas');canvas.width=D.canvas[0]*3;canvas.height=D.canvas[1]*3;canvas.onclick=()=>startManual(w.id);canvas.setAttribute('aria-label',w.name+' 공격 화면. 클릭하면 다음 타격.');cards[w.id]={root:article,canvas,ctx:canvas.getContext('2d'),status:article.querySelector('footer span')};$('grid').append(article);$('weapon').add(new Option(w.name,w.id));}
$('attack').onclick=()=>startManual();document.querySelectorAll('[data-hit]').forEach(b=>b.onclick=()=>startHit(+b.dataset.hit));
$('weapon').onchange=()=>{const id=$('weapon').value;$('grid').classList.toggle('single',id!=='all');D.weapons.forEach(w=>cards[w.id].root.hidden=id!=='all'&&w.id!==id);if(selectedHit&&!visibleWeapons().some(w=>w.hits[selectedHit-1]))selectedHit=1;t=0;actors={};last=null;playing=false;controls();draw();};
$('motion').onchange=()=>{manual=false;actors={};selectedHit=0;t=0;last=null;playing=true;controls();draw();};
$('play').onclick=()=>{if(!manual&&selectedHit&&t>=hitDuration())t=0;playing=!playing;last=null;controls();draw();};
$('reset').onclick=()=>{t=0;actors={};last=null;playing=!manual;controls();draw();};
$('seek').oninput=()=>{if(manual)return;const c=current();let base=0;if(!selectedHit&&$('motion').value==='all')for(const stage of D.stages){if(stage.key===c.key)break;base+=stage.ms;}t=base+Number($('seek').value)/1000*c.duration;playing=false;last=null;controls();draw();};
for(const id of ['background','guide','effects'])$(id).onchange=draw;
document.addEventListener('keydown',e=>{if(['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;if(e.code==='Space'){e.preventDefault();if(!e.repeat)startManual();}else if(/^[1-4]$/.test(e.key)){e.preventDefault();startHit(+e.key);}});
const focus=new URLSearchParams(location.search).get('weapon')||'rapier';if(D.weapons.some(w=>w.id===focus)){$('weapon').value=focus;$('weapon').onchange();}
function tick(now){if(ready&&playing&&last!==null){t+=Math.max(0,Math.min(100,now-last))*Number($('speed').value);if(manual)updateManual();else if(selectedHit&&t>=hitDuration()){t=hitDuration();playing=false;}}last=now;draw();requestAnimationFrame(tick);}
const load=src=>new Promise((ok,no)=>{const im=new Image();im.onload=()=>ok(im);im.onerror=no;im.src=src;});
Promise.all(D.weapons.map(async w=>{cache[w.id]={};await Promise.all(Object.entries(w.frames).map(async([id,png])=>cache[w.id][id]=await load(png)));fxCache[w.id]=await Promise.all(w.fx.map(e=>load(e.png)));})).then(()=>{ready=true;controls();draw();requestAnimationFrame(tick);}).catch(()=>{$('stage').textContent='이미지를 불러오지 못했습니다.';});
