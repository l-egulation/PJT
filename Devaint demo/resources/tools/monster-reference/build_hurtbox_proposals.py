"""Review-only hurtbox drafts. Never changes Unity/catalog/approved artwork."""
import json
import base64
import io
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps

from build import read, uri, frame_at, configure

ROOT = OUT = FONT = SMALL = None

# Review-only compact boxes; never exported into gameplay definitions.
COMPACT = {'01_thorn-root': (1.75,1.9), '02_broom-stalker': (1.2,2.35),
           '03_gutter-spitter': (2.25,1.85), '04_well-warden': (2.75,3.4)}


def encode(im):
    data = io.BytesIO(); im.save(data, format='PNG')
    return uri(data.getvalue())


def encode_alpha(mask):
    image = Image.new('RGBA',mask.size,'white')
    image.putalpha(mask)
    return encode(image)


def prepare(m, clip, index, label):
    sprite = Image.open(ROOT/'unity'/clip['frames'][index]['path']).convert('RGBA')
    body = sprite.getchannel('A').point(lambda a: 255 if a else 0)
    # Review masks for just the three illustrated Warden poses. These are not a
    # production segmentation of every frame. Native visible pixels stay intact.
    if m['id'] == '04_well-warden':
        remove = Image.new('L', sprite.size); d = ImageDraw.Draw(remove)
        if clip['id'] == 'idle':
            d.polygon([(0,90),(33,91),(33,128),(0,128)], fill=255)
        elif index == 12:
            # Retain the entire gauntlet/hand; the chain starts beyond its wrist.
            d.rectangle((0,0,209,sprite.height), fill=255)
        elif index == 28:
            # The exposed forward claw is a hand, not a held weapon. Keep it all.
            pass
        body = ImageChops.subtract(body, remove)
    excluded = ImageChops.subtract(sprite.getchannel('A'), body)
    hurt = body.filter(ImageFilter.MaxFilter(5))  # Proposed two native pixel forgiveness.
    px, py = clip['pivot'][0]*sprite.width, (1-clip['pivot'][1])*sprite.height
    b = hurt.getbbox()
    # Runtime facing +1 flips the native left-facing art about its native pivot.
    bounds = [(px-b[2])/32, (py-b[3])/32, (px-b[0])/32, (py-b[1])/32]
    size, offset = m['bodySize'], m['bodyOffset']
    current = [offset[0]-size[0]/2, offset[1]-size[1]/2,
               offset[0]+size[0]/2, offset[1]+size[1]/2]
    current_mask = Image.new('L', sprite.size); d = ImageDraw.Draw(current_mask)
    d.rectangle((round(px-current[2]*32), round(py-current[3]*32),
                 round(px-current[0]*32), round(py-current[1]*32)), fill=255)
    covered = ImageChops.multiply(body,current_mask).histogram()[255]
    count = body.histogram()[255]
    return dict(label=label, motion=clip['id'], frame=index+1, width=sprite.width,height=sprite.height,
                pivot=[px,py], bounds=bounds, current=current, sprite=encode(sprite),
                mask=encode_alpha(hurt), body=encode_alpha(body), excluded=encode_alpha(excluded),
                bodyPixelCoverage=round(100*covered/count,1))


def panel(pose, proposed):
    sprite = Image.open(io.BytesIO(base64.b64decode(pose['sprite'].split(',')[1]))).convert('RGBA')
    mask = Image.open(io.BytesIO(base64.b64decode(pose['mask'].split(',')[1]))).getchannel('A')
    excluded = Image.open(io.BytesIO(base64.b64decode(pose['excluded'].split(',')[1]))).getchannel('A')
    out = Image.new('RGB',(560,550),'#f6f2e8'); d=ImageDraw.Draw(out)
    scale,ox,ground=3,240,451
    d.text((22,18),'수정안 · 여백을 줄인 사각형' if proposed else '현재 · 고정 몸통 박스',font=FONT,fill='#153b3c')
    d.text((22,55),pose['label']+f' · {pose["frame"]}프레임',font=SMALL,fill='#686f67')
    px,py=pose['pivot'];left=round(ox+(px-sprite.width)*scale);top=round(ground-py*scale)
    for x in range(-2,4):d.line((ox+x*96,100,ox+x*96,ground),fill='#e0e5d9')
    for y in range(4):d.line((0,ground-y*96,560,ground-y*96),fill='#e0e5d9')
    mirrored=ImageOps.mirror(sprite).resize((sprite.width*scale,sprite.height*scale),Image.Resampling.NEAREST)
    out.paste(mirrored,(left,top),mirrored)
    if proposed:
        l,b,r,t=pose['fixedBounds'];draw=ImageDraw.Draw(out,'RGBA')
        draw.rectangle((ox+l*96,ground-t*96,ox+r*96,ground-b*96),fill='#00885e28',outline='#00885e',width=3)
    else:
        l,b,r,t=pose['current'];draw=ImageDraw.Draw(out,'RGBA')
        draw.rectangle((ox+l*96,ground-t*96,ox+r*96,ground-b*96),fill='#087fbd20',outline='#087fbd',width=3)
    d=ImageDraw.Draw(out);d.line((0,ground,560,ground),fill='#a0b5aa',width=2)
    d.text((22,484),'초록 사각형 전체가 피격 범위' if proposed else f'몸 픽셀 포함률 {pose["bodyPixelCoverage"]}%',font=SMALL,fill='#316c60' if proposed else '#277491')
    d.text((22,514),'모션이 달라져도 같은 크기 유지' if proposed else '선택한 대표 자세의 몸 픽셀 기준',font=SMALL,fill='#6e756d')
    return out


def main():
    global ROOT, OUT, FONT, SMALL
    ROOT, OUT, FONT, SMALL = configure()
    OUT.mkdir(parents=True,exist_ok=True)
    specs=read('data/combat/stage1_village_monsters.json')['monsters']
    imports={m['id']:m for m in read('unity/Assets/Deviant/Stage1Monsters/Data/import.json')['monsters']}
    payload=[]
    for m in specs:
        clips={c['id']:c for c in imports[m['id']]['motions']}
        picks=[(clips['idle'],0,'대기 자세')]
        if m['attackKind']=='hook-claw':
            picks += [(clips['attack'],12,'갈고리를 뻗은 자세'),(clips['attack'],28,'팔을 뻗어 타격하는 자세')]
        else:
            picks += [(clips['attack'],frame_at(clips['attack'],m['damageStart']+.03),'공격 자세')]
        poses=[prepare(m,c,i,label) for c,i,label in picks]
        representative_count=len(poses)
        attack=clips['attack']
        for index, frame in enumerate(attack['frames']):
            if any(p['motion']=='attack' and p['frame']==index+1 for p in poses):
                continue
            sprite=Image.open(ROOT/'unity'/frame['path']).convert('RGBA')
            poses.append({**poses[0], 'label':'공격 자세', 'motion':'attack', 'frame':index+1,
                          'width':sprite.width, 'height':sprite.height,
                          'pivot':[attack['pivot'][0]*sprite.width,(1-attack['pivot'][1])*sprite.height],
                          'sprite':encode(sprite)})
        union=[min(p['bounds'][0] for p in poses),min(p['bounds'][1] for p in poses),
               max(p['bounds'][2] for p in poses),max(p['bounds'][3] for p in poses)]
        current=poses[0]['current']
        union=[min(union[0],current[0]),min(union[1],current[1]),max(union[2],current[2]),max(union[3],current[3])]
        wide=union
        w,h=COMPACT[m['id']]
        union=[-w/2,0,w/2,h]
        for pose in poses:pose['fixedBounds']=union
        result={**m,'poses':poses,'fixedBounds':union,'wideBounds':wide}
        for n,p in enumerate(poses):
            if n>=representative_count:continue
            img=Image.new('RGB',(1120,550),'#f6f2e8');img.paste(panel(p,False),(0,0));img.paste(panel(p,True),(560,0))
            name=f'{m["id"]}-{n}-proposal.png';img.save(OUT/name)
            p['comparison']=encode(img);p['comparisonFile']=name
        payload.append(result)
    attack_sheet=Image.new('RGB',(2240,2480),'#f6f2e8')
    draw=ImageDraw.Draw(attack_sheet)
    for row,m in enumerate(payload):
        draw.text((22,row*620+12),m['name']+' · 공격 전 과정 / 승인한 크기 유지',font=FONT,fill='#153b3c')
        attack_poses=sorted((p for p in m['poses'] if p['motion']=='attack'),key=lambda p:p['frame'])
        for column,fraction in enumerate((0,.33,.66,1)):
            pose=attack_poses[round((len(attack_poses)-1)*fraction)]
            attack_sheet.paste(panel(pose,True),(column*560,row*620+55))
    attack_sheet.save(OUT/'attack-postures-proposals.png')
    html=Path(__file__).with_name('hurtbox_proposals.html').read_text(encoding='utf-8')
    (OUT/'index.html').write_text(html.replace('__DATA__',json.dumps(payload,ensure_ascii=False).replace('</',r'<\/')),encoding='utf-8')
    sheet=Image.new('RGB',(2240,1240),'#f6f2e8')
    draw=ImageDraw.Draw(sheet)
    for i,m in enumerate(payload):
        x=(i%2)*1120;y=(i//2)*620
        draw.text((x+22,y+12),m['name'],font=FONT,fill='#153b3c')
        sheet.paste(Image.open(OUT/m['poses'][0]['comparisonFile']),(x,y+55))
    sheet.save(OUT/'all-monsters-proposals.png')
    print(f'Built {OUT / "index.html"}; 4 species, {sum(len(m["poses"]) for m in payload)} review poses; Unity unchanged.')


if __name__=='__main__':main()
