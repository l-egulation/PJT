"""Author 32px terrain variants and assemble an isolated HTML map review."""
from pathlib import Path
from PIL import Image
import argparse, json, hashlib, shutil, subprocess, re
import numpy as np

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--study',type=Path,required=True);ap.add_argument('--preview-source',type=Path,required=True);ap.add_argument('--destination',type=Path,required=True);ap.add_argument('--asset',type=Path,required=True);args=ap.parse_args()
    p=args.destination.resolve();old=args.preview_source.resolve();asset=args.asset.resolve()
    if p.exists(): raise SystemExit('Use a new review destination')
    p.mkdir(parents=True);(asset/'source').mkdir(parents=True,exist_ok=True);(asset/'runtime').mkdir(exist_ok=True)
    for name in ['generated-original.png','prompt.txt']:
        shutil.copy2(args.study/name,asset/'source'/name)
    for key in ['assets','chunks','source','frames']:
        shutil.copytree(old/key,p/key)
    for f in ['layout.json','layout-data.js','geometry-data.js','movement-data.js','movement.js','foreground.js','foreground.py','windows.js','depth.js','viewer.js','package_preview.py','overview-labeled.png','overview-clean.png','wall-check.png','foreground-placement.png']:
        shutil.copy2(old/f,p/f)
    top=Image.open(args.study/'references/NativeTop.png').convert('RGBA');inner=Image.open(args.study/'references/NativeInner.png').convert('RGBA')
    for name in ['NativeTop.png','NativeInner.png','approved-player.png','approved-preview.png']:
        shutil.copy2(args.study/'references'/name,asset/'source'/name)
    atlas=Image.open(asset/'source/generated-original.png').convert('RGBA')
    strips=[atlas.crop((38,y,1498,y+212)).resize((320,64),Image.Resampling.NEAREST) for y in [120,379,636]]
    base=np.array(strips[0].crop((0,0,32,64))).astype(float)
    spec=[('plain-a',0,0,1),('plain-b',0,3,1),('plain-c',0,7,1),('moss-light-a',1,0,1),('moss-light-b',1,2,1),('moss-wide-2',2,2,2),('moss-wide-3',2,6,3)]
    motifs=[]
    for name,row,col,n in spec:
        a=np.array(strips[row].crop((col*32,0,(col+n)*32,64))).astype(float)
        # Lock shared outer seam pixels and walking highlight. Internal moss joins stay intact.
        for x in range(n*32):
            edge=min(x,n*32-1-x);weight=min(1,edge/4)
            a[:,x,:3]=a[:,x,:3]*weight+base[:,x%32,:3]*(1-weight)
            a[:2,x,:3]=base[:2,x%32,:3]
            # Finish in the original dark core, avoiding a rectangular lower edge.
            for y in range(42,64):
                w=(y-42)/21;a[y,x,:3]=a[y,x,:3]*(1-w)+np.array([29,41,42])*w
        a[:,:,3]=255;a[:,-1]=a[:,0];im=Image.fromarray(np.rint(a).astype('uint8'))
        im.save(asset/'runtime'/(name+'.png'));motifs.append(im)
    # Prove exact outer seam colors match for every pair of motifs.
    for a in motifs:
        for b in motifs:
            assert np.array_equal(np.array(a)[:,0],np.array(b)[:,-1])
    d=json.loads((p/'layout.json').read_text(encoding='utf-8'));src=json.loads((p/'source/stage1-seed-17.json').read_text(encoding='utf-8'));g=np.array(src['ground'],np.uint8).reshape(d['worldCells'][1],d['worldCells'][0]);rows,cols=g.shape
    chunks={};placements=[];counts={s[0]:0 for s in spec};seed=20261101;runid=0
    def getchunk(gx,gy):
        k=(gx,gy)
        if k not in chunks: chunks[k]=Image.open(p/'chunks'/f'{gx}-{gy}.png').convert('RGBA')
        return chunks[k]
    for y in range(1,rows-2):
        eligible=g[y].astype(bool)&g[y+1].astype(bool)&~g[y-1].astype(bool)
        x=0
        while x<cols:
            if not eligible[x]:x+=1;continue
            start=x
            while x<cols and eligible[x]:x+=1
            end=x
            if end-start<4:continue
            runid+=1;state=(seed+runid*2654435761)&0xffffffff;choices=[];cursor=start
            while cursor<end:
                state=(state*1664525+1013904223)&0xffffffff;v=state%100
                idx=state%3 if v<60 else 3+state%2 if v<87 else 5+state%2
                if choices and (choices[-1][0]==idx or choices[-1][0]>=5 and idx>=5):idx=(idx+1)%3
                n=spec[idx][3]
                if cursor+n>end:idx=state%3;n=1
                choices.append((idx,cursor,n));counts[spec[idx][0]]+=1;placements.append({'cell':[cursor,y],'motif':spec[idx][0],'cells':n,'run':runid});cursor+=n
            for idx,cx,n in choices:
                for j in range(n):
                    px,py=(cx+j)*32,y*32;tile=np.array(motifs[idx].crop((j*32,0,j*32+32,64))).astype(float)
                    # Blend only into existing geometric endcaps, preserving their alpha and silhouette.
                    for dy in range(64):
                        for dx in range(32):
                            wx,wy=px+dx,py+dy;chunk=getchunk(wx//1024,wy//768);ox,oy=wx%1024,wy%768;original=chunk.getpixel((ox,oy))
                            edge=min((cx+j-start)*32+dx,(end-cx-j)*32-1-dx);blend=min(1,max(0,edge/32))
                            rgb=tuple(round(original[k]*(1-blend)+tile[dy,dx,k]*blend) for k in range(3))
                            chunk.putpixel((ox,oy),rgb+(original[3],))
    for (gx,gy),im in chunks.items():
        original=Image.open(old/'chunks'/f'{gx}-{gy}.png').convert('RGBA')
        assert im.getchannel('A').tobytes()==original.getchannel('A').tobytes(),'Changed collision silhouette'
        im.save(p/'chunks'/f'{gx}-{gy}.png')
    (p/'placements.json').write_text(json.dumps({'seed':seed,'runs':runid,'counts':counts,'placements':placements},ensure_ascii=False,indent=2),encoding='utf-8')
    # Existing depth composition and movement are reused without altering the level geometry.
    renderer=(old/'render_depth.py').read_text(encoding='utf-8');(p/'render_depth.py').write_text(renderer,encoding='utf-8')
    subprocess.run(['python',str(p/'render_depth.py')],check=True)
    html=(old/'source-view.html').read_text(encoding='utf-8').replace('v19','이끼 타일 변주').replace('지하수로 · 캐릭터 이동 미리보기','지하수로 · 랜덤 타일 배치')
    html=html.replace('<main>','<main><p>기본 돌·드문 이끼·2/3칸 연결 이끼를 고정 시드로 배치했습니다. 구간을 바꾸거나 직접 이동해 반복감과 캐릭터 가독성을 확인하세요.</p>',1)
    html=re.sub(r'<a href="[^"]+">([^<]*)</a>',r'\1',html)
    (p/'source-view.html').write_text(html,encoding='utf-8');subprocess.run(['python',str(p/'package_preview.py')],check=True)
    manifest={'status':'user-selected-style; assembled HTML review; Unity integration pending','cellPx':32,'ppu':32,'seed':seed,'motifs':[{'name':name,'cells':n,'file':'runtime/'+name+'.png','sha256':sha(asset/'runtime'/(name+'.png'))} for name,row,col,n in spec],'horizontalSeams':'all outer edge RGBA pixels identical; internal 2/3-cell order preserved','coverage':'straight floor runs >=4 cells; existing endcaps blended over1cell; stairs/walls/ceilings retain original geometry-aware tiles','limits':'32px motifs derived from selected generated study. Full new wall/corner/ceiling atlas is not authored.','unityApplied':False}
    (asset/'source/recipe.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (p/'validation.json').write_text(json.dumps({'modifiedChunks':len(chunks),'floorRuns':runid,'motifCounts':counts,'outerSeamsPassed':True,'allChunkAlphaUnchanged':True,'geometrySha256':sha(p/'geometry-data.js'),'sourceGeometrySha256':sha(old/'geometry-data.js'),'browserVerified':False,'unityApplied':False},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Completed',runid,'floor runs;',len(placements),'motif groups;',len(chunks),'chunks',flush=True)

if __name__=='__main__':main()
