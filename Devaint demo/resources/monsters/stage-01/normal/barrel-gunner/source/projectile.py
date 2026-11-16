def ink_blob(phase,falling):
    """Cohesive ink mass, softly changing silhouette, string and separated droplets."""
    image=Image.new('RGBA',(112,160))
    draw=ImageDraw.Draw(image)
    cx,cy=56,80
    rx=BALL_RADIUS*(1+.045*math.sin(phase))
    ry=BALL_RADIUS*(1.2 if falling else 1.08)
    points=[]
    for j in range(48):
        theta=j*math.tau/48
        lump=1+.065*math.sin(3*theta+phase*.8)+.035*math.sin(5*theta-phase*.6)
        # A heavier rounded lower half, with a narrower shoulder during descent.
        pear=1+.11*math.sin(theta) if falling else 1
        points.append((round(cx+math.cos(theta)*rx*lump*pear),round(cy+math.sin(theta)*ry*lump)))
    direction=-1 if falling else 1
    tail_y=cy+direction*(ry-4)
    bend=round(3*math.sin(phase*.7))
    draw.polygon([(cx-4,round(tail_y)),(cx-2+bend,round(tail_y+direction*12)),
                  (cx+bend,round(tail_y+direction*24)),(cx+2+bend,round(tail_y+direction*10)),
                  (cx+5,round(tail_y))],fill=INK)
    draw.polygon(points,fill=INK)
    # A broad low-contrast irregular fold, not a hard glossy specular spot.
    draw.polygon([(cx-10,cy-5),(cx-9,cy-10),(cx-5,cy-13),(cx-3,cy-11),
                  (cx-6,cy-7),(cx-6,cy-3),(cx-8,cy-1)],fill=colors[1])
    for j in range(3):
        x=cx+round(math.sin(phase*.65+j*1.7)*(4+j*2))
        y=cy+direction*round(ry+29+j*11+2*math.sin(phase+j))
        radius=2 if j==0 else 1
        draw.ellipse((x-radius,y-radius,x+radius,y+radius+1),fill=INK)
    rgba=np.array(image)
    assert set(map(tuple,rgba[rgba[:,:,3]>0,:3].tolist()))<=set(colors)
    return image
