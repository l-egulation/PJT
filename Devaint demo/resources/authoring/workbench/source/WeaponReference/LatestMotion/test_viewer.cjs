const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const path=require('node:path');
const script=fs.readFileSync(path.join(__dirname,'viewer.js'),'utf8');
let nextFrame,clock=0,drawCalls=0;
class Element {
 constructor(tag='div'){this.tagName=tag;this.children=[];this.textContent='';this.value='';this.checked=true;this.attrs={};}
 append(...items){this.children.push(...items);}
 setAttribute(k,v){this.attrs[k]=v;}
 getContext(){const canvas=this;return {clearRect(){},fillRect(){},drawImage(image){assert(image&&image.width>0);canvas.lastImage=image.path;drawCalls++;},save(){},restore(){},translate(){},scale(){}};}
}
const controls=Object.fromEntries(['motion','play','speed','effects','stage','weapons','status'].map(k=>[k,new Element()]));
controls.motion.value='auto';controls.speed.value='1';controls.stage.textContent='대기';
const root={querySelector(selector){const key=selector.match(/data-control="([^"]+)"/)[1];assert(controls[key]);return controls[key];}};
class AssetImage {set src(src){const bytes=fs.readFileSync(path.join(__dirname,src));this.path=src;this.width=bytes.readUInt32BE(16);this.height=bytes.readUInt32BE(20);this.onload();}}
const context={document:{getElementById(id){assert.equal(id,'ink-four-motion-review');return root;},createElement:tag=>new Element(tag),documentElement:{}},window:{INK_MOTION_DATA:JSON.parse(fs.readFileSync(path.join(__dirname,'data.json'),'utf8')),addEventListener(){}},Image:AssetImage,MutationObserver:class{observe(){}},getComputedStyle:()=>({getPropertyValue:()=> '#808080'}),requestAnimationFrame:callback=>{nextFrame=callback;},console};
vm.runInNewContext(script,context,{timeout:5000});
(async()=>{
 await new Promise(setImmediate);assert.equal(controls.weapons.children.length,4);assert(nextFrame);assert(!controls.status.textContent.includes('못했습니다'));
 function tick(delta=50){clock+=delta;const cb=nextFrame;assert(cb);cb(clock);}
 tick(0);const stages=[];
 for(let i=0;i<500;i++){tick();if(stages.at(-1)!==controls.stage.textContent)stages.push(controls.stage.textContent);}
 assert.deepEqual(stages,['대기','걷기','달리기','점프','낙하·착지','대시','공격 연속','죽음']);
 assert.equal(controls.motion.value,'death');
 assert(controls.weapons.children.every(fig=>fig.children[0].children[1].textContent==='사망 유지'));
 controls.motion.value='death';controls.motion.onchange();
 const weapons=Object.values(context.window.INK_MOTION_DATA.weapons);
 const death=weapons.map(w=>w.modules.filter(m=>m.id.startsWith('death')).flatMap(m=>m.steps));
 assert(death.every(steps=>steps.length===12&&steps.reduce((n,s)=>n+s.ms,0)===800));
 for(let n=0;n<12;n++){
  for(let k=0;k<4;k++)assert.equal(controls.weapons.children[k].children[1].lastImage,weapons[k].frames[death[k][n].frame]);
  if(n<11)tick(death[0][n].ms);
 }
 tick(100);tick(80);
 assert(controls.weapons.children.every(fig=>fig.children[0].children[1].textContent==='사망 유지'));
 controls.motion.value='combo';controls.motion.onchange();assert.equal(controls.stage.textContent,'공격 연속');tick(100);tick(40);
 let before=drawCalls;controls.effects.checked=false;controls.effects.onchange();const off=drawCalls-before;
 before=drawCalls;controls.effects.checked=true;controls.effects.onchange();const on=drawCalls-before;
 assert.equal(off,4);assert(on>off);
 controls.play.onclick();assert.equal(controls.play.textContent,'재생');controls.play.onclick();assert.equal(controls.play.textContent,'일시정지');
 controls.speed.value='0.5';controls.speed.onchange();tick();
 const result={weapons:4,automatic_stages:stages,death_hold:true,death_12_pose_boundaries_verified:true,death_duration_ms:800,effect_toggle_draw_calls:{off,on},play_pause:true,decoded_images:true};
 fs.writeFileSync(path.join(__dirname,'viewer-runtime-verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1;});
