from datetime import datetime, timezone
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from django.conf import settings
from django.core.management.base import BaseCommand, CommandError
from sklearn.linear_model import SGDClassifier
from sklearn.metrics import accuracy_score

from closet.ml.image_classifier import extract_features


CATEGORY_CLASSES = np.asarray(['accessory', 'bag', 'bottom', 'outer', 'shoes', 'top'])
SEASON_CLASSES = np.asarray(['all', 'fall', 'spring', 'summer', 'winter'])


def map_category(row):
    article = str(row.get('articleType', '')).lower()
    subcategory = str(row.get('subCategory', '')).lower()
    master = str(row.get('masterCategory', '')).lower()
    if any(word in article for word in ('jacket', 'blazer', 'coat', 'waistcoat', 'cardigan', 'rain')):
        return 'outer'
    if any(word in article for word in ('jeans', 'trouser', 'shorts', 'skirt', 'legging', 'track pant', 'jogger')):
        return 'bottom'
    if any(word in article for word in ('shoe', 'sandal', 'heel', 'flip flop', 'flats')) or master == 'footwear':
        return 'shoes'
    if any(word in article for word in ('bag', 'backpack', 'clutch')) or subcategory == 'bags':
        return 'bag'
    if subcategory in {'topwear', 'innerwear', 'dress', 'loungewear and nightwear'}:
        return 'top'
    if master == 'accessories':
        return 'accessory'
    return None


def map_season(value):
    normalized = str(value).strip().lower()
    return normalized if normalized in {'spring', 'summer', 'fall', 'winter'} else 'all'


class Command(BaseCommand):
    help = 'Train category and season image classifiers from the downloaded Kaggle dataset.'

    def add_arguments(self, parser):
        parser.add_argument('--data', default=str(settings.BASE_DIR / 'data' / 'fashion'))
        parser.add_argument('--output', default=str(settings.BASE_DIR / 'ml_models' / 'fashion_image_classifier.joblib'))
        parser.add_argument('--limit', type=int, default=8000)
        parser.add_argument('--batch-size', type=int, default=256)

    def handle(self, *args, **options):
        data_dir = Path(options['data']).resolve()
        styles_files = list(data_dir.rglob('styles.csv'))
        image_dirs = [path for path in data_dir.rglob('images') if path.is_dir()]
        if not styles_files or not image_dirs:
            raise CommandError('Run download_fashion_dataset first.')
        styles = pd.read_csv(styles_files[0], on_bad_lines='skip', low_memory=False)
        styles['category'] = styles.apply(map_category, axis=1)
        styles['mapped_season'] = styles['season'].map(map_season)
        styles = styles.dropna(subset=['id', 'category']).sample(frac=1, random_state=42)
        if options['limit'] > 0:
            styles = styles.head(options['limit'])
        image_dir = image_dirs[0]
        records = []
        for row in styles.to_dict('records'):
            image_path = image_dir / f"{int(row['id'])}.jpg"
            if image_path.exists():
                records.append((image_path, row['category'], row['mapped_season']))
        if len(records) < 100:
            raise CommandError('Not enough matching image files were found.')

        split = max(int(len(records) * 0.8), 1)
        train_records, validation_records = records[:split], records[split:]
        category_model = SGDClassifier(loss='log_loss', alpha=0.0003, random_state=42, average=True)
        season_model = SGDClassifier(loss='log_loss', alpha=0.0003, random_state=42, average=True)
        first_batch = True
        batch_size = options['batch_size']
        trained = 0
        for start in range(0, len(train_records), batch_size):
            batch = train_records[start:start + batch_size]
            features, categories, seasons = [], [], []
            for image_path, category, season in batch:
                try:
                    features.append(extract_features(image_path))
                    categories.append(category)
                    seasons.append(season)
                except (OSError, ValueError):
                    continue
            if not features:
                continue
            matrix = np.asarray(features)
            kwargs = {'classes': CATEGORY_CLASSES} if first_batch else {}
            category_model.partial_fit(matrix, categories, **kwargs)
            kwargs = {'classes': SEASON_CLASSES} if first_batch else {}
            season_model.partial_fit(matrix, seasons, **kwargs)
            first_batch = False
            trained += len(features)
            self.stdout.write(f'\rProcessed {trained}/{len(train_records)} training images', ending='')
        self.stdout.write('')

        validation_features, category_labels, season_labels = [], [], []
        for image_path, category, season in validation_records[:1500]:
            try:
                validation_features.append(extract_features(image_path))
                category_labels.append(category)
                season_labels.append(season)
            except (OSError, ValueError):
                continue
        if not validation_features:
            raise CommandError('No validation images could be processed.')
        validation_matrix = np.asarray(validation_features)
        metrics = {
            'category_accuracy': float(accuracy_score(category_labels, category_model.predict(validation_matrix))),
            'season_accuracy': float(accuracy_score(season_labels, season_model.predict(validation_matrix))),
            'validation_samples': len(validation_features),
        }
        output = Path(options['output']).resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        joblib.dump({
            'category_model': category_model,
            'season_model': season_model,
            'trained_samples': trained,
            'trained_at': datetime.now(timezone.utc).isoformat(),
            'dataset': 'Kaggle Fashion Product Images (Small)',
            'metrics': metrics,
        }, output)
        self.stdout.write(self.style.SUCCESS(
            f"Saved {output} | category accuracy={metrics['category_accuracy']:.3f}, "
            f"season accuracy={metrics['season_accuracy']:.3f}"
        ))
