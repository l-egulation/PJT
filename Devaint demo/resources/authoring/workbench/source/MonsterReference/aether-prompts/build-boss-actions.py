"""Build Aether reference pairs and prompts; never modify approved sprites."""
import hashlib
import html
import json
from pathlib import Path
import zipfile

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "local-assets/boss-aether-actions-v1"
SIZE = (384, 352)
OFFSET = (64, 80)
GROUND = 320
SCALE = 4
JOBS = [
    ("M01", "잠행", "몸이 잉크로 녹아내려 웅덩이만 남음", "ink-submerge", "boss", "puddle", 1300,
     "걷기 대신 사용하는 사라지기. 온전한 몸이 바닥 아래로 내려가는 출력은 제외."),
    ("M02", "솟구치기", "잉크가 모임 → 몸체 형성 → 무겁게 착지", "ink-emerge", "puddle", "boss", 1500,
     "도착 지점 예고는 게임에서 먼저 표시. 몸체가 완성되어 무게를 받는 순간 주변 잉크 피해를 한 번 동기화."),
    ("B01", "공격 1 · 낙하시침", "양팔을 벌림 → 소환 유지 → 팔 내리기", "needle-rain", "boss", "boss", 2050,
     "낙점 예고와 떨어지는 시침은 게임에서 별도 제작. 열린 팔 자세는 게임에서 유지 가능."),
    ("B02", "공격 2 · 양손 지면 파쇄", "두 주먹을 머리 위에 모음 → 동시에 내려찍기 → 회복", "two-fist-slam", "boss", "boss", 2020,
     "두 주먹이 바닥에 닿는 순간 전방으로 진행하는 지면 파쇄를 게임에서 연결."),
    ("B03", "공격 3 · 잉크 포효", "짧게 힘 모음 → 가슴을 위로 확 펼침 → 머리 젖힌 자세 유지 → 기본 자세", "ink-roar", "boss", "boss", 1900,
     "v2: 깊게 숙이기·힘 빠져 처지기 제외. 머리가 기본형보다 높아진 포효 자세를 길게 유지. 손은 낮게 두고 폭발은 별도로 연결."),
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source = ROOT / "approved/05_clock-executioner/base/frames/00.png"
    base = Image.open(source).convert("RGBA")
    assert base.size == (256, 256)
    palette = json.loads((ROOT / "approved/master-palette.json").read_text(encoding="utf-8"))["colors"]
    allowed = {tuple(bytes.fromhex(c[1:])) for c in palette}
    assert all(a in (0, 255) and (a == 0 or (r, g, b) in allowed)
               for _, (r, g, b, a) in base.getcolors(65536))
    boss = Image.new("RGBA", SIZE)
    boss.alpha_composite(base, OFFSET)
    # Native-pixel construction reference only; not an approved motion or game FX.
    puddle = Image.new("RGBA", SIZE)
    draw = ImageDraw.Draw(puddle)
    draw.polygon([(110, 309), (130, 309), (130, 305), (161, 305), (161, 302),
                  (207, 302), (207, 304), (235, 304), (235, 308), (260, 308),
                  (260, 311), (280, 311), (280, 315), (296, 315), (296, 318),
                  (273, 318), (273, 319), (117, 319), (117, 318), (90, 318),
                  (90, 315), (104, 315), (104, 312), (110, 312)], fill=palette[0])
    draw.polygon([(143, 308), (171, 308), (171, 306), (206, 306), (206, 308),
                  (227, 308), (227, 310), (198, 310), (198, 309), (143, 309)], fill=palette[1])
    draw.rectangle((123, 313, 153, 313), fill=palette[1])
    draw.rectangle((234, 313, 262, 313), fill=palette[1])
    for name, im in (("boss", boss), ("puddle", puddle)):
        im.save(OUT / f"{name}-native-transparent.png")
        white = Image.new("RGBA", SIZE, "white")
        white.alpha_composite(im)
        enlarged = white.convert("RGB").resize((SIZE[0] * SCALE, SIZE[1] * SCALE), Image.Resampling.NEAREST)
        enlarged.save(OUT / f"{name}-4x-white.png")
        assert {rgb for _, rgb in enlarged.getcolors(65536)} <= allowed | {(255, 255, 255)}
    records, cards = [], []
    for ident, title, flow, suffix, start, end, ms, note in JOBS:
        prompt = (ROOT / f"aether-prompts/prompts/05_clock-executioner-{suffix}.txt").read_text(encoding="utf-8").strip()
        # Exact numbers communicate the shared framing; they are not gameplay scale.
        prompt += ("\n\nREFERENCE FRAMING: Both images are 1536 x 1408 px with the SAME 4x nearest-neighbor pixel scale. "
                   "The original standing body is 256 x 256 logical px placed at logical offset (64, 80), "
                   "with the floor boundary at logical y=320 (image y=1280). Keep this framing fixed. "
                   "White is the background only, never a new character highlight color.\n")
        filename = f"{ident}-{suffix}.txt"
        (OUT / filename).write_text(prompt, encoding="utf-8")
        records.append(dict(id=ident, title=title, flow=flow, start=f"{start}-4x-white.png",
                            end=f"{end}-4x-white.png", prompt=filename, suggestedDurationMs=ms, note=note))
        cards.append(f'''<article id="{ident}"><h2>{ident} · {html.escape(title)}</h2>
<p class="flow">{html.escape(flow)}</p><p>{html.escape(note)}</p>
<div class="pair"><figure><img src="{start}-4x-white.png" alt="{title} 시작 참조"><figcaption>시작 이미지 <a href="{start}-4x-white.png" download>PNG 받기</a></figcaption></figure>
<figure><img src="{end}-4x-white.png" alt="{title} 끝 참조"><figcaption>끝 이미지 <a href="{end}-4x-white.png" download>PNG 받기</a></figcaption></figure></div>
<p><button data-copy="p-{ident}">영어 프롬프트 복사</button> <a href="{filename}" download>TXT 받기</a> · 시간 초안 {ms / 1000:g}초</p>
<details><summary>영어 프롬프트 보기</summary><textarea id="p-{ident}" readonly>{html.escape(prompt)}</textarea></details></article>''')
    manifest = dict(status="generation-inputs-only-not-generated-or-approved", facing="screen-left",
                    logicalCanvas=SIZE, integerScale=SCALE, sourceOffset=OFFSET, groundY=GROUND,
                    source=str(source.relative_to(ROOT)), sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                    palette=palette, puddleStatus="construction-reference-only", jobs=records)
    (OUT / "inputs.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>시계탑 보스 · 에테르 동작 5종</title><style>
*{box-sizing:border-box}body{margin:0;background:#eeece6;color:#292824;font:16px/1.65 system-ui,sans-serif}main{max-width:1040px;margin:auto;padding:32px 24px}h1{font-size:30px;margin-bottom:8px}h2{font-size:23px}a{color:#3d514c;text-underline-offset:3px}nav{display:flex;gap:16px;flex-wrap:wrap;margin:24px 0}article{background:#fff;border:1px solid #ccc8bd;border-radius:12px;padding:24px;margin:24px 0}.flow{font-size:19px;font-weight:650}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;text-align:center}img{display:block;width:100%;max-height:360px;object-fit:contain;background:#fff;image-rendering:pixelated;border:1px solid #ddd}figcaption{padding:8px}button{background:#34443c;color:white;border:0;border-radius:6px;padding:10px 16px;cursor:pointer;font:inherit}textarea{width:100%;height:300px;resize:vertical;padding:16px;line-height:1.5}summary{cursor:pointer}small{color:#605c53}.status{background:#ddd8ca;padding:14px;border-radius:8px}@media(max-width:560px){.pair{grid-template-columns:1fr}main{padding:20px 12px}article{padding:16px}}</style>
<main><h1>시계탑 보스 · 에테르 동작 5종</h1>
<p class="status">생성용 입력 자료입니다. 아직 생성된 애니메이션이나 확정 프레임은 아닙니다.</p>
<p>각 동작을 별도로 생성하세요. 시작·끝 참조 칸에 아래 PNG를 각각 넣고 영어 프롬프트를 복사하면 됩니다. 모든 입력은 같은 <b>1536×1408 · 4배 확대 · 발 기준선</b>입니다. 그림의 방향은 왼쪽입니다.</p>
<p>대기·죽음은 이번 범위에서 제외합니다. 걷기 대신 잠행과 솟구치기를 연결합니다. 보스 피격은 별도 자세 없이 흰 점멸·파편·타격음으로 처리합니다.</p>
<p>출력은 우선 원본 프레임을 충분히 받아온 뒤 골라서 연결합니다. 에테르 생성 결과의 픽셀·색상·크기 준수는 돌아온 결과에서 별도로 검사합니다. 바닥 예고·낙하시침·균열·잉크 폭발과 피해 판정은 게임에서 동기화합니다.</p>
<p><a download href="boss-aether-actions-v1.zip">전체 입력 세트 ZIP 받기</a></p><nav>'''
    page += "".join(f'<a href="#{x[0]}">{html.escape(x[1])}</a>' for x in JOBS) + "</nav>" + "".join(cards)
    page += '''<p><small>웅덩이는 동일 바닥 위치를 연결하기 위한 픽셀 참조입니다. 게임용 이펙트 확정본이 아닙니다. 본체 기준 원본은 approved/05_clock-executioner/base/frames/00.png 그대로입니다.</small></p></main>
<script>document.querySelectorAll('[data-copy]').forEach(b=>b.addEventListener('click',async()=>{const t=document.getElementById(b.dataset.copy);try{await navigator.clipboard.writeText(t.value);b.textContent='복사됨'}catch(e){t.closest('details').open=true;t.focus();t.select();document.execCommand('copy');b.textContent='복사됨'}}));</script></html>'''
    (OUT / "index.html").write_text(page, encoding="utf-8")
    archive = OUT / "boss-aether-actions-v1.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.iterdir()):
            if p.is_file() and p != archive:
                z.write(p, p.name)
    # The source canvas must remain byte-identical inside the padded transparent image.
    assert boss.crop((64, 80, 320, 336)).tobytes() == base.tobytes()
    assert Image.open(OUT / "boss-4x-white.png").size == Image.open(OUT / "puddle-4x-white.png").size
    assert puddle.getbbox()[3] == GROUND
    print(json.dumps(dict(output=str(OUT), jobs=len(records), imageSize=[1536, 1408],
                          sourcePixels="unchanged", inputPalette="six master colors plus white background", zip=str(archive)), ensure_ascii=False))


if __name__ == "__main__":
    main()
