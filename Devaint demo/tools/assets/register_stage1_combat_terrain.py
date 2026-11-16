"""Deploy the approved stage-1 lawn tiles, native background, sky and boss approach / boss room rear art into Unity Resources with pixel-exact import settings."""
import hashlib
import json
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = 'unity/Assets/Stage1VillageExperiment/Resources/Stage1Combat/'
LAWN = ('environments.stage1-pixel-lawn-wood-32', 'environments/stage1-pixel-lawn-wood-32/runtime/')
NATIVE = ('environments.stage1-pixel-background-native-20261111', 'environments/stage1-pixel-background-native-20261111/runtime/')
SKY = ('environments.stage1-atmosphere-sky-ash-20261111', 'environments/stage1-atmosphere-sky-ash-20261111/runtime/')
BOSS = ('environments.stage1-boss-approach-pixel-20261112', 'environments/stage1-boss-approach-pixel-20261112/runtime/')
# the backgrounds, sky and rose window come in the floor's cream with depth (user-chosen option C)
CREAM = ('environments.stage1-background-cream-20261112', 'environments/stage1-background-cream-20261112/runtime/')

CENTER, ORIGIN = ('0.5', '0.5'), ('0', '0')
# target (relative to TARGET): (source package, file inside runtime/, pivot, repeat wrap)
FILES = {'terrain-atlas.png': (LAWN, 'terrain-atlas.png', CENTER, False)}
for folder, names in (('decor', ['daisy', 'embedded-pebble-0', 'embedded-pebble-1', 'embedded-pebble-2', 'wall-blade-left', 'wall-blade-right']),
                      ('junctions', ['inside-left-down', 'inside-left-up', 'inside-right-down', 'inside-right-up']),
                      ('wood', ['left', 'middle-a', 'middle-b', 'right', 'single'])):
    for name in names:
        FILES[f'{folder}/{name}.png'] = (LAWN, f'{folder}/{name}.png', CENTER, False)
# the pencil patches are both decoration sprites and the repeating hatch of the soil back walls
for i in range(4):
    FILES[f'decor/pencil-patch-{i}.png'] = (LAWN, f'decor/pencil-patch-{i}.png', CENTER, True)
# the bands are the redrawn village (one drawing with the village's colour version) in its drained cream state:
# combat stages show the drained world, so they share the village's sketch bands dot for dot
COLOUR = ('environments.stage1-background-color-20261112', 'environments/stage1-background-color-20261112/runtime/')
for name in ('near-village', 'far-village', 'mountains'):
    FILES[f'background/{name}.png'] = (COLOUR, f'sketch/{name}.png', ORIGIN, False)
for name in ('approach-colonnade', 'boss-room-towers', 'boss-room-rose-window'):
    FILES[f'boss/{name}.png'] = (CREAM if name == 'boss-room-rose-window' else BOSS, f'{name}.png', ORIGIN, False)
for folder in ('clouds', 'birds'):
    for i in range(6):
        FILES[f'sky/{folder}/{folder}-{i}.png'] = (CREAM, f'sky/{folder}/{folder}-{i}.png', CENTER, False)

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 9
  spritePivot: {{x: {px}, y: {py}}}
  spritePixelsToUnits: 32
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationMethod: 0
  spriteTessellationDetail: -1
  spriteGeometrySubdivision: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def folder_meta(path):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n'
                        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    for rel, ((asset, base), src, pivot, repeat) in FILES.items():
        raw = (ROOT / 'resources' / (base + src)).read_bytes()
        target = TARGET + rel
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
        old = next((item for item in manifest['files'] if item['target'] == target), None)
        if old:                                   # an updated picture may replace the bytes deployed before
            prior = set([old['sha256']] + old.get('replaceHashes', [])) - {row['sha256']}
            if prior: row['replaceHashes'] = sorted(prior)
        row['source'] = {'asset': asset, 'path': base + src}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None: manifest['files'].append(row)
        else: manifest['files'][at] = row
        path = ROOT / target
        path.parent.mkdir(parents=True, exist_ok=True)
        parent = path.parent
        while parent != ROOT / 'unity/Assets/Stage1VillageExperiment/Resources':
            folder_meta(parent); parent = parent.parent
        meta = Path(str(path) + '.meta')
        if not meta.exists():  # Unity re-serializes the meta on import; keep that form once it exists
            meta.write_text(META.format(guid=uuid.uuid4().hex, wrap=0 if repeat else 1, px=pivot[0], py=pivot[1]), encoding='utf-8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(len(FILES), 'files')


if __name__ == '__main__':
    register()
