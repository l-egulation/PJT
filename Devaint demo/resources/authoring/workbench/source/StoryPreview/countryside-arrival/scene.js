(function(root){
const BREATH=[180,180,180,180,170,160,160,160];
function frameAt(ms,durations){let t=ms%durations.reduce((a,b)=>a+b,0);for(let i=0;i<durations.length;i++){if(t<durations[i])return i;t-=durations[i];}return 0;}
function sample(t){
 t=Math.max(0,t);
 let x=-45,phase='establish',clip='walk',frame=0;
 if(t>=450&&t<2450){phase='run';x=-45+(t-450)*.24;frame=Math.floor((t-450)/50)%12;}
 else if(t>=2450&&t<2825){phase='brake';let u=(t-2450)/375;x=435+45*(2*u-u*u);frame=Math.floor((t-450)/65)%12;}
 else if(t>=2825&&t<3095){phase='prepare';x=480;clip='prep';frame=Math.min(2,Math.floor((t-2825)/90));}
 else if(t>=3095){phase='breath';x=480;clip='breath';frame=frameAt(t-3095,BREATH);}
 let line='',lineStart=0;
 if(t>=900&&t<3000){line='할머니!!!';lineStart=900;}
 else if(t>=3500){line='헉… 헉…';lineStart=3500;}
 return {t,x:Math.round(x),y:388,phase,clip,frame,line,lineStart};
}
const api={sample,frameAt,BREATH};if(typeof module!=='undefined')module.exports=api;else root.Arrival=api;
})(typeof window!=='undefined'?window:this);
