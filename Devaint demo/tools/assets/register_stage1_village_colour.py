"""Deploy the stage-1 village and tutorial-road pixel art in its two states into Unity Resources (Hub/pixel-village/).

The village draws every piece twice (colour and sketch) and erases the colour in the opening, so each picture is
deployed as a pair: sketch = the drained pixel art (lawn tiles, wood, decor, cream bands, cream sky, clouds and birds),
colour = the user-approved colour version (hollow chapter-2 style ground, user's colour bands, sky, clouds and birds)."""
import hashlib
import json
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = 'unity/Assets/Deviant/Hub/Resources/Hub/pixel-village/'
LAWN = ('environments.stage1-pixel-lawn-wood-32', 'environments/stage1-pixel-lawn-wood-32/runtime/')
CREAM = ('environments.stage1-background-cream-20261112', 'environments/stage1-background-cream-20261112/runtime/')
COLOUR = ('environments.stage1-background-color-20261112', 'environments/stage1-background-color-20261112/runtime/')

CENTER, ORIGIN, SURFACE = ('0.5', '0.5'), ('0', '0'), ('0', '0.75')   # SURFACE: tile tops are lifted 8 px
# target (relative to TARGET): (source package, file inside runtime/, pivot, readable)
FILES = {
    'ground/sketch/terrain-atlas.png': (LAWN, 'terrain-atlas.png', CENTER, True),
    'ground/color/terrain-atlas.png': (COLOUR, 'village/ground/terrain-atlas.png', CENTER, True),
}
for name in ('left', 'middle-a', 'middle-b', 'right', 'single'):
    FILES[f'ground/sketch/wood/{name}.png'] = (LAWN, f'wood/{name}.png', SURFACE, False)
    FILES[f'ground/color/wood/{name}.png'] = (COLOUR, f'village/ground/wood/{name}.png', SURFACE, False)
for name in ('daisy', 'embedded-pebble-0', 'embedded-pebble-1', 'embedded-pebble-2'):
    FILES[f'ground/sketch/decor/{name}.png'] = (LAWN, f'decor/{name}.png', CENTER, False)
    FILES[f'ground/color/decor/{name}.png'] = (COLOUR, f'village/ground/decor/{name}.png', CENTER, False)
# the sketch bands are the same cleaned drawing as the colour ones, in cream (so both coincide when overlapped)
FILES['background/sketch/near-village.png'] = (COLOUR, 'sketch/near-village.png', ORIGIN, False)
FILES['background/color/near-village.png'] = (COLOUR, 'near-village.png', ORIGIN, False)
# the far bands carry their bottom row 3 units further down (the combat stages stretch it to the floor)
for name in ('mountains', 'far-village'):
    for state in ('sketch', 'color'):
        FILES[f'background/{state}/{name}.png'] = (COLOUR, f'village/background/{state}/{name}.png', ORIGIN, False)
for name, pivot in (('sky', CENTER), ('clouds', ORIGIN), ('bird', CENTER)):
    FILES[f'sky/sketch/{name}.png'] = (COLOUR, f'village/sky/{name}-sketch.png', pivot, False)
    FILES[f'sky/color/{name}.png'] = (COLOUR, f'village/sky/{name}.png', pivot, False)

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
  isReadable: {readable}
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
    for rel, ((asset, base), src, pivot, readable) in FILES.items():
        raw = (ROOT / 'resources' / (base + src)).read_bytes()
        target = TARGET + rel
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
        old = next((item for item in manifest['files'] if item['target'] == target), None)
        if old:
            prior = set([old['sha256']] + old.get('replaceHashes', [])) - {row['sha256']}
            if prior: row['replaceHashes'] = sorted(prior)
        row['source'] = {'asset': asset, 'path': base + src}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None: manifest['files'].append(row)
        else: manifest['files'][at] = row
        path = ROOT / target
        path.parent.mkdir(parents=True, exist_ok=True)
        parent = path.parent
        while parent != ROOT / 'unity/Assets/Deviant/Hub/Resources/Hub':
            folder_meta(parent); parent = parent.parent
        meta = Path(str(path) + '.meta')
        if not meta.exists():  # Unity re-serializes the meta on import; keep that form once it exists
            meta.write_text(META.format(guid=uuid.uuid4().hex, wrap=1, px=pivot[0], py=pivot[1], readable=1 if readable else 0),
                            encoding='utf-8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(len(FILES), 'files')


if __name__ == '__main__':
    register()
