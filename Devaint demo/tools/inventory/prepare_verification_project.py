"""Copy a fresh complete project for inventory verification with the pinned editor.

Original settings and assets are read only. Shipping static HUD/Galmuri fonts are
copied; legacy raw system TTFs that stall the importer are excluded in this snapshot.
"""
import argparse
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEGACY_FONTS = {"arial.ttf", "malgun.ttf", "malgunbd.ttf"}


def ignore_missing_meta(directory, names):
    folder = Path(directory)
    ignored = {name for name in names if name.endswith(".meta") and not (folder / name[:-5]).exists()}
    if folder.as_posix().endswith("/Deviant/UI/Fonts"):
        ignored.update(name for name in names if name in LEGACY_FONTS or name.removesuffix(".meta") in LEGACY_FONTS)
    return list(ignored)


def prepare(output=None):
    project = Path(output).resolve() if output else ROOT / "outputs/inventory-verification/UnityProject"
    if project.exists():
        raise SystemExit("Use --output with a new directory; existing snapshots are never overwritten: " + str(project))
    project.mkdir(parents=True)
    for module in ("Assets", "Packages", "ProjectSettings"):
        shutil.copytree(ROOT / "unity" / module, project / module,
                        ignore=ignore_missing_meta if module == "Assets" else None)
    print(project)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="A new snapshot directory")
    prepare(parser.parse_args().output)
