"""Prepare main-owned HUD bindings, import metadata and the explicit media selection."""
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[2]
GROUP = ROOT / 'resources/ui/combat-hud-expanded-20261106'
TARGET = ROOT / 'unity/Assets/Deviant/TemporaryGUI/Resources/ApprovedCombatHUD'
ASSET = 'ui.combat-hud-expanded-20261106'

def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant/' + path.relative_to(ROOT).as_posix()).hex

def main():
    sprites = json.loads((GROUP/'runtime/sprites.json').read_text(encoding='utf-8'))['sprites']
    catalog = json.loads((GROUP/'runtime/catalog.json').read_text(encoding='utf-8'))
    combat_catalog = json.loads((ROOT/'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json').read_text(encoding='utf-8'))
    weapon_names = {form['id']: form['name'] for form in combat_catalog['forms']}
    for weapon in catalog['weapons']:
        weapon['name'] = weapon_names[weapon['id']]
    launcher_path = ROOT/'data/combat/launcher_skills.json'
    launcher = json.loads(launcher_path.read_text(encoding='utf-8'))
    launcher_aliases = dict(launcher_default='basic', launcher_laser='laser', launcher_shotgun='shotgun', launcher_grenade='grenade')
    for skill in catalog['skills']:
        if skill['weaponId'] == 'launcher':
            current = launcher[launcher_aliases[skill['id']]]
            skill['runtimeId'] = current['id']
            skill['name'] = current['name']
            skill['cooldownSeconds'] = current['cooldown']
            if skill['id'] == 'launcher_default':
                skill['presentationName'] = weapon_names['launcher']
    layout = json.loads((GROUP/'runtime/layout-edited.json').read_text(encoding='utf-8'))
    TARGET.mkdir(parents=True, exist_ok=True)
    for folder in (TARGET, TARGET/'sprites'):
        folder.mkdir(exist_ok=True)
        meta=Path(str(folder)+'.meta')
        if not meta.exists():meta.write_text(f'fileFormatVersion: 2\nguid: {guid(folder)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n',encoding='utf-8')
    manifest_path=ROOT/'docs/assets/manifests/unity-deployment.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files']=[r for r in manifest['files'] if r['source']['asset']!=ASSET]
    for record in sprites:
        target=TARGET/'sprites'/Path(record['file']).name
        data=(GROUP/record['file']).read_bytes()
        if hashlib.sha256(data).hexdigest()!=record['sha256']:raise ValueError('Source changed: '+record['id'])
        manifest['files'].append(dict(target=target.relative_to(ROOT).as_posix(),bytes=len(data),sha256=record['sha256'],
            source=dict(asset=ASSET,path=(GROUP/record['file']).relative_to(ROOT/'resources').as_posix())))
        meta=Path(str(target)+'.meta')
        if not meta.exists():meta.write_text(f'''fileFormatVersion: 2
guid: {guid(target)}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  maxTextureSize: 2048
  textureSettings:
    filterMode: 1
    wrapU: 1
    wrapV: 1
  nPOTScale: 0
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 100
  spritePivot: {{x: 0.5, y: 0.5}}
  alphaIsTransparency: 1
  textureType: 8
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
''',encoding='utf-8')
    bindings=dict(sourceAsset=ASSET,sourceLayoutSha256=hashlib.sha256((GROUP/'runtime/layout-edited.json').read_bytes()).hexdigest(),
        launcherSource='data/combat/launcher_skills.json',launcherSourceSha256=hashlib.sha256(launcher_path.read_bytes()).hexdigest(),
        layers=[dict(id=k,**v) for k,v in layout['layers'].items()],groups=[dict(id=k,**v) for k,v in layout['groups'].items()],
        weapons=catalog['weapons'],skills=catalog['skills'])
    path=TARGET/'bindings.json';path.write_text(json.dumps(bindings,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    meta=Path(str(path)+'.meta')
    if not meta.exists():meta.write_text(f'fileFormatVersion: 2\nguid: {guid(path)}\nTextScriptImporter:\n  externalObjects: {{}}\n',encoding='utf-8')
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(sprites=len(sprites),layoutEntries=len(bindings['layers']))))

if __name__=='__main__':main()
