"""Keep only candidate 7's first 550 ms and progressively fade it out."""
import json
import numpy as np
import render_basic_attack_audio as m


def main():
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source'
    prior=next(i for i in m.load(source/'smoke-release-10/mixes.json')['items'] if i['number']==7)
    original=source/'smoke-release-10'/prior['file'];raw=original.read_bytes()
    assert m.sha(raw)==prior['sha256']
    x=np.asarray(m.decode(raw));n=round(.55*m.RATE);y=x[:n].copy()
    # Keep the existing gentle onset. From 65 ms onward, decrease gain
    # continuously to silence; no pitch change, repeat, or added sound.
    start=round(.065*m.RATE)
    envelope=np.ones(n)
    envelope[start:]=np.cos(np.linspace(0,np.pi/2,n-start))**2.2
    y*=envelope;y[-1]=0
    out=source/'smoke-release-11';out.mkdir(exist_ok=True)
    filename='smoke11-07-front.wav';encoded=m.wav_bytes(y);(out/filename).write_bytes(encoded)
    decoded=np.asarray(m.decode(encoded))
    assert len(decoded)==26400 and decoded[0]==decoded[-1]==0
    assert max(abs(decoded))<=max(abs(x))
    assert np.all(np.diff(envelope)<=0)
    assert np.sqrt(np.mean(decoded[-480:]**2))<.0002
    item=dict(id='A07-front',number=7,file=filename,sha256=m.sha(encoded),duration=.55,status='pending',
        source=dict(file='../smoke-release-10/'+prior['file'],sha256=prior['sha256']),
        edit=dict(cropSeconds=[0,.55],fadeStartSeconds=.065,fadeEndSeconds=.55,curve='cosine power 2.2',
            addedSound=False,playbackRate=1,normalization=False))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision11',status='pending; not applied',items=[item]))
    decisions=m.load(source/'focused-selections.json')
    decisions['smoke'].update(status='pending',currentCandidates=['A07-front'],currentAudition='preview/smoke-release-11.html',
        instruction='7번에서 앞쪽 소리만 사용하고 점점 작아지게',basedOn='smoke-release-10/A07',
        structure=dict(burstSeconds=0,totalSeconds=.55,fadeStartSeconds=.065,playbackRate=1))
    m.dump(source/'focused-selections.json',decisions)
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>연막 7번 · 앞부분 감쇠</title><style>body{margin:0;background:#142123;color:#f2f0e8;font:17px/1.7 'Malgun Gothic',sans-serif}main{max-width:720px;margin:auto;padding:32px}audio{width:100%}a{color:#a3e1c9}h2{font-size:19px}</style><main><h1>연막 7번 · 앞부분만</h1><p>앞 0.55초만 남기고 점점 작아지게 조정했습니다. 음색과 재생속도는 그대로입니다.</p><h2>수정본</h2><audio controls preload="none" src="../source/smoke-release-11/smoke11-07-front.wav"></audio><details><summary>수정 전 7번 비교</summary><audio controls preload="none" src="../source/smoke-release-10/smoke10-07.wav"></audio></details><p>청취용 수정본 · 게임 적용 전</p></main><script>const players=[...document.querySelectorAll('audio')];for(const a of players){a.volume=.7;a.onplay=()=>players.forEach(b=>{if(a!==b)b.pause()});}</script></html>'''
    (asset/'preview/smoke-release-11.html').write_text(page,encoding='utf8')
    readme=source/'README.md';doc=readme.read_text(encoding='utf8')
    start=doc.index('현재 집중 청취 화면은');end=doc.index('\n\n사용자가 전달한',start)
    doc=doc[:start]+'현재 집중 청취 화면은 `../preview/smoke-release-11.html`이다. 사용자가 10차 7번의 앞부분만 요청하여 앞 0.55초를 남기고 65ms부터 550ms까지 연속 감쇠했다. 추가 소리·음높이 변경·정규화는 없으며 수정 전 7번도 비교할 수 있다. 이 수정본은 미정이고 게임에는 적용하지 않았다. 전체 모션 화면의 기존 10개 후보는 변경하지 않았다.'+doc[end:]
    readme.write_text(doc,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition','smoke candidate 7 first 550ms with progressive fade pending; other selections unchanged; not applied')
    print(json.dumps(dict(file=str(out/filename),duration=.55,fadeStart=.065,peak=float(max(abs(decoded))),tailRms=float(np.sqrt(np.mean(decoded[-480:]**2))))))


if __name__=='__main__':main()
