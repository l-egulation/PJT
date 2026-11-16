import assert from 'node:assert/strict';
import {fallAfterBreak,FLOOR,HITSTOP} from './phase-transition.mjs';
for(const y of [20,92,236,380,443]){const p={x:723,y,vy:0},start=y;assert(!fallAfterBreak(p,0,1/120));assert.equal(p.y,start);let landed=false;for(let age=HITSTOP;age<1.2;age+=1/120){const old=p.y;landed=fallAfterBreak(p,age,1/120);assert(p.y>=old);assert.equal(p.x,723);if(landed)break;}assert(landed);assert.equal(p.y,FLOOR);assert.equal(p.vy,0);assert(p.ground);}
console.log('PASS falling from every platform height; fixed horizontal position, hitstop, gravity and floor landing');
