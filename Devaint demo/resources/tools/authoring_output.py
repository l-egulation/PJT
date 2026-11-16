"""Authoring builds go to a fresh review directory, never directly into a consumer checkout."""
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKBENCH = ROOT / 'authoring/workbench/source'


def destination(description):
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument('--destination', type=Path, required=True,
                        help='New/empty review output directory; not a Unity or resource checkout')
    return prepare_output(parser.parse_args().destination)


def prepare_output(path):
    output = path.resolve()
    if output == ROOT or ROOT.is_relative_to(output) or output.is_relative_to(ROOT):
        raise ValueError('Authoring output must be outside the source repository')
    for parent in output.parents:
        if (parent / 'ProjectSettings/ProjectVersion.txt').is_file() and output.is_relative_to(parent / 'Assets'):
            raise ValueError('Authoring output must not be inside Unity Assets')
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError('Preserve existing review output; choose a new/empty directory')
    output.mkdir(parents=True, exist_ok=True)
    return output
