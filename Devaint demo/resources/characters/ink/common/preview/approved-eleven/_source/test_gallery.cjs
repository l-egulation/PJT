// Actual viewer runtime with a deterministic browser clock. No npm dependencies.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'); const D=JSON.parse(fs.readFileSync(path.join(root,'viewer-data.json'),'utf8'));
function el(){return {value:'',checked:false,dataset:{},classList:{toggle(){}},append(){},add(){},setAttribute(){},click(){this.onclick?.({target:this});}};}
async function main(){
 const es={};for(const id of ['weapon','motion','play','reset','seek','guide','effects','speed','background','grid','stage','attack'])es[id]=el();
 es.weapon.value='all';es.motion.value='all';es.speed.value='1';es.background.value='white';es.guide.checked=es.effects.checked=true;
 const buttons=Array.from({length:5},(_,i)=>Object.assign(el(),{dataset:{hit:String(i)}}));
 let drawn=0,keydown;
 class Image{set src(value){assert(value.startsWith('data:image/png;base64,'));const b=Buffer.from(value.split(',')[1],'base64');assert.equal(b.toString('hex',0,8),'89504e470d0a1a0a');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload?.());}}
 const c={D,window:{},document:{getElementById:id=>es[id],querySelectorAll:()=>buttons,createElement:()=>{
  const a=el(),ctx=new Proxy({drawImage(im,...args){assert(im.width>0);assert(args.every(Number.isFinite));drawn++;}},{get:(o,k)=>k in o?o[k]:()=>{}}),canvas=Object.assign(el(),{getContext:()=>ctx}),status=el();
  a.querySelector=s=>s==='canvas'?canvas:status;return a;
 },addEventListener(type,handler){if(type==='keydown')keydown=handler;}},Image,Option:function(text,value){return {text,value};},URLSearchParams,location:{search:''},requestAnimationFrame(){},queueMicrotask};
 vm.createContext(c);const run=s=>vm.runInContext(s,c);
 run(fs.readFileSync(path.join(root,'effects-data.js'),'utf8'));run(fs.readFileSync(path.join(root,'ranged-effects.js'),'utf8'));run(fs.readFileSync(path.join(root,'viewer.js'),'utf8'));
 await new Promise(resolve=>setImmediate(resolve));assert(run('ready'),'all actual PNG files loaded');
 const focus=id=>{es.weapon.value=id;es.weapon.onchange();};
 focus('dagger');es.attack.click();run('tick(0)');for(let i=1;i<=12;i++){es.attack.click();run(`tick(${i*16})`);}
 assert.equal(run('t'),192,'queued clicks must not reset the animation clock');assert(run('actors.dagger.queued')<=1);
 focus('whip');es.attack.click();es.attack.click();for(const t of [0,100,200,225])run(`tick(${t})`);assert.equal(run('actors.whip.current.started'),225);
 focus('whip');es.attack.click();for(const t of [0,100,200,300])run(`tick(${t})`);es.attack.click();assert.equal(run('actors.whip.current.started'),300,'late click must not backdate an attack');
 focus('rapier');for(let i=0;i<3;i++)es.attack.click();for(const t of [0,100,135,220])run(`tick(${t})`);assert.equal(run('actors.rapier.lastHit'),3);assert.equal(run('actors.rapier.current.started'),220);
 focus('shield');es.attack.click();es.attack.click();run('tick(0)');for(let t=100;t<=600;t+=100)run(`tick(${t})`);assert.equal(run('actors.shield.current.started'),600);
 focus('greatsword');es.attack.click();es.attack.click();for(const t of [0,100,200,300,360])run(`tick(${t})`);es.attack.click();for(const t of [460,560,650])run(`tick(${t})`);es.attack.click();for(let t=750;t<=1150;t+=100)run(`tick(${t})`);assert.equal(run('actors.greatsword.current.started'),1150);
 // The previous hit's effect survives a buffered transition.
 focus('rapier');es.attack.click();es.attack.click();for(const t of [0,100,135])run(`tick(${t})`);
 assert(run("activeEffects(D.weapons.find(w=>w.id==='rapier'),0,'attack').some(r=>r.e.hit===1)"));
 // Every motion, hit button, pause/resume, seeking and key path uses real runtime code.
 for(const w of D.weapons){
  focus(w.id);
  for(const key of ['all',...D.order]){es.motion.value=key;es.motion.onchange();for(const t of [0,100,250,500,1500])run(`tick(${t})`);}
  for(let hit=1;hit<=w.hits.length;hit++){buttons[hit].click();run('tick(0)');for(let t=100;t<2000;t+=100)run(`tick(${t})`);assert.equal(run('playing'),false);}
  es.motion.value='idle';es.motion.onchange();es.seek.value='500';es.seek.oninput();assert.equal(run('playing'),false);es.play.click();assert.equal(run('playing'),true);
 }
 focus('sword');keydown({target:{tagName:'BODY'},code:'Space',repeat:false,preventDefault(){}});const queued=run('actors.sword.queued');keydown({target:{tagName:'BODY'},code:'Space',repeat:true,preventDefault(){}});assert.equal(run('actors.sword.queued'),queued);
 es.weapon.value='all';es.weapon.onchange();es.attack.click();assert.equal(run('visibleWeapons().length'),11);
 es.motion.value='all';es.motion.onchange();run('t=sum(D.stages)-50;last=0;tick(100)');assert.equal(run('playing'),false);assert.equal(run('current().key'),'death');
 es.motion.value='single';es.motion.onchange();run('t=100;draw()');assert.equal(run('current().key'),'single');
 fs.writeFileSync(path.join(root,'gallery-validation.json'),JSON.stringify({status:'PASS',weapons:11,allMotions:true,manualChains:true,existingFx:true,singleMode:true,deathHolds:true,browser_tested:false},null,2));
 // Grenade tails survive body recovery and a queued next hit.
 focus('grenade');es.attack.click();es.attack.click();run('t=500;updateManual();draw()');
 assert(run('actors.grenade.history.length===2'));
 assert(run("RangedFX.samples(D.weapons.find(w=>w.id==='grenade'),{key:'attack',fxTime:0,manual:true,selectedHit:0,history:actors.grenade.history,t}).some(s=>s.effects.some(e=>e.part==='frames'))"));
 es.motion.value='single';es.motion.onchange();run('t=650;draw()');
 assert(run("stateFor(D.weapons.find(w=>w.id==='grenade'),'single',650).seq===D.weapons.find(w=>w.id==='grenade').sequences.idle"));
 assert(run("RangedFX.samples(D.weapons.find(w=>w.id==='grenade'),{key:'single',fxTime:650,manual:false,selectedHit:0,history:[],t:650})[0].effects.length>0"));
 assert(run("RangedFX.samples(D.weapons.find(w=>w.id==='grenade'),{key:'idle'}).length===0"));
 assert(drawn>1000);console.log('PASS: 11 weapons / all motions / isolated hits / clock / buffered chains / FX tails / controls');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
