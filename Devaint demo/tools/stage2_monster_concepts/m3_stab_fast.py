"""Retiming only: faster M3 thrust/withdrawal with unchanged source pixels."""
import copy
import json
import shutil
import zipfile

from PIL import Image
from b3_walk_return import dump, white
from k4_d_pixel_walk import ROOT, N, sha
from m3_stab_reuse import page, animation_duration
from walk_review import animate

SPEC = ROOT / 'data/presentation/stage2_m3_stab_fast.json'


def build():
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    source, out = ROOT/spec['source'], ROOT/spec['output']
    original = json.loads((source/'manifest.json').read_text(encoding='utf-8'))
    info = copy.deepcopy(original)
    out.mkdir(parents=True, exist_ok=True)
    for folder in ('native','native-indexed','white-native','enlarged-4x','references','masks'):
        shutil.copytree(source/folder,out/folder,dirs_exist_ok=True)
    (out/'previews').mkdir(exist_ok=True)
    shutil.copy2(source/'manifest.json',out/'references/original-timing.json')
    shutil.copy2(SPEC,out/'timing-spec.json')
    for name in ('cycle-keys.png','selected-keys.png'):
        shutil.copy2(source/'previews'/name,out/'previews'/name)
    for phase, key in [('thrust','thrustDurationsMs'),('withdraw','withdrawDurationsMs')]:
        info[phase]['durationsMs'] = spec[key]
    # Every occurrence uses the exact same three PNGs and new durations.
    for seqname in ('timeline','singleCycle'):
        counters = {}
        for row in info[seqname]:
            if row['phase'] not in ('thrust','withdraw'):
                continue
            phase = row['phase']; group = (phase,row.get('hit'))
            at = counters.get(group,0)
            row['durationMs'] = info[phase]['durationsMs'][at]
            counters[group] = at+1
    elapsed = 0
    for row in info['timeline']:
        row['startMs'] = elapsed; elapsed += row['durationMs']; row['endMs'] = elapsed
    info.update({'status':spec['status'],'output':spec['output'],'timingRevision':spec,
                 'attackDurationMs':elapsed,'singleCycleMs':sum(r['durationMs'] for r in info['singleCycle']),
                 'archiveName':'m3-stab-fast-20261101.zip','sourceManifestSha256':sha(source/'manifest.json'),
                 'policy':spec['policy'],'approval':spec.get('approval','pending-user-review')})
    assert elapsed == 1870 and info['singleCycleMs'] == 150
    images = {r['nativePath']:Image.open(out/r['nativePath']).convert('RGBA') for r in info['mapping']}
    box = info['previewCrop']; scene = Image.open(out/'references/C-game.png').convert('RGBA')
    for name, seq in [('cycle',info['singleCycle']),('combo',info['timeline'])]:
        frames = [images[r['path']] for r in seq]; ds = [r['durationMs'] for r in seq]
        if name == 'combo': frames.append(frames[-1]); ds.append(info['reviewGapMs'])
        animate(frames,ds,out/'previews'/f'{name}-native.webp')
        animate(frames,ds,out/'previews'/f'{name}-native.apng')
        crops = [white(f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N)).convert('RGBA') for f in frames]
        animate(crops,ds,out/'previews'/f'{name}-white-4x.webp')
        if name == 'combo':
            maps = []
            for f in frames:
                c = scene.copy(); c.alpha_composite(f,tuple(info['mapOffset'])); maps.append(c)
            animate(maps,ds,out/'previews/combo-map.webp')
    # Keep the previous timing visible for a direct comparison, without overwriting it.
    shutil.copy2(source/'previews/combo-white-4x.webp',out/'previews/previous-combo.webp')
    page(out,info)
    p = out/'index.html'; html = p.read_text(encoding='utf-8')
    html = html.replace('m3-stab-reuse-20261101.zip',info['archiveName'])
    html = html.replace('찌르기100ms×4 + 사이 회수120ms×3','찌르기70ms×4 + 사이 회수80ms×3')
    html = html.replace('= 2.11초','= 1.87초').replace('사이클은220ms','사이클은150ms')
    html = html.replace('한 번 찌르기를 재사용','빠른 찌르기 · 연타 박자 약1.47배')
    html = html.replace('시간·4연타는 기존 제작 제안이며','빠른 시간은 사용자 요청에 따른 검토안이며4연타 제안을 유지합니다. ')
    if info['approval'] == 'approved-by-user':
        html = html.replace('검토안이며 아직 채택 전입니다.','사용자가 채택한 빠른4연타입니다.')
    html = html.replace('<section><h2>재사용하는6장</h2>',
        '<section><h2>이전 속도 비교</h2><p>이전: 찌르기100ms·회수120ms. 위 빠른 안은70ms·80ms입니다. 준비550ms와 마무리800ms는 같습니다. 두 파일은 전체 길이가 달라 타격 시점이 동기화되지 않습니다.</p><img src="previews/previous-combo.webp" alt="이전 속도"></section><section><h2>재사용하는6장</h2>')
    p.write_text(html,encoding='utf-8')
    dump(out/'manifest.json',info); dump(out/'sequence.json',info['timeline'])
    # Verify timing-only scope against the actual source files and complete timeline.
    for folder in ('native','native-indexed','white-native','enlarged-4x','masks'):
        for path in (source/folder).glob('*.png'):
            assert sha(path) == sha(out/folder/path.name)
    for old,new in zip(original['timeline'],info['timeline']):
        assert old['path'] == new['path'] and old['sourceOneBased'] == new['sourceOneBased']
        if old['phase'] in ('windup','recovery'): assert old['durationMs'] == new['durationMs']
    for kind,total in [('cycle',150),('combo',2470)]:
        assert animation_duration(out/'previews'/f'{kind}-native.webp') == total
        assert round(animation_duration(out/'previews'/f'{kind}-native.apng')) == total
    assert sha(source/'manifest.json') == info['sourceManifestSha256']
    dump(out/'validation.json',{'result':'pass','scope':'timing-only','pixelFilesByteIdentical':True,
         'frameOrderIdentical':True,'windupMs':550,'recoveryMs':800,'thrustMs':70,'withdrawMs':80,
         'singleCycleMs':150,'attackMs':1870,'reviewGapMs':600,'decodedWebpApngMs':2470,
         'sourceManifestUnchanged':True,'browserQA':'not-run','Unity':'not-run'})
    (out/'README.md').write_text('''# 먹침필경사 빠른 찌르기 검토

기존 고유11장·9색·384×224·정수4배 파일은 바이트 그대로 복사한다. 찌르기3장 시간을30/30/40→20/20/30ms, 회수3장은30/40/50→20/30/30ms로 줄인다. 한 사이클220→150ms로 연타 박자는 약1.47배다. 전조550ms·4회 타격·최종회복800ms·확인 간격600ms는 같다. 전체 공격 시간은2110→1870ms이며 확인 간격은 별도다.

새 픽셀·프레임 삭제·보간·팔레트 변경은 없다. 반환 원본과100셀은 기존 m3-stab-reuse-20261101에 보존하고 그 manifest와 프롬프트/팔레트/기본본을 이 결과에 복사한다. 시간표와 한 사이클/4연타·이전 속도 비교·맵합성을 제공한다. 맵은 합성 미리보기이며 실제 게임 캡처가 아니다. 사용자 검토 전이며 게임 전투 수치와 Unity 연결은 변경하지 않는다.
''',encoding='utf-8')
    if info['approval'] == 'approved-by-user':
        p = out/'README.md'
        p.write_text(p.read_text(encoding='utf-8').replace('사용자 검토 전이며','사용자가 빠른4연타를 채택했으며'),encoding='utf-8')
    with zipfile.ZipFile(out/info['archiveName'],'w',zipfile.ZIP_DEFLATED) as z:
        for path in sorted(out.rglob('*')):
            if path.is_file() and path.suffix != '.zip' and path.name != 'commit-message.txt': z.write(path,path.relative_to(out).as_posix())
    with zipfile.ZipFile(out/info['archiveName']) as z: assert z.testzip() is None
    print(json.dumps({'result':'pass','cycleMs':150,'attackMs':1870,'pixels':'unchanged'}))


if __name__ == '__main__':
    build()
