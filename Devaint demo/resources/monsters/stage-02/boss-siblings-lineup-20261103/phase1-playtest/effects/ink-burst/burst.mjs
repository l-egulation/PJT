const times=[0.6, 0.85, 1, 1.025, 1.055, 1.08, 1.14, 1.2, 1.25, 1.31, 1.36, 1.41, 1.46, 1.51, 1.56, 1.61];
export function burstKey(age){if(age<times[0]||age>=1.68)return -1;for(let i=times.length-1;i>=0;i--)if(age>=times[i])return i;return -1;}
export function drawBurst(ctx,frames,x,foot,age,scale=1){const k=burstKey(age);if(k<0)return;ctx.save();ctx.imageSmoothingEnabled=false;ctx.drawImage(frames[k],Math.round(x),Math.round(foot-64*scale),96*scale,64*scale);ctx.restore();}
