"""Build a new review HTML using the pinned resources bundle; never overwrite the source."""
import argparse
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
sys.path.insert(0, str(REPO / 'tools/assets'))
import update_assets as launcher


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path, help='New authoring input: all nine PNGs')
    parser.add_argument('--tile-assets', type=Path, help='New authoring input: three terrain PNGs')
    parser.add_argument('--out', type=Path, required=True,
                        help='HTML in a new/empty review directory, e.g. outputs/map-editor-review/맵 편집기.html')
    args = parser.parse_args()
    launcher.resource_revision(REPO)
    store = launcher.backend(REPO).Store(REPO / 'resources')
    bundle = store.read({'asset': 'authoring.map-editor',
                         'path': 'authoring/map-editor/runtime/맵 편집기.html'})
    tools = REPO / 'resources/tools'
    sys.path.insert(0, str(tools))
    try:
        spec = importlib.util.spec_from_file_location('deviant_map_editor_builder', tools / 'build_map_editor.py')
        builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(builder)
        result = builder.build(ROOT, bundle, args.out, assets=args.assets, tile_assets=args.tile_assets)
    finally:
        sys.path.remove(str(tools))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
