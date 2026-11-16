// Standalone artifact loader/controls check, not real browser QA.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'../../..'),out=path.join(root,'outputs/waterway/attack-fx-polish-20261101');
const html=fs.readFileSync(path.join(out,'index.html'),'utf8');assert(!html.includes('<script src='));
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]);assert.equal(scripts.length,2);
const els=new Map(),raf=[],draws=[];
class Element{constructor(){this.value='';this.checked=false;this.style={};this.textContent='';this.children=[]}append(...items){this.children.push(...items)}getContext(){return {imageSmoothingEnabled:false,fillRect(){},save(){},restore(){},translate(){},setLineDash(){},strokeRect(){},drawImage(im,...args){assert(im);draws.push({im,args})}}}click(){this.onclick?.()}}
for(const [id,value] of Object.entries({length:'128',height:'40','offset-x':'0','offset-y':'0',background:'white'})){const e=new Element();e.value=value;els.set(id,e)}
class LocalImage{set src(uri){assert(uri.startsWith('data:image/png;base64,'));this.uri=uri;queueMicrotask(()=>this.onload())}}
const $=id=>{if(!els.has(id))els.set(id,new Element());return els.get(id)};
const ctx={window:{},document:{getElementById:$,createElement:()=>new Element()},Image:LocalImage,requestAnimationFrame:f=>raf.push(f),console,Map,Math};
vm.createContext(ctx);vm.runInContext(scripts[0],ctx);const D=ctx.window.ATTACK_FX;
for(const [p,uri] of Object.entries(D.images)){const bytes=Buffer.from(uri.split(',')[1],'base64');assert.equal(bytes.subarray(1,4).toString(),'PNG');if(p!=='map')assert(bytes.equals(fs.readFileSync(path.join(out,p))));}
vm.runInContext(scripts[1],ctx);
(async()=>{await new Promise(setImmediate);const m=ctx.window.attackFxModel;
assert.equal($('error').textContent,'');assert($('load').textContent.startsWith('55/55'));
assert.deepEqual(Array.from(m.rect({lengthPx:128,heightPx:40,nozzleOffsetNativePx:[0,0]})),[235,209,128,40]);
for(const [t,on] of [[0,false],[549,false],[550,true],[1199,true],[1200,false],[1900,false]]){assert.equal(m.active(t),on);draws.length=0;vm.runInContext(`time=${t};draw()`,ctx);const fx=draws.filter(r=>D.results.k4.paths.some(p=>D.images[p]===r.im.uri));assert.equal(fx.length,on?1:0);if(on)assert.deepEqual(fx[0].args,[235,209,128,40]);}
$('length').value='200';$('height').value='64';$('offset-x').value='-8';$('offset-y').value='6';$('length').oninput();
const saved=m.export({lengthPx:200,heightPx:64,nozzleOffsetNativePx:[-8,6]});assert.deepEqual(Array.from(saved.worldRectPx),[155,203,200,64]);assert.deepEqual(Array.from(saved.activeWindowMs),[550,1200]);assert.equal(saved.damageOrUnityChanged,false);
$('background').value='map';$('background').onchange();assert.equal($('k4').width,960);assert.equal($('k4').height,540);
$('reset').onclick();assert.equal($('length').value,128);assert.equal($('height').value,40);
$('hold').checked=true;vm.runInContext('time=2000;draw()',ctx);assert($('k4-status').textContent.includes('분사 중'));
$('play').onclick();assert.equal($('play').textContent,'재생');assert.equal($('keys').children.length,27);
for(const [key,filename] of [['beforeWebp','h3/before-after-4x.webp'],['h3Webp','h3/review-native.webp'],['h3MapWebp','h3/map.webp']])assert(Buffer.from(D[key].split(',')[1],'base64').equals(fs.readFileSync(path.join(out,filename))));
console.log(JSON.stringify({result:'pass',kind:'offline-vm-not-browser-qa',embeddedPngs:55,sprayWindow650ms:true,slidersAndExport:true,map960x540:true,h3Keys:27}));
})().catch(e=>{console.error(e);process.exitCode=1});
