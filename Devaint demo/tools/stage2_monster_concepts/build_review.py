"""Static review compositing only: no art generation, motion, sprite sheets or Unity output."""
from pathlib import Path
import json, hashlib, html, zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label, find_objects
from catalog import SETS

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/waterway/monster-concepts-v3-20261031'
REF=OUT/'references'
N=Image.Resampling.NEAREST
FONT='C:/Windows/Fonts/malgun.ttf'
BG=(238,238,234,255)

def write_json(path,obj):
 path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def clean_extract(k):
 raw=Image.open(OUT/k/'raw/generated.png').convert('RGBA')
 arr=np.array(raw)
 # Discard near-transparent empty-space RGB; do not repaint the generated material colors.
 labs,n=label(arr[:,:,3]>=180)
 boxes=find_objects(labs)
 regions=[]
 for index,box in enumerate(boxes,1):
  area=int((labs[box]==index).sum())
  if area>1000:regions.append({'label':index,'area':area,'bbox':[box[1].start,box[0].start,box[1].stop,box[0].stop]})
 assert len(regions)==7,(k,len(regions))
 top=sorted([r for r in regions if r['bbox'][1]<450],key=lambda r:r['bbox'][0])
 bottom=sorted([r for r in regions if r['bbox'][1]>=450],key=lambda r:r['bbox'][0])
 assert (len(top),len(bottom))==(3,4)
 clean=np.array(raw);clean[:,:,3]=0
 sprites=[];meta=[]
 for i,r in enumerate(top+bottom):
  x0,y0,x1,y1=r['bbox'];sel=labs==r['label']
  piece=np.zeros_like(arr);piece[sel,:3]=arr[sel,:3];piece[sel,3]=255
  clean[sel]=piece[sel]
  cut=Image.fromarray(piece).crop((x0,y0,x1,y1))
  cut.save(OUT/k/'cutouts'/f'{k}{i+1}-enlarged.png')
  m=SETS[k]['monsters'][i]
  factor=min(m[4]/cut.width,m[5]/cut.height)
  size=(max(1,round(cut.width*factor)),max(1,round(cut.height*factor)))
  native=cut.resize(size,N)
  native.save(OUT/k/'cutouts'/f'{k}{i+1}-scale-study.png')
  sprites.append(native)
  meta.append({'id':f'{k}{i+1}','name':m[0],'role':m[1],'sourceBBox':r['bbox'],'sourceOpaquePixels':r['area'],'proposedEnvelope':[m[4],m[5]],'actualReviewExtent':list(native.size),'heightVsPlayer':round(native.height/64,3),'note':'Single static cutout for design compositing, not production sprite data.'})
 flat=Image.new('RGBA',raw.size,BG);flat.alpha_composite(Image.fromarray(clean));flat.convert('RGB').save(OUT/k/'design-board.png')
 Image.fromarray(clean).save(OUT/k/'board-transparent.png')
 return sprites,meta

L=json.loads((REF/'layout.json').read_text(encoding='utf-8'))
SCENES={s['id']:s for s in L['scenes']}
front=Image.open(REF/'assets/foreground.png').convert('RGBA')
player=Image.open(REF/'assets/approved-player.png').convert('RGBA')
player=player.crop(player.getchannel('A').getbbox())

def screen(k,sprites,scene,placements,filename):
 base=Image.open(REF/f'{scene}-game.png').convert('RGBA')
 assert base.size==(960,540)
 fg=Image.new('RGBA',base.size)
 nx=SCENES[scene]['view'][0]
 fg.alpha_composite(front,(490-round((nx-480)*.4375),-100))
 layer=Image.new('RGBA',base.size)
 records=[]
 for index,x,ground in placements:
  spr=sprites[index];y=ground-spr.height
  layer.alpha_composite(spr,(x,y))
  records.append({'id':f'{k}{index+1}','xy':[x,y],'groundY':ground,'artExtent':list(spr.size)})
 # The original foreground remains ahead of inserted monsters, while all original pixels
 # outside the effective insertion mask remain byte-identical to the supplied preview.
 la=np.array(layer);fa=np.array(fg.getchannel('A'),dtype=np.uint16)
 la[:,:,3]=(la[:,:,3].astype(np.uint16)*(255-fa)//255).astype(np.uint8)
 effective=la[:,:,3]>0
 output=base.copy();output.alpha_composite(Image.fromarray(la))
 before=np.array(base);after=np.array(output)
 assert np.array_equal(before[~effective],after[~effective])
 # Original player occupies this region in all five provided assembled previews.
 assert np.array_equal(before[220:288,130:198],after[220:288,130:198])
 output.convert('RGB').save(OUT/k/'previews'/filename)
 return {'scene':scene,'size':[960,540],'placements':records,'unchangedPixelsOutsideInsertion':True,'approvedPlayerRegionUnchanged':True,'originalForegroundOcclusionPreserved':True}

def scale_lineup(k,sprites):
 out=Image.new('RGBA',(960,270),(35,40,48,255))
 d=ImageDraw.Draw(out);font=ImageFont.truetype(FONT,15)
 ground=222;d.line((20,ground,940,ground),fill=(90,99,107),width=1)
 out.alpha_composite(player,(24,ground-player.height))
 d.text((26,235),'승인 플레이어',font=font,fill=(225,226,220))
 x=148
 records=[]
 for i,spr in enumerate(sprites):
  out.alpha_composite(spr,(x,ground-spr.height))
  d.text((x,235),f'{k}{i+1}',font=font,fill=(225,226,220))
  records.append({'id':f'{k}{i+1}','xy':[x,ground-spr.height],'size':list(spr.size)})
  x+=spr.width+16
 assert x<960,(k,x)
 out.convert('RGB').save(OUT/k/'previews/scale-lineup.png')
 return records

def visibility(k,sprites):
 wall=Image.open(REF/'assets/wall.png').convert('RGBA').crop((96,128,416,344))
 far=Image.open(REF/'assets/far.png').convert('RGBA').crop((800,300,1120,516))
 black=Image.open(REF/'C-game.png').convert('RGBA').crop((620,0,940,216))
 out=Image.new('RGBA',(960,216*7))
 for i,spr in enumerate(sprites):
  for j,bg in enumerate([wall,far,black]):
   cell=bg.copy();cell.alpha_composite(spr,((320-spr.width)//2,208-spr.height))
   out.alpha_composite(cell,(j*320,i*216))
 out.convert('RGB').save(OUT/k/'previews/visibility-all.png')

def comparison(k):
 font=ImageFont.truetype(FONT,18)
 out=Image.new('RGB',(960,1160),(24,31,38));d=ImageDraw.Draw(out)
 d.text((16,8),f'{k} · 대표 일반 / 원거리 / 엘리트 · 960×540 합성 미리보기',font=font,fill=(229,231,225))
 out.paste(Image.open(OUT/k/'previews/encounter-960x540.png'),(0,40))
 d.text((16,588),f'{k} · 보스와 승인 플레이어 · 960×540 합성 미리보기',font=font,fill=(229,231,225))
 out.paste(Image.open(OUT/k/'previews/boss-960x540.png'),(0,620))
 out.save(OUT/k/'previews/comparison.png')

def gallery(metadata):
 blocks=[]; descriptions=[]
 for k,s in SETS.items():
  lines=[f'{m[0]} / {m[1]} / {m[2]} / {m[3]}' for m in s['monsters']]
  descriptions += [f'\n## {k} · {s["title"]}\n']+lines
  lis=''.join(f'<li><b>{k}{i+1}</b> {html.escape(line)}</li>' for i,line in enumerate(lines))
  dims=' · '.join(f'{m["id"]} {m["actualReviewExtent"][0]}×{m["actualReviewExtent"][1]}' for m in metadata[k]['monsters'])
  blocks.append(f'''<section id="{k}"><h2>{k} · {s['title']}</h2><p>{s['direction']} — 검토용 소재 비중</p>
<figure><a href="{k}/design-board.png"><img class="board" src="{k}/design-board.png" alt="{k} 7종 컬러 디자인 보드"></a><figcaption>왼쪽부터: 위 근거리 3종 / 아래 원거리 2종 · 엘리트 · 보스. 기본 좌향 측면.</figcaption></figure>
<ol class="descriptions">{lis}</ol>
<h3>플레이어와 전 종 크기 비교</h3><div class="native"><img src="{k}/previews/scale-lineup.png" width="960" height="270" alt="실제 검토 크기 줄맞춤"></div><p class="small">{dims} px · 그림 영역 기준, 확정 스프라이트 규격이나 판정 크기가 아닙니다.</p>
<h3>대표 개체 배치 · 논리 화면 960×540</h3><div class="native"><img src="{k}/previews/encounter-960x540.png" width="960" height="540" alt="대표 일반 원거리 엘리트 합성 미리보기"></div>
<p class="small">일반 {metadata[k]['encounter']['placements'][0]['id']} · 원거리 {k}4 · 엘리트 {k}6 / 기존 {metadata[k]['encounter']['scene']} 구역 조립 미리보기 위 합성.</p>
<h3>보스 별도 비교 · 논리 화면 960×540</h3><div class="native"><img src="{k}/previews/boss-960x540.png" width="960" height="540" alt="보스 크기 합성 미리보기"></div>
<details><summary>전 종 시인성 비교: 어두운 벽 / 푸른 원경 / 검정 근경</summary><p>위에서부터 {k}1–{k}7. 원본 배경의 동일 크기 영역을 그대로 잘라 사용했습니다. 각 행은 같은 개체·같은 크기입니다.</p><div class="native"><img src="{k}/previews/visibility-all.png" width="960" height="1512" alt="3가지 배경 위 전 종 시인성"></div></details>
<p><a href="{k}/prompt.txt">생성 프롬프트</a> · <a href="{k}/raw/generated.png">생성 원본</a> · <a href="{k}/board-transparent.png">투명 정리본</a></p></section>''')
 page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>2Stage 지하수로 몬스터 디자인 비교 v3</title><style>
*{box-sizing:border-box}body{margin:0;background:#17212a;color:#e6e8e3;font-family:"Malgun Gothic",sans-serif;line-height:1.65}main{max-width:1600px;margin:auto;padding:28px}h1{font-size:28px;line-height:1.4}h2{font-size:26px;margin:0}h3{font-size:18px;margin-top:30px}a{color:#a9d7e7}nav{position:sticky;top:0;padding:12px 24px;background:#17212aee;display:flex;gap:25px;z-index:2;border-bottom:1px solid #52606a}section{padding:32px 0 48px;border-top:1px solid #52606a;scroll-margin-top:65px}figure{margin:24px 0 10px}.board{display:block;width:100%;max-width:1536px;height:auto}figcaption,.small{color:#b9c6c9;font-size:14px}.descriptions{padding-left:0;list-style:none}.descriptions li{padding:7px 0;border-bottom:1px solid #34434d}.descriptions b{display:inline-block;min-width:36px;color:#e8bd80}.native{overflow-x:auto;max-width:100%;background:#111b23}.native img{display:block;max-width:none;image-rendering:pixelated}summary{cursor:pointer;padding:12px 0}details{margin-top:24px}.intro{max-width:1000px}p{margin:12px 0 18px}
</style><nav><a href="#A">A 떠내려온 살림</a><a href="#B">B 배수 틈새 정원</a><a href="#C">C 검문선의 틈새 생명</a></nav><main><h1>도시로 잠입하는 지하수로 · 몬스터 21종 디자인 시안</h1><div class="intro"><p>디자인 비교용 생성 그림과 합성 미리보기입니다. 실제 게임 캡처가 아닙니다. 세트마다 사물·식물·동물을 섞고, 개체의 소재에 맞는 서로 다른 색을 사용했습니다.</p><p>승인 플레이어 원본은 192×160 캔버스 / 유효 그림 66×64입니다. 실제 크기 검토는 이 66×64와 논리 화면 960×540을 기준으로 합니다. 배경 밝기·채도·선명도는 바꾸지 않았고 기존 검정 근경 가림을 유지했습니다. 아래 960px 미리보기는 화면이 좁으면 가로로 스크롤합니다.</p><p><b>추천: A</b> — 생활 사물의 생물화가 도시 아래 공간과 연결되고, 장화·대야·보따리의 큰 실루엣이 잘 갈립니다. 혼합한다면 A2 터벅장화 + A3 대야소라 + B1 물풀종종 + B4 부들분사초 + C4 우편함쏘개 + C6 봉인실거미 + A7 보따리어멈을 우선 비교하세요. 근거리 3·원거리 2·엘리트 1·보스 1 구성을 유지합니다.</p><p>모든 수치는 시안 비교용입니다. 공격은 외형과 성격만 제안하며, 애니메이션·스프라이트 시트·Unity 적용·전투 시스템 확정은 포함하지 않습니다. 생성 원본은 그대로 보존했고, 합성용 사본만 알파를 정리하고 비율을 유지해 최근접 축소했습니다.</p></div>'''+''.join(blocks)+'''<p><a href="monster-concepts-v3-20261031.zip">전체 결과 ZIP</a> · <a href="descriptions.md">21종 한 줄 설명</a> · <a href="scale-reference.json">크기 기준</a> · <a href="validation.json">합성 검증 자료</a></p></main></html>'''
 (OUT/'index.html').write_text(page,encoding='utf-8')
 (OUT/'descriptions.md').write_text('# 디자인 시안 · 미채택\n\n이름 / 역할 / 외형의 핵심 / 공격 성격\n'+'\n\n'.join(descriptions),encoding='utf-8')

def main():
 metadata={};validation={'tool':'built-in image_gen','status':'Unapproved static design study and composited previews, not game captures','sets':{}}
 normal_scene={'A':'A','B':'B','C':'E'};boss_scene={'A':'C','B':'D','C':'C'}
 for k in SETS:
  sprites,meta=clean_extract(k)
  norm=2 if k=='C' else 1
  encounter=screen(k,sprites,normal_scene[k],[(norm,222,352),(3,338,288),(5,420,288)],'encounter-960x540.png')
  boss=screen(k,sprites,boss_scene[k],[(6,322,288)],'boss-960x540.png')
  lineup=scale_lineup(k,sprites);visibility(k,sprites);comparison(k)
  metadata[k]={'monsters':meta,'encounter':encounter,'boss':boss,'scaleLineup':lineup}
  validation['sets'][k]={'componentCount':len(sprites),'transparentSourcePreserved':True,'rawSha256':sha(OUT/k/'raw/generated.png'),'encounter':encounter,'boss':boss,'allSevenReviewedAgainstThreeUnmodifiedBackgroundCrops':True,'alphaMethod':'Connected components on source alpha >=180; preserve source RGB and set selected alpha to 255; no generated art redrawing; aspect-preserving nearest-neighbor scale study.'}
  write_json(OUT/k/'design-metadata.json',metadata[k])
 original=ROOT/'outputs/waterway/parallax-kit-v3-20261031/assets/approved-player.png'
 validation['approvedPlayer']={'sourceSha256':sha(original),'referenceCopySha256':sha(REF/'assets/approved-player.png'),'unchanged':sha(original)==sha(REF/'assets/approved-player.png'),'canvas':[192,160],'alphaBBox':[53,73,119,137],'visibleExtent':[66,64]}
 validation['limits']=['Static generated concepts, not final production pixel art; native pixel grid still needs cleanup after selection.','No animations, sprite sheets, Unity import or actual gameplay verification.','Side-oriented designs may retain slight depth in a few object surfaces.','Proposed art extents and attack character do not finalize collision, damage or combat systems.']
 write_json(OUT/'validation.json',validation);gallery(metadata)
 # Preserve the checkout's source files alongside the imagery and prompts.
 srcout=OUT/'source';srcout.mkdir(exist_ok=True)
 for name in ['prepare.py','catalog.py','build_review.py','README.md']:
  src=Path(__file__).parent/name
  if src.exists(): (srcout/name).write_bytes(src.read_bytes())
 archive=OUT/'monster-concepts-v3-20261031.zip'
 with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED) as z:
  for path in sorted(OUT.rglob('*')):
   if path.is_file() and path!=archive and '__pycache__' not in path.parts:z.write(path,path.relative_to(OUT))
 print(json.dumps({'setCount':3,'monsterCount':sum(len(v['monsters']) for v in metadata.values()),'screenCount':6,'playerUnchanged':validation['approvedPlayer']['unchanged'],'archiveBytes':archive.stat().st_size},ensure_ascii=False))

if __name__=='__main__': main()
