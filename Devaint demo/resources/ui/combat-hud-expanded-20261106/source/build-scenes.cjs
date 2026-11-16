const fs=require('fs'),path=require('path');
const group=path.resolve(__dirname,'..');
const cat=JSON.parse(fs.readFileSync(path.join(group,'runtime/catalog.json'),'utf8'));
const {buildScene,paletteSlots}=require('../runtime/ui-model.js');
const scenarios=[
 {id:'melee-three-slots',title:'기본검 + 기본망치 / 3칸',hp:82,reveal:true,weapons:[
  {weaponId:'base_sword',skillId:'base_sword_range',paletteSlots:[{color:'blue'},{color:'yellow'},{color:'blue'}]},
  {weaponId:'hammer',skillId:'hammer_default',status:'cooldown',cooldownFraction:.64,remainingSeconds:3.2,paletteSlots:[{color:'red'},{color:'green'},{color:'yellow'}]}]},
 {id:'ranged-four-slots',title:'레이저 + 수리검 / 4칸',hp:64,reveal:true,selected:1,weapons:[
  {weaponId:'launcher',skillId:'launcher_laser',paletteSlots:paletteSlots(4,2,true)},
  {weaponId:'shuriken',skillId:'shuriken_movement',status:'cooldown',cooldownFraction:.32,remainingSeconds:1.6,chargeCapacity:3,availableCharges:1,paletteSlots:paletteSlots(4,3,true)}]},
 {id:'mixed-five-slots',title:'폭탄 + 방패 / 5칸',hp:38,reveal:true,weapons:[
  {weaponId:'bomb',skillId:'bomb_follow_up',chargeCapacity:3,availableCharges:3,paletteSlots:paletteSlots(5,0,true)},
  {weaponId:'shield',skillId:'shield_range',status:'charging',chargeProgress:.72,paletteSlots:paletteSlots(5,2,true)}]},
 {id:'empty-and-charged',title:'빈칸 + 차징 완료',hp:100,reveal:false,selected:1,weapons:[
  {weaponId:'',paletteSlots:[{color:null},{color:null},{color:null,locked:true}]},
  {weaponId:'greatsword',skillId:'greatsword_default',status:'charged',chargeCapacity:2,availableCharges:2,paletteSlots:[{color:'colorless'},{color:'purple'},{color:'blue'},{color:'green'},{color:null,locked:true}]}]},
 {id:'boss-state',title:'보스 전투 / 상단 간소화',hp:25,boss:true,reveal:false},
 {id:'ranged-stamp-weapons',title:'발사기 / 샷건 스킬 + 유탄포 스킬',hp:82,reveal:true,weapons:[
  {weaponId:'launcher',skillId:'launcher_shotgun',paletteSlots:paletteSlots(5,0,true)},
  {weaponId:'launcher',skillId:'launcher_grenade',paletteSlots:paletteSlots(5,2,true)}]},
 {id:'launcher-charge',title:'발사기 / 기본 차징 스킬 + 레이저',hp:88,reveal:true,weapons:[
  {weaponId:'launcher',skillId:'launcher_default',status:'charging',chargeProgress:.72,paletteSlots:paletteSlots(5)},
  {weaponId:'launcher',skillId:'launcher_laser',status:'ready',paletteSlots:paletteSlots(5,2)}]},
];
for(const s of scenarios){fs.writeFileSync(path.join(group,'preview',s.id+'.scene.json'),JSON.stringify(buildScene(cat,s),null,2)+'\n');}
fs.writeFileSync(path.join(group,'runtime','preview-scenarios.json'),JSON.stringify(scenarios,null,2)+'\n');
console.log(scenarios.length+' scenarios built');
