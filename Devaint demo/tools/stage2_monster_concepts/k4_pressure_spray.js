'use strict';
const D=window.PRESSURE_SPRAY,$=id=>document.getElementById(id),images=new Map();
let time=0,previous=null,playing=true,speed=1;
const model={
  frameAt(ds,t){let n=0;for(let i=0;i<ds.length;i++){n+=ds[i];if(t<n)return i}return ds.length-1},
  effectAt(t){return t<D.windupMs||t>=D.retractStartMs?null:Math.min(D.effectPaths.length-1,Math.floor((t-D.windupMs)/D.sampleMs))},
  phase(t){if(t<550)return '준비';if(t<710)return '묵직한 분출 · 노즐 고정';if(t<1200)return '압력 감소 · 낮아지는 물줄기';if(t<D.retractStartMs)return '끝물 낙하 · 노즐 고정';if(t<D.motionMs)return '잉크 종료 → 노즐 회수';return '확인 대기'},
  coverageAt(t){const k=model.effectAt(t);if(k===null)return null;const b=D.effectCoverage[k].bboxLocalPx;if(!b)return null;const x=D.bodyOffsetWorld[0]+D.nozzleNativePx[0]-D.effectPivotPx[0],y=D.bodyOffsetWorld[1]+D.nozzleNativePx[1]-D.effectPivotPx[1];return [x+b[0],y+b[1],b[2]-b[0],b[3]-b[1]]}
};window.pressureSprayModel=model;
function draw(){
  const t=time%D.cycleMs,c=$('scene'),map=$('background').value==='map',dark=$('background').value==='dark',origin=map?[0,0]:[110,145],w=map?960:400,h=map?540:235;
  if(c.width!==w)c.width=w;if(c.height!==h)c.height=h;c.style.width=w*(map?1:2)+'px';c.style.height=h*(map?1:2)+'px';
  const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.fillStyle=dark?'#25252b':'white';ctx.fillRect(0,0,w,h);if(map&&images.has('map'))ctx.drawImage(images.get('map'),0,0);
  ctx.save();ctx.translate(-origin[0],-origin[1]);const k=model.effectAt(t),body=model.frameAt(D.bodyDurationsMs,t),fx=k===null?null:images.get(D.effectPaths[k]),im=images.get(D.bodyPaths[body]);
  if(fx)ctx.drawImage(fx,D.bodyOffsetWorld[0]+D.nozzleNativePx[0]-D.effectPivotPx[0],D.bodyOffsetWorld[1]+D.nozzleNativePx[1]-D.effectPivotPx[1]);if(im)ctx.drawImage(im,...D.bodyOffsetWorld);
  const r=model.coverageAt(t);if($('coverage').checked&&r){ctx.strokeStyle='#3aab77';ctx.lineWidth=1;ctx.strokeRect(r[0]+.5,r[1]+.5,r[2]-1,r[3]-1)}ctx.restore();
  $('phase').textContent=`${model.phase(t)} · ${(t/1000).toFixed(2)} / ${(D.cycleMs/1000).toFixed(2)}초`;$('scrub').value=Math.round(t);$('scrub').max=D.cycleMs-1;
  $('numbers').textContent=`분출 0.65초 · 잉크 종료 ${(D.retractStartMs/1000).toFixed(2)}초부터 회수 · ${D.effectPaths.length}개 효과 키${r?' · 현재 보이는 범위 '+r[2]+'×'+r[3]+'px':''}`;
}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'정지':'재생'};$('restart').onclick=()=>{time=0;previous=null;draw()};$('speed').onchange=e=>speed=+e.target.value;$('background').onchange=$('coverage').onchange=draw;
function seek(t){playing=false;$('play').textContent='재생';time=(t+D.cycleMs)%D.cycleMs;draw()}
$('scrub').oninput=e=>seek(+e.target.value);$('prev').onclick=()=>seek(time-50);$('next').onclick=()=>seek(time+50);
$('export').onclick=()=>{const obj={schema:'stage2-k4-pressure-coverage-review-v1',status:'visual-coverage-only-not-runtime-hitbox',timeMs:time%D.cycleMs,worldRectPx:model.coverageAt(time%D.cycleMs),effectKey:model.effectAt(time%D.cycleMs),emissionWindowMs:[550,1200],retractStartMs:D.retractStartMs,runtimeIntegration:'not-performed'};const url=URL.createObjectURL(new Blob([JSON.stringify(obj,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='stage2-k4-pressure-coverage.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)};
$('h3').src=D.h3Webp;
function tick(now){if(previous!==null&&playing)time+=Math.min(now-previous,250)*speed;previous=now;draw();requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
let loaded=0;const total=Object.keys(D.images).length;
for(const [p,uri] of Object.entries(D.images)){const im=new Image();im.onload=()=>{images.set(p,im);$('load').textContent=`${++loaded}/${total} 이미지 준비 · 압력 분사 검토`;draw()};im.onerror=()=>{$('error').textContent='이미지 읽기 실패: '+p};im.src=uri}
