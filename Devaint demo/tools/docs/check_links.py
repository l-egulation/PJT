#!/usr/bin/env python3
"""Check documentation targets, heading anchors and reachability from docs/README.md."""
from pathlib import Path
from urllib.parse import unquote, urlsplit
import argparse
import re
import unicodedata

ROOT = Path(__file__).resolve().parents[2]
MEDIA = {'.png', '.jpg', '.jpeg', '.webp', '.gif', '.wav', '.mp3', '.ogg', '.mp4', '.zip', '.psd'}


def anchors(text):
    result = set(re.findall(r'<a\s+(?:id|name)=["\']([^"\']+)', text))
    counts = {}
    fenced = False
    for line in text.splitlines():
        if line.startswith(('```', '~~~')):
            fenced = not fenced
        match = re.match(r'^#{1,6}\s+(.+?)\s*#*$', line)
        if fenced or not match:
            continue
        title = re.sub(r'\[([^]]+)\]\([^)]+\)', r'\1', match[1])
        slug = ''.join(c for c in title.lower()
                       if c in '-_ ' or unicodedata.category(c)[0] not in 'PS').replace(' ', '-')
        count = counts.get(slug, 0)
        counts[slug] = count + 1
        result.add(slug + (f'-{count}' if count else ''))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--strict-media', action='store_true', help='also fail on unrestored local media')
    args = parser.parse_args()
    docs = {p.resolve(): p.read_text(encoding='utf-8') for p in (ROOT / 'docs').rglob('*.md')}
    heading_cache = {p: anchors(text) for p, text in docs.items()}
    edges = {p: set() for p in docs}
    errors, media = [], []
    for path, text in docs.items():
        targets = re.findall(r'\]\(([^\s()]+)\)', text)
        targets += re.findall(r'^\[[^\]]+\]:\s*(\S+)', text, re.M)
        for target in targets:
            parsed = urlsplit(target.strip('<>'))
            if parsed.scheme or parsed.netloc or target.startswith('/'):
                continue
            dest = (path.parent / unquote(parsed.path)).resolve() if parsed.path else path
            label = f'{path.relative_to(ROOT)}: {target}'
            if not dest.exists():
                (media if dest.suffix.lower() in MEDIA else errors).append(label)
                continue
            if dest in docs:
                edges[path].add(dest)
                if parsed.fragment and unquote(parsed.fragment) not in heading_cache[dest]:
                    errors.append(label + ' (heading not found)')
    pending, seen = [(ROOT / 'docs/README.md').resolve()], set()
    while pending:
        path = pending.pop()
        if path not in seen:
            seen.add(path)
            pending.extend(edges.get(path, ()))
    # Existing one-off verification artifacts are not current navigation documents.
    for path in docs.keys() - seen:
        if 'verification' not in path.relative_to(ROOT / 'docs').parts:
            errors.append(f'{path.relative_to(ROOT)}: unreachable from docs/README.md')
    for item in errors:
        print('ERROR', item)
    for item in media:
        print('ERROR' if args.strict_media else 'MEDIA NOT RESTORED', item)
    print(f'{len(docs)} Markdown documents; {len(errors)} link/navigation errors; {len(media)} missing media targets')
    return int(bool(errors or (args.strict_media and media)))


if __name__ == '__main__':
    raise SystemExit(main())
