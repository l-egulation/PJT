"""Build an offline reference from current village data and unmodified native PNGs."""
import base64
import argparse
import hashlib
import io
import json
import subprocess
import sys
from datetime import datetime
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from authoring_output import prepare_output
ROOT = OUT = FONT = SMALL = None
BLUE, RED, GOLD = '#087fbd', '#cf3649', '#ad7107'


def configure():
    global ROOT, OUT, FONT, SMALL
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--main-repo', type=Path, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    parser.add_argument('--font', type=Path, help='Review label font; defaults to the main retained font')
    args = parser.parse_args()
    ROOT = args.main_repo.resolve()
    OUT = prepare_output(args.destination)
    font = args.font or ROOT / 'unity/Assets/Deviant/UI/Fonts/malgun.ttf'
    FONT = ImageFont.truetype(str(font), 22)
    SMALL = ImageFont.truetype(str(font), 17)
    return ROOT, OUT, FONT, SMALL


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))


def uri(data, kind='png'):
    return f'data:image/{kind};base64,' + base64.b64encode(data).decode()


def frame_at(clip, age):
    cursor = 0
    for i, ms in enumerate(clip['milliseconds']):
        cursor += ms / 1000
        if age < cursor - 1e-8:
            return i
    return len(clip['frames']) - 1


def snapshot(monster, clips, age, label, filename):
    """A diagram using the exact sprite pivot, 32 PPU and runtime box equations."""
    height = 620 if monster['attackKind'] == 'hook-claw' else 470
    im = Image.new('RGB', (1100, height), '#f6f2e8')
    d = ImageDraw.Draw(im, 'RGBA')
    unit, ox, ground = 96, 290, height-90 if height == 620 else 394
    spec = monster['definition']
    moved = min(monster['chargeDistance'], max(0, age-monster['prepare']) * monster['chargeSpeed'])
    def xy(x, y): return ox + x*unit, ground-y*unit
    for x in range(-3, 9):
        px, _ = xy(x, 0)
        d.line((px, 64, px, ground), fill=(20, 45, 50, 18))
        d.text((px+4, ground+8), f'{x}u', font=SMALL, fill='#77746d')
    for y in range(4):
        _, py = xy(0, y)
        d.line((0, py, 1100, py), fill=(20, 45, 50, 24))
    clip = clips['idle'] if age < 0 else clips['attack']
    sprite = Image.open(ROOT/'unity'/clip['frames'][frame_at(clip, max(0, age))]['path']).convert('RGBA')
    # Approved source faces left. Runtime facing +1 mirrors around the native pivot.
    px = sprite.width*clip['pivot'][0]
    py = sprite.height*(1-clip['pivot'][1])
    left, top = xy(moved+(px-sprite.width)/32, py/32)
    sprite = ImageOps.mirror(sprite).resize((sprite.width*3, sprite.height*3), Image.Resampling.NEAREST)
    im.paste(sprite, (round(left), round(top)), sprite)
    d = ImageDraw.Draw(im, 'RGBA')
    def box(cx, cy, w, h, color, active=True):
        l, t = xy(cx-w/2, cy+h/2); r, b = xy(cx+w/2, cy-h/2)
        d.rectangle((l, t, r, b), fill=color+'20', outline=color, width=3)
    box(moved+spec['hurtOffset'][0], spec['hurtOffset'][1], *spec['hurtSize'], BLUE)
    if age >= 0:
        if monster['attackKind'] == 'spit':
            # The 7x0.6 definition box is NOT the damaging volume for this attack.
            box(moved+spec['attackOffset'][0], spec['attackOffset'][1], *spec['attackSize'], '#92918b')
            if age >= monster['damageStart']:
                x = monster['muzzleOffset'][0]+(age-monster['damageStart'])*monster['projectileSpeed']
                box(x, monster['muzzleOffset'][1], .25, .25, RED)
                l,t=xy(x-.125, monster['muzzleOffset'][1]+.125)
                d.rectangle((l,t,l+24,t+24), fill='#171716')
                d.rectangle((l-2,t-2,l+26,t+26), outline=RED,width=3)
        elif monster['attackKind'] == 'hook-claw' and age < monster['damageStart']:
            reach = monster['hookReach'] * max(0, min(1, (age-monster['hookStart'])/(monster['hookPeak']-monster['hookStart'])))
            # Full outbound swept envelope, not one frame's instantaneous box.
            size = monster['hookSize']
            box(reach/2, monster['hookHeight'], reach+size, size, GOLD)
            box(reach, monster['hookHeight'], size, size, GOLD)
        else:
            box(moved+spec['attackOffset'][0], spec['attackOffset'][1], *spec['attackSize'], RED)
    px, py = xy(moved,0)
    d.ellipse((px-4,py-4,px+4,py+4), fill='#172e35')
    d.text((26, 14), monster['name']+' · '+label, font=FONT, fill='#172e35')
    d.text((26, 49), '실제 게임 PNG + 런타임 판정 수치 / 우향 / 1u = 원본 32px', font=SMALL, fill='#716e64')
    legend = '파랑: 몸통·피격 판정  빨강: 데미지 판정  황토: 갈고리 포획 누적 영역  회색: 참고 범위'
    d.text((26, height-31), legend, font=SMALL, fill='#514e46')
    path = OUT / filename
    im.save(path)
    return {'label': label, 'age': age, 'src': uri(path.read_bytes()), 'file': filename}


def main():
    configure()
    sys.path.insert(0, str(ROOT / 'tools/combat'))
    from export_stage1_village_monsters import compile_content
    OUT.mkdir(parents=True, exist_ok=True)
    content = read('unity/Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json')
    assert content == compile_content(), 'Unity catalog differs from canonical data. Export/resolve before documenting.'
    imports = read('unity/Assets/Deviant/Stage1Monsters/Data/import.json')
    catalog = {m['id']: m for m in imports['monsters']}
    for monster in content['monsters']:
        clips = {m['id']: m for m in catalog[monster['id']]['motions']}
        assert abs(sum(clips['attack']['milliseconds'])/1000 - sum(monster['definition']['attackFrameDurations'])) < 1e-8
        for clip in clips.values():
            for frame in clip['frames']:
                raw = (ROOT/'unity'/frame['path']).read_bytes()
                assert hashlib.sha256(raw).hexdigest() == frame['sha256'], frame['path']
                frame['src'] = uri(raw)
        monster['clips'] = clips
        shots = [(-1, '대기 · 피격 박스')]
        if monster['attackKind'] == 'hook-claw':
            shots += [(1.099, '갈고리 포획 · 1.099초'), (2.30, '잡힌 대상 후속 타격 · 2.30초')]
        elif monster['attackKind'] == 'spit':
            shots += [(1.18, '발사 이후 투사체 · 1.18초')]
        else:
            t = (monster['damageStart']+monster['damageEnd'])/2
            shots += [(t, f'유효 타격 · {t:g}초')]
        monster['snapshots'] = [snapshot(monster,clips,age,label,f'{monster["id"]}-{i}.png') for i,(age,label) in enumerate(shots)]
    boss = catalog['05_clock-executioner']
    boss['preview'] = uri((ROOT/'unity'/boss['motions'][0]['frames'][0]['path']).read_bytes())
    files = ['data/combat/stage1_village_monsters.json','data/combat/enemy_hit_reaction.json',
             'data/monsters/chapter1_approved.json',
             'resources/authoring/workbench/source/MonsterReference/approved/manifest.json',
             'tools/combat/export_stage1_village_monsters.py',
             'unity/Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json',
             'unity/Assets/ForestCombatV2/Runtime/ForestCombatWorld.cs',
             'unity/Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json',
             'unity/Assets/Deviant/Stage1Monsters/Data/import.json',
             'unity/Assets/Deviant/Stage1Monsters/Game/Stage1VillageMonsterAttack.cs',
             'unity/Assets/Deviant/Combat/Runtime/FieldMouseEnemy.cs',
             'unity/Assets/Deviant/Combat/Runtime/FieldMouseBrain.cs',
             'unity/Assets/Deviant/Combat/Runtime/Application/EnemyProjectileSweepExecution.cs',
             'unity/Assets/Deviant/Combat/Runtime/CombatArena.cs',
             'unity/Assets/Deviant/Inventory/Resources/TemporaryInventoryContent.asset',
             'unity/Assets/Deviant/Inventory/Unity/InventoryRuntimeHost.cs']
    content['sources'] = [{'path':p,'sha256':hashlib.sha256((ROOT/p).read_bytes()).hexdigest()} for p in files]
    content['builtAt'] = datetime.now().astimezone().isoformat(timespec='seconds')
    content['boss'] = boss
    content['hitReaction'] = read('data/combat/enemy_hit_reaction.json')
    content['encounters'] = read('unity/Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json')['encounters']
    content['head'] = subprocess.check_output(['git','-c',f'safe.directory={ROOT.as_posix()}','rev-parse','--short','HEAD'],cwd=ROOT,text=True).strip()
    template = Path(__file__).with_name('template.html').read_text(encoding='utf-8')
    html = template.replace('__MONSTER_DATA__',json.dumps(content, ensure_ascii=False).replace('</',r'<\/'))
    (OUT/'index.html').write_text(html, encoding='utf-8')
    print(f'Built {OUT / "index.html"} ({len(html.encode())/1024/1024:.2f} MiB), four species, nine annotated PNGs; native frame hashes verified.')


if __name__ == '__main__':
    main()
