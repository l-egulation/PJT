import sys
WITH_GIFS='--with-gifs' in sys.argv
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
REC=ROOT/'outputs/ink_forest/red-ink-gameplay'
result=(REC/'results.txt').read_text(encoding='utf-8')
assert 'RuntimeErrors=0' in result and 'ResetClearsMarks=True' in result
font=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',18)
labels=['작게 · 표식 6×9 / 폭발 32px','기본 · 표식 8×12 / 폭발 40px','크게 · 표식 10×15 / 폭발 48px']
comparison=Image.new('RGB',(960,930),'#161c25');d=ImageDraw.Draw(comparison)
for preset in range(3):
    folder=REC/f'size-{preset}'
    times=[float(v) for v in (folder/'times.txt').read_text().splitlines()]
    images=[Image.open(folder/f'{i:03}.png').convert('RGB') for i in range(len(times))]
    durations=[max(20,round((times[i+1]-times[i])*100)*10) for i in range(len(times)-1)]+[350]
    scaled=[im.resize((960,540),Image.Resampling.NEAREST) for im in images]
    name=f'game_size_{preset}.gif'
    if WITH_GIFS:
        scaled[0].save(HERE/name,save_all=True,append_images=scaled[1:],duration=durations,loop=0,disposal=2,optimize=False)
    # Find the brightest burst peak by its adjusted red highlight, excluding HUD.
    def score(im):
        region=im.crop((560,370,1060,590))
        return sum(r>240 and 60<=g<=100 and 40<=b<=90 for r,g,b in region.getdata())
    peak=max(range(len(images)),key=lambda i:score(images[i]))
    snapshot=images[peak]
    snapshot.save(HERE/f'game_peak_{preset}.png')
    crop=snapshot.crop((560,350,1060,610)).resize((900,260),Image.Resampling.NEAREST)
    # Uniform aspect-preserving crop for comparison.
    crop=snapshot.crop((550,325,1150,610)).resize((900,round(285*1.5)),Image.Resampling.NEAREST)
    crop=crop.crop((0,100,900,360))
    y=preset*310
    d.text((25,y+12),labels[preset],font=font,fill='#f4e8de')
    comparison.paste(crop,(25,y+42))
    if preset==1:
        if WITH_GIFS:
            scaled[0].save(HERE/'actual_gameplay.gif',save_all=True,append_images=scaled[1:],duration=durations,loop=0,disposal=2,optimize=False)
comparison.save(HERE/'size_comparison.png')
(HERE/'verification.txt').write_text(result+'\n실제 Unity Play Mode 캡처. 추가 폭발 피해 수치는 변경하지 않았으며 이번 적용은 표식·폭발 시각 효과입니다.\n기본값: 표식 8×12px, 폭발 40px, PPU32, Point, 압축 없음. F6/F7/F8로 크기 비교.\n',encoding='utf-8')
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>빨강 이펙트 · 실제 Unity 적용</title><style>body{margin:0;background:#141a22;color:#f1e8dc;font:16px/1.8 Malgun Gothic,sans-serif}main{max-width:1150px;margin:auto;padding:30px 20px}section{background:#202832;padding:24px;border-radius:14px;margin:24px 0}img{display:block;max-width:100%;margin:auto;image-rendering:pixelated}h1{font-size:28px}h2{font-size:21px}p{color:#b9c3ce}a{color:#ffb4a4}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:18px}@media(max-width:800px){.grid{display:block}}</style><main><h1>에테르 빨강 이펙트 · 실제 게임 적용</h1><p>TwilightInkHUDTest 씬의 실제 Unity Play Mode 촬영. 에테르 시트를 투명 PNG로 정리하고 실제 타격에 연결했습니다.</p><section><h2>기본 크기: 표식 8×12px / 폭발 40px</h2><img src="game_peak_1.png"><p>타격마다 별도 표식 생성 → 마지막 프레임 유지 → 세 표식 완성 → 모여서 폭발. 피격의 빨간 실루엣과 구분되도록 폭발의 밝은 빨강을 조금 밝게 조절했습니다.</p><p><b>빨강의 역할:</b> 표식 누적·폭발. 현재 적용은 시각 효과 시안이며, 추가 폭발 피해 수치는 변경하지 않았습니다.</p></section><section><h2>세 크기 비교</h2><img src="size_comparison.png"><p>캐릭터와 몬스터의 실루엣을 유지하면서 알아볼 수 있는 중간 크기를 기본으로 선택했습니다.</p></section><section class="grid"><div><h2>작게</h2><img src="game_peak_0.png"><p>표식 6×9 / 폭발 32px</p></div><div><h2>기본</h2><img src="game_peak_1.png"><p>표식 8×12 / 폭발 40px</p></div><div><h2>크게</h2><img src="game_peak_2.png"><p>표식 10×15 / 폭발 48px</p></div></section><section><h2>게임에서 직접 비교</h2><p>F6 작은 크기 · F7 기본 크기 · F8 큰 크기. A/D 이동, J 또는 좌클릭 공격, R 재시작.</p><a href="mark_transparent_sheet.png">투명 표식 시트</a> · <a href="burst_transparent_sheet.png">투명 폭발 시트</a> · <a href="verification.txt">검증 결과</a></section></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8')
print('Packaged actual Unity gameplay GIFs, size comparison and preview.html')
