const assert=require('assert'),vm=require('vm'),fs=require('fs');
const elements={},events={},keys={};
const classes=()=>{const set=new Set();return {add:s=>set.add(s),remove:s=>set.delete(s),contains:s=>set.has(s),toggle:(s,on)=>on?set.add(s):set.delete(s)};};
const el=id=>elements[id]||(elements[id]={style:{},classList:classes(),getContext:()=>({}),requestFullscreen:async()=>{throw Error('Fullscreen denied by host');}});
const doc={getElementById:el,body:{classList:classes()},fullscreenElement:null,addEventListener:(k,v)=>events[k]=v};
const sandbox={document:doc,window:{addEventListener:(k,v)=>keys[k]=v},innerWidth:1366,innerHeight:768,performance:{now:()=>0},requestAnimationFrame:()=>{},ASSETS:{},Arrival:{sample:()=>({phase:'establish'})}};
vm.createContext(sandbox);vm.runInContext(fs.readFileSync(__dirname+'/player.js','utf8'),sandbox);
(async()=>{
 await elements.fullscreen.onclick();
 assert(elements.stage.classList.contains('screen-mode'),'Host denial must still enable viewport play mode');
 assert(Math.abs(parseFloat(elements.game.style.width)-1365.333333)<.01,'1366x768 should fit at fractional scale instead of staying 960px');
 sandbox.innerWidth=1920;sandbox.innerHeight=1080;keys.resize();assert.equal(parseFloat(elements.game.style.width),1920);
 sandbox.innerWidth=900;sandbox.innerHeight=1000;keys.resize();assert.equal(parseFloat(elements.game.style.width),900);assert.equal(parseFloat(elements.game.style.height),506.25);
 keys.keydown({key:'Escape',preventDefault(){}});assert(!elements.stage.classList.contains('screen-mode'));assert.equal(elements.game.style.width,'960px');
 elements.stage.requestFullscreen=async()=>{doc.fullscreenElement=elements.stage;events.fullscreenchange();};
 await elements.fullscreen.onclick();assert(elements.stage.classList.contains('screen-mode'));
 doc.fullscreenElement=null;events.fullscreenchange();assert(!elements.stage.classList.contains('screen-mode'),'Native Escape must restore normal layout');
 console.log('PASS: denied native fullscreen fallback, 1366x768 / 1920x1080 / portrait fit, Escape, native exit.');
})().catch(e=>{console.error(e);process.exitCode=1;});
