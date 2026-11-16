"""Select a single Aether slam, preserving source positions and airborne motion."""
from pathlib import Path
import argparse,base64,hashlib,io,json,subprocess,warnings
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parent
SOURCE=Path(r'C:/Users/SSAFY/Downloads/1469b244-1add-45bf-8de3-2bc831aa5ec2.png')
SELECTIONS={
    16:[(1,60),(10,60),(14,70),(18,70),(22,80),(32,20),(36,20),(38,20),
        (39,70),(42,50),(50,60),(54,60),(62,80),(70,80),(80,80),(90,70)],
    14:[(1,60),(10,90),(18,110),(22,80),(32,20),(36,20),(38,20),
        (39,70),(42,50),(50,60),(54,60),(62,80),(70,130),(90,100)],
}


def branch():
    assert subprocess.check_output(['git','-C',str(REPO),'branch','--show-current'],text=True).strip()=='feat/story-unity-essentials'
    assert subprocess.check_output(['git','-C',str(ROOT),'branch','--show-current'],text=True).strip()=='feat/basic-sword-normal-weapon'


def dump(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')


def phase(n):
    if n<=22:return '준비 / 큰 백스윙','#17677a'
    if n<39:return '체공 / 빠른 내려치기','#a55d27'
    if n<=42:return '접촉 / 짧게 유지','#947126'
    return '착지 / 복귀','#36706b'


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--destination',required=True,type=Path)
    args=parser.parse_args();branch();out=args.destination.resolve();out.mkdir(parents=True,exist_ok=False)
    warnings.simplefilter('ignore',Image.DecompressionBombWarning)
    raw=SOURCE.read_bytes();digest=hashlib.sha256(raw).hexdigest()
    (out/'original-sheet.png').write_bytes(raw)
    im=Image.open(io.BytesIO(raw)).convert('RGB');assert im.size==(12800,7200)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',24)
    small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
    check=[]
    for count,selected in SELECTIONS.items():
        dest=out/f'selected-{count}';dest.mkdir()
        frames=[];times=[];names=[];images=[];steps=[];clock=0
        cols=4;rows=(count+3)//4
        board=Image.new('RGB',(4*480,rows*400+60),'#e9edf1');bd=ImageDraw.Draw(board)
        bd.text((18,12),f'망치 스킬 · {count}프레임 · 원본 자세와 체공 높이 유지',font=font,fill='#18222b')
        for j,(n,ms) in enumerate(selected):
            i=n-1;x=i%10*1280;y=i//10*720
            tile=im.crop((x,y,x+1280,y+720));name=f'F{n:03d}'
            assert n not in [33,34,35], 'Hammer head clipped at the source frame top'
            assert all(min(tile.getpixel((px,0)))>=225 for px in range(1280)), name
            tile.save(dest/f'{j+1:02d}-{name}.png')
            # One constant crop for all frames. Do not re-anchor airborne feet,
            # resize body parts, remove effects, or edit the source colors.
            crop=tile.crop((180,0,1140,720))
            title,color=phase(n)
            frame=Image.new('RGB',(768,636),'white');d=ImageDraw.Draw(frame)
            d.rectangle((0,0,768,40),fill=color)
            d.text((16,5),f'{count}프레임 · {title}',font=font,fill='white')
            frame.paste(crop.resize((768,576),Image.Resampling.NEAREST),(0,40))
            d.text((16,612),f'{j+1:02d}/{count} · {name} · {ms}ms',font=small,fill='#4b5563')
            frames.append(frame);times.append(ms);names.append(name)
            buf=io.BytesIO();tile.save(buf,format='PNG');images.append('data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode())
            steps.append({'frame':name,'sourceIndex':n,'startMs':clock,'ms':ms,'phase':title})
            if n==39:assert clock==400
            clock+=ms
            bx=j%cols*480;by=j//cols*400+60
            board.paste(crop.resize((480,360),Image.Resampling.NEAREST),(bx,by+30))
            bd.text((bx+10,by+3),f'{j+1:02d} / {name} / {ms}ms',font=small,fill=color)
            # Selected PNG is a lossless source crop, not a generated redraw.
            assert Image.open(dest/f'{j+1:02d}-{name}.png').convert('RGB').tobytes()==tile.tobytes()
        assert clock==950 and len(frames)==count
        review_times=times[:];review_times[-1]+=650
        for suffix,factor in [('',1),('-slow',2)]:
            frames[0].save(out/f'skill-{count}{suffix}.gif',save_all=True,append_images=frames[1:],
                duration=[x*factor for x in review_times],loop=0,disposal=2,optimize=False)
        board.save(out/f'selected-{count}-frames.png')
        dump(out/f'editor-{count}.json',{'schemaVersion':1,'kind':f'hammer-skill-selection-{count}',
            'weapon':'hammer','width':1280,'height':720,'sourceFrames':names,'names':names,
            'images':images,'ms':times,'sequences':{'skill_slam':steps},
            'note':'Frame selection only. White background and source colors retained. Same source canvas throughout; airborne feet not pinned. No Unity application.'})
        with Image.open(out/f'skill-{count}.gif') as gif:
            assert gif.n_frames==count
            assert sum(gif.seek(i) or gif.info['duration'] for i in range(gif.n_frames))==1600
        check.append({'count':count,'selectedFrames':[n for n,ms in selected],'steps':steps,
            'contactSourceFrame':'F039','contactMs':400,'clipEndMs':950,'loopPauseMs':650,
            'sourceGrid':[10,10],'sourceTile':[1280,720],'previewCrop':[180,0,1140,720],
            'rejectedFrames':[{'sourceIndex':n,'reason':'hammer head clipped at source-frame top'} for n in [33,34,35]],
            'alignment':'fixed source canvas; no per-frame vertical or horizontal re-anchoring',
            'sourceColorsAndWhiteBackgroundUnchanged':True,'approval':'pending-user-selection','unityApplied':False})
    dump(out/'selection.json',{'sourceSha256':digest,'sourceFilename':SOURCE.name,'candidates':check,
        'status':'source motion approved by user; selection candidates pending; no cleanup, palette edit or game integration'})
    assert SOURCE.read_bytes()==raw
    branch();print(json.dumps({'output':str(out),'candidates':[16,14],'contactMs':400,'endMs':950,'airbornePreserved':True},ensure_ascii=False))


if __name__=='__main__':main()
