"""Soften only the smoke burst; preserve the existing spread and mixed tail bytes."""
import io
import json
import wave
import numpy as np
import render_basic_attack_audio as m


def main():
    asset = m.RES / 'audio/gamemaster-skill-audition'
    source = asset / 'source'
    old = source / 'smoke-release-06'
    out = source / 'smoke-release-07'
    out.mkdir(exist_ok=True)
    decisions = m.load(source / 'focused-selections.json')
    bomb = decisions['bombExplosion']
    assert m.sha((asset / bomb['file']).read_bytes()) == bomb['sha256']
    items, clips = [], []
    for prior, num, cutoff, attack in zip(m.load(old / 'mixes.json')['items'], (14, 15), (1800, 2100), (.012, .010)):
        burst_raw = (old / prior['parts']['burst']['file']).read_bytes()
        spread_raw = (old / prior['parts']['spread']['file']).read_bytes()
        assert m.sha(spread_raw) == prior['parts']['spread']['sha256']
        burst = np.asarray(m.decode(burst_raw))
        n = len(burst)
        pad = m.RATE // 4
        padded = np.pad(burst, (pad, pad))
        freq = np.fft.rfftfreq(len(padded), 1 / m.RATE)
        rounded = np.fft.irfft(np.fft.rfft(padded) / np.sqrt(1 + (freq / cutoff) ** 8), n=len(padded))[pad:pad+n]
        onset = round(attack * m.RATE)
        rounded[:onset] *= np.sin(np.linspace(0, np.pi / 2, onset)) ** 2
        rounded[-round(.012 * m.RATE):] *= np.cos(np.linspace(0, np.pi / 2, round(.012 * m.RATE))) ** 2
        # Retain the body level without restoring the sharp initial peak.
        body = slice(round(.018 * m.RATE), round(.055 * m.RATE))
        gain = min(1.6, np.linalg.norm(burst[body]) / max(np.linalg.norm(rounded[body]), 1e-9))
        rounded *= gain
        rounded[0] = rounded[-1] = 0
        assert np.max(np.abs(rounded)) < .6
        burst_new = m.wav_bytes(rounded)
        (out / f'S{num}-bang.wav').write_bytes(burst_new)
        (out / f'S{num}-spread.wav').write_bytes(spread_raw)
        original_mix = (old / prior['file']).read_bytes()
        with wave.open(io.BytesIO(original_mix), 'rb') as reader:
            params = reader.getparams()
            frames = reader.readframes(reader.getnframes())
        spread = np.asarray(m.decode(spread_raw))
        prefix_wav = m.wav_bytes(spread[:n] + rounded)
        with wave.open(io.BytesIO(prefix_wav), 'rb') as reader:
            prefix = reader.readframes(n)
        buffer = io.BytesIO()
        with wave.open(buffer, 'wb') as writer:
            writer.setparams(params)
            writer.writeframes(prefix + frames[n * 2:])
        raw = buffer.getvalue()
        (out / f'S{num}.wav').write_bytes(raw)
        with wave.open(io.BytesIO(raw), 'rb') as reader:
            new_frames = reader.readframes(reader.getnframes())
        assert new_frames[n * 2:] == frames[n * 2:]
        assert (out / f'S{num}-spread.wav').read_bytes() == spread_raw
        clips.append(np.asarray(m.decode(raw)))
        items.append(dict(id=f'S{num}', file=f'S{num}.wav', sha256=m.sha(raw), duration=len(spread)/m.RATE,
            status='pending', basedOn=prior['id'], parts=dict(
                burst=dict(file=f'S{num}-bang.wav', sha256=m.sha(burst_new), duration=n/m.RATE),
                spread=dict(file=f'S{num}-spread.wav', sha256=m.sha(spread_raw), duration=len(spread)/m.RATE)),
            sources=dict(burst=dict(file='../smoke-release-06/'+prior['parts']['burst']['file'], sha256=m.sha(burst_raw)),
                spread=dict(file='../smoke-release-06/'+prior['parts']['spread']['file'], sha256=m.sha(spread_raw))),
            edit=dict(burstLowpass=cutoff, burstAttack=attack, bodyGain=float(gain), burstDuration=n/m.RATE,
                spreadByteIdentical=True, mixedTailByteIdenticalFromSeconds=n/m.RATE, postMixNormalization=False)))
    (out / 'S14-S15-compare.wav').write_bytes(m.wav_bytes(np.concatenate([clips[0], np.zeros(m.RATE), clips[1]])))
    m.dump(out / 'mixes.json', dict(kind='smoke-release-revision7', status='pending; not applied', items=items, confirmedBomb=bomb, comparisonGapSeconds=1))
    decisions['smoke'].update(currentCandidates=['S14', 'S15'], currentAudition='preview/smoke-release-07.html', basedOn=['S12', 'S13'],
        spreadPreservedFrom='smoke-release-06', structure=dict(burstSeconds=.08, totalSeconds=1.1, spreadStart=.025))
    m.dump(source / 'focused-selections.json', decisions)
    page = (asset / 'preview/smoke-release-06.html').read_text(encoding='utf8')
    page = page.replace('smoke-release-06', 'smoke-release-07').replace('S12', 'S14').replace('S13', 'S15')
    page = page.replace('짧은 팡 · 낮고 옅은 퍼짐', '부드러운 팡 · 기존 퍼짐').replace('또렷한 팡 · 낮고 옅은 퍼짐', '조금 더 선명한 팡 · 기존 퍼짐')
    page = page.replace('앞소리를 짧은 팡으로 교체했습니다. 퍼짐의 음역을 더 낮추고 음량도 줄였으며, 전체 1.1초까지 부드럽게 옅어집니다.',
        '앞소리의 고역과 첫 타격을 부드럽게 조정했습니다. 뒤의 퍼짐 파일은 이전 버전과 완전히 동일하며, 합성 음원의 0.08초 이후도 그대로 보존했습니다.')
    page = page.replace('version:6', 'version:7').replace('focused-bomb-smoke-choice-06.json', 'focused-bomb-smoke-choice-07.json')
    (asset / 'preview/smoke-release-07.html').write_text(page, encoding='utf8')
    for path in (source / 'README.md', m.MAIN / 'docs/development/presentation/audio/audio-production.md'):
        doc = path.read_text(encoding='utf8').replace('smoke-release-06.html', 'smoke-release-07.html')
        if path.name == 'README.md':
            start = doc.index('S12/S13은')
            end = doc.index('연막은 미정이고', start)
            doc = doc[:start] + 'S14/S15는 앞의 80ms 팡만 저역 통과 1.8/2.1kHz와 12/10ms 시작 페이드로 부드럽게 조정했다. 뒤의 퍼짐 WAV는 S12/S13과 바이트 단위로 동일하며 합성 음원도 80ms 이후는 그대로다. 화면에서 두 부분을 따로 들을 수 있다. S14→S15 비교 파일 사이에는 1초 무음이 있다. ' + doc[end:]
        else:
            start = doc.index('S12/S13은')
            end = doc.index('연막은 미정이다.', start)
            doc = doc[:start] + 'S14/S15는 앞의 80ms 팡만 1.8/2.1kHz 저역 통과와 12/10ms 시작 페이드로 부드럽게 조정했다. 뒤의 퍼짐 파일과 합성 음원의 80ms 이후는 이전 S12/S13과 바이트 단위로 동일하다. 앞부분과 퍼짐을 따로 들을 수 있다. ' + doc[end:]
        path.write_text(doc, encoding='utf8')
    m.register_asset(asset, 'audio.gamemaster-skill-audition', 'bomb B3 user-confirmed; smoke S14/S15 rounded burst pending, spread preserved; not applied')
    print(json.dumps(dict(smoke=['S14', 'S15'], spread='byte-identical', mixedTail='byte-identical after 80ms', bomb='B3 unchanged')))


if __name__ == '__main__':
    main()
