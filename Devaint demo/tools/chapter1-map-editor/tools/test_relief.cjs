// Structural validation of the authored design. This does not simulate Unity.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),box={window:{}};vm.createContext(box);
for(const p of ['data/map.js','data/terrain-rules.js'])vm.runInContext(fs.readFileSync(path.join(root,p),'utf8'),box);
const d=box.window.EXPANDED_MAP,r=box.window.TERRAIN_RULES,W=r.cols,H=r.rows;
const solid=(x,y)=>x<0||x>=W||y<0||y>=H||r.ground[y*W+x]!=='0';
const platforms=d.markers.filter(m=>['platform','dropfloor'].includes(m.kind));
for(const m of platforms)for(let x=m.x/32;x<(m.x+m.w)/32;x++)for(let y=m.y/32-3;y<=m.y/32;y++)assert.ok(!solid(x,y),'Embedded landing '+m.id);
for(const z of d.plan.zones){const x=Math.floor(z.foot[0]/32),y=z.foot[1]/32;assert.ok(solid(x,y)&&!solid(x,y-1)&&!solid(x,y-2),'Invalid preview footing '+z.number);}
assert.equal(d.plan.zones.length,24);assert.equal(d.plan.checks.airComponents.length,1);
const combat=d.plan.zones.filter(z=>z.combatFloor);assert.equal(combat.length,8);for(const z of combat)assert.ok(z.combatFloor.w>=8,'Combat area has insufficient continuous ground');
assert.ok(new Set(d.plan.zones.map(z=>JSON.stringify(z.shape))).size===24);
assert.ok(platforms.length<350,'Excessive shelf density');
assert.ok(platforms.filter(m=>m.w>=10*32).length>=24,'Insufficient broad decks');
for(const z of combat){assert.ok(z.island.w>=14);for(let x=z.island.x;x<z.island.x+z.island.w;x++)assert.ok(solid(x,z.island.y)&&!solid(x,z.island.y-1),'Island top must be solid');}
// Sample 120Hz ballistic arcs from the player locomotion SSOT. This is a
// conservative geometry check, not a substitute for Unity collision simulation.
const motion=JSON.parse(fs.readFileSync(path.resolve(root,'../../data/combat/locomotion/player.json')));
const floor=new Set();for(let y=3;y<H-2;y++)for(let x=2;x<W-2;x++)if(solid(x,y)&&!solid(x,y-1)&&!solid(x,y-2)&&!solid(x,y-3))floor.add(y*W+x);
for(const m of platforms)for(let x=m.x/32;x<(m.x+m.w)/32;x++)if(!solid(x,m.y/32-1)&&!solid(x,m.y/32-2)&&!solid(x,m.y/32-3))floor.add(m.y/32*W+x);
const byX=Array.from({length:W},()=>[]);for(const k of floor)byX[k%W].push(k);
const clear=(x,y)=>{for(let xx=Math.floor(x-.29);xx<=Math.floor(x+.66);xx++)for(let yy=Math.floor(y-2);yy<=Math.floor(y-.015);yy++)if(solid(xx,yy))return false;return true;};
const decks=Array.from({length:H},()=>[]);for(const p of platforms)decks[p.y/32].push([p.x/32,(p.x+p.w)/32]);
const trajectories=new Map();
for(let dy=-5;dy<=22;dy++){
 const arcs=[];
 for(const second of [-2,-1,.12,.24,.30,.40]){
  const samples=[[0,0]];let y=0,v=second===-2?0:-motion.firstJumpVelocity,t=0,jumped=false;
  for(let n=1;n<240;n++){
   if(second>=0&&!jumped&&t>=second){v=-motion.secondJumpVelocity;jumped=true;}
   v+=(v<0?motion.riseGravity:motion.fallGravity)/120;y+=v/120;t=n/120;samples.push([t,y]);
   if(v>0&&y>=dy){
    if(dy<0&&n<2)continue;
    const previous=samples.at(-2),fraction=(dy-previous[1])/(y-previous[1]);
    samples[samples.length-1]=[previous[0]+fraction/120,dy];arcs.push(samples);break;
   }
  }
 }
 trajectories.set(dy,arcs);
}
function arc(a,b){
 const ax=a%W+.5,ay=Math.floor(a/W),bx=b%W+.5,by=Math.floor(b/W),dy=by-ay,dx=bx-ax;
 if(Math.abs(dx)<=1&&dy===0)return clear((ax+bx)/2,ay);
 for(const samples of trajectories.get(dy)||[]){
  const duration=samples.at(-1)[0];if(Math.abs(dx)/duration>motion.runSpeed)continue;
  let valid=true;
  for(let i=1;i<samples.length;i++){
   const [t,y]=samples[i],xx=ax+dx*t/duration;
   if(!clear(xx,ay+y)){valid=false;break;}
   // A downward one-way crossing lands here, rather than passing through it.
   if(y>samples[i-1][1] && i<samples.length-1){
    const yy=ay+y,old=ay+samples[i-1][1];
    for(let row=Math.ceil(Math.max(old,ay+.01)-.001);row<yy-.001;row++)if((decks[row]||[]).some(p=>xx+.66>p[0]&&xx-.29<p[1])){valid=false;break;}
    if(!valid)break;
   }
  }
  if(valid)return true;
 }
 return false;
}
const start=Math.floor(d.plan.spawn[0]/32)+d.plan.spawn[1]/32*W,seen=new Set([start]),queue=[start],reverse=new Map();
for(let i=0;i<queue.length;i++){const a=queue[i],x=a%W;for(let xx=Math.max(2,x-13);xx<=Math.min(W-3,x+13);xx++)for(const b of byX[xx])if(a!==b&&arc(a,b)){
 if(!reverse.has(b))reverse.set(b,[]);reverse.get(b).push(a);if(!seen.has(b)){seen.add(b);queue.push(b);}
}}
const back=new Set([start]),returnQueue=[start];for(let i=0;i<returnQueue.length;i++)for(const a of reverse.get(returnQueue[i])||[])if(!back.has(a)){back.add(a);returnQueue.push(a);}
const unreachable=d.plan.zones.filter(z=>!seen.has(Math.floor(z.foot[0]/32)+z.foot[1]/32*W)).map(z=>({number:z.number,foot:z.foot}));
const noReturn=d.plan.zones.filter(z=>!back.has(Math.floor(z.foot[0]/32)+z.foot[1]/32*W)).map(z=>z.number);
for(const z of combat){const k=z.island.y*W+z.island.x+Math.floor(z.island.w/2);assert.ok(seen.has(k)&&back.has(k),'Island requires a round-trip route '+z.number);}
const report={structuralChecksPassed:true,connectedAir:d.plan.checks.airComponents[0],spaces:24,solidPlatforms:platforms.filter(m=>m.kind==='platform').length,dropPlatforms:platforms.filter(m=>m.kind==='dropfloor').length,standableCells:floor.size,reachableWithPlayerBallistics:seen.size,unreachable,noReturn,floatingIslands:combat.length,longDecks:platforms.filter(m=>m.w>=320).length,unityPhysicsSimulated:false};
fs.mkdirSync(path.resolve(root,'../../outputs/map-design'),{recursive:true});fs.writeFileSync(path.resolve(root,'../../outputs/map-design/relief-validation.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report));assert.equal(unreachable.length,0,'A room has no player-ballistic route from spawn');assert.equal(noReturn.length,0,'A room has no player-ballistic return route');
