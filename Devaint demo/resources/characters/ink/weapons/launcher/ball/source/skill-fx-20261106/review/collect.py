# 발사기 확정 연출 다섯 개(2026-11-06)의 프레임을 게임 규격으로 모은다. 원본 PNG 바이트를 그대로 복사한다(확대·재인코딩 없음).
#   python collect.py → out/<clip>/NN.png + out/clips.json(Unity Resources/LauncherSkillFx/clips.json 형식)
# 기준점은 Unity 식(왼쪽 아래 0..1)으로 적는다. 프레임 시간은 장마다(seconds). loopFrom: 끝까지 가면 그 장부터 되풀이.
import json, shutil
from pathlib import Path
from PIL import Image

O=Path(__file__).resolve().parent.parent;OUT=Path(__file__).resolve().parent/'out'
B=O/'launcher-basic-breakdown-20261106/candidate-v1'
L=O/'launcher-laser-skill-20261106/candidate-v2'
S=O/'launcher-shotgun-review-20261106/candidate-v1'
G=O/'launcher-grenade-review-20261106'
C=O/'launcher-charge-review-20261106'
P30=1/30

clips=[]
def add(name,owner,files,seconds,pivot_px=None,pivot=None,loop=False,loop_from=None):
    """files: PNG 경로 목록. pivot_px=(x,y) 왼쪽 위 기준 화소, 또는 pivot=(u,v) Unity 식."""
    assert len(files)==len(seconds),name
    d=OUT/name;d.mkdir(parents=True,exist_ok=True);frames=[]
    sizes=set()
    for i,f in enumerate(files):
        dst=d/f'{i:02d}.png';shutil.copyfile(f,dst);frames.append(f'LauncherSkillFx/{name}/{i:02d}')
        with Image.open(f) as im:sizes.add(im.size)
    assert len(sizes)==1,(name,sizes)
    w,h=sizes.pop()
    if pivot is None:pivot=(pivot_px[0]/w,1-pivot_px[1]/h)
    clips.append({'name':name,'owner':owner,'loop':loop,'loopFrom':-1 if loop_from is None else loop_from,
                  'pivot':[round(pivot[0],6),round(pivot[1],6)],'size':[w,h],'seconds':[round(s,6) for s in seconds],'frames':frames})
def seq(dir_,n,prefix='F',start=0):return [dir_/f'{prefix}{i:02d}.png' for i in range(start,start+n)]
def pfx(dir_,owner,name):
    m=json.loads((dir_/'meta.json').read_text(encoding='utf-8'))
    add(name,owner,[dir_/f'{i:02d}.png' for i in range(m['frames'])],[P30]*m['frames'],pivot=m['pivot'])

if OUT.exists():shutil.rmtree(OUT)
# 기본 공격 볼 탄환 (ball)
add('ball_pop','ball',seq(B/'pop',4),[0.025,0.03,0.035,0.04],(10,32))
add('ball_bullet','ball',seq(B/'bullet',4),[0.03,0.04,0.04,0.04],(24,8),loop=True,loop_from=1)
add('ball_hit','ball',seq(B/'hit',6),[0.03,0.03,0.04,0.04,0.05,0.05],(24,48))
# 레이저 (laser)
add('laser_pop','laser',seq(L/'pop',4),[0.02,0.03,0.035,0.04],(8,32))
add('laser_hold','laser',seq(L/'hold',2),[0.05,0.05],(4,12),loop=True)
add('laser_head','laser',seq(L/'head',2),[0.025,0.025],(16,16),loop=True)
add('laser_hit','laser',seq(L/'hit',6),[0.03,0.03,0.04,0.04,0.05,0.05],(24,56))
add('laser_beam','laser',seq(L/'beam',6),[0.08]*6,pivot=(0.0,0.5),loop=True)
add('laser_wall_hit','laser',seq(L/'wall',3),[0.03,0.04,0.05],(30,24))
add('laser_wall_hold','laser',seq(L/'wall',2,start=3),[0.05,0.05],(30,24),loop=True)
# 샷건 (shotgun)
add('shotgun_pellet','shotgun',seq(S/'pellet',2),[0.04,0.04],(18,4),loop=True)
add('shotgun_pop','shotgun',seq(S/'pop',4),[0.02,0.03,0.035,0.045],(6,32))
add('shotgun_dust','shotgun',seq(S/'dust',4),[0.04,0.05,0.06,0.07],(32,22))
add('shotgun_wind','shotgun',seq(S/'wind',2),[0.05,0.05],(8,28),loop=True)
add('shotgun_hit','shotgun',seq(S/'hit',5),[0.03,0.035,0.04,0.045,0.05],(16,36))
# 유탄 (grenade): 포탄·연기·총구·파편 탄은 v1, 폭발은 pixelfx v2
add('grenade_shell','grenade',seq(G/'candidate-v1/shell',2),[0.05,0.05],(8,5),loop=True)
add('grenade_smoke','grenade',seq(G/'candidate-v1/smoke',4),[0.05,0.05,0.06,0.07],(6,6))
add('grenade_pop','grenade',seq(G/'candidate-v1/pop',4),[0.025,0.035,0.045,0.06],(6,28))
add('grenade_frag','grenade',seq(G/'candidate-v1/frag',2),[0.04,0.04],(6,4),loop=True)
for k in ('main_air','main_ground','frag_air','frag_ground'):pfx(G/'pfx'/k,'grenade','grenade_'+k)
# 모아쏘기 (ball 형태의 미장착 D): 구슬 3단(v1 구슬 S1·S3·S4), 빨려드는 불티, 단계 반짝, 탄 3단, 큰 총구, 폭발 3단(pixelfx)
for step,src in ((1,1),(2,3),(3,4)):
    add(f'charge_orb{step}','ball',[C/f'candidate-v1/orb/S{src}F{n:02d}.png' for n in range(2)],[0.06,0.06],(14,14),loop=True)
add('charge_suck','ball',seq(C/'candidate-v1/suck',3),[0.05]*3,(24,24),loop=True)
add('charge_up','ball',seq(C/'candidate-v1/full',3),[0.03,0.04,0.05],(20,20))
for step in (1,2,3):
    add(f'charge_shot{step}','ball',[C/f'candidate-v1/shot/S{step-1}F{n:02d}.png' for n in range(2)],[0.04,0.04],(28,12),loop=True)
add('charge_pop_big','ball',seq(C/'candidate-v1/bigpop',4),[0.03,0.04,0.05,0.06],(8,36))
for step in (1,2,3):pfx(C/f'pfx/step{step}','ball',f'charge_burst{step}')

json.dump({'format':'deviant-launcher-skill-fx-v1','ppu':32,'clips':clips},open(OUT/'clips.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
print(len(clips),'clips',sum(len(c['frames']) for c in clips),'frames')
for c in clips:print(f"{c['name']:22}{len(c['frames']):3}  {c['size'][0]}x{c['size'][1]}  pivot {c['pivot']}")
