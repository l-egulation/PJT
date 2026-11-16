"""Bind approved resource clips to main-owned Unity paths and importer metadata."""
import json
from pathlib import Path
import uuid

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'resources/monsters/stage-01/normal/barrel-gunner/source'

def dump(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def export():
    art=json.loads((SOURCE/'presentation.json').read_text(encoding='utf-8'))
    deployment=json.loads((SOURCE/'deployment.json').read_text(encoding='utf-8'))
    manifest_path=ROOT/'docs/assets/manifests/unity-deployment.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    targets={r['target'] for r in deployment['files']}
    manifest['files']=[r for r in manifest['files'] if r['target'] not in targets]+deployment['files']
    for clip in art['clips']:
        for frame in clip['frames']:
            path=ROOT/'unity/Assets/Deviant/Stage1Monsters/Resources'/(frame['resource']+'.png')
            path.parent.mkdir(parents=True,exist_ok=True)
            meta=Path(str(path)+'.meta')
            if meta.exists():continue
            x,y=clip['pivot']
            meta.write_text(f'''fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  serializedVersion: 13
  internalIDToNameTable: []
  externalObjects: {{}}
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 1
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 0
  alignment: 9
  spritePivot: {{x: {x}, y: {y}}}
  spritePixelsToUnits: 32
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    textureFormat: -1
    textureCompression: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {uuid.uuid4().hex}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
  userData:
  assetBundleName:
  assetBundleVariant:
''',encoding='utf-8')
    dump(ROOT/'unity/Assets/ForestCombatV2/Resources/Chapter1NewMonsterArt.json',art)
    dump(manifest_path,manifest)
    print(f'Bound {len(art["clips"])} clips / {len(targets)} PNGs, preserved existing Unity GUIDs.')

if __name__=='__main__':export()
