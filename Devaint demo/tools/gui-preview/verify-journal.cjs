// npm install --no-save playwright; node tools/gui-preview/verify-journal.cjs [preview-directory]
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../..'),dir=path.resolve(process.argv[2]||path.join(root,'resources/ui/journal-preview/preview'));
const output=path.join(root,'outputs/gui-preview');fs.mkdirSync(output,{recursive:true});
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{})}),p=await browser.newPage({viewport:{width:1500,height:1000}}),errors=[];
 p.on('pageerror',e=>errors.push(e.message));await p.goto(pathToFileURL(path.join(dir,'index.html')).href);await p.evaluate(()=>document.fonts.ready);
 const ids=await p.evaluate(()=>screens.map(s=>s[0]));let states=0;
 for(const variant of ['normal','poor','locked','empty','full'])for(const id of ids){await p.evaluate(([id,v])=>{state=initial();scenario=v;go(id)},[id,variant]);await p.locator('#stage img').evaluateAll(es=>Promise.all(es.map(i=>i.decode())));const issues=await p.evaluate(()=>[...document.querySelectorAll('#stage .paper,#stage .column,#stage .service')].filter(e=>e.scrollHeight>e.clientHeight+3||e.scrollWidth>e.clientWidth+3).map(e=>e.className));assert.deepEqual(issues,[],variant+':'+id);assert.equal(await p.locator('#stage').innerText().then(s=>s.includes('\u00b7')),false);states++}
 await p.evaluate(()=>{scenario='normal';state=initial();go('hub')});assert.equal(await p.locator('#hud').innerText().then(t=>/선택 도면|마법안료|가방/.test(t)),false);assert.equal(await p.locator('.hud-control').count(),3);
 await p.evaluate(()=>go('combat'));assert.equal(await p.locator('.boss-summary,.boss-health').count(),0);assert.ok(await p.locator('.combat-weapon').first().evaluate(e=>e.clientWidth<100));
 await p.evaluate(()=>go('boss'));assert.equal(await p.locator('.boss-health').count(),1);assert.equal(await p.locator('.combat-minimap').count(),0);await p.locator('#stage [data-action="go:pause"]').click();assert.equal(await p.locator('#screen .book').count(),0);assert.equal(await p.locator('.pause-buttons button').count(),3);await p.locator('.pause-buttons button').first().click();assert.equal(await p.evaluate(()=>state.screen),'boss');
 await p.evaluate(()=>go('departure'));await p.locator('[data-action="loadout:2"]').click();assert.deepEqual(await p.evaluate(()=>state.loadout),[1,2]);assert.equal(await p.locator('[aria-pressed="true"]').count(),2);
 await p.evaluate(()=>{state=initial();go('atelier')});assert.equal(await p.locator('.facility-visual .drop').count(),0);await p.locator('[data-action="offer:2"]').click();assert.equal(await p.locator('.colorless-slot').count(),1);
 await p.evaluate(()=>go('well'));assert.equal(await p.locator('.well-bubble').count(),1);assert.equal(await p.locator('#screen .book').count(),0);
 await p.evaluate(()=>go('map'));assert.equal(await p.locator('[data-marker="boss"],[data-marker="maintenance"]').count(),0);assert.equal(await p.locator('[data-room]').count(),1);
 assert.equal(await p.evaluate(()=>enterPreviewRoom(8)),false);for(let i=1;i<=8;i++)assert.equal(await p.evaluate(i=>enterPreviewRoom(i),i),true);
 assert.equal(await p.locator('[data-marker="boss"]').count(),1);await p.locator('#stage').screenshot({path:path.join(output,'map-boss-visited.png')});
 await p.evaluate(()=>{applyMapSnapshot({cols:520,rows:156,rooms:fixtureRooms,passages:fixturePassages,visited:[0,1,2,3],current:3});enterPreviewRoom(11);enterPreviewRoom(12)});assert.equal(await p.locator('[data-marker="maintenance"]').count(),1);assert.equal(await p.locator('[data-marker="boss"]').count(),0);
 const before=await p.evaluate(()=>mapSnapshot.visited.slice());await p.locator('[data-action="mapAll"]').click();assert.deepEqual(await p.evaluate(()=>mapSnapshot.visited),before);
 const snap=await p.evaluate(()=>({cols:100,rows:100,rooms:[{id:0,x:10,y:15,w:30,h:20,kind:'maintenance'}],passages:[],visited:[0],current:0}));await p.evaluate(s=>applyMapSnapshot(s),snap);assert.equal(await p.locator('[data-room="0"]').getAttribute('x'),'10');assert.equal(await p.locator('[data-marker="maintenance"]').count(),1);
 for(const id of ['hub','combat','boss','pause','map']){await p.evaluate(id=>go(id),id);await p.locator('#stage').screenshot({path:path.join(output,id+'.png')})}
 const links=await p.evaluate(()=>[...document.querySelectorAll('link[href],script[src],img[src]')].map(e=>e.href||e.src));for(const link of links)if(link.startsWith('file:'))assert.ok(fs.existsSync(require('node:url').fileURLToPath(link)),link);
 assert.deepEqual(errors,[]);console.log(JSON.stringify({screens:ids.length,states,interactionChecks:'compact HUD, boss-only bar, pause resume, weapon replacement, colourless slot, bubble, fog-of-war, facility markers, map snapshot',errors},null,2));await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
