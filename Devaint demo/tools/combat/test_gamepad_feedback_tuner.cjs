'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {spawn} = require('node:child_process');
const readline = require('node:readline');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.FEEDBACK_TEST_PLAYWRIGHT_PATH || 'playwright');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'outputs/dualsense/tuner-browser');
fs.mkdirSync(output, {recursive:true});
const fixture = fs.mkdtempSync(path.join(output, 'fixture-'));
const source = path.join(fixture, 'profile.json'), target = path.join(fixture, 'unity.json');
const original = JSON.parse(fs.readFileSync(path.join(root, 'data/combat/presentation/gamepad_feedback.json'), 'utf8'));
const normalized = structuredClone(original);normalized.schemaVersion=2;
for(const key of ['basic','special','dash','jump','hit','heavyHit','hurt','parry'])if(!normalized[key].segments)normalized[key]={segments:[{...normalized[key],curve:'decay'}]};
fs.writeFileSync(source, JSON.stringify(original));
const program = `import sys
sys.path.insert(0, 'tools/combat')
from http.server import ThreadingHTTPServer
from gamepad_feedback_tuner import ProfileStore, handler_for
server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for(ProfileStore(sys.argv[1], sys.argv[2])))
print('READY http://127.0.0.1:' + str(server.server_port), flush=True)
server.serve_forever()`;

(async()=>{
 const server = spawn(process.env.PYTHON || 'python', ['-u','-c',program,source,target], {cwd:root,stdio:['ignore','pipe','pipe']});
 let stderr='';server.stderr.on('data',chunk=>stderr+=chunk);
 let browser;
 try {
  const url = await new Promise((resolve,reject)=>{
   const timer=setTimeout(()=>reject(Error('Server startup timeout: '+stderr)),15000);
   const lines=readline.createInterface({input:server.stdout});
   lines.on('line',line=>{if(line.startsWith('READY ')){clearTimeout(timer);resolve(line.slice(6));}});
   server.once('exit',code=>{clearTimeout(timer);reject(Error('Server exited '+code+': '+stderr));});
  });
  browser=await chromium.launch({headless:true,...(process.env.FEEDBACK_TEST_BROWSER_PATH?{executablePath:process.env.FEEDBACK_TEST_BROWSER_PATH}:{})});
  const context=await browser.newContext({viewport:{width:1440,height:1160},acceptDownloads:true});
  await context.addInitScript(()=>{
   window.hardware={requests:0,reports:[],rumble:[],resets:0,padConnected:true,trace:[]};
   const listeners=new Set();
   const device={vendorId:0x054c,productId:0x0ce6,productName:'Synthetic DualSense',opened:false,
    collections:[{outputReports:[{reportId:2,items:[{reportSize:8,reportCount:47}]}],children:[]}],
    async open(){this.opened=true;},async close(){this.opened=false;},
    addEventListener(type,listener){if(type==='inputreport')listeners.add(listener);},
    removeEventListener(type,listener){if(type==='inputreport')listeners.delete(listener);},
    async sendReport(id,data){if(hardware.failNext){hardware.failNext=false;throw Error('synthetic device failure');}hardware.reports.push([id,...new Uint8Array(data)]);}
   };
   hardware.device=device;
   hardware.pad={index:0,id:'Synthetic standard pad',mapping:'standard',buttons:Array.from({length:18},()=>({pressed:false,value:0})),vibrationActuator:{async playEffect(type,options){hardware.rumble.push(options);hardware.trace.push([options.strongMagnitude,options.weakMagnitude]);return 'complete';},async reset(){hardware.resets++;hardware.trace.push([0,0]);}}};
   hardware.nativeInput=(state={},format='usb')=>{
    const reportId=format==='bluetooth'?0x31:1,size=format==='usb'?63:format==='bluetooth'||format==='minimal-long'?77:9;
    const bytes=new Uint8Array(size),trigger=format==='usb'?4:format==='bluetooth'?5:7,buttons=format==='usb'?7:format==='bluetooth'?8:4;
    bytes[trigger]=Math.round((state.left||0)*255);bytes[trigger+1]=Math.round((state.right||0)*255);
    bytes[buttons]=8|(state.faces||[]).reduce((mask,index)=>mask|[0x20,0x40,0x10,0x80][index],0);bytes[buttons+1]=state.stop?0x20:0;
    for(const listener of listeners)listener({device,reportId,data:new DataView(bytes.buffer)});
   };
   Object.defineProperty(navigator,'hid',{configurable:true,value:{async requestDevice(){hardware.requests++;return [device];},addEventListener(){}}});
   Object.defineProperty(navigator,'getGamepads',{configurable:true,value:()=>hardware.padConnected?[hardware.pad]:[]});
  });
  const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
  const frames=()=>page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
  const nativeInput=async(state={},format='usb')=>{await page.evaluate(({state,format})=>hardware.nativeInput(state,format),{state,format});await frames();};
  const padInput=async pressed=>{await page.evaluate(pressed=>{for(let index=0;index<hardware.pad.buttons.length;index++){const value=pressed[index]||0;hardware.pad.buttons[index]={pressed:value>=.5,value};}},pressed);await frames();};
  await page.goto(url);await page.waitForFunction(()=>window.FeedbackTuner?.profile);
  assert.equal(await page.locator('[data-event]').count(),8);
  assert.equal(await page.evaluate(()=>hardware.requests),0,'hardware permission is requested only by the connect button');
  assert.deepEqual(await page.evaluate(()=>FeedbackTuner.profile),normalized);
  await page.locator('[data-number="basic.segments.0.low"]').fill('62');
  assert.equal(await page.evaluate(()=>FeedbackTuner.profile.basic.segments[0].low),.62);
  assert.match(await page.locator('#save-status').innerText(),/미저장/);
  await page.locator('[data-number="basic.segments.0.low"]').fill('101');
  assert.equal(await page.locator('#save').isDisabled(),true);
  assert.equal(await page.locator('#play-pulse').isDisabled(),true);
  assert.equal(await page.evaluate(()=>FeedbackTuner.profile.basic.segments[0].low),.62,'invalid input keeps the last valid draft');
  await page.locator('[data-number="basic.segments.0.low"]').fill('62');
  await page.locator('#save').click();await page.waitForFunction(()=>document.querySelector('#source-note').textContent.includes('저장했습니다'));
  assert.equal(JSON.parse(fs.readFileSync(source)).basic.segments[0].low,.62);
  assert.equal(fs.readFileSync(source,'utf8'),fs.readFileSync(target,'utf8'));
  await page.locator('#preset').selectOption('hurt');await page.locator('#apply-preset').click();
  assert.equal(await page.locator('[data-segment]').count(),3);
  assert.equal(JSON.parse(fs.readFileSync(source)).basic.segments[0].low,.62,'applying an example edits only the draft');
  assert.deepEqual(await page.evaluate(()=>FeedbackTuner.profile.special),normalized.special,'an example changes only the selected event');
  await page.locator('#add-rest').click();
  await page.locator('[data-number="basic.segments.3.duration"]').fill('55');
  await page.getByRole('button',{name:'구간 4 복제',exact:true}).click();
  await page.locator('[data-number="basic.segments.4.low"]').fill('20');await page.locator('#segment-curve').selectOption('rise');
  await page.getByRole('button',{name:'구간 5 위로',exact:true}).click();
  assert.equal(await page.evaluate(()=>FeedbackTuner.profile.basic.segments[3].curve),'rise');
  assert.equal(await page.evaluate(()=>FeedbackTuner.profile.basic.segments[4].low),0);
  await page.getByRole('button',{name:'구간 5 삭제',exact:true}).click();
  assert.equal(await page.locator('[data-segment]').count(),4);
  await page.locator('#timeline button').nth(0).click();await page.locator('[data-number="basic.segments.0.duration"]').fill('1000');
  await page.locator('#timeline button').nth(1).click();await page.locator('[data-number="basic.segments.1.duration"]').fill('1000');
  assert.equal(await page.locator('#save').isDisabled(),true);assert.match(await page.locator('#validation').innerText(),/2초/);
  await page.locator('[data-number="basic.segments.1.duration"]').fill('35');
  await page.locator('#timeline button').nth(0).click();await page.locator('[data-number="basic.segments.0.duration"]').fill('40');
  assert.equal(await page.locator('#save').isEnabled(),true);
  const levels=await page.evaluate(()=>[.02,.06,.12,.1975].map(time=>FeedbackTuner.samplePulse(FeedbackTuner.profile.basic,time)));
  assert.ok(levels[0].low>0);assert.deepEqual(levels[1],{low:0,high:0});assert.ok(levels[2].low>0);assert.ok(Math.abs(levels[3].low-.1)<1e-6);
  await padInput({});await page.evaluate(()=>hardware.trace=[]);await padInput({0:1});await page.waitForFunction(()=>!FeedbackTuner.playing);
  const trace=await page.evaluate(()=>hardware.trace);const first=trace.findIndex(level=>level[0]>0||level[1]>0),rest=trace.findIndex((level,index)=>index>first&&level[0]===0&&level[1]===0);
  assert.ok(first>=0&&rest>first&&trace.slice(rest+1).some(level=>level[0]>0||level[1]>0),'controller playback must resume after the silent segment');
  await padInput({});await page.locator('#save').click();await page.waitForFunction(()=>!document.querySelector('#save-status').textContent.includes('미저장'));
  assert.equal(JSON.parse(fs.readFileSync(target)).basic.segments.length,4);
  assert.equal(fs.readFileSync(source,'utf8'),fs.readFileSync(target,'utf8'));
  await page.locator('[data-event="hurt"]').click();assert.match(await page.locator('#pulse-title').innerText(),/피격/);
  await page.locator('#gamepad').selectOption('0');await padInput({});
  const beforeSpace=await page.evaluate(()=>hardware.rumble.length);
  await page.locator('h1').click();await page.keyboard.press('Space');await frames();
  assert.equal(await page.evaluate(()=>FeedbackTuner.playing),false,'Space is no longer a preview shortcut');
  assert.equal(await page.evaluate(()=>hardware.rumble.length),beforeSpace);
  await padInput({0:1});
  await page.waitForFunction(()=>hardware.rumble.some(value=>value.strongMagnitude>0));
  await page.waitForFunction(()=>!FeedbackTuner.playing);
  const heldCount=await page.evaluate(()=>hardware.rumble.length);
  await page.waitForTimeout(300);assert.equal(await page.evaluate(()=>hardware.rumble.length),heldCount,'holding a face button must not replay the pulse');
  await padInput({});await padInput({2:1});
  await page.waitForFunction(count=>hardware.rumble.length>count,heldCount);
  await padInput({2:1,9:1});assert.equal(await page.evaluate(()=>FeedbackTuner.playing),false,'Options stops the controller test');
  assert.ok(await page.evaluate(()=>hardware.resets)>0);await padInput({});
  await page.locator('[data-number="readyStrength"]').fill('0');await page.locator('[data-number="chargingStrength"]').fill('0');
  assert.equal(await page.locator('#save').isEnabled(),true,'zero resistance must be editable and saveable');
  const zeroModes=await page.evaluate(()=>['ready','charge'].map(kind=>FeedbackTuner.sample(FeedbackTuner.profile,kind,.01,'both').left.mode));
  assert.deepEqual(zeroModes,['off','off']);
  const zeroPacket=await page.evaluate(()=>Array.from(FeedbackTuner.encodeReport(48,0,{low:0,high:0,left:{mode:'resistance',start:0,strength:0,frequency:0},right:{mode:'resistance',start:0,strength:0,frequency:0}})));
  assert.equal(zeroPacket[11],5);assert.equal(zeroPacket[22],5);assert.ok(zeroPacket.slice(12,22).every(value=>value===0));assert.ok(zeroPacket.slice(23,33).every(value=>value===0));
  await page.locator('[data-number="readyStrength"]').fill('2');await page.locator('[data-number="chargingStrength"]').fill('5');
  await page.locator('#connect-hid').click();await page.waitForFunction(()=>document.querySelector('#device-status').textContent.includes('DualSense'));
  assert.equal(await page.evaluate(()=>hardware.requests),1);await nativeInput();
  await page.locator('[data-preview="charge"]').click();await frames();
  assert.ok(await page.evaluate(()=>hardware.reports.every(value=>value[11]===5&&value[22]===5)),'selecting an effect must not start the triggers');
  await nativeInput({right:.75});
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[11]===0x21&&value[22]===5;});
  await nativeInput({left:.6,right:.75});
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[11]===0x21&&value[22]===0x21;});
  await nativeInput({left:.6});
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[11]===5&&value[22]===0x21;});
  await nativeInput({left:.6,faces:[0]});
  await page.waitForFunction(()=>hardware.reports.some(value=>value[4]>0&&value[11]===5&&value[22]===0x21));
  await page.evaluate(()=>window.dispatchEvent(new Event('blur')));
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value&&value[3]===0&&value[4]===0&&value[11]===5&&value[22]===5;});
  assert.equal(await page.evaluate(()=>FeedbackTuner.playing),false);
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));await frames();
  assert.equal(await page.evaluate(()=>FeedbackTuner.playing),false,'focus recovery cannot replay held buttons or triggers');
  await nativeInput();await nativeInput({left:.6});assert.equal(await page.evaluate(()=>FeedbackTuner.playing),true);
  await page.locator('#stop').click();await frames();assert.equal(await page.evaluate(()=>FeedbackTuner.playing),false,'held triggers stay stopped after the stop button');
  await nativeInput();
  await page.locator('[data-preview="kick"]').click();await frames();const usbKickStart=await page.evaluate(()=>hardware.reports.length);await nativeInput({right:.8});
  await page.waitForFunction(frequency=>hardware.reports.some(value=>value[11]===0x26&&value[20]===frequency),original.kickFrequency);
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[11]===5&&!FeedbackTuner.playing;});
  await page.waitForTimeout(100);
  assert.ok(await page.evaluate(start=>hardware.reports.slice(start).every(value=>value[11]!==0x21),usbKickStart),'USB kick must not leave resistance while R2 stays held');
  await nativeInput();await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[11]===5&&value[22]===5;});
  await page.locator('[data-preview="charge"]').click();await frames();
  await page.evaluate(()=>hardware.failNext=true);await nativeInput({right:.8});
  await page.waitForFunction(()=>document.querySelector('#message').textContent.includes('패드 출력 실패'));
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return !FeedbackTuner.playing&&value[11]===5&&value[22]===5;});
  await nativeInput();
  await page.locator('#disconnect-hid').click();await page.waitForFunction(()=>document.querySelector('#disconnect-hid').hidden);
  await page.evaluate(()=>{hardware.padConnected=false;hardware.device.collections=[{outputReports:[{reportId:0x31,items:[{reportSize:8,reportCount:77}]}],children:[]}];hardware.reports=[];});
  await page.locator('#connect-hid').click();await page.waitForFunction(()=>document.querySelector('#device-status').textContent.includes('Bluetooth'));
  await nativeInput({},'minimal');await nativeInput({faces:[3]},'minimal');
  await page.waitForFunction(()=>hardware.reports.some(value=>value[0]===0x31&&value[6]>0));
  await page.waitForFunction(()=>!FeedbackTuner.playing);await nativeInput({},'bluetooth');
  await page.locator('[data-preview="ready"]').click();await frames();await nativeInput({left:.5},'bluetooth');
  await page.waitForFunction(()=>hardware.reports.some(value=>value[0]===0x31&&value[24]===0x21));
  const bluetooth=await page.evaluate(()=>hardware.reports.find(value=>value[0]===0x31&&value[24]===0x21));
  assert.equal(bluetooth.length,78);assert.equal(bluetooth[2],0x10);
  const {crc32}=require('node:zlib');
  assert.equal(Buffer.from(bluetooth).readUInt32LE(74),crc32(Buffer.from([0xa2,...bluetooth.slice(0,74)])));
  await nativeInput({},'bluetooth');await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[13]===5&&value[24]===5;});
  await page.locator('[data-preview="kick"]').click();await frames();const bluetoothKickStart=await page.evaluate(()=>hardware.reports.length);await nativeInput({left:.7},'bluetooth');
  await page.waitForFunction(()=>hardware.reports.some(value=>value[0]===0x31&&value[24]===0x26));
  await page.waitForFunction(()=>{const value=hardware.reports.at(-1);return value?.[24]===5&&!FeedbackTuner.playing;});
  assert.ok(await page.evaluate(start=>hardware.reports.slice(start).every(value=>value[24]!==0x21),bluetoothKickStart),'Bluetooth kick must not leave resistance while L2 stays held');
  await nativeInput({},'bluetooth');
  const decoded=await page.evaluate(()=>{
   const results=[];
   for(const length of [9,77]){const buffer=new Uint8Array(length+6);buffer[6+4]=0x90;buffer[6+7]=64;buffer[6+8]=192;results.push(FeedbackTuner.decodeInputReport(1,new DataView(buffer.buffer,6,length)));}
   results.push(FeedbackTuner.decodeInputReport(99,new DataView(new ArrayBuffer(9))));return results;
  });
  assert.deepEqual(decoded[0].faces,[false,false,true,true]);assert.deepEqual(decoded[1],decoded[0]);assert.equal(decoded[0].left,64/255);assert.equal(decoded[0].right,192/255);assert.equal(decoded[2],null);
  const crc=await page.evaluate(()=>{const bytes=FeedbackTuner.encodeReport(78,3,{low:0,high:0,left:{mode:'off'},right:{mode:'off'}});return new DataView(bytes.buffer).getUint32(74,true);});
  assert.equal(crc,0x03faed64,'Bluetooth output matches the independent CRC vector');
  const sequence=await page.evaluate(()=>[.25,.75,1.51,1.9].map(time=>FeedbackTuner.sample(FeedbackTuner.profile,'sequence',time,'both').left));
  assert.deepEqual(sequence.map(value=>value.mode),['resistance','resistance','vibration','off']);
  assert.equal(sequence[0].strength,2);assert.equal(sequence[1].strength,5);
  assert.equal(await page.evaluate(()=>{const profile={...FeedbackTuner.profile,readyStrength:5,chargingStrength:5};return FeedbackTuner.sample(profile,'kick',profile.kickDuration,'left').phase;}),'출력 없음');
  const downloadPromise=page.waitForEvent('download');await page.locator('#download').click();const download=await downloadPromise;
  assert.equal(download.suggestedFilename(),'gamepad_feedback.json');const downloaded=path.join(output,'downloaded.json');await download.saveAs(downloaded);
  assert.deepEqual(JSON.parse(fs.readFileSync(downloaded)),await page.evaluate(()=>FeedbackTuner.profile));
  await page.locator('#file').setInputFiles({name:'bad.json',mimeType:'application/json',buffer:Buffer.from('{"schemaVersion":9}')});
  await page.waitForFunction(()=>document.querySelector('#message').textContent.includes('불러오기 실패'));
  assert.equal(await page.evaluate(()=>FeedbackTuner.profile.schemaVersion),2);
  const beforeBadPattern=await page.evaluate(()=>FeedbackTuner.profile);
  for(const segments of [[],[{low:.2,high:.4,duration:.1,curve:'bad'}],Array.from({length:3},()=>({low:.2,high:.4,duration:.8,curve:'hold'}))]){
   const bad=structuredClone(normalized);bad.hit={segments};
   await page.locator('#file').setInputFiles({name:'bad-pattern.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(bad))});
   await frames();assert.deepEqual(await page.evaluate(()=>FeedbackTuner.profile),beforeBadPattern);
  }
  const imported=structuredClone(original);imported.kickFrequency=90;
  await page.locator('#file').setInputFiles({name:'import.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(imported))});
  await page.waitForFunction(()=>FeedbackTuner.profile.kickFrequency===90);
  const external=structuredClone(original);external.kickFrequency=51;fs.writeFileSync(source,JSON.stringify(external));
  await page.locator('#save').click();await page.waitForFunction(()=>document.querySelector('#message').textContent.includes('다른 곳'));
  assert.deepEqual(JSON.parse(fs.readFileSync(source)),external,'stale browser save must preserve external edits');
  assert.equal(await page.evaluate(()=>FeedbackTuner.profile.kickFrequency),90,'conflict keeps the browser draft');
  await page.locator('#reload').click();await page.waitForFunction(()=>FeedbackTuner.profile.kickFrequency===51);
  await page.locator('#disconnect-hid').click();await page.locator('#data-details').evaluate(element=>element.open=true);
  await page.locator('[data-number="kickFrequency"]').fill('72');
  await page.locator('#preset').selectOption('hurt');await page.locator('#apply-preset').click();
  await page.evaluate(()=>{document.querySelector('#message').hidden=true;window.scrollTo(0,0);});await frames();
  await page.screenshot({path:path.join(output,'desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:1000});await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'mobile width must not overflow');
  assert.deepEqual(errors,[],'no browser runtime errors');
  const offline=await context.newPage();await offline.goto(pathToFileURL(path.join(root,'tools/combat/gamepad_feedback_tuner.html')).href);
  await offline.waitForFunction(()=>document.querySelector('#source-status').textContent.includes('파일 편집'));
  await offline.locator('#file').setInputFiles({name:'profile.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(original))});
  await offline.waitForFunction(()=>window.FeedbackTuner?.profile);
  assert.equal(await offline.locator('#save').isDisabled(),true);
  assert.equal(await offline.locator('#download').isEnabled(),true);
  console.log('PASS browser: zero resistance, kick completion sends Off while held on USB/Bluetooth, pattern editing and controller playback, trigger input/CRC, source/catalog/conflicts, JSON/offline editing, desktop/mobile.');
 } finally {
  if(browser)await browser.close();server.kill();
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
