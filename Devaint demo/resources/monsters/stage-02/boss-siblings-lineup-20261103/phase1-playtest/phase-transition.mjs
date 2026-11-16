export const FLOOR=444,HITSTOP=.09;
export function fallAfterBreak(player,age,dt){if(age<HITSTOP)return false;player.vy=Math.max(0,player.vy)+2100*dt;player.y+=player.vy*dt;if(player.y>=FLOOR){player.y=FLOOR;player.vy=0;player.ground=true;return true;}player.ground=false;return false;}
