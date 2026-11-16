const clips={a1:{folder:'공격1_올려베기',times:[80,110,70,40,50,120]},a2:{folder:'공격2_내려베기',times:[40,100,40,110,100,110,120]},return:{folder:'공격1_복귀',times:[80,100,80]}};
class AttackPlayer{
 constructor(){this.state='idle';this.start=0;this.queued=false;this.window=120;}
 set(state,t){this.state=state;this.start=t;}
 update(t){for(let guard=0;guard<6;guard++){
  if(this.state==='idle')break;
  const length=this.state==='hold'?this.window:clips[this.state].times.reduce((a,b)=>a+b,0);
  if(t-this.start<length)break;
  const end=this.start+length;
  if(this.state==='a1'){if(this.queued){this.queued=false;this.set('a2',end);}else this.set('hold',end);}
  else if(this.state==='hold')this.set('return',end);
  else this.set('idle',end);
 }return this.frame(t);}
 click(t){this.update(t);if(this.state==='idle'){this.queued=false;this.set('a1',t);}
 else if(this.state==='a1')this.queued=true;
 else if(this.state==='hold')this.set('a2',t);}
 frame(t){if(this.state==='idle')return {src:'대기.png',index:0};
 if(this.state==='hold')return {src:'공격1_올려베기/06.png',index:5};
 const c=clips[this.state];let elapsed=t-this.start,index=0;
 while(index<c.times.length-1&&elapsed>=c.times[index]){elapsed-=c.times[index];index++;}
 return {src:c.folder+'/'+String(index+1).padStart(2,'0')+'.png',index};}
}
if(typeof module!=='undefined')module.exports={AttackPlayer,clips};



