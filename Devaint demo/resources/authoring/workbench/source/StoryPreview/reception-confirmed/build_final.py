"""Build the confirmed preview with external PNG paths (artwork is distributed in a ZIP)."""
from pathlib import Path
import hashlib
import json
import struct

ROOT = Path(__file__).resolve().parent
config = json.loads((ROOT/'scene_config.json').read_text(encoding='utf-8'))
data = json.loads(json.dumps(config))
records = {}

def asset(relative):
    path = (ROOT/relative).resolve()
    assert path.is_relative_to(ROOT) and path.is_file(), relative
    raw = path.read_bytes()
    assert raw[:8] == b'\x89PNG\r\n\x1a\n', relative
    records[relative] = {'path':relative, 'size':struct.unpack('>II',raw[16:24]),
                         'bytes':len(raw), 'sha256':hashlib.sha256(raw).hexdigest()}
    return relative

data['hero'] = asset(config['hero'])
data['walk'] = [asset(path) for path in config['walk']]
data['sprites'] = {name:asset(path) for name,path in config['sprites'].items()}
html = (ROOT/'review_template.html').read_text(encoding='utf-8')
for marker,file in [('HIGH_RESOLUTION_TEXT','high_resolution_text.js'),
                    ('SPEECH_MODULE','speech_runtime.js'),
                    ('PIXEL_MOTION_MODULE','pixel_motion.js'),
                    ('INK_SCENE_MODULE','ink_scene.js'),
                    ('LIBRARY_BACKGROUND_MOTION','library_background_motion.js')]:
    source = (ROOT/file).read_text(encoding='utf-8')
    if file == 'ink_scene.js':
        source = source.replace('__BLUE_WISP_FRAMES__',(ROOT/'wisp_patterns.json').read_text(encoding='utf-8'))
    html = html.replace('__'+marker+'__',source)
html = html.replace('__DATA__',json.dumps(data,ensure_ascii=False))
(ROOT/'index.html').write_text(html,encoding='utf-8')
(ROOT/'asset_manifest.json').write_text(json.dumps({'stage':'1차 확정','date':'2026-10-19',
    'canvas':[960,540],'assets':list(records.values()),'portable':True},ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'layout.json').write_text(json.dumps({'canvas':[960,540],'floor_y':config['floorY'],
    'hero_canvas':[128,128],'hero_foot':[64,120],'hero_scale':1,'layers':config['layers'],
    'hero_insertion_index':config['heroInsert']},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'built':'index.html','assets':len(records),'self_contained':False},ensure_ascii=False))
