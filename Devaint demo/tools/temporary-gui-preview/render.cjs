const fs=require('node:fs/promises');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require('playwright');
async function main(){
 const workspace=process.argv[2];
 if(!workspace)throw new Error('Usage: node render.cjs <workspace-root> <output-directory>');
 const out=path.resolve(process.argv[3]||path.join(workspace,'output/gui/temporary-gui-v3'));
 const assets=path.join(out,'assets');await fs.mkdir(assets,{recursive:true});
 const source=path.join(workspace,'03_개발/Worktrees/stage1-village-reference/outputs/village/phase2-final-polish-20261027/after/phase2_market-sketch.png');
 await fs.copyFile(source,path.join(assets,'game-market.png'));
 await fs.copyFile(path.resolve(__dirname,'../../unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/Galmuri11.ttf'),path.join(assets,'Galmuri11.ttf'));
 await fs.copyFile(path.resolve(__dirname,'../../unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/OFL.md'),path.join(assets,'OFL.md'));
 await fs.copyFile(path.join(__dirname,'index.html'),path.join(out,'index.html'));
 const browser=await chromium.launch({headless:true,...(process.env.GUI_BROWSER_PATH?{executablePath:process.env.GUI_BROWSER_PATH}:{})});
 const page=await browser.newPage({viewport:{width:2944,height:1280},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(pathToFileURL(path.join(out,'index.html')).href);await page.evaluate(()=>document.fonts.ready);
 await page.screenshot({path:path.join(out,'six-screens.png'),fullPage:true});
 for(const n of ['01','02','03','04','05','06'])await page.locator('#screen-'+n).screenshot({path:path.join(out,'screen-'+n+'.png')});
 for(const [n,name]of [[1,'death'],[2,'pause'],[3,'title']]){await page.locator(`[data-state="${n}"]`).click();await page.locator('#screen-05').screenshot({path:path.join(out,`screen-05-${name}.png`)});}
 await page.locator('[data-state="0"]').click();
 const count=await page.locator('.baggrid .slot').count();if(count!==15)throw new Error('Inventory capacity mismatch');
 const equipment=await page.locator('.equipment .slot').count();if(equipment!==14)throw new Error('Equipment slot count mismatch');
 await page.locator('.baggrid .slot').nth(2).click();if(!await page.locator('.baggrid .slot').nth(2).evaluate(el=>el.classList.contains('selected')))throw new Error('Selection failed');
 await page.goto(pathToFileURL(path.join(out,'index.html')).href+'?export');await page.evaluate(()=>document.fonts.ready);
 const sprites=path.join(out,'unity-sprites');await fs.mkdir(sprites,{recursive:true});
 const manifest={format:1,filterMode:'Point',compression:'Uncompressed',pixelsPerUnit:32,notes:'Flat uGUI preview components. Text and game data are separate. Panel borders are 4 px; verify final scaling in Unity.',sprites:[]};
 for(const el of await page.locator('[id^="asset-"]').all()){
  const id=(await el.getAttribute('id')).slice(6);const bounds=await el.boundingBox();
  await el.screenshot({path:path.join(sprites,id+'.png'),omitBackground:true});
  const sliced=!id.startsWith('icon-')&&!id.startsWith('ink-')&&!id.includes('fill');
  manifest.sprites.push({file:id+'.png',width:bounds.width,height:bounds.height,border:sliced?[4,4,4,4]:[0,0,0,0],imageType:sliced?'Sliced':'Simple'});
 }
 await fs.writeFile(path.join(sprites,'import-settings.json'),JSON.stringify(manifest,null,2));
 await browser.close();if(errors.length)throw new Error(errors.join('\n'));
 console.log(JSON.stringify({output:out,screens:9,sprites:manifest.sprites.length,bagSlots:count,equipmentSlots:equipment,pageErrors:errors.length},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1});
