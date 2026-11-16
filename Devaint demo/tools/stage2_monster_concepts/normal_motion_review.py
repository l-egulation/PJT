"""Collect current normal monster motions into a synchronized local review viewer."""
import json
import base64
import shutil
import zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont

from b3_walk_return import dump,white
from k4_d_pixel_walk import ROOT,N,sha

SPEC=ROOT/'data/presentation/stage2_normal_motion_review.json'
HERE=__import__('pathlib').Path(__file__).resolve().parent


def read_json(path):return json.loads(path.read_text(encoding='utf-8'))


def load_native(folder,paths,ds,offset,canvas):
    frames=[];records=[]
    assert len(paths)==len(ds) and all(d>0 for d in ds)
    for p in paths:
        f=Image.open(p).convert('RGBA');assert f.size==(384,224)
        c=Image.new('RGBA',tuple(canvas));c.alpha_composite(f,tuple(offset));a=np.array(f);b=np.array(c)
        assert np.array_equal(a,b[offset[1]:offset[1]+224,offset[0]:offset[0]+384])
        frames.append(c);records.append({'source':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'mode':'native-pixel-copy'})
    return frames,list(ds),records


def load_composite(path,world_crop,guide_y,translation,canvas):
    im=Image.open(path);frames=[];ds=[];records=[];x0,y0,x1,y1=world_crop
    for i in range(im.n_frames):
        im.seek(i);im.load();a=np.array(im.convert('RGBA'));assert a.shape[:2]==(y1-y0,x1-x0)
        transparent=np.all(a[:,:,:3]==255,axis=2);guide=np.zeros_like(transparent);gy=guide_y-y0
        if 0<=gy<a.shape[0]:guide[gy]=np.all(a[gy,:,:3]==[180,169,145],axis=1)
        keep=~transparent&~guide;original=a.copy();a[~keep]=0;a[keep,3]=255
        c=Image.new('RGBA',tuple(canvas));pos=[x0-translation[0],y0-translation[1]];c.alpha_composite(Image.fromarray(a),tuple(pos));b=np.array(c)
        assert int((b[:,:,3]>0).sum())==int(keep.sum())
        yy,xx=np.where(keep);assert np.array_equal(b[yy+pos[1],xx+pos[0]],original[yy,xx])
        frames.append(c);ds.append(round(im.info['duration']));records.append({'source':path.relative_to(ROOT).as_posix(),'sha256':sha(path),'decodedFrame':i,'mode':'lossless-composite-white-guide-removed','opaquePixelsPreserved':int(keep.sum())})
    return frames,ds,records


def build():
    s=read_json(SPEC);out=ROOT/s['output'];out.mkdir(parents=True,exist_ok=True);(out/'references').mkdir(exist_ok=True)
    scene=Image.open(ROOT/s['map']).convert('RGBA');assert scene.size==(960,540)
    player=Image.open(ROOT/s['player']).convert('RGBA');pb=player.getbbox();player.crop(pb).save(out/'references/player-visible.png')
    shutil.copy2(ROOT/s['map'],out/'references/C-game.png');shutil.copy2(ROOT/s['player'],out/'references/approved-player.png');shutil.copy2(SPEC,out/'references/presentation-spec.json')
    idle=read_json(ROOT/s['idleRoot']/'manifest.json');move_spec=read_json(ROOT/s['movementSpec']);moves={m['id']:m for m in move_spec['monsters']}
    data=[];all_sources={};static=[];font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',19)
    for m in s['monsters']:
        ident=m['id'];clips=[]
        idle_info=next(r for r in idle if r['id']==ident);folder=ROOT/s['idleRoot']/ident
        frames,ds,records=load_native(folder,[folder/'frames'/f'idle-{i:02}.png' for i in range(len(idle_info['durations']))],idle_info['durations'],s['nativeOffset'],s['canvas'])
        clips.append({'state':'idle','label':'대기','frames':frames,'durationsMs':ds,'records':records,'gapMs':0,'note':idle_info['description'],'status':'기존 몸통 호흡 대기 시안'})
        mv=moves[ident];folder=ROOT/mv['source'];mi=read_json(folder/'manifest.json');frames,ds,records=load_native(folder,sorted((folder/'native').glob('*.png')),mi['durationsMs'],s['nativeOffset'],s['canvas'])
        assert len(frames)==mv['frameCount'] and sum(ds)==mv['cycleMs']
        clips.append({'state':'move','label':'이동','frames':frames,'durationsMs':ds,'records':records,'gapMs':0,'note':mv['choice']+' · 제자리 이동 루프','status':'사용자 채택 이동'})
        folder=ROOT/m['attack'];gap=m['gapMs']
        if m['attackKind']=='composite':
            frames,ds,records=load_composite(folder/m['whiteClip'],m['worldCrop'],m['guideWorldY'],s['canonicalToWorld'],s['canvas'])
            motion=sum(ds)-gap
        else:
            ai=read_json(folder/'manifest.json')
            if m['attackKind']=='timeline':paths=[folder/r['path'] for r in ai['timeline']];ds=[r['durationMs'] for r in ai['timeline']]
            else:paths=sorted((folder/m.get('nativeSubdir','native')).glob('*.png'));ds=ai['durationsMs']
            frames,ds,records=load_native(folder,paths,ds,s['nativeOffset'],s['canvas']);motion=sum(ds)
            frames.append(frames[-1]);ds.append(gap);records.append({**records[-1],'mode':'unchanged-last-pose-review-gap'})
        clips.append({'state':'attack','label':'공격','frames':frames,'durationsMs':ds,'records':records,'gapMs':gap,'motionMs':motion,'note':m['attackNote'],'status':m['attackStatus']})
        union=Image.new('RGBA',tuple(s['canvas']))
        for clip in clips:
            for f in clip['frames']:union.alpha_composite(f)
        bb=union.getbbox();crop=[max(0,bb[0]-12),max(0,bb[1]-12),min(s['canvas'][0],bb[2]+12),min(s['canvas'][1],bb[3]+12)]
        meta={k:v for k,v in m.items() if k in ('id','name','role')};meta.update(crop=crop,clips=[])
        overview=[]
        for clip in clips:
            folder=out/'clips'/ident/clip['state'];folder.mkdir(parents=True,exist_ok=True);cache={};paths=[];hashes=[]
            for f,record in zip(clip['frames'],clip['records']):
                image=f.crop(tuple(crop));a=np.array(image);assert set(np.unique(a[:,:,3]))<={0,255}
                key=__import__('hashlib').sha256(image.tobytes()).hexdigest()
                if key not in cache:
                    target=folder/f'frame-{len(cache)+1:03}.png';image.save(target);cache[key]=target.relative_to(out).as_posix()
                paths.append(cache[key]);hashes.append(key)
                source=ROOT/record['source'];all_sources[source]=record['sha256']
            motion_ms=clip.get('motionMs',sum(clip['durationsMs']));starts=np.cumsum([0]+clip['durationsMs'][:-1]).tolist()
            info={k:v for k,v in clip.items() if k not in ('frames','records')};info.update(paths=paths,rgbaHashes=hashes,sourceRecords=clip['records'],frameEntries=len(paths),motionFrameEntries=sum(t<motion_ms for t in starts),uniqueFrames=len(cache),cycleMs=sum(clip['durationsMs']),motionMs=motion_ms)
            meta['clips'].append(info)
            representative=len(clip['frames'])//3
            if clip['state']=='attack':
                if ident=='B3':representative=max(range(len(clip['frames'])),key=lambda k:clip['frames'][k].getbbox()[3]-clip['frames'][k].getbbox()[1])
                elif ident=='K4':representative=next(k for k,t in enumerate(starts) if t>=1200)
                elif ident=='M3':representative=next(k for k,r in enumerate(clip['records']) if 'source-019' in r['source'])
                elif ident in ('H3','R-selected'):representative=next(k for k,t in enumerate(starts) if t>= (1700 if ident=='H3' else 1560))
            overview.append(clip['frames'][representative].crop(tuple(crop)))
        data.append(meta);static.append((meta,overview))
    # Preserve each actual input file once, rather than changing any approved source.
    source_entries=[]
    for i,(p,digest) in enumerate(all_sources.items()):
        target=out/'references/source-files'/f'{i:03}{p.suffix}';target.parent.mkdir(exist_ok=True);shutil.copy2(p,target);assert sha(p)==sha(target)==digest;source_entries.append({'source':p.relative_to(ROOT).as_posix(),'copy':target.relative_to(out).as_posix(),'sha256':digest})
    info={**s,'monsters':data,'sources':source_entries,'mapPath':'references/C-game.png','playerPath':'references/player-visible.png','playerCanvas':list(player.size),'playerVisibleBounds':list(pb),'runtimeIntegration':'not-performed'}
    # Local artifact viewers may not resolve nested relative image URLs. Ship a
    # self-contained HTML, retaining the separate PNGs for inspection/export.
    paths={info['mapPath'],info['playerPath']}
    paths.update(p for m in data for c in m['clips'] for p in c['paths'])
    info['embeddedImages']={p:'data:image/png;base64,'+base64.b64encode((out/p).read_bytes()).decode('ascii') for p in sorted(paths)}
    for p,uri in info['embeddedImages'].items():assert base64.b64decode(uri.split(',',1)[1])==(out/p).read_bytes()
    dump(out/'manifest.json',info)
    data_js='window.MOTION_DATA='+json.dumps(info,ensure_ascii=False)+';\n'
    viewer_js=(HERE/'normal_motion_review.js').read_text(encoding='utf-8')
    (out/'data.js').write_text(data_js,encoding='utf-8');(out/'viewer.js').write_text(viewer_js,encoding='utf-8')
    # Escape HTML script end tags even when they occur in JSON descriptions.
    inline=lambda source:source.replace('</','<\\/')
    html=(HERE/'normal_motion_review.html').read_text(encoding='utf-8').replace('<script src="data.js"></script><script src="viewer.js"></script>','<script>'+inline(data_js)+'</script><script>'+inline(viewer_js)+'</script>')
    (out/'index.html').write_text(html,encoding='utf-8')
    (out/'review-inline.html').write_text(html,encoding='utf-8')
    draw_board(static,out/'previews/overview-contact.png',font)
    (out/'README.md').write_text('''# 일반 몬스터5종 · 대기/이동/공격 종합 검토

기존 몸통 호흡 대기8키, 채택 이동, K4 A19 본체와 잉크만 분리·픽셀 보충한 분사, B3 무보충19장 찍기, M3 빠른4연타, 신호투척수 원형 통/파손, 수의27키 생성/점성 비행/부글 장판을 모은15동작이다. K4는 기존 본체를 유지하고 분사 자세를2.1초 동안 유지하는 최신 합성을 사용한다. 이번 종합판에서는 추가 포즈/이펙트를 만들거나 원본을 수정하지 않는다. B3 돌진 연결은 별도다.

각 원본 PNG와 합성 원배율 무손실 WebP를 참조 사본/해시로 보존한다. 합성 클립은 정확한 흰 바탕과 알려진 바닥선 색만 제거하며 남은 픽셀/배치/시간은 모두 보존한다.384×224 원본은 한 정수 위치로512×384 공통 좌표에 놓고 개체별3동작 공통 여백으로만 자른다. 종별 크기를 자동으로 맞추지 않는다.1/2/3/4 정수배와 원본960×540 맵/플레이어 비교를 제공한다.

15칸 독립 반복과 개체별 대기→이동→공격 연속 모드, 전체 재생/정지/속도/다시 시작, 개별 프레임 이전/다음/슬라이더를 제공한다. 연속 모드는 기존 클립을 이어 볼 뿐 중간 포즈를 새로 보간하지 않는다. 공격 끝의 기존 확인 간격은 별도 표시한다. 맵은 원본을 밝히거나 흐리지 않은 합성 미리보기이며 실제 게임 캡처가 아니다. Unity/전투/충돌 검증은 포함하지 않는다.

index.html 안에 PNG·맵·플레이어·데이터·재생 코드를 포함하여 상대 이미지 경로를 읽지 않고 재생한다. 개별 PNG와 외부 JS 사본은 검토/내보내기용으로 보존한다. 이미지별로 독립 로딩하며 실패/지연된 이미지는 해당 칸에 표시하고 나머지 동작은 계속 재생한다.
''',encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','monsterCount':5,'clipCount':15,'sourceCopiesByteIdentical':len(source_entries),'framePixelPlacementAndTimingPreserved':True,'compositeForegroundPixelsExact':True,'commonCropPerMonster':True,'integerZoomOnly':True,'embeddedPngsByteIdentical':len(paths),'selfContainedHtml':True,'browserQA':'not-run','Unity':'not-run'})
    print(json.dumps({'monsters':5,'clips':15,'sourceFiles':len(source_entries),'frames':sum(c['frameEntries'] for m in data for c in m['clips'])}))


def draw_board(rows,path,font):
    path.parent.mkdir(exist_ok=True);out=Image.new('RGB',(1800,1820),'white');d=ImageDraw.Draw(out)
    for j,title in enumerate(('대기','이동','공격')):d.text((j*600+190,16),title,font=font,fill='#80586e')
    for i,(m,frames) in enumerate(rows):
        y=55+i*350;d.text((12,y),m['name']+' · '+m['role'],font=font,fill='#29252f')
        for j,f in enumerate(frames):
            # Native pixels only; the viewer supplies integer enlargement.
            w=white(f);out.paste(w,(j*600+max(0,(600-f.width)//2),y+40))
    out.save(path)


if __name__=='__main__':build()
