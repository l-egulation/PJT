const times=[.6,.7,.8,.9,1,1.09,1.18,1.27,1.36,1.45,1.54,1.63,1.72,1.81,1.90,1.99];
export function drawBurst(ctx,frames,x,foot,age,scale=1){
 if(age<times[0]||age>=2.08)return;
 let k=15;while(k>0&&age<times[k])k--;
 const heightScale=1.65;
 ctx.save();ctx.imageSmoothingEnabled=false;
 ctx.drawImage(frames[k],Math.round(x),Math.round(foot-(72*heightScale-2)*scale),96*scale,80*heightScale*scale);
 ctx.restore();
}
