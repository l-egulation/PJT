const { chromium } = require('playwright');
const path = require('node:path');
const fs = require('node:fs');
const output = path.resolve(__dirname, '../../outputs/team-share');
const { pathToFileURL } = require('node:url');
(async()=>{
  const browser=await chromium.launch({headless:true});
  const page=await browser.newPage({viewport:{width:1440,height:1000},colorScheme:'dark'});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  // Verify that the portable document works without any network access.
  await page.route('https://**/*',route=>route.abort());
  await page.goto(pathToFileURL(path.join(output,'chapter1-playtest-share.html')).href,{waitUntil:'load'});
  await page.evaluate(async()=>{for(const im of document.querySelectorAll('img[src]')){im.loading='eager';await im.decode();}});
  const content=await page.evaluate(()=>({sections:document.querySelectorAll('main section').length,rooms:document.querySelectorAll('.room').length,connectors:document.querySelectorAll('.connector').length,images:document.querySelectorAll('.shot img').length,broken:[...document.querySelectorAll('.shot img')].filter(im=>!im.naturalWidth).length}));
  if(content.sections!==6||content.rooms!==12||content.connectors!==5||content.images!==31||content.broken)throw Error(JSON.stringify(content));
  await page.screenshot({path:path.join(output,'preview-desktop.png')});
  await page.locator('#rooms').scrollIntoViewIfNeeded();
  await page.screenshot({path:path.join(output,'preview-rooms.png')});
  await page.locator('#rooms .shot').first().click();
  if(!await page.locator('#lightbox').isVisible())throw Error('Image viewer did not open');
  await page.locator('#pixel-size').click();
  if(await page.locator('#pixel-size').getAttribute('aria-pressed')!=='true')throw Error('Original-size toggle failed');
  await page.keyboard.press('Escape');
  if(await page.locator('#lightbox').isVisible())throw Error('Image viewer did not close');
  await page.locator('[data-map="1"]').click();
  if(!await page.locator('#map-1').isVisible()||await page.locator('#map-0').isVisible())throw Error('Map switch failed');
  await page.keyboard.press('ArrowRight');
  if(!await page.locator('#map-2').isVisible())throw Error('Keyboard map switch failed');
  const widths=[];
  for(const width of [1440,1024,768,375]){
    await page.setViewportSize({width,height:1000});
    const dimensions=await page.evaluate(()=>({viewport:innerWidth,body:document.documentElement.scrollWidth}));
    widths.push(dimensions);if(dimensions.body>dimensions.viewport+1)throw Error('Horizontal overflow '+JSON.stringify(dimensions));
  }
  await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(output,'preview-mobile.png')});
  await page.emulateMedia({colorScheme:'light'});
  if(errors.length)throw Error(errors.join('\n'));
  const result={pass:true,offline:true,...content,widths,imageViewer:true,mapTabs:true,keyboard:true,pageErrors:errors};
  fs.writeFileSync(path.join(output,'verification.json'),JSON.stringify(result,null,2));
  console.log(JSON.stringify(result));await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
