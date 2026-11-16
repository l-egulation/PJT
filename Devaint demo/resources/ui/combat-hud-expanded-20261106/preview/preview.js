'use strict';
const {catalog:cat,sprites}=window.PREVIEW_DATA;
const stage=document.querySelector('#stage'),ctx=stage.getContext('2d');
const byId=Object.fromEntries(sprites.map(s=>[s.id,s]));
const images={},controls=[],state={hp:82,reveal:true,selected:0,weapons:[]};
const el=id=>document.getElementById(id);
let timer;
function options(select,items,value){select.replaceChildren(...items.map(([id,label])=>{const o=new Option(label,id);return o}));if(value!==undefined)select.value=value;}
function createSelect(items,value){const s=document.createElement('select');options(s,items,value);return s}
function label(title,input){const l=document.createElement('label');l.append(title+' ',input);return l}
for(let i=0;i<2;i++){
 const f=document.createElement('fieldset'),legend=document.createElement('legend');legend.textContent='무기 '+(i+1);f.append(legend);
 const weapon=createSelect([['','빈 무기'],...cat.weapons.map(w=>[w.id,w.name])],i?'hammer':'base_sword');
 const skill=createSelect([]),mode=createSelect([]);
 const status=createSelect([['ready','사용 가능'],['cooldown','쿨타임'],['charging','차징 중'],['charged','차징 완료']],'ready');
 const active=document.createElement('input');active.type='radio';active.name='active';active.checked=!i;
 const charges=createSelect([['1','1회'],['2','2회'],['3','3회'],['5','5회']],'1');
 const available=createSelect([['0','0회'],['1','1회']],'1');
 const progress=document.createElement('input');progress.type='range';progress.min=0;progress.max=100;progress.value=64;
 weapon.dataset.control='weapon';skill.dataset.control='skill';status.dataset.control='status';
 const row=document.createElement('div');row.className='controls';row.append(label('선택',active),weapon,skill,mode,status,label('회복/차징',progress),label('최대',charges),label('남음',available));
 const slots=document.createElement('div');slots.className='palette-fields';const meta=document.createElement('div');meta.className='meta';f.append(row,slots,meta);el('weapon-controls').append(f);
 const c={weapon,skill,mode,status,active,charges,available,progress,slots,meta,slotControls:[]};controls.push(c);
 const updateWeapon=()=>{const w=cat.weapons.find(w=>w.id===weapon.value);options(skill,(w?.specials??[]).map(id=>{const s=cat.skills.find(s=>s.id===id);return[id,s.name]}));skill.hidden=!w?.specials.length;mode.hidden=true;status.disabled=!w||w.specialPolicy==='unavailable';meta.textContent=w?.id==='launcher'?'장착 스킬에 따라 무기와 출발 카드의 외형이 바뀝니다. 기본 스킬은 차징 후 발사입니다.':'';sync();};
 weapon.onchange=updateWeapon;charges.onchange=()=>{options(available,Array.from({length:Number(charges.value)+1},(_,j)=>[String(j),j+'회']),charges.value);sync()};
 for(const s of [skill,mode,status,active,available,progress])s.oninput=sync;
 c.updateWeapon=updateWeapon;updateWeapon();
}
function rebuildSlots(){controls.forEach((c,i)=>{const old=c.slotControls.map(s=>s.value);c.slotControls=[];c.slots.replaceChildren();for(let j=0;j<Number(el('slots').value);j++){
 const items=[['color:colorless','무색'],...cat.inkColors.map(c=>['color:'+c.color,c.name]),['empty','빈칸'],['locked','잠금'],...cat.inks.map(p=>['part:'+p.id,p.name])];
 const select=createSelect(items,old[j]??'color:'+['blue','yellow','purple','green','red'][(j+i*2)%5]);select.title=(j+1)+'번 칸';select.onchange=sync;c.slots.append(select);c.slotControls.push(select);
}});sync()}
function sync(){
 state.hp=Number(el('hp').value);el('hp-value').textContent=state.hp;state.reveal=el('reveal').checked;state.boss=el('boss').checked;state.showPartIcons=el('parts').checked;
 state.selected=Math.max(0,controls.findIndex(c=>c.active.checked));state.weapons=controls.map(c=>({weaponId:c.weapon.value,skillId:c.skill.value,launcherMode:c.mode.value,status:c.status.value,cooldownFraction:Number(c.progress.value)/100,remainingSeconds:Number(c.progress.value)/20,chargeProgress:Number(c.progress.value)/100,chargeCapacity:Number(c.charges.value),availableCharges:Number(c.available.value),paletteSlots:c.slotControls.map(s=>{const v=s.value;if(v==='empty')return{color:null,partId:null};if(v==='locked')return{color:null,locked:true};if(v.startsWith('part:')){const p=cat.inks.find(p=>p.id===v.slice(5));return{color:p.color,partId:p.id}}return{color:v.slice(6),partId:null}})}));draw();
}
function draw(){const scene=DeviantUI.buildScene(cat,state);if(window.DeviantLayoutEditor){window.DeviantLayoutEditor.draw(scene);return;}if(!Object.keys(images).length)return;ctx.clearRect(0,0,1670,942);for(const l of scene.layers){ctx.save();
 if(l.type==='sprite'){
  const img=images[l.sprite];if(!img){ctx.restore();continue}let[x,y,w,h]=l.rect;
  if(l.fill!==undefined){ctx.beginPath();ctx.rect(x,y,w*l.fill,h);ctx.clip()}
  if(l.radial!==undefined){ctx.beginPath();ctx.moveTo(x+w/2,y+h/2);ctx.arc(x+w/2,y+h/2,Math.max(w,h),-Math.PI/2,-Math.PI/2+l.radial*Math.PI*2);ctx.closePath();ctx.clip()}
  if(l.contain){const s=Math.min(w/img.width,h/img.height),nw=img.width*s,nh=img.height*s;x+=(w-nw)/2;y+=(h-nh)/2;w=nw;h=nh}
  if(l.flip){ctx.translate(x+w,y);ctx.scale(-1,1);ctx.drawImage(img,0,0,w,h)}else ctx.drawImage(img,x,y,w,h);
 }else if(l.type==='text'){ctx.fillStyle=l.color;ctx.font=`700 ${l.size}px "Malgun Gothic",sans-serif`;ctx.textBaseline='top';ctx.textAlign=l.center?'center':'left';ctx.fillText(l.text,l.x,l.y+5)}
 else if(l.type==='path'){ctx.strokeStyle=l.color;ctx.lineWidth=l.width;ctx.beginPath();l.points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.stroke()}
 else{ctx.fillStyle=l.color;const[x,y,w,h]=l.rect;if(l.type==='ellipse'){ctx.beginPath();ctx.ellipse(x+w/2,y+h/2,w/2,h/2,0,0,2*Math.PI);ctx.fill()}else ctx.fillRect(x,y,w,h)}ctx.restore();}}
el('slots').onchange=rebuildSlots;for(const id of ['hp','reveal','boss','parts'])el(id).oninput=sync;
el('play').onclick=()=>{clearTimeout(timer);el('reveal').checked=true;sync();timer=setTimeout(()=>{el('reveal').checked=false;sync()},cat.palette.revealSeconds*1000)};
let bg=0;el('background').onclick=()=>{bg=(bg+1)%3;stage.className=['','checker','dark'][bg];draw()};
rebuildSlots();
function validateState(input){
 if(!input||typeof input!=='object'||!Array.isArray(input.weapons)||input.weapons.length!==2)throw Error('무기 조건이 올바르지 않습니다.');
 const clean=JSON.parse(JSON.stringify(input));
 for(const w of clean.weapons){
  const legacy=cat.legacyWeaponSelections?.[w?.weaponId];if(legacy)Object.assign(w,legacy,{launcherMode:''});
  if(!w||(!cat.weapons.some(a=>a.id===w.weaponId)&&w.weaponId!==''))throw Error('무기 종류를 확인해 주세요.');
  if(!Array.isArray(w.paletteSlots)||![3,4,5].includes(w.paletteSlots.length))throw Error('팔레트는 3~5칸이어야 합니다.');
  for(const s of w.paletteSlots){if(!s||!['red','yellow','green','blue','purple','colorless',null].includes(s.color??null))throw Error('팔레트 색상이 올바르지 않습니다.');if(s.partId&&!cat.inks.some(p=>p.id===s.partId))throw Error('잉크 종류를 확인해 주세요.');}
  for(const k of ['cooldownFraction','chargeProgress'])if(w[k]!==undefined&&(!Number.isFinite(w[k])||w[k]<0||w[k]>1))throw Error('진행률이 올바르지 않습니다.');
  if(w.chargeCapacity!==undefined&&(!Number.isInteger(w.chargeCapacity)||w.chargeCapacity<1||w.chargeCapacity>10))throw Error('사용 횟수가 올바르지 않습니다.');
 }
 if(clean.weapons[0].paletteSlots.length!==clean.weapons[1].paletteSlots.length)throw Error('두 무기의 팔레트 칸 수가 같아야 합니다.');
 return clean;
}
function setState(input){
 const s=validateState(input);el('hp').value=Math.max(0,Math.min(100,Number(s.hp) || 0));el('reveal').checked=!!s.reveal;el('boss').checked=!!s.boss;el('parts').checked=!!s.showPartIcons;
 el('slots').value=String(s.weapons[0].paletteSlots.length);rebuildSlots();
 controls.forEach((c,i)=>{const w=s.weapons[i];c.weapon.value=w.weaponId;c.updateWeapon();if(w.skillId&&[...c.skill.options].some(o=>o.value===w.skillId))c.skill.value=w.skillId;
  c.status.value=['ready','cooldown','charging','charged'].includes(w.status)?w.status:'ready';c.active.checked=i===(s.selected??0);
  const capacity=w.chargeCapacity??1;options(c.charges,Array.from({length:10},(_,j)=>[String(j+1),(j+1)+'회']),String(capacity));options(c.available,Array.from({length:capacity+1},(_,j)=>[String(j),j+'회']),String(Math.max(0,Math.min(capacity,w.availableCharges??capacity))));
  c.progress.value=Math.round((w.status==='charging'||w.status==='charged'?w.chargeProgress??1:w.cooldownFraction??.64)*100);
  c.slotControls.forEach((control,j)=>{const slot=w.paletteSlots[j];control.value=slot.locked?'locked':slot.partId?'part:'+slot.partId:slot.color?'color:'+slot.color:'empty';});
 });sync();
}
window.DeviantPreview={getState:()=>JSON.parse(JSON.stringify(state)),setState,validateState,getScene:()=>DeviantUI.buildScene(cat,state),images,refresh:draw};
if(window.DeviantLayoutEditor)window.DeviantLayoutEditor.attach(window.DeviantPreview);
Promise.all(sprites.map(s=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[s.id]=im;resolve()};im.onerror=()=>reject(Error(s.file));im.src='../'+s.file}))).then(draw).catch(e=>el('error').textContent='이미지를 불러오지 못했습니다: '+e.message);
