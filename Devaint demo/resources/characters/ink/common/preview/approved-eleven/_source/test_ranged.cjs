const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),D=JSON.parse(fs.readFileSync(path.join(root,'effects-data.json'),'utf8'));
const els={},make=()=>({value:'',checked:false,hidden:false,textContent:'',children:[],routes:{},classList:{toggle(){}},setAttribute(){},append(x){this.children.push(x);},querySelector(s){return this.routes[s]||(this.routes[s]=make());}});
for(const id of ['weapon','mode','play','replay','speed','repeat','effects','background','seek','status','weapons'])els[id]=make();
els.weapon.value='all';els.mode.value='single';els.speed.value='1';els.repeat.checked=els.effects.checked=true;els.background.value='white';
let draws=0;const ctx=new Proxy({drawImage(im,...args){assert(im.width>0);assert(args.every(Number.isFinite));draws++;}},{get:(o,k)=>o[k]||(()=>{})});
function element(){const el=make();el.querySelector=s=>{if(!el.routes[s]){el.routes[s]=make();if(s==='canvas')el.routes[s].getContext=()=>ctx;}return el.routes[s];};return el;}
class Image{set src(s){assert(s.startsWith('data:image/png;base64,'));const b=Buffer.from(s.split(',')[1],'base64');assert.equal(b.toString('hex',0,8),'89504e470d0a1a0a');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload?.());}}
const box={FXD:D,window:{},Image,localStorage:{getItem:()=>null,setItem(){}},document:{getElementById:id=>els[id],createElement:element,addEventListener(){}},requestAnimationFrame(){},console,queueMicrotask};
vm.createContext(box);const run=s=>vm.runInContext(s,box);
async function main(){
 run(fs.readFileSync(path.join(root,'effects-viewer.js'),'utf8'));
 await new Promise(r=>setImmediate(r));assert(run('ready'));assert.equal(run('cards.length'),4);
 const api=box.window.fxAPI;
 for(const w of D.weapons){
  assert.equal(w.variants.length,1);assert.equal(w.fixedVariant,w.variants[0].id);
  run(`choose('${w.id}','C')`);assert.equal(run(`selections.${w.id}`),w.fixedVariant);
  for(const mode of ['single','burst']){
   els.mode.value=mode;const ev=api.events(w,mode),v=w.variants[0];assert.equal(ev.length,w.id==='laser'||mode==='single'?1:3);
   for(let t=-350;t<2600;t+=10){
    const s=api.sample(w,v,{t,laserAge:t-ev[0].start,laserHeld:w.id==='laser'&&t>=ev[0].start&&t<ev[0].end},mode);
    assert(w.frames[s.body]);for(const e of s.effects){assert(e.f>=0&&e.f<6);assert(Number.isFinite(e.x)&&Number.isFinite(e.y));}
    for(const p of s.projectiles)assert(p.x>=6&&p.x<=474&&p.y>=6&&p.y<=214);
    if(w.id==='laser'&&s.beams.length){assert.equal(s.beams[0].width,2);assert.equal(s.beams[0].end,480);}
    if(w.id==='shotgun'){assert.equal(s.projectiles.length,0);for(const e of s.effects){const m=w.muzzles[s.body]||w.muzzles[w.fireFrames[mode]];assert.equal(e.x,m[0]+12);assert.equal(e.y,m[1]+50);}}
    if(w.id==='grenade'&&t<ev[0].start+380)assert(!s.effects.some(e=>e.part==='frames'));
    api.setTime(t+350);
   }
  }
 }
 els.mode.value='single';api.reset();api.fire('laser');api.setTime(5000);
 const laser=D.weapons.find(w=>w.id==='laser');let s=api.sample(laser,laser.variants[0],api.clockFor(laser),'single');
 assert.equal(s.body,'attack_02');assert.equal(s.beams.length,1);api.release('laser');assert.equal(api.sample(laser,laser.variants[0],api.clockFor(laser),'single').beams.length,0);
 api.setTime(5100);assert(api.sample(laser,laser.variants[0],api.clockFor(laser),'single').body.startsWith('대기_'));
 for(const bg of ['auto','white','dark','checker']){els.background.value=bg;api.draw();}
 const samples={weapons:D.weapons.map(w=>{const v=w.variants[0],e=api.events(w,'single')[0],t=e.start+(w.id==='grenade'?480:w.id==='shotgun'?60:w.id==='cannon'?70:135);return {id:w.id,sample:api.sample(w,v,{t,laserAge:t-e.start,laserHeld:w.id==='laser'},'single')};})};
 fs.writeFileSync(path.join(root,'_source/approved-effect-samples.json'),JSON.stringify(samples));
 fs.writeFileSync(path.join(root,'ranged-validation.json'),JSON.stringify({status:'PASS',weapons:4,allSelectionsLocked:true,laserHoldAt5Seconds:true,shotgunMuzzleLocked:true,grenadeFlightAndImpact:true,browserTested:false},null,2));
 assert(draws>1000);console.log('PASS: 4 accepted effects / laser hold / muzzle sync / grenade flight and impact');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
