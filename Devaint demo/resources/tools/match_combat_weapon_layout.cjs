/* Preserve the user's edit and repeat weapon 1 geometry on weapon 2. */
'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert');
const group=path.resolve(__dirname,'../ui/combat-hud-expanded-20261106');
const core=require(path.join(group,'preview/editor-core.js'));
const source=JSON.parse(fs.readFileSync(path.join(group,'source/layout-user.json'),'utf8'));
const known=new Set(JSON.parse(fs.readFileSync(path.join(group,'runtime/sprites.json'),'utf8')).sprites.map(s=>s.id));
const doc=core.validate(source,known);
const geometry=['dx','dy','sx','sy','rotation','opacity','hidden','locked','stretch','fontSize','color'];
const pick=e=>Object.fromEntries(Object.entries(e).filter(([k])=>geometry.includes(k)));
function otherId(id){return id.replace('weapon-0.','weapon-1.').replace(/:(-?[\d.]+),(-?[\d.]+)$/,(_,x,y)=>':'+(Number(x)+370)+','+y);}
// Any conditional skill adornment follows its skill disc, including cooldown text.
const frame=doc.layers['weapon-0.sprite:266,808']??{};
const overlayAnchors={'skill-progress':[266,808],'ellipse:272,814':[272,814],'text:307,830':[307,830],
 'sprite:318,857':[318,857],'text:307,863':[307,863],'rect:312,865':[312,865],'text:334,864':[334,864],
 'sprite:271,872':[271,872],'sprite:284,872':[284,872],'sprite:297,872':[297,872]};
for(const [key,[x,y]] of Object.entries(overlayAnchors)){
 const id='weapon-0.'+key;if(doc.layers[id])continue;
 doc.layers[id]={dx:(frame.dx??0)+(x-266)*((frame.sx??1)-1),dy:(frame.dy??0)+(y-808)*((frame.sy??1)-1),sx:frame.sx??1,sy:frame.sy??1};
}
for(const id of Object.keys(doc.layers))if(id.startsWith('weapon-1.'))delete doc.layers[id];
for(const [id,edit] of Object.entries(doc.layers))if(id.startsWith('weapon-0.'))doc.layers[otherId(id)]=pick(edit);
if(doc.groups['weapon-0'])doc.groups['weapon-1']=pick(doc.groups['weapon-0']);
const result={...doc,previewState:source.previewState,coordinates:source.coordinates,
 sourceLayout:'source/layout-user.json',weaponLayoutRule:'Weapon 2 repeats weapon 1 local geometry at +370px X; weapon/skill identity and key labels remain independent.'};
for(const [id,e] of Object.entries(source.layers))if(!id.startsWith('weapon-1.'))assert.deepEqual(result.layers[id],e);
core.validate(result,known);
fs.writeFileSync(path.join(group,'runtime/layout-edited.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({matchedWeaponLayers:Object.keys(doc.layers).filter(k=>k.startsWith('weapon-1.')).length,sourcePreserved:true}));
