"""ApprovedTheme.Texture / ApprovedTheme.Ink 와 같은 방식으로 승인 테마 그림을 잘라 합성한다(시안용)."""
import json
from pathlib import Path
from PIL import Image

class Approved:
    def __init__(self, root):
        self.base = Path(root) / 'unity/Assets/Deviant/UI/Theme/Resources/ApprovedTheme'
        m = json.loads((self.base / 'manifest.json').read_text(encoding='utf-8'))
        self.sprites = {s['id']: s for s in m['sprites']}; self.inks = {i['id']: i for i in m['inks']}; self.cache = {}

    def source(self, f):
        if f not in self.cache: self.cache[f] = Image.open(self.base / f).convert('RGBA')
        return self.cache[f]

    def texture(self, sid):
        r = self.sprites[sid]; src = self.source(r['file']); v = r['view']; clip = r.get('rect') or v
        out = Image.new('RGBA', (int(-(-v[2] // 1)), int(-(-v[3] // 1))), (0, 0, 0, 0))
        crop = src.crop((int(clip[0]), int(clip[1]), int(clip[0] + clip[2]), int(clip[1] + clip[3])))
        out.alpha_composite(crop, (int(clip[0] - v[0]), int(clip[1] - v[1])))
        return out

    def ink(self, iid):
        ink = self.inks[iid]; v = ink['view']; out = self.texture(ink['bottle']).resize((int(-(-v[2] // 1)), int(-(-v[3] // 1))))
        for key, at in ((ink['effect'], ink['label']), (ink['condition'], ink['badge'])):
            t = self.texture(key); sc = at['size'] / max(t.size); t = t.resize((max(1, round(t.width * sc)), max(1, round(t.height * sc))), Image.LANCZOS)
            out.alpha_composite(t, (round(at['center'][0] - v[0] - t.width / 2), round(at['center'][1] - v[1] - t.height / 2)))
        return out
