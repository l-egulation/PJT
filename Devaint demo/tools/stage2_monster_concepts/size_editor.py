"""Build a self-contained local size/placement editor without touching source artwork."""
from pathlib import Path
import base64, json, shutil
from style_unification import OUT, SOURCE, ITEMS, sha


def uri(path):return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode('ascii')


def build(output=None, items=None, initial=None, namespace=None):
    out=output or OUT; items=items or ITEMS
    meta={m['id']:m for m in json.loads((out/'metadata.json').read_text(encoding='utf-8'))}
    layout=json.loads((out/'references/layout.json').read_text(encoding='utf-8'))
    scenes={s['id']:{'image':uri(out/'references'/f'{s["id"]}-game.png'),'foreground':[490-round((s['view'][0]-480)*.4375),-100]} for s in layout['scenes'] if s['id'] in list('ABCDE')}
    positions={'H3':(230,334),'B3':(322,288),'M3':(440,288),'R-selected':(230,352),'K4':(380,288),'A3':(326,288),'I4':(326,288)}
    groups={'H3':'ranged','B3':'melee','M3':'melee','R-selected':'ranged','K4':'melee','A3':'elite','I4':'boss'}
    monsters=[];defaults={};hashes={}
    for ident,m in items.items():
        before=out/ident/'previews/before-scale-study.png';after=out/ident/'cutouts/style-scale-study.png'
        monsters.append({'id':ident,'name':m[0],'role':m[1],'group':groups[ident],'before':uri(before),'after':uri(after)})
        defaults[ident]={'height':meta[ident]['afterReviewExtent'][1],'x':positions[ident][0],'foot':positions[ident][1],'visible':groups[ident]=='melee'}
        hashes[ident]={'before':sha(before),'after':sha(after)}
    if initial:
        defaults={m['id']:{k:m[k] for k in ['height','x','foot','visible']} for m in initial['monsters']}
    data={'scenes':scenes,'monsters':monsters,'defaults':defaults,'foreground':uri(out/'references/assets/foreground.png'),'initial':{k:initial[k] for k in ['scene','art','selected']} if initial else None,'storageNamespace':namespace}
    page=(SOURCE/'size_editor.html').read_text(encoding='utf-8').replace('__SIZE_EDITOR_DATA__',json.dumps(data,ensure_ascii=False).replace('<',r'\u003c')).replace('__SIZE_EDITOR_JS__',(SOURCE/'size_editor.js').read_text(encoding='utf-8'))
    assert '__SIZE_EDITOR_' not in page
    (out/'size-editor.html').write_text(page,encoding='utf-8')
    (out/'source').mkdir(exist_ok=True)
    for name in ['size_editor.py','size_editor.html','size_editor.js']:shutil.copy2(SOURCE/name,out/'source'/name)
    (out/'size-editor-assets.json').write_text(json.dumps({'logicalScreen':[960,540],'playerVisibleExtent':[66,64],'embeddedBackgrounds':list(scenes),'monsterSourceHashes':hashes,'standalone':True,'defaultSizesAreEditable':True},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'editor':str(out/'size-editor.html'),'bytes':(out/'size-editor.html').stat().st_size,'editableMonsters':len(monsters)},ensure_ascii=False))


if __name__=='__main__':build()
