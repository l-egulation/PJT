// Shared presentation geometry: native dial bottom 301, hand pivot 245.
export const MIXER_SCALE=1.8, DIAL_BOTTOM=301, DIAL_PIVOT=245, FLOOR=464;
// Open 960 px arena: appear 6 units from the edge, travel 15 units, stop 3 units short of the far edge.
export const MIXER_START=Object.freeze({left:192,right:768}), MIXER_DISTANCE=480;
export const mixerCenterY=scale=>FLOOR-125*(scale/MIXER_SCALE);
const clamp=t=>Math.max(0,Math.min(1,t));
const ease=t=>{t=clamp(t);return t*t*(3-2*t)};
export function mixerPresentation(age,side){
 if(age<0||age>=6.5)return null;
 const start=MIXER_START[side],end=start+(side==='left'?1:-1)*MIXER_DISTANCE;
 if(age<.35){const p=clamp(age/.35);return {phase:'exit',x:480,y:280-560*p*p,scale:1,charge:0};}
 if(age<.5)return {phase:'hidden'};
 if(age<1){const p=ease((age-.5)/.5),scale=.65+(MIXER_SCALE-.65)*p;return {phase:'approach',x:start,y:mixerCenterY(scale)+160*(1-p),scale,charge:p};}
 if(age<5.5)return {phase:'active',x:start+(end-start)*clamp((age-1)/4.5),y:mixerCenterY(MIXER_SCALE),scale:MIXER_SCALE,charge:1};
 if(age<5.95){const p=ease((age-5.5)/.45),scale=MIXER_SCALE-.5*p;return {phase:'recede',x:end,y:mixerCenterY(scale)+380*p,scale,charge:1-p};}
 if(age<6.05)return {phase:'hidden'};
 const p=ease((age-6.05)/.45);return {phase:'return',x:480,y:280-560*(1-p),scale:1,charge:0};
}
