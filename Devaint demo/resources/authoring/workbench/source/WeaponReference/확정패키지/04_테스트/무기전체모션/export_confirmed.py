"""Freeze the reviewed four-weapon gallery into explicit game asset packages."""
from pathlib import Path
from PIL import Image
import base64, copy, hashlib, io, json, math

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
W=ROOT/'02_에셋/캐릭터/잉크/무기'
REPORT=ROOT/'00_안내/네무기_확정정리_20261016'
REPORT.mkdir(parents=True,exist_ok=True)
html=(HERE/'전체모션.html').read_text(encoding='utf8')
DATA=json.JSONDecoder().raw_decode(html.split('const DATA=',1)[1])[0]
NAMES={'sword':'검','whip':'채찍','rapier':'레이피어','dagger':'단검'}

def write(p,d):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')

def decode(uri):
    return Image.open(io.BytesIO(base64.b64decode(uri.split(',',1)[1]))).convert('RGBA')

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def relative(p):return p.relative_to(ROOT).as_posix()
def sheet(images,p,cell):
    cols=min(8,len(images));rows=math.ceil(len(images)/cols)
    atlas=Image.new('RGBA',(cols*cell[0],rows*cell[1]))
    rects=[]
    for i,im in enumerate(images):
        x=i%cols*cell[0];y=i//cols*cell[1];atlas.alpha_composite(im,(x,y))
        rects.append([x,y,*cell])
    p.parent.mkdir(parents=True,exist_ok=True);atlas.save(p)
    return rects

def export_weapon(key,w,label=None):
    name=NAMES[key];out=W/name/('99_이전시안/이전공격_확정보존' if label else '04_확정본/게임용_최종')
    out.mkdir(parents=True,exist_ok=True)
    used=sorted({s['frame'] for m in w['modules'] for s in m['steps']})
    originals={k:decode(w['frames'][k]) for k in used};registration={};bounds=[]
    # Align by the same foot pivots and registration offsets as the reviewed gallery.
    for k,im in originals.items():
        p=w.get('framePivots',{}).get(k,w['pivot']);o=w.get('frameOffsets',{}).get(k,[0,0])
        registration[k]=[o[0]-p[0],o[1]-p[1]]
        b=im.getbbox()
        if b:bounds.append([b[0]+registration[k][0],b[1]+registration[k][1],b[2]+registration[k][0],b[3]+registration[k][1]])
    left=min(b[0] for b in bounds)-4;top=min(b[1] for b in bounds)-4
    right=max(b[2] for b in bounds)+4;bottom=max(b[3] for b in bounds)+4
    cell=[math.ceil((right-left)/8)*8,math.ceil((bottom-top)/8)*8];pivot=[-left,-top]
    assets={};logical={};hash_to_id={};source_hashes={}
    for k,im in originals.items():
        aligned=Image.new('RGBA',cell);offset=registration[k]
        aligned.alpha_composite(im,(offset[0]-left,offset[1]-top))
        digest=hashlib.sha256(aligned.tobytes()).hexdigest()
        source_hashes[k]=hashlib.sha256(base64.b64decode(w['frames'][k].split(',',1)[1])).hexdigest()
        if digest not in hash_to_id:
            uid=f'F{len(assets)+1:03}';hash_to_id[digest]=uid;assets[uid]=aligned
            folder=out/'프레임';folder.mkdir(exist_ok=True);aligned.save(folder/f'{uid}.png')
        logical[k]=hash_to_id[digest]
    ids=list(assets);rects=sheet(list(assets.values()),out/'전체_스프라이트시트.png',cell)
    config={'status':'사용자확정','date':'2026-10-16','weapon':name,'native_pixel_scale':1,
      'sheet':'전체_스프라이트시트.png','cell':cell,'pivot_top_left':pivot,
      'unity_pivot_normalized':[pivot[0]/cell[0],1-pivot[1]/cell[1]],
      'frames':{k:{'file':f'프레임/{k}.png','rect':r} for k,r in zip(ids,rects)},
      'source_frame_map':logical,'modules':copy.deepcopy(w['modules']),
      'attack_events':w['attacks'],'source_png_sha256':source_hashes,
      'original_canvas_pivots':{k:w.get('framePivots',{}).get(k,w['pivot']) for k in used},
      'note':'원본 픽셀 확대·축소 없음. 투명 여백과 발 기준점만 통일. rect 좌표는 좌상단 기준. 그림 수와 재생 단계 수는 구분한다.'}
    module_sheets={}
    for m in config['modules']:
        mids=[]
        for s in m['steps']:
            source=s['frame'];s['source_frame']=source;s['frame']=logical[source]
            if s['frame'] not in mids:mids.append(s['frame'])
        rect=sheet([assets[k] for k in mids],out/'모션별시트'/f"{m['id']}.png",cell)
        module_sheets[m['id']]={'file':f"모션별시트/{m['id']}.png",'frames':dict(zip(mids,rect))}
    config['motion_sheets']=module_sheets
    for m in config['modules']:
        m['duration_ms']=sum(s['ms'] for s in m['steps'])
    config['state_groups']={'jump':['jump_start','jump_rise','jump_apex'],'fall':['fall_start','fall_loop','land'],'dash':['dash_start','dash_active','dash_end'],'death':['death_start','death_fall','death_hold']}
    if key=='sword' and not label:
        config['attack_events']['1']['impact']=155
        config['attack_events']['2']['impact']=105
        config['combo_window_ms']=220
    if not label:
        effect_folder='기본공격' if key=='sword' else name
        eout=ROOT/f'02_에셋/이펙트/{effect_folder}/04_확정본'
        effect_config={'status':'사용자확정','weapon':name,'effects':{},'attack_events':w['attacks'],
            'placement':{'source_pivot':w.get('fxPivot',w['pivot']),'offset':w.get('fxOffset',[0,0]),'aligned_hits':w.get('alignedFx')},
            'note':'각 이펙트의 source_pivot은 기존 캐릭터 캔버스 좌표다. 캐릭터를 발 기준으로 배치한 뒤 source_pivot을 빼고 offset을 더해 이펙트를 배치한다. aligned_hits가 있으면 해당 타격의 프레임별 offset/flip_x를 우선한다.'}
        for kind,sources in w['effects'].items():
            images=[decode(s) for s in sources];ecell=[max(im.width for im in images),max(im.height for im in images)]
            aligned=[];files=[]
            for i,(im,src) in enumerate(zip(images,sources),1):
                p=eout/kind/f'{i:02}.png';p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(base64.b64decode(src.split(',',1)[1]));files.append(relative(p))
                tile=Image.new('RGBA',ecell);tile.alpha_composite(im);aligned.append(tile)
            rect=sheet(aligned,eout/f'{kind}_스프라이트시트.png',ecell)
            timing=w.get('effectTiming',{}).get(kind,w['effectMs'])[:len(images)]
            effect_config['effects'][kind]={'sheet':f'{kind}_스프라이트시트.png','cell':ecell,'frames':[f'{kind}/{i:02}.png' for i in range(1,len(images)+1)],'rects':rect,'ms':timing}
        write(eout/'이펙트설정.json',effect_config)
        config['effect_config']=relative(eout/'이펙트설정.json')
    write(out/'재생설정.json',config)
    (out/'읽어주세요.md').write_text(f'# 잉크 {name} 확정 패키지\n\n- 전체_스프라이트시트.png: 중복 그림을 제외한 {len(assets)}장.\n- 프레임/: 투명 PNG 개별 그림.\n- 모션별시트/: 동작별로 필요한 그림만 수록.\n- 재생설정.json: 재사용 순서, 유지 시간, 발 기준점, 타격 이벤트 및 이펙트 설정 경로.\n- 필터링 Point, 압축 None, 회전 패킹 비활성 권장. 해상도 축소 없음.\n- 캔버스 여백을 통일했으므로 원본 PNG와 새 PNG의 pivot을 혼용하지 않는다.\n- Unity에 자동 적용하지 않은 확정 에셋 전달본이다.\n',encoding='utf8')
    return {'weapon':name,'folder':relative(out),'unique_frames':len(assets),'logical_frames':len(used),'modules':len(config['modules']),'cell':cell,'pivot':pivot,'effect_config':config.get('effect_config')}

summaries=[export_weapon(k,w) for k,w in DATA['weapons'].items()]
legacy=copy.deepcopy(DATA['legacySword']);legacy['modules']=[m for m in legacy['modules'] if m['id'].startswith('attack')]
legacy_summary=export_weapon('sword',legacy,'previous')
confirmed={k:copy.deepcopy(w) for k,w in DATA['weapons'].items()}
# A frozen gallery source is independent of experimental generator paths.
write(HERE/'확정미리보기데이터.json',{'weapons':confirmed,'legacySword':DATA['legacySword']})
write(REPORT/'확정패키지_목록.json',{'weapons':summaries,'previous_sword_attack':legacy_summary,'preview':relative(HERE/'전체모션.html')})
for item in summaries:
    name=item['weapon'];guide=W/name/'00_안내.md';text=guide.read_text(encoding='utf8')
    heading='## 2026-10-16 네 무기 최종 확정'
    if heading not in text:
        first,_,rest=text.partition('\n')
        text=first+'\n\n'+heading+'\n\n- 현재 전달용 확정본: [게임용_최종](04_확정본/게임용_최종/읽어주세요.md).\n- 캐릭터 전체 시트·모션별 시트·개별 프레임·재생설정을 함께 사용한다. 이펙트 경로는 재생설정에 기록되어 있다.\n- 전체 확인: 프로젝트 `04_테스트/무기전체모션/전체모션.html`. 이전 날짜의 시안 설명보다 이 항목을 우선한다.\n'+rest
        guide.write_text(text,encoding='utf8')
checks=[]
for item in summaries+[legacy_summary]:
    out=ROOT/item['folder'];cfg=json.loads((out/'재생설정.json').read_text(encoding='utf8'))
    for uid,f in cfg['frames'].items():
        im=Image.open(out/f['file']);assert list(im.size)==cfg['cell'];assert im.getbbox();checks.append(relative(out/f['file']))
    for m in cfg['modules']:assert all(s['frame'] in cfg['frames'] for s in m['steps'])
write(REPORT/'패키지_검증.json',{'passed':True,'individual_frames_checked':len(checks),'files':checks})
inventory={relative(p):sha(p) for item in summaries+[legacy_summary] for p in (ROOT/item['folder']).rglob('*') if p.is_file()}
write(REPORT/'확정파일_SHA256.json',inventory)
print(json.dumps(summaries,ensure_ascii=False));print('Previous sword attack preserved; all package references validated.')
