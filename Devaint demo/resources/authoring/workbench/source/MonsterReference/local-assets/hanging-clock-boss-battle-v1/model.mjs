import {mixerCenterY,MIXER_SCALE,MIXER_START,MIXER_DISTANCE} from './mixer-pose.mjs?rev=open-arena-v12';
// Deterministic, renderer-independent encounter. Units below are native screen pixels.
// Open arena (2026-10-29): no side walls; the fixed 30-unit camera edge is the boundary.
export const RULES = Object.freeze({width:960,height:540,left:0,right:960,floor:464,ppu:32,
  fist:{warn:1,extend:.3,hold:.3,retract:.5,recovery:2,wait:2,width:832,height:192,damage:25},
  needle:{count:13,warn:1,interval:.3,fall:.3,fade:.5,width:96,damage:25,recovery:2,wait:2},
  mixer:{warn:1,travel:4.5,return:1,radius:192,distance:MIXER_DISTANCE,tick:.25,recovery:2,wait:2},
  enrageAt:120,deadline:240,enrageSpeed:1.25,
  intro:{dropStart:1.65,impact:2.15,roar:2.6,barsOut:4,end:4.8},
  // Local prototype balance, not a change to the team's design data.
  playerHp:100,bossHp:1040,attackDamage:26,attackCooldown:.38,
  run:240,dashSpeed:720,dashDuration:.2,dashIFrames:.15,dashCooldown:1,
  jumpVelocity:640,riseGravity:2133.333334,fallGravity:3333.333334});
export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export const overlap=(a,b)=>a.x<b.x+b.w&&a.x+a.w>b.x&&a.y<b.y+b.h&&a.y+a.h>b.y;
export function circleRect(c,b){let x=clamp(c.x,b.x,b.x+b.w),y=clamp(c.y,b.y,b.y+b.h);return (x-c.x)**2+(y-c.y)**2<c.r*c.r;}
export class Encounter {
  constructor({seed=21,mode='play',practice=null}={}){this.seed=seed;this.initialSeed=seed;this.reset(mode,practice);}
  random(){this.seed=(1664525*this.seed+1013904223)>>>0;return this.seed/4294967296;}
  reset(mode='play',practice=null){
    this.seed=this.initialSeed;this.mode=mode;this.practice=practice;this.status='intro';this.elapsed=0;this.time=0;this.intro=0;this.endAge=0;this.paused=false;
    this.enraged=false;this.enragePulse=0;this.pattern=null;this.lastPattern=null;this.patternCount=0;this.nextIn=1.5;
    this.player={x:260,y:RULES.floor,vy:0,face:1,hp:RULES.playerHp,jumps:0,dash:0,dashCd:0,dashAge:0,invuln:0,hurt:0,attack:0,attackCd:0,attackId:0,hit:false,walk:0,knock:0,buffer:0};
    this.boss={hp:RULES.bossHp,flash:0};this.needles=[];this.events=[];this.log=[];this.hitstop=0;this.stats={hits:0,taken:0,dodges:0};this.mixerInside=false;this.mixerNext=0;this.mixerAlternate=0;
  }
  emit(type,detail={}){this.events.push({type,...detail,time:this.time});}
  drain(){return this.events.splice(0);}
  bodyBox(){return {x:398,y:222,w:164,h:195};}
  playerBox(){const p=this.player;return {x:p.x-15,y:p.y-64,w:30,h:64};}
  swingBox(){const p=this.player;return {x:p.face>0?p.x+2:p.x-92,y:p.y-76,w:90,h:65};}
  startPattern(type=null,side=null){
    const choices=['fist','needle','mixer'].filter(k=>k!==this.lastPattern);
    type=type||this.practice||choices[Math.floor(this.random()*choices.length)];
    // Preserve the specified timings and range, but never randomly trap a player
    // against the launching wall. Explicit test / authored sides remain available.
    const randomSide=this.random()<.5?'left':'right';
    if(side==null&&type==='fist')side=this.player.x<455?'right':this.player.x>505?'left':randomSide;
    this.pattern={type,age:0,speed:this.enraged?RULES.enrageSpeed:1,side:side??randomSide,spawned:0,hit:false,stage:'warn',impact:false};
    this.lastPattern=type;this.patternCount++;this.mixerInside=false;this.mixerNext=0;
    if(type==='needle'){
      const x=clamp(this.player.x,RULES.left+48,RULES.right-48),cells=Array.from({length:(RULES.right-RULES.left-96)/48+1},(_,i)=>RULES.left+48+i*48);
      for(let i=cells.length-1;i>0;i--){let j=Math.floor(this.random()*(i+1));[cells[i],cells[j]]=[cells[j],cells[i]];}
      this.pattern.positions=[x,...cells.filter(v=>Math.abs(v-x)>60).slice(0,12)];
      while(this.pattern.positions.length<13)this.pattern.positions.push(cells[this.pattern.positions.length%cells.length]);
    }
    this.log.push({type,side:this.pattern.side,speed:this.pattern.speed,at:this.elapsed});
    this.emit('pattern',{pattern:type,side:this.pattern.side});
  }
  fistGeometry(){const p=this.pattern;if(!p||p.type!=='fist')return null;const a=p.age;
    let progress=a<1?0:a<1.3?(a-1)/.3:a<1.6?1:a<2.1?1-(a-1.6)/.5:0;
    progress=clamp(progress,0,1);let w=progress*RULES.fist.width;
    return {x:p.side==='left'?RULES.left:RULES.right-w,y:272,w,h:192,progress,active:a>=1&&a<1.3,side:p.side};
  }
  mixerGeometry(){const p=this.pattern;if(!p||p.type!=='mixer'||p.age<1||p.age>=5.5)return null;
    const progress=clamp((p.age-1)/4.5,0,1);return {x:p.side==='left'?MIXER_START.left+MIXER_DISTANCE*progress:MIXER_START.right-MIXER_DISTANCE*progress,y:mixerCenterY(MIXER_SCALE),r:192};
  }
  sideOffset(side){const p=this.pattern,t=Math.floor(this.time/.05)*.05;let x=0,y=-Math.round(5*(1-Math.cos((t-.15)*Math.PI*2/1.2))/2);
    if(p?.type==='fist'&&p.side===side){const a=p.age,leave=a<.5?clamp(a/.5,0,1):a<2.1?1:1-clamp((a-2.1)/.45,0,1);x=(side==='left'?-1:1)*470*leave;y-=Math.round(30*Math.sin(leave*Math.PI));}
    return {x,y};
  }
  bossHurtboxes(){if(this.status!=='fight')return [];const p=this.pattern;
    if(p?.type==='mixer'&&p.age<6.5){const c=this.mixerGeometry();return c?[{x:c.x-150,y:c.y-150,w:300,h:300}]:[];}
    const result=[this.bodyBox()];
    ['left','right'].forEach(side=>{
      const offset=this.sideOffset(side);
      result.push({x:(side==='left'?320:554)+offset.x,y:293+offset.y,w:86,h:114});
    });
    const g=this.fistGeometry();if(g?.w>0)result.push({x:g.side==='left'?g.x+g.w-150:g.x,y:g.y,w:150,h:192});
    return result;
  }
  damagePlayer(amount,{mixer=false,direction=0}={}){
    const p=this.player;if(this.status!=='fight'||(p.dash>0&&p.dashAge<RULES.dashIFrames)||(!mixer&&p.invuln>0))return false;
    if(this.mode==='demo'){this.stats.dodges++;return false;}
    p.hp=Math.max(0,p.hp-amount);this.stats.taken+=amount;p.hurt=.16;
    if(!mixer){p.invuln=.72;p.knock=direction*960;p.knockTime=.1;this.hitstop=.045;}
    this.emit('player-hit',{x:p.x,y:p.y-30,amount,mixer});return true;
  }
  damageBoss(amount){if(this.status!=='fight')return;this.boss.hp=Math.max(0,this.boss.hp-amount);this.boss.flash=.075;this.stats.hits++;this.emit('boss-hit',{x:this.player.x+this.player.face*45,y:this.player.y-44,amount});this.hitstop=.035;}
  stun(){if(this.status!=='fight'||this.pattern?.type==='mixer')return false;this.pattern=null;this.nextIn=2/(this.enraged?1.25:1);this.emit('stun');return true;}
  enterFight(){this.status='fight';this.emit('fight');}
  forcePattern(type,side){if(this.status==='intro')this.enterFight();this.needles=[];this.startPattern(type,side);}
  update(dt,input={}){
    if(this.paused||dt<=0)return;dt=Math.min(dt,.05);this.time+=dt;
    if(this.status==='intro'){
      const before=this.intro;this.intro+=dt;
      if(before<RULES.intro.impact&&this.intro>=RULES.intro.impact)this.emit('intro-impact');
      if(before<RULES.intro.roar&&this.intro>=RULES.intro.roar)this.emit('intro-roar');
      if(this.intro>=RULES.intro.end)this.enterFight();return;
    }
    if(this.status==='victory'||this.status==='defeat'){this.endAge+=dt;return;}
    // Hit-stop is presentation and simulation pause, including the encounter clock.
    if(this.hitstop>0){this.hitstop=Math.max(0,this.hitstop-dt);return;}
    this.elapsed+=dt;this.enragePulse=Math.max(0,this.enragePulse-dt);this.boss.flash=Math.max(0,this.boss.flash-dt);
    if(!this.enraged&&this.elapsed>=120){this.enraged=true;this.enragePulse=2;this.emit('enrage');}
    if(this.mode==='demo')input=this.demoInput(dt);
    this.updatePlayer(dt,input);this.updateNeedles(dt);this.updatePattern(dt);
    if(this.player.attack>0&&this.player.attack<.23&&!this.player.hit){
      if(this.bossHurtboxes().some(b=>overlap(b,this.swingBox()))){this.player.hit=true;this.damageBoss(RULES.attackDamage);}
    }
    // Boss death wins ties with the deadline or the player's final hit.
    if(this.boss.hp<=0){this.status='victory';this.endAge=0;this.pattern=null;this.needles=[];this.emit('victory');}
    else if(this.player.hp<=0||this.elapsed>=240){this.status='defeat';this.endAge=0;this.pattern=null;this.needles=[];if(this.elapsed>=240)this.player.hp=0;this.emit('defeat',{timeout:this.elapsed>=240});}
  }
  updatePlayer(dt,i){const p=this.player;
    for(const k of ['invuln','hurt','attack','attackCd','dashCd','buffer'])p[k]=Math.max(0,p[k]-dt);
    let move=clamp(i.move||0,-1,1);if(move)p.face=move;
    if(i.jump)p.buffer=.08;
    if(p.buffer>0&&p.jumps<2){p.jumps++;p.vy=-640;p.buffer=0;p.dash=0;p.dashAge=0;this.emit('jump',{x:p.x,y:p.y});}
    if(i.dash&&p.dashCd<=0){p.dash=.2;p.dashAge=0;p.dashCd=1;p.vy=0;this.emit('dash',{x:p.x,y:p.y,face:p.face});}
    if(p.dash>0){const step=Math.min(dt,p.dash);p.x+=p.face*720*step;p.dashAge+=step;p.dash=Math.max(0,p.dash-dt);p.vy=0;
      if(p.x<RULES.left+15||p.x>RULES.right-15){p.dash=0;p.dashAge=.2;}
    } else {p.x+=move*240*dt;p.vy+=(p.vy<0?RULES.riseGravity:RULES.fallGravity)*dt;p.y+=p.vy*dt;}
    if(p.knockTime>0){p.x+=p.knock*Math.min(dt,p.knockTime);p.knockTime=Math.max(0,p.knockTime-dt);}else p.knock=0;
    p.x=clamp(p.x,RULES.left+15,RULES.right-15);if(p.y>=464){if(p.vy>150)this.emit('land',{x:p.x,y:464});p.y=464;p.vy=0;p.jumps=0;}if(p.y<80){p.y=80;p.vy=0;}
    if(move)p.walk+=dt;
    if(i.attack&&p.attackCd<=0&&p.dash<=0){p.attack=.32;p.attackCd=RULES.attackCooldown;p.attackId++;p.hit=false;this.emit('swing',{x:p.x+p.face*30,y:p.y-40,face:p.face});}
  }
  updatePattern(dt){const p=this.pattern;if(!p){this.nextIn-=dt;if(this.nextIn<=0)this.startPattern();return;}
    const old=p.age;p.age+=dt*p.speed;let total=0,stage='warn';
    if(p.type==='fist'){
      total=6.1;stage=p.age<1?'warn':p.age<1.3?'strike':p.age<1.6?'hold':p.age<2.1?'retract':p.age<4.1?'recovery':'wait';
      if(old<1&&p.age>=1)this.emit('fist-launch',{side:p.side});
      if(old<1.3&&p.age>=1.3)this.emit('fist-stop',{side:p.side,x:p.side==='left'?RULES.left+RULES.fist.width:RULES.right-RULES.fist.width,y:464});
      // Swept advancing rectangle catches fast motion even if the final step crosses extend-end.
      if(old<1.3&&p.age>=1&&!p.hit){let a=Math.min(p.age,1.3),w=RULES.fist.width*clamp((a-1)/.3,0,1);const b={x:p.side==='left'?RULES.left:RULES.right-w,y:272,w,h:192};
        if(overlap(b,this.playerBox())&&this.damagePlayer(25,{direction:p.side==='left'?1:-1}))p.hit=true;
      }
    }else if(p.type==='needle'){
      // 13 * staggered starts: 12*.3 + 1 warning + .3 fall = 4.9.
      // Keep the explicitly requested full warning; the document's 4.6 subtotal conflicts.
      total=8.9;stage=p.age<1?'warn':p.age<4.9?'strike':p.age<6.9?'recovery':'wait';
      while(p.spawned<13&&p.age>=p.spawned*.3){const k=p.spawned++,x=p.positions[k];this.needles.push({id:this.patternCount*100+k,x,age:p.age-k*.3,speed:p.speed,hit:false,landed:false});this.emit('needle-warn',{x,index:k});}
    }else{
      total=10.5;stage=p.age<1?'warn':p.age<5.5?'strike':p.age<6.5?'return':p.age<8.5?'recovery':'wait';
      if(old<1&&p.age>=1)this.emit('mixer-appear',{side:p.side});
      if(old<5.5&&p.age>=5.5)this.emit('mixer-end');
      const c=this.mixerGeometry(),inside=c&&circleRect(c,this.playerBox());
      if(inside){this.mixerNext-=dt;if(!this.mixerInside||this.mixerNext<=0){this.damagePlayer(this.mixerAlternate++%2?8:7,{mixer:true});this.mixerNext=.25/p.speed;}}else this.mixerNext=0;
      this.mixerInside=!!inside;
    }
    if(stage!==p.stage){p.stage=stage;this.emit('stage',{stage,pattern:p.type});}
    if(p.age>=total){this.pattern=null;this.nextIn=0;}
  }
  updateNeedles(dt){for(const n of this.needles){const before=n.age;n.age+=dt*n.speed;
    if(before<1&&n.age>=1)this.emit('needle-drop',{x:n.x});
    if(n.age>=1&&before<1.3&&!n.hit){let tip=40+424*clamp((Math.min(n.age,1.3)-1)/.3,0,1),oldTip=40+424*clamp((Math.max(before,1)-1)/.3,0,1);
      const b={x:n.x-48,y:oldTip-128,w:96,h:tip-oldTip+128};if(overlap(b,this.playerBox())&&this.damagePlayer(25,{direction:this.player.x>n.x?1:-1}))n.hit=true;}
    if(n.age>=1.3&&!n.landed){n.landed=true;this.emit('needle-impact',{x:n.x,y:464});}
  }this.needles=this.needles.filter(n=>n.age<1.8);}
  demoInput(){const p=this.player,q=this.pattern;let target=480,attack=true,dash=false,jump=false;
    if(q?.type==='fist'&&q.age<2.1){target=q.side==='left'?RULES.right-64:RULES.left+64;attack=q.age>1.3;dash=Math.abs(target-p.x)>130&&p.dashCd<=0;}
    else if(q?.type==='mixer'&&q.age<6.5){target=q.side==='left'?RULES.right-48:RULES.left+48;attack=false;}
    else if(q?.type==='needle'&&q.age<4.9){const danger=this.needles.filter(n=>n.age>.3&&n.age<1.3);let best=p.x,cost=1e9;
      for(let x=RULES.left+32;x<=RULES.right-32;x+=32){let score=Math.abs(x-p.x)*.1+Math.abs(x-480)*.02;for(const n of danger)if(Math.abs(x-n.x)<76)score+=500;if(score<cost){best=x;cost=score;}}target=best;
      dash=this.needles.some(n=>n.age>.8&&n.age<1.3&&Math.abs(n.x-p.x)<75)&&p.dashCd<=0;
    }
    return {move:Math.abs(target-p.x)<8?0:Math.sign(target-p.x),attack,dash,jump};
  }
  snapshot(){return {status:this.status,elapsed:this.elapsed,player:{...this.player},boss:{...this.boss},pattern:this.pattern?{...this.pattern}:null,needles:this.needles.length,enraged:this.enraged,stats:{...this.stats}};}
}
