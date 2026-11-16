const canvas=document.querySelector('#stage'),ctx=canvas.getContext('2d');
const ui=Object.fromEntries(['play','restart','speed','ghosts','loop','scrub','status'].map(k=>[k,document.getElementById(k)]));
let images=[],playing=true,clock=0,last=performance.now();
const ppu=80,startX=270,ground=374,scale=ppu/128;
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
// Preview-only burst: most travel happens immediately, followed by a short brake.
// This does not edit the game's movement definition or its collision handling.
const position=t=>{const u=clamp((t-CONFIG.activation)/(CONFIG.dashEnd-CONFIG.activation),0,1);return startX+CONFIG.distance*ppu*(1-Math.pow(1-u,4));};
function frameIndex(t){for(let i=CONFIG.frames.length-1;i>=0;i--)if(t*1000>=CONFIG.frames[i].startMs)return i;return 0;}
function sprite(index,x,alpha=1){ctx.save();ctx.globalAlpha=alpha;ctx.drawImage(images[index],x-384*scale,ground-604*scale,896*scale,768*scale);ctx.restore();}
function render(){
 const t=Math.min(clock,CONFIG.end);ctx.clearRect(0,0,1280,520);ctx.fillStyle='#242c37';ctx.fillRect(0,0,1280,520);
 ctx.strokeStyle='#323d4a';ctx.lineWidth=1;
 for(let x=startX%ppu;x<1280;x+=ppu){ctx.beginPath();ctx.moveTo(x,75);ctx.lineTo(x,ground+35);ctx.stroke();}
 ctx.strokeStyle='#7e91a8';ctx.beginPath();ctx.moveTo(40,ground);ctx.lineTo(1240,ground);ctx.stroke();
 ctx.font='16px system-ui';ctx.textAlign='center';ctx.fillStyle='#a8bbd0';
 for(const [x,txt] of [[startX,'시작'],[startX+CONFIG.distance*ppu,'6유닛 / 도착']]){ctx.beginPath();ctx.moveTo(x,ground-5);ctx.lineTo(x,ground+12);ctx.stroke();ctx.fillText(txt,x,ground+39);}
 if(ui.ghosts.checked){for(let n=0;n<9;n++){const birth=CONFIG.activation+n*.015,age=t-birth;if(age>=0&&age<.13&&birth<CONFIG.dashEnd)sprite(4,position(birth),.34*Math.pow(1-age/.13,1.5));}}
 sprite(frameIndex(t),position(t));
 const phase=t<.15?'준비':t<.35?'찌르기 유지 · 돌진':t<.6?'정지 · 복귀':'완료';
 ctx.fillStyle='#d6e7f5';ctx.textAlign='left';ctx.font='20px system-ui';ctx.fillText(phase,38,42);
 ctx.font='16px system-ui';ctx.fillStyle='#f3bd59';ctx.fillText(`이동 거리 ${((position(t)-startX)/ppu).toFixed(2)} / 6.00유닛`,38,72);
 ui.scrub.value=Math.round(t*1000);ui.status.textContent=`${t.toFixed(3)}초 / 0.600초 · ${phase}`;
}
function tick(now){const dt=Math.min((now-last)/1000,.05);last=now;if(playing){clock+=dt*Number(ui.speed.value);if(clock>=1.6&&ui.loop.checked)clock=0;else if(clock>=.6&&!ui.loop.checked){clock=.6;playing=false;ui.play.textContent='재생';}}render();requestAnimationFrame(tick);}
ui.play.onclick=()=>{playing=!playing;if(playing&&clock>=.6&&!ui.loop.checked)clock=0;ui.play.textContent=playing?'일시정지':'재생';};
ui.restart.onclick=()=>{clock=0;playing=true;ui.play.textContent='일시정지';};
ui.scrub.oninput=()=>{playing=false;clock=Number(ui.scrub.value)/1000;ui.play.textContent='재생';render();};
Promise.all(CONFIG.images.map(src=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src;}))).then(loaded=>{images=loaded;last=performance.now();window.previewReady=true;requestAnimationFrame(tick);}).catch(()=>{ui.status.textContent='이미지 로드 실패';});
