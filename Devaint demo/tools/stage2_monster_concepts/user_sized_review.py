"""Deliver redraws at exact user-specified visible heights, with editable replay."""
import json, shutil, re, zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label, find_objects
from user_sized_redraw import OUT, PRIOR, SOURCE, ROOT, ITEMS, FONT, CONFIG_PATH, config, sha

N=Image.Resampling.NEAREST


def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def at_height(im,height):
    # Point-resample only, then remove empty outer canvas. RGB is never repainted.
    im=im.crop(im.getchannel('A').getbbox())
    for _ in range(3):
        im=im.resize((max(1,round(im.width*height/im.height)),height),N)
        im=im.crop(im.getchannel('A').getbbox())
        if im.height==height:return im
    raise AssertionError(('visible height',height,im.size))


def build():
    cfg=config();config_hash=sha(CONFIG_PATH);requested={m['id']:m for m in cfg['monsters']}
    records=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'));before,after,metadata={},{},[]
    for row in records:
        row['role']=ITEMS[row['id']][1]
    refs=OUT/'references';font=lambda n:ImageFont.truetype(FONT,n)
    for row in records:
        ident=row['id'];p=OUT/ident;raw=Image.open(p/'raw/generated.png').convert('RGBA');arr=np.array(raw)
        labs,_=label(arr[:,:,3]>=245)
        regions=sorted([(int((labs[b]==i).sum()),i,[b[1].start,b[0].start,b[1].stop,b[0].stop]) for i,b in enumerate(find_objects(labs),1)],reverse=True)
        area,component,box=regions[0];assert area>1000
        selected=labs==component;a=np.zeros_like(arr);a[selected,:3]=arr[selected,:3];a[selected,3]=255
        cut=Image.fromarray(a).crop(box);cut.save(p/'cutouts/style-enlarged.png')
        old=Image.open(p/'before/design-cutout.png').convert('RGBA');assert sha(p/'before/design-cutout.png')==row['beforeStyleSha256']==sha(PRIOR/ident/'cutouts/style-enlarged.png')
        h=requested[ident]['height'];before[ident]=at_height(old,h);after[ident]=at_height(cut,h)
        before[ident].save(p/'previews/before-scale-study.png');after[ident].save(p/'cutouts/style-scale-study.png')
        assert after[ident].getchannel('A').getbbox()[3]==h
        m=dict(row,afterReviewExtent=list(after[ident].size),beforeReviewExtent=list(before[ident].size),rawSha256=sha(p/'raw/generated.png'),sourceBBox=box,rawTouchesCanvas=box[0]==0 or box[1]==0 or box[2]==raw.width or box[3]==raw.height,visibleHeightMatchesRequest=True,derivedAlpha='Largest connected core alpha>=245; RGB preserved. Point-resampled derivative has exact requested visible height.')
        metadata.append(m);dump(p/'metadata.json',m)
        pair=Image.new('RGBA',(1200,520),'#eeeee8');d=ImageDraw.Draw(pair);d.text((24,14),f'{ident} {row["name"]} · 목표 높이 {h}px',font=font(24),fill='#263640')
        scale=min(3,500/max(before[ident].width,after[ident].width),365/h)
        for cx,title,spr in [(300,'이전 그림을 지정 크기로 확대',before[ident]),(900,'지정 크기 기준 재생성',after[ident])]:
            shown=spr.resize((round(spr.width*scale),round(spr.height*scale)),N);pair.alpha_composite(shown,(cx-shown.width//2,435-shown.height));d.text((cx-165,475),title,font=font(20),fill='#526570')
        pair.convert('RGB').save(p/'previews/before-after.png')
    dump(OUT/'metadata.json',metadata);(OUT/'previews').mkdir(exist_ok=True)
    player_canvas=Image.open(refs/'assets/approved-player.png').convert('RGBA');pb=player_canvas.getchannel('A').getbbox();player=player_canvas.crop(pb);assert player.size==(66,64)
    for version,sprites in [('before',before),('after',after)]:
        board=Image.new('RGBA',(960,540),'#232c35');d=ImageDraw.Draw(board)
        d.text((20,12),'사용자 지정 크기 · 모든 그림 1배율 / 플레이어 높이64px',font=font(21),fill='#eeeee8')
        for ground,group,title in [(224,['H3','B3','M3','R-selected','K4'],'일반 몬스터'),(494,['A3','I4'],'엘리트 · 남매 보스')]:
            if ground==494:d.text((20,277),title,font=font(19),fill='#b9c6cc')
            x=24;board.alpha_composite(player,(x,ground-64));d.text((x,ground+10),'플레이어 64',font=font(14),fill='#eeeee8');x+=90
            for ident in group:
                spr=sprites[ident];board.alpha_composite(spr,(x,ground-spr.height));label_name='R' if ident=='R-selected' else ident
                d.text((x,ground+10),f'{label_name} {spr.height}px',font=font(14),fill='#efc98d');x+=spr.width+24
            assert x<=960,(version,x)
        board.convert('RGB').save(OUT/'previews'/f'{version}-native-roster.png')
    layout=json.loads((refs/'layout.json').read_text(encoding='utf-8'));scenes={s['id']:s for s in layout['scenes']};front=Image.open(refs/'assets/foreground.png').convert('RGBA')
    def screen(path,scene,placements):
        bg=Image.open(refs/f'{scene}-game.png').convert('RGBA');layer=Image.new('RGBA',bg.size);fg=layer.copy();nx=scenes[scene]['view'][0];fg.alpha_composite(front,(490-round((nx-480)*.4375),-100))
        for spr,x,foot in placements:layer.alpha_composite(spr,(x,foot-spr.height))
        a=np.array(layer);fa=np.array(fg.getchannel('A'),dtype=np.uint16);a[:,:,3]=(a[:,:,3].astype(np.uint16)*(255-fa)//255).astype(np.uint8);mask=a[:,:,3]>0
        result=bg.copy();result.alpha_composite(Image.fromarray(a));original,out=np.array(bg),np.array(result)
        assert np.array_equal(original[~mask],out[~mask]);assert np.array_equal(original[220:288,130:198],out[220:288,130:198]);result.convert('RGB').save(path)
        return {'scene':scene,'size':[960,540],'backgroundAndPlayerOutsideInsertionUnchanged':True,'foregroundOcclusionPreserved':True}
    checks={};sections=[]
    patches=[Image.open(refs/'assets/wall.png').convert('RGBA').crop((96,128,416,344)),Image.open(refs/'assets/far.png').convert('RGBA').crop((800,300,1120,516)),Image.open(refs/'D-game.png').convert('RGBA').crop((620,0,940,216))]
    for version,sprites in [('before',before),('after',after)]:
        visibility=Image.new('RGBA',(960,1512))
        for i,(ident,spr) in enumerate(sprites.items()):
            for j,bg in enumerate(patches):
                cell=bg.copy();cell.alpha_composite(spr,((320-spr.width)//2,208-spr.height));visibility.alpha_composite(cell,(j*320,i*216))
            scene={'H3':'A','B3':'B','M3':'C','R-selected':'C','K4':'D','A3':'D','I4':'E'}[ident]
            checks[f'{ident}-{version}']=screen(OUT/ident/'previews'/f'{version}-960x540.png',scene,[(spr,326,270 if ident=='H3' else 288)])
        visibility.convert('RGB').save(OUT/'previews'/f'{version}-visibility.png')
    checks['saved-user-layout']=screen(OUT/'previews/saved-layout-960x540.png',cfg['scene'],[(after[m['id']],m['x'],m['foot']) for m in cfg['monsters'] if m['visible']])
    overview=Image.new('RGB',(1920,2320),'#18212a');d=ImageDraw.Draw(overview)
    for i,ident in enumerate(list(ITEMS)+['saved']):
        x,y=(i%2)*960,(i//2)*580
        title='저장한 D 장면 · 표시·위치 그대로' if ident=='saved' else f'{ident} {ITEMS[ident][0]} · 높이 {requested[ident]["height"]}px · 비교용 바닥 정렬'
        image_path=OUT/'previews/saved-layout-960x540.png' if ident=='saved' else OUT/ident/'previews/after-960x540.png'
        d.text((x+14,y+8),title,font=font(19),fill='#e7e9e3');overview.paste(Image.open(image_path),(x,y+40))
    overview.save(OUT/'previews/map-overview.png')
    for m in metadata:
        ident=m['id'];sections.append(f'''<section id="{ident}"><h2>{ident} {m['name']} · {m['role']} · {m['afterReviewExtent'][0]}×{m['requestedVisibleHeight']}px</h2><img class="board" src="{ident}/previews/before-after.png" alt="동일한 사용자 지정 높이의 수정 전후"><div class="native"><img src="{ident}/previews/after-960x540.png" width="960" height="540" alt="960x540 배경 합성"></div><p><a href="{ident}/cutouts/style-scale-study.png">지정 크기 투명 PNG</a> · <a href="{ident}/raw/generated.png">생성 원본</a> · <a href="{ident}/prompt.txt">프롬프트</a></p></section>''')
    rows=''.join(f'<tr><td>{m["id"]} {m["name"]}</td><td>{m["role"]}</td><td>{m["requestedVisibleHeight"]}px</td><td>{m["afterReviewExtent"][0]}×{m["afterReviewExtent"][1]}px</td></tr>' for m in metadata)
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>사용자 지정 크기 · 몬스터 7종 재생성</title><style>body{margin:0;background:#17212a;color:#e7e9e3;font:17px/1.75 "Malgun Gothic",sans-serif}main{max-width:1400px;margin:auto;padding:24px}a{color:#addbea}.board{display:block;width:100%;height:auto}.native{overflow-x:auto}.native img{display:block;max-width:none;image-rendering:pixelated}section{padding:25px 0;border-top:1px solid #52606a}td,th{text-align:left;padding:10px 18px;border-bottom:1px solid #52606a}h1{font-size:28px}h2{font-size:22px}summary{cursor:pointer;padding:12px 0}.cta{display:inline-block;padding:12px 20px;border:1px solid #e7b671;border-radius:7px;font-weight:bold}</style><main><h1>사용자가 맞춘 크기 기준 · 7종 재생성 시안</h1><p><a class="cta" href="size-editor.html">이 크기로 다시 조절·배치하기 →</a></p><p>전달한 stage2-monster-sizes.json의 전체 그림 높이를 그대로 적용했습니다. 근거리 3·원거리 2·엘리트 1·남매 보스 1 구성을 유지하고, 각 크기에 맞는 픽셀 묘사를 기본 image_gen으로 다시 편집했습니다. 투척물과 긴 수의, 무기도 전체 높이에 포함됩니다.</p><h2>모두 같은 1배율 · 플레이어 높이64px</h2><p>아래는 개체별 확대를 하지 않은 실제 논리 픽셀 크기 줄맞춤입니다. 좁은 창에서는 가로로 스크롤합니다.</p><div class="native"><img src="previews/after-native-roster.png" width="960" height="540" alt="실제 픽셀 크기 전체 줄맞춤"></div><table><thead><tr><th>개체</th><th>역할</th><th>요청 높이</th><th>재생성 PNG 유효 크기</th></tr></thead><tbody>'''+rows+'''</tbody></table><h2>저장한 장면</h2><p>JSON의 D 배경·A3 선택·표시 여부·좌표를 그대로 재현했습니다. 다른 개체가 숨김이어도 그 높이는 모두 사용했습니다. 개별 비교 화면은 바닥을 맞춘 별도 배치입니다.</p><div class="native"><img src="previews/saved-layout-960x540.png" width="960" height="540"></div><details><summary>같은 높이로 확대한 이전 그림과 비교</summary><div class="native"><img src="previews/before-native-roster.png" width="960" height="540"><img src="previews/after-native-roster.png" width="960" height="540"></div></details><details><summary>어두운 벽·푸른 원경·검정 근경 시인성</summary><p>행 순서 H3·B3·M3·신호투척수·K4·A3·I4, 열 순서 벽·원경·검정 근경입니다.</p><div class="native"><img src="previews/after-visibility.png" width="960" height="1512"></div></details>'''+''.join(sections)+'''<p>디자인 재생성 시안과 합성 미리보기입니다. 배경과 플레이어는 바꾸지 않았고 근경의 가림을 유지했습니다. 원본·이전 그림·사용자 JSON·프롬프트를 보존했습니다. 이번 높이는 사용자 크기 검토값이며 전투 수치·양산 픽셀·애니메이션·Unity 적용을 확정하지 않습니다.</p><p><a href="user-sized-redraw-20261031.zip">원본·지정 크기 PNG·참조·프롬프트 ZIP</a> · <a href="references/user-sizes.json">입력 JSON</a> · <a href="validation.json">검증 정보</a></p></main></html>'''
    page=page.replace('<h1>사용자가 맞춘 크기 기준 · 7종 재생성 시안</h1>', '<h1>사용자가 맞춘 크기 기준 · 7종 재생성 시안</h1><p><a href="combat-plan.html">현재 역할·공격 패턴·전조 기획 →</a> · 근거리 K4·B3·M3 / 원거리 H3·신호투척수</p>')
    (OUT/'index.html').write_text(page,encoding='utf-8')
    from build_combat_plan import build as combat_plan
    combat_plan()
    from size_editor import build as editor
    editor(output=OUT,items=ITEMS,initial=cfg,namespace='user-sized-redraw-20261031')
    dump(OUT/'validation.json',{});archive=OUT/(OUT.name+'.zip')
    missing=[link for link in re.findall(r'(?:href|src)="([^"]+)"',page) if link!=archive.name and not link.startswith('#') and not (OUT/link).is_file()];assert not missing,missing
    assert sha(CONFIG_PATH)==config_hash==sha(OUT/'references/user-sizes.json')
    for name in ['A-game.png','B-game.png','C-game.png','D-game.png','E-game.png','assets/approved-player.png','assets/foreground.png']:
        assert sha(refs/name)==sha(PRIOR/'references'/name)
    dump(OUT/'validation.json',{'editedDesigns':7,'allRequestedVisibleHeightsMatch':{m['id']:m['afterReviewExtent'][1] for m in metadata},'inputConfigSha256':config_hash,'inputConfigUnchanged':True,'previousStylesPreserved':True,'backgroundReferencesUnchanged':True,'playerVisibleExtent':list(player.size),'savedSceneVisibilityAndCoordinatesReplayed':True,'composites':checks,'missingLocalLinks':missing,'status':'Design redraw/size study; no production or runtime integration.'})
    (OUT/'source').mkdir(exist_ok=True)
    for name in ['user_sized_redraw.py','user_sized_review.py','user-size-reference.json','current-choices.json','README.md']:shutil.copy2(SOURCE/name,OUT/'source'/name)
    shutil.copy2(SOURCE/'current-choices.json',OUT/'choices.json')
    shutil.copy2(SOURCE/'build_combat_plan.py',OUT/'source/build_combat_plan.py')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=archive:z.write(p,p.relative_to(OUT))
    print(json.dumps({'exactHeights':{m['id']:m['afterReviewExtent'][1] for m in metadata},'screens':len(checks),'missingLinks':missing,'zipBytes':archive.stat().st_size},ensure_ascii=False))


if __name__=='__main__':build()
