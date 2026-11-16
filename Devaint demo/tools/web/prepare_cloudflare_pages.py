#!/usr/bin/env python3
"""Stage a Unity Web build for Pages, with its large data payload hosted on R2."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
from urllib.parse import urlsplit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", type=Path, required=True)
    parser.add_argument("--dxt-build", type=Path, help="Optional DXT fallback build; --build is the BC7 build")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--assets-base-url", required=True)
    args = parser.parse_args()
    source = args.build.resolve()
    sources = [("bc7" if args.dxt_build else "dxt", source)]
    if args.dxt_build:
        sources.append(("dxt", args.dxt_build.resolve()))
    output = args.output.resolve()
    base_url = args.assets_base_url.rstrip("/")
    url = urlsplit(base_url)
    if url.scheme != "https" or not url.netloc or url.username or url.password or url.query or url.fragment:
        parser.error("--assets-base-url must be a public HTTPS bucket/custom-domain URL")
    for _, original in sources:
        if original == output or original in output.parents or output in original.parents:
            parser.error("--output must be separate from the original builds")
    if output.exists() and any(output.iterdir()):
        parser.error("--output must be empty; keep each prepared deployment in a separate directory")
    variants = []
    for name, original in sources:
        variants.append(inspect_build(parser, name, original, base_url))
    data = Path(variants[0]["data_file"])
    index = (source / "index.html").read_text()
    old_url = f'dataUrl: buildUrl + "/{data.name}"'
    pages = output / "pages"

    def ignore_data(directory, _names):
        return [data.name] if Path(directory) == data.parent else []

    shutil.copytree(source, pages, ignore=ignore_data)
    index = index.replace(old_url, "dataUrl: " + json.dumps(variants[0]["data_url"]))
    if len(variants) == 2:
        fallback = variants[1]
        fallback_source = Path(fallback["data_file"]).parent
        # Keep the page's URL and StreamingAssets path stable so browser save storage is unchanged.
        shutil.copytree(fallback_source, pages / "DxtBuild", ignore=lambda _d, _n: [Path(fallback["data_file"]).name])
        marker = 'var script = document.createElement("script");'
        if index.count(marker) != 1:
            parser.error("unexpected Unity template: script creation marker missing")
        choices = json.dumps({key: "DxtBuild/" + fallback[key] for key in ("loader", "framework", "wasm")})
        selector = '''// Prefer BC7 where both Unity and the browser support BPTC.
      var textureProbe = document.createElement("canvas");
      var textureGl = textureProbe.getContext("webgl2");
      var useBc7 = !!(textureGl && textureGl.getExtension("EXT_texture_compression_bptc"));
      // Unity 6000.6 does not guarantee BC7 in macOS WebGL. Keep its compatible DXT path.
      var isMac = /Macintosh|Mac OS X/.test(navigator.userAgent);
      if (textureGl) {
        var releaseProbe = textureGl.getExtension("WEBGL_lose_context");
        if (releaseProbe) releaseProbe.loseContext();
      }
      if (!useBc7 || isMac) {
        var fallbackFiles = CHOICES;
        loaderUrl = fallbackFiles.loader;
        config.frameworkUrl = fallbackFiles.framework;
        config.codeUrl = fallbackFiles.wasm;
        config.dataUrl = DATA_URL;
      }
      '''.replace("CHOICES", choices).replace("DATA_URL", json.dumps(fallback["data_url"]))
        index = index.replace(marker, selector + marker)
    (pages / "index.html").write_text(index)
    headers = ["/", "  Cache-Control: no-cache", "/index.html", "  Cache-Control: no-cache"]
    for pattern, content_type in [
        ("*.framework.js.unityweb", "application/javascript"),
        ("*.wasm.unityweb", "application/wasm"),
    ]:
        for path in pages.rglob(pattern):
            headers.extend([
                "/" + path.relative_to(pages).as_posix(),
                "  Content-Type: " + content_type,
                "  Content-Encoding: gzip",
            ])
    (pages / "_headers").write_text("\n".join(headers) + "\n")
    manifest = {**variants[0], "pages_directory": str(pages)}
    if len(variants) > 1:
        manifest["variants"] = variants
    (output / "r2-upload.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps(manifest, indent=2))


def inspect_build(parser, name, source, base_url):
    data_files = list((source / "Build").glob("*.data.unityweb"))
    if len(data_files) != 1 or not (source / "index.html").is_file():
        parser.error("expected index.html and exactly one Build/*.data.unityweb (gzip with fallback)")
    data = data_files[0]
    for path in (source / "Build").glob("*.unityweb"):
        with path.open("rb") as stream:
            if stream.read(2) != b"\x1f\x8b":
                parser.error(f"expected gzip compression, not another .unityweb encoding: {path.name}")
    for path in source.rglob("*"):
        if path.is_file() and path != data and path.stat().st_size > 25 * 1024 * 1024:
            parser.error(f"another file exceeds the Pages 25 MiB limit: {path.relative_to(source)}")
    index = (source / "index.html").read_text()
    old_url = f'dataUrl: buildUrl + "/{data.name}"'
    if index.count(old_url) != 1:
        parser.error("unexpected Unity HTML template; could not locate its single dataUrl setting")
    digest = hashlib.sha256()
    with data.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    sha256 = digest.hexdigest()
    object_key = f"releases/{sha256}/{data.name}"
    data_url = f"{base_url}/{object_key}"
    scripts = {}
    for key, pattern in [("loader", "*.loader.js"), ("framework", "*.framework.js.unityweb"), ("wasm", "*.wasm.unityweb")]:
        found = list((source / "Build").glob(pattern))
        if len(found) != 1:
            parser.error(f"expected exactly one {pattern} in {source}")
        scripts[key] = found[0].name
    if name == "dxt":
        for key, prop in [("loader", "loaderUrl ="), ("framework", "frameworkUrl:"), ("wasm", "codeUrl:")]:
            if not re.search(re.escape(prop) + r'\s*buildUrl\s*\+\s*"/' + re.escape(scripts[key]) + '"', index):
                parser.error(f"unexpected DXT fallback template {prop}")
    return {
        "variant": name,
        **scripts,
        "data_file": str(data),
        "object_key": object_key,
        "data_url": data_url,
        "sha256": sha256,
        "bytes": data.stat().st_size,
        "content_type": "application/octet-stream",
        "content_encoding": "gzip",
        "cache_control": "public, max-age=31536000, immutable",
    }


if __name__ == "__main__":
    main()
