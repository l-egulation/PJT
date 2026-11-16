"""Assemble the separate approved-style masonry and portcullis into aligned game frames."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageChops
import numpy as np
from authoring_output import destination
ROOT=Path(__file__).resolve().parents[1]
def clean(im):
    a=np.array(im.convert('RGBA')).copy();a[:,:,3]=np.clip((a[:,:,3].astype(float)-128)*8,0,255).astype('uint8');return Image.fromarray(a)
def export(out):
    src=Image.open(ROOT/'environments/village/approved-hub-20261101/source/boss-sewer/source-simple.png')
    stone=clean(src.crop((113,32,915,978))).resize((112,144),Image.Resampling.LANCZOS)
    gate=clean(src.crop((990,233,1455,954))).resize((68,111),Image.Resampling.LANCZOS)
    mask=Image.new('L',(112,144));draw=ImageDraw.Draw(mask);draw.ellipse((22,20,90,88),fill=255);draw.rectangle((22,54,90,132),fill=255)
    for i in range(13):
        layer=Image.new('RGBA',stone.size);layer.alpha_composite(gate,(22,21-round(120*i/12)))
        layer.putalpha(ImageChops.multiply(layer.getchannel('A'),mask));frame=Image.alpha_composite(stone,layer)
        frame.save(out/(f'{i:02}.png'))
    stone.save(out/'frame.png');gate.save(out/'bars.png');print('13 aligned portcullis states, 112x144 at32 PPU')
if __name__=='__main__':export(destination(__doc__))
