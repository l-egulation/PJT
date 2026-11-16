// Unit checks for the actual app script. DOM/audio stubs are not a browser or listening test.
const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict'),path=require('path');
const html=fs.readFileSync(path.join(__dirname,'../preview/index.html'),'utf8');
const payload=JSON.parse(html.match(/<script id="audition-data" type="application\/json">([\s\S]*?)<\/script>/)[1]);
const script=fs.readFileSync(path.join(__dirname,'app.js'),'utf8');
class Element {
  constructor(tag='div'){this.tag=tag;this.children=[];this.attributes={};this.className='';this.value='';this.checked=false;this.disabled=false;this.textContent='';this.style={};
    this.classList={toggle:(c,on)=>{let s=new Set(this.className.split(' ').filter(Boolean));if(on===undefined)on=!s.has(c);on?s.add(c):s.delete(c);this.className=[...s].join(' ');},remove:c=>this.classList.toggle(c,false)};
  }
  append(...els){this.children.push(...els);if(this.tag==='select'&&this.children.length&&this.value==='')this.value=this.children[0].value;}
  replaceChildren(...els){this.children=[];if(this.tag==='select')this.value='';this.append(...els);}
  setAttribute(k,v){this.attributes[k]=v;}
  querySelector(selector){return this.querySelectorAll(selector)[0]||null;}
  querySelectorAll(selector){const found=[];for(const e of this.children){if(selector.startsWith('.')&&e.className.split(' ').includes(selector.slice(1)))found.push(e);found.push(...e.querySelectorAll(selector));}return found;}
  get selectedOptions(){return this.children.filter(x=>x.value===this.value);}
  getContext(){return new Proxy({},{get:(t,p)=>p in t?t[p]:(()=>{}),set:(t,p,v)=>(t[p]=v,true)});}
  click(){if(this.onclick)return this.onclick();}
}
const nodes=new Map();
for(const match of html.matchAll(/<([a-z][a-z0-9]*)\b[^>]*id="([^"]+)"[^>]*>/g)){nodes.set(match[2],new Element(match[1]));}
nodes.get('audition-data').textContent=JSON.stringify(payload);
nodes.get('volume').value='65';nodes.get('normalize').checked=true;nodes.get('showFx').checked=true;
const storage=new Map();const scheduled=[];
class AudioContext {
  constructor(){this.currentTime=0;this.destination={};}
  resume(){return Promise.resolve();}
  createGain(){return {gain:{value:1,setTargetAtTime(){}},connect(){},disconnect(){}};}
  createBufferSource(){const s={playbackRate:{value:1},connect(){},disconnect(){},start(at){s.at=at;scheduled.push(s);},stop(){s.stopped=true;}};return s;}
  decodeAudioData(array){
    const b=Buffer.from(array);let at=12,rate,channels,bits,bytes;
    while(at+8<=b.length){const key=b.toString('ascii',at,at+4),n=b.readUInt32LE(at+4);if(key==='fmt '){channels=b.readUInt16LE(at+10);rate=b.readUInt32LE(at+12);bits=b.readUInt16LE(at+22);}if(key==='data')bytes=n;at+=8+n+(n%2);}
    assert(rate&&channels&&bits&&bytes);return Promise.resolve({duration:bytes/(rate*channels*bits/8)});
  }
}
const document={getElementById:id=>{assert(nodes.has(id),'Missing element '+id);return nodes.get(id);},createElement:tag=>new Element(tag),
  querySelectorAll:s=>[...nodes.values()].flatMap(e=>e.querySelectorAll(s)),addEventListener(){},hidden:false};
const sandbox={document,localStorage:{getItem:k=>storage.get(k)||null,setItem:(k,v)=>storage.set(k,v)},
  Image:class {set src(v){Promise.resolve().then(()=>{this.width=256;this.height=144;this.onload();});}},
  AudioContext,performance:{now:()=>0},requestAnimationFrame(){},setTimeout(){},window:{addEventListener(){}},
  Blob,URL,atob:s=>Buffer.from(s,'base64').toString('binary'),console};
vm.createContext(sandbox);
const run=code=>vm.runInContext(code,sandbox);
(async()=>{
  vm.runInContext(script,sandbox);
  for(let i=0;i<5;i++)await Promise.resolve();
  assert.equal(run('ready'),true);assert.equal(payload.weapons.length,11);
  assert.equal(payload.weapons.reduce((n,w)=>n+w.basics.length,0),23);
  assert.equal(run("savedRoot.weapons.base_sword.choice.number"),5);
  for(const weapon of payload.weapons){
    run(`activateWeapon(${JSON.stringify(weapon.id)})`);
    assert.equal(nodes.get('candidates').children.length,11);
    assert.equal(nodes.get('motion').children.length,weapon.basics.length+1);
    assert.equal(run('plan().speed'),weapon.defaultSpeed);
    for(let i=0;i<weapon.basics.length;i++){
      nodes.get('motion').value='attack'+(i+1);run('select(10,false);render(0);drawWave()');
      assert(nodes.get('soundInfo').textContent.includes(`현재 ${i+1}타`));
    }
    nodes.get('motion').value='combo';
    for(const speed of [.5,1,weapon.defaultSpeed,2]){
      nodes.get('speed').value=String(speed);
      const p=run('plan()');
      for(let i=0;i<p.segments.length;i++){
        const s=p.segments[i],clip=weapon.clips[s.name];
        const mapped=run(`mapSourceTime(${s.impact},${s.impact},${s.duration},${clip.sourceImpact},${clip.seconds.reduce((a,b)=>a+b,0)})`);
        assert(Math.abs(mapped-clip.sourceImpact)<1e-9);
      }
      run('select(10,false)');const before=scheduled.length;await run("play('loop')");
      assert.equal(scheduled.length-before,weapon.basics.length);
      const current=scheduled.slice(before),origin=run('cycle.origin');
      current.forEach((s,i)=>{
        const ref=payload.audio[weapon.currentVariants['attack'+(i+1)]];
        assert(Math.abs(s.playbackRate.value-ref.nominalDuration/weapon.basics[i].duration*speed)<1e-9);
        assert(Math.abs(s.at-origin-p.segments[i].start/speed)<1e-9);
      });
      run('stop()');assert(current.every(s=>s.stopped));
      run('select(0,false)');await run('play()');
      const authored=!!run('DATA.sounds[0].authored');
      assert(scheduled.slice(-weapon.basics.length).every(s=>Math.abs(s.playbackRate.value-(authored?speed:1))<1e-9));run('stop()');
    }
  }
  run("activateWeapon('whip');select(3,false);offsets[3]=25;persist()");
  nodes.get('confirmChoice').click();run("activateWeapon('dagger');activateWeapon('whip')");
  assert.equal(run('selected'),3);assert.equal(run('choice.number'),4);assert.equal(run('offsets[3]'),25);
  assert.equal(run('savedRoot.weapons.base_sword.choice.number'),5);
  assert.equal(run('savedRoot.weapons.launcher.choice.number'),11);
  run("activateWeapon('whip');select(0,false)");const count=scheduled.length;
  const first=run('play()');run("activateWeapon('dagger')");await first;
  assert.equal(scheduled.length,count,'Cancelled preparation must not schedule stale weapon audio');
  run("select(0,false)");await run("play('sequence')");
  for(let i=0;i<11;i++){assert.equal(run('selected'),i);run('ac.currentTime=cycle.origin+cycle.finish+.5;tick()');}
  assert.equal(run('running'),false);
  assert(nodes.get('status').textContent.includes('11개 비교'));
  // A device with 120 ms latency must display the audible position, not render-ahead time.
  run('ac.currentTime=10;ac.baseLatency=.02;ac.outputLatency=.1');
  assert(Math.abs(run('audibleTime()')-9.88)<1e-9);
  run('ac.getOutputTimestamp=()=>({contextTime:9.8,performanceTime:0});performance.now=()=>50');
  assert(Math.abs(run('audibleTime()')-9.85)<1e-9);
  run('delete ac.getOutputTimestamp;ac.baseLatency=0;ac.outputLatency=0');
  // The two stems move independently; attack1 must remain unchanged.
  run("activateWeapon('hammer');select(0,false)");
  assert.equal(nodes.get('layerEditor').hidden,false);
  nodes.get('motion').value='combo';nodes.get('speed').value='2';
  const original=run('playbackEvents(plan(),DATA.sounds[selected],0)');
  assert.equal(original.length,3);
  nodes.get('swingOffset').value='-100';nodes.get('swingOffset').oninput();nodes.get('swingOffset').onchange();
  nodes.get('impactOffsetNumber').value='150';nodes.get('impactOffsetNumber').onchange();
  const shifted=run('playbackEvents(plan(),DATA.sounds[selected],0)');
  assert.equal(shifted[0].time,original[0].time);
  assert(Math.abs(shifted.find(e=>e.key==='attack2:swing').time-original.find(e=>e.key==='attack2:swing').time+.05)<1e-9);
  assert(Math.abs(shifted.find(e=>e.key==='attack2:impact').time-original.find(e=>e.key==='attack2:impact').time-.075)<1e-9);
  nodes.get('confirmChoice').click();
  let exported=run('exportSelection()').weapons.find(w=>w.weapon==='hammer');
  assert.equal(exported.choice.layerOffsetsMs.swing,-100);assert.equal(exported.choice.layerOffsetsMs.impact,150);
  assert(exported.candidates[0].layers.attack2.impact.sha256);
  run("activateWeapon('whip');activateWeapon('hammer');select(0,false)");
  assert.equal(run('layerOffsets[0].swing'),-100);assert.equal(run('layerOffsets[0].impact'),150);
  nodes.get('layerListen').value='impact';nodes.get('layerListen').onchange();
  assert.equal(nodes.get('motion').value,'attack2');
  const solo=run('playbackEvents(plan(),DATA.sounds[selected],0)');
  assert.equal(solo.length,1);assert.equal(solo[0].key,'attack2:impact');
  const beforeSolo=scheduled.length;await run("play('loop')");
  assert.equal(scheduled.length,beforeSolo+1);
  nodes.get('impactOffset').value='-400';nodes.get('impactOffset').oninput();nodes.get('impactOffset').onchange();
  for(let i=0;i<12;i++)await Promise.resolve();
  assert(scheduled[beforeSolo].stopped,'Moving a layer cancels the prior loop audio');
  run('stop()');nodes.get('resetLayers').click();
  assert.equal(run('layerOffsets[0].swing'),0);assert.equal(run('layerOffsets[0].impact'),0);
  assert.equal(nodes.get('layerListen').value,'both');
  run('select(10,false)');assert.equal(nodes.get('layerEditor').hidden,true);
  const hammer=payload.weapons.find(w=>w.id==='hammer');
  if(hammer.reviewCandidates){
    assert.equal(hammer.reviewCandidates.length,10);
    for(const candidate of hammer.reviewCandidates){
      assert(candidate.authored&&candidate.fixedMix);
      assert.equal(payload.audio[candidate.variants.attack1].recipe.floor,null);
      assert(payload.audio[candidate.variants.attack2].recipe.floor);
      const contact=payload.audio[candidate.variants.attack2].recipe.floorContact;
      const recipe=payload.audio[candidate.variants.attack2].recipe;
      assert.equal(recipe.floorAlignment,'shared-energy-peak');
      assert.equal(recipe.swingEndsAtContact,false);
      assert(Math.abs(recipe.accentSeconds-(contact.seconds-.04))<1e-9);
      assert.equal(contact.frame,'CharacterBasicOutline/hammer-a/15');
      assert.equal(hammer.clips.attack2.frames[contact.frameIndex],contact.frame);
      for(const speed of [.5,1,1.5,2]){
        const basic=hammer.basics[1],clip=hammer.clips.attack2;
        const mapped=run(`mapSourceTime(${contact.seconds/speed}*${speed},${basic.impact},${basic.duration},${clip.sourceImpact},${clip.seconds.reduce((a,b)=>a+b,0)})`);
        assert(Math.abs(mapped-contact.sourceSeconds)<1e-8);
        assert.equal(run(`frameAt(${JSON.stringify(clip)},${mapped})`),contact.frameIndex);
      }
    }
    const launcher=payload.weapons.find(w=>w.id==='launcher');
    for(const key of launcher.reviewCandidates)assert.equal(payload.audio[key].recipe.layers,1);
  }
  console.log('PASS: independent hammer stem offsets, speed scaling, solo, loop cancellation, saved/exported settings, reset; 11 weapons / 23 hits; latency and playback regression checks.');
})().catch(error=>{console.error(error);process.exitCode=1;});
