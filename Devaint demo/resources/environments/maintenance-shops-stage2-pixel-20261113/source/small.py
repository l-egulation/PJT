# Chapter 2 maintenance lantern 20x38 and palette sign 52x66, hand-drawn on the grid.
import sys, math
from PIL import Image
from pal import *
M=[INK,C[1],C[3],C[4],C[5],C[6],C[7],C[8],C[9],STEEL[3],STEEL[4]]
# ------------------------------------------------------------------ lantern (lit: warm glass, candle flame)
lv=Can(20,38)
for (x,y) in ((9,0),(10,0),(11,1),(11,2),(10,3),(9,4),(9,5),(10,6)): lv.px(x,y,M[8])           # S hook
for (x,y) in ((8,0),(11,0),(12,1),(12,2),(11,3),(8,4),(8,5),(9,6),(11,6)): lv.px(x,y,INK)
lv.circle(10,9,2,M[7]); lv.px(9,8,M[10]); lv.circle(10,9,3,INK)                                   # ring
for (y,w) in ((12,2),(13,3),(14,4),(15,5),(16,6),(17,7)):                                        # cap
    lv.rect(10-w-1,y,10+w+1,y+1,INK)
    for x in range(10-w,10+w): lv.px(x,y,M[8] if x<10-w+2 else M[4] if x>=10+w-2 else M[6])
lv.px(9,12,M[10])
lv.rect(0,18,20,21,INK); lv.hline(1,19,19,M[8]); lv.px(1,19,M[10])                                # roof band
lv.rect(2,21,18,34,INK)
for y in range(22,33):                                                                            # glass: warm glow, brightest near flame
    for x in range(3,17):
        d=math.hypot((x-9.5)/7,(y-28)/7)
        c=W[10] if d<.35 else W[9] if d<.6 else W[8] if d<.85 else W[7]
        lv.px(x,y,c)
lv.vline(9,25,29,rgb('#fff1c2')); lv.vline(10,26,29,rgb('#ffe08a')); lv.px(9,24,W[10])              # flame
lv.rect(8,30,12,33,PAPER[2]); lv.hline(8,12,30,PAPER[3])                                           # candle
for x in (2,3,16,17): lv.vline(x,21,34,M[5] if x in (3,16) else INK)                              # frame posts
lv.vline(3,22,33,M[8]); lv.vline(16,22,33,M[4]); lv.hline(3,17,26,M[5]); lv.px(4,23,rgb('#fff1c2'))  # cross bar, glint
lv.rect(1,33,19,36,INK); lv.hline(2,18,34,M[7]); lv.hline(2,18,35,M[4])                          # base
lv.rect(4,36,16,38,INK); lv.hline(5,15,36,M[5])
Image.fromarray(lv.a).save((sys.argv[1] if len(sys.argv)>1 else 'v4')+'/lantern.png')
# ------------------------------------------------------------------ palette sign
sv=Can(52,66)
WOOD=[W[4],W[6],rgb('#8a6a48'),rgb('#a5845c'),rgb('#b8976a'),rgb('#cfb084'),rgb('#e6cda0')]
cx,cy,rx,ry=26,43,25,22.5
def inside(x,y):
    if ((x+.5-cx)/rx)**2+((y+.5-cy)/ry)**2>1: return False
    if (x+.5-40)**2+(y+.5-36)**2<=4.6**2: return False                    # thumb hole
    if x>=44 and 41+max(0,(47-x))//2<=y<=47-max(0,(47-x))//2: return False   # wedge notch, 1:2 sides
    return True
for y in range(66):
    for x in range(52):
        if not inside(x,y): continue
        edge=not(inside(x-1,y) and inside(x+1,y) and inside(x,y-1) and inside(x,y+1))
        if edge: sv.px(x,y,INK); continue
        inner=not(inside(x-2,y) and inside(x+2,y) and inside(x,y-2) and inside(x,y+2))
        lit=(x-cx)/rx+(y-cy)/ry<-.25
        if inner: c=WOOD[6] if lit else WOOD[2]
        else:
            c=WOOD[4]
            if y%5==2 and hsh(x//7,y,5)%3: c=WOOD[3]                      # broken grain lines
            elif y%5==0 and hsh(x//9,y,6)%4==0: c=WOOD[5]
            if (x-cx)/rx+(y-cy)/ry>.55: c=WOOD[3] if c==WOOD[4] else WOOD[2]   # lower-right falls into shade
        sv.px(x,y,c)
for (x,y) in ((12,58),(34,27),(42,56)):                                   # knots
    sv.circle(x,y,1,WOOD[2]); sv.px(x,y,WOOD[1])
def blob(px_,py_,r,col,dark,light):
    for y in range(py_-r-1,py_+r+2):
        for x in range(px_-r-1,px_+r+2):
            d=math.hypot(x-px_+.5*(hsh(y,1)%2),y-py_)
            if d<=r+.3: sv.px(x,y,col)
    for y in range(py_-r,py_+r+1):
        for x in range(px_-r,px_+r+1):
            if math.hypot(x-px_,y-py_)<=r and math.hypot(x-px_-1,y-py_-1)>r-1: sv.px(x,y,dark)
    sv.rect(px_-r//2-1,py_-r//2-1,px_-r//2+1,py_-r//2,light); sv.px(px_-r//2-1,py_-r//2,light)
blob(16,32,6,rgb('#c0352c'),rgb('#7e1f1a'),rgb('#ef7a68')); sv.px(24,31,rgb('#c0352c')); sv.px(25,32,rgb('#7e1f1a'))
blob(12,46,6,rgb('#2c5aa8'),rgb('#1b3a72'),rgb('#6e9ad8')); sv.px(19,51,rgb('#2c5aa8'))
blob(25,55,5,rgb('#e0a52a'),rgb('#9a6a12'),rgb('#ffe08a')); sv.px(32,52,rgb('#e0a52a')); sv.px(31,58,rgb('#e0a52a'))
for (x,y,c) in ((8,29,'#c0352c'),(23,37,'#c0352c'),(21,27,'#7e1f1a'),(5,43,'#2c5aa8'),(19,44,'#2c5aa8'),(10,53,'#1b3a72'),(18,58,'#e0a52a'),(31,60,'#9a6a12'),(28,49,'#e0a52a')):
    sv.px(x,y,rgb(c))                                                         # paint spatter
# chain: interlocked links (front oval, side bar), brass mount plate with two screws
sv.rect(16,0,36,5,INK); sv.rect(17,1,35,4,W[7]); sv.hline(17,35,1,W[9]); sv.hline(17,35,3,W[5])
for x in (19,32): sv.px(x,2,W[3]); sv.px(x,1,W[10])
for (y0,front) in ((5,True),(10,False),(13,True),(18,False)):
    if front:
        sv.rect(23,y0,29,y0+6,INK); sv.rect(24,y0+1,28,y0+5,W[6]); sv.rect(25,y0+2,27,y0+4,INK); sv.px(24,y0+1,W[9]); sv.px(27,y0+4,W[4])
    else:
        sv.rect(25,y0,27,y0+4,INK); sv.vline(25,y0,y0+4,W[7]); sv.vline(26,y0,y0+4,W[5])
sv.rect(23,20,29,24,INK); sv.rect(24,21,28,23,W[8]); sv.px(25,22,W[3]); sv.px(24,21,W[10])          # eye screw plate
Image.fromarray(sv.a).save((sys.argv[1] if len(sys.argv)>1 else 'v4')+'/sign.png')
