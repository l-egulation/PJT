"""잉크 버프·상태이상 FX 후보 미리보기를 만든다.

source/preview_template.html 의 /*__IMAGES__*/ 자리에 게임 캐릭터 대기 동작
(characters/ink/common/runtime/forest-character/idle)과 1스테이지 빗자루 추적자
대기 동작(monsters/stage-01/normal/broom-stalker/runtime/approved-motion/idle/frames)
PNG를 그대로(재인코딩 없이) data URI로 넣어 preview/ink_buff_fx_candidates.html 을 쓴다.

사용: python effects/ink-buffs/source/tools/build_preview.py   (리소스 저장소 루트 기준)
"""
import base64, json, pathlib

HERE = pathlib.Path(__file__).resolve().parent
ASSET = HERE.parent.parent
ROOT = ASSET.parent.parent
SOURCES = {
    "player": ROOT / "characters/ink/common/runtime/forest-character/idle",
    "monster": ROOT / "monsters/stage-01/normal/broom-stalker/runtime/approved-motion/idle/frames",
}

def uris(folder):
    return ["data:image/png;base64," + base64.b64encode(p.read_bytes()).decode() for p in sorted(folder.glob("*.png"))]

images = {k: uris(v) for k, v in SOURCES.items()}
html = (ASSET / "source/preview_template.html").read_text(encoding="utf-8").replace("/*__IMAGES__*/", json.dumps(images))
out = ASSET / "preview/ink_buff_fx_candidates.html"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(html, encoding="utf-8", newline="\n")
print(out, len(html), {k: len(v) for k, v in images.items()})
