# Chapter 2 maintenance back wall 480x270, redrawn by hand on the grid (v26 sewer palette + a few extra tones).
# Detail pass after user feedback 2026-11-13: "add colour or carve shapes, keep the details".
import math, sys
from PIL import Image
from pal import *
cv=Can(480,270)
MOSS=[rgb(c) for c in ['#1f2a1a','#2f3d22','#46562b','#64743a','#8a9a4c']]
RUST=[W[4],W[5],W[6],W[7],W[8],rgb('#a8552e'),rgb('#c46d3a')]
def chk(x,y): return (x+y)&1

# ------------------------------------------------------------------ wall bricks (bevelled, varied, pitted)
def bricks(x0,y0,x1,y1,tones,mort,seed,bw=20,bh=10):
    for y in range(y0,y1):
        row=(y-y0)//bh; oy=(y-y0)%bh; off=bw//2 if row%2 else 0
        for x in range(x0,x1):
            ox=(x-x0+off)%bw; bx=(x-x0+off)//bw
            if oy==bh-1 or ox==bw-1: cv.px(x,y,mort); continue
            h=hsh(bx,row,seed); t=h%len(tones); face,hi,lo=tones[t]
            c=face
            if oy==0: c=hi
            elif ox==0: c=hi if oy<bh-3 else face
            elif oy==bh-2 or ox==bw-2: c=lo
            if c==face:
                if hsh(x,y,seed)%41==0: c=lo                         # pits
                elif (h>>6)%5==0 and ox<4 and oy<3 and ox+oy<4: c=mort  # chipped corner
            cv.px(x,y,c)
OUT=[(C[4],C[5],C[3]),(C[4],C[5],C[3]),(C[5],C[6],C[4]),(C[3],C[4],C[2])]
INN=[(C[3],C[4],C[2]),(C[3],C[4],C[2]),(C[4],C[5],C[3]),(C[2],C[3],C[1])]
bricks(24,48,472,270,OUT,C[1],1)
bricks(100,95,377,270,INN,C[0],2)
# recess shade: under the lintel and beside the jambs (stepped, no dither noise)
for i,c in enumerate((C[0],C[0],C[1],C[1],C[2])): cv.hline(100,377,95+i,c)
for i,c in enumerate((C[1],C[1],C[2])): cv.vline(100+i,95,270,c); cv.vline(376-i,95,270,c)
# moss / grime drips from the lintel into the recess (clean tapered shapes)
def drip(x,y,l,cols):
    for i in range(l):
        w=3 if i<l*.35 else 2 if i<l*.7 else 1
        for k in range(w): cv.px(x+k-(w//2),y+i,cols[0] if k==0 and w>1 else cols[1] if i<l-2 else cols[2])
for (x,l) in ((128,14),(171,9),(214,17),(262,11),(305,15),(349,8)): drip(x,101,l,(MOSS[2],MOSS[1],MOSS[0]))
for (x,y,l) in ((40,150,10),(450,140,8),(445,60,6)): drip(x,y,l,(MOSS[2],MOSS[1],MOSS[0]))

# ------------------------------------------------------------------ big stones (two-step bevel, mottling, cracks, moss tufts)
def block(x0,y0,x1,y1,seed,moss_top=False):
    h=hsh(x0,y0,seed); face=[C[5],C[5],C[4],C[6]][h%4]                 # one step darker to sit with the map walls
    cv.rect(x0,y0,x1,y1,C[1]); cv.rect(x0+1,y0+1,x1-1,y1-1,face)
    cv.hline(x0+1,x1-1,y0+1,C[7]); cv.vline(x0+1,y0+1,y1-1,C[7])
    cv.hline(x0+2,x1-2,y0+2,C[6]); cv.vline(x0+2,y0+2,y1-2,C[6])
    cv.hline(x0+2,x1-1,y1-2,C[3]); cv.vline(x1-2,y0+2,y1-1,C[3])
    cv.hline(x0+3,x1-2,y1-3,C[4]); cv.vline(x1-3,y0+3,y1-2,C[4])
    w_,h_=x1-x0,y1-y0
    for k in range(3):                                   # mottled patches 3x2 / 2x1
        px_,py_=x0+4+(h>>(3+k*4))%max(1,w_-10),y0+4+(h>>(7+k*3))%max(1,h_-9)
        cv.rect(px_,py_,px_+3-(k%2),py_+2-(k%2),C[5] if face!=C[5] else C[4]); cv.px(px_,py_-1,C[7] if face!=C[7] else C[8])
    if h%4==0:                                           # crack: varied length/slope, small branch, lit edge
        cx,cy=x0+5+(h>>11)%max(1,w_-14),y0+3+(h>>15)%3; n=4+(h>>19)%7; step=1+(h>>23)%2; d=1 if (h>>25)%2 else -1
        x=cx
        for i in range(min(n,h_-7)):
            cv.px(x,cy+i,C[2]); cv.px(x+1,cy+i,C[8])
            if i%step==step-1: x+=d
        if n>6: cv.px(x-d,cy+n-2,C[2]); cv.px(x-2*d,cy+n-1,C[2])
    if h%5==1:
        cv.px(x0+4,y1-5,C[2]); cv.px(x0+5,y1-5,C[3]); cv.px(x0+4,y1-6,C[3])
    if moss_top and h%2==0:                              # moss tuft sitting on the top edge
        mx=x0+3+(h>>13)%max(1,w_-12); n=5+(h>>17)%5
        cv.hline(mx,mx+n,y0+1,MOSS[3]); cv.hline(mx+1,mx+n-1,y0+2,MOSS[2]); cv.hline(mx+2,mx+n-2,y0+3,MOSS[1])
        cv.vline(mx+n//2,y0+4,y0+7,MOSS[1])
xs=[62,92,126,160,196,232,266,302,336,370,413]
for a,b in zip(xs,xs[1:]): block(a,72,b+1,96,3,True)
ys=[95,121,147,173,199,225,251,270]
for a,b in zip(ys,ys[1:]): block(62,a,101,b+1,4); block(377,a,414,b+1,5)

# ------------------------------------------------------------------ pipes in the chapter 2 map language (v26 renderer):
# brightness levels -> sewer ramp with the +1.4 ambient lift and the 4x4 Bayer dither, the same profiles as
# render_boss.Painter: horizontal 5.6..1.0 lit from the top with a 4-row shadow, vertical 4.6..0.9 lit from the left,
# flange = 8 px band at 3.6 with a 5.0 lit edge and two 2x2 rivets at 5.2. No outline, rust or brass (user 2026-11-13).
BAY=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]]
def lv(x,y,level):
    b=(BAY[y%4][x%4]+.5)/16; cv.px(x,y,C[int(max(0,min(10,math.floor(level+1.4+(b-.5)*.3))))])
HP=[5.6,5.0,4.5,4.2,4.0,3.9,3.8,3.7,3.6,3.5,3.4,3.3,3.2,3.1,3.0,2.9,2.7,2.5,2.2,1.9,1.5,1.0]
VP=[4.6,4.2,3.8,3.6,3.4,3.3,3.2,3.1,3.0,2.9,2.8,2.7,2.5,2.3,2.0,1.7,1.3,.9]
def prof(base,t): return [base[round(i*(len(base)-1)/max(1,t-1))] for i in range(t)]
def hpipe(x0,x1,y0,t,*_,**__):
    P=prof(HP,t)
    for i in range(t):
        for x in range(x0,x1): lv(x,y0+i,P[i])
    for i in range(4):
        for x in range(x0,x1): lv(x,y0+t+i,1.0)
def vpipe(x0,y0,y1,t,*_,**__):
    P=prof(VP,t)
    for i in range(t):
        for y in range(y0,y1): lv(x0+i,y,P[i])
def elbow(cx,cy,r0,r1,quad,*_,**__):
    for y in range(cy-r1-1,cy+r1+2):
        for x in range(cx-r1-1,cx+r1+2):
            dx,dy=x-cx+.5,y-cy+.5
            if quad=='dl' and not(dx<=0 and dy>=0): continue
            if quad=='ur' and not(dx>=0 and dy<=0): continue
            d=math.hypot(dx,dy)
            if r0-.5<=d<=r1+.5:
                t=(d-r0)/max(1,r1-r0)                     # 0 inner -> 1 outer
                lv(x,y,(1.6+t*2.8) if quad=='dl' else (4.6-(1-t)*3.2))
def rivet(x,y):
    for dy in (0,1):
        for dx in (0,1): lv(x+dx,y+dy,5.2)
def flange_h(x0,x1,y0,y1,*_,**__):                # band around a horizontal pipe (8 px wide, centred)
    c=(x0+x1)//2
    for y in range(y0,y1):
        for x in range(c-4,c+4): lv(x,y,5.0 if y==y0 else 3.6)
    rivet(c-1,y0+5); rivet(c-1,y1-7)
def flange_v(x0,x1,y0,y1,*_,**__):                # band around a vertical pipe (8 px tall, centred)
    c=(y0+y1)//2
    for y in range(c-4,c+4):
        for x in range(x0,x1): lv(x,y,5.0 if x==x0 else 3.6)
    rivet(x0+4,c-1); rivet(x1-6,c-1)

def bolt(x,y,dark=C[2],mid=C[7],hi=C[10]):
    cv.px(x+1,y,hi); cv.px(x,y+1,hi); cv.px(x+1,y+1,mid); cv.px(x+2,y+1,dark); cv.px(x+1,y+2,dark)
# thin left pipe going down
vpipe(22,148,270,12); flange_v(18,38,192,205); flange_v(18,38,252,266)
# thick left pipe from the ceiling, bending right at the bottom into the band
vpipe(4,0,120,26); elbow(34,120,4,30,'dl'); hpipe(34,50,124,26)
flange_v(0,34,29,42); flange_v(0,34,88,101); flange_h(48,61,120,154)
# top pipe, bending down into the right pipe
hpipe(30,457,38,22); elbow(457,60,0,22,'ur'); vpipe(457,60,270,22)
for a,b in ((35,48),(128,150),(351,369),(440,457)): flange_h(a,b,35,63)
for a,b in ((77,91),(120,132),(190,201),(223,236)): flange_v(454,480,a,b)

# ------------------------------------------------------------------ lantern hook: wall plate + scroll arms + hook (brass)
B=[W[3],W[5],W[7],W[8],W[9],W[10]]
cv.rect(112,95,124,109,INK); cv.rect(113,96,123,108,B[2]); cv.hline(113,123,96,B[4]); cv.vline(113,96,108,B[3]); cv.hline(114,123,107,B[1]); cv.vline(122,97,108,B[1])
for (x,y) in ((115,98),(119,98),(115,104),(119,104)): cv.px(x,y,B[0]); cv.px(x+1,y,B[5])
for s in (-1,1):                                      # scroll curls either side
    bx=118+s*7
    for (dx,dy) in ((0,0),(s,0),(2*s,1),(3*s,2),(3*s,3),(2*s,4),(s,4),(s,3)): cv.px(bx+dx,100+dy,B[3])
    cv.px(bx+2*s,102,B[0])
cv.rect(116,109,120,117,INK); cv.rect(117,109,119,117,B[3]); cv.vline(117,109,117,B[4])
for (x,y) in ((116,117),(115,118),(115,119),(115,120),(116,121),(117,122),(118,122),(119,122),(120,121),(121,120),(121,119),(121,118)):
    cv.px(x,y,B[3])
for (x,y) in ((114,118),(114,119),(114,120),(115,121),(116,122),(117,123),(118,123),(119,123),(120,122),(121,121),(122,120),(122,119),(122,117),(121,117)):
    cv.px(x,y,INK)

# ------------------------------------------------------------------ tool rack (wood grain) + ruler + compass + wrench
cv.rect(105,163,151,173,INK); cv.rect(106,164,150,172,W[6]); cv.hline(106,150,164,W[8]); cv.hline(106,150,165,W[7]); cv.hline(106,150,171,W[4])
for (x,l,y) in ((109,9,167),(122,14,168),(140,7,166),(115,6,169)): cv.hline(x,x+l,y,W[5])
for x in (107,148): cv.px(x,167,INK); cv.px(x,166,W[9])
for x in (111,127,143): cv.rect(x,172,x+2,178,W[3]); cv.px(x,172,W[8])
cv.rect(107,177,116,224,INK); cv.rect(108,178,115,223,PAPER[1]); cv.vline(108,178,223,PAPER[2]); cv.vline(114,178,223,PAPER[0])
for y in range(180,222,2): cv.hline(112 if (y-180)%10 else 110,114,y,PAPER[0])
cv.rect(111,178,113,180,INK)                                                                       # hanging hole
cv.rect(124,176,131,183,INK); cv.rect(125,177,130,182,W[7]); cv.px(126,178,W[10]); cv.px(127,179,W[3])
cv.circle(127,186,3,W[8]); cv.circle(127,186,4,INK); cv.px(127,186,W[3])
for i in range(32):
    y=190+i; dx=i//6
    cv.px(125-dx,y,W[9]); cv.px(124-dx,y,W[6]); cv.px(126-dx,y,INK if i%6==5 else W[7])
    cv.px(129+dx,y,W[8]); cv.px(130+dx,y,W[5]); cv.px(128+dx,y,W[9] if i%6==0 else W[7])
cv.rect(119,222,121,225,STEEL[4]); cv.rect(134,222,136,225,STEEL[3])                                 # needle tips
cv.rect(139,183,147,217,INK); cv.rect(140,184,146,216,STEEL[2]); cv.vline(141,184,216,STEEL[4]); cv.vline(142,184,216,STEEL[3]); cv.vline(145,184,216,STEEL[1])
cv.rect(136,176,150,186,INK); cv.rect(137,177,149,185,STEEL[3]); cv.hline(137,149,177,STEEL[4]); cv.hline(138,149,184,STEEL[1])
cv.rect(141,176,145,181,C[2]); cv.rect(142,176,144,182,C[2])                                        # open jaw
cv.rect(136,214,150,226,INK); cv.rect(137,215,149,225,STEEL[3]); cv.hline(137,149,215,STEEL[4]); cv.circle(143,220,3,STEEL[1]); cv.disc(143,220,2,C[2])  # ring end

# ------------------------------------------------------------------ blueprint: aged paper, stains, folded corner, real drawings
cv.rect(181,134,305,231,INK)
for y in range(135,230):
    for x in range(182,304):
        e=min(x-182,303-x,y-135,229-y)
        c=PAPER[1] if e==0 else PAPER[2]
        if e<=2 and hsh(x,y,77)%3==0: c=PAPER[1]
        cv.px(x,y,c)
for (cx,cy,r) in ((288,150,9),(197,218,6)):                                      # tea-ring stains
    cv.circle(cx,cy,r,PAPER[1])
for i in range(8):                                                               # folded bottom-right corner
    cv.hline(304-8+i,304,229-i,(0,0,0,0)) if False else None
for i in range(8):
    for j in range(8-i): cv.px(303-j,229-i,PAPER[1] if j<8-i-1 else INK)
    cv.px(303-(7-i),229-i,INK)
L=rgb('#7c6c4c'); L2=PAPER[0]; Lb=rgb('#4f6f86')
# porthole with bolt ring and cross-hatch
cv.circle(213,168,18,L); cv.circle(213,168,14,L); cv.circle(213,168,11,L2)
for k in range(12):
    a=k*math.pi/6; cv.px(213+round(16*math.cos(a)),168+round(16*math.sin(a)),L)
for d in range(-20,21,3):
    for x in range(213-11,213+12):
        y=168+(x-213)+d
        if (x-213)**2+(y-168)**2<=100: cv.px(x,y,PAPER[1])
cv.hline(190,236,190,L2); cv.vline(190,186,194,L2); cv.vline(236,186,194,L2)       # dimension line
for (x,dx) in ((191,1),(235,-1)): cv.px(x+dx,189,L2); cv.px(x+dx,191,L2)
# bent pipe drawing (top right)
cv.rect(246,142,268,143,L); cv.rect(246,150,262,151,L)
cv.circle(268,158,15,L) if False else None
for i in range(9): cv.px(268+i//2+ (1 if i>4 else 0),143+i,L)
for i in range(6): cv.px(262+i//2,151+i,L)
cv.vline(273,151,170,L); cv.vline(265,156,170,L); cv.hline(265,274,170,L); cv.vline(246,142,152,L)
for x in (250,258): cv.vline(x,142,151,L2)
# gear (right middle)
cv.circle(286,186,7,L); cv.circle(286,186,3,L); cv.px(286,186,L)
for k in range(8):
    a=k*math.pi/4+.2; tx,ty=286+round(9*math.cos(a)),186+round(9*math.sin(a)); cv.rect(tx-1,ty-1,tx+1,ty+1,L)
# lower run: pipe with valve and a small tank with fins
cv.hline(190,244,206,L); cv.hline(190,244,211,L); cv.rect(204,203,210,214,L); cv.hline(203,212,201,L); cv.vline(207,201,203,L)
cv.vline(244,206,212,L)
cv.rect(250,200,272,222,L); cv.rect(251,201,271,221,PAPER[2])
for x in range(253,271,3): cv.vline(x,203,220,L2)
cv.rect(262,196,264,200,L)
# title block + scale bar
cv.rect(278,212,301,227,L); cv.rect(279,213,300,226,PAPER[2])
for (y,l) in ((216,18),(219,14),(222,19)): cv.hline(281,281+l,y,L2)
cv.hline(186,226,225,L);
for x in range(186,227,8): cv.vline(x,223,225,L)
cv.rect(186,224,194,225,L)
# blue pencil notes
cv.hline(246,262,180,Lb); cv.hline(246,256,183,Lb); cv.px(244,180,Lb)
for x0 in (189,285):                                                             # brass clips
    cv.rect(x0,127,x0+10,142,INK); cv.rect(x0+1,128,x0+9,141,B[2]); cv.hline(x0+1,x0+9,128,B[4]); cv.vline(x0+1,128,141,B[3]); cv.hline(x0+1,x0+9,140,B[1])
    cv.rect(x0+4,132,x0+6,134,B[0]); cv.px(x0+4,132,B[5])

# ------------------------------------------------------------------ sign bracket (upper right): iron arm, scroll brace, brass plate
I=[INK,W[2],W[4],W[5],W[6],W[8]]
cv.rect(399,96,412,136,INK); cv.rect(400,97,411,135,W[6]); cv.hline(400,411,97,W[8]); cv.vline(400,97,135,W[7]); cv.vline(410,97,135,W[4]); cv.hline(401,411,134,W[4])
for (x,y) in ((403,100),(403,129)): bolt(x,y,W[3],W[7],W[10])
cv.rect(399,101,451,111,INK); cv.rect(400,102,450,110,I[4]); cv.hline(400,450,102,I[5]); cv.hline(400,450,103,W[7]); cv.hline(400,450,109,I[2])
for x in range(414,450,9): cv.px(x,105,I[2]); cv.px(x+1,105,W[8])
cv.rect(447,99,451,113,INK); cv.rect(448,100,450,112,W[7])                               # end stop
# scroll brace: 1:1 diagonal ending in a curl
for i in range(16): cv.px(411+i,126-i,I[5]); cv.px(412+i,126-i,I[3]); cv.px(413+i,126-i,INK)
for (dx,dy) in ((0,0),(-1,1),(-2,1),(-3,0),(-3,-1),(-2,-2),(-1,-2)): cv.px(424+dx,117+dy,I[4])
for (x,y) in ((446,103),): cv.px(x,y,W[10])

# ------------------------------------------------------------------ faucet (lower right): wall pipe, valve bulb, wheel handle, spout + drip
vpipe(421,180,206,11,21,False); elbow(432,206,0,11,'dl'); hpipe(432,457,206,12,22,False)
cv.rect(419,176,434,181,INK); cv.rect(420,177,433,180,C[9]); cv.hline(420,433,177,STEEL[4])
cv.disc(440,207,5,INK); cv.disc(440,207,4,C[8]); cv.px(438,205,STEEL[4]); cv.px(439,205,C[10]); cv.rect(442,208,444,210,C[5])
cv.rect(438,194,443,202,INK); cv.rect(439,195,442,202,C[7]); cv.vline(439,195,202,C[9])
cv.circle(440,191,5,INK); cv.circle(440,191,4,RUST[4]); cv.px(440,191,INK)
for (dx,dy) in ((-4,0),(4,0),(0,-4),(0,4)): cv.px(440+dx,191+dy,RUST[6])
cv.rect(437,212,444,219,INK); cv.rect(438,212,443,218,C[7]); cv.vline(438,212,218,C[9])
cv.px(440,221,rgb('#9cc7c0')); cv.px(440,224,rgb('#6fa29b'))

# ================================================================== the shop stands out: it is the one lit, lived-in room
# (user 2026-11-13: "match chapter 2, but it is interactive, so give it its own feel").
GLOW=[W[8],W[9],W[10],rgb('#ffe3a0'),rgb('#fff6d8')]
# --- paint shelf on the right of the recess: plank, 1:1 brackets, three paint cans (the sign's colours) and a brush jar
cv.rect(328,149,373,154,INK); cv.rect(329,150,372,153,W[6]); cv.hline(329,372,150,W[8]); cv.hline(329,372,152,W[4])
for bx in (333,366):
    for i in range(7): cv.px(bx+i if bx<350 else bx-i,154+i,W[5]); cv.px(bx+i+1 if bx<350 else bx-i-1,154+i,INK)
    cv.vline(bx,154,162,W[5]); cv.vline(bx-1 if bx<350 else bx+1,154,162,INK)
def can(x0,col,dark,light):
    cv.rect(x0,139,x0+10,150,INK); cv.rect(x0+1,141,x0+9,149,col); cv.vline(x0+1,141,149,light); cv.rect(x0+7,141,x0+9,149,dark)
    cv.rect(x0+1,140,x0+9,141,STEEL[3]); cv.hline(x0+2,x0+8,139,STEEL[4]); cv.hline(x0+1,x0+9,144,PAPER[2]); cv.hline(x0+3,x0+7,145,PAPER[0])
    cv.vline(x0+3,141,144,col); cv.px(x0+3,144,col)                                     # paint run over the label
for x0,c in ((330,('#c0352c','#7e1f1a','#ef7a68')),(341,('#2c5aa8','#1b3a72','#6e9ad8')),(352,('#e0a52a','#9a6a12','#ffe08a'))): can(x0,*map(rgb,c))
cv.rect(363,141,372,150,INK); cv.rect(364,142,371,149,rgb('#3f5f5a')); cv.vline(364,142,149,rgb('#7fa8a0'))      # brush jar
for (x,top,tip) in ((365,132,'#c0352c'),(368,134,'#2c5aa8'),(370,131,'#e0a52a')):
    cv.vline(x,top+3,142,W[7]); cv.rect(x-1,top,x+1,top+3,rgb(tip)); cv.px(x-1,top,INK)
for (x,y,c) in ((334,151,'#c0352c'),(346,152,'#2c5aa8'),(357,151,'#e0a52a'),(346,155,'#2c5aa8')): cv.px(x,y,rgb(c))   # drips on the plank
# --- string of warm bulbs sagging under the lintel (two spans, parabolic sag: run lengths change monotonically)
anch=[132,252,372]; bulbs=[]
for a,b in zip(anch,anch[1:]):
    m=(a+b)/2; half=(b-a)/2
    for x in range(a,b+1):
        y=101+round(8*(1-((x-m)/half)**2))
        cv.px(x,y,C[1]); cv.px(x,y+1,C[0]) if False else None
    for x in range(a+12,b-6,20): bulbs.append((x,101+round(8*(1-((x-m)/half)**2))))
for a in anch: cv.rect(a-1,99,a+2,103,INK); cv.px(a,100,W[8])
for (x,y) in bulbs:
    cv.rect(x-1,y+1,x+2,y+3,C[2]); cv.px(x,y+1,C[6])                                    # socket
    cv.rect(x-2,y+3,x+3,y+8,INK); cv.rect(x-1,y+3,x+2,y+7,GLOW[1]); cv.vline(x,y+4,y+7,GLOW[3]); cv.px(x-1,y+4,GLOW[4]); cv.px(x,y+8,INK)
# --- relight: the lantern, the bulbs and the work area warm the stone like the map lamps do (levels up + warm ramp),
# the outer wall falls one step darker so the eye lands on the shop.
import numpy as np
A=cv.a.copy(); H_,W_=A.shape[:2]
lights=[(117,112,175,3.0,140,1.9)]+[(x,y+6,64,1.4,52,1.9) for (x,y) in bulbs]+[(240,185,190,2.4,190,1.1),(418,128,95,1.3,80,1.6)]
yy,xx=np.mgrid[0:H_,0:W_].astype(np.float32)
Lm=np.zeros((H_,W_),np.float32); Wm=np.zeros((H_,W_),np.float32)
for (lx,ly,r,s,rw,ex) in lights:
    d=np.hypot(xx-lx,(yy-ly)*1.15); Lm+=np.clip(1-d/r,0,1)**ex*s; Wm=np.maximum(Wm,np.clip(1-d/rw,0,1)**(1.6 if ex>1.5 else 1.0)*(1 if ex>1.5 else .8))
vig=np.clip((np.hypot(xx-240,(yy-175)*1.3)-170)/110,0,1)*.9
Ci={tuple(c[:3]):k for k,c in enumerate(C)}; Wi={tuple(c[:3]):k for k,c in enumerate(W)}
for y in range(H_):
    for x in range(W_):
        if A[y,x,3]==0: continue
        key=tuple(int(v) for v in A[y,x,:3]); b=(BAY[y%4][x%4]+.5)/16
        if key in Ci:
            li=int(max(0,min(10,np.floor(Ci[key]+Lm[y,x]*.85-vig[y,x]+(b-.5)*.3))))
            ws=min(3,int(Wm[y,x]*3+b))/3
            c=np.array(C[li][:3])*(1-ws)+np.array(W[li][:3])*ws
            cv.a[y,x,:3]=np.round(c).astype(np.uint8)
        elif key in Wi:
            li=int(max(0,min(10,np.floor(Wi[key]+Lm[y,x]*.5-vig[y,x]*.6+(b-.5)*.3)))); cv.a[y,x,:3]=W[li][:3]
Image.fromarray(cv.a).save(sys.argv[1] if len(sys.argv)>1 else 'v4/stage2-backdrop.png')
