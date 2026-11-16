(function(root){
 const GATE=4314;
 class Journey {
 constructor(){this.reset();}
 reset(){Object.assign(this,{x:180,y:448,vy:0,facing:1,hp:3,phase:'fight',scene:1,timer:0,time:0,camera:0,cameraY:0,dust:[],swapped:false,jumpQueued:false,grateHp:3,grateFlash:0,grateBreaking:0,attackCooldown:0});}
 floor(x=this.x){return this.scene===2?448:(x<3258?448:x<3386?480:x<3546?512:544);}
 ceiling(x=this.x){if(this.scene===1&&x>=960&&x<1920)return 256;if(this.scene===2||x<3600)return -1000;let t=Math.max(0,Math.min(1,(x-3600)/560));return 184+248*t*t*(3-2*t);}
 defeat(){if(this.phase!=='fight')return;this.hp=0;this.phase='breaking';this.timer=0;for(let i=0;i<100;i++)this.dust.push({x:960+Math.random()*32,y:302+Math.random()*146,vx:-40-Math.random()*120,vy:-80+Math.random()*110,life:1+Math.random()*1.8,size:3+Math.random()*13,rock:i%5===0});}
 attack(){if(this.attackCooldown>0)return;this.attackCooldown=.18;
 if(this.phase==='fight'&&Math.abs(this.x-480)<160){if(--this.hp<=0)this.defeat();}
 else if(this.scene===1&&this.phase!=='fight'&&this.phase!=='breaking'&&this.grateHp>0&&this.facing===1&&GATE-this.x<125&&Math.abs(this.y-this.floor())<75){
 this.grateHp--;this.grateFlash=.13;
 for(let n=0;n<14;n++)this.dust.push({x:GATE-8,y:402+Math.random()*130,vx:-110+Math.random()*220,vy:-100-Math.random()*100,life:.3+Math.random()*.5,size:2+Math.random()*5,rock:true});
 if(this.grateHp===0)this.grateBreaking=.65;
 }}
 preview(x){this.defeat();this.dust=[];this.phase='open';this.x=x;this.y=this.floor();this.camera=x<=960?0:Math.max(0,x-432);this.cameraY=this.y-448;}
 step(dt,i={}){if(dt===0){if(i.jump)this.jumpQueued=true;return;}dt=Math.min(dt,.05);this.time+=dt;this.timer+=dt;this.attackCooldown=Math.max(0,this.attackCooldown-dt);this.grateFlash=Math.max(0,this.grateFlash-dt);this.grateBreaking=Math.max(0,this.grateBreaking-dt);
 if(this.phase==='breaking'&&this.timer>=1.8){this.phase='open';this.timer=0;}
 if(this.phase==='entering'){
  if(this.timer>=.4&&!this.swapped){this.scene=2;this.swapped=true;this.x=180;this.y=448;this.vy=0;this.camera=0;this.cameraY=0;}
  if(this.timer>=.8){this.phase='sewer';this.timer=0;}
 }else{
 const dir=(i.right?1:0)-(i.left?1:0);if(dir)this.facing=dir;
 const previous=this.x,feet=this.y,ground=this.floor();
 if((i.jump||this.jumpQueued)&&Math.abs(this.y-ground)<.1)this.vy=-370;this.jumpQueued=false;
 const limit=this.scene===2?1840:(this.phase==='fight'||this.phase==='breaking'?940:(this.grateHp>0||this.grateBreaking>0?GATE-45:GATE));
 let next=Math.max(46,Math.min(limit,this.x+dir*220*dt));if(this.floor(next)<feet-1)next=previous;this.x=next;
 this.vy+=1100*dt;this.y=Math.min(this.floor(),this.y+this.vy*dt);if(this.y-64<this.ceiling()){this.y=this.ceiling()+64;if(this.vy<0)this.vy=0;}if(this.y===this.floor())this.vy=0;

 if(this.scene===1&&this.x>=GATE&&this.grateHp===0&&this.grateBreaking===0){this.phase='entering';this.timer=0;}
 const goal=this.scene===1&&this.x<=960?0:this.scene===2?Math.min(960,Math.max(0,this.x-432)):Math.min(3530,Math.max(0,this.x-432));
 this.camera+=(goal-this.camera)*(1-Math.exp(-6*dt));this.cameraY+=((this.scene===1?Math.max(0,this.floor()-448):0)-this.cameraY)*(1-Math.exp(-3*dt));
 }
 for(const p of this.dust){p.x+=p.vx*dt;p.y+=p.vy*dt;p.vy+=(p.rock?280:-9)*dt;p.life-=dt;}this.dust=this.dust.filter(p=>p.life>0&&p.y<980);
 }
 }
 Journey.GATE=GATE;if(typeof module!=='undefined')module.exports=Journey;else root.Journey=Journey;
})(typeof window!=='undefined'?window:globalThis);
