"""Compare original selected art with style-transfer candidates on unchanged sewer layers."""
import json, shutil, zipfile, re
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label, find_objects
from style_unification import ROOT, SOURCE, BASE, OUT, FONT, ITEMS, sha

N=Image.Resampling.NEAREST


def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def fit(im,cap):
    factor=min(cap[0]/im.width,cap[1]/im.height)
    return im.resize((max(1,round(im.width*factor)),max(1,round(im.height*factor))),N)


def build():
    manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
    choices=json.loads((SOURCE/'current-choices.json').read_text(encoding='utf-8'))
    assert choices['rosterCounts']=={'melee':3,'ranged':2,'elite':1,'bossConcept':1}
    roles_before=choices['adoptedIds'][:]
    refs=OUT/'references';original_bg=ROOT.parent/'stage2-waterway-reference/outputs/waterway/parallax-kit-v3-20261031'
    for p in refs.rglob('*'):
        if p.is_file() and p.name!='sources.json':assert sha(p)==sha(original_bg/p.relative_to(refs))
    player_canvas=Image.open(refs/'assets/approved-player.png').convert('RGBA');pb=player_canvas.getchannel('A').getbbox();player=player_canvas.crop(pb)
    assert player.size==(66,64)
    font=lambda n:ImageFont.truetype(FONT,n)
    before,after,meta={},{},[]
    for record in manifest:
        ident=record['id'];folder=OUT/ident
        assert sha(folder/'before/design-cutout.png')==record['beforeCutoutSha256']==sha(BASE/'selected'/ident/'design-cutout.png')
        assert sha(folder/'before/original-board.png')==record['beforeOriginalSha256']
        raw=Image.open(folder/'raw/generated.png').convert('RGBA');arr=np.array(raw);labs,_=label(arr[:,:,3]>=180)
        regions=sorted([(int((labs[b]==i).sum()),i,[b[1].start,b[0].start,b[1].stop,b[0].stop]) for i,b in enumerate(find_objects(labs),1)],reverse=True)
        assert len([r for r in regions if r[0]>1000])==1,(ident,regions[:3])
        area,component,box=regions[0]
        assert 0<box[0]<box[2]<raw.width and 0<box[1]<box[3]<raw.height,(ident,box,raw.size)
        selected=labs==component;a=np.zeros_like(arr);a[selected,:3]=arr[selected,:3];a[selected,3]=255
        cut=Image.fromarray(a).crop(box);cut.save(folder/'cutouts/style-enlarged.png')
        old=Image.open(folder/'before/design-cutout.png').convert('RGBA')
        cap=ITEMS[ident][2:4];before[ident]=fit(old,cap);after[ident]=fit(cut,cap)
        after[ident].save(folder/'cutouts/style-scale-study.png');before[ident].save(folder/'previews/before-scale-study.png')
        om=np.array(old.getchannel('A').resize((256,256),N))>=180;nm=np.array(cut.getchannel('A').resize((256,256),N))>=180
        overlap=float((om&nm).sum()/(om|nm).sum())
        row=dict(record,afterRawSha256=sha(folder/'raw/generated.png'),afterSourceBBox=box,afterReviewExtent=list(after[ident].size),beforeReviewExtent=list(before[ident].size),normalizedSilhouetteOverlap=round(overlap,4),aspectRatioBefore=round(old.width/old.height,4),aspectRatioAfter=round(cut.width/cut.height,4),derivedAlpha='Largest connected silhouette alpha>=180; RGB unchanged; generated original preserved.')
        meta.append(row);dump(folder/'metadata.json',row)
        pair=Image.new('RGBA',(1200,460),'#eeeee8');d=ImageDraw.Draw(pair)
        d.text((24,12),f'{ident} {ITEMS[ident][0]} · {ITEMS[ident][1]}',font=font(23),fill='#263640')
        for cx,title,spr in [(300,'이전 채택 그림',before[ident]),(900,'화풍·색 조정 시안',after[ident])]:
            scale=min(4,500/cap[0],310/cap[1]);shown=spr.resize((round(spr.width*scale),round(spr.height*scale)),N)
            pair.alpha_composite(shown,(cx-shown.width//2,380-shown.height));d.text((cx-120,410),title,font=font(20),fill='#526570')
        pair.convert('RGB').save(folder/'previews/before-after.png')

    (OUT/'previews').mkdir(exist_ok=True)
    board=Image.new('RGBA',(1600,1560),'#eeeee8');d=ImageDraw.Draw(board)
    d.text((32,22),'2스테이지 · 화풍과 색 조정 시안',font=font(34),fill='#24323c')
    d.text((34,72),'근거리 3 · 원거리 2 · 엘리트 1 · 남매 보스 1 / 테두리 없는 큰 색 면과 차가운 그림자',font=font(21),fill='#526570')
    slots=[(24,120,504,410),(548,120,504,410),(1072,120,504,410),(24,550,764,410),(812,550,764,410),(24,980,764,470),(812,980,764,470)]
    for (ident,m),(x,y,w,h) in zip(ITEMS.items(),slots):
        d.rounded_rectangle((x,y,x+w,y+h),12,fill='#f9f8f2',outline='#c5ccc9',width=2)
        d.text((x+20,y+16),f'{ident} · {m[1]}',font=font(22),fill='#526570')
        cut=Image.open(OUT/ident/'cutouts/style-enlarged.png').convert('RGBA');shown=fit(cut,(w-64,h-128))
        board.alpha_composite(shown,(x+(w-shown.width)//2,y+64+(h-128-shown.height)//2))
        d.text((x+20,y+h-48),m[0],font=font(25),fill='#263640')
    d.text((30,1475),'기존 채택 디자인의 화풍 편집 시안 · 개체별 확대 배율 다름 · 실제 체급은 아래 화면에서 비교',font=font(19),fill='#526570')
    d.text((30,1510),'보스는 원래 연결체 유지 · 독립된 남매 재설계 미포함 · 최종 크기와 전투 수치 미정',font=font(19),fill='#526570')
    board.convert('RGB').save(OUT/'unified-board.png')
    compare=Image.new('RGB',(1600,3360),'#eeeee8');draw=ImageDraw.Draw(compare)
    draw.text((24,16),'채택 디자인 · 동일 배율 수정 전후 비교',font=font(30),fill='#24323c')
    for i,ident in enumerate(ITEMS):
        # Each row keeps its own equal before/after display scale.
        panel=Image.open(OUT/ident/'previews/before-after.png')
        compare.paste(panel,(200,65+i*470))
    compare.save(OUT/'before-after-board.png')
    for label_name,sprites in [('before',before),('after',after)]:
        strip=Image.new('RGBA',(1200,250),'#232c35');sd=ImageDraw.Draw(strip);x=24;ground=198
        for ident,spr in [('플레이어',player)]+list(sprites.items()):
            strip.alpha_composite(spr,(x,ground-spr.height));sd.text((x,214),'R 투척수' if ident=='R-selected' else ident,font=font(15),fill='#eeeee8');x+=spr.width+24
        assert x<1200,x
        strip.convert('RGB').save(OUT/'previews'/f'{label_name}-native-lineup.png')

    layout=json.loads((refs/'layout.json').read_text(encoding='utf-8'));scenes={s['id']:s for s in layout['scenes']};front=Image.open(refs/'assets/foreground.png').convert('RGBA')
    def screen(path,scene,placements):
        bg=Image.open(refs/f'{scene}-game.png').convert('RGBA');layer=Image.new('RGBA',bg.size);fg=layer.copy();nx=scenes[scene]['view'][0]
        fg.alpha_composite(front,(490-round((nx-480)*.4375),-100))
        for spr,x,foot in placements:layer.alpha_composite(spr,(x,foot-spr.height))
        a=np.array(layer);fa=np.array(fg.getchannel('A'),dtype=np.uint16);a[:,:,3]=(a[:,:,3].astype(np.uint16)*(255-fa)//255).astype(np.uint8);mask=a[:,:,3]>0
        result=bg.copy();result.alpha_composite(Image.fromarray(a));old,new=np.array(bg),np.array(result)
        assert np.array_equal(old[~mask],new[~mask]);assert np.array_equal(old[220:288,130:198],new[220:288,130:198])
        result.convert('RGB').save(path)
        return {'scene':scene,'size':[960,540],'backgroundAndPlayerOutsideInsertionsUnchanged':True,'originalForegroundOcclusionPreserved':True}
    checks={};sections=[]
    patches=[Image.open(refs/'assets/wall.png').convert('RGBA').crop((96,128,416,344)),Image.open(refs/'assets/far.png').convert('RGBA').crop((800,300,1120,516)),Image.open(refs/'C-game.png').convert('RGBA').crop((620,0,940,216))]
    for label_name,sprites in [('before',before),('after',after)]:
        vis=Image.new('RGBA',(960,1512))
        for i,(ident,spr) in enumerate(sprites.items()):
            for j,bg in enumerate(patches):
                cell=bg.copy();cell.alpha_composite(spr,((320-spr.width)//2,208-spr.height));vis.alpha_composite(cell,(j*320,i*216))
        vis.convert('RGB').save(OUT/'previews'/f'{label_name}-visibility.png')
        for ident,spr in sprites.items():
            scene={'H3':'A','B3':'A','M3':'C','R-selected':'C','K4':'C','A3':'D','I4':'E'}[ident]
            x,foot=(230,352) if ident=='R-selected' else (326,270 if ident=='H3' else 288)
            checks[f'{ident}-{label_name}']=screen(OUT/ident/'previews'/f'{label_name}-960x540.png',scene,[(spr,x,foot)])
        groups={'melee':('A',[(sprites['K4'],230,334),(sprites['B3'],322,288),(sprites['M3'],440,288)]),'ranged':('C',[(sprites['R-selected'],230,352),(sprites['H3'],380,288)]),'elite':('D',[(sprites['A3'],326,288)]),'boss':('E',[(sprites['I4'],326,288)])}
        for group,(scene,placements) in groups.items():checks[f'{group}-{label_name}']=screen(OUT/'previews'/f'{group}-{label_name}-960x540.png',scene,placements)
    overview=Image.new('RGB',(1920,1160),'#18212a');od=ImageDraw.Draw(overview)
    for i,(group,title) in enumerate([('melee','근거리 3종'),('ranged','원거리 2종'),('elite','엘리트'),('boss','남매 보스 · 비교 크기')]):
        x,y=(i%2)*960,(i//2)*580
        od.text((x+16,y+7),f'{title} · 960×540 합성 미리보기',font=font(20),fill='#e7e9e3')
        overview.paste(Image.open(OUT/'previews'/f'{group}-after-960x540.png'),(x,y+40))
    overview.save(OUT/'previews/map-overview.png')
    for ident,m in ITEMS.items():
        sections.append(f'''<section id="{ident}"><h2>{ident} {m[0]} · {m[1]}</h2><img class="board" src="{ident}/previews/before-after.png" alt="{ident} 수정 전후"><details><summary>원본 배경 위 · 같은 크기 비교</summary><p>위: 이전 / 아래: 조정 시안. 배경과 플레이어를 바꾸지 않은 960×540 합성입니다.</p><div class="native"><img src="{ident}/previews/before-960x540.png" width="960" height="540"><img src="{ident}/previews/after-960x540.png" width="960" height="540"></div></details><p><a href="{ident}/raw/generated.png">편집 생성 원본</a> · <a href="{ident}/before/design-cutout.png">이전 채택 그림</a> · <a href="{ident}/prompt.txt">편집 프롬프트</a></p></section>''')
    groups_html=''.join(f'<h3>{name}</h3><div class="native"><img src="previews/{group}-after-960x540.png" width="960" height="540"></div>' for group,name in [('melee','근거리 3종'),('ranged','원거리 2종'),('elite','엘리트'),('boss','남매 보스 크기 검토')])
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>2스테이지 몬스터 화풍·색 조정</title><style>body{margin:0;background:#17212a;color:#e7e9e3;font:17px/1.75 "Malgun Gothic",sans-serif}main{max-width:1600px;margin:auto;padding:28px}a{color:#addbea}.board{display:block;width:100%;height:auto}.native{overflow-x:auto}.native img{display:block;max-width:none;image-rendering:pixelated}section{padding:24px 0;border-top:1px solid #52606a}summary{cursor:pointer;padding:14px 0}p{max-width:1200px}h1{font-size:30px}h2{font-size:24px}</style><main><h1>채택 7종 · 화풍과 색 조정 시안</h1><p>근거리 K4·B3·M3 / 원거리 H3·신호투척수 / 엘리트 A3 / 남매 보스 I4를 유지했습니다. 큰 재질 면, 제한된 명암, 차가운 회청색 그림자, 테두리 없는 표현을 기준으로 각 개체를 기본 image_gen으로 편집했습니다. 개체별 원래 색을 살려 모두 같은 색으로 통일하지 않았습니다.</p><p>새 화풍 시안은 검토용 파생본이며 이전 채택 원본을 덮어쓰지 않았습니다. 생성 편집이므로 픽셀 단위로 동일한 실루엣을 보장하는 양산본은 아닙니다. B3는 원본의 안쪽 다리를 포함한 다섯 접점을 유지한 최초 편집본을 사용했습니다. 다리 수를 잘못 판단해 제거했던 재편집본은 제외하고 기록만 보존했습니다. I4는 원래 연결체를 유지하며 독립된 남매 디자인으로 새로 분리하지 않았습니다.</p><img class="board" src="unified-board.png" alt="화풍 조정 7종"><h2>원본 배경 위 조합</h2><p>논리 화면 960×540의 디자인 합성 미리보기이며 실제 게임 캡처가 아닙니다. 배경의 밝기·색·선명도는 원본과 같고 검정 근경의 가림을 유지했습니다. 플레이어 원본 192×160 안 실제 그림은 66×64입니다. 크기는 비교용입니다. 보스는 높이128px 상한, 엘리트는112px 상한으로 검토했으며 최종 크기를 확정하지 않습니다.</p>'''+groups_html+'''<details><summary>모든 개체의 실제 크기 줄맞춤 · 위 이전 / 아래 조정</summary><div class="native"><img src="previews/before-native-lineup.png" width="1200" height="250"><img src="previews/after-native-lineup.png" width="1200" height="250"></div></details><details><summary>시인성 · 어두운 벽 / 푸른 원경 / 검정 근경</summary><p>행 순서: H3, B3, M3, 신호투척수, K4, A3, I4. 위 표 이전 / 아래 표 조정 시안. 동일한 원본 배경 조각과 동일 크기 상한으로 비교합니다.</p><div class="native"><img src="previews/before-visibility.png" width="960" height="1512"><img src="previews/after-visibility.png" width="960" height="1512"></div></details>'''+''.join(sections)+'''<p>원본과 모든 생성·편집 시도, 참조·프롬프트를 보존했습니다. 애니메이션·스프라이트 시트·Unity 적용은 제작하지 않았습니다.</p><p><a href="style-unified-20261031.zip">원본·수정본·참조·프롬프트 ZIP</a> · <a href="metadata.json">크기와 실루엣 비교 정보</a> · <a href="validation.json">합성 검증</a></p></main></html>'''
    page=page.replace('<main>','<main><div style="padding:20px;background:#314452;border:1px solid #d4ab70;border-radius:8px;margin-bottom:24px"><a href="size-editor.html" style="font-size:24px;font-weight:bold">몬스터 크기·위치 직접 조절하기 →</a><p>몬스터별 높이(px), 드래그 배치, 설정 자동 저장·JSON, 합성 PNG 저장을 지원합니다.</p></div>')
    (OUT/'index.html').write_text(page,encoding='utf-8');dump(OUT/'metadata.json',meta)
    from size_editor import build as build_editor
    build_editor()
    archive=OUT/(OUT.name+'.zip');dump(OUT/'validation.json',{})
    missing=[link for link in re.findall(r'(?:href|src)="([^"]+)"',page) if link!=archive.name and not link.startswith('#') and not (OUT/link).is_file()];assert not missing,missing
    assert json.loads((SOURCE/'current-choices.json').read_text(encoding='utf-8'))['adoptedIds']==roles_before
    validation={'editedDesigns':7,'rolesUnchanged':{'melee':3,'ranged':2,'elite':1,'bossConcept':1},'allSelectedOriginalHashesPreserved':True,'backgroundReferenceHashesMatch':True,'playerCanvas':list(player_canvas.size),'playerAlphaBBox':list(pb),'playerVisibleExtent':list(player.size),'composites':checks,'missingLocalLinks':missing,'notes':['Style variants for review, not production pixel cleanup.','Silhouette overlap is a normalized comparison aid, not an approval score.','B3 original has five visible ground-contact feet; first style edit retains all five. A mistaken four-foot corrective edit was rejected and preserved separately.','I4 remains original joined body plan; boss native height cap128 is only a size study.']}
    dump(OUT/'validation.json',validation);dump(refs/'sources.json',[{'copy':str(p.relative_to(OUT)),'sha256':sha(p)} for p in sorted(refs.rglob('*')) if p.is_file() and p.name!='sources.json'])
    (OUT/'source').mkdir(exist_ok=True)
    for name in ['style_unification.py','style_review.py','current-choices.json','README.md']:shutil.copy2(SOURCE/name,OUT/'source'/name)
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=archive:z.write(p,p.relative_to(OUT))
    print(json.dumps({'styleEdits':7,'comparisonScreens':len(checks),'originalsPreserved':True,'missingLinks':missing,'zipBytes':archive.stat().st_size,'overlaps':{m['id']:m['normalizedSilhouetteOverlap'] for m in meta}},ensure_ascii=False))


if __name__=='__main__':build()
