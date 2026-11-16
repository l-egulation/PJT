"""Transparent presentation GIFs from the confirmed native sprites and VFX."""
from pathlib import Path
from PIL import Image
import base64, io, json, hashlib, zipfile
import numpy as np

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
OUT=ROOT/'05_공유/네무기_대기_공격_GIF_20261016'
OUT.mkdir(parents=True,exist_ok=True)
raw=(ROOT/'04_테스트/128px_플레이테스트/assets.js').read_text(encoding='utf8')
DATA=json.loads(raw.split('=',1)[1].rstrip(';'))['weapons']
N=Image.Resampling.NEAREST
ANCHOR=(144,152)
SCALE=3

def decode(src):return Image.open(io.BytesIO(base64.b64decode(src.split(',',1)[1]))).convert('RGBA')

def timeline(w,kind):
    mods={m['id']:m for m in w['modules']}
    steps=[];events=[];time=0
    if kind=='대기':
        for s in mods['idle']['steps']:
            steps.append((time,time+s['ms'],s));time+=s['ms']
        return steps,events,time
    idle=mods['idle']['steps'][0]
    steps.append((0,180,idle));time=180
    attacks=sorted((m for m in w['modules'] if m['id'].startswith('attack')),key=lambda m:int(m['id'][6:]))
    for i,m in enumerate(attacks):
        hit=i+1;c=w['attacks'][str(hit)]
        events.append({'hit':hit,'kind':c.get('effect','up' if hit==1 else 'down'),'start':time+c['fxStart']})
        limit=c.get('chainAt',m['duration_ms']) if i<len(attacks)-1 else m['duration_ms']
        used=0
        for s in m['steps']:
            if used>=limit:break
            ms=min(s['ms'],limit-used);steps.append((time,time+ms,s));time+=ms;used+=ms
    steps.append((time,time+420,idle));time+=420
    return steps,events,time

def effect_at(ms,t):
    if t<0:return None
    for i,d in enumerate(ms):
        if t<d:return i
        t-=d
    return None

def render(w,images,effects,steps,events,t):
    step=next((s for a,b,s in steps if a<=t<b),steps[-1][2])
    fid=step['frame'];pivot=w.get('framePivots',{}).get(fid,w['pivot']);off=w.get('frameOffsets',{}).get(fid,[0,0])
    image=Image.new('RGBA',(448,192))
    image.alpha_composite(images[fid],(round(ANCHOR[0]-pivot[0]+off[0]+step.get('dx',0)),round(ANCHOR[1]-pivot[1]+off[1]+step.get('dy',0))))
    for e in events:
        idx=effect_at(w.get('effectTiming',{}).get(e['kind'],w['effectMs']),t-e['start'])
        if idx is None:continue
        im=effects[e['kind']][idx];p=w.get('fxPivot',w['pivot'])
        if w.get('alignedFx'):
            cfg=w['alignedFx'][e['hit']-1];off=cfg['offsets'][idx]
            if cfg['flip_x']:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        else:off=w.get('fxOffset',[0,0])
        image.alpha_composite(im,(ANCHOR[0]-p[0]+off[0],ANCHOR[1]-p[1]+off[1]))
    return image

def compress(frames):
    images=[];durations=[];last=None
    for im,ms in frames:
        digest=hashlib.sha256(im.tobytes()).digest()
        if digest==last:durations[-1]+=ms
        else:images.append(im);durations.append(ms);last=digest
    return images,durations

def save_gif(frames,durations,path,crop):
    colors=set()
    arrays=[]
    for im in frames:
        a=np.array(im.crop(crop));arrays.append(a)
        colors.update(tuple(v) for v in np.unique(a[a[:,:,3]>0,:3],axis=0))
    assert len(colors)<=255,('Unexpected palette size',len(colors))
    colors=sorted(colors);palette=[0,0,0]+[c for rgb in colors for c in rgb]
    palette += [0]*(768-len(palette));indexed=[]
    for a in arrays:
        indices=np.zeros(a.shape[:2],dtype=np.uint8)
        for i,color in enumerate(colors,1):indices[(a[:,:,:3]==color).all(2)&(a[:,:,3]>0)]=i
        im=Image.fromarray(indices);im.putpalette(palette);im.info['transparency']=0
        indexed.append(im.resize((im.width*SCALE,im.height*SCALE),N))
    indexed[0].save(path,save_all=True,append_images=indexed[1:],duration=durations,loop=0,transparency=0,background=0,disposal=2,optimize=False)
    with Image.open(path) as check:
        assert check.info.get('transparency')==0
        assert check.info.get('loop')==0
        total=0
        for i in range(check.n_frames):
            check.seek(i);assert check.info['duration']>=20,('Browser-clamped GIF frame',path,i)
            total+=check.info['duration']
            assert check.convert('RGBA').getpixel((0,0))[3]==0
        assert total==sum(durations),(path,total,sum(durations))
    return {'file':path.name,'size':list(indexed[0].size),'frames':len(indexed),'duration_ms':sum(durations),'transparent':True,'effects':bool('공격' in path.name)}

records=[]
for key,w in DATA.items():
    name='기본검' if key=='sword' else w['name']
    images={k:decode(v) for k,v in w['frames'].items()}
    effects={k:[decode(src) for src in values] for k,values in w['effects'].items()}
    clips={}
    boxes=[]
    for kind in ('대기','공격'):
        steps,events,total=timeline(w,kind)
        total=int((total+19)//20)*20
        frames=[(render(w,images,effects,steps,events,t),min(20,total-t)) for t in range(0,total,20)]
        ims,ms=compress(frames);clips[kind]=(ims,ms)
        boxes.extend(im.getbbox() for im in ims if im.getbbox())
    crop=(max(0,min(b[0] for b in boxes)-8),max(0,min(b[1] for b in boxes)-8),min(448,max(b[2] for b in boxes)+8),min(192,max(b[3] for b in boxes)+8))
    for kind,(ims,ms) in clips.items():
        result=save_gif(ims,ms,OUT/f'{name}_{kind}.gif',crop)
        result['weapon']=name;result['crop_native']=list(crop);records.append(result)
        print(result['file'],result['size'],result['duration_ms'],'ms')

(OUT/'내보내기정보.json').write_text(json.dumps({'source':'네 무기 게임용_최종 및 확정 이펙트','scale':SCALE,'timing':'게임 원본 1배속 · 최소 20ms 간격으로 재샘플링해 브라우저의 10ms 지연 보정 회피 · 동일 그림 유지 시간 병합','files':records},ensure_ascii=False,indent=2),encoding='utf8')
rows=''.join('<tr><th>'+name+'</th>'+''.join('<td><img src="'+name+'_'+kind+'.gif?timing=browser-safe-20ms"><p><a href="'+name+'_'+kind+'.gif">'+kind+' GIF</a></p></td>' for kind in ('대기','공격'))+'</tr>' for name in ('기본검','채찍','레이피어','단검'))
(OUT/'미리보기.html').write_text('<!doctype html><html lang="ko"><meta charset="utf-8"><title>네 무기 대기·공격 GIF</title><style>body{background:#202a36;color:#eee;font:16px system-ui;padding:24px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #718096;padding:18px;text-align:center}img{max-width:100%;image-rendering:pixelated;background:repeating-conic-gradient(#46505c 0 25%,#515c68 0 50%) 0/20px 20px}a{color:#efc78c}</style><h1>네 무기 · 투명 배경 대기 / 공격 GIF</h1><p>게임 원래 속도입니다. 모든 GIF 프레임은 20ms 이상으로 저장해 브라우저에서 느려지지 않도록 수정했습니다. 임의 배속 보정 없음. 투명 배경·공격 이펙트 포함.</p><table><tr><th>무기</th><th>대기</th><th>공격 · 전체 연속 타격</th></tr>'+rows+'</table></html>',encoding='utf8')
(OUT/'미리보기_게임속도.html').write_bytes((OUT/'미리보기.html').read_bytes())
manifest=[];timings=[]
for p in sorted(OUT.glob('*.gif')):
    with Image.open(p) as im:
        delays=[]
        for i in range(im.n_frames):im.seek(i);delays.append(im.info['duration'])
    assert min(delays)>=20
    manifest.append({'file':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size,'duration_ms':sum(delays),'minimum_delay_ms':min(delays)})
    timings.append({'file':p.name,'frames':len(delays),'min_delay_ms':min(delays),'duration_ms':sum(delays),'browser_duration_ms':sum(delays)})
(OUT/'최종공유본.json').write_text(json.dumps({'status':'사용자확정','version':'whip-final-7-8-20ms','source_rate':1,'scale':SCALE,'background':'transparent','attack_effects':True,'preview':'미리보기.html','files':manifest},ensure_ascii=False,indent=2),encoding='utf8')
(OUT/'브라우저타이밍_검증.json').write_text(json.dumps({'passed':True,'source_rate':1,'gif_min_delay_ms':20,'files':timings},ensure_ascii=False,indent=2),encoding='utf8')
with zipfile.ZipFile(OUT/'네무기_대기_공격_GIF.zip','w',compression=zipfile.ZIP_DEFLATED) as z:
    for p in sorted(OUT.iterdir()):
        if p.suffix in ('.gif','.html','.txt','.json'):z.write(p,p.name)
print('Verified eight transparent looping GIFs; wrote preview, current hashes and ZIP.')
