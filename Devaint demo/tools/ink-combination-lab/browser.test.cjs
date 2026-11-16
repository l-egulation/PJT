const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.INK_TEST_PLAYWRIGHT_PATH||'playwright');
const output=path.resolve(__dirname,'../../outputs/ink-combination-lab');
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.INK_TEST_BROWSER_PATH?{executablePath:process.env.INK_TEST_BROWSER_PATH}:{})});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1200},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.evaluate(()=>{const store=new Map();Object.defineProperty(window,'localStorage',{value:{getItem:k=>store.get(k)||null,setItem:(k,v)=>store.set(k,v)}});});
  const html=fs.readFileSync(path.join(output,'ink-parts.html'),'utf8');
  await page.setContent(html,{waitUntil:'load'});
  await page.waitForFunction(()=>window.inkLab&&document.querySelectorAll('[data-equip]').length===50);
  assert.equal(await page.locator('[data-equip]').count(),50);
  await page.locator('#attack-a').click();await page.locator('#attack-b').click();
  const state=await page.evaluate(()=>({active:inkLab.sim.active,damage:inkLab.sim.stats.damage,detonations:inkLab.sim.stats.detonations,events:inkLab.sim.events.map(e=>({type:e.type,owner:e.owner,part:e.part}))}));
  assert.equal(state.active,1);assert.ok(state.damage>0);assert.ok(state.detonations>0);
  const sw=state.events.findIndex(e=>e.type==='switch');assert.ok(state.events.slice(0,sw).some(e=>e.type==='proc'&&e.part==='ink2_red_04'));
  await page.locator('#edit-b').click();await page.locator('[data-remove="1:0"]').click();await page.locator('[data-equip="ink2_blue_04"]').click();
  assert.ok(await page.evaluate(()=>inkLab.loadout[1].includes('ink2_blue_04')));
  await page.locator('#color').selectOption('purple');assert.equal(await page.locator('[data-equip]').count(),10);
  await page.locator('#trigger').selectOption('detonate');assert.equal(await page.locator('[data-equip]').count(),2);
  await page.locator('#query').fill('없는파츠검색어');assert.ok(await page.locator('#empty').isVisible());
  await page.locator('#query').fill('');await page.locator('#trigger').selectOption('all');await page.locator('#color').selectOption('all');
  await page.locator('.settings>summary').click();await page.locator('#markMode').selectOption('manual');
  await page.locator('#manual-mark').click();assert.equal(await page.evaluate(()=>inkLab.sim.enemies[0].marks.length),1);
  await page.locator('#internalCooldown').fill('0');await page.locator('#internalCooldown').dispatchEvent('change');
  assert.equal(await page.evaluate(()=>inkLab.options.internalCooldown),0);
  await page.locator('.settings>summary').click();await page.locator('#reset').click();
  await page.locator('#arena').click({position:{x:10,y:100}});
  const before=await page.evaluate(()=>inkLab.sim.player.x);await page.keyboard.down('ArrowRight');await page.waitForTimeout(250);await page.keyboard.up('ArrowRight');assert.ok(await page.evaluate(()=>inkLab.sim.player.x)>before);
  await page.locator('#pause').click();const time=await page.evaluate(()=>inkLab.sim.time);await page.waitForTimeout(120);assert.equal(await page.evaluate(()=>inkLab.sim.time),time);await page.locator('#pause').click();
  await page.locator('#fit-view').click();assert.equal(await page.locator('#fit-view').getAttribute('aria-pressed'),'true');await page.locator('#fit-view').click();
  await page.locator('#reset').click();await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:path.join(output,'simulator-desktop.png')});
  await page.setViewportSize({width:390,height:1000});await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:path.join(output,'simulator-mobile.png')});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1),'mobile must not overflow');
  assert.deepEqual(errors,[],'browser runtime errors');
  console.log('PASS browser: boot, 50-part equip/remove, real attack/switch/detonation, filters/search, experimental controls, movement, pause, mobile width; no page errors');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
