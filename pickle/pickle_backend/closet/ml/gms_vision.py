"""Vision-based clothing analysis via GMS, an OpenAI-compatible gateway.

One low-detail image request returns category, season, color, style, and
material so we avoid spending extra credits on repeated vision calls.
"""

import base64
import io
import json
import logging

from django.conf import settings
from openai import OpenAIError
from PIL import Image as PILImage

from ..style_options import AIHUB_PARENT_STYLE_VALUES

logger = logging.getLogger(__name__)

CATEGORY_VALUES = {'top', 'bottom', 'outer', 'dress', 'shoes', 'bag', 'accessory'}
SEASON_VALUES = {'spring', 'summer', 'fall', 'winter', 'all'}
COLOR_VALUES = {
    'black',
    'white',
    'ivory',
    'beige',
    'gray',
    'blue',
    'navy',
    'pink',
    'red',
    'green',
    'brown',
    'yellow',
    'orange',
    'purple',
    'skyblue',
    'mint',
    'khaki',
    'silver',
}

SYSTEM_PROMPT = (
    'You are a fashion expert classifying a single clothing item photo. '
    'Respond with a strict JSON object only - no markdown, no extra text.'
)

MAX_UPLOAD_DIMENSION = 512


def _build_user_prompt():
    styles = ', '.join(sorted(AIHUB_PARENT_STYLE_VALUES))
    categories = ', '.join(sorted(CATEGORY_VALUES))
    seasons = ', '.join(sorted(SEASON_VALUES))
    colors = ', '.join(sorted(COLOR_VALUES))
    return (
        'Classify the clothing item in the photo. Respond with a JSON object with '
        'exactly these keys:\n'
        '{\n'
        f'  "category": one of [{categories}],\n'
        f'  "season": one of [{seasons}] for when this item is typically worn,\n'
        f'  "color": one of [{colors}], choose the dominant clothing color,\n'
        f'  "style": one of [{styles}] (Korean style label, pick the closest match),\n'
        '  "material": a short Korean material guess, or "" if you cannot tell,\n'
        '  "confidence": {\n'
        '    "category": float from 0 to 1,\n'
        '    "season": float from 0 to 1,\n'
        '    "color": float from 0 to 1,\n'
        '    "style": float from 0 to 1\n'
        '  }\n'
        '}'
    )


def _encode_image(image):
    image = image.convert('RGB')
    if max(image.size) > MAX_UPLOAD_DIMENSION:
        image.thumbnail((MAX_UPLOAD_DIMENSION, MAX_UPLOAD_DIMENSION), PILImage.Resampling.LANCZOS)
    buffer = io.BytesIO()
    image.save(buffer, format='JPEG', quality=85)
    encoded = base64.b64encode(buffer.getvalue()).decode('ascii')
    return f'data:image/jpeg;base64,{encoded}'


def _client():
    if not settings.GMS_API_KEY:
        return None
    from openai import OpenAI
    return OpenAI(base_url=settings.GMS_OPENAI_BASE_URL, api_key=settings.GMS_API_KEY)


def _confidence(raw_confidence, key, default=0.7):
    try:
        return max(0.0, min(1.0, float(raw_confidence.get(key, default))))
    except (AttributeError, TypeError, ValueError):
        return default


def analyze_clothing_with_gms(image):
    """Ask the GMS vision model for clothing metadata."""
    client = _client()
    if client is None:
        return None

    try:
        response = client.chat.completions.create(
            model=settings.GMS_VISION_MODEL,
            response_format={'type': 'json_object'},
            max_tokens=180,
            timeout=15,
            messages=[
                {'role': 'system', 'content': SYSTEM_PROMPT},
                {
                    'role': 'user',
                    'content': [
                        {'type': 'text', 'text': _build_user_prompt()},
                        {'type': 'image_url', 'image_url': {'url': _encode_image(image), 'detail': 'low'}},
                    ],
                },
            ],
        )
        payload = json.loads(response.choices[0].message.content)
    except (OpenAIError, json.JSONDecodeError, KeyError, IndexError, ValueError, TypeError) as exc:
        logger.warning('GMS vision analysis failed, falling back to local logic: %s', exc)
        return None

    category = str(payload.get('category', '')).strip().lower()
    season = str(payload.get('season', '')).strip().lower()
    color = str(payload.get('color', '')).strip().lower()
    style = str(payload.get('style', '')).strip()
    if (
        category not in CATEGORY_VALUES
        or season not in SEASON_VALUES
        or color not in COLOR_VALUES
        or style not in AIHUB_PARENT_STYLE_VALUES
    ):
        logger.warning('GMS vision returned unrecognized values: %s', payload)
        return None

    raw_confidence = payload.get('confidence', {})
    if not isinstance(raw_confidence, dict):
        raw_confidence = {'style': raw_confidence}

    return {
        'category': category,
        'season': season,
        'color': color,
        'style': style,
        'aihub_style': '',
        'material': str(payload.get('material', '') or '').strip(),
        'confidence': {
            'category': _confidence(raw_confidence, 'category'),
            'season': _confidence(raw_confidence, 'season'),
            'color': _confidence(raw_confidence, 'color'),
            'style': _confidence(raw_confidence, 'style'),
        },
        'model': {'type': f'gms:{settings.GMS_VISION_MODEL}'},
    }
