// Entrance cutscene: room entry, flank appearance, argument, bubble capture and the hallucination battlefield.
import {sprite,sheet,bubble,clamp,FLOOR,PLATFORMS,PLAYER_PHASE1,ease,mix,loadArena,playerFrame,headY,ghosts,floor,columnOrder,platform,machine,bars,mountPlayer,showError,say,eyeGlint} from '../arena.mjs';
const ctx=document.querySelector('#stage').getContext('2d');
let R,A,T;
function render(t){const S=T.times,P=T.positions;ctx.imageSmoothingEnabled=false;ctx.drawImage(R.img.room,0,0);
 const span=S.floorEraseEnd-S.floorEraseStart-.25;floor(ctx,R,col=>clamp((t-S.floorEraseStart-columnOrder(col)*span)/.25));
 PLATFORMS.forEach((p,i)=>platform(ctx,R,p,(t-S.platformsStart-i*.08)/.4));machine(ctx,R,(t-S.machineStart)/.45);
 // Player walks along the real floor, stops at centre, then the bubble carries them to the phase 1 start platform.
 let px=P.playerEnterX,py=FLOOR,walking=false;
 if(t>=S.walkStart){px=mix(P.playerEnterX,P.playerStopX,clamp((t-S.walkStart)/(S.walkEnd-S.walkStart)));walking=t<S.walkEnd;}
 const lift=ease((t-S.bubbleFormed)/(S.liftEnd-S.bubbleFormed));if(t>=S.bubbleFormed){px=mix(P.playerStopX,PLAYER_PHASE1.x,lift);py=mix(FLOOR,PLAYER_PHASE1.y,lift)-Math.round(Math.sin(lift*Math.PI)*28);}
 const speaking=T.dialogue.find(d=>t>=d.from&&t<d.to);
 let faceLeft=t>=S.lookRose&&t<S.lookBlue;if(t>=S.lookBlue+.5&&speaking)faceLeft=speaking.speaker==='rose';if(t>=S.castStart&&t<S.bubbleStart)faceLeft=false;
 // Rose on the left facing right, Blue on the right facing left; both flash in and leave before the floor goes.
 const rx=P.roseX+P.roseStepPx*ease((t-S.roseStep)/.35),bx=P.blueX+P.blueStepBackPx*ease((t-S.roseStep-.25)/.3);
 const shy=t>=S.roseStep+.25&&t<S.castStart,flinch=t>=S.blueFlinch&&t<S.blueFlinch+.3?Math.round(Math.sin((t-S.blueFlinch)*48)*2):0;
 const blueId=t>=S.castStart?'blue-cast':'blue-idle',blueAge=t-S.castStart;
 if(t>=S.appear&&t<S.vanish+.2){
  if(t<S.appear+.2){ghosts(ctx,A,'rose-idle',rx,t-S.appear,-1,true,false);ghosts(ctx,A,'blue-idle',bx,t-S.appear,1,false,false);sheet(ctx,A,'blue-vanish',bx,FLOOR,t-S.appear);}
  else if(t>=S.vanish){ghosts(ctx,A,'rose-idle',rx,t-S.vanish,-1,true,true);ghosts(ctx,A,blueId,bx,t-S.vanish,1,false,true);sheet(ctx,A,'blue-vanish',bx,FLOOR,t-S.vanish);}
  else{const raise=t>=S.roseTurn&&t<S.roseTurn+.22?-2:0;sprite(ctx,A,'rose-idle',rx,FLOOR+raise,0,{flip:true});
   sprite(ctx,A,blueId,bx+flinch,FLOOR+(shy?2:0),blueAge,{});
   if(t>=S.castStart+.9)sheet(ctx,A,'ink-charge',bx-34,FLOOR-80,(t-S.castStart)%.5,{scale:.55});}
 }
 if(walking)playerFrame(ctx,R.frames.walk[Math.floor((t-S.walkStart)/.09)%R.frames.walk.length],px,py);else sprite(ctx,A,'player',px,py,0,{flip:faceLeft});
 // Blue-violet magic rises from the feet and closes over the whole body before it lifts, then breaks on landing.
 const d=R.bubbleArt.width,cy=py-34;
 if(t>=S.bubbleStart&&t<S.bubblePop){const grow=clamp((t-S.bubbleStart)/(S.bubbleFormed-S.bubbleStart)),reveal=Math.ceil(d*Math.floor(grow*8)/8);
  if(reveal>0)ctx.drawImage(R.bubbleArt,0,d-reveal,d,reveal,Math.round(px-d/2),Math.round(cy+d/2-reveal),d,reveal);
  if(grow<1){ctx.fillStyle='#a8b2f4';for(let k=0;k<6;k++)ctx.fillRect(Math.round(px-d/2+6+((k*29)%(d-12))),Math.round(py-4-((t*60+k*13)%Math.max(4,reveal))),2,2);}}
 if(t>=S.bubblePop&&t<S.bubblePop+.3){const u=(t-S.bubblePop)/.3;ctx.fillStyle='#b9c2ff';for(let k=0;k<16;k++){const a=k/16*Math.PI*2,r=d/2+u*14;if((k+Math.floor(u*6))%3)ctx.fillRect(Math.round(px+Math.cos(a)*r),Math.round(cy+Math.sin(a)*r),2,2);}}
 // Rose's eye glints after her boast: eye at 11px toward her face and 102px above her feet (rose-idle frame).
 if(t>=S.appear+.2&&t<S.vanish)eyeGlint(ctx,rx-11,FLOOR-102,t-S.roseEyeGlint);
 say(ctx,T,t,d=>d.speaker==='rose'?{x:rx,y:headY(A,'rose-idle',FLOOR)-6}:{x:bx,y:headY(A,blueId,FLOOR)-6});
 // 32px bars slide in on entry and leave only after the bubble breaks.
 bars(ctx,ease((t-S.barsIn)/.38)*(1-ease((t-S.barsOut)/.55)));
}
try{[R,T]=await Promise.all([loadArena(),fetch('timeline.json').then(r=>r.json())]);A=R.A;mountPlayer(T,render,'stage2-intro-dialogue.json',()=>({x:PLAYER_PHASE1.x,y:PLAYER_PHASE1.y}));}catch(e){showError(e);}
