const { chromium }=require('C:/Users/SSAFY/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL}=require('node:url');
const path=require('node:path'),fs=require('node:fs'),assert=require('node:assert/strict');
const output=path.resolve(__dirname,'../../../outputs/monster_editor_checks');fs.mkdirSync(output,{recursive:true});
async function original(page){return page.evaluate(()=>Object.keys(arrays).every(id=>arrays[id].every((a,i)=>a.every((v,j)=>v===original[id][i][j]))));}
(async()=>{const browser=await chromium.launch({channel:'chrome',headless:true});try{
 const context=await browser.newContext({viewport:{width:1440,height:1050},acceptDownloads:true});const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(path.join(__dirname,'index.html')).href);await page.waitForFunction(()=>document.getElementById('status').textContent.includes('준비 완료'));assert(await original(page));
 await page.screenshot({path:path.join(output,'editor.png'),fullPage:true});
 // Draw, fill, erase, undo, redo using the actual editor controls.
 let b=await page.locator('#edit').boundingBox();await page.mouse.click(b.x+3,b.y+3);assert.equal(await page.evaluate(()=>arrays[selected][frame][3]),255);
 await page.locator('#customColor').fill('#eba516');await page.locator('[data-tool=fill]').click();await page.mouse.click(b.x+3,b.y+3);assert.equal(await page.evaluate(()=>arrays[selected][frame][0]),235);
 await page.locator('[data-tool=erase]').click();await page.mouse.click(b.x+3,b.y+3);assert.equal(await page.evaluate(()=>arrays[selected][frame][3]),0);
 await page.locator('#undo').click();assert.equal(await page.evaluate(()=>arrays[selected][frame][3]),255);await page.locator('#redo').click();assert.equal(await page.evaluate(()=>arrays[selected][frame][3]),0);
 await page.locator('#undo').click();await page.locator('#undo').click();await page.locator('#undo').click();assert(await original(page));
 // Ctrl+K and batched undo; ink-eye must remain untouched.
 await page.locator('#motion').selectOption('멧돼지/걷기');await page.locator('h1').click();await page.keyboard.press('Control+k');assert(await page.locator('#replacePanel').isVisible());
 await page.locator('#fromColor').fill('#1b1616');await page.locator('#toColor').fill('#332233');await page.locator('#scope').selectOption('species');await page.locator('#replaceApply').click();
 assert(await page.evaluate(()=>specs.filter(m=>m.species==='멧돼지').every(m=>arrays[m.id].every(a=>{for(let p=0;p<a.length;p+=4)if(a[p+3]&&a[p]===27&&a[p+1]===22&&a[p+2]===22)return false;return true;}))));
 assert(await page.evaluate(()=>specs.filter(m=>m.species==='잉크눈').every(m=>arrays[m.id].every((a,i)=>a.every((v,j)=>v===original[m.id][i][j])))));
 await page.locator('#undo').click();assert(await original(page));
 // Different canvas geometry and preview timings.
 await page.locator('#motion').selectOption('멧돼지/돌진');assert.equal(await page.locator('#edit').getAttribute('width'),'960');assert.equal(await page.locator('#preview').getAttribute('width'),'160');
 await page.locator('#play').click();await page.waitForFunction(()=>document.getElementById('timing').textContent.startsWith('2/12'));await page.locator('#play').click();
 // PNG and ZIP exported by the browser.
 let event=page.waitForEvent('download');await page.locator('#png').click();await (await event).saveAs(path.join(output,'frame.png'));
 event=page.waitForEvent('download');await page.locator('#zip').click();await (await event).saveAs(path.join(output,'assets.zip'));
 // IndexedDB restoration and portable JSON.
 await page.locator('[data-tool=pen]').click();await page.locator('#edit').scrollIntoViewIfNeeded();b=await page.locator('#edit').boundingBox();await page.mouse.click(b.x+3,b.y+3);await page.waitForFunction(()=>document.getElementById('status').textContent.includes('자동 저장됨'));
 event=page.waitForEvent('download');await page.locator('#save').click();await (await event).saveAs(path.join(output,'work.json'));
 await page.reload();await page.waitForFunction(()=>document.getElementById('status').textContent.includes('복원'));await page.locator('#motion').selectOption('멧돼지/돌진');assert.equal(await page.evaluate(()=>arrays[selected][0][3]),255);
 page.once('dialog',d=>d.accept());await page.locator('#reset').click();assert(await original(page));
 await page.locator('#load').setInputFiles(path.join(output,'work.json'));await page.waitForFunction(()=>arrays['멧돼지/돌진'][0][3]===255);assert.equal(await page.locator('#edit').getAttribute('width'),'960');
 // Direct-write backend is mocked: validate exact paths without touching final assets.
 const result=await page.evaluate(async()=>{const writes=new Map();function dir(prefix=''){return {getDirectoryHandle:async name=>dir(prefix+name+'/'),getFileHandle:async name=>({createWritable:async()=>({write:async blob=>writes.set(prefix+name,blob),close:async()=>{}})})};}directory=dir();await writeDirectory();const paths=[...writes.keys()];return {count:paths.length,pngs:paths.filter(p=>/\/[0-9]{2}\.png$/.test(p)).length,hasZip:paths.includes('잉크눈_멧돼지_최종픽셀.zip'),hasPreview:paths.includes('멧돼지/4_픽셀128/돌진/미리보기_3배.gif')};});assert.equal(result.pngs,48);assert(result.hasZip&&result.hasPreview);
 assert.deepEqual(errors,[]);fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,checks:['pen/fill/erase','undo/redo','Ctrl+K batch replacement','128/160 dimensions','playback','PNG/ZIP export','IndexedDB restore','JSON import/export','folder writer mock'],directWrite:result},null,2));console.log('PASS: browser editor interactions, file exports and persistence; no JS errors.');
 await context.close();
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1);});
