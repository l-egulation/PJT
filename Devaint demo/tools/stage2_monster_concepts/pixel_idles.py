"""Package eight local-pixel idles; preserve K4 exactly and animate seven bases."""
from pathlib import Path
from html import escape
import base64,json,shutil,zipfile,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from k4_pixel_idle import shift,isin,move_region

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_pixel_idles.json'
OUT=ROOT/'outputs/waterway/pixel-idles-8-v1-20261031'
N=Image.Resampling.NEAREST
def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def font(n):return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',n)
def rectmask(shape,rects):
    m=np.zeros(shape,bool)
    for x,y,x2,y2 in rects:m[y:y2,x:x2]=True
    return m

def animate(a,m):
    locks=rectmask(a.shape[:2],m['locks']);groups=[];allowed=np.zeros(a.shape[:2],bool)
    for g in m['groups']:
        src=rectmask(a.shape[:2],g['rects'])&isin(a,g['colors'])
        if g.get('fillRows'):
            for y in range(a.shape[0]):
                xs=np.where(src[y])[0]
                if len(xs):src[y,xs.min():xs.max()+1]=True
        src&=~locks;dest=shift(src,-1)&~locks
        if g['receive'] is not None:
            valid=isin(a,g['receive'])
            if g['outside']:valid|=a[:,:,3]==0
            dest&=valid
        assert src.any() and dest.any(),(m['id'],g['name'])
        touch=src|dest;assert not np.any(allowed&touch),(m['id'],'overlapping motion regions')
        groups.append((src,dest));allowed|=touch
    frames=[]
    for active in [[],[0],[0,1],[1]]:
        b=a.copy()
        for i in active:move_region(a,b,*groups[i])
        assert np.array_equal(b[~allowed],a[~allowed]) and np.array_equal(b[locks],a[locks])
        assert {tuple(v) for v in b.reshape(-1,4)}<={tuple(v) for v in a.reshape(-1,4)}
        frames.append(b)
    return frames,groups,allowed,locks

def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));base=ROOT/spec['base'];manifest=[];browser=[];allframes={}
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'references').mkdir(exist_ok=True);shutil.copy2(base/'references/C-game.png',OUT/'references/C-game.png');shutil.copy2(base/'references/assets/approved-player.png',OUT/'references/approved-player.png')
    for m in spec['assets']:
        ident=m['id'];p=OUT/ident
        for folder in ['references','frames','enlarged','previews','masks']:(p/folder).mkdir(parents=True,exist_ok=True)
        source=base/ident/'native/base-frame.png';meta=json.loads((base/ident/'metadata.json').read_text(encoding='utf-8'))
        assert sha(source)==meta['frameSha256']
        shutil.copy2(source,p/'references/base-frame.png');shutil.copy2(base/ident/'palette.json',p/'references/palette.json')
        original=Image.open(source).convert('RGBA');box=original.getbbox();cropped=original.crop(box);a=np.array(cropped)
        if 'reuse' in m:
            prior=ROOT/m['reuse'];frames=[Image.open(prior/'frames'/f'idle-{i:02}.png').convert('RGBA') for i in range(4)]
            cropframes=[f.crop(box) for f in frames]
            allowed=np.array(Image.open(prior/'masks/allowed-change.png'))>0
            locks=~allowed;groups=[]
            for name in ['face','shoulders']:
                src=np.array(Image.open(prior/'masks'/f'{name}.png'))>0;groups.append((src,shift(src,-1)))
            for i in range(4):shutil.copy2(prior/'frames'/f'idle-{i:02}.png',p/'frames'/f'idle-{i:02}.png')
            shutil.copy2(prior/'motion-spec.json',p/'references/reused-k4-spec.json')
        else:
            arrays,groups,allowed,locks=animate(a,m);cropframes=[Image.fromarray(b) for b in arrays];frames=[]
            for i,c in enumerate(cropframes):
                f=original.copy();f.paste(c,box[:2]);f.save(p/'frames'/f'idle-{i:02}.png');frames.append(f)
        assert frames[0].tobytes()==original.tobytes()
        assert len({f.tobytes() for f in frames})==4,ident
        changed=[]
        for i,(f,c) in enumerate(zip(frames,cropframes)):
            b=np.array(c);assert np.array_equal(b[~allowed],a[~allowed]);assert np.array_equal(b[locks],a[locks]);assert np.array_equal(b[-5:],a[-5:])
            changed.append(int(np.any(b!=a,axis=2).sum()))
            c.save(p/'frames'/f'idle-{i:02}-cropped.png')
            for factor in [4,8]:f.resize((384*factor,224*factor),N).save(p/'enlarged'/f'idle-{i:02}-{factor}x.png')
        Image.fromarray(allowed.astype(np.uint8)*255).save(p/'masks/allowed-change.png');Image.fromarray(locks.astype(np.uint8)*255).save(p/'masks/locked.png')
        maskview=Image.new('RGBA',cropped.size,'#26313b');maskview.alpha_composite(cropped);overlay=np.zeros_like(a)
        for i,(src,dest) in enumerate(groups):
            Image.fromarray(src.astype(np.uint8)*255).save(p/'masks'/f'group-{i}.png');overlay[src]=([240,175,90,180],[95,205,235,180])[i]
        maskview.alpha_composite(Image.fromarray(overlay));maskview.resize((a.shape[1]*4,a.shape[0]*4),N).save(p/'previews/moving-regions.png')
        ds=m['durations']
        for ext in ['webp','apng']:
            kwargs={'lossless':True,'exact':True} if ext=='webp' else {'disposal':0,'blend':0}
            frames[0].save(p/'previews'/f'idle-native.{ext}',save_all=True,append_images=frames[1:],duration=ds,loop=0,**kwargs)
            decoded=Image.open(p/'previews'/f'idle-native.{ext}');assert decoded.n_frames==4
            for i,f in enumerate(frames):decoded.seek(i);assert decoded.convert('RGBA').tobytes()==f.tobytes()
        expanded=[];gifs=[]
        for c in cropframes:
            pad=Image.new('RGBA',(cropped.width+24,cropped.height+24));pad.alpha_composite(c,(12,12));pad=pad.resize((pad.width*4,pad.height*4),N);expanded.append(pad)
            bg=Image.new('RGBA',pad.size,'#26313b');bg.alpha_composite(pad);gifs.append(bg.convert('RGB'))
        expanded[0].save(p/'previews/idle-4x.webp',save_all=True,append_images=expanded[1:],duration=ds,loop=0,lossless=True,exact=True)
        gifs[0].save(p/'previews/idle-4x.gif',save_all=True,append_images=gifs[1:],duration=ds,loop=0,optimize=False,disposal=2)
        strip=Image.new('RGBA',(384*4,224));contact=Image.new('RGBA',((cropped.width+16)*4*2,(cropped.height+16)*2+40),'#26313b');draw=ImageDraw.Draw(contact)
        for i,(f,c) in enumerate(zip(frames,cropframes)):
            strip.alpha_composite(f,(384*i,0));contact.alpha_composite(c.resize((c.width*2,c.height*2),N),(i*(c.width+16)*2+16,56));draw.text((i*(c.width+16)*2+10,6),str(i),font=font(20),fill='#e4c390')
        strip.save(p/'frames/idle-strip.png');contact.save(p/'previews/key-poses.png')
        scene=Image.open(OUT/'references/C-game.png').convert('RGBA');maps=[]
        for c in cropframes:
            bg=scene.copy();bg.alpha_composite(c,(430-c.width//2,286-c.height));maps.append(bg)
        maps[0].save(p/'previews/map-idle.webp',save_all=True,append_images=maps[1:],duration=ds,loop=0,lossless=True,exact=True)
        row={**m,'sourceHash':sha(source),'visibleSize':list(cropped.size),'artOrigin':list(box[:2]),'changedPixels':changed,'fixedRgbaIdentical':True,'groundPixelsIdentical':True,'originalFrameZero':True,'newColors':0,'maxNativeOffset':1,'cycleMs':sum(ds),'uniqueFrames':4,'reusedK4Exact':ident=='K4'}
        dump(p/'metadata.json',row);manifest.append(row);allframes[ident]=frames
        browser.append({'id':ident,'box':[box[0]-12,box[1]-12,cropped.width+24,cropped.height+24],'durations':ds,'images':[base64.b64encode((p/'frames'/f'idle-{i:02}.png').read_bytes()).decode() for i in range(4)]})
    # Same-size still roster; per-character playback uses each character's actual timing.
    roster=Image.new('RGBA',(960,540),'#26313b');d=ImageDraw.Draw(roster);player=Image.open(OUT/'references/approved-player.png').convert('RGBA');player=player.crop(player.getbbox())
    d.text((20,12),'8명 국소 픽셀 대기 / 기본 키·동일1배율',font=font(20),fill='#eee2cf')
    for ids,foot in [(['K4','B3','M3','H3','R-selected'],255),(['A3','I4-rose','I4-blue'],495)]:
        x=20;roster.alpha_composite(player,(x,foot-player.height));x+=90
        for ident in ids:
            c=allframes[ident][0].crop(allframes[ident][0].getbbox());roster.alpha_composite(c,(x,foot-c.height));d.text((x,foot+8),ident,font=font(13),fill='#e4bd87');x+=c.width+18
    roster.save(OUT/'native-roster.png')
    dump(OUT/'manifest.json',manifest);shutil.copy2(SPEC,OUT/'motion-spec.json')
    combat=json.loads((ROOT/'data/combat/stage2_waterway_patterns.json').read_text(encoding='utf-8'));dump(OUT/'combat-patterns.json',combat)
    page(manifest,browser,combat)
    (OUT/'README.md').write_text('''# 8명 국소 픽셀 대기 시안

기존 픽셀 기본본의 선택 영역만1px 위로 복사 이동한다. 원본 프레임·팔레트·발/무기 고정,새 색과 재생성 없음. 등짐 분사수의 네 PNG는 검토한 개별 시안에서 그대로 복사했다. 다른7명은 개체별 마스크와 시간으로 제작했다. 보행기는 강체와 접점을 고정한 내부 명암 변화,필경사는 침 고정/등판,수의는 손끝/주름,나머지 사람형은 얼굴/어깨다.

`index.html`:8명 독립 재생,정지·키 선택·확대·원본 비교와 공격 패턴 요약.
개체별 `frames/`:384×224 PNG4장·스트립. `enlarged/`:4배·8배 정수 확대 PNG.
`previews/`:투명 APNG/WebP·4배 GIF·키/마스크·기존 맵 합성. `references/`:무변형 기본본·팔레트.
`motion-spec.json`:영역·시간 편집 원본. `manifest.json`:실제 변경 픽셀 수·규격·검사.

디자인 모션 시안이며 이동·공격·Unity 구현은 아니다. 맵 이미지는 합성 미리보기이며 실제 게임 캡처가 아니다. 일반/엘리트 공격 핵심은 사용자 지정,전조·시간·보스 세부는 기획 제안이며 검증되지 않았다.
''',encoding='utf-8')
    sources=OUT/'source';sources.mkdir(exist_ok=True)
    for filename in ['pixel_idles.py','k4_pixel_idle.py']:shutil.copy2(Path(__file__).parent/filename,sources/filename)
    shutil.copy2(SPEC,sources/SPEC.name)
    with zipfile.ZipFile(OUT/'pixel-idles-8-v1-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and not p.name.endswith('-coordinates.png'):z.write(p,str(p.relative_to(OUT)))
    print(json.dumps([{'id':m['id'],'changed':m['changedPixels'],'cycleMs':m['cycleMs']} for m in manifest]))

def page(rows,browser,combat):
    cards=[]
    for m in rows:
        i=m['id'];cards.append(f'''<section id="{i}"><h2>{escape(m['name'])} <small>{i} · {m['role']} · {m['cycleMs']/1000:g}초</small></h2><p>{escape(m['description'])}</p><div class="scroll"><canvas data-id="{i}"></canvas></div><p><input data-step="{i}" type="range" min="0" max="3" value="0"> <span data-label="{i}">0</span></p><p><a href="{i}/previews/idle-4x.gif">4배 GIF</a> · <a href="{i}/previews/idle-native.apng">투명 APNG</a> · <a href="{i}/frames/idle-strip.png">PNG 프레임</a></p><details><summary>원본·변경 영역·맵에서 보기</summary><img src="{i}/references/base-frame.png" width="384" height="224"><img class="mask" src="{i}/previews/moving-regions.png"><div class="scroll"><img src="{i}/previews/map-idle.webp" width="960" height="540"></div></details></section>''')
    patternrows=[]
    for m in combat['monsters']:
        patterns=' / '.join(p['name'] for p in m['patterns']);tells=' / '.join(p['phases'][0]['action'] for p in m['patterns'])
        patternrows.append(f'<tr><td>{escape(m["name"])}</td><td>{escape(m["role"])}</td><td>{escape(patterns)}</td><td>{escape(tells)}</td></tr>')
    for p in combat['boss']['patterns']:patternrows.append(f'<tr><td>{escape(p["name"])}</td><td>보스·제안</td><td>{escape(p["correction"])}</td><td>{escape(p["phases"][0]["action"])}</td></tr>')
    js='''const data=DATA;let playing=true,zoom=3,showBase=false;const states=[];
function draw(s){const c=document.querySelector('[data-id="'+s.id+'"]'),ctx=c.getContext('2d');c.width=s.box[2];c.height=s.box[3];c.style.width=c.width*zoom+'px';c.style.height=c.height*zoom+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(s.img[showBase?0:s.key],...s.box,0,0,c.width,c.height);document.querySelector('[data-label="'+s.id+'"]').textContent=s.key;document.querySelector('[data-step="'+s.id+'"]').value=s.key}
Promise.all(data.map(async d=>{d.img=await Promise.all(d.images.map(v=>new Promise(r=>{const im=new Image();im.onload=()=>r(im);im.src='data:image/png;base64,'+v})));d.key=0;d.last=performance.now();states.push(d);draw(d)})).then(()=>requestAnimationFrame(tick));
function tick(t){if(playing)for(const s of states)if(t-s.last>=s.durations[s.key]){s.key=(s.key+1)%4;s.last=t;draw(s)}requestAnimationFrame(tick)}
document.querySelector('#play').onclick=()=>{playing=!playing;states.forEach(s=>s.last=performance.now());document.querySelector('#play').textContent=playing?'전체 정지':'전체 재생'};
document.querySelector('#zoom').onchange=e=>{zoom=+e.target.value;states.forEach(draw)};
document.querySelector('#base').onchange=e=>{showBase=e.target.checked;states.forEach(draw)};
document.querySelectorAll('[data-step]').forEach(el=>el.oninput=()=>{playing=false;document.querySelector('#play').textContent='전체 재생';const s=states.find(v=>v.id===el.dataset.step);s.key=+el.value;draw(s)});'''.replace('DATA',json.dumps(browser))
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>지하수로8명 · 국소 픽셀 대기와 공격 패턴</title><style>body{margin:0;background:#17212a;color:#e8e3d8;font:16px/1.7 system-ui}main{max-width:1280px;margin:auto;padding:30px}a{color:#e5bc82}h1{font-size:30px}h2{font-size:23px}small{font-size:14px;color:#b4c8ce}section{padding:22px;background:#26313b;border:1px solid #475560;border-radius:10px;margin-bottom:22px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:22px}.scroll{overflow:auto}canvas,img{image-rendering:pixelated}button,select{padding:8px 14px;margin-right:14px;background:#e5bd87;border:0;border-radius:5px}button{cursor:pointer}.controls{position:sticky;top:0;z-index:1;padding:12px;background:#17212af5}.mask{max-width:100%;max-height:430px;object-fit:contain}table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #50616b;padding:12px;text-align:left;vertical-align:top}td:first-child{min-width:120px}nav{display:flex;gap:14px;flex-wrap:wrap}@media(max-width:800px){.grid{display:block}}</style><main><h1>8명 · 기존 픽셀로 만든 대기</h1><p>분사수의 검토한 움직임을 유지하고 나머지7명도 선택 부위만 최대1px 움직였습니다. 발·무기 고정, 새 이미지 생성·새 색 없음. 아래는 모션 검토 시안입니다.</p><p><a download href="pixel-idles-8-v1-20261031.zip">8명 전체 PNG·모션 ZIP</a> · <a href="#patterns">현재 공격 패턴</a> · <a href="../user-sized-redraw-20261031/combat-plan.html">상세 전조·회복 기획</a></p><nav>'''+''.join(f'<a href="#{m["id"]}">{escape(m["name"])}</a>' for m in rows)+'''</nav><div class="controls"><button id="play">전체 정지</button><select id="zoom"><option value="1">실제1배</option><option value="3" selected>3배</option><option value="4">4배</option><option value="8">8배</option></select><label><input id="base" type="checkbox">기본본으로 비교</label></div><div class="grid">'''+''.join(cards)+'''</div><h2 id="patterns">현재 공격 패턴</h2><p>일반3근거리·2원거리·엘리트의 핵심 공격은 사용자 지정. 보스는 교대 방향을 채택했으며 아래 구체 패턴·전조 시간·공유 체력은 제안 단계입니다. 실제 공격 모션과 전투 구현은 아직 없습니다.</p><div class="scroll"><table><thead><tr><th>몬스터</th><th>역할</th><th>공격</th><th>첫 전조</th></tr></thead><tbody>'''+''.join(patternrows)+'''</tbody></table></div><p>남매는 자주색의 석궁 밀쳐치기와 푸른색의 단발 잉크술을 교대하는 안입니다. 원거리 탄이 남아 있을 때 근접 공격을 겹치지 않게 합니다. 최종 체력·피해·속도·사거리·탄속·세부 밸런스는 미정입니다.</p><p>맵 이미지는 기존960×540 배경에 올린 합성 미리보기이며 실제 게임 캡처가 아닙니다.</p></main><script>'''+js+'</script></html>'
    (OUT/'index.html').write_text(html,encoding='utf-8');(OUT/'preview.js').write_text(js,encoding='utf-8')

if __name__=='__main__':build()
