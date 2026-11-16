const $=s=>document.querySelector(s);
const paths=['assets/fused-idle.png','assets/fused-arrival.png','assets/blue-roar.png','assets/player.png','assets/room.png'];
const images={};
window.reviewState={ready:false,errors:[]};
try{
 await Promise.all(paths.map(path=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[path]=im;resolve();};im.onerror=()=>reject(new Error(`Image failed: ${path}`));im.src=path;})));
 await $('#start').decode();
 const response=await fetch('../source/homing-aether-prompt.txt');if(!response.ok)throw new Error(`Prompt HTTP ${response.status}`);
 const prompt=await response.text();$('#prompt').textContent=prompt;
 $('#copy').onclick=async()=>{try{await navigator.clipboard.writeText(prompt);$('#copy').textContent='복사 완료';}catch{const range=document.createRange();range.selectNodeContents($('#prompt'));const sel=window.getSelection();sel.removeAllRanges();sel.addRange(range);$('#copy').textContent='선택됨 · Ctrl+C로 복사';}};
 function drawPoses(){for(const [id,file]of[['idle','fused-idle'],['arrival','fused-arrival']]){const canvas=$(`#${id}`),ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.fillStyle=$('#background').value;ctx.fillRect(0,0,384,256);ctx.drawImage(images[`assets/${file}.png`],0,0);canvas.style.width=`${384*Number($('#zoom').value)}px`;canvas.style.height=`${256*Number($('#zoom').value)}px`;}}
 function drawMap(){const canvas=$('#map'),ctx=canvas.getContext('2d'),room=images['assets/room.png'];ctx.imageSmoothingEnabled=false;ctx.drawImage(room,0,0);ctx.drawImage(room,0,445,288,2,544,445,288,2);for(const[file,x]of[['player',310],['fused-idle',650]])ctx.drawImage(images[`assets/${file}.png`],x-192,445-232);canvas.style.width=`${960*Number($('#screenZoom').value)}px`;canvas.style.height=`${540*Number($('#screenZoom').value)}px`;}
 $('#background').onchange=drawPoses;$('#zoom').onchange=drawPoses;$('#screenZoom').onchange=drawMap;drawPoses();drawMap();
 window.reviewState={ready:true,errors:[],loaded:paths.length+1,imageSizes:Object.fromEntries(Object.entries(images).map(([k,v])=>[k,[v.naturalWidth,v.naturalHeight]]))};
 $('#loadState').textContent=`이미지 ${paths.length+1}개 로딩 완료 · 승인 픽셀 보존 · 새 공격 프레임 대기`;
}catch(error){window.reviewState.errors.push(String(error));$('#loadState').textContent=String(error);$('#loadState').style.color='#ff9999';throw error;}
