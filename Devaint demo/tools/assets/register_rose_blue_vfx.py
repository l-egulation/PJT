"""Register the Rose/Blue boss VFX set (dark fantasy) in the main-owned deployment contract.

Reads resources/monsters/stage-02/rose-blue-boss-vfx-20261105/runtime/sheets.json, points every listed sheet at
unity/Assets/Chapter2/Resources/RoseBlueVfx/, writes their texture importers, and copies the sheet table to the
Unity binding RoseBlueVfxSheets.json. Run after pulling that resources commit's LFS files: python tools/assets/register_rose_blue_vfx.py
"""
from pathlib import Path
import hashlib, json, uuid

ROOT = Path(__file__).resolve().parents[2]
GROUP = 'monsters/stage-02/rose-blue-boss-vfx-20261105'
ASSET = 'monsters.stage-02.rose-blue-boss-vfx-20261105'
PREFIX = 'unity/Assets/Chapter2/Resources/RoseBlueVfx/'
BINDING = ROOT / 'unity/Assets/Chapter2/Resources/RoseBlueVfxSheets.json'
TEXTURE_META = '''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  externalObjects: {{}}
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 4096
  textureCompression: 0
  nPOTScale: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 1
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 1
  spritePixelsToUnits: 32
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
'''


def guid(target):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant-rose-blue-vfx/' + target).hex


def main():
    runtime = ROOT / 'resources' / GROUP / 'runtime'
    table = json.loads((runtime / 'sheets.json').read_text(encoding='utf-8'))
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files'] = [x for x in manifest['files'] if not x['target'].startswith(PREFIX)]
    targets = set()
    for sheet in table['sheets']:
        source = runtime / (sheet['name'] + '.png'); raw = source.read_bytes()
        if raw.startswith(b'version https://git-lfs'):
            raise SystemExit('Pull the LFS object first: ' + str(source))
        target = PREFIX + source.name; targets.add(target)
        manifest['files'].append({'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
                                  'source': {'asset': ASSET, 'path': GROUP + '/runtime/' + source.name}})
        meta = ROOT / (target + '.meta'); meta.parent.mkdir(parents=True, exist_ok=True)
        meta.write_text(TEXTURE_META.format(guid=guid(target)), encoding='utf-8', newline='\n')
    # Importers left from an earlier set would make Unity keep stale entries; only the listed sheets remain.
    for meta in (ROOT / PREFIX).glob('*.png.meta'):
        if PREFIX + meta.name[:-5] not in targets:
            meta.unlink()
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    BINDING.write_text(json.dumps({'source': GROUP + '/runtime/sheets.json', 'sheets': table['sheets']}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    binding_meta = BINDING.with_name(BINDING.name + '.meta')
    if not binding_meta.exists():
        binding_meta.write_text('fileFormatVersion: 2\nguid: ' + guid('RoseBlueVfxSheets.json') + '\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8', newline='\n')
    print(f'Registered {len(targets)} Rose/Blue VFX sheets')


if __name__ == '__main__':
    main()
