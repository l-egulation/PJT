import sys
WITH_COMPARISONS='--with-comparisons' in sys.argv
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import json, hashlib, shutil
R=Path(__file__).parent
P=R/"UnityReady/Assets/Deviant/UI/Sprites"
P.mkdir(parents=True,exist_ok=True)
(R/"Preview").mkdir(exist_ok=True)
src=Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/아트/GUI/전투HUD_초안_v1/Preview/새_게임화면_GUI.png")
im=Image.open(src).convert("RGBA")
W,H=im.size
ops=[]; manifest=[]
def save(id,pic,xy,kind="sprite",border=None):
    pic.save(P/(id+".png"))
    manifest.append(dict(id=id,file="UnityReady/Assets/Deviant/UI/Sprites/"+id+".png",size=list(pic.size),border=border or [0,0,0,0],sha256=hashlib.sha256((P/(id+".png")).read_bytes()).hexdigest()))
    ops.append(dict(type=kind,id=id,x=xy[0],y=xy[1],w=pic.width,h=pic.height))
    return pic
def polygon_mask(size,cut):
    w,h=size; c=cut
    pts=[(c,0),(w-c-1,0),(w-c-1,c//2),(w-c//2-1,c//2),(w-c//2-1,c),(w-1,c),(w-1,h-c-1),(w-c//2-1,h-c-1),(w-c//2-1,h-c//2-1),(w-c-1,h-c//2-1),(w-c-1,h-1),(c,h-1),(c,h-c//2-1),(c//2,h-c//2-1),(c//2,h-c-1),(0,h-c-1),(0,c),(c//2,c),(c//2,c//2),(c,c//2)]
    mask=Image.new("L",size);ImageDraw.Draw(mask).polygon(pts,fill=255);return mask
def frame(id,box,inset,cut,color=None):
    pic=im.crop(box)
    # Retain the reference border at original pixel size. Rebuild empty interior
    # from a clean reference column so labels/icons are independent layers.
    x0,y0,x1,y1=box; w,h=pic.size
    if color:
        ImageDraw.Draw(pic).rectangle((inset,inset,w-inset-1,h-inset-1),fill=color)
    else:
        for y in range(inset,h-inset):
            col=im.getpixel((x1-inset-3,y0+y))
            ImageDraw.Draw(pic).line((inset,y,w-inset-1,y),fill=col)
    # Keep exact source corner contours; remove purple scenery only along
    # the outside band instead of imposing a different generic corner shape.
    mask=Image.new("L",pic.size,255)
    for y in range(h):
        for x in range(w):
            if x<inset or y<inset or x>=w-inset or y>=h-inset:
                r,g,b,_=pic.getpixel((x,y))
                if r>g+12 or b>g+43:mask.putpixel((x,y),0)
    pic.putalpha(mask)
    return save(id,pic,(x0,y0),border=[inset]*4)
def icon(id,box,pad=1,threshold=88):
    pic=im.crop(box); rgb=pic.convert("RGB")
    mask=Image.new("L",pic.size)
    a=mask.load();p=rgb.load()
    for y in range(pic.height):
        for x in range(pic.width):
            r,g,b=p[x,y]
            # Only use foreground color/light as a seed; retain connected dark
            # interior by filling enclosed holes after extending the outline.
            if max(r,g,b)>threshold and (max(r,g,b)-min(r,g,b)>22 or min(r,g,b)>threshold):
                a[x,y]=255
    if pad:mask=mask.filter(ImageFilter.MaxFilter(pad*2+1))
    # Fill enclosed silhouettes, preserving dark bottle interiors.
    bg=Image.new("L",(pic.width+2,pic.height+2),0);bg.paste(mask,(1,1))
    ImageDraw.floodfill(bg,(0,0),128)
    a=bg.load()
    for y in range(bg.height):
        for x in range(bg.width):
            a[x,y]=0 if a[x,y]==128 else 255
    pic.putalpha(bg.crop((1,1,pic.width+1,pic.height+1)))
    return save(id,pic,box[:2])
def text(s,box,size=24,font="malgun.ttf"):
    ops.append(dict(type="text",text=s,x=box[0],y=box[1],w=box[2],h=box[3],font=font,fontSize=size,color="#F3EFDF"))
# Outer status panel, filled in reference blue-slate.
frame("status_frame",(39,38,487,207),17,20,"#3A4F5C")
# Two icon seats retain the exact original frame.
frame("hp_seat",(63,57,138,119),9,10,"#26343E")
frame("exp_seat",(63,128,138,192),9,10,"#26343E")
# Meter track: source frame without fill or numeric text.
frame("hp_track",(151,60,466,115),10,10,"#202930")
frame("exp_track",(151,132,466,188),10,10,"#202930")
# Fill art: remove sample text by copying clean per-row material; cap shape kept.
def gauge(id,box,sampleX,eraseX):
    pic=im.crop(box);x0,y0,x1,y1=box
    for y in range(pic.height):
        col=im.getpixel((sampleX,y0+y))
        for x in range(max(0,eraseX-x0),pic.width-5):
            pic.putpixel((x,y),col)
    pic.putalpha(polygon_mask(pic.size,5))
    return save(id,pic,box[:2])
gauge("hp_fill",(158,67,375,106),190,240)
gauge("exp_fill",(158,142,253,179),180,245)
icon("hp_bottle",(76,62,125,113),1,88)
# EXP cap's upper-right is occluded by the level badge in source; it stays under it.
icon("exp_bottle",(76,134,125,185),1,88)
frame("level_badge",(101,121,158,156),6,7,"#26343E")
text("Lv.3",(106,126,46,24),21,"arial.ttf")
text("70 / 100",(244,73,109,29),26,"arial.ttf")
text("40 / 100",(245,146,112,28),26,"arial.ttf")
# Coin/gem frame; two source rows.
frame("currency_frame",(1293,31,1483,142),10,12,"#202930")
# Native separator pixels from the reference, not stretched tall.
sep=im.crop((1303,81,1471,89));save("currency_divider",sep,(1303,81))
icon("coin",(1321,47,1354,78),1,90)
icon("gem",(1321,93,1354,126),1,90)
text("125",(1404,48,49,29),26,"arial.ttf")
text("38",(1411,95,41,28),26,"arial.ttf")
frame("pause_frame",(1500,39,1555,90),8,8,"#202930")
frame("escape_frame",(1560,39,1633,90),8,8,"#202930")
icon("pause_glyph",(1515,55,1541,80),0,130)
text("ESC",(1573,51,48,30),25,"arial.ttf")
# Region plaque in the concept is dark and should not be omitted.
frame("region_plate",(1502,103,1632,143),5,6,"#292B33")
text("숲 · 1-2",(1525,109,91,29),24)
# Loadout frame and physical native separators.
frame("loadout_frame",(519,794,1150,917),15,18,"#202930")
save("loadout_divider",im.crop((666,811,675,902)),(666,811))
ops.append(dict(type="sprite",id="loadout_divider",x=922,y=811,w=9,h=91))
quill=icon("quill",(550,810,646,890),1,92)
# The source word sits beside the feather tip; exclude it from the sprite.
ImageDraw.Draw(quill).rectangle((16,69,95,79),fill=(0,0,0,0))
quill.save(P/"quill.png")
text("깃털펜",(568,879,68,29),22)
# Separate bottle shell, colored inserts (not flat painted blocks).
bottle=icon("active_bottle_shell",(696,811,787,900),1,92)
for box in [(703,849,730,891),(730,849,755,891),(755,849,780,891)]:
    local=(box[0]-696,box[1]-811,box[2]-696,box[3]-811)
    ImageDraw.Draw(bottle).rectangle((local[0],local[1],local[2]-1,local[3]-1),fill="#1B2831")
bottle.save(P/"active_bottle_shell.png")
icon("ink_insert_red",(704,850,729,891),0,90)
# second red insert kept identical in size to its source, separately extracted.
icon("ink_insert_red_b",(730,850,755,891),0,90)
icon("ink_insert_blue",(756,850,779,891),0,90)
text("장착 잉크",(801,819,99,31),22,"malgunbd.ttf")
text("3 / 3",(818,855,67,30),26,"arial.ttf")
icon("drop_red",(951,829,992,885),1,95)
ops.append(dict(type="sprite",id="drop_red",x=1014,y=829,w=41,h=56))
icon("drop_blue",(1076,829,1119,885),1,95)
frame("bag_frame",(1310,846,1519,914),10,12,"#202930")
frame("mix_frame",(1524,846,1652,914),10,12,"#202930")
icon("bag",(1330,858,1377,902),1,88)
text("가방 4 / 6",(1385,862,116,35),24,"malgunbd.ttf")
text("잉크 조합",(1540,862,99,35),22,"malgunbd.ttf")
# Update hashes after shell extraction.
for m in manifest:m["sha256"]=hashlib.sha256((R/m["file"]).read_bytes()).hexdigest()
# All sprite widths/heights must equal source dimensions: no anisotropic resizing.
lookup={m["id"]:m for m in manifest}
layer=Image.new("RGBA",(W,H))
for op in ops:
    if op["type"]=="sprite":
        pic=Image.open(R/lookup[op["id"]]["file"]).convert("RGBA")
        assert pic.size==(op["w"],op["h"]),op
        layer.alpha_composite(pic,(op["x"],op["y"]))
    else:
        font=ImageFont.truetype("C:/Windows/Fonts/"+op["font"],op["fontSize"])
        box=ImageDraw.Draw(layer).textbbox((0,0),op["text"],font=font)
        x=op["x"]+(op["w"]-(box[2]-box[0]))/2-box[0]
        y=op["y"]+(op["h"]-(box[3]-box[1]))/2-box[1]
        ImageDraw.Draw(layer).text((round(x),round(y)),op["text"],font=font,fill=op["color"])
layer.save(R/"Preview/GUI_투명레이어.png")
bg=Image.open(r"C:/Users/SSAFY/Desktop/자율 프로젝트/outputs/ink_forest/01-start.png").convert("RGBA").resize((W,H),Image.Resampling.NEAREST)
if WITH_COMPARISONS:Image.alpha_composite(bg,layer).save(R/"Preview/실제파츠조립_게임화면.png")
# Controlled comparison: same generated reference environment, clear only the HUD
# areas with flat slate where covered by replacement; do NOT call this game capture.
comparison=Image.new("RGB",(W,450),"#151C22")
ref=im.crop((0,0,W,220));rebuilt=Image.alpha_composite(Image.new("RGBA",(W,H),"#151C22"),layer).crop((0,0,W,220))
comparison.paste(ref,(0,0));comparison.paste(rebuilt,(0,230))
if WITH_COMPARISONS:comparison.save(R/"Preview/상단비교_위원본_아래실제파츠.png")
bottom=Image.new("RGB",(W,320),"#151C22")
bottom.paste(im.crop((0,785,W,941)),(0,0))
bottom.paste(Image.alpha_composite(Image.new("RGBA",(W,H),"#151C22"),layer).crop((0,785,W,941)),(0,164))
if WITH_COMPARISONS:bottom.save(R/"Preview/하단비교_위원본_아래실제파츠.png")
(R/"sprite-manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
(R/"layout.json").write_text(json.dumps(dict(referenceResolution=[W,H],source=str(src),operations=ops),ensure_ascii=False,indent=2),encoding="utf-8")
(R/"검증.json").write_text(json.dumps(dict(spriteCount=len(manifest),spriteInstances=sum(o["type"]=="sprite" for o in ops),allSpritesAtNativeDimensions=True,background="Original actual game capture; nearest scaled to reference canvas",sourceSHA256=hashlib.sha256(src.read_bytes()).hexdigest(),note="Only the complete canvas should be uniformly scaled. Offline assembled preview, not Unity Game View."),ensure_ascii=False,indent=2),encoding="utf-8")
print("Saved",len(manifest),"sprites and",len(ops),"layout operations; no sprite stretched.")

