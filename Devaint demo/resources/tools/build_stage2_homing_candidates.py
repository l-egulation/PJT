"""Extract original Aether cells and build timing-only reviews; no final pixel cleanup."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'


def dump(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def make_candidates():
    p=GROUP/'preview/homing-candidates'
    p.mkdir(exist_ok=True)
    candidates=[
        {'id':'D','name':'연속 연결 · 기본','sourceFrames':list(range(4,22)),'durationsMs':[520]+[60]*8+[50]*8+[300],'phasesMs':{'prepare':940,'release':60,'settle':400,'hold':300},'projectileCueMs':1000,'note':'04~21을 한 장도 건너뛰지 않음. 처음 자세 520ms 뒤 손·소매가 50~60ms 간격으로 이어짐. 방출 후 기본 복귀 없음.'},
        {'id':'E','name':'연속 연결 · 빠른 동작','sourceFrames':list(range(4,22)),'durationsMs':[640]+[45]*8+[40]*8+[300],'phasesMs':{'prepare':955,'release':45,'settle':320,'hold':300},'projectileCueMs':1000,'note':'D와 같은 18장을 모두 사용. 처음 자세 640ms 뒤 손·소매를 40~45ms 간격으로 빠르게 연결. 전조 총 1초는 유지.'},
        {'id':'R','name':'원본 흐름 참고','sourceFrames':list(range(1,36)),'durationsMs':[50]*35,'phasesMs':{'prepare':550,'release':50,'settle':450,'hold':700},'projectileCueMs':600,'phaseLabels':{'hold':'원본 복귀'},'referenceOnly':True,'note':'01~35를 빠짐없이 50ms씩 재생한 원본 비교용. 시트에는 FPS 정보가 없어 20fps를 가정. 초반 발 이동·후반 팔 복귀 포함. 게임용 타이밍 후보가 아님.'},
    ]
    for c in candidates:
        c['frameCount']=len(c['sourceFrames'])
        c['totalMs']=sum(c['durationsMs'])
        c['repeat']=False
        assert c['totalMs']==sum(c['phasesMs'].values())
        assert sum(c['durationsMs'][:c['sourceFrames'].index(13)])==c['projectileCueMs']
        assert c['sourceFrames']==list(range(c['sourceFrames'][0],c['sourceFrames'][-1]+1))
    dump(p/'candidates.json',{'format':'stage2-homing-timing-candidates-v2','status':'continuous-source-timing-review-pending-user-selection','source':'../../source/homing-aether-01/aether-original.png','frameBase':'../../source/homing-aether-01/frames/','frameNumbering':'zero-based row-major; source index 5 = first row sixth column','sourceCellSize':[640,360],'logicalCanvas':[384,256],'commonTransform':{'sourceAnchor':[320,337],'outputPivot':[192,232],'scale':0.75,'rasterOffset':[-48,-21],'destinationSize':[480,270],'resampling':'nearest-neighbor','perFrameAutoFit':False,'perFrameFootLock':False,'note':'Same affine transform for every frame. Native source boot motion remains visible; no per-frame bone/leg locking.'},'previewMatte':{'scope':'browser memory only; source pixels unmodified; not final cleanup','whiteMinimum':250,'maximumChannelDifference':4,'classifyAtSourceResolution':True,'paletteCorrection':False},'excluded':{'frame0':'Smaller than generated animation; retain as source but exclude from all playback.','performanceVariantsOnly':'D/E exclude 01~03 leg settling and 22~35 automatic return. R deliberately shows all 01~35 for source comparison.'},'candidates':candidates})
    return candidates


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--input',type=Path,required=True)
    parser.add_argument('--candidates-only',action='store_true')
    args=parser.parse_args()
    if args.candidates_only:
        print(json.dumps(make_candidates(),ensure_ascii=False))
        return
    source=GROUP/'source/homing-aether-01'
    if source.exists():
        raise SystemExit('Refusing to overwrite an existing extraction.')
    source.mkdir()
    original=source/'aether-original.png'
    shutil.copy2(args.input,original)
    im=Image.open(original).convert('RGBA')
    assert im.size==(3840,2160),im.size
    frames=source/'frames'
    frames.mkdir()
    records=[]
    for i in range(36):
        x,y=i%6*640,i//6*360
        cell=im.crop((x,y,x+640,y+360))
        cell.save(frames/f'{i:02}.png')
        # Measurement mask only: excludes pale background noise, never edits extracted pixels.
        mask=Image.new('L',cell.size)
        mask.putdata([255 if min(p[:3])<200 and p[3]>0 else 0 for p in cell.get_flattened_data()])
        box=mask.getbbox()
        records.append({'sourceFrame':i,'cell':[x,y,640,360],'file':f'frames/{i:02}.png','foregroundMeasurementBounds':box})
    dump(source/'extraction.json',{'sourceName':args.input.name,'sourceBytes':original.stat().st_size,'sourceSha256':hashlib.sha256(original.read_bytes()).hexdigest(),'sourceSize':list(im.size),'grid':[6,6],'cellSize':[640,360],'numbering':'zero-based, row-major','frames':records,'pixelEdits':False})
    sample=[0,1,5,8,11,12,13,16,19,24,27,35]
    contact=Image.new('RGB',(1920,1536),'#e5e5e5')
    draw=ImageDraw.Draw(contact)
    for n,i in enumerate(sample):
        x,y=n%3*640,n//3*384
        contact.paste(Image.open(frames/f'{i:02}.png').convert('RGB'),(x,y+24))
        draw.text((x+12,y+4),f'Frame {i:02} / row {i//6+1} col {i%6+1}',fill='black')
    contact.save(source/'inspection-contact.png')
    make_candidates()
    print(json.dumps(records,ensure_ascii=False))


if __name__=='__main__':
    main()
