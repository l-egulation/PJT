"""Add planted full-body breathing using integer row edits of existing pixels."""
from pathlib import Path
import base64,json,shutil,zipfile,hashlib
from html import escape
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/waterway/body-idles-8-v2-20261031'
SPEC=ROOT/'data/presentation/stage2_body_idles.json'
N=Image.Resampling.NEAREST
def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def font(n):return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',n)

def deform(im,m,dy,origin):
    a=np.array(im);ys=np.arange(a.shape[0]);top=origin[1]
    if m.get('floating'):displacement=np.full(len(ys),dy,dtype=int)
    else:
        start=top+m['cutoff'];end=top+m['pinFrom']
        weight=np.clip((end-ys)/(end-start),0,1)
        displacement=np.rint(dy*weight).astype(int)
    source_y=ys-displacement;valid=(source_y>=0)&(source_y<len(ys))
    out=np.zeros_like(a);out[valid]=a[source_y[valid]]
    if not m.get('floating'):assert np.array_equal(out[top+m['pinFrom']:],a[top+m['pinFrom']:])
    return Image.fromarray(out),displacement.tolist()

def encode(frames,ds,path):
    if path.suffix=='.webp':kw={'lossless':True,'exact':True}
    else:kw={'disposal':0,'blend':0}
    frames[0].save(path,save_all=True,append_images=frames[1:],duration=ds,loop=0,**kw)
    decoded=Image.open(path);assert decoded.n_frames==len(frames),(path,decoded.n_frames)
    for i,f in enumerate(frames):decoded.seek(i);assert decoded.convert('RGBA').tobytes()==f.tobytes()

def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));prior=ROOT/spec['prior'];base=ROOT/spec['base'];rows=[];browser=[]
    for m in spec['assets']:
        ident=m['id'];p=OUT/ident
        for name in ['references','frames','enlarged','previews']:(p/name).mkdir(parents=True,exist_ok=True)
        shutil.copy2(base/ident/'native/base-frame.png',p/'references/base-frame.png')
        shutil.copy2(prior/ident/'metadata.json',p/'references/previous-metadata.json')
        shutil.copy2(prior/ident/'previews/idle-4x.gif',p/'references/previous-idle-4x.gif')
        original=Image.open(p/'references/base-frame.png').convert('RGBA');bbox=original.getbbox();origin=bbox[:2]
        old=[Image.open(prior/ident/'frames'/f'idle-{i:02}.png').convert('RGBA') for i in range(4)]
        for i in range(4):shutil.copy2(prior/ident/'frames'/f'idle-{i:02}.png',p/'references'/f'previous-{i:02}.png')
        keys=m.get('sourceKeys',spec['sourceKeys']);frames=[];mapping=[];changed=[]
        for i,(key,dy) in enumerate(zip(keys,m['offsets'])):
            f,displacements=deform(old[key],m,dy,origin);a=np.array(f);b=np.array(original)
            assert {tuple(c) for c in a.reshape(-1,4)}<={tuple(c) for c in b.reshape(-1,4)}
            assert set(np.unique(a[:,:,3]))=={0,255}
            if not m.get('floating'):assert np.array_equal(a[origin[1]+m['pinFrom']:],b[origin[1]+m['pinFrom']:])
            for x,y,x2,y2 in m['rigidRects']:
                # Preserve the selected v1 pose exactly in the rigid upper region.
                # Broad rectangles include its intentional face/shoulder animation.
                source=np.array(old[key])
                assert np.array_equal(a[origin[1]+y+dy:origin[1]+y2+dy,origin[0]+x:origin[0]+x2],source[origin[1]+y:origin[1]+y2,origin[0]+x:origin[0]+x2]),(ident,i,'rigid equipment')
            f.save(p/'frames'/f'idle-{i:02}.png');frames.append(f);mapping.append(displacements)
            for s in [4,8]:f.resize((384*s,224*s),N).save(p/'enlarged'/f'idle-{i:02}-{s}x.png')
            changed.append(int(np.any(a!=b,axis=2).sum()))
        assert frames[0].tobytes()==original.tobytes()
        ds=m['durations'];encode(frames,ds,p/'previews/idle-native.webp');encode(frames,ds,p/'previews/idle-native.apng')
        reviewbox=(bbox[0]-12,bbox[1]-12,bbox[2]+12,bbox[3]+12)
        gifs=[];webps=[]
        for f in frames:
            im=f.crop(reviewbox);im=im.resize((im.width*4,im.height*4),N);webps.append(im)
            bg=Image.new('RGBA',im.size,'#26313b');bg.alpha_composite(im);gifs.append(bg.convert('RGB'))
        gifs[0].save(p/'previews/idle-4x.gif',save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2,optimize=False)
        encode(webps,ds,p/'previews/idle-4x.webp')
        cw=reviewbox[2]-reviewbox[0];ch=reviewbox[3]-reviewbox[1]
        contact=Image.new('RGBA',(cw*4*2,(ch+24)*2*2),'#26313b');d=ImageDraw.Draw(contact)
        for i,f in enumerate(frames):
            x=i%4*cw*2;y=i//4*(ch+24)*2;d.text((x+8,y+5),f'{i} / {m["offsets"][i]:+}px',font=font(18),fill='#e8c48d');contact.alpha_composite(f.crop(reviewbox).resize((cw*2,ch*2),N),(x,y+42))
        contact.save(p/'previews/key-poses.png')
        strip=Image.new('RGBA',(384*8,224))
        for i,f in enumerate(frames):strip.alpha_composite(f,(384*i,0))
        strip.save(p/'frames/idle-strip.png')
        scene=Image.open(base/'references/C-game.png').convert('RGBA');maps=[]
        for f in frames:
            bg=scene.copy();bg.alpha_composite(f,(430-(bbox[0]+bbox[2])//2,286-192));maps.append(bg)
        encode(maps,ds,p/'previews/map-idle.webp')
        row={**m,'baseHash':sha(base/ident/'native/base-frame.png'),'sourceKeys':keys,'artOrigin':list(origin),'changedPixels':changed,'newColors':0,'fixedFootBand':not m.get('floating',False),'rigidEquipmentChecks':len(m['rigidRects']),'cycleMs':sum(ds),'firstFrameEqualsBase':True,'frameCanvas':[384,224]}
        rows.append(row);dump(p/'metadata.json',row);dump(p/'source-row-displacements.json',mapping)
        oldmeta=json.loads((prior/ident/'metadata.json').read_text(encoding='utf-8'))
        browser.append({'id':ident,'box':[reviewbox[0],reviewbox[1],cw,ch],'oldDurations':oldmeta['durations'],'durations':ds,'old':[base64.b64encode((p/'references'/f'previous-{i:02}.png').read_bytes()).decode() for i in range(4)],'frames':[base64.b64encode((p/'frames'/f'idle-{i:02}.png').read_bytes()).decode() for i in range(8)]})
    dump(OUT/'manifest.json',rows);shutil.copy2(SPEC,OUT/'motion-spec.json');page(rows,browser)
    (OUT/'source').mkdir(exist_ok=True);shutil.copy2(Path(__file__),OUT/'source/body_idles.py');shutil.copy2(SPEC,OUT/'source'/SPEC.name)
    (OUT/'README.md').write_text('''# 8명 몸통 호흡 대기 v2

얼굴/어깨만 움직이던 v1에 몸통 전체의 무게 이동을 추가한 시안이다. 기존 픽셀의 정수 이동과 다리·지지대 영역의 행 샘플링으로 제작하며 새 색·이미지 생성·보간은 없다. 상체와 장비는 같이 움직이고 장비의 형태와 길이는 보존한다. 발/지지점 하단은 고정한다. 수의는 발 없는 캐릭터이므로 전체가3px 부유한다.

`index.html`에서 이전/보정 대기를 나란히 재생하거나 정지/키 선택/확대할 수 있다. 각 개체 `frames/`에384×224 PNG8장·스트립,`enlarged/`에4배·8배,`previews/`에 투명 APNG/WebP·4배 GIF·맵 합성,`references/`에 기존 기본본과 v1 프레임이 있다. 데이터의 cutoff/pinFrom은 크롭 기준이며 source-row-displacements.json은 출력 행이 원본 어느 행을 참조하는지 복원할 수 있는 변위다.

원래 기본본·v1 결과는 변경하지 않았다. 맵 결과는 합성 미리보기이며 실제 게임 캡처가 아니다. 대기 아트 시안이며 공격 패턴과 전투 수치·이동·Unity는 변경하지 않았다.
''',encoding='utf-8')
    with zipfile.ZipFile(OUT/'body-idles-8-v2-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p.suffix!='.zip':z.write(p,str(p.relative_to(OUT)))
    print(json.dumps([{'id':m['id'],'changes':m['changedPixels'],'cycleMs':m['cycleMs']} for m in rows]))

def page(rows,data):
    cards=[]
    for m in rows:
        i=m['id'];cards.append(f'<section id="{i}"><h2>{escape(m["name"])} <small>{m["cycleMs"]/1000:g}초</small></h2><p>{escape(m["description"])}</p><div class="pair"><div><b>이전 · 국소 움직임</b><div class="scroll"><canvas data-old="{i}"></canvas></div></div><div><b>보정 · 몸통 호흡</b><div class="scroll"><canvas data-new="{i}"></canvas></div></div></div><input data-step="{i}" type="range" min="0" max="7" value="0"><span data-label="{i}">0</span><p><a href="{i}/previews/idle-4x.gif">보정 GIF</a> · <a href="{i}/previews/idle-native.apng">투명 APNG</a> · <a href="{i}/frames/idle-strip.png">PNG 프레임</a></p><details><summary>키·맵 비교</summary><img class="board" src="{i}/previews/key-poses.png"><div class="scroll"><img src="{i}/previews/map-idle.webp" width="960" height="540"></div></details></section>')
    js='''const data=DATA;const states=[];let playing=true,scale=3;
function draw(s){for(const [tag,images,key] of [['old',s.oldImages,s.oldKey],['new',s.images,s.key]]){const c=document.querySelector('[data-'+tag+'="'+s.id+'"]');c.width=s.box[2];c.height=s.box[3];c.style.width=c.width*scale+'px';c.style.height=c.height*scale+'px';const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(images[key],...s.box,0,0,c.width,c.height)}document.querySelector('[data-label="'+s.id+'"]').textContent=s.key;document.querySelector('[data-step="'+s.id+'"]').value=s.key}
function load(arr){return Promise.all(arr.map(src=>new Promise(r=>{const im=new Image();im.onload=()=>r(im);im.src='data:image/png;base64,'+src})))}
Promise.all(data.map(async d=>{d.images=await load(d.frames);d.oldImages=await load(d.old);d.key=0;d.oldKey=0;d.last=d.oldLast=performance.now();states.push(d);draw(d)})).then(()=>requestAnimationFrame(tick));
function tick(t){if(playing)for(const s of states){let dirty=false;if(t-s.last>=s.durations[s.key]){s.key=(s.key+1)%8;s.last=t;dirty=true}if(t-s.oldLast>=s.oldDurations[s.oldKey]){s.oldKey=(s.oldKey+1)%4;s.oldLast=t;dirty=true}if(dirty)draw(s)}requestAnimationFrame(tick)}
document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'전체 정지':'전체 재생';states.forEach(s=>{s.last=s.oldLast=performance.now()})};document.querySelector('#zoom').onchange=e=>{scale=+e.target.value;states.forEach(draw)};
document.querySelectorAll('[data-step]').forEach(el=>el.oninput=()=>{playing=false;document.querySelector('#play').textContent='전체 재생';const s=states.find(s=>s.id===el.dataset.step);s.key=+el.value;s.oldKey=0;draw(s)});'''.replace('DATA',json.dumps(data))
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>몸통까지 들썩이는 대기 · v2</title><style>body{margin:0;background:#17212a;color:#eee5d6;font:16px/1.7 system-ui}main{max-width:1180px;margin:auto;padding:28px}h1{font-size:30px}small{font-size:15px;color:#adc4cd}a{color:#e7be86}section{background:#26313b;padding:24px;margin:24px 0;border-radius:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}.pair>div{min-width:0}.scroll{overflow:auto}canvas,img{image-rendering:pixelated}.board{max-width:100%}.controls{position:sticky;top:0;padding:12px;background:#17212af5;z-index:1}button,select{padding:10px 18px;margin-right:12px;background:#e6c18e;border:0;border-radius:6px}button{cursor:pointer}nav{display:flex;gap:14px;flex-wrap:wrap}@media(max-width:650px){.pair{grid-template-columns:1fr}}</style><main><h1>몸통 호흡을 더한 대기8명</h1><p>상체·골반·장비가 함께 오르내리고 무릎·지지대가 움직임을 받아 줍니다. 발은 고정하며 발 없는 수의만 전체가 부유합니다. 기존 픽셀·팔레트를 사용했고 이전 시안도 보존했습니다.</p><p><a download href="body-idles-8-v2-20261031.zip">전체 PNG·모션 ZIP</a> · <a href="../pixel-idles-8-v1-20261031/index.html#patterns">현재 공격 패턴 · 변경 없음</a></p><nav>'''+''.join(f'<a href="#{m["id"]}">{m["name"]}</a>' for m in rows)+'''</nav><div class="controls"><button id="play">전체 정지</button><select id="zoom"><option value="1">실제1배</option><option value="3" selected>3배</option><option value="4">4배</option><option value="8">8배</option></select></div>'''+''.join(cards)+'''<p>대기 모션 검토용이며 실제 게임 적용은 아닙니다. 맵은 기존 배경 위 합성 미리보기입니다.</p></main><script>'''+js+'</script></html>'
    (OUT/'index.html').write_text(html,encoding='utf-8');(OUT/'preview.js').write_text(js,encoding='utf-8')

if __name__=='__main__':build()
