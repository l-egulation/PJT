#!/usr/bin/env python3
"""Restore the licensed package locally without redistributing it or changing originals."""
import argparse
import hashlib
from pathlib import Path, PurePosixPath
import tarfile

PACKAGE_SHA256 = "04ee5667913bca16e0986783b5372aa5388338947922aa1b28c48c643510ef85"
PREFIX = PurePosixPath("Assets/CartoonVFX by Wallcoeur")


def restore(package, project):
    if hashlib.sha256(package.read_bytes()).hexdigest() != PACKAGE_SHA256:
        raise ValueError("Expected the verified product 382436 version 1.0 package; archive hash differs.")
    root = project / "Assets/ThirdParty/Wallcoeur"
    pending = {}
    with tarfile.open(package, "r:gz") as archive:
        members = {m.name: m for m in archive if m.isfile()}
        for name, member in members.items():
            if not name.endswith("/pathname"):
                continue
            path = PurePosixPath(archive.extractfile(member).read().decode("utf-8").splitlines()[0])
            if path.is_absolute() or ".." in path.parts or not path.is_relative_to(PREFIX):
                raise ValueError(f"Unexpected package path: {path}")
            relative = path.relative_to(PREFIX)
            if not relative.parts:
                continue
            guid = name.rsplit("/", 1)[0]
            for entry, suffix in [("asset", ""), ("asset.meta", ".meta")]:
                source = members.get(f"{guid}/{entry}")
                if source is None:
                    continue
                dest = Path(str(root / relative) + suffix)
                if dest in pending:
                    raise ValueError(f"Duplicate destination: {dest}")
                pending[dest] = archive.extractfile(source).read()
    # Preflight the whole archive before writing; never overwrite local edits.
    for dest, content in pending.items():
        if dest.is_symlink() or any(p.is_symlink() for p in dest.parents):
            raise ValueError(f"Symlink destination refused: {dest}")
        if dest.exists() and (not dest.is_file() or dest.read_bytes() != content):
            raise ValueError(f"Existing local content differs: {dest}")
    for dest, content in pending.items():
        dest.parent.mkdir(parents=True, exist_ok=True)
        if not dest.exists():
            dest.write_bytes(content)
    return len(pending)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2] / "unity")
    args = parser.parse_args()
    print(f"Verified and restored {restore(args.package, args.project)} files. No Unity import or catalog generation was run.")
