import assert from 'node:assert/strict';
import {createSequence,sequenceSchedule,sequenceTiming as T} from './sequence.mjs';
for(let i=0;i<7;i++){let n=0;const s=createSequence(7,()=>n++===0?(i+.1)/7:.4);assert.equal(s.safe,i);assert.equal(s.order.length,6);assert.equal(new Set(s.order).size,6);assert(!s.order.includes(i));const t=sequenceSchedule(s.order);assert(t.warningAt-t.flashEnd>=1.6-.0001);assert.equal(t.blastAt,t.warningAt+T.warning);assert(t.end>t.blastAt+5*T.blastGap);}
console.log('PASS seven possible safe platforms, unique six-target ordering, relocation grace, warning and sequential explosions');
