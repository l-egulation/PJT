'use strict';
const D=window.OIL_MOTION,$=id=>document.getElementById(id),images=new Map();let elapsed=0,previous=null,playing=true,rate=1;
const model={
  frameAt(ds,t){let n=0;for(let k=0;k<ds.length;k++){n+=ds[k];if(t<n)return k}return ds.length-1},
  effectAt(t){return t<550||t>=D.retractStartMs?null:model.frameAt(D.effectDurationsMs,t-550)},
  phase(t){if(t<550)return '준비';if(t<D.retractStartMs)return D.effectPhaseNames[model.effectAt(t)];if(t<D.motionMs)return '노즐 회수';return '확인 대기'}
};window.oilMotionModel=model;
function draw(){
  const t=((elapsed%D.cycleMs)+D.cycleMs)%D.cycleMs,c=$('scene'),map=$('background').value==='map',dark=$('background').value==='dark',origin=map?[0,0]:[110,145],w=map?960:400,h=map?540:235;
  if(c.width!==w)c.width=w;if(c.height!==h)c.height=h;c.style.width=w*(map?1:2)+'px';c.style.height=h*(map?1:2)+'px';const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.fillStyle=dark?'#25252b':'white';ctx.fillRect(0,0,w,h);if(map&&images.has('map'))ctx.drawImage(images.get('map'),0,0);
  ctx.save();ctx.translate(-origin[0],-origin[1]);const b=model.frameAt(D.bodyDurationsMs,t),e=model.effectAt(t),oil=e===null?null:images.get(D.effectPaths[e]),body=images.get(D.bodyPaths[b]);const x=D.bodyOffsetWorld[0]+D.nozzleNativePx[0]-D.effectPivotPx[0],y=D.bodyOffsetWorld[1]+D.nozzleNativePx[1]-D.effectPivotPx[1];
  if(oil)ctx.drawImage(oil,x,y);if(body)ctx.drawImage(body,...D.bodyOffsetWorld);
  if($('coverage').checked&&e!==null){const box=D.effectRecords[e].bbox;if(box){ctx.strokeStyle='#399e70';ctx.lineWidth=1;ctx.strokeRect(x+box[0]+.5,y+box[1]+.5,box[2]-box[0]-1,box[3]-box[1]-1)}}ctx.restore();
  $('phase').textContent=`${model.phase(t)} · ${(t/1000).toFixed(2)} / ${(D.cycleMs/1000).toFixed(2)}초`;$('scrub').max=D.timeline.length-1;$('scrub').value=model.frameAt(D.timeline.map(r=>r.durationMs),t);$('timing').textContent=`기름 ${D.effectPaths.length}키 · ${(D.retractStartMs/1000).toFixed(2)}초에 노즐 회수 시작 · 기존 몸체19장 유지`;
}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'정지':'재생'};$('restart').onclick=()=>{elapsed=0;previous=null;draw()};$('speed').onchange=e=>rate=+e.target.value;$('background').onchange=$('coverage').onchange=draw;
function seek(i){playing=false;$('play').textContent='재생';elapsed=D.timeline[(i+D.timeline.length)%D.timeline.length].timeMs;draw()}
$('scrub').oninput=e=>seek(+e.target.value);$('prev').onclick=()=>seek(+$('scrub').value-1);$('next').onclick=()=>seek(+$('scrub').value+1);$('h3').src=D.h3Webp;
function tick(now){if(previous!==null&&playing)elapsed+=Math.min(now-previous,250)*rate;previous=now;draw();requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
let loaded=0;const total=Object.keys(D.images).length;for(const [p,uri] of Object.entries(D.images)){const im=new Image();im.onload=()=>{images.set(p,im);$('load').textContent=`${++loaded}/${total} 이미지 준비`;draw()};im.onerror=()=>{$('error').textContent='이미지 읽기 실패: '+p};im.src=uri}
