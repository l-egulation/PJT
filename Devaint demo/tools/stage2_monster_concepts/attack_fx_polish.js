'use strict';
const D=window.ATTACK_FX,$=id=>document.getElementById(id),images=new Map();
let playing=true,time=0,previous=null;
const settings=()=>({lengthPx:+$('length').value,heightPx:+$('height').value,nozzleOffsetNativePx:[+$('offset-x').value,+$('offset-y').value]});
const model={
  frameAt(ds,t){let acc=0;for(let k=0;k<ds.length;k++){acc+=ds[k];if(t<acc)return k}return ds.length-1},
  active(t){return t>=D.k4.attackStartMs&&t<D.k4.attackEndMs},
  rect(s){const x=D.k4.nozzleNativePx[0]+D.k4.bodyOffsetWorld[0]+s.nozzleOffsetNativePx[0],y=D.k4.nozzleNativePx[1]+D.k4.bodyOffsetWorld[1]+s.nozzleOffsetNativePx[1];return [x-s.lengthPx,y-s.heightPx/2,s.lengthPx,s.heightPx]},
  export(s){return {schema:'stage2-k4-spray-envelope-review-v1',status:'user-adjusted-preview-not-runtime-hitbox',direction:'left',pixelsPerUnit:32,...s,worldRectPx:model.rect(s),activeWindowMs:[550,1200],durationMs:650,hitPolicy:'once per target per spray',damageOrUnityChanged:false}}
};window.attackFxModel=model;
function labels(){const s=settings();$('length-value').textContent=s.lengthPx+'px';$('height-value').textContent=s.heightPx+'px';$('offset-x-value').textContent=s.nozzleOffsetNativePx[0]+'px';$('offset-y-value').textContent=s.nozzleOffsetNativePx[1]+'px'}
function draw(){
  const s=settings(),t=$('hold').checked?550+time%650:time%2400,active=model.active(t),bodyKey=model.frameAt(D.results.k4.bodyDurationsMs,t),body=images.get(D.results.k4.bodyPaths[bodyKey]);
  const c=$('k4'),map=$('background').value==='map',dark=$('background').value==='dark',origin=map?[0,0]:[110,100],w=map?960:440,h=map?540:220;
  if(c.width!==w)c.width=w;if(c.height!==h)c.height=h;c.style.width=w*(map?1:2)+'px';c.style.height=h*(map?1:2)+'px';
  const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.fillStyle=dark?'#25252b':'white';ctx.fillRect(0,0,w,h);
  if(map&&images.has('map'))ctx.drawImage(images.get('map'),0,0);
  ctx.save();ctx.translate(-origin[0],-origin[1]);const r=model.rect(s);
  if(active){const f=images.get(D.results.k4.paths[Math.floor((t-550)/D.k4.effectKeyMs)%D.k4.effectKeys]);if(f)ctx.drawImage(f,r[0],r[1],r[2],r[3])}
  if(body)ctx.drawImage(body,...D.k4.bodyOffsetWorld);
  if($('hitbox').checked){ctx.strokeStyle=active?'#35a870':'#aaa0a0';ctx.lineWidth=1;ctx.setLineDash(active?[]:[4,4]);ctx.strokeRect(r[0]+.5,r[1]+.5,r[2]-1,r[3]-1);ctx.setLineDash([]);ctx.fillStyle='#c67b30';ctx.fillRect(r[0]+r[2]-1,r[1]+r[3]/2-1,3,3)}
  ctx.restore();$('k4-status').textContent=`${t<550?'준비 · 공격 판정 없음':active?'분사 중 · 범위 활성':t<1900?'회복 · 공격 판정 없음':'반복 확인 대기'} / ${(t/1000).toFixed(2)}초 · 임시 범위 ${r[2]}×${r[3]}px (${(r[2]/32).toFixed(2)}×${(r[3]/32).toFixed(2)}유닛)`;
}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'정지':'재생'};
$('restart').onclick=()=>{time=0;previous=null;draw()};$('reset').onclick=()=>{for(const [id,v] of Object.entries({length:128,height:40,'offset-x':0,'offset-y':0}))$(id).value=v;labels();draw()};
for(const id of ['length','height','offset-x','offset-y'])$(id).oninput=()=>{labels();draw()};for(const id of ['hitbox','hold','background'])$(id).onchange=draw;
$('download').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify(model.export(settings()),null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='stage2-k4-spray-envelope.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)};
$('h3-before-after').src=D.beforeWebp;$('h3-full').src=D.h3Webp;$('h3-map').src=D.h3MapWebp;
for(let i=0;i<D.results.h3.paths.length;i++){const figure=document.createElement('figure'),img=document.createElement('img'),caption=document.createElement('figcaption');img.src=D.images[D.results.h3.paths[i]];img.alt=`구 생성 ${i+1}`;caption.textContent=`${i+1}/27 · ${i===26?1300:i*50}ms`;figure.append(img,caption);$('keys').append(figure)}
function tick(now){if(previous!==null&&playing)time+=Math.min(now-previous,250);previous=now;draw();requestAnimationFrame(tick)}
labels();draw();requestAnimationFrame(tick);
let loaded=0,failed=0;const total=Object.keys(D.images).length;
for(const [path,uri] of Object.entries(D.images)){const im=new Image();im.onload=()=>{images.set(path,im);loaded++;$('load').textContent=`${loaded}/${total} 이미지 준비 · 분사 크기는 아래에서 조절하세요`;draw()};im.onerror=()=>{failed++;$('error').textContent=`이미지 ${failed}개 읽기 실패: ${path}`};im.src=uri}
