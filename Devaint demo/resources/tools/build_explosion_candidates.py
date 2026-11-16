"""Explosion candidate pool for the three explosion slots (not applied to the game).

Slots (current runtime ids): basic = explosion_bomb (bomb basic, bomb triple throw, explosive shuriken),
large = explosion_large (bomb range/follow-up/movement skills), grenade = explosion_grenade (launcher follow-up
cluster of three blasts). Every candidate is a purchased explosion-type recording at original speed: only a
2nd-order 30 Hz high-pass (inaudible rumble), onset trim and the recording's own decay (-54 dB, or the recording
end with a short fade). Loudness matches the slot's current file (K-weighted 400 ms maximum).

Run from the main repository root: python resources/tools/build_explosion_candidates.py
"""
import base64
import json
import zipfile
import numpy as np
import render_basic_attack_audio as m

ARCHIVE = 'C:/Users/SSAFY/Downloads/Gamemaster Audio - Pro Sound Collection.zip'
OUT = m.RES/'audio/explosion-candidates'
UNITY_SKILLS = m.MAIN/'unity/Assets/Deviant/Audio/Resources/SelectedSkillAudio'
UNITY_WEAPONS = m.MAIN/'unity/Assets/Deviant/Audio/SFX/Weapons'
R = m.RATE
GRENADE_PATTERN = [(0, .7), (.13, .55), (.27, .85)]  # same onsets/gains as the current grenade cluster
E = 'Explosion_Fire_Gas/'
SLOTS = {
    'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=[
        ('짧은 실제 폭발', '펑', E+'explosion_small_short_01.wav', '실제 폭발 녹음 중 가장 짧고 단단한 것. 꼬리가 짧아 연달아 터져도 덜 뭉친다.'),
        ('꼬리 없는 폭발', '쾅', E+'explosion_small_no_tail_03.wav', '처음부터 꼬리를 덜어 낸 녹음. 중저음 덩어리가 크고 끝이 깔끔하다.'),
        ('불꽃 펑', '화악', 'Magic_Spells/fireball_impact_burn_04.wav', '화염구 충돌. 폭발보다 가볍고 불이 확 번지는 결이 남는다.'),
        ('마법 에너지 펑', '퐁', 'Magic_Spells/energy_blast_small_03.wav', '에너지 마법 폭발. 실제 폭발보다 둥글고 판타지 쪽이다.'),
        ('레트로 폭발', '팡', 'Retro_Classic/Explosion/retro_explosion_general_01.wav', '8비트 게임 폭발. 일부러 거친 디지털 질감이 있어 깨진 소리로 들릴 수 있다.'),
    ]),
    'large': dict(name='큰 폭발', uses='폭탄 원거리 · 후속 · 이동 스킬', current='explosion_large_01.wav', candidates=[
        ('큰 실제 폭발', '콰광', E+'explosion_large_06.wav', '큰 폭발의 긴 잔향까지 그대로 둔 녹음. 가장 무겁고 길다.'),
        ('꼬리 짧은 큰 폭발', '쾅', E+'explosion_large_no_tail_06.wav', '큰 폭발이지만 꼬리를 덜어 1초 남짓으로 끝난다.'),
        ('중간 폭발 + 긴 잔향', '쿠웅', E+'explosion_med_long_tail_02.wav', '중간 크기 폭발 뒤 멀리 퍼지는 잔향이 길게 남는다.'),
        ('화염 폭발 마법', '화르륵', 'Magic_Spells/fireball_blast_projectile_spell_02.wav', '불덩이 마법 폭발. 실제 폭발보다 불길이 강조된다.'),
        ('레트로 깊은 폭발', '쿵팡', 'Retro_Classic/Explosion/retro_explosion_deep_12.wav', '8비트 깊은 폭발. 거친 디지털 질감이 있다.'),
    ]),
    'grenade': dict(name='유탄 3연쇄', uses='발사기 후속 특수기', current='explosion_grenade_01.wav', candidates=[
        ('짧은 폭발 3연쇄', '퍼버벙', E+'explosion_small_short_02.wav', '짧은 실제 폭발을 지금과 같은 간격으로 세 번. 지금의 먹먹한 저역 통과는 쓰지 않는다.'),
        ('꼬리 없는 폭발 3연쇄', '쾅쾅쾅', E+'explosion_small_no_tail_01.wav', '꼬리 없는 실제 폭발 세 번. 덩어리감이 크다.'),
        ('불꽃 3연쇄', '화화확', 'Magic_Spells/fireball_impact_burn_01.wav', '화염구 충돌 세 번. 가볍고 빠르다.'),
        ('에너지 3연쇄', '퐁퐁퐁', 'Magic_Spells/energy_blast_small_04.wav', '에너지 마법 폭발 세 번. 둥근 판타지 쪽.'),
        ('레트로 3연쇄', '팡팡팡', 'Retro_Classic/Explosion/retro_explosion_short_05.wav', '8비트 짧은 폭발 세 번. 거친 디지털 질감이 있다.'),
    ]),
}
# Listening contexts: the real attack/skill sound plus explosions at illustrative collision times.
CONTEXTS = {
    'basic': [('폭탄 평타', UNITY_WEAPONS/'sfx_weapon_bomb_basic_0_01_v01.wav', [.65]),
              ('폭탄 3연투', UNITY_SKILLS/'bomb_basic_special_02.wav', [.7, .85, 1.0]),
              ('폭발 수리검', UNITY_SKILLS/'shuriken_range_special_01.wav', [.75])],
    'large': [('폭탄 원거리 스킬', UNITY_SKILLS/'bomb_range_special_08.wav', [.9]),
              ('폭탄 후속 스킬', UNITY_SKILLS/'bomb_followup_special_02.wav', [1.5])],
    'grenade': [('발사기 후속 특수기', UNITY_SKILLS/'launcher_followup_special_tail_02.wav', [.45])],
}


def biquad(x, b, a):
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.
    for i, v in enumerate(x):
        o = b[0]*v+b[1]*x1+b[2]*x2-a[1]*y1-a[2]*y2
        x2, x1, y2, y1 = x1, v, y1, o; y[i] = o
    return y


def highpass(x, hz):
    w = 2*np.pi*hz/R; al = np.sin(w)/(2*.7071); c = np.cos(w)
    b = np.array([(1+c)/2, -(1+c), (1+c)/2]); a = np.array([1+al, -2*c, 1-al])
    return biquad(x, b/a[0], a/a[0])


def lowpass(x, hz):
    """RBJ 2nd-order (12 dB/oct) causal low-pass: darkens crackle/debris without changing timing."""
    w = 2*np.pi*hz/R; al = np.sin(w)/(2*.7071); c = np.cos(w)
    b = np.array([(1-c)/2, 1-c, (1-c)/2]); a = np.array([1+al, -2*c, 1-al])
    return biquad(np.asarray(x, float), b/a[0], a/a[0])


def loud(x, window=.4):
    y = biquad(np.asarray(x, float), [1.53512485958697, -2.69169618940638, 1.19839281085285], [1, -1.69065929318241, .73248077421585])
    y = biquad(y, [1, -2, 1], [1, -1.99004745483398, .99007225036621])
    n = int(window*R)
    return float(10*np.log10(np.convolve(y*y, np.ones(n)/n, 'full').max()+1e-12)-.691)


def natural(x):
    x = highpass(np.asarray(x, float), 30)
    env = np.sqrt(np.convolve(x*x, np.ones(int(.005*R))/int(.005*R), 'same'))
    start = max(0, int(np.argmax(env > env.max()*.01))-int(.005*R))
    x = x[start:].copy()
    env = env[start:]
    peak = int(np.argmax(env))
    below = np.where(env[peak:] < env[peak]*10**(-54/20))[0]
    if len(below):
        end, fade, rule = min(len(x), peak+int(below[0])+int(.05*R)), int(.05*R), 'recording decay to -54 dB'
    else:
        end, fade, rule = len(x), int(.08*R), 'recording ends before -54 dB: 80 ms fade'
    x = x[:end]
    ramp = int(.002*R)
    x[:ramp] *= np.linspace(0, 1, ramp)
    x[-fade:] *= np.cos(np.linspace(0, np.pi/2, fade))**2
    return x, dict(startSeconds=round(start/R, 4), endRule=rule)


REFERENCE_JS = r'''
// Reference recording: decoded locally, its loudest event is cut out and matched to the candidate's level.
let refBuf=null;
function loudestRms(b){const d=b.getChannelData(0),w=Math.round(b.sampleRate*.4);let best=0;for(let i=0;i+w<=d.length;i+=Math.round(w/4)){let s=0;for(let j=i;j<i+w;j++)s+=d[j]*d[j];best=Math.max(best,Math.sqrt(s/w));}return best||1e-6;}
function refSegment(b){const d=b.getChannelData(0),n=Math.round(b.sampleRate*.01);let peak=0,at=0;for(let i=0;i+n<d.length;i+=n){let s=0;for(let j=i;j<i+n;j++)s+=d[j]*d[j];if(s>peak){peak=s;at=i;}}let on=at;for(let i=at;i>0;i-=n){let s=0;for(let j=i;j<i+n;j++)s+=d[j]*d[j];if(s<peak*.01){on=i;break;}}const start=Math.max(0,on-Math.round(b.sampleRate*.05)),len=Math.min(d.length-start,Math.round(b.sampleRate*1.6));const out=ac.createBuffer(1,len,b.sampleRate);out.getChannelData(0).set(d.subarray(start,start+len));return out;}
async function refPlay(withCandidate){stop('준비 중…');const my=token;await ctx();const cand=await decode(pick().key);const seg=refSegment(refBuf);const g=loudestRms(cand)/loudestRms(seg);
  const t0=ac.currentTime+.06;const a=ac.createBufferSource(),ga=ac.createGain();a.buffer=seg;ga.gain.value=g;a.connect(ga);ga.connect(expBus);a.start(t0);nodes.push(a);$('status').textContent='참고 소리 (같은 크기로 맞춤)';
  if(withCandidate){timer=setTimeout(()=>{if(my!==token)return;const b=ac.createBufferSource();b.buffer=cand;b.connect(expBus);b.start();nodes.push(b);$('status').textContent='이 후보 · '+pick().name;},(seg.duration+.45)*1000);}}
$('refLoad').onclick=()=>$('refFile').click();
$('refFile').onchange=async()=>{try{const f=$('refFile').files[0];if(!f)return;await ctx();refBuf=await ac.decodeAudioData(await f.arrayBuffer());$('refPlay').disabled=$('refAB').disabled=false;$('refInfo').textContent='불러옴: '+f.name+' · 가장 큰 소리 구간 1.6초를 후보와 같은 크기로 맞춰 재생합니다.';}catch(e){$('refInfo').textContent='불러오지 못했습니다: '+e.message;}};
$('refPlay').onclick=()=>refPlay(false);$('refAB').onclick=()=>refPlay(true);
'''


def payload_audio(raw, x):
    return dict(src='data:audio/wav;base64,'+base64.b64encode(raw).decode(), duration=len(x)/R,
                wave=[round(float(np.max(np.abs(v))), 4) for v in np.array_split(x, 120)])


def main():
    build(OUT, SLOTS, 'audio.explosion-candidates', 'explosion candidate pool; awaiting user selection; not applied')


def build(OUT, SLOTS, asset_id, status, title=None, intro=None, store=None, reference_loader=False):
    """Shared builder; later candidate sets pass their own folder, slots and page text."""
    for d in ('runtime', 'source/originals', 'preview'):
        (OUT/d).mkdir(parents=True, exist_ok=True)
    data = dict(slots=[], audio={})
    recipes = []
    with zipfile.ZipFile(ARCHIVE) as archive:
        for sid, slot in SLOTS.items():
            cur_raw = (UNITY_SKILLS/slot['current']).read_bytes()
            cur = np.asarray(m.decode(cur_raw))
            target = loud(cur)
            data['audio']['current-'+sid] = payload_audio(cur_raw, cur)
            items = []
            for n, (name, voice, entry, desc, *options) in enumerate(slot['candidates'], 1):
                opt = options[0] if options else {}
                if opt.get('synth'):  # generated in code: no purchased source involved
                    raw = None
                    x, info = opt['synth']()
                else:
                    raw = archive.read(entry)
                    (OUT/'source/originals'/entry.split('/')[-1]).write_bytes(raw)
                    x, info = natural(m.decode(raw))
                if opt.get('lowpass'):
                    x = lowpass(x, opt['lowpass'])
                    info['lowpassHz'] = opt['lowpass']
                if opt.get('process'):
                    x, extra = opt['process'](x)
                    info.update(extra)
                if sid == 'grenade':
                    mix = np.zeros(int(GRENADE_PATTERN[-1][0]*R)+len(x))
                    for at, g in GRENADE_PATTERN:
                        mix[int(at*R):int(at*R)+len(x)] += x*g
                    x = mix
                gain = 10**((target+opt.get('levelOffsetDb', 0)-loud(x))/20)
                x = x*gain
                if opt.get('levelOffsetDb'):
                    info['levelOffsetDb'] = opt['levelOffsetDb']
                peak = float(np.max(np.abs(x)))
                if peak > .89:  # keep headroom like the current renders; note the shortfall
                    x *= .89/peak
                rendered = m.wav_bytes(x)
                file = f'{sid}-{n}.wav'
                (OUT/'runtime'/file).write_bytes(rendered)
                key = f'{sid}-{n}'
                data['audio'][key] = payload_audio(rendered, x)
                items.append(dict(key=key, number=n, name=name, voice=voice, source=entry.split('/')[-1], description=desc,
                                  duration=round(len(x)/R, 3)))
                recipes.append(dict(slot=sid, number=n, name=name, archiveEntry=entry, sourceSha256=m.sha(raw) if raw else None, **info,
                                    highpassHz=30, pitchOrRateChange=False, distortion=False,
                                    grenadeCluster=GRENADE_PATTERN if sid == 'grenade' else None,
                                    gainDb=round(20*np.log10(gain), 2), limitedTo089=peak > .89,
                                    loudnessDb=round(loud(x), 2), targetDb=round(target, 2), peak=round(float(np.max(np.abs(x))), 4),
                                    file='runtime/'+file, sha256=m.sha(rendered)))
            contexts = []
            for label, path, times in CONTEXTS[sid]:
                raw = path.read_bytes(); key = 'ctx-'+path.stem
                data['audio'][key] = payload_audio(raw, np.asarray(m.decode(raw)))
                contexts.append(dict(label=label, audio=key, gain=.8, explosions=times))
            data['slots'].append(dict(id=sid, name=slot['name'], uses=slot['uses'], current='current-'+sid,
                                      currentFile=slot['current'], candidates=items, contexts=contexts))
    m.dump(OUT/'source/recipes.json', dict(status=status,
                                            levelMatch='K-weighted 400 ms maximum equal to the slot current file',
                                            candidates=recipes))
    source = m.RES/'audio/explosion-candidates/source'
    template = (source/'template.html').read_text(encoding='utf8')
    app = (source/'app.js').read_text(encoding='utf8')
    if title:
        template = template.replace('<title>폭발음 후보</title>', f'<title>{title}</title>').replace('<h1>폭발음 후보</h1>', f'<h1>{title}</h1>')
    if intro:
        template = template.replace('폭발이 쓰이는 세 자리마다 서로 다른 성격의 후보 5개와 지금 소리를 나란히 듣는 화면입니다. 모두 구매팩의 폭발 계열 녹음을 원래 속도로 썼고, 30 Hz 아래 들리지 않는 울림만 걷어냈습니다.', intro)
    if store:
        app = app.replace("STORE='deviant-explosion-candidates-v1'", f"STORE='{store}'").replace("explosion-candidates-selection", store+'-selection')
    if reference_loader:
        # Local A/B against the user's own reference recording; the file is never embedded or uploaded.
        template = template.replace('<div id="contexts"></div>', '<div class="ctx"><strong>참고 소리 (내 녹화 파일)</strong><div class="row" style="margin-top:6px"><button id="refLoad">참고 파일 불러오기</button><button id="refPlay" disabled>참고 듣기</button><button id="refAB" disabled>참고 → 이 후보</button></div><p id="refInfo" class="note">영상(mp4)이나 음원 파일을 고르면 이 브라우저 안에서만 재생합니다. 파일은 저장되지 않습니다.</p><input id="refFile" type="file" accept="audio/*,video/*" hidden></div><div id="contexts"></div>')
        app = app + REFERENCE_JS
    if OUT != m.RES/'audio/explosion-candidates':
        (OUT/'source/template.html').write_text(template, encoding='utf8'); (OUT/'source/app.js').write_text(app, encoding='utf8')
    (OUT/'preview/index.html').write_text(template.replace('__DATA__', json.dumps(data, ensure_ascii=False).replace('</', '<\\/'))
                                          .replace('__APP__', app), encoding='utf8')
    lfs = m.load(m.RES/'.lfs-payloads.json'); entry = OUT.relative_to(m.RES).as_posix()+'/preview/index.html'
    if entry not in lfs:
        lfs.append(entry); m.dump(m.RES/'.lfs-payloads.json', sorted(lfs))
    m.register_asset(OUT, asset_id, status)
    print(json.dumps([(r['slot'], r['number'], r['name'], r['gainDb'], r['limitedTo089'], r['loudnessDb'], r['targetDb']) for r in recipes], ensure_ascii=False))


if __name__ == '__main__':
    main()
