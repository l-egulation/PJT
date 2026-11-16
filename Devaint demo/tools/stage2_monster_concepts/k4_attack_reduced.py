"""Compare fewer K4 attack keys while preserving phase times and the spray hold."""
import json
import shutil
import zipfile
from html import escape

from PIL import Image, ImageDraw, ImageFont

from k4_d_pixel_walk import ROOT, N, sha
from b3_walk_return import dump, white
from k4_attack_return import durations
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_k4_attack_reduced.json'


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));source=ROOT/spec['source'];out=ROOT/spec['output']
    parent=json.loads((source/'manifest.json').read_text(encoding='utf-8'));crop=parent['previewCrop']
    assert spec['phaseDurationsMs']==[550,650,700] and spec['previewGapMs']==500
    assert [p['durationMs'] for p in parent['phases']]==spec['phaseDurationsMs']
    for sub in ('references','previews'):(out/sub).mkdir(parents=True,exist_ok=True)
    for src,name in [(source/'manifest.json','source-manifest.json'),(source/'references/base-frame.png','base-frame.png'),(source/'references/palette.json','palette.json'),(source/'references/C-game.png','C-game.png')]:shutil.copy2(src,out/'references'/name)
    rows=[]
    for candidate in spec['candidates']:
        keys=candidate['windupKeys']+[candidate['holdKey']]+candidate['recoveryKeys']
        assert candidate['holdKey']==parent['holdKeyOneBased']==12
        assert keys==sorted(set(keys)) and keys[0]==1 and keys[-1]==24
        assert all(parent['mapping'][k-1]['phase']=='windup' for k in candidate['windupKeys'])
        assert all(parent['mapping'][k-1]['phase']=='recovery' for k in candidate['recoveryKeys'])
        ds=durations(len(candidate['windupKeys']),550)+[650]+durations(len(candidate['recoveryKeys']),700)
        folder=out/candidate['id'];frames=[];mapping=[]
        for sub in ('native','native-indexed','white-native','enlarged-4x','previews'):(folder/sub).mkdir(parents=True,exist_ok=True)
        for i,(key,duration) in enumerate(zip(keys,ds),1):
            name=f'attack-{i:02}.png';old=f'attack-{key:02}.png'
            for sub in ('native','native-indexed','white-native','enlarged-4x'):
                shutil.copy2(source/sub/old,folder/sub/name);assert sha(source/sub/old)==sha(folder/sub/name)
            frame=Image.open(folder/'native'/name).convert('RGBA');frames.append(frame)
            mapping.append({**parent['mapping'][key-1],'key':i,'priorKeyOneBased':key,'durationMs':duration,'png':f'{candidate["id"]}/native/{name}','sha256':sha(folder/'native'/name)})
        hold_index=len(candidate['windupKeys'])
        assert sum(ds)==1900 and sum(ds[:hold_index])==550 and ds[hold_index]==650
        assert frames[hold_index].tobytes()==Image.open(source/'native/attack-12.png').convert('RGBA').tobytes()
        crops=[white(f.crop(crop).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N)).convert('RGBA') for f in frames]
        animate(frames,ds,folder/'previews/attack-native.webp');animate(frames,ds,folder/'previews/attack-native.apng')
        animate(crops,ds,folder/'previews/attack-white-4x.webp')
        animate(crops+[crops[-1]],ds+[spec['previewGapMs']],folder/'previews/review-white-4x.webp')
        for filename,expected in [('attack-native.webp',frames),('attack-native.apng',frames),('attack-white-4x.webp',crops)]:
            im=Image.open(folder/'previews'/filename);assert im.n_frames==len(ds)
            for i,(f,t) in enumerate(zip(expected,ds)):
                im.seek(i);im.load();assert im.convert('RGBA').tobytes()==f.tobytes() and round(im.info['duration'])==t
        row={**candidate,'frameCount':len(keys),'priorKeysOneBased':keys,'durationsMs':ds,'cycleMs':1900,'holdIndexZeroBased':hold_index,'mapping':mapping,'approval':('approved-by-user' if candidate['id']==spec.get('selectedCandidate') else 'not-selected-by-user' if spec.get('selectedCandidate') else 'pending-user-choice')}
        rows.append(row);dump(folder/'manifest.json',row)
    info={**spec,'candidates':rows,'paletteRgb':parent['paletteRgb'],'canvas':parent['canvas'],'previewCrop':crop,'effectAnchorProposal':parent['effectAnchorProposal'],'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);shutil.copy2(SPEC,out/'presentation-spec.json');page(out,info)
    # Exact integer20ms samples show each candidate at the same elapsed attack time.
    cw,ch=(crop[2]-crop[0])*2,(crop[3]-crop[1])*2
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    all_frames=[[Image.open(out/r['id']/'native'/f'attack-{i+1:02}.png').convert('RGBA') for i in range(r['frameCount'])] for r in rows]
    def composite(time):
        result=Image.new('RGBA',(cw*3,(ch+100)),'white');draw=ImageDraw.Draw(result)
        for pos,(r,frames) in enumerate(zip(rows,all_frames)):
            t=min(time,1899);k=0
            while k<len(frames)-1 and t>=r['durationsMs'][k]:t-=r['durationsMs'][k];k+=1
            x=pos*cw;result.alpha_composite(frames[k].crop(crop).resize((cw,ch),N),(x,90))
            draw.text((x+12,8),f'{r["id"]} · {r["frameCount"]}장'+(' · 채택' if r['id']==spec.get('selectedCandidate') else ' · 추천' if r['recommended'] and not spec.get('selectedCandidate') else ''),font=font,fill='#25252d')
            phase='확인 간격' if time>=1900 else '전조' if time<550 else '분사 고정' if time<1200 else '회복'
            draw.text((x+12,36),phase,font=font,fill='#874e4d')
            draw.text((x+12,62),r['name'],font=font,fill='#596171')
        return result
    clips=[composite(t) for t in range(0,2400,20)]
    animate(clips,[20]*len(clips),out/'previews/ABC-overview.webp');composite(550).save(out/'previews/ABC-hold.png')
    (out/'README.md').write_text('''# 등짐분사수 공격 · 프레임 축소 후보

A19장(9+1+9), B15장(7+1+7), C11장(5+1+5)을 비교한다. 셋 모두 전조550ms·같은 단일 분사 고정650ms·회복700ms, 공격1900ms다. 프레임을 삭제한 자리는 같은 단계의 선택 자세를 더 길게 유지한다. 새 동작이나 픽셀을 합성하지 않고 이전 native/indexed/흰 배경/4배 파일을 바이트 그대로 복사한다. 첫 자세와 마지막 자세도 같다. B는 비교 추천이며 사용자 선택은 아직 없다.

index.html은 동기화된3종과 이전24장 기준을 같은 시간축으로 재생하며1/2/4/8배·배경·일시정지·단계 이동·시간 탐색·원본 맵 개체 선택을 제공한다. 확인용 반복 간격500ms는 공격 시간에서 제외된다. 개별 native WebP/APNG는 정확한 장별 시간표를 보존한다. ABC-overview.webp는20ms 샘플의2.4초 비교 클립으로20ms 미만 키 경계는 표시에 양자화된다. 실제 게임 캡처가 아닌 아트/합성 미리보기이며 잉크 효과와 전투/Unity는 변경하지 않는다. 원본 생성 시트·셀·참조·입력 프롬프트는 references/source-manifest.json이 가리키는 기존 출력에 그대로 보존된다.
''',encoding='utf-8')
    if spec.get('selectedCandidate'):
        p=out/'README.md'
        p.write_text(p.read_text(encoding='utf-8').replace('B는 비교 추천이며 사용자 선택은 아직 없다.',f"{spec['selectedCandidate']}를 사용자가 채택했다. 나머지는 비교 보존본이다."),encoding='utf-8')
    allowed={'A','B','C','references','previews','README.md','index.html','preview.js','manifest.json','presentation-spec.json'}
    with zipfile.ZipFile(out/'K4-attack-reduced-candidates-20261101.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.relative_to(out).parts[0] in allowed:z.write(p,p.relative_to(out).as_posix())
    print(json.dumps({'frameCounts':[r['frameCount'] for r in rows],'allCycleMs':1900,'holdMs':650,'byteExactPngCopies':sum(r['frameCount'] for r in rows)*4,'decoderPixelsTiming':'pass','selectedCandidate':spec.get('selectedCandidate')}))


def page(out,info):
    script='''const data=DATA,states=[];let elapsed=0,playing=true,last=null,scene;
const $=s=>document.querySelector(s);const load=p=>new Promise((ok,no)=>{let i=new Image;i.onload=()=>ok(i);i.onerror=()=>no(Error(p));i.src=p});
const keyAt=(r,t)=>{t=Math.min(t,1899);let k=0;while(k<r.durationsMs.length-1&&t>=r.durationsMs[k]){t-=r.durationsMs[k];k++}return k};
const prior=Object.assign({},data.prior,{id:'prior',name:'이전24장'});const rows=data.candidates.concat([prior]);
async function init(){try{for(const row of rows){let r={...row};r.images=await Promise.all(r.mapping.map(m=>load(m.png)));states.push(r)}scene=await load('references/C-game.png');$('#status').textContent='A/B/C와 이전24장 준비 완료';requestAnimationFrame(tick)}catch(e){$('#status').textContent='파일 로드 실패: '+e.message}}
function paint(c,r,scale){const b=data.previewCrop,w=b[2]-b[0],h=b[3]-b[1];if(c.width!==w||c.height!==h){c.width=w;c.height=h}c.style.width=w*scale+'px';c.style.height=h*scale+'px';const x=c.getContext('2d');x.imageSmoothingEnabled=false;x.fillStyle=$('#bg').value;x.fillRect(0,0,w,h);x.drawImage(r.images[keyAt(r,elapsed)],b[0],b[1],w,h,0,0,w,h)}
function draw(){if(states.length!==4||!scene)return;const scale=+$('#scale').value;for(const r of states){paint(document.querySelector('[data-canvas="'+r.id+'"]'),r,scale);let k=keyAt(r,elapsed);document.querySelector('[data-key="'+r.id+'"]').textContent=`키${k+1}/${r.frameCount} · ${r.durationsMs[k]}ms`};$('#phase').textContent=elapsed>=1900?'확인용 반복 간격':elapsed<550?'전조':elapsed<1200?'분사 자세 고정':'회복';$('#time').textContent=(elapsed/1000).toFixed(2)+'초';$('#seek').value=Math.min(elapsed,1900);let r=states.find(r=>r.id===$('#mapselect').value),c=$('#map'),x=c.getContext('2d');x.imageSmoothingEnabled=false;x.clearRect(0,0,960,540);x.drawImage(scene,0,0);x.drawImage(r.images[keyAt(r,elapsed)],238,94)}
function tick(t){if(last===null)last=t;if(playing)elapsed=(elapsed+t-last)%2400;last=t;draw();requestAnimationFrame(tick)}
$('#play').onclick=()=>{playing=!playing;last=performance.now();$('#play').textContent=playing?'전체 정지':'전체 재생'};$('#restart').onclick=()=>{elapsed=0;last=performance.now();draw()};$('#seek').oninput=e=>{playing=false;$('#play').textContent='전체 재생';elapsed=+e.target.value;draw()};document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{playing=false;$('#play').textContent='전체 재생';elapsed=+b.dataset.time;draw()});['scale','bg','mapselect'].forEach(id=>$('#'+id).onchange=draw);document.addEventListener('visibilitychange',()=>last=performance.now());init();'''
    # Read the prior timing and keep the comparison independent of the old output folder.
    parent=json.loads((ROOT/info['source']/'manifest.json').read_text(encoding='utf-8'))
    prior={**parent,'frameCount':24,'mapping':[dict(r) for r in parent['mapping']]}
    # Include the previous24 PNGs locally too, so the comparison ZIP works independently.
    (out/'references/prior-native').mkdir(exist_ok=True)
    for r in prior['mapping']:
        old=ROOT/info['source']/parent['mapping'][r['key']-1]['png'];target=out/'references/prior-native'/old.name;shutil.copy2(old,target);r['png']='references/prior-native/'+old.name
        assert sha(old)==sha(target)
    data={**info,'prior':prior};(out/'preview.js').write_text(script.replace('DATA',json.dumps(data,ensure_ascii=False)),encoding='utf-8')
    cards=[]
    for r in info['candidates']:
        cards.append(f'''<article><h2>{r['id']} · {escape(r['name'])}{' · 채택' if r['id']==info.get('selectedCandidate') else ' · 추천' if r['recommended'] and not info.get('selectedCandidate') else ''}</h2><p>{r['frameCount']}장 = 준비{len(r['windupKeys'])} + 분사1 + 회복{len(r['recoveryKeys'])}</p><p>{escape(r['description'])}</p><div class="viewport"><canvas data-canvas="{r['id']}"></canvas></div><p data-key="{r['id']}"></p><p><a href="{r['id']}/previews/review-white-4x.webp">개별 확대 재생</a> · <a href="{r['id']}/manifest.json">선정 키·시간</a></p></article>''')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 공격 · 프레임 축소 A/B/C</title><style>body{margin:0;background:#f2f0ec;color:#252935;font:16px/1.6 system-ui}main{max-width:1600px;margin:auto;padding:28px}h1{margin:0;font-size:30px}h2{font-size:22px}p{color:#596171}.controls{display:flex;flex-wrap:wrap;gap:12px;align-items:center;margin:18px 0}button,select{font:inherit;padding:8px 12px;background:white;border:1px solid #ccc;border-radius:7px}button{cursor:pointer}a{color:#34548d}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px}article{padding:18px;background:white;border:1px solid #ddd;border-radius:12px;min-width:0}.viewport{overflow:auto;background:white}.viewport canvas{display:block}canvas,img{image-rendering:pixelated}img{max-width:100%}details{margin:24px 0}.note{font-size:14px}#seek{width:280px}@media(max-width:950px){.grid{grid-template-columns:1fr}}</style><main><h1>등짐분사수 공격 · 프레임을 줄인3가지 후보</h1><p>기존24장에서 A19장 / B15장 / C11장으로 줄였습니다. 세 후보 모두 전조0.55초 → 같은 분사 자세0.65초 고정 → 회복0.7초입니다.<br>같은 공격 시간에 남긴 자세를 더 오래 보여줍니다. 기존 픽셀·11색·몸 크기·발/노즐 분사 고정은 그대로이며 새 자세는 만들지 않았습니다.</p><p><a href="K4-attack-reduced-candidates-20261101.zip" download>3후보 PNG·확대·시간 ZIP</a> · <a href="manifest.json">전체 시간·선정 키</a></p><div class="controls"><button id="play">전체 정지</button><button id="restart">함께 처음부터</button><select id="scale"><option value="1">실제1배</option><option value="2">2배</option><option value="4" selected>4배</option><option value="8">8배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 벽색</option><option value="#708b92">푸른 원경색</option><option value="#111316">검정 근경색</option></select></div><div class="controls"><button data-time="0">전조 처음</button><button data-time="550">분사 고정</button><button data-time="1200">회복 처음</button><input id="seek" type="range" min="0" max="1900" step="1" value="0"><strong id="phase"></strong><span id="time"></span></div><div class="grid">'''+''.join(cards)+'''</div><p id="status">프레임 준비 중…</p><p class="note">공격은 모두1.9초이며 반복 사이0.5초는 확인용 간격입니다. B는 자세 변화와 연결의 균형을 추천한 후보이며 아직 선택하지 않았습니다.</p><details><summary>이전24장도 같은 시간축으로 비교</summary><div class="viewport"><canvas data-canvas="prior"></canvas></div><p data-key="prior"></p></details><h2>기존 맵에서 비교</h2><div class="controls"><select id="mapselect"><option>A</option><option selected>B</option><option>C</option><option value="prior">이전24장</option></select></div><div class="viewport"><canvas id="map" width="960" height="540"></canvas></div><p class="note">원본960×540 배경·플레이어와 같은 배율인 합성 미리보기이며 실제 게임 캡처가 아닙니다. 잉크 효과·전투 수치·Unity는 변경하지 않았습니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')

    if info.get('selectedCandidate'):
        p=out/'index.html';html=p.read_text(encoding='utf-8')
        html=html.replace('B는 자세 변화와 연결의 균형을 추천한 후보이며 아직 선택하지 않았습니다.',f"{info['selectedCandidate']}19장을 사용자가 채택했습니다. 기존11색/384×224 논리 픽셀 프레임이며 분사 고정650ms를 유지합니다.")
        html=html.replace('<option>A</option><option selected>B</option>','<option selected>A</option><option>B</option>')
        p.write_text(html,encoding='utf-8')


if __name__=='__main__':build()
