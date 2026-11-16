export const U=32;
const source=await (await fetch(new URL('./current-tuning.json',import.meta.url))).json();
const V=source.values;
export const rules={deviceHP:V.MachineHp,deviceW:V.MachineWidth*U,deviceH:V.MachineHeight*U,roseGap:V.Phase1ShotInterval,roseSpeed:V.Phase1ShotSpeed*U,orbSpeed:V.Phase1OrbSpeed*U,shotTell:V.Phase1ShotTell,arrowMin:V.Phase1ArrowMinDamage,arrowMax:V.Phase1ArrowMaxDamage,orbInterval:V.Phase1OrbInterval,rest:V.Phase1Rest,machineMoveInterval:V.MachineMoveInterval,burstTell:V.MachineBurstTell,rainStart:V.MachineBurstTell+V.PlatformRainTell,rainEnd:V.MachineBurstTell+V.PlatformRainTell+V.PlatformRainLife,trapWarn:V.TrapTell,trapEnd:V.TrapTell+V.TrapLife,snipeTrack:V.TrapTell+V.SniperDelay,snipeLock:V.TrapTell+V.SniperDelay+V.SniperTrack,snipeHit:V.TrapTell+V.SniperDelay+V.SniperTrack+V.SniperLock,rootSeconds:V.TrapRoot,fallProtection:.6};
export const layerY=[380,236,92];
export const platforms=await (await fetch(new URL('./current-platforms.json',import.meta.url))).json();
export function nearestLayer(centerY){let best=0;for(let i=1;i<3;i++)if(Math.abs(centerY-layerY[i])<Math.abs(centerY-layerY[best]))best=i;return best;}
export function overlap(a,b){return a.x<b.x+b.w&&a.x+a.w>b.x&&a.y<b.y+b.h&&a.y+a.h>b.y;}
export function fallIndex(random=Math.random){return Math.min(6,Math.floor(random()*7));}
export function trapState(t){return t<rules.trapWarn?'warning':t<rules.trapEnd?'active':'off';}
export function rainTicks(previous,current){return [rules.rainStart,rules.rainStart+V.PlatformRainTick].filter(t=>previous<t&&current>=t);}
export function chooseTraps(player,random=Math.random){
 const safe=Math.min(platforms.length-1,Math.floor(random()*platforms.length));
 return {safe,selected:platforms.map((_,i)=>i).filter(i=>i!==safe)};
}
export function nextMachinePlatform(current,random=Math.random){let n=Math.min(5,Math.floor(random()*6));return n>=current?n+1:n;}
