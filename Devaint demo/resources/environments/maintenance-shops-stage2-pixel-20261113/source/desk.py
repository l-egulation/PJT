# Chapter 2 maintenance workbench 166x72, hand-drawn on the grid (same layout as the adopted painting).
import sys, math
from PIL import Image
from pal import *
cv=Can(166,72)
RUST=[W[4],W[5],W[6],W[7]]
M=[INK,C[1],C[3],C[4],C[5],C[6],C[7],C[8],C[9],STEEL[3],STEEL[4]]     # metal ramp dark -> light
def rivet(x,y): cv.px(x+1,y,M[10]); cv.px(x,y+1,M[9]); cv.px(x+1,y+1,M[6]); cv.px(x+2,y+1,M[2]); cv.px(x+1,y+2,M[2])
def post(x0,x1,y0,y1,rings=()):
    t=x1-x0; R=[INK]+[M[[7,9,10,8,7,6,6,5,5,4,4,3,3,2,2][min(14,round(i*14/max(1,t-3)))]] for i in range(t-2)]+[INK]
    for i in range(t): cv.vline(x0+i,y0,y1,R[i])
    for (ry,grow) in rings:                                      # turned rings: wider, lit top, dark bottom
        cv.rect(x0-grow,ry,x1+grow,ry+4,INK); cv.hline(x0-grow+1,x1+grow-1,ry+1,M[9]); cv.hline(x0-grow+1,x1+grow-1,ry+2,M[6]); cv.hline(x0-grow+1,x1+grow-1,ry+3,M[3])
        cv.px(x0-grow+1,ry+1,M[10])
def foot(x0,x1,y0):
    for i,y in enumerate(range(y0,72)):
        g=min(i,3); cv.rect(x0-g,y,x1+g,y+1,INK)
        cv.hline(x0-g+1,x1+g-1,y,[M[8],M[6],M[5],M[4],M[3],M[3],M[2],M[2]][min(i,7)])
    cv.hline(x0-3,x1+3,71,INK)
# back rail under the top
cv.rect(5,28,154,37,INK); cv.rect(6,29,153,36,M[3]); cv.hline(6,153,29,M[1]); cv.hline(6,153,35,M[2])
for x in range(80,134,13): rivet(x,31)
# legs
post(6,23,28,66,((36,1),(58,1))); foot(6,23,66)
post(64,74,28,66,()); foot(64,74,66)
for y in (33,46,59): rivet(67,y)
post(137,151,36,66,((40,1),(58,1))); foot(137,151,66)
# drawer cabinet
cv.rect(22,28,66,67,INK); cv.rect(23,29,65,66,M[5]); cv.hline(23,65,29,M[7]); cv.vline(23,29,66,M[7]); cv.hline(24,65,65,M[2]); cv.vline(64,30,66,M[2])
def drawer(x0,y0,x1,y1,hx,hy,hw):
    cv.rect(x0,y0,x1,y1,INK); cv.rect(x0+1,y0+1,x1-1,y1-1,M[4]); cv.hline(x0+1,x1-1,y0+1,M[6]); cv.vline(x0+1,y0+1,y1-1,M[6])
    cv.hline(x0+2,x1-1,y1-2,M[2]); cv.vline(x1-2,y0+2,y1-1,M[2])
    cv.rect(hx-1,hy-1,hx+hw+1,hy+3,INK); cv.rect(hx,hy,hx+hw,hy+2,STEEL[3]); cv.hline(hx,hx+hw,hy,STEEL[4]); cv.hline(hx+1,hx+hw-1,hy+1,STEEL[2])
    for x in (hx-1,hx+hw): cv.px(x,hy+3,M[2])
drawer(25,31,63,42,37,35,12); drawer(25,44,63,64,29,49,11)
cv.rect(52,52,56,55,M[2]); cv.px(52,52,M[1])                                       # key plate
for (x,y,l) in ((30,42,3),(58,64,2),(40,64,3)): cv.vline(x,y,y+l,RUST[2]); cv.px(x,y+l,RUST[1])
for (x,y) in ((27,33),(60,47),(45,58),(33,60)): cv.px(x,y,RUST[3]); cv.px(x+1,y,RUST[2])
# table top: thick plank with grain, lit front edge, end grain
cv.rect(0,16,160,29,INK)
for y in range(17,28):
    for x in range(1,159):
        c=[W[8],W[7],W[7],W[6],W[6],W[6],W[6],W[5],W[5],W[4],W[3]][y-17]
        cv.px(x,y,c)
for (y,segs) in ((19,((8,40),(52,90),(104,150))),(22,((3,30),(44,72),(85,121),(133,156))),(25,((12,58),(70,110),(122,145)))):
    for a,b in segs: cv.hline(a,b,y,W[5] if y<25 else W[4])
for (x,y) in ((34,21),(97,23),(141,20)): cv.circle(x,y,1,W[4]); cv.px(x,y,W[6])         # knots
cv.vline(1,17,28,W[5]); cv.vline(158,17,28,W[4]); cv.hline(1,159,17,W[9])
for x in (2,157):
    for y in range(18,27,3): cv.px(x,y,W[4])
# book (worn purple) on the left
cv.rect(4,11,23,17,INK); cv.rect(5,12,22,16,rgb('#5a3a55')); cv.hline(5,22,12,rgb('#7d5577')); cv.hline(6,22,15,PAPER[2]); cv.vline(5,12,16,rgb('#3e2840'))
cv.hline(6,22,14,rgb('#4a3047')); cv.px(18,13,W[9])
# jars: glass body, colour, label, cap, highlight
def jar(x0,col,dark,light):
    # widths (half, from centre x0+7) per row: neck, shoulder, round body
    rows={4:2,5:2,6:3,7:5,8:6,9:6,10:6,11:6,12:6,13:6,14:6,15:5,16:4}
    for y,w in rows.items(): cv.rect(x0+7-w-1,y,x0+7+w+1,y+1,INK)
    cv.rect(x0+7-w-1,17,x0+7+w+1,17,INK)
    cv.rect(x0+3,16,x0+12,17,INK)
    for y,w in rows.items():
        if y in (4,): continue
        for x in range(x0+7-w,x0+7+w):
            r=(x-(x0+7-w))/(2*w-1)
            c=light if r<.22 else dark if r>.72 else col
            if y>=15 and r>.5: c=dark
            cv.px(x,y,c)
    cv.rect(x0+4,0,x0+11,4,INK); cv.rect(x0+5,1,x0+10,3,M[6]); cv.hline(x0+5,x0+10,1,M[9]); cv.px(x0+9,2,M[3])     # cap
    cv.rect(x0+4,10,x0+11,14,PAPER[2]); cv.hline(x0+4,x0+11,13,PAPER[1]); cv.hline(x0+5,x0+9,11,PAPER[0])          # label
    cv.vline(x0+3,8,10,rgb('#e8f0ec')); cv.px(x0+4,7,rgb('#e8f0ec')); cv.px(x0+3,14,rgb('#c9d8d2'))                 # glass glints
jar(23,rgb('#2c5a8a'),rgb('#1b3758'),rgb('#4d82b8')); jar(38,rgb('#8a2f2a'),rgb('#5a1d1a'),rgb('#b8473c')); jar(52,rgb('#b8801c'),rgb('#7a5212'),rgb('#e0ab3a'))
# spirit level
cv.rect(112,12,134,17,INK); cv.rect(113,13,133,16,W[8]); cv.hline(113,133,13,W[10]); cv.hline(113,133,15,W[6])
cv.rect(120,13,126,16,INK); cv.rect(121,14,125,15,rgb('#9fd27a')); cv.px(123,14,rgb('#e6ffd0'))
# bench vise: base plate, body, two serrated jaws, screw, T handle
cv.rect(126,12,155,17,INK); cv.rect(127,13,154,16,M[4]); cv.hline(127,154,13,M[7]); cv.hline(127,154,15,M[2])
for (x,y) in ((129,14),(151,14)): rivet(x-1,y-1)
cv.rect(131,6,152,14,INK); cv.rect(132,7,151,13,M[6]); cv.hline(132,151,7,M[8]); cv.vline(132,7,13,M[8]); cv.hline(133,151,12,M[3]); cv.vline(150,8,13,M[4])
cv.px(131,6,(0,0,0,0)); cv.px(151,6,(0,0,0,0))
cv.rect(124,8,132,12,INK); cv.rect(125,9,131,11,M[7]); cv.hline(125,131,9,M[10])                     # small anvil face
for (x0,x1) in ((133,141),(142,150)):                                                                # jaws
    cv.rect(x0,0,x1,8,INK); cv.rect(x0+1,1,x1-1,7,M[7]); cv.hline(x0+1,x1-1,1,M[10]); cv.vline(x0+1,1,7,M[9]); cv.hline(x0+1,x1-1,6,M[4])
    for x in range(x0+1,x1-1,2): cv.px(x,2,M[3])
cv.vline(141,1,7,M[1])                                                                                # gap between jaws
cv.rect(151,3,160,9,INK); cv.rect(152,4,159,8,M[6])
for x in range(152,159,2): cv.vline(x,4,8,M[3]); cv.px(x+1,4,M[9])                                  # thread
cv.rect(158,2,163,10,INK); cv.rect(159,3,162,9,M[8])                                                 # boss
cv.rect(159,0,165,28,INK); cv.rect(160,1,164,27,M[7]); cv.vline(160,1,27,M[9]); cv.vline(163,1,27,M[4])
cv.rect(158,0,166,3,INK); cv.rect(159,1,165,2,M[9]); cv.rect(158,25,166,28,INK); cv.rect(159,26,165,27,M[6])
Image.fromarray(cv.a).save(sys.argv[1] if len(sys.argv)>1 else 'v4/stage2-desk.png')
