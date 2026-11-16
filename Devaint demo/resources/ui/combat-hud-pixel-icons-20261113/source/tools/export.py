# 무기 14종·스킬 40종을 게임 스프라이트 이름으로 내보낸다.
#   source/native/<이름>.png : 1배 원본(무기 48×48, 스킬 44×44, 캔버스 1화소 = 그림 1칸)
#   runtime/<이름>.png       : 2배 최근접 확대본(Unity 에서 Bilinear·밉맵으로 읽어 1080p 에서 1:1, 그 밖의 크기에서 부드럽게 줄어든다)
# 사용: python export.py <출력 폴더>   (같은 코드로 같은 바이트가 나온다)
import os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import pixicon, weapons, skills

WEAPON_SAFE, SKILL_SAFE = 27, 21.5      # 무기 원 링 안쪽(29)에서 2칸, 스킬 원 크림 선(24.5)에서 3칸 안쪽까지만
SKILL_IDS = {   # skills.SKILLS 의 무기 순서 → bindings.json 의 스킬 아이콘 이름(기본·범위·이동·후속)
    "검": "special-base_sword_{}", "채찍": "special-whip_{}", "레이피어": "special-rapier_{}", "단검": "special-dagger_{}",
    "망치": "special-hammer_{}", "방패": "special-shield_{}", "대검": "special-greatsword_{}", "폭탄": "special-bomb_{}",
    "수리검": "special-shuriken_{}",
}
SLOTS = ["default", "range", "movement", "follow_up"]
LAUNCHER = ["launcher-ball", "launcher-laser", "launcher-shotgun", "launcher-grenade"]

def items():
    for key, fn in weapons.WEAPONS.items(): yield key, fn, WEAPON_SAFE
    for weapon, lst in skills.SKILLS:
        for k, (_, fn) in enumerate(lst):
            yield (LAUNCHER[k] if weapon == "발사기" else SKILL_IDS[weapon].format(SLOTS[k])), fn, SKILL_SAFE

def save(im, path):
    im.save(path, optimize=False, compress_level=9)

if __name__ == "__main__":
    out = sys.argv[1]
    for sub in ("source/native", "runtime"): os.makedirs(os.path.join(out, sub), exist_ok=True)
    names = []
    for name, fn, safe in items():
        im = pixicon.render(fn, safe, name)
        save(im, os.path.join(out, "source/native", name + ".png"))
        save(im.resize((im.width * 2, im.height * 2), Image.NEAREST), os.path.join(out, "runtime", name + ".png"))
        names.append(name)
    if pixicon.SAFE_HITS: raise SystemExit("\n".join(pixicon.SAFE_HITS))
    if pixicon.LINT: raise SystemExit("\n".join(pixicon.LINT))
    print(len(names), "장:", ", ".join(names))
