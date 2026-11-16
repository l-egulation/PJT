// Offline artifact/control check; does not claim real-browser QA.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'../../..'),out=path.join(root,'outputs/waterway/k4-oil-motion-20261101');
const html=fs.readFileSync(path.join(out,'index.html'),'utf8'),scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]);assert.equal(scripts.length,2);assert(!html.includes('<script src='));
const els=new Map(),raf=[],boxes=[];class Element{constructor(){this.value='';this.checked=false;this.style={};this.textContent=''}getContext(){return {fillRect(){},save(){},restore(){},translate(){},drawImage(im){assert(im)},strokeRect(...args){boxes.push(args)}}}}
const $=id=>{if(!els.has(id))els.set(id,new Element());return els.get(id)};$('background').value='white';
class LocalImage{set src(uri){this.uri=uri;queueMicrotask(()=>this.onload())}}
const ctx={window:{},document:{getElementById:$},Image:LocalImage,requestAnimationFrame:f=>raf.push(f),console,Map,Math};vm.createContext(ctx);vm.runInContext(scripts[0],ctx);const D=ctx.window.OIL_MOTION;
for(const [p,uri] of Object.entries(D.images)){const b=Buffer.from(uri.split(',')[1],'base64');assert.equal(b.subarray(1,4).toString(),'PNG');if(p!=='map')assert(b.equals(fs.readFileSync(path.join(out,p))));}
vm.runInContext(scripts[1],ctx);
(async()=>{await new Promise(setImmediate);const m=ctx.window.oilMotionModel;assert.equal($('error').textContent,'');assert.equal(m.effectAt(549),null);assert.equal(m.effectAt(550),0);assert.equal(m.effectAt(D.retractStartMs),null);
let cursor=550;for(let i=0;i<D.effectPaths.length;i++){assert.equal(m.effectAt(cursor),i);assert.equal(m.effectAt(cursor+D.effectDurationsMs[i]-.01),i);cursor+=D.effectDurationsMs[i]}
for(const t of [550,1200,1600,1830])assert.equal(m.frameAt(D.bodyDurationsMs,t),9);assert.equal(m.frameAt(D.bodyDurationsMs,1930),10);
$('coverage').checked=true;vm.runInContext('elapsed=750;draw()',ctx);assert(boxes.length>0,'Coverage checkbox must draw the actual oil bounds');
$('background').value='map';$('background').onchange();assert.equal($('scene').width,960);assert.equal($('scene').height,540);
$('scrub').oninput({target:{value:10}});assert.equal($('play').textContent,'재생');$('next').onclick();assert.equal(+$('scrub').value,11);$('prev').onclick();assert.equal(+$('scrub').value,10);$('restart').onclick();assert.equal(+$('scrub').value,0);
assert(Buffer.from(D.h3Webp.split(',')[1],'base64').equals(fs.readFileSync(path.join(root,'outputs/waterway/attack-fx-polish-20261101/h3/review-native.webp'))));
console.log(JSON.stringify({result:'pass',kind:'offline-vm-not-browser-qa',embeddedPngs:Object.keys(D.images).length,oilKeyBoundaries:16,holdUntilOilEnds:true,coverage:true,frameControls:true,map960x540:true,h3Exact:true}));
})().catch(e=>{console.error(e);process.exitCode=1});
