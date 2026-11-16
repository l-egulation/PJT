from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,html
HERE=Path(__file__).resolve().parents[1]
PACK=HERE.parent/'stage2-unified-stone-v21'
OUT=HERE/'preview';OUT.mkdir(parents=True,exist_ok=True)
source=(PACK/'source/preview_map.py').read_text(encoding='utf-8')
prefix=source[:source.index("font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)")]
prefix=prefix.replace('ROOT=Path(__file__).resolve().parents[1]','ROOT=PACK')
scope={'__file__':__file__,'PACK':PACK};exec(prefix,scope);scope['OUT']=OUT
layout=scope['layout'];render=scope['render'];data=scope['DATA']
selection=[('S','시작 구간'),('F01','독립형 통과 발판'),('F02','벽 부착·긴 발판'),('F03','장거리 통과 발판'),('F04','하부 착지 구간'),('F05','수직방 발판'),('F06','좁은 석재 발판'),('F09','벽·발판 연결')]
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',23);small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
boards=[Image.new('RGB',(1920,1210),'white'),Image.new('RGB',(1920,1210),'white')]
rows=[]
for i,(rid,title) in enumerate(selection):
 room=next(r for r in layout['rooms'] if r['id']==rid)
 actors=[a for a in layout['actors'] if a['roomId']==rid]
 actor=layout['spawn'] if rid=='S' else actors[2] if rid=='F01' else actors[0]
 # Every character uses a real existing landing location; no invented ground or platform.
 px,py=actor['x'],actor['y'];lands=[s for s in layout['surfaces'] if s['x']<=px<s['x']+s['w'] and abs(s['y']+s['h']-py)<.02]
 assert lands,(rid,px,py)
 cx=px+2;cy=py+4.6875
 im=render(cx,cy,actor=(px,py));assert im.size==(960,540);file=f'{i+1:02d}-{rid}-960x540.png';im.save(OUT/file)
 board=boards[i//4];d=ImageDraw.Draw(board);j=i%4;x=(j%2)*960;y=60+(j//2)*575
 d.text((x+16,y),rid+' · '+title,font=font,fill='#253740');board.paste(im,(x,y+33))
 # Whole-room views preserve the native scale; only the viewer may scale the displayed image.
 w=round(room['w']*32);h=round(room['h']*32)
 full=render(room['x']+room['w']/2,room['y']+room['h']/2,W=w,H=h,actor=(px,py));whole=f'{i+1:02d}-{rid}-whole-room-native.png';full.save(OUT/whole)
 rows.append({'id':rid,'title':title,'screen':file,'wholeRoom':whole,'camera':[cx,cy],'heroFeet':[px,py],'size':[960,540],'roomNativeSize':[w,h]})
for i,board in enumerate(boards):
 d=ImageDraw.Draw(board);d.text((16,12),f'2스테이지 새 재질 · 구간 비교 {i+1}/2 · 각 화면 960×540 / 32px·unit / 배경 제외',font=font,fill='#253740')
 board.save(OUT/f'regions-{i+1}-overview.png')
# A single scrollable contact sheet for one-click review.
contact=Image.new('RGB',(960,8*582),'white');d=ImageDraw.Draw(contact)
for i,row in enumerate(rows):d.text((16,i*582+10),row['id']+' · '+row['title'],font=font,fill='#253740');contact.paste(Image.open(OUT/row['screen']),(0,i*582+42))
contact.save(OUT/'all-eight-regions-native.png')
sections=''.join(f'<section id="{r["id"]}"><h2>{r["id"]} · {html.escape(r["title"])}</h2><a href="preview/{r["screen"]}"><img src="preview/{r["screen"]}" width="960" height="540"></a><p><a href="preview/{r["wholeRoom"]}">방 전체 원본 {r["roomNativeSize"][0]}×{r["roomNativeSize"][1]} 보기</a></p></section>' for r in rows)
nav=' · '.join(f'<a href="#{r["id"]}">{r["id"]}</a>' for r in rows)
(HERE/'review.html').write_text('<!doctype html><html lang="ko"><meta charset="utf-8"><title>2스테이지 통일 재질 구간 비교</title><style>body{font:16px system-ui;margin:24px;color:#253740;background:#fff}nav{position:sticky;top:0;background:#fff;padding:12px 0}section{margin:32px 0 60px}h2{font-size:20px}img{display:block;image-rendering:pixelated;max-width:100%;height:auto;border:1px solid #e5eaea}a{color:#426575}p{max-width:960px}</style><h1>2스테이지 · 타일 구간 비교</h1><p>승인한 v21 통일 재질 타일, 실제 current 레이아웃. 각 화면 960×540·32px/unit, 캐릭터 원본을 확대 없이 배치. 흰 배경의 오프라인 합성본이며 Unity 캡처가 아닙니다. 이미지를 누르면 원본 크기로 볼 수 있습니다.</p><nav>'+nav+'</nav>'+sections+'</html>',encoding='utf-8')
(HERE/'manifest.json').write_text(json.dumps({'tilePackage':'../stage2-unified-stone-v21','layout':'current','source':'../source/chapter2-layouts.json','sourceSha256':hashlib.sha256(data.read_bytes()).hexdigest(),'pixelsPerUnit':32,'heroVisibleNativeSize':list(scope['hero'].size),'characterResampled':False,'background':'white','views':rows,'unityModified':False},ensure_ascii=False,indent=2),encoding='utf-8')
print('8 screen views +8 whole rooms; each hero is at a verified existing landing point. No character resizing or geometry edits.')
