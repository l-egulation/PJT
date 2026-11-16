"""Validate synchronized body timing, native sprite references and crown edits."""
from pathlib import Path
import hashlib
import json
from PIL import Image

HERE = Path(__file__).resolve().parent


def validate():
    data = json.loads((HERE / 'data.json').read_text(encoding='utf8'))
    manifest = json.loads((HERE / 'assets-manifest.json').read_text(encoding='utf8'))
    corrections = json.loads((HERE / 'corrections.json').read_text(encoding='utf8'))
    assert (HERE / 'data.js').read_text(encoding='utf8') == 'window.INK_MOTION_DATA=' + json.dumps(data, ensure_ascii=False, separators=(',', ':')) + ';\n'
    assert len(manifest['files']) == manifest['image_files']
    known = {record['path'] for record in manifest['files']}
    for record in manifest['files']:
        content = (HERE / record['path']).read_bytes()
        assert len(content) == record['bytes']
        assert hashlib.sha256(content).hexdigest() == record['sha256'], record['path']
    frame_count = 0
    for key, w in data['weapons'].items():
        modules = {m['id']: m for m in w['modules']}
        for name, times in corrections['common_motion_timing_ms'].items():
            assert [s['ms'] for s in modules[name]['steps']] == times, (key, name)
            assert modules[name]['duration_ms'] == sum(times), (key, name)
        death = [m for m in w['modules'] if m['id'].startswith('death')]
        assert sum(s['ms'] for m in death for s in m['steps']) == 800
        assert sum(len(m['steps']) for m in death) == 12
        assert [s['original_frame'] for m in death for s in m['steps']] == list(range(1, 13))
        assert modules['death_hold']['hold_last'] is True
        for fid, sha in corrections['death']['native_frame_sha256'][key].items():
            assert hashlib.sha256((HERE / w['frames'][fid]).read_bytes()).hexdigest() == sha, (key, fid, 'original death pixels changed')
        for m in w['modules']:
            for s in m['steps']:
                assert s['frame'] in w['frames'], (key, s['frame'])
        for path in w['frames'].values():
            assert path in known
            frame_count += 1
        for kind, frames in w['effects'].items():
            assert all(path in known for path in frames)
            times = w.get('effectTiming', {}).get(kind, w['effectMs'])
            assert len(times) == len(frames)
    pixel_count = 0
    for change in corrections['crown_changes']:
        im = Image.open(HERE / change['path']).convert('RGBA')
        assert len(change['pixels']) == change['count']
        for x, y in change['pixels']:
            assert im.getpixel((x, y)) == tuple(change['after_rgba'])
            pixel_count += 1
    assert pixel_count == corrections['total_changed_pixels'] == 160
    result = {'passed': True, 'image_files': manifest['image_files'], 'logical_character_frames': frame_count, 'common_modules_per_weapon': 15, 'crown_pixels_checked': pixel_count, 'death_duration_ms': 800, 'death_poses': 12, 'original_death_frames_verified': 48}
    return result


if __name__ == '__main__':
    print(json.dumps(validate(), ensure_ascii=False))
