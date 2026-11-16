'use strict';
const offsets=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]];
function canonical(mask){
 for(const i of [1,3,5,7]) if(!(mask&(1<<((i+7)%8)))||!(mask&(1<<((i+1)%8)))) mask&=~(1<<i);
 return mask;
}
function stone(occupied,x,y){
 let mask=0;
 offsets.forEach(([dx,dy],i)=>{if(occupied(x+dx,y+dy))mask|=1<<i;});
 // Coordinates are final world grid coordinates, with Y down.
 const variant=((x*13+y*7)%4+4)%4;
 return 'stone-'+canonical(mask).toString(16).toUpperCase().padStart(2,'0')+'-v'+variant;
}
function platform(index,length,leftWall=false,rightWall=false){
 if(!Number.isInteger(length)||length<1||!Number.isInteger(index)||index<0||index>=length)throw new RangeError('Whole positive cell length and valid index required');
 if(length===1)return 'single-'+(leftWall?'wall':'free')+'-'+(rightWall?'wall':'free');
 if(index===0)return 'left-'+(leftWall?'wall':'free');
 if(index===length-1)return 'right-'+(rightWall?'wall':'free');
 return ['middle-a','middle-b','middle-c'][(index-1)%3];
}
if(typeof module!=='undefined')module.exports={offsets,canonical,stone,platform};
