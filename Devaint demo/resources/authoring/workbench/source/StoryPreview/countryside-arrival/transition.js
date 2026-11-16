(function(root){
 const BAR=72,SWAP=350,DURATION=1050;
 const clamp=v=>Math.max(0,Math.min(1,v));
 const smooth=v=>{const t=clamp(v);return t*t*(3-2*t);};
 function sample(ms){
  const t=Math.max(0,ms);
  const fade=t<SWAP?smooth(t/SWAP):t<550?1:1-smooth((t-550)/500);
  return {scene:t<SWAP?'library':'village',bars:t<SWAP?0:BAR,fade,arrivalTime:Math.max(0,t-DURATION),done:t>=DURATION};
 }
 const api={sample,BAR,SWAP,DURATION};if(typeof module!=='undefined')module.exports=api;else root.Cinematic=api;
})(typeof window!=='undefined'?window:this);
