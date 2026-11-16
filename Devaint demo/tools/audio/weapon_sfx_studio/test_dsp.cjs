const assert=require('assert');
const DSP=require('./dsp.js');
const SR=48000;
const sine=Float32Array.from({length:SR*2},(_,i)=>Math.sin(2*Math.PI*440*i/SR)*.1);
const event={id:'test',mode:'one',at:200,trimA:0,trimB:1000,pitch:0,gain:0,speed:1,pan:0,interval:60,count:4,duration:1000,spinHz:4,depth:.5,sweep:0};
function hz(samples){let crossings=0;const a=Math.round(samples.length*.2),b=Math.round(samples.length*.8);for(let i=a;i<b;i++)if(samples[i]<=0&&samples[i+1]>0)crossings++;return crossings/((b-a)/SR);}
for(const [pitch,speed,expected]of [[0,2,440],[12,1,880],[-12,1,220],[7,1.5,440*2**(7/12)]]){
 const output=DSP.transform(sine,SR,{...event,pitch,speed});
 assert(Math.abs(output.data.length-SR/speed)<=2,'Independent speed determines duration');
 assert(Math.abs(hz(output.data)-expected)/expected<.035,`Pitch independence: ${pitch}, ${speed}, ${hz(output.data)}`);
}
const impulse=new Float32Array(4800);impulse[1440]=.1;
let m=DSP.render(impulse,SR,[{...event,trimB:100,at:200},{...event,trimB:100,at:600}],1);
assert(m.left[9600]>0&&m.left[28800]>0,'Measured peaks align to event time');
assert.equal(m.left.reduce((n,v)=>n+(v!==0),0),2);
let spin=DSP.render(sine.slice(0,4800),SR,[{...event,trimB:100,mode:'spin',at:100,interval:30,duration:600}],1);
assert.equal(spin.pulses,21);assert(spin.left.some((v,i)=>Math.abs(v-spin.right[i])>.001),'Stereo rotation changes channel balance');
let clipped=DSP.render(sine.slice(0,4800),SR,Array.from({length:30},()=>({...event,trimB:100,gain:18})),1);
assert(clipped.attenuationDb<0);assert(clipped.left.every(v=>Math.abs(v)<=.94001));
const wav=new DataView(DSP.wav(m));assert.equal(wav.getUint16(22,true),2);assert.equal(wav.getUint32(24,true),48000);assert.equal(wav.byteLength,44+m.left.length*4);
assert.throws(()=>DSP.render(impulse,SR,[{...event,count:NaN}],1));
assert.throws(()=>DSP.render(impulse,SR,[{...event,trimB:1}],1));
assert.throws(()=>DSP.render(impulse,SR,Array(65).fill(event),1));
console.log('PASS: pitch/speed independent, duration, exact event peaks, spin overlap/stereo, overload protection, WAV headers, invalid input.');
