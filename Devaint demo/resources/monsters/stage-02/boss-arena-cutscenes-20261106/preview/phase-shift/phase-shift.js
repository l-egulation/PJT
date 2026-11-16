// Phase 1 to 2 cutscene: the machine breaks, the hallucination platforms go, the stone floor returns, the player falls
// from their current X and lands, then Rose and Blue appear at the floor ends (phase2 markers) and argue.
import {sprite,sheet,bubble,clamp,FLOOR,PLATFORMS,MACHINE,ROSE_PHASE2,BLUE_PHASE2,ease,loadArena,query,playerFrame,headY,ghosts,floor,columnOrder,platform,machine,bars,mountPlayer,showError,say} from '../arena.mjs';
const ctx=document.querySelector('#stage').getContext('2d');
let R,A,T;
// Inside the full boss flow the phase 1 battle hands over the player's X, Y and HP and where the machine broke.
const num=k=>query.has(k)?Number(query.get(k)):null,START={x:num('x'),y:num('y'),hp:num('hp')??100},AT=num('mx')!==null?{x:num('mx'),y:num('my')}:MACHINE;
// Small unscaled pieces of the machine thrown out by the blast; they fall and drop out in steps.
function fragments(t){const age=t-T.times.machineBlast;if(age<0||age>.9)return;const im=R.img.machine,ox=AT.x-30,oy=AT.y-80;
 for(let y=0;y<80;y+=6)for(let x=0;x<61;x+=6){const k=x*13+y*7,vx=((x-30)/30)*(140+(k%9)*22),vy=-(120+(k%7)*40),gx=ox+x+vx*age,gy=oy+y+vy*age+700*age*age;
  const life=.45+(k%5)*.1;if(age>life)continue;ctx.globalAlpha=age>life-.15?.5:1;ctx.drawImage(im,x,y,6,6,Math.round(gx),Math.round(gy),6,6);}
 ctx.globalAlpha=1;}
function render(t){const S=T.times,P=T.positions;ctx.imageSmoothingEnabled=false;ctx.drawImage(R.img.room,0,0);
 // The floor is missing during phase 1 and returns column by column.
 const span=S.floorRestoreEnd-S.floorRestoreStart-.25;floor(ctx,R,col=>1-clamp((t-S.floorRestoreStart-columnOrder(col)*span)/.25));
 const goneAt=i=>S.platformsGone+i*.06;PLATFORMS.forEach((p,i)=>platform(ctx,R,p,1-(t-goneAt(i))/.3));
 if(t<S.machineBlast){const shake=Math.round(Math.sin(t*90)*Math.min(3,1+t*4));sheet(ctx,A,'machine-warn',AT.x,AT.y,t%0.48);machine(ctx,R,1,shake,AT);}
 fragments(t);sheet(ctx,A,'machine-ink-blast',AT.x,AT.y-40,t-S.machineBlast);
 // Player keeps their X and falls naturally once the platform under them is gone.
 const px=START.x??P.playerX,fromY=START.y!==null?Math.min(START.y,FLOOR):PLATFORMS[P.playerPlatform].y;
 const fall=goneAt(P.playerPlatform)+.18,drop=Math.max(0,FLOOR-fromY),land=fall+Math.sqrt(2*drop/P.fallGravity);
 const speaking=T.dialogue.find(d=>t>=d.from&&t<d.to);
 let faceLeft=t>=S.lookRose&&t<S.lookBlue;if(t>=S.lookBlue+.3&&speaking)faceLeft=speaking.speaker==='rose';
 if(t<fall)sprite(ctx,A,'player',px,fromY,0,{});
 else if(t<land){const y=fromY+.5*P.fallGravity*(t-fall)**2;playerFrame(ctx,R.frames.jump[3+Math.floor((t-fall)/.09)%2],px,y);}
 else if(t<land+.36)playerFrame(ctx,R.frames.land[Math.min(5,Math.floor((t-land)/.06))],px,FLOOR);
 else sprite(ctx,A,'player',px,FLOOR,0,{flip:faceLeft});
 if(drop>0&&t>=land&&t<land+.3)sheet(ctx,A,'dive-impact',px,FLOOR,t-land,{scale:.45});
 // Siblings at the floor ends: Rose left facing right, Blue right facing left. Blue shrinks once Rose takes over and stays that way.
 const rx=ROSE_PHASE2.x,bx=BLUE_PHASE2.x;
 if(t>=S.appear){if(t<S.appear+.2){ghosts(ctx,A,'rose-idle',rx,t-S.appear,-1,true,false);ghosts(ctx,A,'blue-idle',bx,t-S.appear,1,false,false);sheet(ctx,A,'blue-vanish',bx,FLOOR,t-S.appear);}
  else{const raise=t>=S.roseRaise&&t<S.roseRaise+.22?-2:0,shy=t>=S.roseRaise+.2;
   sprite(ctx,A,'rose-idle',rx,FLOOR+raise,0,{flip:true});sprite(ctx,A,'blue-idle',bx,FLOOR+(shy?2:0),0,{});}}
 say(ctx,T,t,d=>d.speaker==='rose'?{x:rx,y:headY(A,'rose-idle',FLOOR)-6}:{x:bx,y:headY(A,'blue-idle',FLOOR)-6});
 bars(ctx,ease((t-S.barsIn)/.38)*(1-ease((t-S.barsOut)/.55)));
}
try{[R,T]=await Promise.all([loadArena(),fetch('timeline.json').then(r=>r.json())]);A=R.A;
 mountPlayer(T,render,'stage2-phase-shift-dialogue.json',()=>({x:START.x??T.positions.playerX,hp:START.hp}));}catch(e){showError(e);}
