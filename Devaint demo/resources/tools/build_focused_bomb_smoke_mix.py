"""Build three focused water-balloon mixes and three 1-second pressure releases."""
import argparse
import html
import json
import zipfile
import numpy as np
import render_basic_attack_audio as m


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';out=asset/'source/focused-mixes-01';out.mkdir(exist_ok=True)
    rate=m.RATE;records=[];cache={};inputs={}
    def rms(x):return np.sqrt(np.mean(x*x))+1e-12
    def band(x,low,high):
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+n]
    def fade(x,attack,release):
        y=x.copy();a=min(round(attack*rate),len(y)//3);b=min(round(release*rate),len(y)//2)
        y[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2;y[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
        return y
    def body(x):
        width=round(.012*rate);power=np.convolve(x*x,np.ones(width)/width,mode='same')
        env=np.sqrt(power+1e-10);pivot=np.percentile(env[env>max(env)*.1],65)
        y=x*np.minimum(1,(pivot/np.maximum(env,1e-9))**.65)
        return y/rms(y)
    def mix(length,parts):
        y=np.zeros(round(length*rate))
        for x,start,gain in parts:
            at=round(start*rate);n=min(len(x),len(y)-at)
            if n>0:y[at:at+n]+=x[:n]*gain
        return y
    def level(x,target=.085):
        active=x[abs(x)>max(abs(x))*.03]
        return x*min(target/rms(active),.48/max(abs(x)))
    def write(id,label,x,parts,notes):
        raw=m.wav_bytes(x);file=id+'.wav';(out/file).write_bytes(raw);decoded=np.asarray(m.decode(raw))
        records.append(dict(id=id,label=label,file=file,duration=len(decoded)/rate,sha256=m.sha(raw),
                            peak=float(max(abs(decoded))),sources=parts,edit=notes))
        cache[id]=decoded
    with zipfile.ZipFile(args.archive) as z:
        def source(entry):
            raw=z.read(entry);inputs[entry]=m.sha(raw);return np.asarray(m.decode(raw))
        # Real spray supplies the initial "pu". A separate pneumatic release
        # overlaps it to provide the "shhh" tail; no synthesized crowd-like bed.
        for i,(spray,pressure,front_gain,air_low) in enumerate([(7,3,.72,4200),(12,1,.92,3400),(8,2,.57,4800)],1):
            front_entry=f'Miscellaneous/Spray Bottle/spray_bottle_{spray:02}.wav'
            tail_entry=f'Doors/hydraulic_strut_air_gas_shock_door_{pressure:02}.wav'
            front=source(front_entry);front=band(front,2400,230)
            # Exclude the handle-click onset, retain the actual pressurized spray.
            front=front[round(.025*rate):round(.24*rate)]
            front=fade(body(front),.014,.07)
            tail=source(tail_entry);tail=band(tail,air_low,550)
            tail=tail[round(.06*rate):round(1.01*rate)]
            tail=body(tail);tail=fade(tail,.085,.30)
            y=mix(1,[(front,0,front_gain),(tail,.045,.34)])
            y=fade(y,.008,.24);y=level(y,.075)
            label=['표준 분출','앞의 푸 강조','뒤의 쉬 강조'][i-1]
            write(f'S{i}',label,y,[front_entry,tail_entry],dict(kind='pressure-release',duration=1,
                frontCrop=[.025,.24],tailCrop=[.06,1.01],frontGain=front_gain,tailGain=.34,tailOnset=.045,
                noExplosion=True,noLoop=True,noSyntheticNoise=True))
        # Keep the flowing body of a water spell, with a small real splash tail.
        # The hard cartoon pop and explosive/thud ingredients are deliberately absent.
        for i,(spell,splash,seconds,wet_gain,tau) in enumerate([(5,2,.85,.42,.115),(2,4,.95,.65,.145),(4,5,1.05,.82,.175)],1):
            main_entry=f'Magic_Spells/water_blast_projectile_spell_{spell:02}.wav'
            wet_entry=f'Water/water_splash_object_body_{splash:02}.wav'
            main=source(main_entry);peak=m.sliding_peak(main.tolist());start=max(0,round((peak-.065)*rate))
            main=band(main[start:],4400,190);main=body(main)
            t=np.arange(len(main))/rate
            main*=np.exp(-np.maximum(0,t-.09)/tau)
            main=fade(main,.022,.12)
            wet=source(wet_entry);at=round((m.sliding_peak(wet.tolist())+.04)*rate)
            wet=band(wet[at:],3800,280);wet=body(wet)
            t=np.arange(len(wet))/rate;wet*=np.exp(-t/(tau*1.25));wet=fade(wet,.025,.12)
            y=mix(seconds,[(main,0,.72),(wet,.025,wet_gain)])
            y=fade(y,.012,.20);y=level(y,.095)
            label=['짧고 가벼운 물 파열','물 퍼짐 강조','큰 물풍선 방향'][i-1]
            write(f'B{i}',label,y,[main_entry,wet_entry],dict(kind='layered-water-release',mainStart=start/rate,
                wetStart=at/rate,wetOnset=.025,mainGain=.72,wetGain=wet_gain,decay=tau,
                noExplosionRecording=True,noThud=True,noCartoonPop=True,noSyntheticNoise=True))
    for family in ('S','B'):
        # Full-strength samples separated by one second of silence, no overlaps.
        parts=[]
        for i in range(1,4):
            if parts:parts.append(np.zeros(rate))
            parts.append(cache[f'{family}{i}'])
        raw=m.wav_bytes(np.concatenate(parts));(out/f'{family}-compare.wav').write_bytes(raw)
    metadata=dict(kind='focused-synthesized-comparison',status='audition only; not approved or applied',
                  bombReference='Crazy Arcade water balloon burst direction; reference audio not sampled',
                  inputs=inputs,items=records,comparisonOrder=[1,2,3],comparisonGapSeconds=1)
    m.dump(out/'mixes.json',metadata)
    sections=[]
    for family,title in [('S','연막 · 1초 푸쉬이이'),('B','폭탄 · 물풍선 파열 방향')]:
        cards=''.join(f'<article><h3>{r["id"]} · {html.escape(r["label"])}</h3><p>{r["duration"]:.2f}초</p><audio controls preload="none" src="../source/focused-mixes-01/{r["file"]}"></audio><button data-id="{r["id"]}" data-family="{family}">이 시안 선택</button></article>' for r in records if r['id'].startswith(family))
        sections.append(f'<h2>{title}</h2><p>1 → 2 → 3 이어 듣기 · 사이에 1초 간격</p><audio controls preload="none" src="../source/focused-mixes-01/{family}-compare.wav"></audio><div class="grid">{cards}</div>')
    page='''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>폭탄·연막 합성 비교 · 3안</title><style>
body{background:#142123;color:#f2f0e8;font:16px/1.6 'Malgun Gothic',sans-serif;margin:0}main{max-width:1100px;margin:auto;padding:28px 22px}h1,h3{margin:0}h2{margin-top:28px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(275px,1fr));gap:14px;margin-top:16px}article{background:#213335;border:1px solid #46605d;border-radius:10px;padding:16px}audio{width:100%;max-width:620px;margin:8px 0}button{background:#a3e1c9;border:0;border-radius:7px;padding:9px 14px;cursor:pointer;font:inherit}.chosen{background:#f1c281}a{color:#a3e1c9}.hint{font-size:13px;color:#b5c4c2}#selection{color:#f1c281}
</style></head><body><main><h1>폭탄·연막 합성 비교 · 3안</h1><p>원음을 겹치고 길이·음색을 조정한 청취 시안입니다. 적합성 확인과 게임 적용은 아직 하지 않았습니다.</p><p class="hint">폭탄은 투척음 없이 파열 부분만, 연막은 분출음 하나만 비교합니다.</p>__SECTIONS__<p id="selection"></p><button id="save">선택 파일 저장</button><p><a href="index.html">전체 무기 청취 화면</a></p></main><script>
const key='lost-words-focused-bomb-smoke-01',players=[...document.querySelectorAll('audio')],buttons=[...document.querySelectorAll('[data-id]')];let picked={};try{picked=JSON.parse(localStorage.getItem(key)||'{}')}catch(_){}
for(const p of players){p.volume=.8;p.onplay=()=>{for(const q of players)if(p!==q)q.pause();};}
function update(){document.getElementById('selection').textContent=`연막 ${picked.S||'미정'} / 폭탄 ${picked.B||'미정'}`;for(const b of buttons){const active=picked[b.dataset.family]===b.dataset.id;b.classList.toggle('chosen',active);b.setAttribute('aria-pressed',String(active));}}
for(const b of buttons)b.onclick=()=>{picked[b.dataset.family]=b.dataset.id;localStorage.setItem(key,JSON.stringify(picked));update();};
document.getElementById('save').onclick=()=>{const data={kind:'focused-bomb-smoke-choice',version:1,selected:picked,status:'user choice; not applied'};const u=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=u;a.download='focused-bomb-smoke-choice.json';a.click();setTimeout(()=>URL.revokeObjectURL(u),1000);};update();
</script></body></html>'''.replace('__SECTIONS__',''.join(sections))
    (asset/'preview/focused-mixes-01.html').write_text(page,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition','focused synthesized bomb/smoke comparison: 3 each pending; prior stars preserved; not applied')
    print(json.dumps(dict(files=len(records),page=str(asset/'preview/focused-mixes-01.html'),durations={r['id']:r['duration'] for r in records})))


if __name__=='__main__':main()
