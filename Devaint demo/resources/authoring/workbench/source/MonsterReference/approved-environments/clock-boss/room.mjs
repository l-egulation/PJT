import {createPassage,passageSpec,ceilingY} from './passage.mjs';
import {journeySpec} from './journey-layout.mjs';
const canvas=document.querySelector('#scene'),sceneCtx=canvas.getContext('2d');
let ctx=sceneCtx;
ctx.imageSmoothingEnabled=false;
const $=id=>document.getElementById(id),images={};
let mode='approach',elapsed=0,paused=matchMedia('(prefers-reduced-motion: reduce)').matches,last=0,progress=0,traveling=false,passage;
const passageVersions={};
const names=['aged-wall-source','aged-alcove-source','aged-buttress-source','aged-gear-large-source','aged-gear-small-source','aged-pulley-source','boss-rear','boss-original-parts','hero-original','preview-existing-floor','suspension-connector','existing-vault','terrain-body','terrain-wall-left','terrain-wall-right','terrain-underside','village_mid_native','village_tall_native','sky_native',...Array.from({length:5},(_,i)=>`float-${i}`)];
const chamber=document.createElement('canvas');chamber.width=1280;chamber.height=720;
const chamberCtx=chamber.getContext('2d');chamberCtx.imageSmoothingEnabled=false;

// The corridor and room use the same transparent pencil-drawn machinery.
// Render at native canvas resolution; keep the original floor and actors intact.
function roomProp(name,x,y,h,{angle=0,light=.76,alpha=.82}={}){
 const im=images[name],w=im.width*h/im.height;
 ctx.save();ctx.translate(Math.round(x),Math.round(y));ctx.rotate(angle);
 ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';
 ctx.filter='brightness('+light+')'+($('previous-colors').checked?' saturate(.48)':'');ctx.globalAlpha=alpha;
 ctx.drawImage(im,-w/2,-h/2,w,h);ctx.restore();
}
function bossMechanics(t){
 // Side recesses leave a calm center for the boss, with uneven placement
 // suggesting an old machine room rather than a symmetrical icon display.
 roomProp('aged-gear-large-source',139,368,228,{angle:t*.13,light:.76,alpha:.78});
 roomProp('aged-gear-small-source',221,493,141,{angle:-t*.21+.4,light:.77,alpha:.78});
 roomProp('aged-gear-large-source',1140,287,202,{angle:-t*.12+.8,light:.73,alpha:.80});
 roomProp('aged-gear-small-source',1061,402,132,{angle:t*.19,light:.75,alpha:.80});
 roomProp('aged-pulley-source',164,149,216,{light:.76,alpha:.72});
 roomProp('aged-pulley-source',1157,508,178,{light:.75,alpha:.75});
}
const debris=[{x:239,y:337,k:0,p:0},{x:257,y:480,k:2,p:2},{x:290,y:231,k:1,p:1.1},{x:946,y:353,k:3,p:3},{x:1029,y:462,k:4,p:1.7},{x:902,y:165,k:2,p:4.1}];
function ambience(t){ctx.save();
 for(const o of debris){const x=Math.round(o.x+Math.sin(t*.34+o.p)*5),y=Math.round(o.y+Math.sin(t*.72+o.p)*12);ctx.globalAlpha=.36;ctx.drawImage(images['float-'+o.k],x,y);}
 for(let i=0;i<30;i++){const q=(i*.173+t*.035)%1,x=(i%2?960:223)+(i*23)%165+Math.sin(t*.6+i)*7,y=590-q*450;ctx.globalAlpha=Math.sin(q*Math.PI)*.24;ctx.fillStyle=i%7===0?'#996856':'#9e9686';ctx.fillRect(Math.round(x),Math.round(y),i%3?2:3,2);}ctx.restore();
}
function renderChamber(t){
 ctx=chamberCtx;ctx.clearRect(0,0,1280,720);
 // One shared room light keeps plaster, stone and machines in the same value range.
 ctx.save();ctx.filter=$('previous-colors').checked?'brightness(.82) saturate(.48)':'brightness(.82)';ctx.drawImage(images['boss-rear'],0,0);ctx.restore();
 if($('decor').checked)bossMechanics(t);
 if($('ambient').checked)ambience(t);
 if($('actors').checked){ctx.drawImage(images['suspension-connector'],491,58);ctx.drawImage(images['suspension-connector'],765,58);ctx.drawImage(images['boss-original-parts'],384,47);}
 ctx=sceneCtx;
}
function draw(t=elapsed){
 if(!passage)return;
 const cam=Math.round(progress*journeySpec.cameraTravel),edge=journeySpec.roomStart-cam;
 ctx=sceneCtx;ctx.globalAlpha=1;ctx.clearRect(0,0,1280,720);
 passage.draw({cameraX:cam,t,decor:$('decor').checked,ambient:$('ambient').checked,actors:false,showGuide:$('guide').checked});
 if(edge<1280){
  renderChamber(t);
  ctx.save();ctx.beginPath();ctx.rect(Math.max(0,edge),0,1280,journeySpec.floorY);ctx.clip();
  ctx.drawImage(chamber,edge,0);ctx.restore();
 }
 ctx.drawImage(images['preview-existing-floor'],0,0);
 if($('actors').checked)ctx.drawImage(images['hero-original'],240,454);
}
function setMode(next){traveling=false;setProgress(next==='boss'?1:0);}
function updateProgress(){
 mode=progress>=1?'boss':'approach';
 const stage=[...journeySpec.stages].reverse().find(s=>progress>=s.progress)||journeySpec.stages[0];
 $('progress').value=progress*100;$('travel').textContent=traveling?'길 따라 보기 정지':'길 따라 보기 ▶';$('stage-name').textContent=stage.name;
 $('caption').textContent=progress>.80&&progress<1?'낮은 천장 너머로 시계실이 드러납니다.':stage.description;
 document.querySelectorAll('[data-progress]').forEach(b=>b.classList.toggle('active',Number(b.dataset.progress)===stage.progress));
 document.querySelectorAll('[data-mode]').forEach(b=>{b.classList.toggle('active',b.dataset.mode===mode);b.setAttribute('aria-pressed',String(b.dataset.mode===mode));});
}
function setProgress(value){progress=Math.max(0,Math.min(1,value));updateProgress();draw();}
$('progress').oninput=e=>{traveling=false;setProgress(Number(e.target.value)/100);};
document.querySelectorAll('[data-progress]').forEach(b=>b.onclick=()=>{traveling=false;setProgress(Number(b.dataset.progress));});
$('travel').onclick=()=>{if(progress>=1)progress=0;traveling=!traveling;if(traveling){paused=false;$('pause').textContent='움직임 정지';}updateProgress();};
document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>{setMode(b.dataset.mode);if(b.dataset.mode==='approach')$('travel').click();});
for(const id of ['decor','ambient','actors','guide'])$(id).onchange=()=>draw();
$('previous-colors').onchange=()=>{
 const previousColors=$('previous-colors').checked;
 passage=passageVersions[previousColors]??=createPassage(sceneCtx,images,{previousColors});
 $('color-status').textContent=$('previous-colors').checked?'이전 V11 · 회색으로 표시되던 색':'수정 V12 · 기존 마을의 크림색';
 draw();
};
$('pause').textContent=paused?'움직임 재생':'움직임 정지';$('pause').onclick=()=>{paused=!paused;$('pause').textContent=paused?'움직임 재생':'움직임 정지';};
$('reset').onclick=()=>{elapsed=0;traveling=false;setProgress(0);};
$('save').onclick=()=>canvas.toBlob(blob=>{const u=URL.createObjectURL(blob),a=document.createElement('a');a.href=u;a.download=`clock-journey-v12-${$('previous-colors').checked?'before':'warm'}-${Math.round(progress*100)}.png`;a.click();setTimeout(()=>URL.revokeObjectURL(u),1500);});
try{
 await Promise.all(names.map(n=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[n]=im;resolve();};im.onerror=()=>reject(new Error('불러오지 못한 그림: '+n));im.src=`assets/${n}.png`;})));
 passage=createPassage(sceneCtx,images);
 passageVersions.false=passage;
 $('loading').hidden=true;setMode('approach');
 const requestedProgress=Number(new URLSearchParams(location.search).get('progress'));
 if(Number.isFinite(requestedProgress))setProgress(requestedProgress);
 window.roomPreview={setMode,setProgress,renderAt(t){elapsed=t;draw(t)},getState(){return{mode,paused,elapsed,progress,traveling,previousColors:$('previous-colors').checked,cameraX:Math.round(progress*journeySpec.cameraTravel)}},getPassageSpec(){return passageSpec},getJourneySpec(){return journeySpec},getCeilingY:ceilingY,occlusionAt:p=>passage.occlusionAt(p),wallCoverageAt:x=>passage.wallCoverageAt(x),canvas};
 window.roomReady=true;
 if(new URLSearchParams(location.search).has('autoplay')&&!paused)$('travel').click();
 requestAnimationFrame(function tick(now){
  const dt=last?Math.min((now-last)/1000,.05):0;last=now;
  if(!paused&&!document.hidden){elapsed+=dt;if(traveling){progress=Math.min(1,progress+dt/journeySpec.travelSeconds);if(progress>=1)traveling=false;updateProgress();}}
  draw();requestAnimationFrame(tick);
 });
}catch(error){$('loading').textContent=error.message;console.error(error);}
