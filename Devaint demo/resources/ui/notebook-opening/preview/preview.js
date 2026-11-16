'use strict';
const canvas=document.querySelector('#scene'),ctx=canvas.getContext('2d');
const $=s=>document.querySelector(s), W=350,H=470, END=9;
const state={time:0,playing:false,speed:1,page:0,turn:-1,ready:false};
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
const smooth=t=>{t=clamp(t);return t*t*(3-2*t)};
const ease=t=>{t=clamp(t);return t<.5?4*t*t*t:1-Math.pow(-2*t+2,3)/2};
const lerp=(a,b,t)=>a+(b-a)*t;
const narrations=[['요즘 마을들이 하나둘 색을 잃고 있다.','색이 빠진 자리에는 연필로 그린 듯한 선만 남는다.','사람들은 이것을 스케치화라고 부른다.'],['스케치화에 휩쓸린 사람은 그 자리에서 굳어 버리고,','지금까지 다시 움직인 사람은 한 명도 없다.','나는 신고가 들어온 마을을 찾아다니며 이 현상을 기록하는 조사원이다.'],['이번 신고는 이 길 끝에 있는 마을에서 들어왔다.','윗마을과 연락이 끊겼다고 한다.']];
let seed=1729;function rand(){seed=(seed*1664525+1013904223)>>>0;return seed/4294967296}
function surface(w,h){const c=document.createElement('canvas');c.width=w;c.height=h;return c}
function load(src){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(new Error('그림을 불러오지 못했어요: '+src));i.src=src})}
let cover,liner,paper,pages=[],written=[],revealPages=[];

// Paper grain and pen strokes live on the page, so they bend with the sheet.
function paperBase(){const c=surface(700,940),g=c.getContext('2d');g.fillStyle='#dfcda6';g.fillRect(0,0,700,940);const wash=g.createRadialGradient(310,400,60,350,470,640);wash.addColorStop(0,'#f5ead1');wash.addColorStop(.65,'#eadcbd');wash.addColorStop(1,'#b79a6d');g.fillStyle=wash;g.fillRect(0,0,700,940);for(let i=0;i<60000;i++){g.fillStyle=rand()>.5?'rgba(72,46,20,.035)':'rgba(255,250,225,.1)';g.fillRect(rand()*700,rand()*940,1+rand()*2,1)}for(let i=0;i<160;i++){g.strokeStyle='rgba(100,77,46,.035)';g.beginPath();let x=rand()*700,y=rand()*940;g.moveTo(x,y);g.lineTo(x+rand()*30,y+rand()*5);g.stroke()}return c}
function script(g,x,y,width,rows,seedOffset=0){seed=199+seedOffset;g.save();g.lineCap='round';g.lineJoin='round';for(let row=0;row<rows;row++){let bx=x+(rand()-.5)*8,by=y+row*24;const length=width*(row%6===5?.65:1);while(bx<x+length){if(rand()<.1){bx+=12;continue}const h=8+rand()*8,w=5+rand()*6;g.strokeStyle=`rgba(65,48,31,${.36+rand()*.29})`;g.lineWidth=.9+rand()*.5;g.beginPath();g.moveTo(bx,by+h*.6);g.bezierCurveTo(bx+w*.4,by-h*.4,bx+w*.8,by+h*1.5,bx+w,by+h*.15);g.stroke();if(rand()>.32){g.beginPath();g.moveTo(bx+2,by+h*.8);g.lineTo(bx+w*.7,by+h*.28);g.lineTo(bx+w+2,by+h*.55);g.stroke()}if(rand()>.65){g.beginPath();g.arc(bx+w*.5,by-2,1,0,7);g.stroke()}bx+=w+4+rand()*3}}g.restore()}
// Centre the drawing itself across the spread, not its canvas: the source art keeps uneven side margins (map.png sits 45px left).
function contentBox(img){const c=surface(img.width,img.height),g=c.getContext('2d');g.drawImage(img,0,0);const d=g.getImageData(0,0,img.width,img.height).data;let x0=img.width,y0=img.height,x1=-1,y1=-1;for(let y=0;y<img.height;y++)for(let x=0;x<img.width;x++)if(d[(y*img.width+x)*4+3]>40){if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y}return x1<0?{x:0,y:0,w:img.width,h:img.height}:{x:x0,y:y0,w:x1-x0+1,h:y1-y0+1}}
function pageTexture(art,index,draft=false){
  const c=surface(1400,940),g=c.getContext('2d');
  g.drawImage(paper,0,0);g.drawImage(paper,700,0);
  script(g,92,83,495,5,index*22);script(g,793,83,490,5,index*22+81);
  script(g,92,753,495,4,index*22+130);script(g,793,753,490,4,index*22+200);
  const binding=g.createLinearGradient(657,0,744,0);
  binding.addColorStop(0,'rgba(80,52,23,0)');binding.addColorStop(.42,'rgba(80,52,23,.2)');
  binding.addColorStop(.5,'rgba(47,30,15,.42)');binding.addColorStop(.6,'rgba(255,247,219,.15)');
  binding.addColorStop(1,'rgba(80,52,23,0)');g.fillStyle=binding;g.fillRect(657,0,87,940);
  if(art){
    // Wash out both the binding and the paper's dark inner edges beneath the illustration.
    // Keep the fade on the paper layer: the drawing itself must stay intact at the fold.
    const wash=surface(600,640),w=wash.getContext('2d');
    const across=w.createLinearGradient(0,0,600,0);
    across.addColorStop(0,'#efe2c500');across.addColorStop(.36,'#efe2c5');
    across.addColorStop(.64,'#efe2c5');across.addColorStop(1,'#efe2c500');
    w.fillStyle=across;w.fillRect(0,0,600,640);
    w.globalCompositeOperation='destination-in';
    const ends=w.createLinearGradient(0,0,0,640);
    ends.addColorStop(0,'#0000');ends.addColorStop(.13,'#000');
    ends.addColorStop(.87,'#000');ends.addColorStop(1,'#0000');
    w.fillStyle=ends;w.fillRect(0,0,600,640);g.drawImage(wash,400,165);
    const maxW=1150,maxH=485,s=Math.min(maxW/art.width,maxH/art.height);
    if(draft){script(g,92,253,495,19,index*22+301);script(g,793,253,490,19,index*22+401)}
    else{const box=contentBox(art);g.globalAlpha=.92;g.drawImage(art,Math.round(700-(box.x+box.w/2)*s),245+(485-art.height*s)/2,art.width*s,art.height*s);g.globalAlpha=1;}
  }
  g.font='italic 18px Georgia';g.fillStyle='#8a7353';g.textAlign='center';
  g.fillText(String(index*2+1).padStart(2,'0'),350,892);g.fillText(String(index*2+2).padStart(2,'0'),1050,892);
  return c;
}
function writtenTexture(index){const c=surface(700,940),g=c.getContext('2d');g.drawImage(paper,0,0);script(g,78,76,545,32,index*53);if(index%2===0){g.strokeStyle='#7d674565';g.lineWidth=2;g.beginPath();g.ellipse(530,720,52,48,-.2,0,7);g.stroke()}return c}
function split(c,side){const r=surface(700,940);r.getContext('2d').drawImage(c,side*700,0,700,940,0,0,700,940);return r}

// Reveal only the illustration band: identical paper and margin writing remain fixed.
// The opaque illustrated paper gently covers the middle writing with a feathered sweep.
function revealedSpread(index,progress){
  const r=revealPages[index],a=smooth(progress);
  if(a>=1)return pages[index];
  if(r.progress===a)return r.halves;
  const g=r.composite.getContext('2d'),m=r.mask.getContext('2d');
  g.clearRect(0,0,1400,940);g.drawImage(r.draft,0,0);
  if(a>0){
    m.clearRect(0,0,1400,940);m.drawImage(r.final,0,0);
    m.save();m.globalCompositeOperation='destination-in';
    const edge=lerp(-420,1820,a),fade=m.createLinearGradient(edge-420,0,edge+420,0);
    fade.addColorStop(0,'#000');fade.addColorStop(1,'#0000');
    m.fillStyle=fade;m.fillRect(0,0,1400,940);m.restore();g.drawImage(r.mask,0,0);
  }
  r.halves.forEach((c,side)=>{const h=c.getContext('2d');h.clearRect(0,0,700,940);h.drawImage(r.composite,side*700,0,700,940,0,0,700,940)});
  r.progress=a;return r.halves;
}

let view={offset:175,tilt:.15,roll:-.045,zoom:1};
function project(x,y,z=0){x-=view.offset;const cr=Math.cos(view.roll),sr=Math.sin(view.roll),xx=x*cr-y*sr,yy=x*sr+y*cr;const ty=yy*Math.cos(view.tilt)-z*Math.sin(view.tilt),tz=yy*Math.sin(view.tilt)+z*Math.cos(view.tilt);const p=1550/(1550-tz);return {x:640+xx*p*view.zoom,y:354+ty*p*view.zoom}}
function path(points){ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath()}
function flatRect(x,y,w,h,z,color){path([project(x,y,z),project(x+w,y,z),project(x+w,y+h,z),project(x,y+h,z)]);ctx.fillStyle=color;ctx.fill()}
function triangle(img,p0,p1,p2,t0,t1,t2){const det=t0.x*(t1.y-t2.y)+t1.x*(t2.y-t0.y)+t2.x*(t0.y-t1.y);if(Math.abs(det)<.001)return;const a=(p0.x*(t1.y-t2.y)+p1.x*(t2.y-t0.y)+p2.x*(t0.y-t1.y))/det,b=(p0.y*(t1.y-t2.y)+p1.y*(t2.y-t0.y)+p2.y*(t0.y-t1.y))/det,c=(p0.x*(t2.x-t1.x)+p1.x*(t0.x-t2.x)+p2.x*(t1.x-t0.x))/det,d=(p0.y*(t2.x-t1.x)+p1.y*(t0.x-t2.x)+p2.y*(t1.x-t0.x))/det,e=(p0.x*(t1.x*t2.y-t2.x*t1.y)+p1.x*(t2.x*t0.y-t0.x*t2.y)+p2.x*(t0.x*t1.y-t1.x*t0.y))/det,f=(p0.y*(t1.x*t2.y-t2.x*t1.y)+p1.y*(t2.x*t0.y-t0.x*t2.y)+p2.y*(t0.x*t1.y-t1.x*t0.y))/det;ctx.save();const center={x:(p0.x+p1.x+p2.x)/3,y:(p0.y+p1.y+p2.y)/3};path([p0,p1,p2].map(p=>({x:p.x+(p.x-center.x)/Math.max(1,Math.hypot(p.x-center.x,p.y-center.y))*1.2,y:p.y+(p.y-center.y)/Math.max(1,Math.hypot(p.x-center.x,p.y-center.y))*1.2})));ctx.clip();ctx.transform(a,b,c,d,e,f);ctx.drawImage(img,0,0);ctx.restore()}
function leaf(tex,angle,z=0,bend=0,backTex=tex,opts={}){const nx=22,ny=2,coords=[],back=angle>Math.PI/2;let x=0,zz=z;for(let i=0;i<=nx;i++){const u=i/nx;if(i){const mid=(i-.5)/nx,phi=angle+bend*Math.sin(Math.PI*mid)*Math.sin(angle);x+=W/nx*Math.cos(phi);zz+=W/nx*Math.sin(phi)}const row=[];for(let j=0;j<=ny;j++){const v=j/ny,y=(v-.5)*H;row.push(project(x,y,zz+Math.sin(v*Math.PI)*Math.sin(angle)*bend*12))}coords.push(row)}path([...coords.map(r=>r[0]),...coords.slice().reverse().map(r=>r[ny])]);ctx.fillStyle=opts.fill&&!back?opts.fill:"#e4d4b4";ctx.fill();const img=back?backTex:tex;for(let i=0;i<nx;i++)for(let j=0;j<ny;j++){let u=i/nx,v=j/ny,un=(i+1)/nx,vn=(j+1)/ny;const tx=q=>(back?1-q:q)*img.width;const p00=coords[i][j],p10=coords[i+1][j],p11=coords[i+1][j+1],p01=coords[i][j+1],t00={x:tx(u),y:v*img.height},t10={x:tx(un),y:v*img.height},t11={x:tx(un),y:vn*img.height},t01={x:tx(u),y:vn*img.height};triangle(img,p00,p10,p11,t00,t10,t11);triangle(img,p00,p11,p01,t00,t11,t01);const shade=Math.sin(angle)*(.06+.18*Math.pow(u,1.3));if(shade>.01){path([p00,p10,p11,p01]);ctx.fillStyle=`rgba(60,37,15,${shade})`;ctx.fill()}}if(opts.edge){ctx.strokeStyle=opts.edge;ctx.lineWidth=1.2;ctx.beginPath();coords.forEach((r,i)=>i?ctx.lineTo(r[ny].x,r[ny].y):ctx.moveTo(r[ny].x,r[ny].y));ctx.stroke()}return coords}
function fixedPage(tex,side,z){return leaf(tex,side?0:Math.PI,z,0,tex)}
function bookShadow(open){ctx.save();const p=project(175*(1-open),40,-25);ctx.translate(p.x,p.y+105);ctx.scale(1,.23);const r=lerp(260,460,open),g=ctx.createRadialGradient(0,0,30,0,0,r);g.addColorStop(0,'#000b');g.addColorStop(.65,'#0007');g.addColorStop(1,'#0000');ctx.fillStyle=g;ctx.fillRect(-r,-r,r*2,r*2);ctx.restore()}
function background(){const g=ctx.createRadialGradient(640,330,70,640,400,740);g.addColorStop(0,'#3a2f20');g.addColorStop(.48,'#211a13');g.addColorStop(1,'#0b0908');ctx.fillStyle=g;ctx.fillRect(0,0,1280,800);ctx.save();ctx.globalAlpha=.17;for(let i=0;i<25;i++){const x=(i*137.2+20)%1280,y=(i*79.3+state.time*3*(.2+i%3))%800;ctx.fillStyle='#ddbf82';ctx.beginPath();ctx.arc(x,y,.4+i%2*.3,0,7);ctx.fill()}ctx.restore()}
function drawCaption(){const start=state.page===0?5.35:state.textAt??state.time;let lines=narrations[state.page];for(let i=0;i<lines.length;i++){const a=smooth((state.time-start-i*.85)/.6);if(a<=0)continue;ctx.save();ctx.globalAlpha=a;ctx.fillStyle='#e7dcc6';ctx.textBaseline='middle';ctx.textAlign='center';ctx.font='20px BookText,sans-serif';const text=lines[i],y=659+i*34+(1-a)*5,idx=text.indexOf('스케치화');if(idx<0)ctx.fillText(text,640,y);else{const before=text.slice(0,idx),after=text.slice(idx+4);const aw=ctx.measureText(before).width,cw=ctx.measureText(after).width;ctx.font='bold 26px BookText,sans-serif';const bw=ctx.measureText('스케치화').width;const x=640-(aw+bw+cw)/2;ctx.textAlign='left';ctx.font='20px BookText,sans-serif';ctx.fillText(before,x,y);ctx.font='bold 26px BookText,sans-serif';ctx.fillStyle='#f6e5be';ctx.fillText('스케치화',x+aw,y-1);ctx.font='20px BookText,sans-serif';ctx.fillStyle='#e7dcc6';ctx.fillText(after,x+aw+bw,y)}ctx.restore()}}
function render(){const box=canvas.getBoundingClientRect(),dpr=Math.min(devicePixelRatio||1,2);if(canvas.width!==Math.round(box.width*dpr)||canvas.height!==Math.round(box.height*dpr)){canvas.width=Math.round(box.width*dpr);canvas.height=Math.round(box.height*dpr)}ctx.setTransform(dpr,0,0,dpr,0,0);ctx.imageSmoothingQuality='high';ctx.fillStyle='#0e0b09';ctx.fillRect(0,0,box.width,box.height);const scale=Math.min(box.width/(box.width<650?900:1120),box.height/800);ctx.translate((box.width-1280*scale)/2,(box.height-800*scale)/2);ctx.scale(scale,scale);background();if(!state.ready)return;const t=state.time,open=ease((t-1.3)/1.65);view.offset=lerp(175,0,smooth((t-1.15)/2.1));view.tilt=lerp(.17,.075,open);view.roll=lerp(-.045,0,open);view.zoom=lerp(1.06,1,open);bookShadow(open);
  // Back board and genuine stacked fore-edge; the thickness remains visible while the front pivots.
  for(let k=12;k>=0;k--){flatRect(-5,-H/2-5,W+12,H+10,-k-3,k>9?'#352619':k%3===0?'#baa078':'#d8c39c')}
  if(open>.999){flatRect(-W-5,-H/2-5,W+6,H+10,-13,'#34261a');flatRect(-W-3,-H/2-2,W,H+5,-11,'#795737');fixedPage(liner,0,-8)}
  const turning=state.turn>=0;
  const revealAt=state.page===0?4.85:state.imageAt;
  const spread=revealedSpread(state.page,(t-revealAt)/1.35),previous=turning?pages[Math.max(0,state.page-1)]:spread;
  const leaves=Array.from({length:7},(_,i)=>({i,u:clamp((t-(2.58+i*.165))/.86)}));
  const intro=t<4.43;
  const backOf=i=>i===6?spread[0]:written[(i+1)%written.length];
  const landed=leaves.filter(sheet=>sheet.u>=1).at(-1);
  // The illustrated spread is already underneath the stack. No whole-page texture swap or crossfade.
  // A sheet's reverse stays on the left when it lands; the final reverse IS the first illustrated left page.
  if(!intro||landed||open>.999)fixedPage(intro?(landed?backOf(landed.i):liner):(turning?previous[0]:spread[0]),0,1);
  fixedPage(spread[1],1,1);
  if(intro){
    const pending=leaves.find(sheet=>sheet.u===0);
    if(pending)fixedPage(written[pending.i%written.length],1,9+pending.i*.45);
  }
  const flying=leaves.filter(sheet=>sheet.u>0&&sheet.u<1);
  flying.sort((a,b)=>Math.sin(a.u*Math.PI)-Math.sin(b.u*Math.PI));
  for(const {u,i} of flying){
    const a=ease(u)*Math.PI;
    ctx.save();ctx.globalAlpha=.13*Math.sin(a);const q=project(Math.cos(a)*W*.5,0,2);
    ctx.filter='blur(10px)';ctx.fillStyle='#251604';ctx.fillRect(q.x-45,q.y-225,90,450);ctx.restore();
    // Settle onto the same plane as the stationary texture, without a final position jump.
    leaf(written[i%written.length],a,lerp(9+i*.45,1,ease(u)),.46,backOf(i),{edge:'#b59d77'});
  }
  if(open<1){const angle=open*Math.PI;const shadow=project(45+140*Math.cos(angle),5,1);ctx.save();ctx.globalAlpha=Math.sin(angle)*.28;ctx.fillStyle='#23170b';ctx.filter='blur(13px)';ctx.fillRect(shadow.x-40,shadow.y-235,110+Math.abs(Math.cos(angle))*190,470);ctx.restore();leaf(cover,angle,13,0,liner,{edge:'#a48250',fill:'#56351e'})}
  if(turning&&state.turn<1){leaf(previous[1],ease(state.turn)*Math.PI,10,.4,spread[0],{edge:'#b59d77'})}
  drawCaption();
  $('#phase').textContent=t<1.3?'제목 없는 조사 수첩':t<2.9?'표지가 열리는 순간':t<4.5?'남겨진 글씨들 사이로':state.page===0?'첫 번째 기록 · 색을 잃은 마을':state.page===1?'두 번째 기록 · 멈춘 사람들':'세 번째 기록 · 이번 신고';
  $('#play').setAttribute('aria-label',state.playing?'일시정지':'재생');$('#seek').value=Math.min(t,END);$('#next').hidden=(state.page===0?t<8.2:t<(state.textAt??END)+(narrations[state.page].length-1)*.85+.6)||state.turn>=0;$('#next').innerHTML=state.page<2?'다음 기록 <span>→</span>':'처음으로 <span>↺</span>';$('#pageNumber').textContent=t>5.2?`${String(state.page+1).padStart(2,'0')} / 03`:'';
}
let audioContext;
function sound(type){if(!audioContext)return;const duration=type==='cover'?.48:.19,buffer=audioContext.createBuffer(1,audioContext.sampleRate*duration,audioContext.sampleRate),data=buffer.getChannelData(0);for(let i=0;i<data.length;i++){const t=i/data.length;data[i]=(Math.random()*2-1)*Math.sin(Math.PI*t)*Math.exp(-t*3)}const source=audioContext.createBufferSource(),filter=audioContext.createBiquadFilter(),gain=audioContext.createGain();source.buffer=buffer;filter.type='bandpass';filter.frequency.value=type==='cover'?650:2100;filter.Q.value=.7;gain.gain.value=type==='cover'?.08:.035;source.connect(filter).connect(gain).connect(audioContext.destination);source.start()}
function play(){if(!state.ready)return;if(!audioContext){try{audioContext=new(window.AudioContext||window.webkitAudioContext)()}catch{}}audioContext?.resume();if(!$('#next').hidden&&state.turn<0){nextPage();return}state.playing=!state.playing;$('#begin').hidden=true;$('#play').textContent=state.playing?'Ⅱ':'▶';$('#play').setAttribute('aria-label',state.playing?'일시정지':'재생')}
function reset(auto=true){Object.assign(state,{time:0,page:0,turn:-1,playing:auto,textAt:undefined});$('#begin').hidden=auto;$('#play').textContent=auto?'Ⅱ':'▶';render()}
function nextPage(){if(state.page===2){reset(true);return}state.page++;state.turn=0;state.imageAt=state.time+1.45;state.textAt=state.imageAt+.5;state.playing=true;$('#play').textContent='Ⅱ';sound('page')}
$('#begin').onclick=play;$('#play').onclick=play;$('#replay').onclick=()=>reset(true);$('#next').onclick=nextPage;
$('#seek').oninput=e=>{state.time=Number(e.target.value);state.page=0;state.turn=-1;state.playing=false;$('#begin').hidden=true;$('#play').textContent='▶';render()};
document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{state.speed=Number(b.dataset.speed);document.querySelectorAll('[data-speed]').forEach(x=>x.classList.toggle('active',x===b))});
document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{state.time=Number(b.dataset.time);state.page=0;state.turn=-1;state.playing=false;$('#begin').hidden=true;$('#play').textContent='▶';render()});
function fullscreen(){const el=$('.theatre');if(document.fullscreenElement)document.exitFullscreen();else if(el.classList.contains('expanded'))el.classList.remove('expanded');else el.classList.add('expanded');dirty=true}$('#fullscreen').onclick=fullscreen;$('#exitFull').onclick=fullscreen;
document.addEventListener('keydown',e=>{if(e.target instanceof HTMLInputElement)return;if(e.code==='Space'){e.preventDefault();if(!$('#next').hidden&&state.turn<0)nextPage();else play()}if(e.code==='KeyR')reset(true);if(e.code==='KeyF')fullscreen();if(e.code==='Escape'&&$('.theatre').classList.contains('expanded'))fullscreen()});
let dirty=true;new ResizeObserver(()=>{dirty=true}).observe(canvas);document.fonts.ready.then(()=>{dirty=true});let last=performance.now(),lastSound=-1;function frame(now){const delta=Math.min((now-last)/1000,.06);last=now;if(state.playing&&state.ready){dirty=true;let old=state.time;state.time+=delta*state.speed;if(old<1.32&&state.time>=1.32)sound('cover');const sheet=Math.floor((state.time-2.58)/.165);if(state.time>=2.58&&state.time<3.7&&sheet!==lastSound){sound('page');lastSound=sheet}if(state.turn>=0){state.turn+=delta*state.speed/1.05;if(state.turn>=1)state.turn=-1}const end=state.page===0?END:(state.textAt??END)+3.3;if(state.time>=end){state.time=end;state.playing=false;$('#play').textContent='▶'}}if(dirty){render();dirty=false}requestAnimationFrame(frame)}requestAnimationFrame(frame);
Promise.all([load('output/imagegen/journal-cover.png'),load('assets/village.png'),load('assets/residents.png'),load('assets/map.png')]).then(images=>{cover=images[0];paper=paperBase();liner=surface(700,940);const g=liner.getContext('2d');g.drawImage(paper,0,0);g.fillStyle='#4b331a18';g.fillRect(0,0,700,940);g.strokeStyle='#96744766';g.lineWidth=2;g.strokeRect(28,29,644,882);written=Array.from({length:5},(_,i)=>writtenTexture(i));pages=images.slice(1).map((im,i)=>{const c=pageTexture(im,i);revealPages.push({final:c,draft:pageTexture(im,i,true),composite:surface(1400,940),mask:surface(1400,940),halves:[surface(700,940),surface(700,940)],progress:-1});return[split(c,0),split(c,1)]});state.ready=true;$('#loading').hidden=true;render()}).catch(error=>{$('#loading').textContent=error.message;console.error(error)});
// Read-only inspection data is useful when checking this standalone preview.
window.bookPreview={state,seek(t){state.time=t;state.playing=false;state.page=0;state.turn=-1;$('#begin').hidden=true;render()},restart:reset};
