// Independent finite-trajectory geometry check; NOT Unity motor/physics proof.
const fs=require('fs'),path=require('path');
const repo=path.resolve(__dirname,'../..');
const args=process.argv.slice(2);
if(args.includes('--help')){console.log('node validate_ballistics.cjs [--output generated-directory]');process.exit(0);}
let root=path.join(repo,'outputs/village/layout');
if(args.length){if(args.length!==2||args[0]!=='--output')throw new Error('Expected only --output generated-directory');root=path.resolve(args[1]);}
const d=JSON.parse(fs.readFileSync(path.join(root,'VillageWorld.json'))),l=JSON.parse(fs.readFileSync(path.join(root,'village_layout.json')));
const motion=JSON.parse(fs.readFileSync(path.join(repo,'data/combat/locomotion/player.json')));
const W=d.cols,H=d.rows,solid=(x,y)=>x<0||x>=W||y<0||y>=H||d.ground[y*W+x]!==0;
const floor=new Set(),clear=(x,y)=>{for(let xx=Math.floor(x-.29);xx<=Math.floor(x+.66);xx++)for(let yy=Math.floor(y-2);yy<=Math.floor(y-.015);yy++)if(solid(xx,yy))return false;return true;};
for(let y=3;y<H-2;y++)for(let x=2;x<W-2;x++)if(solid(x,y)&&clear(x+.5,y))floor.add(y*W+x);
for(const p of d.platforms)for(let x=Math.ceil(p.x/32);x<Math.floor((p.x+p.w)/32);x++)if(clear(x+.5,p.y/32))floor.add(p.y/32*W+x);
const byX=Array.from({length:W},()=>[]);for(const k of floor)byX[k%W].push(k);
const decks=Array.from({length:H},()=>[]);for(const p of d.platforms)decks[p.y/32].push([p.x/32,(p.x+p.w)/32]);
const trajectories=new Map();
for(let dy=-5;dy<=22;dy++){
 const arcs=[];
 for(const second of [-2,-1,.12,.24,.30,.40]){
  const samples=[[0,0]];let y=0,v=second===-2?0:-motion.firstJumpVelocity,t=0,jumped=false;
  for(let n=1;n<240;n++){
   if(second>=0&&!jumped&&t>=second){v=-motion.secondJumpVelocity;jumped=true;}
   v+=(v<0?motion.riseGravity:motion.fallGravity)/120;y+=v/120;t=n/120;samples.push([t,y]);
   const prev=samples.at(-2);
   // A valid landing must actually cross target altitude downwards. Merely being
   // below an unreachable higher target is not a crossing (and extrapolated to
   // negative time in the old source-inspired check).
   if(v>0&&prev[1]<=dy&&y>=dy){const f=(dy-prev[1])/(y-prev[1]),duration=prev[0]+f/120;if(duration<=0)continue;samples[samples.length-1]=[duration,dy];arcs.push({samples,second});break;}
  }
 }
 trajectories.set(dy,arcs);
}
function arc(a,b){
 const ax=a%W+.5,ay=Math.floor(a/W),bx=b%W+.5,by=Math.floor(b/W),dy=by-ay,dx=bx-ax;
 if(Math.abs(dx)<=1&&dy===0)return clear((ax+bx)/2,ay)?{kind:'walk'}:null;
 for(const {samples,second} of trajectories.get(dy)||[]){
  const duration=samples.at(-1)[0];if(!Number.isFinite(duration)||duration<=0||Math.abs(dx)/duration>motion.runSpeed)continue;
  let valid=true;
  for(let i=1;i<samples.length;i++){
   const [t,y]=samples[i],xx=ax+dx*t/duration;if(!clear(xx,ay+y)){valid=false;break;}
   if(y>samples[i-1][1]&&i<samples.length-1){
    const yy=ay+y,old=ay+samples[i-1][1];
    // Do not skip a platform above the launch floor during descent: the native
    // actor lands on it. Only an intentional drop may ignore its launch deck.
    for(let row=Math.ceil(old-.001);row<=Math.floor(yy+.001);row++){
     if(second===-2&&row===ay&&samples[i-1][1]<=.001)continue;
     if((decks[row]||[]).some(p=>xx+.66>p[0]&&xx-.29<p[1])){valid=false;break;}
    }
    if(!valid)break;
   }
  }
  if(valid)return {kind:second===-2?'drop':second===-1?'jump':'double_jump',duration,axisSpeed:dx/duration,secondJumpTime:second};
 }
 return null;
}
const start=Math.floor(d.spawn.x/32)+d.spawn.y/32*W,seen=new Set([start]),q=[start],reverse=new Map(),prev=new Map();
for(let i=0;i<q.length;i++){
 const a=q[i],x=a%W;
 for(let xx=Math.max(2,x-9);xx<=Math.min(W-3,x+9);xx++)for(const b of byX[xx])if(a!==b){
  const dy=Math.floor(b/W)-Math.floor(a/W);if(dy<-5||dy>22)continue;
  const witness=arc(a,b);if(!witness)continue;
  if(!reverse.has(b))reverse.set(b,[]);reverse.get(b).push(a);
  if(!seen.has(b)){seen.add(b);q.push(b);prev.set(b,{from:a,...witness});}
 }
}
const back=new Set([start]),rq=[start];for(let i=0;i<rq.length;i++)for(const a of reverse.get(rq[i])||[])if(!back.has(a)){back.add(a);rq.push(a);}
const zoneKey=z=>Math.floor(z.x)+z.y*W,unreachable=l.zones.filter(z=>!seen.has(zoneKey(z))).map(z=>z.id),noReturn=l.zones.filter(z=>!back.has(zoneKey(z))).map(z=>z.id);
function routeTo(zone,key){let k=key,steps=[],limit=10000;while(k!==start&&prev.has(k)&&limit--){const p=prev.get(k);steps.push({from:[p.from%W+.5,Math.floor(p.from/W)],to:[k%W+.5,Math.floor(k/W)],kind:p.kind,duration:p.duration,axisSpeed:p.axisSpeed,secondJumpTime:p.secondJumpTime});k=p.from;}return {zone,reachable:k===start,steps:steps.reverse()};}
const routes=l.zones.map(z=>routeTo(z.id,zoneKey(z)));
// The exit is a navigation target, not a 25th landmark/view. Keep all authored
// zone routes unchanged and append the explicit spawn-to-exit witness.
const exitTarget=[475.5,108],exitKey=Math.floor(exitTarget[0])+exitTarget[1]*W;
if(!floor.has(exitKey)||!clear(...exitTarget))throw new Error('Exit target lacks safe body clearance/support.');
routes.push({...routeTo(25,exitKey),name:'exit',target:exitTarget});
const worldSha256=require('crypto').createHash('sha256').update(fs.readFileSync(path.join(root,'VillageWorld.json'))).digest('hex');
const report={unityPhysicsSimulated:false,worldSha256,method:'120Hz finite analytic arcs from unmodified source locomotion; positive-time downward crossing, full-body clearance, all intermediate one-way landings',standableCells:floor.size,reachable:seen.size,roundTrip:back.size,unreachable,noReturn,allZonesReachable:unreachable.length===0,allZonesReturnable:noReturn.length===0,exitReachable:seen.has(exitKey),exitTarget,routeCount:routes.length};
fs.writeFileSync(path.join(root,'ballistic_validation.json'),JSON.stringify(report,null,2));fs.writeFileSync(path.join(root,'ballistic_routes.json'),JSON.stringify(routes));console.log(JSON.stringify(report));
process.exitCode=unreachable.length||noReturn.length||!seen.has(exitKey)?1:0;
