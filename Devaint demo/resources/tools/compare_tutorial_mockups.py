"""Align original mockups and Unity renders for review; never edits production artwork."""
from pathlib import Path
import argparse,shutil,json
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
PAGES=[('attack','공격','attack.png'),('sketch','1. 스케치','page-1.png'),('ink','2. 잉크','page-2.png'),
       ('palette','3. 팔레트','page-3.png'),('activation','4. 활성화','page-4.png'),('durability','5. 내구도','page-5.png')]

def build(mockups,current,font):
    group=ROOT/'ui/first-run-tutorial-pixel/preview/comparison'
    for name in ('mockups','current','pairs'):(group/name).mkdir(parents=True,exist_ok=True)
    face=ImageFont.truetype(str(font),18)
    for key,title,filename in PAGES:
        shutil.copy2(mockups/(key+'.png'),group/'mockups'/(key+'.png'))
        shutil.copy2(current/filename,group/'current'/(key+'.png'))
        original=Image.open(group/'mockups'/(key+'.png')).convert('RGB')
        original=original.resize((960,540),Image.Resampling.LANCZOS).crop((212,114,748,426))
        assembled=Image.open(group/'current'/(key+'.png')).convert('RGB')
        assert assembled.size==(960,540)
        assembled=assembled.crop((212,114,748,426))
        comparison=Image.new('RGB',(1104,364),'#202321')
        pen=ImageDraw.Draw(comparison)
        pen.text((10,8),title+' · 기존 시안',font=face,fill='#f3e8cb')
        pen.text((558,8),title+' · 현재 조합',font=face,fill='#f3e8cb')
        comparison.paste(original,(10,42));comparison.paste(assembled,(558,42))
        comparison.save(group/'pairs'/(key+'.png'))
    template=(ROOT/'ui/first-run-tutorial-pixel/source/comparison-template.html').read_text(encoding='utf-8')
    (group/'index.html').write_text(template,encoding='utf-8')
    (group/'comparison.json').write_text(json.dumps({'left':'original approved mockup',
        'right':'Unity 6000.6.5f1 composition with user-edited native characters',
        'normalization':{'viewport':[960,540],'panelCrop':[212,114,748,426]},
        'pages':[{'id':k,'title':t}for k,t,_ in PAGES]},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(group/'index.html')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mockups',type=Path,required=True)
    parser.add_argument('--current',type=Path,required=True)
    parser.add_argument('--font',type=Path,required=True)
    args=parser.parse_args();build(args.mockups,args.current,args.font)
