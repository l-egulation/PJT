import sys
WITH_GIFS='--with-gifs' in sys.argv
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import bisect, shutil, json
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
ART=ROOT/'unity/Assets/Deviant/World/Art'
GEN=Path(r'C:\Users\SSAFY\.codex\generated_images\01a12e1a-6e0c-71c2-81ce-79ae27b362ad')
SOURCES={'up':'exec-d65fb24d-3332-4361-98b1-dfb48a582a4d.png','down':'exec-eaf689d6-be31-4f8e-9629-a7a9ee4a6d93.png'}
N=Image.Resampling.NEAREST
font=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',18)
small=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',14)
effects={}
for name,filename in SOURCES.items():
    source=Image.open(GEN/filename).convert('RGBA')
    shutil.copy2(GEN/filename,HERE/f'{name}_generated_source.png')
    cellx,celly=source.width//3,source.height//2
    folder=HERE/name;folder.mkdir(exist_ok=True)
    tiles=[];sheet=Image.new('RGBA',(384,256))
    for i in range(6):
        tile=source.crop((i%3*cellx,i//3*celly,(i%3+1)*cellx,(i//3+1)*celly)).resize((128,128),N)
        # Sprite import cleanup: quantize generated RGBA to an opaque three-tone pixel palette.
        # The generated transparent RGB can contain haze, so alpha defines the silhouette.
        clean=Image.new('RGBA',(128,128))
        pixels=[]
        for r,g,b,a in tile.getdata():
            if a<245:pixels.append((0,0,0,0))
            else:
                v=(r+g+b)//3
                gray=255 if v>=235 else 218 if v>=170 else 153
                pixels.append((gray,gray,gray,255))
        clean.putdata(pixels);clean.save(folder/f'{i+1:02}.png')
        sheet.alpha_composite(clean,(i%3*128,i//3*128));tiles.append(clean)
    sheet.save(HERE/f'{name}_128px_3x2.png');effects[name]=tiles

def savegif(frames,path,durations):
    sample=Image.new('RGB',(320,180*3))
    for n,index in enumerate([0,len(frames)//2,max(0,len(frames)//2-2)]):
        sample.paste(frames[index].resize((320,180),N),(0,n*180))
    palette=sample.quantize(colors=256)
    indexed=[f.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    indexed[0].save(path,save_all=True,append_images=indexed[1:],duration=durations,loop=0,disposal=2,optimize=False)

board=Image.new('RGB',(816,660),'#1d2630');draw=ImageDraw.Draw(board)
for row,(name,title) in enumerate([('up','1타 · 아래 → 위 · 6프레임'),('down','2타 · 위 → 아래 · 6프레임')]):
    draw.text((16,row*330+10),title,font=font,fill='white')
    for i,tile in enumerate(effects[name]):
        x=16+i%3*264;y=row*330+42+i//3*144
        panel=Image.new('RGBA',(256,128),'#293541')
        panel.alpha_composite(tile,(64,0));board.paste(panel.convert('RGB'),(x,y))
        draw.text((x+5,y+4),f'{i+1:02}',font=small,fill='#aebaca')
board.save(HERE/'frame_board.png')
for name,tiles in effects.items():
    frames=[]
    for tile in tiles+[Image.new('RGBA',(128,128))]:
        canvas=Image.new('RGBA',(128,128),'#26313d');canvas.alpha_composite(tile)
        frames.append(canvas.convert('RGB').resize((384,384),N))
    savegif(frames,HERE/f'{name}_effect.gif',[30,30,40,40,30,30,600])

bg=Image.new('RGBA',(640,360),'#252333')
for name in ['far','mid']:bg.alpha_composite(Image.open(ART/'twilight'/f'{name}.png').convert('RGBA').crop((0,0,640,360)))
floor=285
for x in range(0,640,32):bg.alpha_composite(Image.open(ART/'twilight'/f'top{(x//32)%4}.png').convert('RGBA'),(x,floor))
screen=Image.open(ROOT/'outputs/ink_forest/red-ink-gameplay/size-1/000.png').convert('RGBA')
bg.alpha_composite(screen.crop((0,0,1280,160)).resize((640,80),N),(0,0))
bg.alpha_composite(screen.crop((0,610,1280,720)).resize((640,55),N),(0,305))
motions={name:[Image.open(ART/f'ApprovedDeathCombo/attack{n}/{i:02}.png').convert('RGBA') for i in range(1,count+1)] for name,n,count in [('up',1,6),('down',2,7)]}
idle=Image.open(ART/'Character/idle/01.png').convert('RGBA')
seconds={'up':[.08,.11,.07,.04,.05,.12],'down':[.04,.10,.04,.11,.10,.11,.12]}
feet={'up':[119,123,120,120,120,120],'down':[120,120,120,121,120,120,120]}
ends={k:[sum(v[:i+1]) for i in range(len(v))] for k,v in seconds.items()}
# Each short VFX runs at the actual swing transition, rather than throughout preparation/recovery.
fxstarts={'up':.25,'down':.13}
fxends=[.03,.06,.10,.14,.17,.20]
def scene(t,mode='combo'):
    canvas=bg.copy();local=t-.40
    if mode=='combo':
        name='up' if local<.47 else 'down';local=local if name=='up' else local-.47
    else:name=mode
    active=0<=local<ends[name][-1]
    body=idle;foot=121;active_effect=None
    if active:
        index=min(len(motions[name])-1,bisect.bisect_right(ends[name],local))
        body=motions[name][index];foot=feet[name][index]
        elapsed=local-fxstarts[name]
        if 0<=elapsed<.20:
            fxindex=min(5,bisect.bisect_right(fxends,elapsed))
            active_effect=effects[name][fxindex]
    canvas.alpha_composite(body,(230,floor-foot))
    if active_effect is not None:
        canvas.alpha_composite(active_effect,(240,floor-121))
    image=canvas.convert('RGB').resize((960,540),N);d=ImageDraw.Draw(image)
    d.rounded_rectangle((285,18,825,76),radius=7,fill='#192632')
    d.text((300,25),'기본 공격 · 흰색 베기 · 합성 미리보기',font=font,fill='white')
    title='1타 아래 → 위' if name=='up' else '2타 위 → 아래'
    d.text((300,51),title+'  |  128×128px / 각 6프레임',font=small,fill='#bdc9d5')
    return image
for mode,duration in [('combo',2.1),('up',1.45),('down',1.6)]:
    frames=[scene(i*.02,mode) for i in range(round(duration/.02))]
    if WITH_GIFS:savegif(frames,HERE/f'{mode}_attack_preview.gif',20)
scene(.75).save(HERE/'up_peak.png');scene(1.09).save(HERE/'down_peak.png')
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>기본 공격 상하 베기</title><style>body{background:#18222d;color:#ecf2f7;font:16px/1.8 Malgun Gothic;margin:0}main{max-width:1000px;margin:auto;padding:24px}img{max-width:100%;image-rendering:pixelated}section{padding:20px;background:#25313e;border-radius:12px;margin:20px 0}a{color:#b9d7ff}</style><main><h1>기본 공격 · 올려베기 / 내려베기</h1><p>실제 캐릭터의 공격 프레임과 게임 배경을 사용한 합성 미리보기.</p><section><img src="frame_board.png"><p>1타: 아래에서 위로 올려 벤다. 2타: 위에서 아래로 내려 벤다. 준비 동작을 지난 실제 베기 구간에만 약 0.20초 동안 궤적이 나타난다.</p></section><section><img src="up_peak.png"><img src="down_peak.png"></section><section><img src="frame_board.png"><p>각 6프레임 · 한 셀 128×128px · 흰색과 회색 · 투명 PNG</p><a href="up_128px_3x2.png">올려베기 시트</a> / <a href="down_128px_3x2.png">내려베기 시트</a></section></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8')
(HERE/'manifest.json').write_text(json.dumps({'previewOnly':True,'cell':[128,128],'framesPerAttack':6,'effectSeconds':[.03,.03,.04,.04,.03,.03],'palette':['#FFFFFF','#DADADA','#999999'],'motion':'ApprovedDeathCombo attack1 / attack2'},ensure_ascii=False,indent=2),encoding='utf-8')
for name in effects:
    assert all(t.size==(128,128) for t in effects[name])
    assert all(t.getbbox() for t in effects[name])
print('Created transparent attack sheets, effect GIFs, and character composite previews.')
