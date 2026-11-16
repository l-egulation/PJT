from pathlib import Path
import json
from PIL import Image

HERE=Path(__file__).resolve().parent
SOURCES={
 'mark':Path(r'C:\Users\SSAFY\Downloads\8fcdf1dd-8f84-43e1-9873-8ac2effa0cd4.png'),
 'burst':Path(r'C:\Users\SSAFY\Downloads\dbe43dc7-a7e4-4249-8059-c1977be0740a.png')
}
clips={}
for kind,path in SOURCES.items():
    image=Image.open(path).convert('RGB')
    grid=2 if kind=='mark' else 3
    folder=HERE/kind
    folder.mkdir(exist_ok=True)
    frames=[]
    for i in range(grid*grid):
        x,y=i%grid,i//grid
        # Remove only the divider lines on cell boundaries; retain source artwork and white background.
        box=(round(x*image.width/grid)+2,round(y*image.height/grid)+2,round((x+1)*image.width/grid)-2,round((y+1)*image.height/grid)-2)
        frame=image.crop(box).resize((128,128),Image.Resampling.NEAREST)
        frame.save(folder/f'{kind}_{i+1:02}.png')
        frames.append(frame)
    clips[kind]=frames

specs={
 'mark_preview.gif':('mark',[120,90,70,700],True),
 'burst_preview.gif':('burst',[70]*8+[380],False),
 'burst_slow.gif':('burst',[180]*8+[600],False)
}
for name,(kind,durations,extra_blank) in specs.items():
    frames=[f.resize((384,384),Image.Resampling.NEAREST) for f in clips[kind]]
    if extra_blank:
        frames.append(Image.new('RGB',(384,384),'white'))
        durations=durations+[240]
    frames[0].save(HERE/name,save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2,optimize=False)
    gif=Image.open(HERE/name)
    assert gif.size==(384,384)
    assert sum((gif.seek(i) or gif.info['duration']) for i in range(gif.n_frames))==sum(durations)

html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>에테르 빨강 이펙트 재생</title><style>body{background:#171d25;color:#eee;font:16px/1.8 Malgun Gothic,sans-serif}main{max-width:1100px;margin:auto;padding:24px}section{display:flex;gap:24px;flex-wrap:wrap}article{background:#232b35;border-radius:12px;padding:20px;flex:1;min-width:260px}img{max-width:100%;image-rendering:pixelated}h2{font-size:19px}p{color:#bac3cc}a{color:#ffafa3}</style><main><h1>에테르 빨강 이펙트 · 원본 프레임 재생</h1><p>왼쪽 위부터 가로 순서로 재생. 구분선만 제외하고 원본 흰 배경과 형태를 유지했습니다. 프레임당 128px로 정리하고 3배 확대 표시합니다.</p><section><article><h2>표식 생성 · 4프레임</h2><img src="mark_preview.gif"><p>120 / 90 / 70ms → 마지막 모습 700ms 유지.</p></article><article><h2>폭발 · 9프레임</h2><img src="burst_preview.gif"><p>프레임당 70ms, 약 14fps. 마지막 빈 프레임은 380ms 유지.</p></article><article><h2>폭발 · 느리게 보기</h2><img src="burst_slow.gif"><p>프레임당 180ms, 약 5.6fps.</p></article></section><p>빨강의 역할: 표식 누적·폭발. 두 시트의 개별 재생 미리보기입니다.</p></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8')
(HERE/'timing.json').write_text(json.dumps({'sourceFiles':{k:str(v) for k,v in SOURCES.items()},'order':'row-major','frameSize':[128,128],'previewSize':[384,384],'preserveWhiteBackground':True},ensure_ascii=False,indent=2),encoding='utf-8')
print('Created mark_preview.gif, burst_preview.gif, burst_slow.gif and preview.html')
