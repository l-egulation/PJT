import numpy as np
def rgb(h): return (int(h[1:3],16),int(h[3:5],16),int(h[5:7],16),255)
C=[rgb(c) for c in ['#070b0e','#0b1216','#0f191e','#142228','#1a2c31','#21373a','#2a4443','#36534e','#46655a','#5a7b69','#75967d']]
W=[rgb(c) for c in ['#0b0b0d','#130f10','#1c1513','#281c16','#36261a','#46301e','#5b3d22','#764e28','#94632f','#b77d3a','#dba04c']]
INK=rgb('#06090b')
PAPER=[rgb(c) for c in ['#9c8a64','#c9b98f','#e4e2d0','#f3f0e2']]
STEEL=[rgb(c) for c in ['#2c3a3a','#4a5c5a','#6f817c','#8fa89a','#b9c6b8']]
def hsh(*a):
    v=2166136261
    for x in a: v=((v^(x&0xffffffff))*16777619)&0xffffffff
    v^=v>>13; v=(v*2246822519)&0xffffffff; v^=v>>16; return v
class Can:
    def __init__(s,w,h): s.w,s.h=w,h; s.a=np.zeros((h,w,4),np.uint8)
    def px(s,x,y,c):
        if 0<=x<s.w and 0<=y<s.h: s.a[y,x]=c
    def rect(s,x0,y0,x1,y1,c):   # end exclusive
        x0,x1=max(0,x0),min(s.w,x1); y0,y1=max(0,y0),min(s.h,y1)
        if x1>x0 and y1>y0: s.a[y0:y1,x0:x1]=c
    def hline(s,x0,x1,y,c): s.rect(x0,y,x1,y+1,c)
    def vline(s,x,y0,y1,c): s.rect(x,y0,x+1,y1,c)
    def get(s,x,y): return tuple(int(v) for v in s.a[y,x]) if 0<=x<s.w and 0<=y<s.h else (0,0,0,0)
    def circle(s,cx,cy,r,c):     # midpoint circle: run lengths change monotonically
        x,y,d=r,0,1-r
        while x>=y:
            for (a,b) in ((x,y),(y,x),(-y,x),(-x,y),(-x,-y),(-y,-x),(y,-x),(x,-y)): s.px(cx+a,cy+b,c)
            y+=1
            if d<0: d+=2*y+1
            else: x-=1; d+=2*(y-x)+1
    def disc(s,cx,cy,r,c):
        for y in range(-r,r+1):
            for x in range(-r,r+1):
                if x*x+y*y<=r*r+r*.8: s.px(cx+x,cy+y,c)
    def line(s,x0,y0,x1,y1,c):   # use with integer slopes only
        dx,dy=x1-x0,y1-y0; n=max(abs(dx),abs(dy))
        for i in range(n+1): s.px(round(x0+dx*i/n),round(y0+dy*i/n),c)
