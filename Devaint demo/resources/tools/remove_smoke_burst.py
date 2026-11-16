"""Remove the smoke burst, retaining both existing spread recordings byte for byte."""
import io
import re
import wave
import render_basic_attack_audio as m


def main():
    asset = m.RES / 'audio/gamemaster-skill-audition'
    source = asset / 'source'
    old = source / 'smoke-release-07'
    out = source / 'smoke-release-08'
    out.mkdir(exist_ok=True)
    decisions = m.load(source / 'focused-selections.json')
    bomb = decisions['bombExplosion']
    assert m.sha((asset / bomb['file']).read_bytes()) == bomb['sha256']
    items, clips = [], []
    for prior, num in zip(m.load(old / 'mixes.json')['items'], (16, 17)):
        part = prior['parts']['spread']
        raw = (old / part['file']).read_bytes()
        assert m.sha(raw) == part['sha256']
        target = out / f'S{num}.wav'
        target.write_bytes(raw)
        assert target.read_bytes() == raw
        with wave.open(io.BytesIO(raw), 'rb') as reader:
            params = reader.getparams()
            assert params.nchannels == 1 and params.sampwidth == 2 and params.framerate == m.RATE
            assert params.nframes == 52800
            clips.append(reader.readframes(params.nframes))
        items.append(dict(id=f'S{num}', file=target.name, sha256=m.sha(raw), duration=1.1,
            status='pending', basedOn=prior['id'],
            source=dict(file='../smoke-release-07/'+part['file'], sha256=part['sha256']),
            edit=dict(burstRemoved=True, spreadByteIdentical=True, postMixNormalization=False)))
    with wave.open(str(out / 'S16-S17-compare.wav'), 'wb') as writer:
        writer.setparams(params)
        writer.writeframes(clips[0] + b'\0' * (m.RATE * 2) + clips[1])
    m.dump(out / 'mixes.json', dict(kind='smoke-release-revision8', status='pending; not applied',
        items=items, confirmedBomb=bomb, comparisonGapSeconds=1))
    decisions['smoke'].update(currentCandidates=['S16', 'S17'], currentAudition='preview/smoke-release-08.html',
        basedOn=['S14', 'S15'], structure=dict(burstSeconds=0, totalSeconds=1.1, spreadStart=.025),
        direction='user-confirmed: spread only; no burst', instruction='팡 소리 빼 그냥 쉬이이이로 가자')
    m.dump(source / 'focused-selections.json', decisions)
    page = (asset / 'preview/smoke-release-07.html').read_text(encoding='utf8')
    page = page.replace('smoke-release-07', 'smoke-release-08').replace('S14', 'S16').replace('S15', 'S17')
    page = re.sub(r'<details>.*?</details>', '', page, flags=re.S)
    page = page.replace('각 1.1초 · 앞의 팡 0.08초 · 사이 1초 간격', '퍼짐만 1.1초 · 사이 1초 간격')
    page = page.replace('부드러운 팡 · 기존 퍼짐', '쉬이이이 · 기존 첫 번째 퍼짐')
    page = page.replace('조금 더 선명한 팡 · 기존 퍼짐', '쉬이이이 · 기존 두 번째 퍼짐')
    page = page.replace('팡 0.08초 · 전체 1.1초', '앞의 팡 제거 · 퍼짐 1.1초')
    page = re.sub(r'<p class="hint">.*?</p>', '<p class="hint">팡 소리를 제거했습니다. 뒤의 퍼짐은 음색·음량·길이를 바꾸지 않고 그대로 남겼습니다.</p>', page)
    page = page.replace('version:7', 'version:8').replace('focused-bomb-smoke-choice-07.json', 'focused-bomb-smoke-choice-08.json')
    preview = asset / 'preview/smoke-release-08.html'
    preview.write_text(page, encoding='utf8')
    for ref in re.findall(r'src="([^"]+)"', page):
        assert (preview.parent / ref).is_file(), ref
    for path in (source / 'README.md', m.MAIN / 'docs/development/presentation/audio/audio-production.md'):
        doc = path.read_text(encoding='utf8').replace('smoke-release-07.html', 'smoke-release-08.html')
        start = doc.index('S14/S15는')
        end = doc.index('연막은 미정', start)
        doc = doc[:start] + 'S16/S17은 사용자 지시에 따라 앞의 팡을 제거하고 기존 두 퍼짐 파일만 그대로 사용한다. 음색·음량·1.1초 길이와 파일 바이트를 유지했다. 퍼짐만 사용하는 방향은 확정이며 두 음색 중 최종 선택은 아직 없다. 비교 파일 사이에는 1초 무음이 있다. ' + doc[end:]
        path.write_text(doc, encoding='utf8')
    m.register_asset(asset, 'audio.gamemaster-skill-audition', 'bomb B3 user-confirmed; smoke spread-only direction confirmed, S16/S17 pending; not applied')
    print('PASS: S16/S17 are byte-identical to previous spread WAVs; burst removed; B3 unchanged; preview links valid.')


if __name__ == '__main__':
    main()
