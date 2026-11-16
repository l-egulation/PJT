"""Extend the continuous quiet front of #7, excluding its loud later release."""
import json
import numpy as np
import render_basic_attack_audio as m


def main():
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source'
    prior=next(i for i in m.load(source/'smoke-release-10/mixes.json')['items'] if i['number']==7)
    raw=(source/'smoke-release-10'/prior['file']).read_bytes();assert m.sha(raw)==prior['sha256']
    n=round(.78*m.RATE);x=np.asarray(m.decode(raw))[:n];t=np.arange(n)/m.RATE
    # This recording's second, loud release starts around 0.8 s. End before
    # that, using contiguous original samples rather than a repeated snippet.
    width=round(.05*m.RATE)
    def smooth(a):
        padded=np.pad(a,(width//2,width//2),mode='edge')
        sums=np.concatenate(([0.],np.cumsum(padded)))
        return ((sums[width:]-sums[:-width])/width)[:len(a)]
    envelope=np.sqrt(smooth(x*x)+1e-12)
    reference=np.median(envelope[round(.07*m.RATE):round(.30*m.RATE)])
    tame=smooth(np.minimum(1,reference/np.maximum(envelope,1e-9)))
    fade=np.exp(-np.maximum(0,t-.065)/.38)
    end=round(.27*m.RATE);fade[-end:]*=np.cos(np.linspace(0,np.pi/2,end))**2
    y=x*tame*fade;y[0]=y[-1]=0
    out=source/'smoke-release-12';out.mkdir(exist_ok=True)
    name='smoke12-07-soft-tail.wav';encoded=m.wav_bytes(y);(out/name).write_bytes(encoded)
    decoded=np.asarray(m.decode(encoded))
    assert len(decoded)==n and decoded[-1]==0
    assert np.sqrt(np.mean(decoded[-480:]**2))<.00003
    assert max(abs(decoded))<=max(abs(x))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision12',status='pending; not applied',items=[dict(
        id='A07-soft-tail',number=7,file=name,sha256=m.sha(encoded),duration=.78,
        source=dict(file='../smoke-release-10/'+prior['file'],sha256=prior['sha256']),
        edit=dict(cropSeconds=[0,.78],decaySeconds=.38,tailFadeSeconds=.27,lateReleaseExcluded=True,
            localLevelCeilingWindowSeconds=.05,playbackRate=1,repeat=False,addedSound=False,normalization=False))]))
    d=m.load(source/'focused-selections.json');d['smoke'].update(currentCandidates=['A07-soft-tail'],
        currentAudition='preview/smoke-release-12.html',instruction='끝이 끊기는 느낌 수정',
        basedOn='smoke-release-10/A07 continuous front',structure=dict(burstSeconds=0,totalSeconds=.78,tailFadeSeconds=.27))
    m.dump(source/'focused-selections.json',d)
    page=(asset/'preview/smoke-release-11.html').read_text(encoding='utf8')
    page=page.replace('smoke-release-11/smoke11-07-front.wav','smoke-release-12/'+name)
    page=page.replace('앞 0.55초만 남기고 점점 작아지게 조정했습니다. 음색과 재생속도는 그대로입니다.',
        '조용한 앞 구간을 0.78초까지 이어 쓰고 천천히 감쇠했습니다. 뒤의 큰 분출은 제외하고 끝 0.27초를 부드럽게 마무리했습니다. 반복이나 추가 소리는 없습니다.')
    (asset/'preview/smoke-release-12.html').write_text(page,encoding='utf8')
    p=source/'README.md';doc=p.read_text(encoding='utf8');start=doc.index('현재 집중 청취 화면은');end=doc.index('\n\n사용자가 전달한',start)
    doc=doc[:start]+'현재 집중 청취 화면은 `../preview/smoke-release-12.html`이다. 7번 수정본의 끝이 끊겨 들린다는 피드백에 따라 조용한 연속 앞 구간을 0.78초까지 사용하고 지수 감쇠 및 마지막 0.27초 코사인 감쇠를 적용했다. 0.8초 부근의 큰 후속 분출은 제외하고 중간 음량 상승만 완만하게 제한했다. 반복·추가 소리·재생속도 변경은 없다. 게임 적용 전 미정이며 다른 스킬과 전체 모션 화면은 변경하지 않았다.'+doc[end:];p.write_text(doc,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition','smoke #7 continuous front extended to 780ms with soft tail pending; not applied')
    print(json.dumps(dict(duration=.78,tailFade=.27,endRms=float(np.sqrt(np.mean(decoded[-480:]**2))),peak=float(max(abs(decoded))))))


if __name__=='__main__':main()
