from pathlib import Path
from io import BytesIO

import joblib
import numpy as np
from django.conf import settings
from PIL import Image
from skimage.feature import hog

from .gms_vision import analyze_clothing_with_gms


MODEL_PATH = settings.BASE_DIR / 'ml_models' / 'fashion_image_classifier.joblib'
PALETTE = {
    'black': (28, 25, 23),
    'white': (245, 243, 238),
    'ivory': (232, 220, 194),
    'beige': (198, 166, 125),
    'gray': (137, 137, 137),
    'blue': (72, 112, 160),
    'navy': (35, 50, 78),
    'pink': (224, 145, 157),
    'red': (166, 54, 52),
    'green': (78, 116, 83),
    'brown': (112, 76, 51),
    'yellow': (220, 183, 74),
    'orange': (224, 126, 68),
    'purple': (128, 91, 145),
    'skyblue': (157, 195, 218),
    'mint': (151, 200, 179),
    'khaki': (112, 108, 72),
    'silver': (194, 197, 200),
}


def open_rgb_image(source):
    if isinstance(source, bytes):
        source = BytesIO(source)
    if hasattr(source, 'seek'):
        source.seek(0)
    image = Image.open(source).convert('RGB')
    if hasattr(source, 'seek'):
        source.seek(0)
    return image


def extract_features(source):
    image = open_rgb_image(source).resize((96, 96), Image.Resampling.LANCZOS)
    array = np.asarray(image, dtype=np.float32) / 255.0
    grayscale = np.asarray(image.convert('L'), dtype=np.float32) / 255.0
    shape_features = hog(
        grayscale,
        orientations=9,
        pixels_per_cell=(8, 8),
        cells_per_block=(2, 2),
        block_norm='L2-Hys',
        feature_vector=True,
    )
    color_features = []
    for channel in range(3):
        histogram, _ = np.histogram(array[:, :, channel], bins=16, range=(0, 1), density=True)
        color_features.extend(histogram / max(histogram.sum(), 1))
    return np.concatenate([shape_features, np.asarray(color_features, dtype=np.float32)])


def detect_color(source):
    image = open_rgb_image(source).resize((80, 80), Image.Resampling.LANCZOS)
    pixels = np.asarray(image, dtype=np.float32).reshape(-1, 3)
    spread = pixels.max(axis=1) - pixels.min(axis=1)
    foreground = pixels[(pixels.mean(axis=1) < 242) | (spread > 18)]
    if not len(foreground):
        foreground = pixels
    representative = np.median(foreground, axis=0)
    return min(
        PALETTE,
        key=lambda name: np.linalg.norm(representative - np.asarray(PALETTE[name])),
    )


def load_model(path=MODEL_PATH):
    path = Path(path)
    if not path.exists():
        raise FileNotFoundError('The fashion image model has not been trained yet.')
    return joblib.load(path)


def predict_style(source):
    """Style classification is GMS-only (see closet/ml/gms_vision.py).

    The previous local fallback trained on the AIHub K-Fashion dataset was
    removed because AIHub's terms of use prohibit redistributing the dataset
    or models derived from it outside the registered usage scope. Returns
    None when GMS is unavailable/disabled - the user fills style in manually.
    """
    prediction = analyze_clothing_with_gms(open_rgb_image(source))
    if prediction is None:
        return None
    confidence = prediction.get('confidence', {})
    if isinstance(confidence, dict):
        confidence = confidence.get('style', 0.7)
    return {
        'style': prediction['style'],
        'aihub_style': prediction.get('aihub_style', ''),
        'material': prediction.get('material', ''),
        'confidence': confidence,
        'model': prediction['model'],
    }


def predict_image(source, path=MODEL_PATH):
    gms_prediction = analyze_clothing_with_gms(open_rgb_image(source))
    if gms_prediction is not None:
        confidence = dict(gms_prediction['confidence'])
        prediction = {
            'category': gms_prediction['category'],
            'season': gms_prediction['season'],
            'color': gms_prediction['color'],
            'material': gms_prediction.get('material', ''),
            'style': gms_prediction['style'],
            'confidence': confidence,
            'model': gms_prediction['model'],
            'style_model': gms_prediction['model'],
        }
        if gms_prediction.get('aihub_style'):
            prediction['aihub_style'] = gms_prediction['aihub_style']
        return prediction

    features = extract_features(source).reshape(1, -1)
    try:
        payload = load_model(path)
        category_model = payload['category_model']
        season_model = payload['season_model']
        category = str(category_model.predict(features)[0])
        season = str(season_model.predict(features)[0])
        category_confidence = float(category_model.predict_proba(features).max())
        season_confidence = float(season_model.predict_proba(features).max())
        prediction = {
            'category': category,
            'season': season,
            'color': detect_color(source),
            'confidence': {
                'category': category_confidence,
                'season': season_confidence,
            },
            'model': {
                'type': 'HOG + SGDClassifier',
                'trained_samples': payload['trained_samples'],
                'trained_at': payload['trained_at'],
                'dataset': payload['dataset'],
            },
        }
    except FileNotFoundError:
        prediction = {
            'category': 'top',
            'season': 'all',
            'color': detect_color(source),
            'confidence': {
                'category': 0.0,
                'season': 0.0,
            },
            'model': None,
            'partial': True,
            'detail': 'Category and season model is not trained yet.',
        }
    return prediction
