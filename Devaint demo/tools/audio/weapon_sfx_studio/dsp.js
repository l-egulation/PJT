/* Offline DSP shared by audition and WAV export. No network or generated audio. */
(function(root){
'use strict';
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
function resample(input,rate){
  const n=Math.max(1,Math.round(input.length/rate)),out=new Float32Array(n);
  for(let i=0;i<n;i++){const x=i*rate,j=Math.min(input.length-1,Math.floor(x)),f=x-j;out[i]=input[j]*(1-f)+input[Math.min(j+1,input.length-1)]*f;}
  return out;
}
function stretch(input,ratio){
  if(Math.abs(ratio-1)<.001)return input.slice();
  const length=Math.max(1,Math.round(input.length*ratio));
  const n=input.length>=4096?2048:input.length>=2048?1024:input.length>=1024?512:input.length>=512?256:128,hop=n>>2,search=n>>2;
  const out=new Float32Array(length+n),weights=new Float32Array(length+n),win=new Float32Array(n);
  for(let j=0;j<n;j++)win[j]=.5-.5*Math.cos(2*Math.PI*j/(n-1));
  for(let at=0;at<length;at+=hop){
    const expected=Math.round(at/ratio);let best=clamp(expected,0,Math.max(0,input.length-n));
    if(at>0){
      let score=-Infinity;
      for(let candidate=Math.max(0,best-search);candidate<=Math.min(input.length-n,best+search);candidate+=4){
        let dot=0,a=0,b=0;
        for(let j=0;j<n-hop;j+=4){const old=weights[at+j]>.0001?out[at+j]/weights[at+j]:0,cur=input[candidate+j];dot+=old*cur;a+=old*old;b+=cur*cur;}
        const similarity=dot/Math.sqrt(a*b+1e-20)-Math.abs(candidate-expected)*1e-5;
        if(similarity>score){score=similarity;best=candidate;}
      }
    }
    for(let j=0;j<n&&at+j<out.length;j++){out[at+j]+=(input[best+j]||0)*win[j];weights[at+j]+=win[j];}
  }
  const result=new Float32Array(length);
  for(let i=0;i<length;i++)result[i]=weights[i]>.00001?out[i]/weights[i]:0;
  return result;
}
function transform(source,rate,event,pitchOffset=0){
  const a=Math.round(event.trimA/1000*rate),b=Math.round(event.trimB/1000*rate);
  let cut=source.slice(a,b);if(cut.length<rate*.01)throw Error('소리 구간은 10ms 이상 선택하세요.');
  const pitch=Math.pow(2,(event.pitch+pitchOffset)/12);
  cut=stretch(resample(cut,pitch),pitch/event.speed);
  const fade=Math.min(Math.round(rate*.003),Math.floor(cut.length/4));
  for(let i=0;i<fade;i++){const k=Math.sin(i/fade*Math.PI/2)**2;cut[i]*=k;cut[cut.length-1-i]*=k;}
  let peak=0;for(let i=1;i<cut.length;i++)if(Math.abs(cut[i])>Math.abs(cut[peak]))peak=i;
  return {data:cut,peak};
}
function validateEvent(e,sourceMs){
  const bounds={at:[0,30000],trimA:[0,sourceMs],trimB:[0,sourceMs],pitch:[-12,12],gain:[-48,18],speed:[.25,4],interval:[20,2000],count:[1,128],duration:[20,10000],pan:[-1,1],spinHz:[.1,20],depth:[0,1],sweep:[-12,12]};
  if(!e||!['one','repeat','spin'].includes(e.mode))throw Error('이벤트 형식이 올바르지 않습니다.');
  for(const [key,[a,b]]of Object.entries(bounds))if(!Number.isFinite(e[key])||e[key]<a||e[key]>b)throw Error('값 범위 확인: '+key);
  if(e.trimB-e.trimA<10)throw Error('소리 구간은 10ms 이상 선택하세요.');
  if(!Number.isInteger(e.count))throw Error('반복 횟수는 정수여야 합니다.');
  return e;
}
function render(source,rate,events,duration){
  if(!Array.isArray(events)||events.length>64)throw Error('한 공격에는 최대 64개 이벤트를 배치할 수 있습니다.');
  if(!Number.isFinite(duration)||duration<=0||duration>30)throw Error('최대 길이는 30초입니다.');
  let end=duration,total=0;const pulses=[],cache=new Map();
  for(const e of events){
    validateEvent(e,source.length/rate*1000);
    const count=e.mode==='one'?1:e.mode==='repeat'?e.count:Math.min(128,Math.floor(e.duration/e.interval)+1);
    total+=count;if(total>1024)throw Error('반복이 너무 많습니다. 횟수나 이벤트 수를 줄이세요.');
    for(let i=0;i<count;i++){
      const offset=count>1?e.sweep*i/(count-1):0,key=[e.trimA,e.trimB,e.pitch+offset,e.speed].join('/');
      if(!cache.has(key))cache.set(key,transform(source,rate,e,offset));
      const sound=cache.get(key),peakTime=(e.at+i*e.interval)/1000,start=peakTime-sound.peak/rate;
      end=Math.max(end,start+sound.data.length/rate);
      pulses.push({e,sound,start});
    }
  }
  if(end>30)throw Error('잔향을 포함한 전체 길이가 30초를 넘습니다.');
  const left=new Float32Array(Math.ceil(end*rate)),right=new Float32Array(left.length);
  for(const {e,sound,start}of pulses){
    const gain=Math.pow(10,e.gain/20),at=Math.round(start*rate);
    for(let j=Math.max(0,-at);j<sound.data.length&&at+j<left.length;j++){
      const t=(at+j)/rate-e.at/1000,phase=t*e.spinHz*2*Math.PI;
      const pan=e.mode==='spin'?clamp(e.pan+Math.sin(phase)*.9,-1,1):e.pan;
      const amp=e.mode==='spin'?1-e.depth+e.depth*(.5+.5*Math.cos(phase)):1;
      const value=sound.data[j]*gain*amp,angle=(pan+1)*Math.PI/4;
      left[at+j]+=value*Math.cos(angle);right[at+j]+=value*Math.sin(angle);
    }
  }
  let peak=0;for(let i=0;i<left.length;i++)peak=Math.max(peak,Math.abs(left[i]),Math.abs(right[i]));
  const attenuation=peak>.94?.94/peak:1;
  if(attenuation<1)for(let i=0;i<left.length;i++){left[i]*=attenuation;right[i]*=attenuation;}
  return {left,right,rate,duration:end,attenuationDb:20*Math.log10(attenuation),pulses:total};
}
function wav(mix){
  const n=mix.left.length,buffer=new ArrayBuffer(44+n*4),v=new DataView(buffer);
  const str=(at,s)=>{for(let i=0;i<s.length;i++)v.setUint8(at+i,s.charCodeAt(i));};
  str(0,'RIFF');v.setUint32(4,36+n*4,true);str(8,'WAVE');str(12,'fmt ');v.setUint32(16,16,true);v.setUint16(20,1,true);v.setUint16(22,2,true);v.setUint32(24,mix.rate,true);v.setUint32(28,mix.rate*4,true);v.setUint16(32,4,true);v.setUint16(34,16,true);str(36,'data');v.setUint32(40,n*4,true);
  for(let i=0;i<n;i++){v.setInt16(44+i*4,Math.round(clamp(mix.left[i],-1,.999969)*32768),true);v.setInt16(46+i*4,Math.round(clamp(mix.right[i],-1,.999969)*32768),true);}
  return buffer;
}
const api={resample,stretch,transform,validateEvent,render,wav};root.StudioDSP=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
