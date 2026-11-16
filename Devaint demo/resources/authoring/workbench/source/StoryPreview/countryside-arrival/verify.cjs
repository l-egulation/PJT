const assert=require('assert'),fs=require('fs'),vm=require('vm');
const A=require('./scene.js');
for(let t=0;t<15000;t+=5){const s=A.sample(t);assert(s.x>=-45&&s.x<=480);assert(s.frame>=0&&s.frame<({walk:12,prep:3,breath:8}[s.clip]));if(t>=2825)assert.equal(s.x,480);}
for(const t of [450,2450,2825])assert(Math.abs(A.sample(t).x-A.sample(t-1).x)<=1);
assert.equal(A.sample(3095).clip,'breath');assert.equal(A.sample(4465).frame,0);assert.equal(A.sample(899).line,'');assert.equal(A.sample(900).line,'할머니!!!');assert.equal(A.sample(3500).line,'헉… 헉…');
const elements={},calls=[],handlers={};let callback;
const ctx=new Proxy({}, {get:(o,k)=>o[k]||((...args)=>{if(k==='drawImage'){assert(args[0]);calls.push(args);}}),set:(o,k,v)=>(o[k]=v,true)});
function el(id){return elements[id]||(elements[id]={style:{},tagName:'DIV',getContext:()=>ctx,requestFullscreen:async()=>{}});}
const doc={getElementById:el,hidden:false,addEventListener:()=>{},exitFullscreen:async()=>{}};
const sandbox={Arrival:A,Cinematic:require('./transition.js'),document:doc,window:{addEventListener:(k,f)=>handlers[k]=f},performance:{now:()=>1},requestAnimationFrame:f=>callback=f,Image:class{constructor(){this.width=128;this.height=128;}set src(v){assert(v.startsWith('assets/') && fs.existsSync(__dirname+'/'+v));Promise.resolve().then(()=>this.onload());}},innerWidth:1920,innerHeight:1080};
vm.createContext(sandbox);vm.runInContext(fs.readFileSync(__dirname+'/assets.js','utf8'),sandbox);vm.runInContext(fs.readFileSync(__dirname+'/player.js','utf8'),sandbox);
setImmediate(()=>{assert(callback);callback(1);assert.equal(elements.status.textContent,'할머니 기록 확인 직후');const key=k=>handlers.keydown({key:k,preventDefault(){}});key('e');key('e');key('a');for(let t=17;t<10000;t+=16)callback(t);assert.equal(elements.status.textContent,'헐떡임');elements.pause.onclick();let before=calls.length;callback(10001);assert(calls.length>before);elements.replay.onclick();assert.equal(elements.status.textContent,'할머니 기록 확인 직후');assert(calls.some(c=>c.length===3));console.log('PASS: movement, frames, dialogue, record outro → playable library exit → letterbox → village breath; pause/replay.');});
