const text=(el,value)=>{if(el.textContent!==value)el.textContent=value;};
const $=s=>document.querySelector(s),load=path=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>reject(new Error(`Image failed: ${path}`));im.src=path;});
const ARROW={'upper-left':'↖','right':'→'};
window.candidateReview={ready:false,errors:[]};
try{
 const r=await fetch('candidates.json');if(!r.ok)throw Error(`Config HTTP ${r.status}`);const D=await r.json();
 const T=D.commonTransform,[DW,DH]=T.destinationSize,[OX,OY]=T.rasterOffset;
 const ids=[...new Set(D.candidates.flatMap(c=>c.sourceFrames))],cut={};
 await Promise.all(ids.map(async id=>{const im=await load(D.frameBase+String(id).padStart(2,'0')+'.png');const canvas=document.createElement('canvas');canvas.width=640;canvas.height=360;const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.drawImage(im,0,0);const px=ctx.getImageData(0,0,640,360),d=px.data,wm=D.previewMatte.whiteMinimum,dm=D.previewMatte.maximumChannelDifference;for(let i=0;i<d.length;i+=4){const mn=Math.min(d[i],d[i+1],d[i+2]),mx=Math.max(d[i],d[i+1],d[i+2]);if(mn>=wm&&mx-mn<=dm)d[i+3]=0;}ctx.putImageData(px,0,0);cut[id]=canvas;}));
 const [idle,player,room]=await Promise.all(['../assets/fused-idle.png','../assets/player.png','../assets/room.png'].map(load));
 const cards={},states=Object.fromEntries(D.candidates.map(c=>[c.id,{time:0,playing:false,loop:false}]));let selected=D.candidates[0].id,last=performance.now(),detailKey='',mapKey='';const paintKeys=new Map();
 $('#seek').max=Math.max(...D.candidates.map(c=>c.totalMs));
 const starts=c=>c.durationsMs.map((_,i)=>c.durationsMs.slice(0,i).reduce((a,b)=>a+b,0));
 function select(c){selected=c.id;text($('#selected'),selected);}
 function playOnly(c,restart=false){for(const s of Object.values(states))s.playing=false;const s=states[c.id];if(restart||s.time>=c.totalMs)s.time=0;s.playing=true;select(c);last=performance.now();draw();}
 function index(c){let t=Math.min(states[c.id].time,c.totalMs-0.001);for(let i=0;i<c.durationsMs.length;i++){if(t<c.durationsMs[i])return i;t-=c.durationsMs[i];}return c.sourceFrames.length-1;}
 const key=c=>c.sourceFrames[index(c)];
 function step(c,offset){const s=states[c.id],next=Math.max(0,Math.min(c.sourceFrames.length-1,index(c)+offset));s.playing=false;s.time=starts(c)[next];select(c);draw();}
 function shotText(c){const t=states[c.id].time,n=c.releaseCuesMs.filter(x=>t>=x).length;return n?`방출 ${n}/5 ${ARROW[D.visualDirections[n-1]]}`:'준비';}
 for(const c of D.candidates){const st=starts(c),article=document.createElement('article');article.className='card';
  article.innerHTML=`<h2>${c.id}. ${c.name}</h2><canvas width="384" height="256"></canvas><p class="shot"></p><div class="local-controls"><button class="toggle">▶ 재생</button><button class="restart">↺ 처음부터</button><button class="prev">◀ 이전</button><button class="next">다음 ▶</button></div><label class="local-seek">${c.id} 시간 <input aria-label="${c.id} 시간 탐색" type="range" min="0" max="${c.totalMs}" step="1" value="0"><output>0 / ${c.totalMs} ms</output></label><label><input class="local-loop" type="checkbox"> ${c.id}만 반복</label><div class="timeline">${c.durationsMs.map((v,i)=>`<span class="${c.roles[i]}" title="${String(c.sourceFrames[i]).padStart(2,'0')} · ${v}ms" style="width:${v/c.totalMs*100}%"></span>`).join('')}${c.releaseCuesMs.map(x=>`<b class="cue" style="left:${x/c.totalMs*100}%"></b>`).join('')}<b class="now"></b></div><p>${c.frameCount}장 · ${(c.totalMs/1000).toFixed(2)}초 · 방출 ${c.releaseCuesMs.map(x=>x/1000).join(' / ')}초</p><p>${c.note}</p><p class="number">${c.sourceFrames.map((f,i)=>`${String(f).padStart(2,'0')}:${c.durationsMs[i]}`).join(' ')}</p><p class="number current"></p><button class="select">${c.id} 아래 비교에 보기</button>`;
  $('#choices').append(article);cards[c.id]=article;
  article.querySelector('.toggle').onclick=()=>{if(states[c.id].playing){states[c.id].playing=false;draw();}else playOnly(c);};
  article.querySelector('.restart').onclick=()=>playOnly(c,true);
  article.querySelector('.prev').onclick=()=>step(c,-1);article.querySelector('.next').onclick=()=>step(c,1);
  article.querySelector('input[type="range"]').oninput=e=>{states[c.id].playing=false;states[c.id].time=Number(e.target.value);select(c);draw();};
  article.querySelector('.local-loop').onchange=e=>{states[c.id].loop=e.target.checked;};
  article.querySelector('.select').onclick=()=>{select(c);draw();};
 }
 function clear(canvas){const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.fillStyle=$('#bg').value;ctx.fillRect(0,0,canvas.width,canvas.height);return ctx;}
 function pose(ctx,id,x=192,y=232){ctx.imageSmoothingEnabled=false;ctx.drawImage(cut[id],x-192+OX,y-232+OY,DW,DH);}
 function draw(){
  for(const c of D.candidates){const card=cards[c.id],s=states[c.id],i=index(c),id=c.sourceFrames[i],pk=id+'/'+$('#bg').value;
   if(paintKeys.get(c.id)!==pk){pose(clear(card.querySelector('canvas')),id);paintKeys.set(c.id,pk);}
   text(card.querySelector('.shot'),shotText(c));
   text(card.querySelector('.current'),`원본 ${String(id).padStart(2,'0')} · ${c.durationsMs[i]}ms · ${s.playing?'재생 중':'정지'}${s.time>=c.totalMs?' · 끝 유지':''}`);
   card.querySelector('.now').style.left=`${Math.min(s.time,c.totalMs)/c.totalMs*100}%`;
   card.querySelector('.select').classList.toggle('active',c.id===selected);
   text(card.querySelector('.toggle'),s.playing?'Ⅱ 일시정지':'▶ 재생');
   card.querySelector('input[type="range"]').value=s.time;text(card.querySelector('output'),`${Math.round(s.time)} / ${c.totalMs} ms`);
   card.querySelector('.prev').disabled=i===0;card.querySelector('.next').disabled=i===c.sourceFrames.length-1;card.querySelector('.local-loop').checked=s.loop;}
  const c=D.candidates.find(c=>c.id===selected),id=key(c),dk=id+'/'+$('#bg').value;
  if(detailKey!==dk){clear($('#baseline')).drawImage(idle,0,0);pose(clear($('#motion')),id);detailKey=dk;}
  const m=$('#map');if(mapKey!==id){const ctx=m.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(room,0,0);ctx.drawImage(player,180-192,445-232);ctx.drawImage(idle,440-192,445-232);pose(ctx,id,740,445);mapKey=id;}
  const z=Number($('#mapZoom').value);m.style.width=960*z+'px';m.style.height=540*z+'px';
  $('#seek').value=states[selected].time;text($('#time'),`${selected}: ${Math.round(states[selected].time)} ms`);
  window.candidateReview.states=Object.fromEntries(Object.entries(states).map(([k,s])=>[k,{...s}]));window.candidateReview.currentFrames=Object.fromEntries(D.candidates.map(c=>[c.id,key(c)]));}
 $('#play').onclick=()=>{for(const s of Object.values(states)){s.time=0;s.playing=true;}last=performance.now();draw();};
 $('#pause').onclick=()=>{for(const s of Object.values(states))s.playing=false;draw();};
 $('#loop').onchange=()=>{for(const s of Object.values(states))s.loop=$('#loop').checked;draw();};
 $('#seek').oninput=()=>{for(const c of D.candidates){states[c.id].playing=false;states[c.id].time=Math.min(Number($('#seek').value),c.totalMs);}draw();};
 $('#bg').onchange=()=>{mapKey='';draw();};$('#mapZoom').onchange=draw;
 function tick(now){let changed=false;const dt=(now-last)*Number($('#speed').value);for(const c of D.candidates){const s=states[c.id];if(!s.playing)continue;changed=true;s.time+=dt;if(s.time>=c.totalMs){if(s.loop)s.time%=c.totalMs;else{s.time=c.totalMs;s.playing=false;}}}if(changed)draw();last=now;requestAnimationFrame(tick);}
 draw();requestAnimationFrame(tick);
 window.candidateReview={...window.candidateReview,ready:true,loadedFrames:ids.length,candidates:D.candidates.length};
 text($('#status'),`원본 ${ids.length}장과 비교 이미지 3장 로딩 완료. 후보를 고르면 배경제거·픽셀 정리를 진행합니다.`);
}catch(error){window.candidateReview.errors.push(String(error));$('#status').textContent=String(error);$('#status').className='errors';throw error;}
