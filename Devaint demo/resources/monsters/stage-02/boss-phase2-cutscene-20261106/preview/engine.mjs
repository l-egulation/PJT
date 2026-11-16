// HTML QA port of main 8cc629a15. Pixel coordinates: 32 px = 1 Unity unit.
export const U=32,FLOOR=445,W=960;
export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export const overlap=(a,b)=>a.x<b.x+b.w&&a.x+a.w>b.x&&a.y<b.y+b.h&&a.y+a.h>b.y;
export function dashFraction(t,curve){t=clamp(t,0,1);const hold=curve?.holdFraction??0,power=curve?.easeOutPower??1,area=hold+(1-hold)/power;if(t<=hold)return t/area;return(hold+(1-hold)/power*(1-((1-t)/(1-hold))**power))/area;}
export function arrowOverlaps(s,ox,oy,b){
 const speed=Math.hypot(s.vx,s.vy),ux=speed?s.vx/speed:1,uy=speed?s.vy/speed:0;
 const dx=(s.x+ox-b.x*2-b.w)/2,dy=(s.y+oy-b.y*2-b.h)/2,half=(s.w+Math.hypot(s.x-ox,s.y-oy))/2;
 return [[1,0],[0,1],[ux,uy],[-uy,ux]].every(([x,y])=>Math.abs(dx*x+dy*y)<b.w/2*Math.abs(x)+b.h/2*Math.abs(y)+half*Math.abs(ux*x+uy*y)+s.h/2*Math.abs(-uy*x+ux*y));
}
export class Battle{
 constructor(V,P,random=Math.random){this.V=V;this.P=P;this.random=random;this.mode=0;this.invincible=false;this.reset();}
 reset(){this.t=0;this.damageAt=-Infinity;this.state='ready';this.player={x:480,y:FLOOR,vx:0,vy:0,face:-1,hp:100,ground:true,jumps:0,dashEnd:0,dashReady:0,inv:0,root:0,stun:0,cast:null,combo:0,comboEnd:0,specialReady:0};this.rose={id:'rose',x:220,y:FLOOR,hp:this.V.RoseHp,max:this.V.RoseHp,stun:0,face:1};this.blue={id:'blue',x:740,y:FLOOR,hp:this.V.BlueHp,max:this.V.BlueHp,stun:0,face:-1};this.shots=[];this.effects=[];this.events=[];this.pattern=null;this.lastPattern=0;this.restAt=.8;this.exchange=null;this.waiting=false;this.hits=0;this.serial=0;this.message='2페이즈 전투 시작';this.ko=null;}
 start(){this.reset();this.state='active';this.log('평타 25 · 한쪽 보스 격파 시 흡수 컷씬');}
 log(s){this.message=s;this.events.unshift({time:this.t,text:s});this.events.length=Math.min(8,this.events.length);}
 fx(kind,x,y,extra={}){this.effects.push({kind,x,y,born:this.t,life:.5,...extra});}
 body(){const p=this.player,b=this.P.hurtbox;return {x:p.x+p.face*b.offset_unity_facing_right[0]*U-b.size_unity[0]*U/2,y:p.y-b.size_unity[1]*U,w:b.size_unity[0]*U,h:b.size_unity[1]*U};}
 bossBox(b){return{x:b.x-this.V.BossHurtWidth*U/2,y:b.y-this.V.BossHurtHeight*U,w:this.V.BossHurtWidth*U,h:this.V.BossHurtHeight*U};}
 immune(){return this.invincible||this.t<this.player.inv||this.t<this.player.dashEnd-(this.P.movement.dashDuration-this.P.movement.dashInvulnerability);}
 hurt(n,label,stun=0){if(this.state!=='active'||this.immune())return false;const p=this.player;p.hp=Math.max(0,p.hp-n);p.inv=this.t+.3;p.stun=Math.max(p.stun,this.t+stun);if(stun)p.cast=null;this.hits++;this.fx('damage',p.x,p.y-72,{text:`−${n}`,life:.4});this.damageAt=this.t;this.log(`${label} · ${n} 피해`);if(!p.hp){this.state='dead';this.log('패배 · 다시 시작으로 재시험');}return true;}
 vulnerable(b){return this.state==='active'&&b.hp>0&&!b.hidden&&(!this.exchange||this.exchange.stage==='impact'&&this.exchange.hit===b.id);}
 damageBoss(id,n,force=false){const b=this[id];if(!b.hp||this.state!=='active'||!force&&!this.vulnerable(b))return false;b.hp=Math.max(0,b.hp-n);b.flash=this.t+.09;this.fx('damage',b.x,b.y-120,{text:`−${Math.round(n)}`});if(!b.hp)this.absorb(id);return true;}
 absorb(id){this.ko={branch:id==='rose'?'rose-first':'blue-first',playerX:this.player.x,playerHp:this.player.hp,roseX:this.rose.x,blueX:this.blue.x};this.state='absorb';this.pattern=null;this.exchange=null;this.shots=[];this.effects=[];this.player.cast=null;this.player.root=this.player.stun=0;this.blue.hidden=false;this.log('2페이즈 종료 · 흡수 컷씬');}
 relocate(b,x){this.fx('vanish',b.x,b.y,{boss:b.id});b.x=clamp(x,40,920);b.y=FLOOR;this.fx('vanish',b.x,b.y,{boss:b.id});}
 begin(n){if(this.state!=='active')return;this.lastPattern=n;const V=this.V,p=this.player;
  if(n===3||n===4){const left=this.rose.x<this.blue.x;this.relocate(this.rose,left?740:220);this.relocate(this.blue,left?220:740);}
  if(n===2){const dir=p.x<W/2?1:-1;let x=clamp(p.x-dir*7.1875*U,80,880);if(Math.abs(x-p.x)<4.6875*U)x=clamp(p.x+dir*14.375*U,80,880);this.relocate(this.rose,x);this.fx('vanish',this.blue.x,FLOOR,{boss:'blue'});}
  const c=this.pattern={id:n,start:this.t,done:new Set(),blueHome:this.blue.x,rootX:null};this.restAt=Infinity;
  for(const b of[this.rose,this.blue])b.face=Math.sign(p.x-b.x)||1;
  const body=this.body();c.aim={x:body.x+body.w/2,y:body.y+body.h/2};
  if(n===2){this.blue.hidden=true;this.blue.y=FLOOR-V.BlueDropHeight*U;}
  if(n===3)this.exchange={stage:'tell',start:this.t};
  if(n===4)c.flanks=[this.rose,this.blue].map(b=>({owner:b.id,left:b.x<W/2,hit:false}));
  this.log(['','① 반사 스턴 화살 + 잉크 화염구','② 블루 소멸 → 급강하 속박 + 유도 화살','③ 가속 교환탄 · 평타 4회로 한쪽 파괴','④ 자리 교대 → 화살 울타리 / 잉크 회오리'][n]);return c;
 }
 event(c,key,at,fn){if(this.t-c.start>=at&&!c.done.has(key)){c.done.add(key);fn();}}
 end(){this.pattern=null;this.waiting=true;}
 shoot(kind,b,aim,speed,damage,extra={}){const x=b.x+b.face*18,y=FLOOR-57,a=Math.atan2(aim.y-y,aim.x-x);this.shots.push({id:++this.serial,kind,owner:b.id,x,y,vx:Math.cos(a)*speed*U,vy:Math.sin(a)*speed*U,w:U,h:.3*U,damage,born:this.t,...extra});b.fire=this.t;}
 tickBosses(){const c=this.pattern;if(c?.id!==2)return;const a=this.t-c.start,at=this.V.RootTell-this.V.BlueDropSeconds;
  if(a<at){this.blue.hidden=true;this.blue.x=clamp(this.player.x,this.V.RootWidth*U/2,W-this.V.RootWidth*U/2);}
  else if(a<this.V.RootTell){if(c.rootX===null)c.rootX=clamp(this.player.x,this.V.RootWidth*U/2,W-this.V.RootWidth*U/2);this.blue.x=c.rootX;this.blue.hidden=false;const t=clamp((a-at)/this.V.BlueDropSeconds,0,1);this.blue.y=FLOOR-this.V.BlueDropHeight*U*(1-t*t);}
 }
 tickPattern(){const c=this.pattern,V=this.V;
  if(!c){if(this.exchange)return;if(this.waiting){if(this.shots.length||this.t<Math.max(this.rose.stun,this.blue.stun))return;this.waiting=false;this.restAt=this.t+V.EarlyPhaseRest;}if(this.t>=this.restAt){const ns=[1,2,3,4].filter(n=>n!==this.lastPattern);this.begin(this.mode||ns[Math.floor(this.random()*ns.length)]);}return;}
  const a=this.t-c.start;
  if(c.id===1){this.event(c,'fire',V.StunArrowTell,()=>{this.shoot('stun',this.rose,c.aim,V.StunArrowSpeed,V.StunArrowDamage,{life:V.StunArrowLife,stun:V.StunArrowSeconds,piercing:true,lastHit:-9,inside:false});const b=this.body();this.shoot('ink',this.blue,{x:b.x+b.w/2,y:b.y+b.h/2},V.InkBallSpeed,V.InkBallDamage,{w:V.InkBallSize*U,h:V.InkBallSize*U,life:V.InkBallFuse,unparryable:true});});if(a>=V.StunArrowTell+.02)this.end();}
  if(c.id===2){this.event(c,'impact',V.RootTell,()=>{const x=c.rootX??this.player.x;this.fx('dive-impact',x,FLOOR);const b=this.body(),feet=b.x+b.w/2;
    if(this.player.ground&&Math.abs(feet-x)<V.RootWidth*U/2+b.w/2&&!this.immune()){this.player.root=this.t+V.RootSeconds;this.fx('dive-root',this.player.x,FLOOR,{life:V.RootSeconds});this.log('낙하에 속박! 2초');}
    const aim=this.body();this.shoot('homing',this.rose,{x:aim.x+aim.w/2,y:aim.y+aim.h/2},V.DuoShotSpeed,V.DuoShotDamage,{life:V.DuoShotLife,homingSeconds:V.DuoHomingSeconds,turnRate:V.DuoTurnDegrees*Math.PI/180});this.relocate(this.blue,c.blueHome);this.blue.hidden=false;
   });if(a>=V.RootTell+.02)this.end();}
  if(c.id===3)this.event(c,'flight',V.ExchangeTell,()=>{const e=this.exchange,gap=Math.abs(this.rose.x-this.blue.x)-2*V.ExchangeSpawnOffset*U;e.stage='flight';e.flightAt=this.t;e.baseSpeed=gap/(2*V.ExchangeMeetSeconds*(1+.5*V.ExchangeAcceleration*V.ExchangeMeetSeconds));e.items=[this.rose,this.blue].map(b=>{const target=b.id==='rose'?'blue':'rose',dir=Math.sign(this[target].x-b.x);return{owner:b.id,target,x:b.x+dir*V.ExchangeSpawnOffset*U,y:FLOOR-U,hp:V[b.id==='rose'?'ExchangeRoseHp':'ExchangeBlueHp'],max:V[b.id==='rose'?'ExchangeRoseHp':'ExchangeBlueHp'],impactDamage:V[b.id==='rose'?'ExchangeRoseImpactDamage':'ExchangeBlueImpactDamage'],dir,active:true};});});
  if(c.id===4){if(a>=V.SwapTell&&a<V.SwapAreaEnd)for(const z of c.flanks){const w=V.SwapWidth*U*clamp((a-V.SwapTell)/V.SwapExpandSeconds,0,1),box={x:z.left?0:W-w,y:FLOOR-V.SwapHeight*U,w,h:V.SwapHeight*U};if(!z.hit&&overlap(this.body(),box)&&this.hurt(V.SwapDamage,z.owner==='rose'?'화살 울타리':'잉크 회오리'))z.hit=true;}if(a>=V.SwapAreaEnd){this.fx('press-tail',0,FLOOR,{pattern:c,life:.6});this.end();}}
 }
 tickExchange(dt){const e=this.exchange,V=this.V;if(!e||e.stage==='tell')return;if(e.stage==='impact'){if(this.t>=e.until){this.exchange=null;this.end();}return;}
  const list=e.items.filter(s=>s.active);if(!list.length){this.exchange=null;this.log('교환 탄 모두 파괴 · 무적 해제');this.end();return;}
  if(list.length===1&&!e.finishing){e.finishing=true;e.speed=Math.abs(this[list[0].target].x-list[0].x)/V.ExchangeSurvivorSeconds;this.log('한쪽 파괴 성공 · 남은 탄 0.28초 가속');}
  if(list.length===2)e.speed=e.baseSpeed*(1+V.ExchangeAcceleration*Math.max(0,this.t-e.flightAt));for(const s of list)s.x+=s.dir*e.speed*dt;
  if(list.length===2&&list[0].dir*(list[0].x-list[1].x)>=0){const x=(list[0].x+list[1].x)/2,y=list[0].y,box={x:x-V.ExchangeBlastWidth*U/2,y:y-V.ExchangeBlastHeight*U/2,w:V.ExchangeBlastWidth*U,h:V.ExchangeBlastHeight*U};if(overlap(this.body(),box))this.hurt(V.ExchangeDamage,'교환 탄 충돌');this.fx('exchange-flash',x,y,{scale:1.5,box,life:.96});this.exchange=null;this.end();return;}
  if(list.length===1){const s=list[0],b=this[s.target];if(s.dir*(s.x-b.x)>=0){e.stage='impact';e.hit=b.id;e.until=this.t+V.ExchangeStun;b.stun=e.until;this[s.owner].hp=Math.min(this[s.owner].max,this[s.owner].hp+V.ExchangeHeal);this.fx('ink-blast',b.x,FLOOR-45);this.damageBoss(b.id,s.impactDamage,true);this.log(`반격 적중 · ${b.id} −${s.impactDamage} / 발사자 +${V.ExchangeHeal}`);}}
 }
 hitExchange(box,n,hit){const e=this.exchange;if(e?.stage!=='flight')return;for(const s of e.items){const key='shot'+s.owner;if(s.active&&!hit.has(key)&&overlap(box,{x:s.x-U,y:s.y-U,w:2*U,h:2*U})){hit.add(key);s.hp=Math.max(0,s.hp-n);this.fx('damage',s.x,s.y-43,{text:`−${n}`});if(!s.hp){s.active=false;this.fx('orb-hit',s.x,s.y);this.log(`${s.owner} 투사체 파괴`);}}}}
 attack(special=false){const p=this.player;if(this.state!=='active'||p.cast||this.t<p.stun||this.t<p.dashEnd||special&&this.t<p.specialReady)return;const i=special?0:this.t<p.comboEnd?p.combo:0,d=special?this.P.special:this.P.basics[i];p.cast={special,data:d,index:i,start:this.t,dir:p.face,hit:new Set()};if(special)p.specialReady=this.t+d.cooldown;}
 tickSword(){const p=this.player,c=p.cast;if(!c)return;const a=this.t-c.start,d=c.special?c.data.hits[0]:c.data,b=d.box,at=c.special?d.time:d.impact;c.box={x:p.x+c.dir*b.x*U-b.width*U/2,y:p.y-(b.y+b.height/2)*U,w:b.width*U,h:b.height*U};c.live=a>=at&&a<at+d.active;
  if(c.live){this.hitExchange(c.box,d.damage,c.hit);for(const boss of[this.rose,this.blue])if(!c.hit.has(boss.id)&&overlap(c.box,this.bossBox(boss))&&this.damageBoss(boss.id,d.damage))c.hit.add(boss.id);}
  if(a>=(c.special?c.data.end:c.data.duration)){p.cast=null;if(!c.special){p.combo=(c.index+1)%this.P.basics.length;p.comboEnd=this.t+this.P.lateCombo;}}}
 blocked(){return this.t<this.player.root||this.t<this.player.stun;}
 jump(){const p=this.player;if(this.state!=='active'||this.blocked())return;if(p.jumps<2){p.vy=-this.P.movement.secondJumpVelocity*U;p.jumps++;p.ground=false;p.cast=null;}}
 dash(){const p=this.player;if(this.state!=='active'||this.blocked()||this.t<p.dashReady)return;p.cast=null;p.dashDir=p.face;p.dashStart=this.t;p.dashEnd=this.t+this.P.movement.dashDuration;p.dashReady=p.dashEnd+this.P.movement.dashRecovery;}
 tickPlayer(dt,input){const p=this.player,M=this.P.movement,axis=(input.has('d')||input.has('ArrowRight')?1:0)-(input.has('a')||input.has('ArrowLeft')?1:0),dashing=this.t-dt<p.dashEnd;
  if(axis&&!dashing)p.face=axis;
  if(dashing&&!this.blocked()){const prev=dashFraction((this.t-dt-p.dashStart)/M.dashDuration,M.dashCurve),next=dashFraction((this.t-p.dashStart)/M.dashDuration,M.dashCurve);p.vx=p.dashDir*M.dashSpeed*U*M.dashDuration*(next-prev)/dt;}
  else p.vx=this.blocked()?0:axis*(p.cast?(p.cast.special?this.P.special.inputSpeed:2.5):M.runSpeed)*U;
  p.x=clamp(p.x+p.vx*dt,15,W-15);if(this.t<p.dashEnd&&!this.blocked())p.vy=0;else p.vy+=(p.vy<0?M.riseGravity:M.fallGravity)*U*dt;p.y+=p.vy*dt;if(p.y>=FLOOR){p.y=FLOOR;p.vy=0;p.ground=true;p.jumps=0;}else p.ground=false;}
 explodeInk(s){s.remove=true;const size=this.V.InkBallBlastSize*U;this.fx('ink-blast',s.x,s.y,{life:.8});if(overlap(this.body(),{x:s.x-size/2,y:s.y-size/2,w:size,h:size}))this.hurt(this.V.InkBallDamage,'잉크 화염구 폭발');}
 reflect(s){const ang=Math.atan2(s.vy,s.vx),speed=Math.hypot(s.vx,s.vy),hx=Math.abs(Math.cos(ang))*s.w/2+Math.abs(Math.sin(ang))*s.h/2,hy=Math.abs(Math.sin(ang))*s.w/2+Math.abs(Math.cos(ang))*s.h/2;
  const hitX=s.x<hx&&s.vx<0||s.x>W-hx&&s.vx>0,hitY=s.y<hy&&s.vy<0||s.y>FLOOR-hy&&s.vy>0;if(!hitX&&!hitY)return;
  const tangent=hitX?Math.abs(s.vy):Math.abs(s.vx),normal=hitX?Math.abs(s.vx):Math.abs(s.vy);
  if(hitX)s.vx=-s.vx;if(hitY)s.vy=-s.vy;
  if(!(hitX&&hitY)&&Math.atan2(tangent,normal)<this.V.BounceNormalToleranceDegrees*Math.PI/180){const a=(this.V.BounceRandomMinDegrees+this.random()*(this.V.BounceRandomMaxDegrees-this.V.BounceRandomMinDegrees))*Math.PI/180,sign=this.random()<.5?-1:1;if(hitX){s.vx=Math.sign(s.vx)*Math.cos(a)*speed;s.vy=sign*Math.sin(a)*speed;}else{s.vy=Math.sign(s.vy)*Math.cos(a)*speed;s.vx=sign*Math.sin(a)*speed;}}
  s.x=clamp(s.x,hx,W-hx);s.y=clamp(s.y,hy,FLOOR-hy);
 }
 tickShots(dt){for(const s of this.shots){const age=this.t-s.born;if(s.kind==='ink'&&age>=s.life){this.explodeInk(s);continue;}if(s.kind==='homing'&&!s.reflected&&age<s.homingSeconds){const b=this.body(),aim=Math.atan2(b.y+b.h/2-s.y,b.x+b.w/2-s.x),a=Math.atan2(s.vy,s.vx),delta=Math.atan2(Math.sin(aim-a),Math.cos(aim-a)),next=a+clamp(delta,-s.turnRate*dt,s.turnRate*dt),speed=this.V.DuoShotSpeed*U;s.vx=Math.cos(next)*speed;s.vy=Math.sin(next)*speed;}
   const ox=s.x,oy=s.y;s.x+=s.vx*dt;s.y+=s.vy*dt;const hit=b=>s.kind==='ink'?overlap({x:s.x-s.w/2,y:s.y-s.h/2,w:s.w,h:s.h},b):arrowOverlaps(s,ox,oy,b);
   if(s.kind==='stun')this.reflect(s);
   if(s.reflected){if(hit(this.bossBox(this.rose))&&this.damageBoss('rose',s.damage))s.remove=true;continue;}
   const c=this.player.cast;if(c?.live&&!c.special&&!s.unparryable&&hit(c.box)){s.reflected=true;s.vx=-s.vx;s.vy=-s.vy;this.fx('rose-arrow-impact',s.x,s.y,{scale:1});this.log('패링! 화살 반사');continue;}
   const inside=hit(this.body());if(s.kind==='ink'){if(inside)this.explodeInk(s);}else if(inside){if(s.kind==='stun'){if(!s.inside&&this.t-s.lastHit>=.5&&this.hurt(s.damage,'반사 스턴 화살',s.stun)){s.lastHit=this.t;this.fx('rose-arrow-impact',s.x,s.y,{scale:2});}s.inside=true;}else{if(this.hurt(s.damage,'고속 유도 화살'))this.fx('rose-arrow-impact',s.x,s.y,{scale:2});s.remove=true;}}else s.inside=false;
  }this.shots=this.shots.filter(s=>!s.remove&&this.t-s.born<s.life&&(s.kind==='ink'||s.kind==='stun'||s.x>-64&&s.x<W+64&&s.y>-64&&s.y<FLOOR+64));}
 step(dt,input=new Set()){if(this.state!=='active')return;this.t+=dt;this.tickPlayer(dt,input);this.tickBosses();this.tickSword();if(this.state!=='active')return;this.tickShots(dt);if(this.state!=='active')return;this.tickPattern();if(this.state==='active')this.tickExchange(dt);this.effects=this.effects.filter(e=>this.t-e.born<e.life);}
}
