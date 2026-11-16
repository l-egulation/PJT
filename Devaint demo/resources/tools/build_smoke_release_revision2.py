"""Preserve the user-confirmed B3 and audition two nonmechanical smoke releases."""
import argparse
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';previous=source/'focused-mixes-01'
    meta=m.load(previous/'mixes.json');bomb=next(v for v in meta['items'] if v['id']=='B3')
    bombraw=(previous/bomb['file']).read_bytes();assert m.sha(bombraw)==bomb['sha256']
    decisions=dict(kind='focused-sound-decisions',bombExplosion=dict(candidate='B3',
        file='source/focused-mixes-01/B3.wav',sha256=bomb['sha256'],status='user-confirmed; not applied',
        instruction='폭탄 b3할게',mix=bomb),smoke=dict(status='pending',rejected=['focused-mixes-01/S1','focused-mixes-01/S2','focused-mixes-01/S3']))
    m.dump(source/'focused-selections.json',decisions)
    for item in meta['items']:
        item['status']='user-confirmed; not applied' if item['id']=='B3' else 'rejected' if item['id'].startswith('S') else 'not selected'
    meta['status']='B3 user-confirmed; S1-S3 rejected; not applied';m.dump(previous/'mixes.json',meta)
    oldpage=asset/'preview/focused-mixes-01.html';page=oldpage.read_text(encoding='utf8')
    if 'B3-confirmed-banner' not in page:
        page=page.replace('<main>','<main><p id="B3-confirmed-banner"><strong>폭탄 B3 확정 · 연막 S1~S3 기각</strong> · <a href="smoke-release-02.html">새 연막 시안 S4·S5 듣기</a></p>',1)
        page=page.replace("for(const p of players){", "picked.B='B3';delete picked.S;for(const b of buttons)b.disabled=true;\nfor(const p of players){",1)
        page=page.replace('적합성 확인과 게임 적용은 아직 하지 않았습니다.','폭탄 B3는 사용자 확정이며, 게임에는 아직 적용하지 않았습니다.')
        oldpage.write_text(page,encoding='utf8')
    out=source/'smoke-release-02';out.mkdir(exist_ok=True);rate=m.RATE;items=[];cache=[]
    def rms(x):return np.sqrt(np.mean(x*x))+1e-12
    def band(x,low,high):
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+n]
    def fade(x,start,end):
        y=x.copy();a=min(round(start*rate),len(y)//3);b=min(round(end*rate),len(y)//2)
        y[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2;y[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
        return y
    with zipfile.ZipFile(args.archive) as z:
        for num,entry,start,low,front_gain in [(4,'Explosion_Fire_Gas/gas_leak_med_burst_01.wav',.10,5200,.34),
                                              (5,'Explosion_Fire_Gas/gas_leak_long_burst_01.wav',.12,4300,.48)]:
            raw=z.read(entry);x=np.asarray(m.decode(raw));x=x[round(start*rate):round((start+1)*rate)]
            assert len(x)==rate
            x=band(x,low,350)
            # Flatten the source's gradual onset so the pressure is already
            # present at release, then apply one smooth pressure-decay envelope.
            power=np.convolve(x*x,np.ones(960)/960,mode='same');env=np.sqrt(power+1e-10)
            target=np.percentile(env,60);gain=np.clip(target/np.maximum(env,1e-8),.35,2.2)
            x*=gain;x/=rms(x)
            t=np.arange(rate)/rate;x*=.83*np.exp(-t/.70)+.17
            x=fade(x,.012,.20)
            front_entry=f'Whooshes/whoosh_low_deep_soft_{num-3:02}.wav'
            front_raw=z.read(front_entry);front=np.asarray(m.decode(front_raw))
            front=band(front,1800,230);front=fade(front,.015,.10);front/=rms(front)
            y=x*.66;y[:len(front)]+=front*front_gain
            y=fade(y,.006,.18)
            active=y[abs(y)>max(abs(y))*.03];y*=min(.080/rms(active),.48/max(abs(y)))
            raw_out=m.wav_bytes(y);file=f'S{num}.wav';(out/file).write_bytes(raw_out)
            decoded=np.asarray(m.decode(raw_out));cache.append(decoded)
            items.append(dict(id=f'S{num}',label='압력 분출 중심' if num==4 else '낮은 푸 강조',file=file,
                duration=1,sha256=m.sha(raw_out),status='pending',sources=[dict(archiveEntry=entry,sha256=m.sha(raw),start=start,end=start+1),
                    dict(archiveEntry=front_entry,sha256=m.sha(front_raw))],
                edit=dict(lowpass=low,highpass=350,frontGain=front_gain,tailGain=.66,pressureDecaySeconds=.70,
                    attackSeconds=.012,tailFadeSeconds=.20,noExplosion=True,noMechanicalSource=True,noSyntheticNoise=True)))
    (out/'S4-S5-compare.wav').write_bytes(m.wav_bytes(np.concatenate([cache[0],np.zeros(rate),cache[1]])))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision2',status='pending; not applied',items=items,
        confirmedBomb=dict(file='../focused-mixes-01/B3.wav',sha256=bomb['sha256']),comparisonGapSeconds=1))
    cards=''.join(f'<article><h3>{v["id"]} · {v["label"]}</h3><p>1초</p><audio controls preload="none" src="../source/smoke-release-02/{v["file"]}"></audio><button data-id="{v["id"]}">이 연막 선택</button></article>' for v in items)
    page='''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>폭탄 B3 확정 · 연막 S4/S5</title><style>
body{background:#142123;color:#f2f0e8;font:16px/1.6 'Malgun Gothic',sans-serif;margin:0}main{max-width:950px;margin:auto;padding:28px 22px}h1,h3{margin:0}h2{margin-top:28px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px}article{background:#213335;border:1px solid #46605d;border-radius:10px;padding:16px}audio{width:100%;max-width:620px;margin:8px 0}button{background:#a3e1c9;border:0;border-radius:7px;padding:9px 14px;cursor:pointer;font:inherit}.chosen{background:#f1c281}.hint{color:#b5c4c2;font-size:13px}#selection{color:#f1c281}
</style></head><body><main><h1>폭탄 B3 확정 · 연막 재청취</h1><h2>폭탄 B3 · 확정 원음</h2><p>선택한 파일 그대로 보존했습니다. 게임 적용 전입니다.</p><audio controls preload="none" src="../source/focused-mixes-01/B3.wav"></audio><h2>연막 S4 → S5</h2><p>각 1초 · 사이 1초 간격</p><audio controls preload="none" src="../source/smoke-release-02/S4-S5-compare.wav"></audio><div class="grid">__CARDS__</div><p class="hint">폭발음·점화음·기계 작동음 없이 가스 분출과 낮은 공기 방출을 조합한 청취 시안입니다.</p><p id="selection"></p><button id="save">선택 파일 저장</button></main><script>
const key='lost-words-smoke-release-02',players=[...document.querySelectorAll('audio')],buttons=[...document.querySelectorAll('[data-id]')];let selected=null;try{selected=JSON.parse(localStorage.getItem(key)||'null')}catch(_){}
for(const p of players){p.volume=.8;p.onplay=()=>{for(const q of players)if(p!==q)q.pause();};}
function update(){document.getElementById('selection').textContent=`폭탄 B3 확정 / 연막 ${selected||'미정'}`;for(const b of buttons){b.classList.toggle('chosen',b.dataset.id===selected);b.setAttribute('aria-pressed',String(b.dataset.id===selected));}}
for(const b of buttons)b.onclick=()=>{selected=b.dataset.id;localStorage.setItem(key,JSON.stringify(selected));update();};
document.getElementById('save').onclick=()=>{const data={kind:'focused-bomb-smoke-choice',version:2,bomb:{id:'B3',sha256:'__BOMB_HASH__',status:'user-confirmed'},smoke:selected,status:'not applied'};const u=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=u;a.download='focused-bomb-smoke-choice-02.json';a.click();setTimeout(()=>URL.revokeObjectURL(u),1000);};update();
</script></body></html>'''.replace('__CARDS__',cards).replace('__BOMB_HASH__',bomb['sha256'])
    (asset/'preview/smoke-release-02.html').write_text(page,encoding='utf8')
    assert (previous/'B3.wav').read_bytes()==bombraw
    m.register_asset(asset,'audio.gamemaster-skill-audition','bomb B3 user-confirmed; smoke S4/S5 pending; prior stars preserved; not applied')
    print(json.dumps(dict(bomb='B3 confirmed, WAV unchanged',smoke=[x['id'] for x in items],page=str(asset/'preview/smoke-release-02.html'))))


if __name__=='__main__':main()
