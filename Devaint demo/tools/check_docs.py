"""Check relative links and retired documentation paths without writing files."""
from pathlib import Path
import json
import re
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]


def main():
    # Current media manifests declare separately distributed images that may
    # be absent from a code-only checkout; retired snapshots are not sources.
    media = set()
    for manifest in sorted((ROOT / 'docs/assets/manifests').glob('*.json')):
        data = json.loads(manifest.read_text(encoding='utf-8-sig'))
        records = data.get('files', []) if isinstance(data, dict) else []
        if not isinstance(records, list):
            continue
        for record in records:
            if not isinstance(record, dict):
                continue
            value = record.get('target', record.get('targetPath', record.get('path', '')))
            if Path(value).suffix.lower() in ('.png', '.gif', '.jpg', '.jpeg', '.webp', '.zip'):
                media.add((ROOT / value).resolve())
    pages = [ROOT / 'README.md', ROOT / 'AGENTS.md', *sorted((ROOT / 'docs').rglob('*.md'))]
    errors = []
    count = 0
    for page in pages:
        text = page.read_text(encoding='utf-8-sig')
        # Fenced command examples are not rendered Markdown links.
        text = re.sub(r'```.*?```', '', text, flags=re.S)
        for value in re.findall(r'\[[^\]]*\]\(([^)]+)\)', text):
            url = urlsplit(value.strip('<>'))
            if url.scheme or url.netloc or not url.path:
                continue
            target = (page.parent / unquote(url.path)).resolve()
            count += 1
            if not target.exists() and target not in media:
                errors.append(f'{page.relative_to(ROOT)}: missing {value}')
        active_text = text
        for retired in ('docs/LostWords/', 'docs/verification/'):
            if retired in active_text:
                errors.append(f'{page.relative_to(ROOT)}: retired path {retired}')
    if errors:
        raise SystemExit('\n'.join(errors))
    print(f'PASS: {len(pages)} documents, {count} local links')


if __name__ == '__main__':
    main()
