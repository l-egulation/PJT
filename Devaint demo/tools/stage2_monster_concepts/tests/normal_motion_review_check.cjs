// Offline loader/control regression checks. This is not browser QA.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const out=path.resolve(process.argv[2]||'outputs/waterway/normal-motion-review-v2-20261101');
const viewer=process.argv[3]&&process.argv[3]!=='--inline'?process.argv[3]:path.join(__dirname,'../normal_motion_review.js');
const D=JSON.parse(fs.readFileSync(path.join(out,'manifest.json'),'utf8'));
let source=fs.readFileSync(viewer,'utf8');
if(process.argv[3]==='--inline'){
  const html=fs.readFileSync(path.join(out,'review-inline.html'),'utf8');
  assert(!/<script\s+src=/i.test(html),'Standalone HTML must not load external scripts');
  const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]);
  assert.equal(scripts.length,2);source=scripts[1];
  const dataContext={window:{}};vm.runInNewContext(scripts[0],dataContext);assert.deepEqual(dataContext.window.MOTION_DATA,D);
}
const allPaths=[...new Set([D.mapPath,D.playerPath,...D.monsters.flatMap(m=>m.clips.flatMap(c=>c.paths))])];
function harness({fail=new Set(),defer=new Set()}={}){
  const elements=new Map(),created=[],raf=[],pending=[],timers=new Map();let timerId=0;
  class Element{
    constructor(tag,id){Object.assign(this,{tag,id,style:{},children:[],value:'',textContent:'',attrs:{},draws:0});created.push(this)}
    append(...nodes){this.children.push(...nodes);if(this.id==='monster'&&!this.value)this.value=nodes[0].value}
    setAttribute(k,v){this.attrs[k]=v}scrollIntoView(){}
    getContext(){const canvas=this;return {clearRect(){},fillRect(){},drawImage(im){assert(im,'Missing decoded image passed to canvas');canvas.draws++}}}
  }
  const defaults={mode:'sequence','focus-bg':'map',zoom:'2',rate:'1',background:'white',scrub:'0'};
  const document={getElementById(id){if(!elements.has(id)){const e=new Element(id==='focus-canvas'?'canvas':'div',id);e.value=defaults[id]||'';elements.set(id,e)}return elements.get(id)},createElement(tag){return new Element(tag)}};
  class LocalImage{
    set src(value){this.path=value;const p=allPaths.find(p=>D.embeddedImages?.[p]===value)||value;
      if(value.startsWith('data:image/png;base64,'))assert.equal(Buffer.from(value.split(',')[1],'base64').subarray(1,4).toString(),'PNG');
      else assert(fs.existsSync(path.join(out,value)),value);
      const finish=()=>{if(fail.has(p))this.onerror?.();else this.onload?.()};
      if(defer.has(p))pending.push(finish);else queueMicrotask(finish);
    }get src(){return this.path}
  }
  const context={window:{MOTION_DATA:D},document,Image:LocalImage,console,requestAnimationFrame:f=>raf.push(f),setTimeout:f=>{timers.set(++timerId,f);return timerId},clearTimeout:id=>timers.delete(id),Map,Promise,Math};
  vm.createContext(context);vm.runInContext(source,context);
  return {context,created,raf,pending,timers,$:id=>document.getElementById(id)};
}
async function settle(){await new Promise(setImmediate)}
(async()=>{
  const delayed=allPaths.at(-1),partial=harness({defer:new Set([delayed])});await settle();
  assert(partial.created.filter(e=>e.tag==='canvas'&&e.id!=='focus-canvas').every(e=>e.draws>0),'A pending image must not blank all 15 cards');
  partial.pending.forEach(f=>f());await settle();
  const timeout=harness({defer:new Set([D.monsters[0].clips[0].paths[0]])});await settle();
  for(const f of [...timeout.timers.values()])f();
  assert(timeout.created.some(e=>e.className==='phase'&&e.textContent.includes('이미지 읽기 실패')),'Stalled image needs a timeout message');
  const mapFailure=harness({fail:new Set([D.mapPath])});await settle();
  assert(mapFailure.created.filter(e=>e.tag==='canvas').every(e=>e.draws>0),'A failed map must not blank monster previews');
  assert(mapFailure.$('focus-label').textContent.includes('맵 이미지 읽기 실패'));
  const first=D.monsters[0].clips[0].paths[0],failed=harness({fail:new Set([first])});await settle();
  assert(failed.created.filter(e=>e.tag==='canvas').some(e=>e.draws>0),'One failed sprite must not block other clips');
  assert(failed.created.some(e=>e.textContent.includes('이미지 읽기 실패')&&e.className==='phase'),'Failed clip needs an explicit message');
  const none=harness({fail:new Set(allPaths)});await settle();
  assert.equal(none.created.filter(e=>e.className==='phase'&&e.textContent.includes('이미지 읽기 실패')).length,15);
  const h=harness();await settle();const {$,context,created}=h,model=context.window.motionReviewModel;
  assert($('load').textContent.includes('15동작 준비 완료'));assert.equal(created.filter(e=>e.tag==='canvas').length,16);
  let boundaries=0;for(const m of D.monsters){for(const c of m.clips){let t=0;for(let k=0;k<c.paths.length;k++){assert.equal(model.frameAt(c.durationsMs,t),k);assert.equal(model.frameAt(c.durationsMs,t+c.durationsMs[k]-.01),k);t+=c.durationsMs[k];boundaries++}assert.equal(model.frameAt(c.durationsMs,t),0)}for(const mode of ['sequence','idle','move','attack']){const es=model.entries(m,mode);for(let k=0;k<es.length;k++){assert.equal(model.choose(es,es[k].startMs),k);assert.equal(model.choose(es,es[k].endMs-.01),k)}assert.equal(model.choose(es,es.at(-1).endMs),0)}}
  $('play').onclick();assert.equal($('play').attrs['aria-pressed'],'false');$('next').onclick();assert($('focus-label').textContent.includes('2/8'));$('previous').onclick();assert($('focus-label').textContent.includes('1/8'));
  $('mode').value='attack';$('mode').onchange();$('scrub').oninput({target:{value:'9'}});assert($('focus-label').textContent.includes(D.monsters[0].clips.find(c=>c.state==='attack').durationsMs[9]+'ms'));
  $('zoom').onchange({target:{value:'3'}});$('focus-bg').value='white';$('focus-bg').onchange();const m=D.monsters[0];assert.equal($('focus-canvas').style.width,(m.crop[2]-m.crop[0])*3+'px');$('focus-bg').value='map';$('focus-bg').onchange();assert.equal($('focus-canvas').width,960);assert.equal($('focus-canvas').height,540);
  $('monster').value='R-selected';$('monster').onchange();vm.runInContext('elapsed=2100;draw()',context);assert($('focus-label').textContent.includes('확인 대기'));
  $('background').onchange({target:{value:'#25252b'}});$('rate').onchange({target:{value:'0.5'}});$('restart').onclick();$('play').onclick();assert.equal($('play').attrs['aria-pressed'],'true');
  h.raf.shift()(0);h.raf.shift()(100);assert.equal(vm.runInContext('elapsed',context),50);
  console.log(JSON.stringify({result:'pass',kind:'offline-node-vm-not-browser-qa',inline:process.argv[3]==='--inline',cards:15,clipBoundaryChecks:boundaries,partialLoad:true,timeout:true,mapFailure:true,spriteFailure:true,allFailureMessages:true,controlsAndTiming:true}));
})().catch(e=>{console.error(e);process.exitCode=1});
