from pathlib import Path
import argparse,bisect,json,shutil
from PIL import Image,ImageDraw,ImageFont
parser=argparse.ArgumentParser(description='Rebuild PNG sources; GIF previews are optional.')
parser.add_argument('--with-gifs', action='store_true', help='Explicitly regenerate the two legacy GIF previews.')
WITH_GIFS=parser.parse_args().with_gifs
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
UNITY=ROOT/'unity/Assets/Deviant/World'
ART=UNITY/'Art'
GEN=Path(r'C:\Users\SSAFY\.codex\generated_images\01a12e1a-6e0c-71c2-81ce-79ae27b362ad\exec-515aada2-8b02-4a64-a063-1319c307fce8.png')
NEAREST=Image.Resampling.NEAREST
src=Image.open(GEN).convert('RGBA');shutil.copy2(GEN,HERE/'burst_generated_source.png')
assert src.width==src.height
side=src.width//3
bursts=[src.crop((i%3*side,i//3*side,(i%3+1)*side,(i//3+1)*side)).resize((128,128),NEAREST) for i in range(9)]
# Keep generated art, fit the peak to a readable 72px footprint, and preserve relative motion.
boxes=[f.getchannel('A').point(lambda a:255 if a>96 else 0).getbbox() for f in bursts]
valid=[b for b in boxes if b]
extent=max(max(b[2]-b[0],b[3]-b[1]) for b in valid)
fit=round(128*72/extent)
fx=[];sheet=Image.new('RGBA',(384,384))
folder=HERE/'burst';folder.mkdir(exist_ok=True)
for i,f in enumerate(bursts):
    tile=Image.new('RGBA',(128,128));tile.alpha_composite(f.resize((fit,fit),NEAREST),((128-fit)//2,(128-fit)//2))
    tile.save(folder/f'{i+1:02}.png');sheet.alpha_composite(tile,(i%3*128,i//3*128));fx.append(tile)
sheet.save(HERE/'burst_128px_3x3.png')
markdir=ROOT/'아트/이펙트/에테르_빨강_게임적용_v1/mark'
markimgs=[Image.open(markdir/f'{i:02}.png').convert('RGBA') for i in range(1,5)]
union=(60,58,68,70)
marks=[f.crop(union).resize((10,14),NEAREST) for f in markimgs]

def flattened(f):
    bg=Image.new('RGBA',(128,128),'#232b34');bg.alpha_composite(f)
    return bg.convert('RGB').resize((384,384),NEAREST)
seq=[flattened(f) for f in fx]+[flattened(Image.new('RGBA',(128,128)))]
if WITH_GIFS:
    seq[0].save(HERE/'burst_preview.gif',save_all=True,append_images=seq[1:],duration=[30,40,40,80,50,50,60,70,40,500],loop=0,disposal=2,optimize=False)

# Compose a preview from game art only. No Unity execution or project modifications.
bg=Image.new('RGBA',(640,360),'#252333')
for name in ['far','mid']:
    layer=Image.open(ART/'twilight'/f'{name}.png').convert('RGBA')
    bg.alpha_composite(layer.crop((0,0,640,360)))
floor=285
for x in range(0,640,32):
    top=Image.open(ART/'twilight'/f'top{(x//32)%4}.png').convert('RGBA')
    bg.alpha_composite(top,(x,floor))
# Use actual HUD strips without any previously rendered character/effect pixels.
screen=Image.open(ROOT/'outputs/ink_forest/red-ink-gameplay/size-1/000.png').convert('RGBA')
bg.alpha_composite(screen.crop((0,0,1280,160)).resize((640,80),NEAREST),(0,0))
bg.alpha_composite(screen.crop((0,610,1280,720)).resize((640,55),NEAREST),(0,305))
hero=[Image.open(ART/f'Character/attack/{i:02}.png').convert('RGBA') for i in range(1,13)]
idle=Image.open(ART/'Character/idle/01.png').convert('RGBA')
foes=[Image.open(UNITY/f'Monsters/Art/boar/move/{i:02}.png').convert('RGBA') for i in range(1,13)]
data=json.loads((UNITY/'Data/integration.json').read_text(encoding='utf-8-sig'))
attack=next(c for c in data['clips'] if c['id']=='attack')
ends=[];total=0
for v in attack['seconds']:total+=v;ends.append(total)
starts=[.40,1.70]
hits=sorted(s+sum(attack['seconds'][:i]) for s in starts for i in [4,7])
gather=hits[2]+.14;burst_start=gather+.12
burst_times=[.03,.04,.04,.08,.05,.05,.06,.07,.04]
burst_ends=[];bt=0
for v in burst_times:bt+=v;burst_ends.append(bt)
font=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',18)
small=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',14)
frames=[]
for tick in range(210):
    t=tick*.02;canvas=bg.copy()
    recent=max((h for h in hits if h<=t),default=-100)
    recoil=round(3*max(0,1-(t-recent)/.16))
    foe=foes[int(t/.1)%12];box=foe.getbbox()
    ex=315+recoil;ey=floor-box[3]
    canvas.alpha_composite(foe,(ex,ey))
    active=next((t-s for s in starts if 0<=t-s<total),None)
    i=min(11,bisect.bisect_right(ends,active)) if active is not None else None
    body=hero[i] if i is not None else idle
    feet=attack['feet'][i] if i is not None else 121
    canvas.alpha_composite(body,(230,floor-feet))
    center=(ex+(box[0]+box[2])//2,ey+(box[1]+box[3])//2)
    head=(center[0],ey+box[1]-14)
    draw=ImageDraw.Draw(canvas)
    draw.rectangle((head[0]-30,head[1]-23,head[0]+30,head[1]-19),fill='#15232d')
    draw.rectangle((head[0]-29,head[1]-22,head[0]+29,head[1]-20),fill='#db7766')
    events=hits[:3] if t<burst_start else [hits[3]]
    for n,event in enumerate(events):
        if t<event:continue
        offset=(-16,0,16)[n]
        x,y=head[0]+offset,head[1]
        if gather<=t<burst_start:
            p=(t-gather)/.12;p=p*p*(3-2*p)
            x=round(x+(center[0]-x)*p);y=round(y+(center[1]-y)*p)
        stamp=marks[min(3,int((t-event)/.03))]
        canvas.alpha_composite(stamp,(x-5,y-7))
    if burst_start<=t<burst_start+bt:
        index=min(8,bisect.bisect_right(burst_ends,t-burst_start))
        canvas.alpha_composite(fx[index],(center[0]-64,center[1]-64))
    image=canvas.convert('RGB').resize((960,540),NEAREST)
    ld=ImageDraw.Draw(image)
    label='머리 위 가로 표식 · 1개 → 2개 → 3개' if t<gather else '세 표식이 모임 → 강한 잉크 폭발' if t<burst_start+bt else '다음 표식'
    ld.rounded_rectangle((280,18,735,77),radius=7,fill='#18232d')
    ld.text((295,25),'빨강 잉크 v3 · 합성 미리보기',font=font,fill='#f3e8d8')
    ld.text((295,51),label,font=small,fill='#ffaba0')
    frames.append(image)
    if tick in [109,120]:image.save(HERE/f'check_{tick:03}.png')
if WITH_GIFS:
    frames[0].save(HERE/'head_marks_attack.gif',save_all=True,append_images=frames[1:],duration=20,loop=0,disposal=2,optimize=False)
board=Image.new('RGB',(800,860),'#18212a');d=ImageDraw.Draw(board)
d.text((16,12),'폭발 9프레임 · 밝은 중심 / 굵은 잉크 파열',font=font,fill='#f3e8d8')
for n,f in enumerate(fx):
    x,y=16+n%3*260,50+n//3*268
    b=Image.new('RGBA',(256,256),'#29313b');b.alpha_composite(f.resize((256,256),NEAREST))
    board.paste(b.convert('RGB'),(x,y))
board.save(HERE/'burst_board.png')
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>빨강 v3 미리보기</title><style>body{background:#151d26;color:#f3e8dc;font:16px/1.8 Malgun Gothic,sans-serif;margin:0}main{max-width:1080px;margin:auto;padding:28px 20px}section{background:#222c36;padding:24px;border-radius:14px;margin:24px 0}img{max-width:100%;display:block;margin:auto;image-rendering:pixelated}p{color:#bbc5cf}a{color:#ffb0a2}</style><main><h1>빨강 v3 · 머리 위 가로 표식</h1><p>게임 아트로 만든 합성 미리보기입니다. Unity 적용 변경은 회수했습니다.</p><section><img src="check_109.png" alt="표식 합성 검증 화면"><img src="check_120.png" alt="폭발 합성 검증 화면"><p>표식은 몬스터 머리 위에 가로로 쌓입니다. 세 번째 표식이 보인 뒤 세 표식이 몸통 중심으로 모이고, 밝은 중심과 굵은 붉은 잉크가 크게 터집니다.</p><p><b>역할:</b> 빨강 — 표식 누적·폭발.</p></section><section><h2>폭발 단독</h2><img src="burst_board.png" alt="폭발 원본 9프레임 보드"><p>표식 10×14px · 간격 16px · 폭발 최대 약72px. 한 셀은128×128px.</p></section><section><h2>폭발 프레임</h2><img src="burst_board.png"><a href="burst_128px_3x3.png">투명 시트</a></section></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8')
(HERE/'manifest.json').write_text(json.dumps({'previewOnly':True,'markPosition':'horizontal above monster head','markSize':[10,14],'markSpacing':16,'burstPeak':72,'frameCell':128,'burstFrameSeconds':burst_times,'role':'표식 누적·폭발'},ensure_ascii=False,indent=2),encoding='utf-8')
if WITH_GIFS:
    g=Image.open(HERE/'head_marks_attack.gif');assert g.size==(960,540)
    assert sum((g.seek(i) or g.info['duration']) for i in range(g.n_frames))==4200
print('Preview sources rebuilt; optional GIF export:', WITH_GIFS)
