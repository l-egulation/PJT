"""Keep the generated art; arrange its own cells into the real 15-slot bag."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
raw = Image.open(root / 'source/generated-raw.png').convert('RGB')
image = raw.copy()
# The generated draft drew six columns. Reuse its own paper, frames and icons,
# arranging exactly 5 x 3 slots and six owned items, without changing the rest.
paper = raw.crop((879, 768, 1546, 805)).resize((669, 330), Image.Resampling.BICUBIC)
image.paste(paper, (877, 258))
occupied = [(878,260,995,371), (998,262,1108,369), (1112,262,1218,369),
            (1223,262,1329,369), (1332,262,1438,369), (1442,262,1548,369)]
empty = (1442,484,1548,587)
for slot in range(15):
    cell = raw.crop(occupied[slot] if slot < 6 else empty)
    cell = cell.resize((126, 103), Image.Resampling.LANCZOS)
    image.paste(cell, (879 + slot % 5 * 134, 262 + slot // 5 * 110))
image.save(root / 'preview/inventory-journal-concept.png')
print('Prepared exactly 15 bag cells: 6 occupied, 9 empty.')
