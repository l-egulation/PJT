const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert'),crypto=require('crypto');
const canvasPackage=process.env.CANVAS_PACKAGE||'@napi-rs/canvas';
const {createCanvas,Image}=require(canvasPackage),root=__dirname;
const scene=createCanvas(960,540),textOverlay=createCanvas(1920,1080),events={};let next;
scene.focus=()=>{};scene.style={};scene.getBoundingClientRect=()=>({left:0,top:0,width:1920,height:1080});
const elements={scene,textOverlay,sceneFull:{requestFullscreen(){}},message:{},pause:{},reset:{},full:{},deskDemo:{}};
const scope={Image:class extends Image{set src(value){super.src=path.isAbsolute(value)||value.startsWith('data:')?value:path.join(root,value)}get src(){return super.src}},Promise,Map,Set,Math,console,setTimeout:()=>0,requestAnimationFrame:fn=>next=fn,addEventListener:(n,fn)=>events[n]=fn,document:{getElementById:id=>elements[id],createElement:()=>createCanvas(1,1)},window:{devicePixelRatio:1}};
vm.createContext(scope);
const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
assert(!/href="(?:글자_|서가_|말풍선_|깃발_|도서관_밝은)/.test(html));
vm.runInContext(html.match(/<script>([\s\S]*?)<\/script>/)[1],scope);
function capture(){const c=createCanvas(1920,1080),x=c.getContext('2d');x.imageSmoothingEnabled=false;x.drawImage(scene,0,0,1920,1080);x.drawImage(textOverlay,0,0);return c;}
(async()=>{
 const until=Date.now()+30000;while(!scope.window.reviewState.ready){if(Date.now()>until)throw Error(elements.message.textContent||'Scene load timeout');await new Promise(r=>setTimeout(r,10))}
 const config=JSON.parse(fs.readFileSync(path.join(root,'scene_config.json'),'utf8'));
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'asset_manifest.json'),'utf8'));
 for(const entry of manifest.assets){const b=fs.readFileSync(path.join(root,entry.path));assert.equal(crypto.createHash('sha256').update(b).digest('hex'),entry.sha256)}
 assert(!('reference' in config));assert.equal(config.layers.filter(o=>o.motion==='wisp').length,16);
 assert(!config.layers.some(o=>o.motion==='libraryShelf'||o.motion==='libraryBook'));
 vm.runInContext('paused=true;t=1.25;x=512',scope);next(1000);const stable=capture().toBuffer('image/png');next(1033);assert(stable.equals(capture().toBuffer('image/png')));
 fs.writeFileSync(path.join(root,'verification-screen.png'),stable);
 assert(scope.window.reviewState.textRendering.separate);assert.equal(textOverlay.width,1920);
 elements.deskDemo.onclick();next(1066);assert(scope.window.speechReview.active);
 events.keydown({key:'e',preventDefault(){}});next(1083);assert(scope.window.speechReview.visible>0);
 events.keydown({key:'Escape',preventDefault(){}});next(1100);assert(!scope.window.speechReview.active);assert(!scope.window.reviewState.objectiveComplete);
 elements.deskDemo.onclick();for(let i=0;scope.window.speechReview.active&&i<100;i++){events.keydown({key:'e',preventDefault(){}});next(1116+i*17)}
 assert(scope.window.reviewState.objectiveComplete);
 elements.reset.onclick();next(3000);assert.equal(scope.window.reviewState.heroX,512);
 vm.runInContext('x=126',scope);events.keydown({key:'e',preventDefault(){}});next(3016);assert.equal(scope.window.reviewState.interactionCount,1);
 const result={passed:true,stage:'1차 확정',date:'2026-10-19',runtime:'Native Canvas executing the final HTML; browser/Unity not directly tested',portableAssetCount:manifest.assets.length,heroScale:1,cameraX:0,blueWisps:16,floatingMidgroundRemoved:true,highResolutionText:true,checks:['All final PNG hashes match','No rejected comparison/reference resources','Pause freezes scene','Desk speech opens/reveals/cancels','Objective completes after full dialogue only','Reset and portal interaction work']};
 fs.writeFileSync(path.join(root,'verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1});
