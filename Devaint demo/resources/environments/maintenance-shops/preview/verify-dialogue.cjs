const fs=require('fs'),vm=require('vm'),assert=require('assert');const nodes=new Map(),calls=[];
const ctx=new Proxy({measureText:t=>({width:Array.from(t).length*7}),createRadialGradient:()=>({addColorStop(){}})},{get:(o,k)=>o[k]||((...a)=>calls.push([k,...a])),set:(o,k,v)=>(o[k]=v,true)});
function el(id){if(!nodes.has(id))nodes.set(id,{checked:true,style:{},setAttribute(){},classList:{add(){},toggle(){}}});return nodes.get(id)}
const box={console,window:{addEventListener(){}},document:{hidden:false,fonts:{load:()=>Promise.resolve()},querySelector:s=>s==='canvas'?{getContext:()=>ctx,style:{}}:el(s),getElementById:el},Image:class{set src(v){assert(fs.existsSync(v),v);this.onload()}},matchMedia:()=>({matches:false}),requestAnimationFrame(){}};
vm.createContext(box);for(const f of ['layout.js','idle.js','motion.js','dialogue.js'])vm.runInContext(fs.readFileSync(f,'utf8'),box);
setImmediate(()=>{
 vm.runInContext('viewIndex=0;ready=true;t=0;startConversation();t=1.2;draw()',box);assert.equal(vm.runInContext('conversation.mode',box),'talk');
 vm.runInContext('nextDialogue()',box);assert.equal(vm.runInContext('conversation.index',box),0);assert(vm.runInContext('conversation.shown',box));
 vm.runInContext('nextDialogue()',box);assert.equal(vm.runInContext('conversation.index',box),1);
 vm.runInContext('conversation.index=5;conversation.shown=true;nextDialogue()',box);assert.equal(vm.runInContext('conversation.mode',box),'service');
 vm.runInContext('nextDialogue()',box);assert.equal(vm.runInContext('conversation.index',box),6);
 vm.runInContext('conversation.index=9;conversation.shown=true;nextDialogue()',box);assert.equal(vm.runInContext('conversation.mode',box),'done');
 vm.runInContext('viewIndex=1;startConversation();t+=1.2;draw();conversation.index=2;conversation.shown=true;nextDialogue();draw()',box);assert.equal(vm.runInContext('conversation.mode',box),'service');
 vm.runInContext('nextDialogue()',box);assert.equal(vm.runInContext('conversation.mode',box),'done');
 assert(fs.existsSync('assets/fonts/Galmuri11-Bold.ttf'));console.log('PASS: assets/font resolve; approach → talk; first input reveals, next advances; stage1 service → callback → C/V; stage2 short service → end. Mock rendering, no browser visual verification.');
});
