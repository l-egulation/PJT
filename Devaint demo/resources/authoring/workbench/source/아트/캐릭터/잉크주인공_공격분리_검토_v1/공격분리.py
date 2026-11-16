from PIL import Image,ImageDraw
from pathlib import Path
import shutil,json
root=Path(__file__).resolve().parent
source=root.parent/'최종확정_잉크주인공_모션'/'공격'
sets={
 '공격1_올려베기':([1,2,3,4,5,6],[80,110,70,40,50,120]),
 '공격2_내려베기':([6,7,8,9,10,11,12],[40,100,40,110,100,110,120]),
 '공격1_복귀':([5,12,1],[80,100,80])}
config={'canvas':[128,128],'combo_window_ms':120,'queue_second_click_during_attack1':True,'shared_pose_source_frame':6,'clips':{}}
for name,(indices,times) in sets.items():
 p=root/name;p.mkdir(exist_ok=True);frames=[]
 sheet=Image.new('RGBA',(128*len(indices),128))
 for i,idx in enumerate(indices):
  original=source/f'{idx:02}.png';out=p/f'{i+1:02}.png';shutil.copyfile(original,out)
  im=Image.open(out).convert('RGBA');sheet.paste(im,(i*128,0));frames.append(im)
  assert out.read_bytes()==original.read_bytes()
 sheet.save(p/'스프라이트시트.png')
 visible=[]
 for im in frames:
  bg=Image.new('RGBA',(128,128),'#707070');bg.alpha_composite(im);visible.append(bg.convert('RGB').resize((512,512),Image.Resampling.NEAREST))
 visible[0].save(p/'미리보기.gif',save_all=True,append_images=visible[1:],duration=times,loop=0,disposal=2)
 config['clips'][name]={'source_frames':indices,'durations_ms':times,'total_ms':sum(times),'frame_count':len(indices)}
shutil.copyfile(root.parent/'최종확정_잉크주인공_모션'/'대기'/'01.png',root/'대기.png')
(root/'공격분리_재생설정.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'사용안내.txt').write_text('기존 확정 공격 PNG를 변경 없이 분리한 검토본. 공격1은 원본1~6, 공격2는 원본6~12. 공유자세 원본6. 연속 입력은 공격1 중 예약 가능하며 공격1 완료 후 공격2로 전환. 입력 없으면120ms 연결 대기 후 원본5,12,1로 복귀(기존 프레임 재배열 시안). 공격2는 완료 후 대기로 전환. 기존 확정 에셋은 덮어쓰지 않음. 각 클립은 억지로12장으로 늘리지 않음. 게임의 공격 판정과 콤보 입력 시간은 구현에서 별도 설정 필요.',encoding='utf-8')
print(json.dumps(config,ensure_ascii=False))



