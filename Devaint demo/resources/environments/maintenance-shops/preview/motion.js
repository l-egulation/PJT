const data=window.SHOP_LAYOUT,canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d'),status=document.getElementById('status');
let viewIndex=1,ready=false,original=false,paused=false,light=true,swing=true,idle=true,t=0,last=0;
const images={};
const fx=[{lamp:[561,274,20,38],sign:[766,273,52,66],radius:66},{lamp:[483,204,20,38],sign:[781,213,52,66],radius:62}];
function load(file){return new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[file]=im;resolve()};im.onerror=()=>reject(new Error('이미지를 열 수 없습니다: '+file));im.src=file})}
function sprite(key,x,y,w,h){const a=data.assets[key],b=a.bounds;ctx.imageSmoothingEnabled=!a.file.startsWith('native/');ctx.drawImage(images[a.file],...b.slice(0,2),b[2]-b[0],b[3]-b[1],x,y,w,h)}
function effects(){const f=fx[viewIndex];ctx.save();ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';
 const [cx,top,w,h]=f.lamp,cy=top+h*.75;
 if(light){const pulse=1+.10*Math.sin(t*2.4)+.025*Math.sin(t*6.1),r=f.radius*pulse;
 ctx.globalCompositeOperation='screen';const g=ctx.createRadialGradient(cx,cy,2,cx,cy,r);g.addColorStop(0,'rgba(255,183,65,0.48)');g.addColorStop(.38,'rgba(243,147,41,0.19)');g.addColorStop(1,'rgba(236,137,38,0)');ctx.fillStyle=g;ctx.fillRect(cx-r,cy-r,r*2,r*2);ctx.globalCompositeOperation='source-over';
 ctx.fillStyle='#e99c32';ctx.fillRect(cx-w*.23,cy-h*.24,w*.46,h*.39);
 ctx.fillStyle='#fff2ac';const fh=8+2*Math.sin(t*3.2);ctx.fillRect(Math.round(cx-2),Math.round(cy-fh/2),4,Math.round(fh));}
 sprite('lantern',cx-w/2,top,w,h);
 // A small emissive core in front of the dim glass; housing remains fixed.
 if(light){ctx.globalCompositeOperation='screen';ctx.fillStyle='rgba(255,223,120,.85)';ctx.fillRect(cx-3,cy-4,6,9);ctx.fillStyle='#fff4b6';ctx.fillRect(cx-1,cy-3-Math.sin(t*3.2),2,5+Math.sin(t*3.2));}
 ctx.globalCompositeOperation='source-over';const [sx,sy,sw,sh]=f.sign;ctx.translate(sx,sy);ctx.rotate(swing?Math.sin(t*1.35+viewIndex*.6)*.045:0);sprite('sign',-sw/2,0,sw,sh);ctx.restore();}
function draw(){if(!ready)return;const v=data.views[viewIndex];ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,960,540);ctx.drawImage(images[v.capture],0,0);
 if(!original){for(const l of v.layers){if(!document.querySelector('[data-layer="'+l.id+'"]').checked)continue;ctx.imageSmoothingEnabled=l.id!=='npc'&&l.asset!=='player';ctx.imageSmoothingQuality='high';if(l.id==="npc"){drawIdleNpc(ctx,images[data.assets[l.asset].file],data.assets[l.asset].bounds,l,viewIndex,t,idle)}else{sprite(l.asset,l.asset==="player"?dialoguePlayerX(l):l.x,l.y,l.width,l.height);}if(l.asset===v.id+'-backdrop')effects();}}
 drawConversation();document.getElementById('source').textContent=v.name+' · 실제 캡처 위 HTML 연출 미리보기';document.getElementById('png').href=v.id+'-actual-composite.png';document.getElementById('raw').href=v.capture;
 status.textContent=original?'원본 캡처':paused?'연출 일시정지':'NPC 대기 호흡 / 불빛 / 간판 · Unity 적용 전';
 document.getElementById('s1').setAttribute('aria-pressed',viewIndex===0);document.getElementById('s2').setAttribute('aria-pressed',viewIndex===1);}
function frame(now){if(last&&!paused&&!document.hidden)t+=Math.min((now-last)/1000,.05);last=now;draw();requestAnimationFrame(frame)}
document.getElementById('s1').onclick=()=>{viewIndex=0;conversation.mode="off";draw()};document.getElementById('s2').onclick=()=>{viewIndex=1;conversation.mode="off";draw()};
document.getElementById('compare').onclick=()=>{original=!original;document.getElementById('compare').textContent=original?'조립 보기':'원본 캡처만 보기';draw()};
document.getElementById('size').onclick=()=>{const big=canvas.style.width!=='1920px';canvas.style.width=big?'1920px':'960px';canvas.style.height=big?'1080px':'540px';document.getElementById('size').textContent=big?'실제 크기 1×':'정수 확대 2×'};
document.getElementById('pause').onclick=()=>{paused=!paused;document.getElementById('pause').textContent=paused?'연출 재생':'연출 정지'};
document.getElementById('idle').onchange=e=>idle=e.target.checked;document.getElementById('light').onchange=e=>light=e.target.checked;document.getElementById('swing').onchange=e=>swing=e.target.checked;
if(matchMedia('(prefers-reduced-motion: reduce)').matches){paused=true;document.getElementById('pause').textContent='연출 재생'}
Promise.all([document.fonts.load("700 12px GalmuriBold"),...([...new Set([...data.views.map(v=>v.capture),...Object.values(data.assets).map(a=>a.file)])].map(load))]).then(()=>{ready=true;requestAnimationFrame(frame)}).catch(e=>{status.textContent=e.message;status.classList.add('failed')});
