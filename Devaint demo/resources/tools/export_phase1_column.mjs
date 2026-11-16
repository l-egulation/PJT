// Bake the approved deterministic pixel simulation, preserving its ground anchor.
import {writeFileSync,mkdirSync} from 'node:fs';
import {WaterColumn,DT} from '../monsters/stage-02/boss-siblings-lineup-20261103/phase1-playtest/effects/ink-burst/water-column-spec/water-column.mjs';
const out=new URL('../monsters/stage-02/boss-siblings-lineup-20261103/unity-phase1/',import.meta.url);
mkdirSync(out,{recursive:true});
const width=240,height=220,frames=[],sim=new WaterColumn();sim.emit(120,{height:129,width:21,hold:.8,ringScale:3});
let pixels,fill='#000000';
const ctx={set fillStyle(v){fill=v},clearRect(){pixels.fill(0)},save(){},restore(){},translate(){},fillRect(x,y,w,h){
 const c=fill.match(/[a-f\d]{2}/gi).map(v=>parseInt(v,16));
 for(let yy=Math.round(y)+50;yy<Math.round(y)+50+h;yy++)for(let xx=Math.round(x);xx<Math.round(x)+w;xx++)if(xx>=0&&xx<width&&yy>=0&&yy<height){let i=(yy*width+xx)*4;pixels.set([...c,255],i)}
}};
for(let f=0;f<132;f++){pixels=new Uint8Array(width*height*4);sim.render(ctx,false,{transparent:true,floor:false});frames.push(Buffer.from(pixels));sim.step(DT)}
writeFileSync(new URL('ink-column.rgba',out),Buffer.concat(frames));
console.log('132 native-pixel frames; 42 x 129 px pillar; ground y=180');
