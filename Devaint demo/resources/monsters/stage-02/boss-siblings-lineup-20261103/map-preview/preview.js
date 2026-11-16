'use strict';
const cv=document.querySelector('#view'),ctx=cv.getContext('2d'),q=s=>document.querySelector(s);
const names=['흡수 전 Blue','흡수 전 Rose','흡수 후 Blue'];
let figures=[{name:'승인 플레이어',x:145,foot:438,height:64},...names.map((name,i)=>({name,x:[350,555,790][i],foot:438,height:[139,120,179][i]}))];
const initial=JSON.stringify(figures),images={},sprites=[],boxes=[];
const load=src=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>reject(Error(src+' 로드 실패'));im.src=src});
function bounds(im,x0=0,x1=im.width){const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const cc=c.getContext('2d');cc.drawImage(im,0,0);const d=cc.getImageData(0,0,c.width,c.height).data;let l=x1,t=im.height,r=x0,b=0;for(let y=0;y<im.height;y++)for(let x=x0;x<x1;x++)if(d[(y*im.width+x)*4+3]>128){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y)}return {im,sx:l,sy:t,sw:r-l+1,sh:b-t+1}}
function controls(){q('#controls').innerHTML=figures.slice(1).map((f,i)=>'<fieldset><legend>'+f.name+'</legend><label>몸 높이 <output id="value'+i+'">'+f.height+'px</output><input aria-label="'+f.name+' 몸 높이" type="range" min="64" max="256" step="1" value="'+f.height+'" data-id="'+(i+1)+'"></label><span class="hint" id="ratio'+i+'"></span></fieldset>').join('');document.querySelectorAll('[data-id]').forEach(el=>el.oninput=()=>{figures[+el.dataset.id].height=+el.value;draw()})}
function draw(){if(!sprites.length)return;ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,960,540);if(q('#scene').value==='white'){ctx.fillStyle='#fff';ctx.fillRect(0,0,960,540)}else ctx.drawImage(images[q('#scene').value],0,0,960,540);
for(let x=0;x<960;x+=32)ctx.drawImage(images.terrain,32,289,32,47,x,438,32,47);
boxes.length=0;figures.forEach((f,i)=>{const s=sprites[i],scale=f.height/s.bodyH,w=Math.round(s.sw*scale),h=Math.round(s.sh*scale),x=Math.round(f.x-w/2),y=Math.round(f.foot-h);ctx.drawImage(s.im,s.sx,s.sy,s.sw,s.sh,x,y,w,h);boxes.push({x,y,w,h});if(q('#guide').checked){ctx.font='13px sans-serif';ctx.textAlign='center';const text=f.name+' · '+f.height+'px';ctx.fillStyle='rgba(10,18,28,.88)';ctx.fillRect(f.x-84,f.foot+53,168,25);ctx.fillStyle='#edf2f8';ctx.fillText(text,f.x,f.foot+70)}if(i){q('#value'+(i-1)).textContent=f.height+'px';q('#ratio'+(i-1)).textContent=(f.height/64).toFixed(2)+' × 플레이어 · '+(f.height/32).toFixed(2)+'u 참고'}});
if(q('#fg').checked)ctx.drawImage(images.front,490,-100);
try{localStorage.setItem('boss-size-review-v1',JSON.stringify(figures))}catch{}}
function download(data,name){const a=document.createElement('a');a.href=data;a.download=name;a.click()}
q('#save').onclick=()=>{const data={schema:'stage2-boss-size-review-v1',width:960,height:540,scene:q('#scene').value,monsters:figures,bodyHeightExcludesFloatingCrossbow:true};const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));download(url,'stage2-boss-sizes.json');setTimeout(()=>URL.revokeObjectURL(url),1000)};
q('#png').onclick=()=>download(cv.toDataURL('image/png'),'stage2-boss-map-preview.png');
q('#reset').onclick=()=>{figures=JSON.parse(initial);controls();draw()};
['scene','fg','guide'].forEach(id=>q('#'+id).onchange=draw);q('#zoom').onchange=()=>{if(q('#zoom').value==='fit'){cv.style.width='100%';cv.style.height='auto';return}cv.style.width=(960*+q('#zoom').value)+'px';cv.style.height=(540*+q('#zoom').value)+'px'};
document.querySelectorAll('[data-preset]').forEach(btn=>btn.onclick=()=>{const h={small:[125,108,156],middle:[132,114,166],large:[139,120,179]}[btn.dataset.preset];figures.slice(1).forEach((f,i)=>f.height=h[i]);controls();draw()});
let dragging=-1,offset=[0,0];const point=e=>{const r=cv.getBoundingClientRect();return[(e.clientX-r.left)*960/r.width,(e.clientY-r.top)*540/r.height]};
cv.onpointerdown=e=>{const [x,y]=point(e);dragging=boxes.findLastIndex(b=>x>=b.x&&x<=b.x+b.w&&y>=b.y&&y<=b.y+b.h);if(dragging>=0){offset=[x-figures[dragging].x,y-figures[dragging].foot];cv.setPointerCapture(e.pointerId)}};
cv.onpointermove=e=>{if(dragging<0)return;const [x,y]=point(e);figures[dragging].x=Math.round(x-offset[0]);figures[dragging].foot=Math.round(y-offset[1]);draw()};cv.onpointerup=()=>dragging=-1;
(async()=>{try{for(const zone of ['A','B','C','D','E'])images[zone]=await load('assets/'+zone+'.png');images.terrain=await load('assets/terrain-preview.png');images.front=await load('assets/foreground.png');const player=await load('assets/approved-player.png'),board=await load('assets/bosses-subtle-v4-transparent.png');const p=bounds(player);p.bodyH=p.sh;sprites.push(p);
const ranges=[[0,.29],[.29,.65],[.65,1]];ranges.forEach((range,i)=>{const s=bounds(board,Math.floor(board.width*range[0]),Math.floor(board.width*range[1]));s.bodyH=i===2?Math.round(board.height*.775):s.sh;sprites.push(s)});
controls();draw();q('#status').textContent='선택본 v4 · 사용자 저장 크기 적용 완료 · 플레이어 캔버스 '+player.width+'×'+player.height+' / 실제 그림 '+p.sw+'×'+p.sh+'px · 세 보스 개별 크기 조절 가능';
}catch(e){q('#status').textContent=e.message;console.error(e)}})();




