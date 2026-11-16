import numpy as np
from PIL import Image,ImageDraw
from scipy import ndimage
al=np.asarray(Image.open('preview-aligned-3840.png').convert('RGB')).copy()
cell=lambda f:al[(f//6)*360:(f//6+1)*360,(f%6)*640:(f%6+1)*640]
# 화살 잔상: 앞팔이 고정이라 바로 앞 칸의 같은 자리로 덮는다
FIX={13:(12,215,85,150),19:(18,205,85,150),24:(23,290,86,148),29:(28,205,85,150)}
for f,(r,xc,ya,yb) in FIX.items():
  c=cell(f);c[ya:yb,0:xc]=cell(r)[ya:yb,0:xc]
for f in range(36):
  c=cell(f);m=(765-c.astype(int).sum(2))>8
  lab,n=ndimage.label(m,structure=np.ones((3,3)));sz=ndimage.sum(m,lab,range(1,n+1));keep=np.argmax(sz)+1
  body=ndimage.binary_dilation(lab==keep,iterations=3);drop=np.zeros_like(m)
  for k in range(1,n+1):
    if k!=keep and not ((lab==k)&body).any():drop|=lab==k
  c[drop]=255
Image.fromarray(al).save('preview-fixed-3840.png')
fr=[12,13,18,19,20,23,24,25,28,29];O=Image.new('RGB',(5*300,2*150),'white');dr=ImageDraw.Draw(O);im=Image.fromarray(al)
for k,f in enumerate(fr):
  X,Y=(k%5)*300,(k//5)*150;O.paste(im.crop(((f%6)*640+120,(f//6)*360+70,(f%6)*640+420,(f//6)*360+200)),(X,Y+16));dr.text((X+3,Y+2),f'{f:02d}',fill='red')
O.resize((O.width*4//3,O.height*4//3),Image.NEAREST).save(r'C:\Users\SSAFY\AppData\Local\Temp\claude\a222fd4d74955e13\614ef2ef-7978-4c86-ac12-2b9d0126d111\scratchpad\streaks_fixed.png')
