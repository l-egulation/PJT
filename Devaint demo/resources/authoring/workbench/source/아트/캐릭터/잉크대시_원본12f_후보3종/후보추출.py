import sys
if '--rebuild-retired' not in sys.argv:
    print('Use --rebuild-retired explicitly for old A/B/C candidates. Current motion: WeaponReference/LatestMotion/index.html')
    raise SystemExit(0)
from pathlib import Path
from PIL import Image, ImageChops
import json, shutil

ROOT = Path(__file__).resolve().parent
SOURCE = Path(r'C:/Users/SSAFY/Downloads/5a31bc44-fa22-4a30-8be5-c205c09bec2a.png')
GIF_SOURCE = Path(r'C:/Users/SSAFY/Downloads/a213e20e-db8f-4741-93e2-d2feabf74616.gif')
CHOICES = {
    'A': {'label': '균형형', 'note': '준비·낮은 대시·복귀를 고르게 배분', 'frames': [1,5,7,9,11,14,17,20,22,25,28,36]},
    'B': {'label': '낮은 자세 유지', 'note': '빠르게 낮아지고 대시 자세에 더 많은 프레임 배분', 'frames': [1,6,8,9,10,12,16,20,22,26,29,36]},
    'C': {'label': '자세 전환 중시', 'note': '몸을 낮추는 초반과 대기 복귀의 변화를 세밀하게 선택', 'frames': [1,5,6,7,8,10,14,19,22,25,28,36]},
}

def main():
    source_folder = ROOT / '원본'
    source_folder.mkdir(parents=True, exist_ok=True)
    for source in (SOURCE, GIF_SOURCE):
        shutil.copy2(source, source_folder / source.name)
    sheet = Image.open(SOURCE).convert('RGBA')
    assert sheet.size == (3840,2160)
    assert Image.open(GIF_SOURCE).n_frames == 36
    cells = [sheet.crop((i%6*640, i//6*360, (i%6+1)*640, (i//6+1)*360)) for i in range(36)]
    gif = Image.open(GIF_SOURCE)
    durations=[]
    for i in range(gif.n_frames):
        gif.seek(i)
        durations.append(gif.info.get('duration',0))
    for key, choice in CHOICES.items():
        folder = ROOT / key
        folder.mkdir(exist_ok=True)
        combined=Image.new('RGBA',(2560,1080))
        previews=[]
        for j, original in enumerate(choice['frames']):
            cell=cells[original-1]
            cell.save(folder/f'{j+1:02}.png')
            combined.paste(cell,(j%4*640,j//4*360))
            assert ImageChops.difference(Image.open(folder/f'{j+1:02}.png'),cell).getbbox() is None
            gif.seek(original-1)
            previews.append(gif.convert('RGB').copy())
        combined.save(folder/'12프레임.png')
        previews[0].save(folder/'미리보기.gif', save_all=True, append_images=previews[1:], duration=50, loop=0, optimize=False, disposal=2)
        choice['durations_ms']=[50]*12
        choice['total_ms']=600
        choice['source_timestamps_ms']=[sum(durations[:i-1]) for i in choice['frames']]
        (folder/'프레임선정.json').write_text(json.dumps(choice,ensure_ascii=False,indent=2),encoding='utf-8')
    manifest={'version':1,'source_grid':[6,6],'frame_canvas':[640,360], 'output_grid':[4,3], 'source_frame_count':36, 'source_durations_ms':durations, 'candidates':CHOICES, 'notes':'원본 PNG 시트를 정확히 분할한 후보. 픽셀·팔레트·이펙트 보정 전. GIF는 원본 GIF에서 대응 프레임을 추출한 재생 참고본. HTML은 원본 PNG 프레임으로 재생.'}
    (ROOT/'후보설정.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'후보데이터.js').write_text('const DASH_CANDIDATES='+json.dumps(manifest,ensure_ascii=False)+';\n',encoding='utf-8')
    print('검증 완료: A/B/C 각 12프레임, 원본 36프레임의 6×6 시트에서 픽셀 변경 없이 추출.')

if __name__ == '__main__':
    main()
