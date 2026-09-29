#!/usr/bin/env python3
"""Render K3s YAML structurally, without text substitutions."""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import Any

import yaml

TAG_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]*$")
NAMESPACE_RE = re.compile(r"^[a-z0-9]([-a-z0-9]*[a-z0-9])?$")


def render(value: Any, *, namespace: str, image_tag: str | None,
           event_consumers_image_name: str | None,
           job_name: str | None, root: bool = False) -> Any:
    if isinstance(value, list):
        return [render(item, namespace=namespace, image_tag=image_tag,
                       event_consumers_image_name=event_consumers_image_name,
                       job_name=job_name) for item in value]
    if not isinstance(value, dict):
        return value
    kind = value.get("kind") if root else None
    result: dict[str, Any] = {}
    for key, item in value.items():
        if key == "namespace" and isinstance(item, str):
            result[key] = namespace
        elif key == "image" and isinstance(item, str):
            image = item
            if (
                image_tag is not None
                and event_consumers_image_name is not None
                and image.startswith("docker.io/hanjjak/event-consumers:")
            ):
                image = f"docker.io/hanjjak/{event_consumers_image_name}:{image_tag}-event-consumers"
            elif image_tag is not None and (
                image.startswith("docker.io/hanjjak/game-api:")
                or image.startswith("docker.io/hanjjak/web:")
                or image.startswith("docker.io/hanjjak/admin-console:")
                or image.startswith("docker.io/hanjjak/event-consumers:")
            ):
                image = image.rsplit(":", 1)[0] + ":" + image_tag
            result[key] = image
        else:
            result[key] = render(item, namespace=namespace, image_tag=image_tag,
                                 event_consumers_image_name=event_consumers_image_name,
                                 job_name=job_name)
    if root and kind == "Namespace":
        result.setdefault("metadata", {})["name"] = namespace
    if root and kind == "Job" and job_name:
        result.setdefault("metadata", {})["name"] = job_name
    return result


def image_values(value: Any):
    if isinstance(value, dict):
        for key, item in value.items():
            if key == "image" and isinstance(item, str):
                yield item
            else:
                yield from image_values(item)
    elif isinstance(value, list):
        for item in value:
            yield from image_values(item)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--namespace", default="hanjjak")
    parser.add_argument("--image-tag")
    parser.add_argument("--event-consumers-image-name", choices=("web",))
    parser.add_argument("--job-name")
    args = parser.parse_args()
    if not NAMESPACE_RE.fullmatch(args.namespace):
        parser.error("--namespace must be a DNS label")
    if args.image_tag is not None and (args.image_tag == "latest" or not TAG_RE.fullmatch(args.image_tag)):
        parser.error("--image-tag must be a concrete tag other than latest")
    if args.job_name is not None and not NAMESPACE_RE.fullmatch(args.job_name):
        parser.error("--job-name must be a DNS label")

    with args.input.open("r", encoding="utf-8") as stream:
        documents = list(yaml.safe_load_all(stream))
    rendered = [render(document, namespace=args.namespace, image_tag=args.image_tag,
                       event_consumers_image_name=args.event_consumers_image_name,
                       job_name=args.job_name, root=True)
                for document in documents if document is not None]
    if any(
        image.endswith(":bootstrap") and image.startswith("docker.io/hanjjak/")
        for document in rendered for image in image_values(document)
    ):
        parser.error("rendered application image still uses bootstrap")
    yaml.safe_dump_all(rendered, sys.stdout, default_flow_style=False, sort_keys=False)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
