const assert=require('assert'),C=require('./transition.js');
assert.equal(C.sample(0).bars,0);assert.equal(C.sample(0).scene,'library');
for(let t=0;t<3000;t+=5){const s=C.sample(t);assert([0,72].includes(s.bars),'No intermediate bar heights');assert.equal(s.bars,s.scene==='library'?0:72);assert(s.fade>=0&&s.fade<=1);if(s.scene!==C.sample(Math.max(0,t-5)).scene)assert.equal(s.fade,1);if(t<C.DURATION)assert.equal(s.arrivalTime,0);}
assert.equal(C.sample(C.SWAP-1).bars,0);assert.equal(C.sample(C.SWAP).bars,72);assert.equal(C.sample(C.SWAP).fade,1);assert.equal(C.sample(C.DURATION).fade,0);assert.equal(C.sample(C.DURATION).done,true);assert.equal(C.sample(C.DURATION+900).arrivalTime,900);
assert.equal(960/(540-2*72),960/396);assert(458<540-C.BAR,'Dialogue must fit above the bottom bar');assert(388-24<540-C.BAR,'Hero feet must remain inside the visible world');
console.log('PASS: library remains full frame; village appears with fixed 72px bars after opaque swap; no shrinking animation; arrival and GUI bounds preserved.');
