(async()=>{
 const load=src=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src;});
 const [parry,sphere,rush,ghost]=await Promise.all(['parry.png','sphere.png','rush.png','decoy-native-v2-source.png'].map(load));
 const art={parry,sphere,rush},clips=[];
 // 칸 크기: 기본 256x320, 발밑 (96,280), 한 줄 4칸. 뒤로 길게 뻗는 방패 밀기·터짐은 왼쪽·아래가 잘려(2026-11-08) 넓은 칸을 쓰고,
 // 시트가 Unity 최대 4096px 을 넘지 않게 한 줄 8칸으로 놓는다.
 const CELL={default:{W:256,H:320,X:96,Y:280,C:4},rush:{W:480,H:400,X:288,Y:320,C:8},burst:{W:480,H:512,X:288,Y:320,C:8}};
 let W,H,X,Y,C;
 function use(name){({W,H,X,Y,C}=CELL[name]||CELL.default);}
 function add(name,count,fps,render){use(name);clips.push({name,count,fps,render,cell:{W,H,X,Y,C}});}
 add('basic',12,60,(ctx,fx,i)=>{ctx.save();ctx.translate(X+44,Y-30);ctx.scale(1.6,1.6);shieldBasicContact(ctx,i/60,0,0);ctx.restore();});
 add('parry-empty',27,60,(ctx,fx,i)=>{const t=i/60;fx.parry(t<.30?t*.5:.15+(t-.30)*2,X,Y,1,-1);});
 add('parry-success',14,60,(ctx,fx,i)=>fx.parry(-1,X,Y,1,i/60));
 add('rush',48,60,(ctx,fx,i)=>{fx.rush(i/60,X,Y,1,false);fx.rush(i/60,X,Y,1,true);});
 for(const hp of [30,24,18,12,6])for(const front of [false,true])add('guard-'+hp+'-'+(front?'front':'back'),16,16,(ctx,fx,i)=>fx.guard(.4+i/16,X,Y,1,front,hp,99,-1));
 for(const front of [false,true])add('guard-break-'+(front?'front':'back'),24,60,(ctx,fx,i)=>fx.guard(1+i/60,X,Y,1,front,0,i/60,i/60));
 add('guard-summon',16,60,(ctx,fx,i)=>{fx.guard(.1+i/60,X,Y,1,false,30,99,-1);fx.guard(.1+i/60,X,Y,1,true,30,99,-1);});
 add('guard-hit',14,60,(ctx,fx,i)=>fx.guard(1+i/60,X,Y,1,true,0,i/60,.5));
 add('burst',38,60,(ctx,fx,i)=>fx.burst(i/60,X,Y));
 add('decoy',1,1,(ctx)=>{ctx.save();ctx.translate(X-83,Y-122);ctx.filter='grayscale(.8) brightness(1.15)';ctx.drawImage(ghost,473,494,515,451,59,62,69,60);ctx.filter='none';ctx.globalCompositeOperation='source-atop';ctx.fillStyle='rgba(245,248,255,.22)';ctx.fillRect(0,0,160,160);ctx.restore();});
 window.shieldBake={manifest:clips.map(({name,count,fps,cell})=>({name,count,fps,width:cell.W,height:cell.H,columns:cell.C,pivot:[cell.X/cell.W,(cell.H-cell.Y)/cell.H],ppu:32})),render(name){
  const clip=clips.find(c=>c.name===name);use(name);const sheet=document.createElement('canvas');sheet.width=W*C;sheet.height=H*Math.ceil(clip.count/C);const output=sheet.getContext('2d');
  const cell=document.createElement('canvas');cell.width=W;cell.height=H;const ctx=cell.getContext('2d'),fx=createShieldSimFx(ctx,art);
  for(let i=0;i<clip.count;i++){ctx.clearRect(0,0,W,H);clip.render(ctx,fx,i);output.drawImage(cell,(i%C)*W,Math.floor(i/C)*H);}
  return sheet.toDataURL('image/png').split(',')[1];
 }};
 document.body.dataset.ready='true';
})().catch(e=>{document.body.dataset.error=String(e);console.error(e);});
