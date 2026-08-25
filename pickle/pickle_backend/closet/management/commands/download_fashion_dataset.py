import json
import shutil
import zipfile
from datetime import datetime, timezone
from pathlib import Path

import requests
from django.conf import settings
from django.core.management.base import BaseCommand, CommandError


DATASET_URL = 'https://www.kaggle.com/api/v1/datasets/download/paramaggarwal/fashion-product-images-small'


class Command(BaseCommand):
    help = 'Download and extract the MIT-licensed Kaggle Fashion Product Images Small dataset.'

    def add_arguments(self, parser):
        parser.add_argument('--output', default=str(settings.BASE_DIR / 'data' / 'fashion'))
        parser.add_argument('--force', action='store_true')

    def handle(self, *args, **options):
        output = Path(options['output']).resolve()
        marker = output / 'DATASET_INFO.json'
        if marker.exists() and not options['force']:
            self.stdout.write(f'Dataset already exists at {output}')
            return
        output.mkdir(parents=True, exist_ok=True)
        archive = output.parent / 'fashion-product-images-small.zip.part'
        self.stdout.write('Downloading Kaggle Fashion Product Images Small...')
        with requests.get(DATASET_URL, stream=True, timeout=60) as response:
            response.raise_for_status()
            total = int(response.headers.get('content-length', 0))
            downloaded = 0
            with archive.open('wb') as target:
                for chunk in response.iter_content(chunk_size=1024 * 1024):
                    if not chunk:
                        continue
                    target.write(chunk)
                    downloaded += len(chunk)
                    if total:
                        self.stdout.write(f'\r{downloaded * 100 / total:5.1f}%', ending='')
        self.stdout.write('\nExtracting dataset...')
        if options['force']:
            for child in output.iterdir():
                if child.is_dir():
                    shutil.rmtree(child)
                else:
                    child.unlink()
        with zipfile.ZipFile(archive) as zipped:
            for member in zipped.infolist():
                destination = (output / member.filename).resolve()
                if output not in destination.parents and destination != output:
                    raise CommandError('Unsafe path detected in dataset archive.')
            zipped.extractall(output)
        archive.unlink(missing_ok=True)
        marker.write_text(json.dumps({
            'dataset': 'Fashion Product Images (Small)',
            'source': 'https://www.kaggle.com/datasets/paramaggarwal/fashion-product-images-small',
            'license_reported_by_kaggle': 'MIT',
            'downloaded_at': datetime.now(timezone.utc).isoformat(),
        }, indent=2), encoding='utf-8')
        styles = list(output.rglob('styles.csv'))
        images = list(output.rglob('images'))
        if not styles or not images:
            raise CommandError('styles.csv or images directory was not found after extraction.')
        self.stdout.write(self.style.SUCCESS(f'Dataset extracted to {output}'))
