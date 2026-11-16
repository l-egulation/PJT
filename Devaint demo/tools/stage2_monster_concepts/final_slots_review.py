"""Compare final-slot candidates with unchanged adopted art and 960x540 backgrounds."""
from pathlib import Path
import hashlib, html, json, re, shutil, zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label, find_objects
from final_slots_catalog import ROOT, OUT, ADOPTED, SETS, FONT

SOURCE = Path(__file__).parent
N = Image.Resampling.NEAREST


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def dump(p, data):
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def build():
    refs = OUT/'references'
    choices_file = SOURCE/'current-choices.json'
    choices_hash = sha(choices_file)
    # This historical exploration keeps its original five-design snapshot.
    choices = json.loads((OUT/'choices.json').read_text(encoding='utf-8'))
    assert choices['adoptedIds'] == ['H3','B3','R-selected','A3','I4']
    for ident in choices['adoptedIds']:
        for name in ['original-board.png','design-cutout.png']:
            assert sha(OUT/'adopted'/ident/name) == sha(ADOPTED/'selected'/ident/name)
    bg_source = ROOT.parent/'stage2-waterway-reference/outputs/waterway/parallax-kit-v3-20261031'
    for p in refs.rglob('*'):
        if p.is_file() and p.name != 'sources.json':
            assert sha(p) == sha(bg_source/p.relative_to(refs))
    player_canvas = Image.open(refs/'assets/approved-player.png').convert('RGBA')
    player_box = player_canvas.getchannel('A').getbbox()
    player = player_canvas.crop(player_box)
    assert player.size == (66,64)
    retained = {ident:Image.open(OUT/'adopted'/ident/'earlier-scale-study.png').convert('RGBA')
                for ident in ['H3','B3','R-selected']}
    layout = json.loads((refs/'layout.json').read_text(encoding='utf-8'))
    scenes = {s['id']:s for s in layout['scenes']}
    front = Image.open(refs/'assets/foreground.png').convert('RGBA')
    font = ImageFont.truetype(FONT,20)
    small = ImageFont.truetype(FONT,16)

    def screen(path, scene, placements):
        bg = Image.open(refs/f'{scene}-game.png').convert('RGBA')
        layer = Image.new('RGBA',bg.size); fg = layer.copy()
        nx = scenes[scene]['view'][0]
        fg.alpha_composite(front,(490-round((nx-480)*.4375),-100))
        for spr,x,foot in placements:
            layer.alpha_composite(spr,(x,foot-spr.height))
        a = np.array(layer); fa = np.array(fg.getchannel('A'),dtype=np.uint16)
        a[:,:,3] = (a[:,:,3].astype(np.uint16)*(255-fa)//255).astype(np.uint8)
        mask = a[:,:,3]>0
        out = bg.copy(); out.alpha_composite(Image.fromarray(a))
        before, after = np.array(bg),np.array(out)
        assert np.array_equal(before[~mask],after[~mask])
        assert np.array_equal(before[220:288,130:198],after[220:288,130:198])
        out.convert('RGB').save(path)
        return {'scene':scene,'logicalSize':[960,540],'playerAndBackgroundOutsideInsertionsUnchanged':True,'originalForegroundOcclusionPreserved':True}

    def lineup(path, items):
        canvas = Image.new('RGBA',(960,230),'#232c35'); draw = ImageDraw.Draw(canvas)
        x,ground = 24,184
        for text,spr in [('승인 플레이어',player)]+items:
            canvas.alpha_composite(spr,(x,ground-spr.height))
            draw.text((x,200),text,font=small,fill='#eeeee8')
            x += max(spr.width,130)+24
        assert x<960,x
        canvas.convert('RGB').save(path)

    validation = {'newCandidates':12,'newMelee':6,'newRanged':6,'allFiveAdoptedOriginalsAndCutoutsUnchanged':True,
                  'backgroundReferenceHashesMatch':True,'playerCanvas':list(player_canvas.size),
                  'playerAlphaBBox':list(player_box),'playerVisibleExtent':list(player.size),'sets':{}}
    metadata, sections, descriptions = {}, [], []
    for k,items in SETS.items():
        folder = OUT/k; raw = Image.open(folder/'raw/generated.png').convert('RGBA')
        arr = np.array(raw); labs,_ = label(arr[:,:,3]>=180)
        regions = [(ident,[b[1].start,b[0].start,b[1].stop,b[0].stop])
                   for ident,b in enumerate(find_objects(labs),1) if (labs[b]==ident).sum()>1000]
        assert len(regions)==4,(k,len(regions))
        top = sorted([r for r in regions if (r[1][1]+r[1][3])/2<raw.height/2],key=lambda r:r[1][0])
        bottom = sorted([r for r in regions if (r[1][1]+r[1][3])/2>=raw.height/2],key=lambda r:r[1][0])
        assert len(top)==len(bottom)==2
        sprites,cuts,meta = [],[],[]
        for i,(ident,box) in enumerate(top+bottom):
            assert 0<box[0]<box[2]<raw.width and 0<box[1]<box[3]<raw.height
            selected = labs==ident
            a = np.zeros_like(arr); a[selected,:3]=arr[selected,:3]; a[selected,3]=255
            cut = Image.fromarray(a).crop(box); cut.save(folder/'cutouts'/f'{k}{i+1}-enlarged.png')
            m = items[i]; factor = min(m[4]/cut.width,m[5]/cut.height)
            spr = cut.resize((round(cut.width*factor),round(cut.height*factor)),N)
            spr.save(folder/'cutouts'/f'{k}{i+1}-scale-study.png')
            sprites.append(spr);cuts.append(cut)
            meta.append({'id':f'{k}{i+1}','name':m[0],'role':m[1],'status':'unselected-candidate','sourceBBox':box,'reviewExtent':list(spr.size),'derivedAlpha':'Connected silhouette alpha>=180; RGB unchanged. Raw preserved.'})
        color = Image.new('RGBA',(1600,1040),'#eeeee8'); silhouette = color.copy()
        cd,sd = ImageDraw.Draw(color),ImageDraw.Draw(silhouette)
        for i,(cut,spr,(cx,ground)) in enumerate(zip(cuts,sprites,[(400,470),(1200,470),(400,990),(1200,990)])):
            shown = cut.resize((spr.width*5,spr.height*5),N)
            pos = (cx-shown.width//2,ground-shown.height)
            color.alpha_composite(shown,pos)
            flat = Image.new('RGBA',shown.size,'#202830');flat.putalpha(shown.getchannel('A'))
            silhouette.alpha_composite(flat,pos)
            text = f'{k}{i+1} · {items[i][1]}'
            for d in [cd,sd]:d.text((cx-360,24 if i<2 else 544),text,font=font,fill='#4f626d')
        color.convert('RGB').save(folder/'design-board.png')
        silhouette.convert('RGB').save(folder/'previews/silhouette-board.png')
        lineup(folder/'previews/melee-lineup.png',[(f'{k}1',sprites[0]),(f'{k}2',sprites[1]),('H3 채택 근거리',retained['H3']),('B3 큰 근거리',retained['B3'])])
        lineup(folder/'previews/ranged-lineup.png',[(f'{k}3',sprites[2]),(f'{k}4',sprites[3]),('채택 투척수',retained['R-selected'])])
        melee = screen(folder/'previews/melee-960x540.png',{'K':'A','L':'D','M':'E'}[k],[(retained['H3'],224,334),(sprites[0],326,288),(sprites[1],440,288)])
        ranged = screen(folder/'previews/ranged-960x540.png','C',[(retained['R-selected'],230,352),(sprites[2],326,270 if k=='L' else 288),(sprites[3],440,288)])
        comparison = Image.new('RGB',(960,1160),'#18212a');draw=ImageDraw.Draw(comparison)
        for y,name,text in [(0,'melee','신규 근거리 2종 + H3 채택 근거리'),(580,'ranged','신규 원거리 2종 + 채택 신호투척수')]:
            draw.text((16,y+8),f'{k} · {text} · 960×540 합성 미리보기',font=small,fill='#e7e9e3')
            comparison.paste(Image.open(folder/'previews'/f'{name}-960x540.png'),(0,y+40))
        comparison.save(folder/'previews/comparison.png')
        patches = [Image.open(refs/'assets/wall.png').convert('RGBA').crop((96,128,416,344)),Image.open(refs/'assets/far.png').convert('RGBA').crop((800,300,1120,516)),Image.open(refs/'C-game.png').convert('RGBA').crop((620,0,940,216))]
        vis = Image.new('RGBA',(960,864))
        for i,spr in enumerate(sprites):
            for j,bg in enumerate(patches):
                cell=bg.copy();cell.alpha_composite(spr,((320-spr.width)//2,208-spr.height));vis.alpha_composite(cell,(j*320,i*216))
        vis.convert('RGB').save(folder/'previews/visibility.png')
        validation['sets'][k]={'completeSilhouettes':4,'rawSha256':sha(folder/'raw/generated.png'),'meleePreview':melee,'rangedPreview':ranged}
        metadata[k]=meta;dump(folder/'metadata.json',meta)
        lines=[f'{m[0]} / {m[1]} / {m[2]} / {m[3]}' for m in items]
        descriptions.extend(f'{k}{i+1} {line}' for i,line in enumerate(lines))
        lis=''.join(f'<li><b>{k}{i+1}</b> {html.escape(line)}</li>' for i,line in enumerate(lines))
        sections.append(f'''<section id="{k}"><h2>{k} · 근거리 2종 / 원거리 2종</h2><img class="board" src="{k}/design-board.png" alt="{k} 신규 후보"><p>윗줄 근거리, 아랫줄 원거리. 검토 크기의 5배 확대입니다. 모두 미채택 후보입니다.</p><ul>{lis}</ul><details><summary>색을 뺀 실루엣 비교</summary><img class="board" src="{k}/previews/silhouette-board.png" alt="{k} 단색 실루엣"></details><h3>근거리 · 크기 비교와 960×540 합성</h3><div class="native"><img src="{k}/previews/melee-lineup.png" width="960" height="230"><img src="{k}/previews/melee-960x540.png" width="960" height="540"></div><h3>원거리 · 크기 비교와 960×540 합성</h3><div class="native"><img src="{k}/previews/ranged-lineup.png" width="960" height="230"><img src="{k}/previews/ranged-960x540.png" width="960" height="540"></div><details><summary>시인성 · 어두운 벽 / 푸른 원경 / 검정 근경</summary><p>위에서부터 {k}1~{k}4. 배경 원본을 그대로 잘랐으며 같은 크기로 배치했습니다.</p><div class="native"><img src="{k}/previews/visibility.png" width="960" height="864"></div></details><p><a href="{k}/raw/generated.png">생성 원본</a> · <a href="{k}/prompt.txt">프롬프트</a></p></section>''')
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>남은 근거리·원거리 후보 12종</title><style>body{margin:0;background:#17212a;color:#e7e9e3;font:17px/1.75 "Malgun Gothic",sans-serif}main{max-width:1600px;margin:auto;padding:28px}nav{position:sticky;top:0;background:#17212af5;padding:12px 28px;border-bottom:1px solid #52606a;z-index:2}a{color:#addbea}nav a{margin-right:28px}.board{display:block;width:100%;height:auto}.native{overflow-x:auto}.native img{display:block;max-width:none;image-rendering:pixelated}section{padding:30px 0;border-top:1px solid #52606a;scroll-margin-top:60px}li{list-style:none;padding:7px 0;border-bottom:1px solid #34434d}ul{padding:0}b{color:#edc68b}summary{cursor:pointer;padding:15px 0}h1{font-size:30px}h2{font-size:26px}p{max-width:1200px}</style><nav><a href="#K">K 후보</a><a href="#L">L 후보</a><a href="#M">M 후보</a><a href="#adopted">현재 채택</a></nav><main><h1>남은 일반 근거리 1종 · 원거리 1종을 위한 후보 12종</h1><p>근거리 6종·원거리 6종을 새로 비교합니다. 채택된 H3 수의형·B3 쐐기형·신호투척수·A3 거구·I4 남매 보스 방향을 유지하고, 다른 몸의 구조와 공격 준비 부위를 탐색했습니다. 새 후보를 자동 채택하지 않습니다.</p><p>관리되지 않은 잉크 도시 지하수로의 오염감을 큰 재질 면의 균열·잉크 연결부로 표현했습니다. 신규 디자인은 테두리 없이 재질색 면과 제한된 명암을 사용합니다. 생성 원본을 보존하며, 합성 사본은 알파 180 이상 연결 실루엣만 사용하고 RGB는 바꾸지 않았습니다.</p><p>디자인 시안과 합성 미리보기입니다. 실제 게임 캡처가 아닙니다. 화면은 960×540, 승인 플레이어는 192×160 캔버스 안 실제 그림 66×64입니다. 배경의 밝기·색·선명도는 바꾸지 않았고 검정 근경의 가림 순서를 유지했습니다. 크기는 비교용이며 최종 제작 규격·공격 수치·게임 시스템을 확정하지 않습니다.</p>'''+''.join(sections)+'''<p><b>우선 비교:</b> 근거리 K2 먹톱고리, 원거리 K3 갈비분사체를 우선 추천합니다. 더 가는 가로축은 L2 횡팔전단체, 부유형 발사부는 L3 바늘부채로 비교하세요. K1·M2의 검은 팔다리와 L2의 가는 팔은 검정 근경에서 일부 약해지는 점도 함께 검토하세요.</p><section id="adopted"><h2>보존한 현재 채택 5건</h2><img class="board" src="adopted-board.png" alt="현재 채택 다섯 디자인"><p>위 보드는 보기 좋은 확대 크기이며 상대 체급 비교가 아닙니다. H3는 근거리, I4는 수로를 지키는 남매 2인조 보스 콘셉트입니다. 두 남매를 분리한 최종 디자인은 아직 제작하지 않았습니다.</p></section><p><a href="final-slots-candidates-20261031.zip">원본·참조·프롬프트 ZIP</a> · <a href="descriptions.md">한 줄 설명</a> · <a href="choices.json">보존 선택 정보</a> · <a href="validation.json">합성 검증</a></p></main></html>'''
    (OUT/'index.html').write_text(page,encoding='utf-8')
    (OUT/'descriptions.md').write_text('\n\n'.join(descriptions)+'\n',encoding='utf-8')
    dump(OUT/'metadata.json',metadata)
    dump(OUT/'validation.json',validation)
    archive=OUT/(OUT.name+'.zip')
    missing=[link for link in re.findall(r'(?:href|src)="([^"]+)"',page) if link!=archive.name and not link.startswith('#') and not (OUT/link).is_file()]
    assert not missing,missing
    assert sha(choices_file)==choices_hash
    validation['sourceSelectionFileUnchangedByBuild']=True;validation['missingLocalLinks']=missing
    dump(OUT/'validation.json',validation)
    dump(refs/'sources.json',[{'copy':str(p.relative_to(OUT)),'sha256':sha(p)} for p in sorted(refs.rglob('*')) if p.is_file() and p.name!='sources.json'])
    (OUT/'source').mkdir(exist_ok=True)
    for name in ['final_slots_catalog.py','final_slots_review.py','current-choices.json','README.md']:
        shutil.copy2(SOURCE/name,OUT/'source'/name)
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=archive:z.write(p,p.relative_to(OUT))
    print(json.dumps({'candidates':12,'adoptedUnchanged':5,'normalScreens':6,'missingLinks':missing,'zipBytes':archive.stat().st_size},ensure_ascii=False))


if __name__=='__main__':
    build()
