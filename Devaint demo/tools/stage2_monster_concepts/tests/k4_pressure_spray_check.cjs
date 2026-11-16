// Offline control/data checks for the standalone pressure spray artifact.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'../../..'),out=path.join(root,'outputs/waterway/k4-pressure-spray-20261101');
const html=fs.readFileSync(path.join(out,'index.html'),'utf8'),scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]);assert.equal(scripts.length,2);assert(!html.includes('<script src='));
const els=new Map(),raf=[],draws=[];class Element{constructor(){this.value='';this.checked=false;this.style={};this.textContent=''}getContext(){return {fillRect(){},save(){},restore(){},translate(){},strokeRect(){},drawImage(im,...a){assert(im);draws.push({im,a})}}}}
const $=id=>{if(!els.has(id))els.set(id,new Element());return els.get(id)};$('background').value='white';
class LocalImage{set src(uri){this.uri=uri;queueMicrotask(()=>this.onload())}}
const ctx={window:{},document:{getElementById:$},Image:LocalImage,requestAnimationFrame:f=>raf.push(f),console,Map,Math};vm.createContext(ctx);vm.runInContext(scripts[0],ctx);const D=ctx.window.PRESSURE_SPRAY;
for(const [p,uri] of Object.entries(D.images)){const bytes=Buffer.from(uri.split(',')[1],'base64');assert.equal(bytes.subarray(1,4).toString(),'PNG');if(p!=='map')assert(bytes.equals(fs.readFileSync(path.join(out,p))));}
vm.runInContext(scripts[1],ctx);
(async()=>{await new Promise(setImmediate);const m=ctx.window.pressureSprayModel;
assert.equal($('error').textContent,'');assert.equal(m.effectAt(549),null);assert.equal(m.effectAt(550),0);assert.equal(m.effectAt(D.retractStartMs),null);
for(const t of [600,900,1200,D.retractStartMs-50,D.retractStartMs,D.motionMs]){vm.runInContext(`time=${t};draw()`,ctx);assert($('phase').textContent.includes(m.phase(t)));const k=m.frameAt(D.bodyDurationsMs,t);if(t>=550&&t<D.retractStartMs)assert.equal(k,9);if(t>=D.retractStartMs)assert(k>=10)}
assert.equal(m.coverageAt(D.retractStartMs),null);assert(m.coverageAt(850));
$('background').value='map';$('background').onchange();assert.equal($('scene').width,960);assert.equal($('scene').height,540);
$('scrub').oninput({target:{value:1300}});assert.equal($('play').textContent,'재생');$('next').onclick();assert.equal(+$('scrub').value,1350);$('prev').onclick();assert.equal(+$('scrub').value,1300);$('restart').onclick();assert.equal(+$('scrub').value,0);
assert(Buffer.from(D.h3Webp.split(',')[1],'base64').equals(fs.readFileSync(path.join(root,'outputs/waterway/attack-fx-polish-20261101/h3/review-native.webp'))));
console.log(JSON.stringify({result:'pass',kind:'offline-vm-not-browser-qa',embeddedPngs:Object.keys(D.images).length,holdUntilDrain:true,seekAndMap:true,h3ApprovedClipExact:true}));
})().catch(e=>{console.error(e);process.exitCode=1});
