"""Retouch D's native pixels; preserve source, palette, poses and timing."""
from pathlib import Path
import json
import shutil
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_dilation, binary_fill_holes, convolve, label
from k4_d_pixel_walk import ROOT, N, sha, map_preview, page
from walk_review import animate, review_board

SPEC = ROOT / 'data/presentation/stage2_k4_d_pixel_cleanup.json'


def zone_mask(shape, box):
    mask = np.zeros(shape, bool)
    x0, y0, x1, y1 = box
    mask[y0:y1, x0:x1] = True
    return mask


def fill_holes(a, spec):
    result = a.copy()
    solid = a[:, :, 3] > 0
    holes, count = label(binary_fill_holes(solid) & ~solid)
    ankle = zone_mask(solid.shape, spec['ankleRepairZone'])
    changes = []
    for component in range(1, count + 1):
        mask = holes == component
        size = int(mask.sum())
        kind = 'ankle-connection' if np.all(ankle[mask]) else 'pinhole'
        limit = spec['maxAnkleHoleArea'] if kind == 'ankle-connection' else spec['maxPinholeArea']
        if size > limit:
            continue
        # Propagate existing material from the boundary, one logical pixel at a time.
        pending = mask.copy()
        while pending.any():
            frontier = pending & binary_dilation(result[:, :, 3] > 0)
            assert frontier.any()
            updates = []
            for y, x in zip(*np.where(frontier)):
                patch = result[y-1:y+2, x-1:x+2]
                colors, counts = np.unique(patch[patch[:, :, 3] > 0], axis=0, return_counts=True)
                rgb = colors[counts.argmax()]
                # Boot material fills boot/ankle breaks; do not extend skin through a boot.
                if kind == 'ankle-connection':
                    rgb = np.array([37, 37, 45, 255], np.uint8)
                updates.append((y, x, rgb))
            for y, x, rgb in updates:
                result[y, x] = rgb
                pending[y, x] = False
        changes.append({'kind': kind, 'pixels': size})
    return result, changes


def align(reference, neighbor, spec):
    x0, y0, x1, y1 = spec['registrationRegion']
    ref = reference[y0:y1, x0:x1]
    limit = spec['maxRegistrationShift']
    candidates = []
    for dy in range(-limit, limit + 1):
        for dx in range(-limit, limit + 1):
            patch = neighbor[y0+dy:y1+dy, x0+dx:x1+dx]
            error = np.any(ref != patch, axis=2).sum()
            candidates.append((int(error), abs(dx)+abs(dy), dy, dx))
    _, _, dy, dx = min(candidates)
    aligned = np.roll(neighbor, (-dy, -dx), axis=(0, 1))
    return aligned, [dx, dy]


def temporal_cleanup(frames, spec):
    output, stats = [], []
    for i, a in enumerate(frames):
        prev, pd = align(a, frames[(i-1) % len(frames)], spec)
        nxt, nd = align(a, frames[(i+1) % len(frames)], spec)
        result = a.copy()
        solid = a[:, :, 3] > 0
        region = zone_mask(solid.shape, spec['temporalRegion'])
        neighbors = convolve(solid.astype(int), np.ones((3, 3), int), mode='constant') - solid
        consensus = np.all(prev == nxt, axis=2)
        # Only fill supported 1px notches; large silhouette changes and intended gaps survive.
        notch = ~solid & region & (neighbors >= 6) & consensus & (prev[:, :, 3] > 0)
        gaps, gap_count = label(binary_fill_holes(solid) & ~solid)
        for gap in range(1, gap_count + 1):
            mask = gaps == gap
            if mask.sum() > 20:
                notch &= ~mask
        for box in spec['protectedGapZones']:
            notch &= ~zone_mask(solid.shape, box)
        result[notch] = prev[notch]
        # Prune unsupported one-frame spurs only when BOTH neighboring poses lack them.
        spur = solid & region & (neighbors <= 2) & (prev[:, :, 3] == 0) & (nxt[:, :, 3] == 0)
        spur[:, :167] = False  # nozzle and forward hand are intentional thin shapes
        result[spur] = 0
        recolors = 0
        for color in np.unique(a[solid], axis=0):
            if tuple(color[:3]) == (234, 170, 22):
                continue
            parts, count = label(np.all(a == color, axis=2))
            for component in range(1, count + 1):
                mask = parts == component
                if mask.sum() != 1:
                    continue
                y, x = np.argwhere(mask)[0]
                if not (region[y, x] and neighbors[y, x] == 8 and consensus[y, x] and prev[y, x, 3] > 0):
                    continue
                replacement = prev[y, x]
                if tuple(replacement[:3]) == (234, 170, 22):
                    continue
                patch = a[y-1:y+2, x-1:x+2]
                if np.all(patch == replacement, axis=2).sum() >= 5:
                    result[y, x] = replacement
                    recolors += 1
        output.append(result)
        stats.append({'neighborSampleShifts': [pd, nd], 'notchesFilled': int(notch.sum()),
                      'spursRemoved': int(spur.sum()), 'interiorSpecksRecolored': recolors})
    return output, stats


def build():
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    source, out = ROOT / spec['source'], ROOT / spec['output']
    info = json.loads((source / 'manifest.json').read_text(encoding='utf-8'))
    for folder in ('native', 'native-indexed', 'white-native', 'enlarged-4x', 'previews', 'masks', 'references/before'):
        (out / folder).mkdir(parents=True, exist_ok=True)
    originals = [np.array(Image.open(source / 'native' / f'walk-{i+1:02}.png').convert('RGBA')) for i in range(20)]
    filled, hole_stats = zip(*(fill_holes(a, spec) for a in originals))
    retouched, temporal = temporal_cleanup(filled, spec)
    # A contour repair can enclose a formerly exterior pinhole: clear these in a final pass.
    final, final_holes = zip(*(fill_holes(a, spec) for a in retouched))
    for edit in spec['manualPixels']:
        x, y = edit['xy']
        final[edit['key']-1][y, x] = edit['rgba']
    frames = [Image.fromarray(a) for a in final]
    palette = np.array(info['paletteRgb'], np.uint8)
    index_palette = np.concatenate([np.zeros((1,3), np.uint8), palette])
    mapping = []
    for i, (before, after, frame) in enumerate(zip(originals, final, frames)):
        name = f'walk-{i+1:02}.png'
        frame.save(out / 'native' / name)
        frame.resize((1536, 896), N).save(out / 'enlarged-4x' / name)
        bg = Image.new('RGBA', frame.size, 'white'); bg.alpha_composite(frame)
        bg.convert('RGB').save(out / 'white-native' / name)
        indices = np.zeros(after.shape[:2], np.uint8)
        for c, rgb in enumerate(palette, 1):
            indices[np.all(after[:, :, :3] == rgb, axis=2) & (after[:, :, 3] > 0)] = c
        indexed = Image.fromarray(indices); indexed.putpalette(index_palette.ravel().tolist() + [0]*(768-index_palette.size))
        indexed.info['transparency'] = 0
        indexed.save(out / 'native-indexed' / name, transparency=0)
        assert indexed.convert('RGBA').tobytes() == frame.tobytes()
        shutil.copy2(source / 'native' / name, out / 'references/before' / name)
        changed = np.any(before != after, axis=2)
        mask = np.zeros_like(after); mask[changed] = [220, 30, 140, 255]
        Image.fromarray(mask).save(out / 'masks' / name)
        edits = [{'xy': [int(x), int(y)], 'before': before[y,x].tolist(), 'after': after[y,x].tolist()} for y,x in zip(*np.where(changed))]
        mapping.append({'key': i+1, 'sourceSha256': sha(source / 'native' / name), 'changedPixels': len(edits),
                        'holes': hole_stats[i] + final_holes[i], **temporal[i], 'edits': edits})
        print(i+1, len(edits), 'holes', mapping[-1]['holes'], 'temporal', temporal[i], flush=True)
    ds, box = info['durationsMs'], info['previewCrop']
    animate(frames, ds, out / 'previews/walk-native.webp'); animate(frames, ds, out / 'previews/walk-native.apng')
    white, compare = [], []
    cw, ch = (box[2]-box[0])*4, (box[3]-box[1])*4
    font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 16)
    for before, frame in zip(originals, frames):
        panels = []
        for array in (before, np.array(frame)):
            im = Image.fromarray(array).crop(box).resize((cw,ch), N)
            bg = Image.new('RGBA', im.size, 'white'); bg.alpha_composite(im); panels.append(bg)
        white.append(panels[1])
        pair = Image.new('RGBA', (cw*2+16, ch+30), 'white')
        pair.alpha_composite(panels[0], (0,30)); pair.alpha_composite(panels[1], (cw+16,30))
        draw = ImageDraw.Draw(pair); draw.text((8,4),'BEFORE',font=font,fill='black'); draw.text((cw+24,4),'CLEANED',font=font,fill='black')
        compare.append(pair)
    animate(white, ds, out / 'previews/walk-white-4x.webp')
    animate(compare, ds, out / 'previews/before-after-4x.webp')
    review_board(frames, list(range(20)), box, 5, 3, out / 'previews/all-20-dark.png')
    # A white board shows tiny alpha defects directly, without checkerboard interference.
    w,h = white[0].size
    board = Image.new('RGB', (w*5,(h+24)*4),'white'); draw=ImageDraw.Draw(board)
    for i,frame in enumerate(white):
        x,y=i%5*w,i//5*(h+24);board.paste(frame.convert('RGB'),(x,y+24));draw.text((x+5,y+2),f'{i+1:02}',font=font,fill='black')
    board.save(out/'previews/all-20-white.png')
    detail = Image.new('RGB', (2720, 1000), 'white')
    draw = ImageDraw.Draw(detail)
    for column, index in enumerate((3, 6, 7, 19)):
        bg = Image.new('RGBA', frames[index].size, 'white'); bg.alpha_composite(frames[index])
        detail.paste(bg.crop((150,78,235,194)).resize((680,928),N),(column*680,40))
        draw.text((column*680+8,8),f'{index+1:02} / 8x',font=font,fill='black')
    detail.save(out/'previews/detail-clean-8x.png')
    for filename in ('base-frame.png','palette.json','selected-D.json'):
        shutil.copy2(source/'references'/filename,out/'references'/filename)
    shutil.copy2(source/'manifest.json',out/'references/before-manifest.json')
    shutil.copy2(SPEC,out/'cleanup-spec.json')
    result={**info,'output':spec['output'],'pixelConversionStatus':'cleanup-review-not-approved','cleanup':spec,
            'cleanupMapping':mapping,'changedPixelCount':sum(m['changedPixels'] for m in mapping)}
    (out/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    map_preview(out,frames,ds);page(out,result)
    html=(out/'index.html').read_text(encoding='utf-8')
    html=html.replace('선택 D · 등짐분사수11색 픽셀 변환','선택 D · 빈 픽셀과 연결부 보정')
    html=html.replace('k4-D-pixel-walk-20261031.zip','k4-D-pixel-clean-20261031.zip')
    html=html.replace('이번 픽셀 변환본은 검토 단계입니다.','내부 빈 픽셀·발목 연결부와 프레임 간 작은 잡점을 기존11색으로 보정했습니다. 손잡이·호스·다리 사이 여백은 유지합니다. 보정본은 검토 단계입니다.')
    html=html.replace('<h2>1배·확대·외곽 확인</h2>','<h2>보정 전 / 후 · 같은 타이밍 비교</h2><img class="loop" src="previews/before-after-4x.webp"><p>왼쪽 이전 / 오른쪽 보정. 두 결과는 같은 이미지 안에서 동시에 재생됩니다. D20장·1초·크기는 동일합니다.</p><h2>1배·확대·외곽 확인</h2>')
    (out/'index.html').write_text(html,encoding='utf-8')
    (out/'README.md').write_text('''# 등짐분사수 D 픽셀 보정 검토본

이전20장의 native PNG를 원본으로 사용한다. 작은 내부 알파 구멍과 발목 연결부를 주변 재질색으로 메우고, 인접 두 자세가 같은 결론을 주는1px 외곽 홈·고립 내부 색점만 제한적으로 정리한다. 이웃 비교의 정수 좌표 이동은 근거 샘플링에만 쓰며 출력 몸체를 이동하지 않는다. 손잡이·호스·다리 사이의 의도된 여백은 보존한다.

20장 순서·D30/55ms·1초 주기·384×224 캔버스·기존11색을 유지한다. RGBA/indexed/흰 배경/정확한4배/무손실 루프를 제공한다. references/before에 이전20장을 무변형 보존하고 manifest의 픽셀별 전후 값·변경 마스크로 보정을 확인할 수 있다. 원시 반환 원본과 최초 팔레트 변환 자료는 이전 출력 폴더에서 유지한다.

합성 미리보기이며 실제 게임 캡처가 아니다. 이번 픽셀 리터치는 아트 검토 단계이며 Unity 통합은 포함하지 않는다.
''',encoding='utf-8')
    with zipfile.ZipFile(out/'k4-D-pixel-clean-20261031.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(out.rglob('*')):
            if path.is_file() and path.suffix!='.zip':archive.write(path,path.relative_to(out).as_posix())


if __name__ == '__main__':
    build()
