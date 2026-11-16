"""Download credited public GIF references, never add them to approved assets."""
from pathlib import Path
import hashlib
import json
import urllib.request
import urllib.error
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'local-assets/attack-effect-references'
OUT.mkdir(parents=True, exist_ok=True)
sources = json.loads((Path(__file__).with_name('sources.json')).read_text(encoding='utf-8'))
for item in sources:
    target = OUT / (item['id'] + '.gif')
    if not target.exists():
        request = urllib.request.Request(item['gif'], headers={'User-Agent': 'Mozilla/5.0'})
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                target.write_bytes(response.read())
        except urllib.error.URLError as error:
            raise SystemExit(f"Download unavailable: {item['page']}\n"
                             f"Save the displayed GIF through your browser to {target}, then rerun.\n"
                             f"Reason: {error}") from None
    with Image.open(target) as im:
        assert im.format == 'GIF' and im.n_frames > 1, target
        item['width'], item['height'] = im.size
        item['frames'] = im.n_frames
        samples = [round(n * (im.n_frames - 1) / 7) for n in range(8)]
        sheet = Image.new('RGB', (800, 4 * 175), '#dddddd')
        for n, index in enumerate(samples):
            im.seek(index)
            frame = im.convert('RGB')
            frame.thumbnail((400, 150))
            x, y = (n % 2) * 400, (n // 2) * 175
            sheet.paste(frame, (x, y + 20))
            ImageDraw.Draw(sheet).text((x + 5, y + 3), str(index + 1), fill='black')
        sheet.save(OUT / (item['id'] + '-contact.jpg'))
    item['bytes'] = target.stat().st_size
    item['sha256'] = hashlib.sha256(target.read_bytes()).hexdigest()
    print(json.dumps({k: item[k] for k in ['id', 'frames', 'width', 'height', 'bytes']}))
(OUT / 'download-receipt.json').write_text(json.dumps(sources, ensure_ascii=False, indent=2), encoding='utf-8')
