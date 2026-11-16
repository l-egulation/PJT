/* Local Chrome smoke test; uses an isolated profile and loopback-only server. */
'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert'),{spawn}=require('child_process');
const root=path.resolve(__dirname,'..'),group=path.join(root,'ui/combat-hud-expanded-20261106');
const out=path.resolve(root,'../outputs/combat-ui-editor-qa',new Date().toISOString().replace(/[:.]/g,'-'));
fs.mkdirSync(out,{recursive:true});
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
const checks=[];
function check(name){checks.push(name);process.stdout.write('PASS '+name+'\n');}
async function until(fn,timeout=20000){const t=Date.now();while(Date.now()-t<timeout){try{const v=await fn();if(v)return v;}catch{}await sleep(100);}throw Error('Timed out waiting for condition');}
class CDP{
 constructor(url){this.id=0;this.pending=new Map();this.events=[];this.ws=new WebSocket(url);this.ready=new Promise((resolve,reject)=>{this.ws.onopen=resolve;this.ws.onerror=reject;});this.ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=this.pending.get(m.id);if(p){this.pending.delete(m.id);clearTimeout(p.timer);m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}}else this.events.push(m);};}
 async send(method,params={}){await this.ready;return new Promise((resolve,reject)=>{const id=++this.id,timer=setTimeout(()=>{this.pending.delete(id);reject(Error('CDP timeout '+method));},15000);this.pending.set(id,{resolve,reject,timer});this.ws.send(JSON.stringify({id,method,params}));});}
 async eval(expression){const r=await this.send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description??r.exceptionDetails.text);return r.result.value;}
}
let server,chrome,client,browser;
async function main(){
 const core=require(path.join(group,'preview/editor-core.js')),ui=require(path.join(group,'runtime/ui-model.js'));
 const cat=JSON.parse(fs.readFileSync(path.join(group,'runtime/catalog.json'),'utf8'));
 const measure=(t,s)=>t.length*s*.6,doc=core.empty(),scene=ui.buildScene(cat,{reveal:true});
 assert.equal(new Set(scene.layers.map(l=>l.id)).size,scene.layers.length);
 for(const w of cat.weapons){const s=ui.buildScene(cat,{reveal:true,weapons:[{weaponId:w.id,paletteSlots:ui.paletteSlots(5),status:'cooldown',chargeCapacity:3,availableCharges:1}]});assert.equal(new Set(s.layers.map(l=>l.id)).size,s.layers.length);}
 let rs=core.resolve(scene,doc,measure),item=rs.items.find(l=>l.role==='weapon');
 core.setRect(doc,item,[200,600,120,120]);rs=core.resolve(scene,doc,measure);assert.deepEqual(rs.items.find(l=>l.id===item.id).rect,[200,600,120,120]);
 const next=ui.buildScene(cat,{weapons:[{weaponId:'launcher_laser',paletteSlots:ui.paletteSlots(5)}]});const changed=core.resolve(next,doc,measure).items.find(l=>l.role==='weapon');assert.deepEqual(changed.rect,[200,600,120,120]);
 check('stable layer IDs and position across weapon changes');
 const ids=new Set(JSON.parse(fs.readFileSync(path.join(group,'runtime/sprites.json'),'utf8')).sprites.map(s=>s.id));
 assert.throws(()=>core.validate({...doc,layers:{test:{sx:Infinity}}},ids));
 assert.throws(()=>core.validate({...doc,extras:[{id:'custom-x',type:'sprite',sprite:'remote-url',rect:[0,0,10,10]}]},ids));
 assert.deepEqual(core.validate(JSON.parse(JSON.stringify(doc)),ids),doc);check('layout validation and JSON round trip');
 server=spawn('python',[path.join(__dirname,'open_combat_ui_editor.py'),'--port','8870','--no-browser'],{cwd:root,windowsHide:true,stdio:['ignore','pipe','pipe']});
 let serverLog='';server.stdout.on('data',b=>serverLog+=b);server.stderr.on('data',b=>serverLog+=b);
 const url=await until(()=>serverLog.match(/http:\/\/127\.0\.0\.1:\d+\/preview\/index\.html/)?.[0]);
 const profile=path.join(out,'chrome-profile');fs.mkdirSync(profile);
 const browserExe=process.env.DEVIANT_TEST_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe';
 chrome=spawn(browserExe,['--headless=new','--no-first-run','--no-default-browser-check','--disable-extensions','--remote-debugging-port=0','--window-size=1700,1150','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:['ignore','pipe','pipe']});
 let chromeLog='';chrome.stdout.on('data',b=>chromeLog+=b);chrome.stderr.on('data',b=>chromeLog+=b);chrome.on('error',e=>chromeLog+=e.stack);
 const portFile=path.join(profile,'DevToolsActivePort');try{await until(()=>fs.existsSync(portFile));}catch(e){fs.writeFileSync(path.join(out,'browser-launch.log'),chromeLog);throw Error(`Browser did not start (exit ${chrome.exitCode}): ${chromeLog.slice(-3000)}`);}
 const [port,browserPath]=fs.readFileSync(portFile,'utf8').trim().split(/\r?\n/);
 browser=new CDP('ws://127.0.0.1:'+port+browserPath);await browser.send('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:out,eventsEnabled:true});
 const target=await (await fetch('http://127.0.0.1:'+port+'/json/new?'+encodeURIComponent(url),{method:'PUT'})).json();client=new CDP(target.webSocketDebuggerUrl);
 await client.send('Page.enable');await client.send('Runtime.enable');await client.send('DOM.enable');
 await until(()=>client.eval('window.DeviantLayoutEditor?.ready()'));check('editor loads all 169 sprites in Chrome');
 const ev=expr=>client.eval(expr);
 async function click(selector){const r=await ev(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error('missing selector');e.scrollIntoView({block:'nearest'});const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()`);await client.send('Input.dispatchMouseEvent',{type:'mousePressed',...r,button:'left',clickCount:1});await client.send('Input.dispatchMouseEvent',{type:'mouseReleased',...r,button:'left',clickCount:1});await sleep(40);}
 async function fill(selector,value){await ev(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);await sleep(30);}
 async function rect(id){return ev(`DeviantLayoutEditor.getResolved().find(l=>l.id===${JSON.stringify(id)}).rect`);}
 async function pointerAt(x,y,type){const p=await ev(`(()=>{const r=document.querySelector('#selection').getBoundingClientRect();return {x:r.x+${x}/1670*r.width,y:r.y+${y}/942*r.height}})()`);await client.send('Input.dispatchMouseEvent',{type,...p,button:'left',buttons:type==='mouseReleased'?0:1,clickCount:type==='mousePressed'||type==='mouseReleased'?1:0});}
 const mapId=await ev(`DeviantLayoutEditor.getResolved().find(l=>l.role==='minimap').id`);
 await click('[data-layer='+JSON.stringify(mapId)+']');const before=await rect(mapId),cx=before[0]+before[2]/2,cy=before[1]+before[3]/2;
 await pointerAt(cx,cy,'mousePressed');await pointerAt(cx-32,cy+24,'mouseMoved');await pointerAt(cx-32,cy+24,'mouseReleased');
 let after=await rect(mapId);assert(Math.abs(after[0]-before[0]+32)<2);assert(Math.abs(after[1]-before[1]-24)<2);check('pointer drag changes map position');
 const oldWidth=after[2];await pointerAt(after[0]+after[2],after[1]+after[3],'mousePressed');await pointerAt(after[0]+after[2]+35,after[1]+after[3]+23,'mouseMoved');await pointerAt(after[0]+after[2]+35,after[1]+after[3]+23,'mouseReleased');
 assert((await rect(mapId))[2]>oldWidth+25);check('resize handle changes size');
 await click('#undo');assert(Math.abs((await rect(mapId))[2]-oldWidth)<.1);await click('#redo');assert((await rect(mapId))[2]>oldWidth+25);check('undo and redo restore geometry');
 await click('[data-group="weapon-0"]');const groupX=Number(await ev(`document.querySelector('#prop-x').value`));await fill('#prop-x',groupX+80);
 await ev(`document.querySelector('#simulation').open=true`);
 await fill('#weapon-controls fieldset:first-child [data-control="weapon"]','launcher');
 await ev(`(()=>{const s=document.querySelector('#weapon-controls fieldset:first-child [data-control="skill"]');s.value='launcher_laser';s.dispatchEvent(new Event('input',{bubbles:true}));})()`);
 const icon=await ev(`DeviantLayoutEditor.getResolved().find(l=>l.role==='weapon'&&l.group==='weapon-0')`);assert.equal(icon.sprite,'weapon-launcher-laser');assert(Math.abs(icon.rect[0]-161)<.1);check('group move survives shape-stamp weapon change');
 await ev(`document.querySelector('#simulation').open=false`);
 await click('[data-layer='+JSON.stringify(mapId)+']');await fill('#prop-x',1200);await fill('#prop-w',360);const savedMap=await rect(mapId);assert.equal(savedMap[0],1200);assert(Math.abs(savedMap[2]-360)<1e-8);check('numeric position and size editing');
 await click('#prop-hidden');assert(await ev(`DeviantLayoutEditor.getResolved().find(l=>l.id===${JSON.stringify(mapId)}).hidden`));await click('#prop-hidden');
 await click('#prop-locked');assert(await ev(`DeviantLayoutEditor.getResolved().find(l=>l.id===${JSON.stringify(mapId)}).locked`));await click('#prop-locked');check('visibility and locking');
 await click('#duplicate');assert.equal(await ev('DeviantLayoutEditor.getDocument().extras.length'),1);await click('#remove');assert.equal(await ev('DeviantLayoutEditor.getDocument().extras.length'),0);check('duplicate and remove standalone instance');
 await click('#save-layout');const jsonPath=path.join(out,'deviant-ui-layout.json');await until(()=>fs.existsSync(jsonPath));const saved=JSON.parse(fs.readFileSync(jsonPath,'utf8'));assert.equal(saved.schema,core.schema);assert.equal(saved.resolvedLayers.find(l=>l.id===mapId).rect[0],1200);check('downloaded JSON includes resolved positions');
 await click('#reset-layout');assert.equal(Object.keys(await ev('DeviantLayoutEditor.getDocument().layers')).length,0);
 async function upload(selector,file){const {root:dom}=await client.send('DOM.getDocument');const {nodeId}=await client.send('DOM.querySelector',{nodeId:dom.nodeId,selector});await client.send('DOM.setFileInputFiles',{nodeId,files:[file]});await sleep(150);}
 await upload('#load-layout',jsonPath);assert.deepEqual(await rect(mapId),savedMap);check('saved file imports with exact geometry');
 const invalid=path.join(out,'invalid-layout.json');fs.writeFileSync(invalid,JSON.stringify({...saved,assetSet:'wrong-set'}));await upload('#load-layout',invalid);assert.deepEqual(await rect(mapId),savedMap);assert((await ev(`document.querySelector('#editor-notice').textContent`)).includes('실패'));check('invalid import leaves document unchanged');
 await sleep(400);await client.send('Page.reload');await until(()=>client.eval('window.DeviantLayoutEditor?.ready()'));assert.deepEqual(await rect(mapId),savedMap);check('autosave survives page reload');
 await click('#export-png');const pngPath=path.join(out,'deviant-ui-layout.png');await until(()=>fs.existsSync(pngPath));const png=fs.readFileSync(pngPath);assert.equal(png.readUInt32BE(16),1670);assert.equal(png.readUInt32BE(20),942);check('transparent PNG export at reference resolution');
 await upload('#background-file',path.join(group,'preview/melee-three-slots.png'));assert((await ev(`document.querySelector('#editor-notice').textContent`)).includes('배경'));await click('#clear-background');assert((await ev(`document.querySelector('#editor-notice').textContent`)).includes('지웠습니다'));check('game screenshot background import and clear');
 await ev(`document.querySelector('#background').click()`);assert((await ev(`getComputedStyle(document.querySelector('#stage')).backgroundImage`)).includes('repeating-conic-gradient'));
 await ev(`document.querySelector('#background').click()`);assert.equal(await ev(`getComputedStyle(document.querySelector('#stage')).backgroundColor`),'rgb(22, 26, 25)');await ev(`document.querySelector('#background').click()`);check('checker and dark background modes');
 const exceptions=client.events.filter(e=>e.method==='Runtime.exceptionThrown');assert.deepEqual(exceptions,[],'Browser exceptions');
 await ev(`window.scrollTo(0,0);document.querySelector('#simulation').open=false`);
 const shot=await client.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,'editor-desktop.png'),Buffer.from(shot.data,'base64'));
 await client.send('Emulation.setDeviceMetricsOverride',{width:900,height:900,deviceScaleFactor:1,mobile:false});await sleep(150);
 assert(await ev(`document.documentElement.scrollWidth<=window.innerWidth+2`));check('900px viewport has no horizontal page overflow');
 const small=await client.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,'editor-compact.png'),Buffer.from(small.data,'base64'));
 // Load the preserved user edit, inspect both weapon layouts, then exercise all launcher skills.
 await client.send('Emulation.setDeviceMetricsOverride',{width:1700,height:1150,deviceScaleFactor:1,mobile:false});
 await client.send('Page.navigate',{url:url+'?layout=matched-weapons'});await until(()=>client.eval('window.DeviantLayoutEditor?.ready()'));
 await until(()=>client.eval(`document.querySelector('#editor-notice').textContent.includes('무기 2')`));
 const submitted=JSON.parse(fs.readFileSync(path.join(group,'source/layout-user.json'),'utf8'));
 const matched=await ev('DeviantLayoutEditor.getDocument()');
 for(const [id,e] of Object.entries(submitted.layers))if(!id.startsWith('weapon-1.'))assert.deepEqual(matched.layers[id],e);
 const current=await ev('DeviantLayoutEditor.getResolved()');
 for(const l of current.filter(l=>l.group==='weapon-0'&&l.type==='sprite')){
  const id=l.id.replace('weapon-0.','weapon-1.').replace(/:(-?[\d.]+),(-?[\d.]+)$/,(_,x,y)=>':'+(Number(x)+370)+','+y);
  const right=current.find(r=>r.id===id);assert(right,id);assert(Math.abs(right.rect[0]-l.rect[0]-370)<1e-8,id);for(let k=1;k<4;k++)assert(Math.abs(right.rect[k]-l.rect[k])<1e-8,id);
 }
 assert.equal(current.find(l=>l.id==='weapon-1.text:597,788').text,'W');assert.equal(current.find(l=>l.id==='weapon-1.text:679,788').text,'R');
 check('user geometry preserved and weapon 2 mirrors all weapon 1 sprite geometry');
 async function saveArt(name){const png=await ev(`(()=>{const c=document.createElement('canvas');c.width=1670;c.height=942;const x=c.getContext('2d');x.fillStyle='#f8eddc';x.fillRect(0,0,1670,942);x.drawImage(document.querySelector('#stage'),0,0);return c.toDataURL('image/png').split(',')[1]})()`);fs.writeFileSync(path.join(group,'preview',name+'.png'),Buffer.from(png,'base64'));}
 const finalLayout=JSON.parse(fs.readFileSync(path.join(group,'runtime/layout-edited.json'),'utf8'));
 finalLayout.resolvedLayers=current.map(l=>({id:l.id,group:l.group,type:l.type,sprite:l.sprite,rect:l.rect.map(v=>Math.round(v*10)/10),rotation:l.rotation,opacity:l.opacity,hidden:l.hidden,locked:l.locked,text:l.type==='text'?l.text:undefined,fontSize:l.type==='text'?l.size:undefined,z:l.z}));
 fs.writeFileSync(path.join(group,'runtime/layout-edited.json'),JSON.stringify(finalLayout,null,2)+'\n');
 await saveArt('layout-edited');
 for(const skill of cat.skills.filter(s=>s.weaponId==='launcher')){
  await ev(`(()=>{const s=DeviantPreview.getState();s.weapons[0].weaponId='launcher';s.weapons[0].skillId=${JSON.stringify(skill.id)};s.weapons[0].status='charging';DeviantPreview.setState(s)})()`);
  const scene=await ev('DeviantLayoutEditor.getResolved()');const icon=scene.find(l=>l.role==='weapon'&&l.group==='weapon-0');assert.equal(icon.sprite,skill.presentationIcon);assert.deepEqual(icon.rect,current.find(l=>l.role==='weapon'&&l.group==='weapon-0').rect);
  const special=scene.find(l=>l.role==='skill'&&l.group==='weapon-0');assert.equal(special.sprite,skill.icon);assert.equal(special.status,'charging');
  assert(scene.some(l=>l.group==='reveal-0'&&l.sprite===skill.presentationIcon));
  const progress=scene.find(l=>l.id==='weapon-0.skill-progress'),frame=scene.find(l=>l.id==='weapon-0.sprite:266,808');assert.deepEqual(progress.rect,frame.rect);
 }
 check('four launcher skills change HUD and run-card art while preserving matched geometry and charge rings');
 await ev(`(()=>{const s=DeviantPreview.getState();s.weapons[0].skillId='launcher_default';s.weapons[0].status='charging';s.weapons[0].chargeProgress=.72;s.weapons[1].weaponId='launcher';s.weapons[1].skillId='launcher_laser';DeviantPreview.setState(s)})()`);
 await saveArt('layout-launcher-skills');
 assert.deepEqual(client.events.filter(e=>e.method==='Runtime.exceptionThrown'),[],'Browser exceptions after preset');
 fs.writeFileSync(path.join(out,'result.json'),JSON.stringify({checks,out},null,2));process.stdout.write(JSON.stringify({passed:checks.length,out})+'\n');
}
main().catch(async e=>{console.error(e.stack);process.exitCode=1;if(client){try{const shot=await client.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,'failure.png'),Buffer.from(shot.data,'base64'));console.error('Screenshot: '+path.join(out,'failure.png'));}catch{}}}).finally(async()=>{
 try{if(browser)await browser.send('Browser.close');}catch{}
 if(client)client.ws.close();if(browser)browser.ws.close();if(chrome&&!chrome.killed)chrome.kill();if(server&&!server.killed)server.kill();
});
