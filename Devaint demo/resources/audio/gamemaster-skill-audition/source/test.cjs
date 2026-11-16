// Unit checks for the actual app script. DOM/audio stubs are not a browser or listening test.
const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict'),path=require('path');
const html=fs.readFileSync(path.join(__dirname,'../preview/index.html'),'utf8');
const payload=JSON.parse(html.match(/<script id="skill-data" type="application\/json">([\s\S]*?)<\/script>/)[1]);
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
nodes.get('skill-data').textContent=JSON.stringify(payload);
nodes.get('volume').value='70';nodes.get('speed').value='1';
const storage=new Map();const scheduled=[];
storage.set('lost-words-skill-sounds-v1',JSON.stringify({actions:{shield_default:{selected:2,choice:{number:3},settings:{},favorites:[3]},base_sword_default:{selected:4,choice:{number:5},settings:{},favorites:[5]}}}));
const seeded=JSON.parse(storage.get('lost-words-skill-sounds-v1'));
seeded.actions.bomb_range_special={candidateRevision:11,selected:7,favorites:[8],settings:{'bomb_range_special-v11-08':{throw:{offsetMs:30,volume:90},explosion:{offsetMs:15,volume:85}}}};
seeded.actions.whip_follow_up={candidateRevision:3,selected:0,settings:{'whip_follow_up-v3-01':{wrap:{offsetMs:35,volume:85},pop:{offsetMs:100,volume:50}}},favorites:[],choice:null};
seeded.previousReviews={dagger_default:{candidateRevision:1,favorites:[2,4],settings:{}}};seeded.actions.dagger_default={candidateRevision:1,favorites:[],settings:{},choice:null};
storage.set('lost-words-skill-sounds-v1',JSON.stringify(seeded));
class AudioContext {
  constructor(){this.currentTime=0;this.destination={};}
  resume(){return Promise.resolve();}
  createGain(){return {gain:{value:1,setTargetAtTime(){},setValueAtTime(){},linearRampToValueAtTime(){}},connect(){},disconnect(){}};}
  createBufferSource(){const s={playbackRate:{value:1},connect(){},disconnect(){},start(at){s.at=at;s.started=true;scheduled.push(s);},stop(at){assert(s.started,'WebAudio stop requires start first');if(at===undefined)s.stopped=true;else s.stopAt=at;}};return s;}
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
const flush=async()=>{for(let i=0;i<25;i++)await Promise.resolve();};
(async()=>{
  vm.runInContext(script,sandbox);await flush();assert.equal(run('ready'),true);
  assert.equal(payload.weapons.length,11);assert.equal(payload.actions.length,42);assert.equal(payload.actions.filter(a=>!a.isBasicReview).length,40);
  assert.equal(payload.reference,undefined);assert(!html.includes('id="referenceAudio"'));assert(Object.values(payload.audio).every(a=>a.edit?.mode!=='reference-excerpt'));
  assert.equal(run('saved.actions.shield_default.choice.number'),3);assert.equal(run("reviewRows().find(r=>r.id==='shield_default').pending"),false);assert.equal(run("reviewRows().find(r=>r.id==='shield_default').modifiedSinceConfirmation"),true);assert.equal(run('saved.previousReviews.shield_default.choice.number'),3);
  assert.equal(run('saved.actions.base_sword_default.choice.number'),5);
  assert.equal(run("saved.actions.bomb_range_special.favorites[0]"),8);
  assert.equal(run("saved.actions.bomb_range_special.settings['bomb_range_special-v12-08'].throw.offsetMs"),30);
  assert.equal(run("saved.actions.bomb_range_special.settings['bomb_range_special-v12-08'].explosion.volume"),85);
  assert.equal(run("reviewRows().find(r=>r.id==='bomb_range_special').pending"),false);
  run("setWeapon('whip');setAction('whip_follow_up');select(0)");assert.equal(run('recipe().layers.length'),1);assert.equal(run('recipe().layers[0].audio'),payload.fixedWhipLayer.audio);assert.equal(run('settings().pop'),undefined);
  assert.equal(payload.weapons.find(w=>w.id==='hammer').selection.layerOffsetsMs.impact,210);
  for(const action of payload.actions){
    run(`setWeapon(${JSON.stringify(action.weapon)});setAction(${JSON.stringify(action.id)})`);
    assert.equal(nodes.get('candidates').children.length,action.fixedCandidate?2:11);
    assert.equal(action.candidates.length,10);assert(payload.audio[action.current]);
    const signatures=action.candidates.map(r=>JSON.stringify(r.layers));if(!action.fixedCandidate&&!action.confirmedComponents)assert.equal(new Set(signatures).size,10,action.id+' unique recipes');
    for(const speed of [.5,1,1.5,2]){
      nodes.get('speed').value=String(speed);
      for(let i=0;i<11;i++){
        run(`select(${i});render(0);render(action.impact);render(action.previewDuration)`);
        const events=run("eventsFor(plan('skill'))");assert(events.length>0);
        for(const e of events){assert(payload.audio[e.key]);assert(Number.isFinite(e.time)&&Number.isFinite(e.gain)&&e.gain>=0);assert(e.rate>0);}
        if(i===10){assert.equal(events.length,1+(action.currentExtras?.length||0));assert(Math.abs(events[0].rate-(action.currentRate||1)*speed)<1e-9);assert(Math.abs(events[0].time-run('timing().shift')/speed)<1e-9);assert.equal(events[0].gain,.8);}
        else{
          assert(events.length>=run('recipe().layers.length'));
          assert(run("overlapGain(eventsFor(plan('skill')))")>.9999999,'Actual overlap peak bound');
        }
      }
    }
    const before=scheduled.length;run('select(0)');await run("play('once')");assert(scheduled.length>before);run('stop()');assert(scheduled.slice(before).every(s=>s.stopped));
  }
  run("setWeapon('hammer');setAction('hammer_range');select(5)");nodes.get('speed').value='2';
  const initial=run("eventsFor(plan('skill'))");
  run("settings().detail.offsetMs=150;settings().detail.volume=75;saveEdit()");
  const edited=run("eventsFor(plan('skill'))");assert(Math.abs(edited[1].time-initial[1].time-.075)<1e-9);assert.equal(edited[0].time,initial[0].time);
  nodes.get('choose').click();const exported=run('exportSelection()');const chosen=exported.actions.find(a=>a.id==='hammer_range');assert.equal(chosen.choice.number,6);assert.equal(chosen.choice.settings.detail.offsetMs,150);assert.equal(chosen.choice.settings.detail.volume,75);
  run("setWeapon('whip');setWeapon('hammer');setAction('hammer_range')");assert.equal(run('selected'),5);assert.equal(run('settings().detail.offsetMs'),150);
  run("solo='detail'");assert.equal(run("eventsFor(plan('skill')).length"),1);run('solo=null');
  nodes.get('speed').value='1';await run("play('ab')");assert.equal(run('cycle.kind'),'basic');
  const p=run('cycle.p');assert.equal(p.segments.length,2);
  run('ac.currentTime=cycle.origin+cycle.finish+.5;tick()');await flush();assert.equal(run('cycle.kind'),'skill');run('stop()');
  run('select(0)');await run("play('sequence')");
  for(let i=0;i<11;i++){assert.equal(run('selected'),i);run('ac.currentTime=cycle.origin+cycle.finish+.5;tick()');await flush();}
  assert.equal(run('running'),false);
  // Charge grows for the whole held duration and the release sound follows it.
  run("setWeapon('launcher');setAction('launcher_basic_special');select(0)");nodes.get('speed').value='1';
  for(const hold of [.2,.5,.8]){
    run(`state().scenario.charge=${hold}`);const events=run("eventsFor(plan('skill'))");const charge=events.find(e=>e.layer==='charge'),release=events.find(e=>e.layer==='release');
    assert.equal(charge,undefined,'Charging must be silent');
    assert(Math.abs(release.time+payload.audio[release.key].peakTime/release.rate-hold)<1e-9);
  }
  // Three skills resolve the same selected teleport and two resolve the same throw.
  for(const [group,ids,layer] of [['teleport',['rapier_movement','rapier_follow_up','shuriken_move_special'],'teleport'],['shuriken_throw',['shuriken_basic_special','shuriken_followup_special'],'throw'],['large_explosion',['bomb_range_special','bomb_followup_special','bomb_move_special'],'explosion'],['normal_explosion',['bomb_basic_review','shuriken_range_special'],'explosion']]){
    let key;
    for(const id of ids){const a=payload.actions.find(a=>a.id===id);run(`setWeapon('${a.weapon}');setAction('${id}');select(2);saved.shared['${group}']=7`);const e=run("eventsFor(plan('skill'))").find(e=>e.layer===layer);if(key)assert.equal(e.key,key);key=e.key;}
  }
  run("setWeapon('shuriken');setAction('shuriken_range_special');select(0)");assert.equal(run("eventsFor(plan('skill')).find(e=>e.layer==='explosion').key"),payload.sharedGroups.normal_explosion.variants[0].audio);
  run("setWeapon('bomb');setAction('bomb_followup_special');select(0)");assert(Math.abs(run("eventsFor(plan('skill')).find(e=>e.layer==='explosion').time")-2.7)<1e-9);
  for(const [id,part,seconds] of [['greatsword_movement','spin',2],['shuriken_followup_special','spin',2]]){
    const a=payload.actions.find(a=>a.id===id);run(`setWeapon('${a.weapon}');setAction('${id}');select(0)`);const e=run("eventsFor(plan('skill'))").find(e=>e.layer===part);assert(e.loop);assert.equal(e.duration,seconds);
    const at=scheduled.length;await run('play()');assert(scheduled.slice(at).some(s=>s.loop&&Math.abs(s.stopAt-s.at-seconds)<1e-8));run('stop()');assert(scheduled.slice(at).every(s=>s.stopped));
  }
  run("setWeapon('shield');setAction('shield_movement');select(0)");assert(run("eventsFor(plan('skill')).every(e=>!e.loop&&e.duration<.6)"));
  run("setWeapon('greatsword');setAction('greatsword_movement');select(0)");
  const spin=run("eventsFor(plan('skill')).find(e=>e.layer==='spin')");assert(spin.gain>.45,'Sustain must not be divided by hit count');
  run("setWeapon('rapier');setAction('rapier_default');select(0)");assert.equal(run('recipe().layers.length'),1);assert.equal(run("eventsFor(plan('skill')).some(e=>e.layer==='charge')"),false);
  run("setWeapon('shield');setAction('shield_range');select(0)");assert.equal(run('recipe().layers.length'),1);assert.equal(run('recipe().layers[0].id'),'deploy');assert.equal(run('recipe().layers[0].loop'),undefined);
  run("setWeapon('bomb');setAction('bomb_move_special');select(0)");const missile=run("eventsFor(plan('skill'))");assert.equal(missile.length,1);assert.equal(missile[0].layer,'explosion');assert.equal(missile[0].time,3.2);
  run("setWeapon('shuriken');setAction('shuriken_move_special');select(0)");
  for(const delay of [.05,.25,.55]){run(`state().scenario.impactDelay=${delay}`);const e=run("eventsFor(plan('skill'))"),hit=e.find(e=>e.layer==='hit'),teleport=e.find(e=>e.layer==='teleport'),back=e.find(e=>e.layer==='return');
    const contact=hit.time+payload.audio[hit.key].peakTime/hit.rate;assert(Math.abs(contact-(.2+delay))<1e-8);assert(teleport.time>contact);assert(Math.abs(back.time-contact-5)<1e-8);}
  run("setWeapon('shuriken');setAction('shuriken_followup_special');select(0)");assert.equal(run("recipe().layers.find(l=>l.id==='spin').previousRevision"),2);assert.equal(run("recipe().layers.find(l=>l.id==='spin').rate"),1.12);
  // Approved explosions remain fixed even when cycling through throw candidates.
  run("setWeapon('bomb');setAction('bomb_basic_review');select(0)");await run("play('sequence')");run('ac.currentTime=cycle.origin+cycle.finish+.5;tick()');await flush();assert.equal(run('selected'),1);assert.equal(run("eventsFor(plan('skill')).find(e=>e.layer==='explosion').key"),'approved/bomb-B3');run('stop()');
  run('select(10)');const currentBomb=run("eventsFor(plan('skill'))");assert.equal(currentBomb[1].key,payload.basicExplosion);assert.equal(currentBomb[1].gain,.8);
  const basicBomb=run("eventsFor(plan('basic'))");assert.equal(basicBomb.length,2);assert.equal(basicBomb[1].key,'approved/bomb-B3');assert.equal(basicBomb[1].gain,.8);
  const export2=run('exportSelection()');assert.equal(export2.schemaVersion,7);assert(export2.sharedChoices.teleport>=0);assert(export2.sharedGroups.large_explosion.fixed);assert.equal(export2.sharedGroups.large_explosion.variants.length,1);assert.equal(export2.actions.filter(a=>a.isBasicReview).length,2);
  assert.equal(export2.basicExplosionPreview.audio,'approved/bomb-B3');
  for(const id of ['bomb_range_special','bomb_followup_special']){
    const a=payload.actions.find(a=>a.id===id);run(`setWeapon('bomb');setAction('${id}');select(0)`);
    assert.equal(run("recipe().layers.find(l=>l.id==='throw').gain"),1.9);
    assert.equal(run("eventsFor(plan('skill')).find(e=>e.layer==='explosion').key"),'r11/380a9a7cdb2eb0736ec9');
  }
  run("setAction('bomb_basic_special');select(7)");assert(run("eventsFor(plan('skill')).filter(e=>e.layer==='explosion').every(e=>e.key==='approved/bomb-B3')"));
  const count=scheduled.length;const pending=run("play('loop')");run("setWeapon('dagger')");await pending;assert.equal(scheduled.length,count,'No stale schedule after switching weapons');
  run("setWeapon('unarmed')");assert.equal(run('action'),null);assert.equal(nodes.get('play').disabled,true);assert.equal(nodes.get('basic').disabled,false);
  await run("play('basic')");assert.equal(run('cycle.p.segments.length'),2);run('stop()');
  for(const [id,contract] of Object.entries(payload.frozenLayers)){const parts=id.split('/'),a=payload.actions.find(a=>a.id===parts[0]),layer=a.candidates[Number(parts[1])-1].layers.find(l=>l.id===parts[2]);assert.deepStrictEqual(layer,contract.layer);assert.equal(payload.audio[layer.audio].sha256,contract.sha256);}
  // Fixed whip has only the exact #1 wrap, irrespective of which former candidate is requested.
  run("setWeapon('whip');setAction('whip_follow_up')");for(let i=0;i<10;i++){run(`select(${i})`);assert.equal(run('selected'),0);assert.equal(run('recipe().layers.length'),1);assert.equal(run('recipe().layers[0].audio'),payload.fixedWhipLayer.audio);}
  // Echo follows the confirmed strong slash and its edits, shifted to the first strike only.
  run("saved.actions.base_sword_default.choice={number:7};setWeapon('base_sword');setAction('base_sword_default');select(6)");nodes.get('speed').value='1';
  run("settings().main.offsetMs=25;settings().main.volume=80;saveEdit()");const strong=run("eventsFor(plan('skill'))");
  run("setAction('base_sword_follow_up');select(0)");const echo=run("eventsFor(plan('skill'))");assert.equal(echo.length,strong.length);for(let i=0;i<echo.length;i++){assert.equal(echo[i].key,strong[i].key);assert.equal(echo[i].gain,strong[i].gain);assert(Math.abs(echo[i].time-strong[i].time-.045)<1e-8);assert(echo[i].time<.3);}
  run("settings().main.volume=75;saveEdit()");assert.equal(run('saved.actions.base_sword_default.choice.settings.main.volume'),75);
  run("saved.actions.base_sword_default.choice={number:11}");const currentLink=run("eventsFor(plan('skill'))");assert.equal(currentLink[0].key,payload.actions.find(a=>a.id==='base_sword_default').current);assert(Math.abs(currentLink[0].time-.045)<1e-8);
  assert.equal(run("reviewRows().find(r=>r.id==='whip_follow_up').pending"),false);
  assert.equal(run("reviewRows().find(r=>r.id==='base_sword_follow_up').pending"),false);
  run("saved.actions.base_sword_default.choice=null;saved.actions.base_sword_default.favorites=[]");assert.equal(run("reviewRows().find(r=>r.id==='base_sword_follow_up').pending"),true);
  run("saved.actions.rapier_range={selected:1,favorites:[2],settings:{}}");assert.equal(run("reviewRows().find(r=>r.id==='rapier_range').pending"),false,'Stars are confirmations');
  for(const id of payload.reviewedActions){const a=payload.actions.find(a=>a.id===id);run(`setWeapon('${a.weapon}');setAction('${id}');select(0)`);const p=run("plan('skill')"),events=run("eventsFor(plan('skill'))");assert(p.duration/p.speed>=Math.max(...events.map(e=>e.time+e.duration))-.00001,'Timeline includes the entire tail');}
  assert.equal(run('exportSelection().reviewStatus.length'),42);
  run("setWeapon('dagger');setAction('dagger_default')");assert.deepStrictEqual(Array.from(run('confirmedNumbers(action)')),[2,4]);assert.equal(run("reviewRows().find(r=>r.id==='dagger_default').pending"),false);
  const approved=run('exportSelection()').actions.find(a=>a.id==='dagger_default').confirmedCandidates;assert.equal(approved.length,2);assert(approved.every(a=>a.restoredFromPrevious));
  run('confirmStar(2)');assert.deepStrictEqual(Array.from(run('confirmedNumbers(action)')),[4]);assert.equal(JSON.parse(storage.get('lost-words-skill-sounds-v1')).starConfirmationMigration,1);
  run('confirmStar(6)');assert.deepStrictEqual(Array.from(run('confirmedNumbers(action)')),[4,6]);run('settings().main.volume=75;saveEdit()');assert.equal(run("confirmationDetails(action).find(c=>c.number===6).settings.main.volume"),75);
  run("setWeapon('shield');setAction('shield_default');select(2)");nodes.get('choose').click();assert.equal(run('confirmationDetails(action)[0].isCurrentRevision'),true);
  run("setWeapon('base_sword');setAction('base_sword_default');saved.actions.base_sword_default.choice=null;saved.actions.base_sword_default.favorites=[];confirmStar(4);setAction('base_sword_follow_up');select(0)");assert.equal(run('recipe().linkedNumber'),4);assert.equal(run("reviewRows().find(r=>r.id==='base_sword_follow_up').pending"),false);

  // Smoke re-review uses a single unlooped 1.1-second recording regardless of visual lifetime.
  run("setWeapon('dagger');setAction('dagger_range');select(0)");nodes.get('speed').value='1';
  for(const sustain of [1,3,5]){run(`state().scenario.sustain=${sustain}`);const e=run("eventsFor(plan('skill'))");assert.equal(e.length,1);assert.equal(e[0].layer,'smoke');assert.equal(e[0].loop,false);assert.equal(e[0].duration,1.1);assert.equal(e[0].time,run('action.impact'));}
  assert.equal(run("reviewRows().find(r=>r.id==='dagger_range').pending"),true);
  assert.deepStrictEqual(Array.from(run("saved.actions.greatsword_default.favorites")),[6,7]);
  assert.equal(run("saved.actions.launcher_range_special.favorites[0]"),9);
  // Installation candidates never begin during the cast windup, at any playback speed.
  run("setWeapon('shield');setAction('shield_follow_up');select(0)");
  for(const speed of [.5,1,2]){nodes.get('speed').value=String(speed);const placed=run("eventsFor(plan('skill'))");assert.equal(placed.length,2);assert(Math.abs(placed[0].time-.2/speed)<1e-9);assert(Math.abs(placed[1].time-.218/speed)<1e-9);}
  nodes.get('speed').value='1';run('render(.19)');assert.equal(nodes.get('phase').textContent,'미끼 설치 준비 · 무음');run('render(.2)');assert(nodes.get('phase').textContent.includes('설치 순간'));assert(nodes.get('markers').textContent.includes('미끼 설치 시점 200'));
  console.log('PASS: pending/fixed/linked status, full-tail timeline and scheduling, fixed #1 whip, strong slash linkage, restored throws, event envelopes, edits/export/cancellation.');
})().catch(e=>{console.error(e);process.exitCode=1;});
