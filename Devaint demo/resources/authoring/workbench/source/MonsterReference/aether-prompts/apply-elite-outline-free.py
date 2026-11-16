"""Apply native outline-free colors to the elite, preserving geometry and FX."""
import argparse, importlib.util, json, shutil, io, base64
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('outline',Path(__file__).with_name('apply-normal-outline-free.py'))
u=importlib.util.module_from_spec(spec);spec.loader.exec_module(u)
DEST=ROOT/'approved';BACKUP=ROOT/'local-assets/99_archive/before-elite-outline-free-motions-v1'
COMPARISON=ROOT/'local-assets/99_archive/before-elite-outline-free-motions-v3'
OUT=ROOT/'local-assets/elite-outline-free-motions-v3';MID='04_well-warden'
POLICY={**u.POLICY,'version':'elite-outline-free-motions-v3','alphaUnchanged':False,'tinyGapRepair':True,'internalDetailUnchanged':True}
POLICY.pop('broomRimMaximumTone',None)
spec2=importlib.util.spec_from_file_location('tidy',Path(__file__).with_name('tidy-elite-rim.py'))
tidy=importlib.util.module_from_spec(spec2);spec2.loader.exec_module(tidy)
def main():
    argparse.ArgumentParser(description=__doc__).parse_args()
    OUT.mkdir(exist_ok=True,parents=True)
    if not BACKUP.exists():
        BACKUP.mkdir(parents=True);shutil.copytree(DEST/MID,BACKUP/MID);shutil.copy2(DEST/'manifest.json',BACKUP/'manifest.json')
    current=u.read(DEST/'manifest.json');old=u.read(BACKUP/'manifest.json')
    mon=next(m for m in current['monsters'] if m['id']==MID)
    palette=np.array([list(bytes.fromhex(c[1:])) for c in current['palette']],dtype=np.uint8)
    cache={};processed=set();checks=[];preview={'id':MID,'name':mon['name'],'motions':{}}
    def clean(im, fill_gaps=True):
        key=(u.digest(im),fill_gaps)
        if key not in cache:cache[key]=tidy.tidy(im,u.clean(im,palette,MID),palette,fill_gaps)
        return cache[key].copy()
    def save(m,ims):
        data=u.read(BACKUP/m['editorJSON'])
        for key,im,path in zip(data.get('sourceFrames',[0]),ims,m['frames']):
            im.save(DEST/path)
            if 'images' in data:data['images'][str(key)]=u.encode(im)
            else:data['image']=u.encode(im)
        data['outlineFree']=POLICY;data['paletteLock']=True;u.write(DEST/m['editorJSON'],data);processed.add(m['editorJSON'])
        if m.get('sheet'):
            sheet=Image.new('RGBA',(ims[0].width*len(ims),ims[0].height))
            for i,im in enumerate(ims):sheet.paste(im,(i*im.width,0))
            sheet.save(DEST/m['sheet'])
        m['outlineFree']=POLICY
    for key,m in mon['motions'].items():
        originals=[u.rgba(BACKUP/f) for f in m['frames']]
        ims=[clean(im, key!="death") for im in originals]
        if key=='death':
            body=m['layers']['body'];fx=m['layers']['fx'];newbody=[clean(u.rgba(BACKUP/f),False) for f in body['frames']]
            for path,nb in zip(body['frames'],newbody):
                ob=np.array(u.rgba(BACKUP/path));nbarr=np.array(nb)
                from scipy import ndimage as ndi
                inner=ndi.distance_transform_cdt(ob[:,:,3]>0,metric='chessboard')>2
                assert np.array_equal(ob[inner],nbarr[inner])
            save(body,newbody);processed.add(fx['editorJSON'])
            for i,original in enumerate(originals):
                # Apply only body recolors visible in the original composite.
                oldbody=Image.new('RGBA',original.size);oldbody.paste(u.rgba(BACKUP/body['frames'][i]),tuple(m['bodyOffset']))
                newcanvas=Image.new('RGBA',original.size);newcanvas.paste(newbody[i],tuple(m['bodyOffset']))
                a=np.array(original);b=np.array(oldbody);nb=np.array(newcanvas);f=np.array(u.rgba(BACKUP/fx['frames'][i]))
                edit=(b[:,:,3]>0)&(f[:,:,3]==0)&np.all(a==b,axis=2)
                a[edit]=nb[edit];ims[i]=Image.fromarray(a)
        save(m,ims)
        for before,after in zip(originals,ims):
            a=np.array(before);b=np.array(after);changed=np.any(a!=b,axis=2)
            assert before.size==after.size
            assert np.all(b[a[:,:,3]>0,3]==255)
            from scipy import ndimage as ndi
            depth=ndi.distance_transform_cdt(a[:,:,3]>0,metric='chessboard')
            if key!='death':assert not np.any(changed & (depth>2)),(key,int(np.sum(changed & (depth>2))))
            if key=='death':assert np.array_equal(a[:,:,3],b[:,:,3])
            assert set(map(tuple,b[b[:,:,3]>0,:3]))<=set(map(tuple,palette))
        checks.append({'motion':key,'frames':len(ims),'addedPixels':sum(int(np.sum((np.array(y)[:,:,3]>0)&(np.array(x)[:,:,3]==0))) for x,y in zip(originals,ims)),'changedPixels':sum(int(np.any(np.array(x)!=np.array(y),axis=2).sum()) for x,y in zip(originals,ims))})
        preview['motions'][key]={'before':[u.encode(u.rgba(COMPARISON/f)) for f in m['frames']],'after':[u.encode(x) for x in ims],'ms':m['ms'],'loop':m['loop'],'pivot':m['pivot']}
    # These two editable subsets share the full attack poses; don't process idle
    # construction parts because their cut edges are not external silhouettes.
    for name in ['hook-until-pull.json','claw-followup.json']:
        p=Path(MID)/'attack'/name;d=u.read(BACKUP/p)
        for key,value in d['images'].items():d['images'][key]=u.encode(clean(u.rgba(io.BytesIO(base64.b64decode(value.split(',')[1])))))
        d['outlineFree']=POLICY;u.write(DEST/p,d)
    for p in (BACKUP/MID/'death/fx').rglob('*'):
        if p.is_file():assert p.read_bytes()==(DEST/p.relative_to(BACKUP)).read_bytes()
    originalmon=next(m for m in old['monsters'] if m['id']==MID)
    for k,m in mon['motions'].items():
        for field in ['width','height','frameCount','ms','loop','ppu','pivot','frames','sheet']:
            assert m.get(field)==originalmon['motions'][k].get(field)
    for m in current['monsters']:
        if m['id']!=MID:assert m==next(x for x in old['monsters'] if x['id']==m['id'])
    current['eliteOutlineFree']=POLICY;u.write(DEST/'manifest.json',current)
    u.write(OUT/'data.json',[preview]);u.write(OUT/'verification.json',checks)
    html=Path(__file__).with_name('normal-outline-free-preview.html').read_text(encoding='utf-8')
    html=html.replace('일반 몬스터','엘리트 몬스터').replace('이전 시안','정돈 전').replace('어두운 경계·빗자루 밝기 보정','잉크 가장자리 색 보정 후').replace('3종 · 기본 포함 175프레임','엘리트 · 기본 포함 114프레임')
    html=html.replace('기존 6색 · 원래 크기·실루엣·타이밍 유지','기존 6색 · 크기·타이밍 유지 · 잉크·금속 재질색 분리')
    html=html.replace('grid-template-columns:repeat(3,minmax(0,1fr))','grid-template-columns:1fr').replace('max-width:760px','max-width:1750px')
    html=html.replace('width="256" height="144"','width="960" height="208"').replace('256,144','960,208').replace('0,136,256,1','0,192,960,1').replace("ctx.drawImage(m.before[f.i],0,16);ctx.drawImage(m.after[f.i],128,16);", "const im=m.before[f.i],px=Math.round(m.pivot[0]*im.width),py=Math.round((1-m.pivot[1])*im.height);ctx.drawImage(im,330-px,192-py);ctx.drawImage(m.after[f.i],810-px,192-py);")
    html=html.replace('4,136-b[3]','380,192-b[3]').replace("256*zoom+'px'","960*zoom+'px'").replace("144*zoom+'px'","208*zoom+'px'")
    html=html.replace('zoom=2','zoom=1').replace('<option value="2">2배</option>','<option value="1">1배</option><option value="2">2배</option>')
    (OUT/'index.html').write_text(html,encoding='utf-8',newline='\n')
    # Common world canvas and pivot for differently sized attack/death frames.
    lut={tuple(c):i+1 for i,c in enumerate(palette)};gpal=[255]*3+palette.ravel().tolist();gpal += [0]*(768-len(gpal));gifs=[];durations=[]
    for k in ['idle','walk','attack','hit','death']:
        m=mon['motions'][k]
        for _ in range(2 if k in ['idle','walk'] else 1):
            for path,ms in zip(m['frames'],m['ms']):
                im=u.rgba(DEST/path);canvas=Image.new('RGBA',(480,208));canvas.paste(im,(330-round(m['pivot'][0]*im.width),192-round((1-m['pivot'][1])*im.height)))
                p=Image.new('P',canvas.size);p.putpalette(gpal);p.putdata([lut[tuple(v[:3])] if v[3] else 0 for v in np.array(canvas).reshape(-1,4)])
                gifs.append(p.resize((960,416),Image.Resampling.NEAREST));durations.append(ms)
        gifs.append(gifs[-1].copy());durations.append(650)
    gifs[0].save(OUT/(MID+'.gif'),save_all=True,append_images=gifs[1:],duration=durations,loop=0,optimize=False,disposal=1)
    # Key poses side by side for native-scale inspection.
    sheet=Image.new('RGBA',(960,900),'#efeee8');d=ImageDraw.Draw(sheet)
    for row,(key,idx) in enumerate([('idle',10),('walk',12),('attack',11),('hit',0),('death',10)]):
        m=mon['motions'][key];d.text((4,row*180),key+' '+str(idx+1),fill='black')
        for col,root in enumerate([COMPARISON,DEST]):
            im=u.rgba(root/m['frames'][idx]);sheet.alpha_composite(im,(col*480+330-round(m['pivot'][0]*im.width),row*180+174-round((1-m['pivot'][1])*im.height)))
    sheet.convert('RGB').save(OUT/'key-poses.png')
    print(json.dumps({'frames':sum(c['frames'] for c in checks),'checks':'no erased pixels/palette/internal detail/timing/FX/other monsters PASS','output':str(OUT)}))
if __name__=='__main__':main()
