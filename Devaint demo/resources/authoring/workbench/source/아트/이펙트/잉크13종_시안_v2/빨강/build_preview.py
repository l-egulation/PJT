import sys
if '--rebuild-retired' not in sys.argv:
    print('Retired proposal. Use --rebuild-retired explicitly to regenerate original candidate outputs; current material: '+str(__file__).replace('build_preview.py','preview.html'))
    raise SystemExit(0)
from pathlib import Path
import bisect, json, shutil, hashlib
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
UNITY=ROOT/'unity/Assets/Deviant/World'
GEN=Path(r'C:\Users\SSAFY\.codex\generated_images\01a12e1a-6e0c-71c2-81ce-79ae27b362ad')
SOURCES={'mark':'exec-50d50cc8-36e1-473e-be51-5ce886b9898a.png','burst':'exec-21076fc5-6bc2-4e80-8a77-003861b8e7c0.png'}
NEAREST=Image.Resampling.NEAREST
font=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',18)
small=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',14)
clips={}
for kind,name in SOURCES.items():
    shutil.copy2(GEN/name,HERE/f'{kind}_generated_source.png')
    src=Image.open(GEN/name).convert('RGBA')
    assert src.width*3==src.height*4
    side=src.width//4
    clips[kind]=[src.crop((i%4*side,i//4*side,(i%4+1)*side,(i//4+1)*side)).resize((128,128),NEAREST) for i in range(12)]
    sheet=Image.new('RGBA',(512,384))
    folder=HERE/kind
    folder.mkdir(exist_ok=True)
    for i,f in enumerate(clips[kind]):
        f.save(folder/f'{kind}_{i+1:02}.png')
        sheet.alpha_composite(f,(i%4*128,i//4*128))
    sheet.save(HERE/f'{kind}_128x128_4x3.png')
    board=Image.new('RGB',(800,658),'#161b22')
    d=ImageDraw.Draw(board)
    d.text((16,12),('표식 생성 · 1개를 나타내고 마지막 프레임 유지' if kind=='mark' else '폭발 · 세 표식 모임 → 합쳐짐 → 짧은 파열'),font=font,fill='#f3ede3')
    for i,f in enumerate(clips[kind]):
        x,y=12+i%4*196,50+i//4*202
        d.rectangle((x,y,x+191,y+191),fill='#232b34')
        scaled=f.resize((192,192),NEAREST)
        board.paste(scaled,(x,y),scaled)
        d.text((x+5,y+4),f'{i+1:02}',font=small,fill='#88949e')
    board.save(HERE/f'{kind}_board.png')

# Use one generated mark asset at each of three fixed positions.
# Derive their anchors and size from the first burst frame, so the transition matches.
first=clips['burst'][0]
alpha=first.getchannel('A')
occupied={(x,y) for y in range(128) for x in range(128) if alpha.getpixel((x,y))>128}
components=[]
while occupied:
    seed=occupied.pop(); todo=[seed]; part=[seed]
    while todo:
        x,y=todo.pop()
        for q in [(x+1,y),(x-1,y),(x,y+1),(x,y-1)]:
            if q in occupied: occupied.remove(q);todo.append(q);part.append(q)
    if len(part)>12:
        xs,ys=zip(*part);components.append((min(xs),min(ys),max(xs)+1,max(ys)+1,len(part)))
components=sorted(components,key=lambda b:-b[4])[:3]
assert len(components)==3
components=sorted(components,key=lambda b:(b[1],b[0]))
anchors=[((b[0]+b[2])//2,(b[1]+b[3])//2) for b in components]
target=(max(b[2]-b[0] for b in components),max(b[3]-b[1] for b in components))
boxes=[f.getchannel('A').point(lambda a:255 if a>96 else 0).getbbox() for f in clips['mark']]
union=(min(b[0] for b in boxes),min(b[1] for b in boxes),max(b[2] for b in boxes),max(b[3] for b in boxes))
stamps=[f.crop(union).resize(target,NEAREST) for f in clips['mark']]

def marks_layer(t,events,offset=(0,0)):
    layer=Image.new('RGBA',(128,128))
    for index,event in enumerate(events):
        if t<event:continue
        n=min(11,int((t-event)/.01))
        x,y=anchors[index]
        layer.alpha_composite(stamps[n],(x-target[0]//2+offset[0],y-target[1]//2+offset[1]))
    return layer

def flat(f,scale=3):
    bg=Image.new('RGBA',(128,128),'#232b34');bg.alpha_composite(f)
    return bg.convert('RGB').resize((128*scale,128*scale),NEAREST)

for kind in ['mark','burst']:
    seq=[flat(f) for f in clips[kind]]
    times=([20]*11+[600]) if kind=='mark' else ([60,50,40,30,30,40,40,40,40,40,40,40])
    seq.append(flat(Image.new('RGBA',(128,128))))
    seq[0].save(HERE/f'{kind}_preview.gif',save_all=True,append_images=seq[1:],duration=times+[350],loop=0,disposal=2,optimize=False)

burst_durations=[.06,.05,.04,.03,.03,.04,.04,.04,.04,.04,.04,.04]
burst_ends=[];total=0
for v in burst_durations:total+=v;burst_ends.append(total)
def burst_at(t):return clips['burst'][min(11,bisect.bisect_right(burst_ends,t))]

effect=[]
sample_events=[.2,.9,1.6]
sample_burst=sample_events[2]+.18
for tick in range(160):
    t=tick*.02
    f=marks_layer(t,sample_events) if t<sample_burst else burst_at(t-sample_burst) if t<sample_burst+total else Image.new('RGBA',(128,128))
    effect.append(flat(f))
effect[0].save(HERE/'stack_then_burst.gif',save_all=True,append_images=effect[1:],duration=20,loop=0,disposal=2,optimize=False)

data=json.loads((UNITY/'Data/integration.json').read_text(encoding='utf-8-sig'))
attack=next(c for c in data['clips'] if c['id']=='attack')
durations=attack['seconds'];ends=[];attack_total=0
for v in durations:attack_total+=v;ends.append(attack_total)
paths=[UNITY/f'Art/Character/attack/{i:02}.png' for i in range(1,13)]
hero=[Image.open(p).convert('RGBA') for p in paths]
idle=Image.open(UNITY/'Art/Character/idle/01.png').convert('RGBA')
foes=[Image.open(UNITY/f'Monsters/Art/boar/move/{i:02}.png').convert('RGBA') for i in range(1,13)]
starts=[.30,1.55]
hits=sorted(s+sum(durations[:i]) for s in starts for i in [4,7])
burst_start=hits[2]+.18
seq=[]
for tick in range(180):
    t=tick*.02
    canvas=Image.new('RGBA',(320,200),'#232b34');d=ImageDraw.Draw(canvas)
    d.rectangle((0,174,320,200),fill='#30383e');d.line((0,174,320,174),fill='#6d787a')
    d.ellipse((88,169,135,176),fill='#161b22');d.ellipse((151,168,260,177),fill='#161b22')
    active=next((t-s for s in starts if 0<=t-s<attack_total),None)
    i=min(11,bisect.bisect_right(ends,active)) if active is not None else None
    body=hero[i] if i is not None else idle
    feet=attack['feet'][i] if i is not None else 121
    recent=max((h for h in hits if h<=t),default=-100)
    recoil=round(3*max(0,1-(t-recent)/.16))
    foe=foes[int(t/.1)%12];ex=139+recoil
    canvas.alpha_composite(foe,(ex,174-foe.getbbox()[3]))
    canvas.alpha_composite(body,(48,174-feet))
    pos=(ex,127-64)
    if t<burst_start:canvas.alpha_composite(marks_layer(t,hits[:3]),pos)
    elif t<burst_start+total:canvas.alpha_composite(burst_at(t-burst_start),pos)
    if t>=hits[3]:canvas.alpha_composite(marks_layer(t,[hits[3]]),pos)
    large=canvas.convert('RGB').resize((960,600),NEAREST);ld=ImageDraw.Draw(large)
    count=sum(t>=h for h in hits)
    label='공격 준비' if count==0 else f'표식 {count}개' if t<burst_start else '세 표식이 모여 폭발' if t<burst_start+total else '다음 표식 1개'
    ld.rounded_rectangle((18,16,375,79),radius=8,fill='#161b22')
    ld.text((30,22),'빨강 v2 · 표식 생성 / 폭발 분리',font=font,fill='#f3ede3')
    ld.text((30,49),label,font=small,fill='#ff7b70')
    seq.append(large)
    if tick in [34,54,99,108,113]:large.save(HERE/f'check_{tick:03}.png')
seq[0].save(HERE/'hero_attack_composite.gif',save_all=True,append_images=seq[1:],duration=20,loop=0,disposal=2,optimize=False)

manifest={'role':'빨강 / 표식 누적·폭발','frameSize':[128,128],'grid':[4,3],
 'clips':{'mark':{'frames':12,'behavior':'생성 애니메이션 후 마지막 프레임 유지; 인스턴스를 3개까지 개별 배치'},'burst':{'frames':12,'seconds':burst_durations,'behavior':'세 표식이 모이고 폭발; 끝나면 제거'}},
 'anchors':anchors,'markSizeInDemo':target,'thirdMarkSettleSeconds':.18,
 'heroHitFramesOneBased':[5,8],'heroFrameSeconds':durations,'demoHitTimes':hits,
 'note':'실제 Unity 스프라이트를 합성한 시안 GIF. 세 번째 표식 생성 후 0.18초에 세 표식이 모이기 시작합니다. 다음 타격의 새 표식은 별도 인스턴스입니다.',
 'sourceHashes':{str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}}
(HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>빨강 잉크 v2</title><style>body{background:#12171d;color:#f3ede3;font:16px/1.8 Malgun Gothic,sans-serif;margin:0}main{max-width:1050px;margin:auto;padding:32px 20px}section{background:#1b222a;padding:24px;border-radius:14px;margin:24px 0}h1{font-size:28px}h2{font-size:21px}p{color:#bbc4cc}img{max-width:100%;display:block;margin:auto;image-rendering:pixelated}a{color:#ffab9a}.role{border-left:3px solid #f44236;padding-left:16px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}@media(max-width:700px){.grid{display:block}}</style><main><h1>빨강 v2 — 표식 세 개가 모여 터짐</h1><p>단순한 물방울 형태 · 표식 생성과 폭발은 별도 12프레임 · 각 프레임 128×128px</p><section><h2>표식 생성 시트</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><p>한 표식이 짧게 나타난 뒤 유지됩니다. 타격마다 동일한 표식을 다른 위치에 추가합니다.</p><a href="../../../../WeaponReference/확정7종_20261019/index.html">투명 표식 시트</a></section><section><h2>세 표식이 모여 터지는 시트</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><p>세 개가 중심으로 모임 → 하나로 압축 → 짧은 파열 → 소멸.</p><a href="../../../../WeaponReference/확정7종_20261019/index.html">투명 폭발 시트</a></section><section><h2>1개 → 2개 → 3개 → 폭발</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><div class="role"><p><b>실제 역할: 빨강 — 표식 누적·폭발</b><br>적중한 적에게 표식을 개별 누적하고, 세 표식이 모이면 소모하며 폭발 피해를 줍니다.</p></div></section><section><h2>실제 주인공 공격 합성</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><div class="role"><p>Unity의 공격 PNG 12장과 기존 공격 타이밍을 사용했습니다. 세 번째 표식도 잠시 보인 뒤 모이므로, 개수가 채워지는 순간을 읽을 수 있습니다. 폭발 중 다음 타격은 별도 새 표식을 만듭니다.</p></div><p>Unity 실행 녹화가 아닌 합성 미리보기입니다.</p></section><section class="grid"><div><h2>표식 단독</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p></div><div><h2>폭발 단독</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p></div></section></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8')
for kind in clips:
    assert len(clips[kind])==12
    assert all(f.size==(128,128) and f.getextrema()[3][0]==0 for f in clips[kind])
g=Image.open(HERE/'hero_attack_composite.gif')
assert g.size==(960,600)
assert sum((g.seek(k) or g.info['duration']) for k in range(g.n_frames))==3600
print(json.dumps({'frameSize':[128,128],'clips':2,'anchors':anchors,'gifFrames':g.n_frames,'attackSeconds':round(attack_total,2)},ensure_ascii=False))
