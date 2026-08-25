from itertools import combinations as color_pairs, product

import joblib
import requests
from django.conf import settings

from closet.models import ClothingItem
from closet.style_options import AIHUB_PARENT_STYLE_VALUES, LEGACY_STYLE_FAMILIES, parent_style_for_substyle, style_family
from .llm_reranker import rerank_outfit_candidates
from .models import OutfitEvaluation, RecommendationEvent
from .rules import (
    check_material_weather,
    check_rain_shoes,
    check_required_structure,
    check_snow_shoes,
    check_tpo_forbidden,
    is_hard_violated,
    soft_rule_score,
)


WEATHER_URL = 'https://api.open-meteo.com/v1/forecast'
MODEL_PATH = settings.BASE_DIR / 'ml_models' / 'outfit_recommender.joblib'
STYLE_PROFILES = {
    'minimal': {
        'colors': {'white', 'ivory', 'beige', 'gray', 'black', 'navy'},
        'materials': {'cotton', 'linen', 'wool', '코튼', '면', '린넨', '울'},
    },
    'casual': {
        'colors': {'white', 'ivory', 'beige', 'blue', 'skyblue', 'navy', 'green'},
        'materials': {'cotton', 'denim', 'canvas', '코튼', '면', '데님', '캔버스'},
    },
    'street': {
        'colors': {'black', 'gray', 'white', 'navy', 'red', 'khaki'},
        'materials': {'denim', 'leather', 'nylon', '데님', '가죽', '나일론'},
    },
    'lovely': {
        'colors': {'pink', 'ivory', 'white', 'beige', 'purple', 'skyblue'},
        'materials': {'lace', 'chiffon', 'knit', '레이스', '시폰', '니트'},
    },
    'formal': {
        'colors': {'black', 'navy', 'gray', 'white', 'ivory', 'beige', 'brown'},
        'materials': {'wool', 'leather', 'silk', '울', '가죽', '실크'},
    },
    'chic': {
        'colors': {'black', 'white', 'gray', 'navy', 'silver', 'purple'},
        'materials': {'leather', 'silk', 'wool', '가죽', '실크', '울'},
    },
}

TPO_PROFILES = {
    'work': {
        'colors': {'white', 'black', 'navy', 'gray', 'beige', 'ivory'},
        'keywords': {'셔츠', '블라우스', '슬랙스', '블레이저', '구두', '로퍼', 'shirt', 'blouse', 'slacks', 'blazer', 'loafer'},
        'avoid_keywords': {'반바지', '슬리퍼', '운동화', 'shorts', 'slipper', 'sneakers'},
    },
    'date': {
        'colors': {'pink', 'ivory', 'white', 'skyblue', 'purple', 'beige'},
        'keywords': {'니트', '원피스', '스커트', 'knit', 'dress', 'skirt'},
        'avoid_keywords': set(),
    },
    'daily': {
        'colors': {'white', 'beige', 'blue', 'black', 'gray'},
        'keywords': {'데님', '맨투맨', '후드', 'denim', 'sweatshirt', 'hoodie'},
        'avoid_keywords': set(),
    },
    'travel': {
        'colors': {'beige', 'khaki', 'white', 'gray', 'green'},
        'keywords': {'운동화', '스니커즈', '백팩', 'sneakers', 'backpack'},
        'avoid_keywords': {'구두', '힐', 'heels'},
    },
    'formal': {
        'colors': {'black', 'navy', 'gray', 'white', 'ivory', 'beige', 'brown'},
        'keywords': {'슈트', '정장', '구두', '블레이저', 'suit', 'blazer', 'oxford'},
        'avoid_keywords': {'운동화', '슬리퍼', 'sneakers', 'slipper'},
    },
}

HOT_TEMPERATURE = 23
VERY_HOT_TEMPERATURE = 27
COLD_TEMPERATURE = 9
COOL_TEMPERATURE = 16
MILD_OUTER_LIMIT = 20
HEAVY_WINTER_KEYWORDS = {
    '후리스', '플리스', 'fleece', '패딩', 'puffer', '무스탕', 'fur',
    '울', 'wool', '코트', 'coat', '부츠', 'boots', '기모',
}
LIGHT_OUTER_KEYWORDS = {
    '가디건', 'cardigan', '셔츠', 'shirt', '바람막이', 'windbreaker',
    '윈드브레이커', 'wind breaker',
}
RAIN_CODES = {51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99}


def _style_profile(preferred_style):
    preferred_family = style_family(preferred_style) or preferred_style
    return STYLE_PROFILES.get(preferred_family, STYLE_PROFILES['casual'])


def fetch_weather(latitude=36.3504, longitude=127.3845):
    params = {
        'latitude': latitude,
        'longitude': longitude,
        'current': 'temperature_2m,weather_code',
        'timezone': 'Asia/Seoul',
    }
    last_exc = None
    for _ in range(3):
        try:
            response = requests.get(WEATHER_URL, params=params, timeout=8)
            response.raise_for_status()
            current = response.json()['current']
            return {
                'temperature': current['temperature_2m'],
                'weather_code': current['weather_code'],
                'source': 'Open-Meteo',
            }
        except Exception as exc:
            last_exc = exc
    raise last_exc


def _season_for_temperature(temperature):
    """Map a temperature to the seasons whose clothes are acceptable, blending near boundaries."""
    if temperature >= 23:
        return ['summer', 'all']
    if temperature >= 17:
        return ['spring', 'summer', 'all']
    if temperature >= 12:
        return ['spring', 'fall', 'all']
    if temperature >= 5:
        return ['fall', 'winter', 'all']
    return ['winter', 'all']


def _style_score(item, preferred_style):
    preferred_family = style_family(preferred_style) or preferred_style
    item_family = style_family(getattr(item, 'style', ''))
    item_substyle_parent = parent_style_for_substyle(getattr(item, 'aihub_style', ''))
    profile = _style_profile(preferred_style)
    confidence = getattr(item, 'style_confidence', None)
    confidence = 1.0 if confidence is None else max(0.0, min(1.0, confidence))
    score = 0
    if getattr(item, 'style', '') == preferred_style:
        score += 8 * confidence
    elif item_substyle_parent == preferred_style:
        score += 7 * confidence
    elif item_family and item_family == preferred_family:
        score += 5 * confidence
    score += 4 if item.color.lower() in profile['colors'] else 0
    material = item.material.lower() if item.material else ''
    if any(keyword in material for keyword in profile['materials']):
        score += 2
    return score


def _tpo_score(item, tpo):
    profile = TPO_PROFILES.get(tpo)
    if not profile:
        return 0
    score = 0
    if item.color.lower() in profile['colors']:
        score += 2
    if _has_any_keyword(item, profile['keywords']):
        score += 3
    if _has_any_keyword(item, profile['avoid_keywords']):
        score -= 4
    if tpo == 'formal' and item.category in {'top', 'bottom', 'outer'}:
        score += 2
    return score


def _item_seasons(item):
    return {season.strip() for season in str(item.season or 'all').split(',') if season.strip()}


def _item_matches_season(item, season):
    seasons = _item_seasons(item)
    if 'all' in seasons:
        return True
    if isinstance(season, str):
        return season in seasons
    return bool(seasons & set(season))


def _item_has_explicit_season(item, season):
    return season in _item_seasons(item)


def _item_text(item):
    return ' '.join([
        str(item.name or ''),
        str(item.material or ''),
        str(item.category or ''),
        str(item.memo or ''),
    ]).lower()


def _has_any_keyword(item, keywords):
    text = _item_text(item)
    return any(keyword.lower() in text for keyword in keywords)


def _temperature_score(item, temperature, weather_code=0):
    score = 0
    is_rainy = int(weather_code or 0) in RAIN_CODES
    is_outer = item.category == 'outer'
    heavy = _has_any_keyword(item, HEAVY_WINTER_KEYWORDS)
    light_outer = _has_any_keyword(item, LIGHT_OUTER_KEYWORDS)

    if temperature >= HOT_TEMPERATURE:
        if temperature >= VERY_HOT_TEMPERATURE and is_outer and not (is_rainy and light_outer):
            return -50
        if heavy:
            score -= 30
        if is_outer and not (is_rainy and light_outer):
            score -= 12
        if _item_has_explicit_season(item, 'summer'):
            score += 7
    elif temperature >= MILD_OUTER_LIMIT:
        if is_outer and light_outer:
            score -= 3
        elif is_outer:
            score -= 8
        if heavy:
            score -= 18
    elif temperature <= COLD_TEMPERATURE:
        if item.category == 'outer':
            score += 8
        if heavy:
            score += 6
        if _item_matches_season(item, 'winter'):
            score += 6
    elif temperature <= COOL_TEMPERATURE:
        if item.category == 'outer':
            score += 3
        if heavy:
            score -= 4
    return score


def get_weights(feedback_count, has_ml_model):
    """Return (rule_weight, ml_weight, feedback_weight) scaled by how much feedback data exists."""
    if not has_ml_model:
        feedback_weight = min(0.20, feedback_count * 0.02)
        return (1.0 - feedback_weight, 0.0, feedback_weight)

    feedback_weight = min(0.30, feedback_count * 0.02)
    ml_weight = 0.50
    rule_weight = 1.0 - ml_weight - feedback_weight
    return (rule_weight, ml_weight, feedback_weight)


def _item_score(item, season, colors, tpo, preferred_style, weather=None):
    weather = weather or {'temperature': 20, 'weather_code': 0}
    score = 0
    if _item_matches_season(item, season):
        score += 5
    else:
        score -= 6
    score += _temperature_score(
        item,
        float(weather.get('temperature', 20)),
        weather.get('weather_code', 0),
    )
    score += _style_score(item, preferred_style)
    if item.color.lower() in colors:
        score += 3
    score += _tpo_score(item, tpo)
    return score


NEUTRAL_COLORS = {'black', 'white', 'ivory', 'beige', 'gray', 'navy', 'brown', 'khaki', 'silver'}

HARMONIOUS_COLOR_PAIRS = {
    frozenset({'blue', 'yellow'}), frozenset({'blue', 'orange'}), frozenset({'blue', 'green'}),
    frozenset({'skyblue', 'pink'}), frozenset({'skyblue', 'mint'}), frozenset({'skyblue', 'yellow'}),
    frozenset({'mint', 'pink'}), frozenset({'purple', 'pink'}),
}

CLASHING_COLOR_PAIRS = {
    frozenset({'red', 'green'}), frozenset({'red', 'orange'}), frozenset({'red', 'pink'}),
    frozenset({'red', 'purple'}), frozenset({'orange', 'pink'}), frozenset({'orange', 'purple'}),
    frozenset({'yellow', 'purple'}), frozenset({'yellow', 'pink'}), frozenset({'purple', 'green'}),
}


def _color_combo_score(items):
    """Score how well the item colors work together across the whole outfit, not just per item."""
    colors = [item.color.lower() for item in items if item.color]
    if len(colors) < 2:
        return 0

    unique_colors = set(colors)
    if len(unique_colors) == 1:
        return 6  # monochrome / tone-on-tone

    vivid_colors = unique_colors - NEUTRAL_COLORS
    if not vivid_colors:
        return 5  # all-neutral base, always safe together
    if len(vivid_colors) == 1:
        return 5  # one point-of-color against a neutral base

    score = 0
    for first, second in color_pairs(vivid_colors, 2):
        pair = frozenset({first, second})
        if pair in HARMONIOUS_COLOR_PAIRS:
            score += 3
        elif pair in CLASHING_COLOR_PAIRS:
            score -= 5
        else:
            score -= 1  # two unverified vivid colors together - mild caution
    return score


POSITIVE_FEEDBACK_KEYWORDS = (
    'good', 'great', 'comfortable', 'nice', 'love',
    '\uc88b', '\ud3b8\ud574', '\ub9cc\uc871', '\uc608\uc058', '\uac00\ubcbc',
    '\ub530\ub73b', '\uc2dc\uc6d0', '\uc798 \ub9de',
)
NEGATIVE_FEEDBACK_KEYWORDS = (
    'bad', 'hot', 'cold', 'uncomfortable', 'heavy',
    '\ub365', '\ucd94', '\ubd88\ud3b8', '\ubb34\uac81', '\ubcc4\ub85c',
    '\uc544\uc26c', '\uc548 \ub9de',
)


def _feedback_adjustment(feedback):
    text = (feedback or '').lower()
    if not text:
        return 0
    score = 0
    if any(keyword in text for keyword in POSITIVE_FEEDBACK_KEYWORDS):
        score += 0.4
    if any(keyword in text for keyword in NEGATIVE_FEEDBACK_KEYWORDS):
        score -= 0.6
    return score


def _evaluation_score(evaluation):
    return (
        evaluation.rating * 0.40
        + evaluation.fit_score * 0.20
        + evaluation.activity_score * 0.25
        + evaluation.satisfaction_score * 0.15
        + _feedback_adjustment(evaluation.feedback)
    )


def _recommendation_event_for_outfit(outfit):
    try:
        return outfit.recommendation_event
    except RecommendationEvent.DoesNotExist:
        return None


def _candidate_signature(candidate):
    return tuple(sorted(item.id for item in candidate['items']))


def _relevant_evaluations(user, closet_item_ids):
    if not closet_item_ids:
        return []
    return list(
        OutfitEvaluation.objects
        .filter(outfit__owner=user, outfit__items__id__in=closet_item_ids)
        .select_related('outfit', 'outfit__recommendation_event')
        .prefetch_related('outfit__items')
        .distinct()
        .order_by('-updated_at')[:30]
    )


def _relevant_worn_events(user, closet_item_ids):
    """Recommendations the user wore but never rated - a weak positive signal."""
    if not closet_item_ids:
        return []
    events = (
        RecommendationEvent.objects
        .filter(user=user, was_worn=True, rating__isnull=True)
        .order_by('-created_at')[:30]
    )
    return [event for event in events if set(event.item_ids or []) & closet_item_ids]


def _raw_feedback_bonus(items, evaluations, worn_events, weather, tpo):
    """Combine explicit evaluation feedback and weak 'worn without rating' signals, unclamped."""
    item_ids = {item.id for item in items}
    if not item_ids:
        return 0.0

    target_temperature = float(weather.get('temperature', 20))
    total = 0.0

    for evaluation in evaluations:
        overlap = len(item_ids & {item.id for item in evaluation.outfit.items.all()})
        if not overlap:
            continue

        score = _evaluation_score(evaluation) - 3
        context_multiplier = 1.0

        event = _recommendation_event_for_outfit(evaluation.outfit)
        if event:
            if event.tpo == tpo:
                context_multiplier += 0.25
            if abs(float(event.temperature) - target_temperature) <= 5:
                context_multiplier += 0.25

        total += score * overlap * context_multiplier

    for event in worn_events:
        overlap = len(item_ids & set(event.item_ids or []))
        if not overlap:
            continue

        signal = event.feedback_signal()
        if signal is None:
            continue

        context_multiplier = 1.0
        if event.tpo == tpo:
            context_multiplier += 0.25
        if abs(float(event.temperature) - target_temperature) <= 5:
            context_multiplier += 0.25

        total += signal * overlap * context_multiplier

    return total


def _normalized_feedback_bonus(raw_bonus, rule_score_range):
    """Cap the feedback bonus to at most 30% of the candidate pool's rule_score spread."""
    max_bonus = rule_score_range * 0.30
    return max(-max_bonus, min(max_bonus, raw_bonus))


def build_features(items, weather, tpo, preferred_style, preferred_colors):
    colors = {color.lower() for color in preferred_colors}
    season = _season_for_temperature(weather['temperature'])
    preferred_family = style_family(preferred_style) or preferred_style
    item_by_category = {item.category: item for item in items}
    count = max(len(items), 1)
    features = {
        'temperature': float(weather['temperature']),
        'weather_code': float(weather['weather_code']),
        'tpo': tpo,
        'preferred_style': preferred_style,
        'preferred_style_family': preferred_family,
        'item_count': float(len(items)),
        'season_match_ratio': sum(_item_matches_season(item, season) for item in items) / count,
        'style_match_ratio': sum(
            getattr(item, 'style', '') == preferred_style
            or parent_style_for_substyle(getattr(item, 'aihub_style', '')) == preferred_style
            or style_family(getattr(item, 'style', '')) == preferred_family
            for item in items
        ) / count,
        'style_profile_score_ratio': sum(_style_score(item, preferred_style) for item in items) / (count * 14),
        'preferred_color_ratio': sum(item.color.lower() in colors for item in items) / count,
    }
    for category in ('top', 'bottom', 'outer', 'dress', 'shoes'):
        item = item_by_category.get(category)
        features[f'{category}_color'] = item.color.lower() if item else 'none'
        features[f'{category}_material'] = item.material.lower() if item and item.material else 'none'
        features[f'{category}_season'] = item.season if item else 'none'
        features[f'{category}_style'] = item.style if item and item.style else 'none'
        features[f'{category}_style_family'] = style_family(item.style) if item and item.style else 'none'
        features[f'{category}_aihub_style'] = item.aihub_style if item and item.aihub_style else 'none'
        features[f'{category}_aihub_parent_style'] = (
            parent_style_for_substyle(item.aihub_style) if item and item.aihub_style else 'none'
        )
    return features


REQUIRED_MODEL_FEATURE_KEYS = ('temperature', 'item_count', 'style_profile_score_ratio')
REQUIRED_MODEL_FEATURE_PREFIXES = (
    'preferred_style=',
    'preferred_style_family=',
    'tpo=',
    'top_aihub_style=',
    'top_aihub_parent_style=',
)


def _load_model():
    if not MODEL_PATH.exists():
        return None
    try:
        payload = joblib.load(MODEL_PATH)
        vocabulary = payload['pipeline'].named_steps['vectorizer'].vocabulary_
        if not all(key in vocabulary for key in REQUIRED_MODEL_FEATURE_KEYS):
            return None
        if not all(
            any(key.startswith(prefix) for key in vocabulary)
            for prefix in REQUIRED_MODEL_FEATURE_PREFIXES
        ):
            return None
        return payload
    except (OSError, ValueError, EOFError, KeyError):
        return None


def _candidate_combinations(items, season, colors, tpo, preferred_style, weather, fixed_item=None):
    ranked = {}
    for category in ('top', 'bottom', 'outer', 'dress', 'shoes'):
        category_items = [item for item in items if item.category == category]
        category_items.sort(
            key=lambda item: (-_item_score(item, season, colors, tpo, preferred_style, weather), item.id)
        )
        ranked[category] = category_items[:4 if category in {'top', 'bottom', 'dress'} else 3]
        if fixed_item and fixed_item.category == category and fixed_item not in ranked[category]:
            ranked[category].append(fixed_item)

    if (not ranked['top'] or not ranked['bottom']) and not ranked['dress']:
        return []

    outer_options = [None]
    temperature = float(weather.get('temperature', 20))
    weather_code = weather.get('weather_code', 0)
    is_rainy = int(weather_code or 0) in RAIN_CODES
    for item in ranked['outer']:
        if (
            item != fixed_item
            and
            temperature >= MILD_OUTER_LIMIT
            and not (is_rainy and _has_any_keyword(item, LIGHT_OUTER_KEYWORDS))
        ):
            continue
        if item == fixed_item or _item_score(item, season, colors, tpo, preferred_style, weather) > 0:
            outer_options.append(item)
    if temperature <= COLD_TEMPERATURE and len(outer_options) == 1 and ranked['outer']:
        # Cold weather needs an outer even if none clears the style/color bar.
        outer_options.append(ranked['outer'][0])
    shoes_options = ranked['shoes'] or [None]
    separates = [
        [item for item in combination if item is not None]
        for combination in product(ranked['top'], ranked['bottom'], outer_options, shoes_options)
    ]
    dresses = [
        [item for item in combination if item is not None]
        for combination in product(ranked['dress'], outer_options, shoes_options)
    ]
    combinations = separates + dresses
    if fixed_item:
        combinations = [
            combination
            for combination in combinations
            if any(item.id == fixed_item.id for item in combination)
        ]
    return combinations


MAX_SCORED_CANDIDATES = 50


def recommend_items(
    user,
    *,
    tpo,
    preferred_style,
    preferred_colors=None,
    weather=None,
    fixed_item_id=None,
    excluded_item_signatures=None,
):
    weather = weather or fetch_weather()
    preferred_colors = preferred_colors or []
    excluded_signatures = {
        tuple(sorted(int(item_id) for item_id in signature))
        for signature in (excluded_item_signatures or [])
        if signature
    }
    colors = {color.lower() for color in preferred_colors}
    preferred_family = style_family(preferred_style) or preferred_style
    season = _season_for_temperature(weather['temperature'])
    primary_season = next((value for value in season if value != 'all'), 'all')
    closet_items = list(ClothingItem.objects.filter(owner=user))
    fixed_item = None
    if fixed_item_id is not None:
        fixed_item = next((item for item in closet_items if item.id == fixed_item_id), None)
        if fixed_item is None:
            raise ValueError('fixed_item_not_found')
        if fixed_item.category not in {'top', 'bottom', 'outer', 'dress', 'shoes'}:
            raise ValueError('fixed_item_not_recommendable')
    closet_item_ids = {item.id for item in closet_items}
    combinations = _candidate_combinations(
        closet_items, season, colors, tpo, preferred_style, weather, fixed_item
    )
    raw_combinations = combinations
    # Hard constraints run before scoring. If every candidate is removed, fall back
    # to the original candidate pool so a sparse closet can still get a best-effort outfit.
    rule_context = {
        'tpo': tpo,
        'temperature': float(weather.get('temperature', 20)),
        'weather_code': weather.get('weather_code', 0),
    }
    combinations = [combo for combo in combinations if not is_hard_violated(combo, rule_context)]
    used_relaxed_rules = False
    if not combinations and raw_combinations:
        combinations = [
            combo for combo in raw_combinations
            if not check_required_structure(combo)
            and not check_tpo_forbidden(combo, tpo)
            and not check_material_weather(
                combo,
                float(weather.get('temperature', 20)),
                weather.get('weather_code', 0),
            )
            and not check_rain_shoes(combo, weather.get('weather_code', 0))
            and not check_snow_shoes(combo, weather.get('weather_code', 0))
        ]
        used_relaxed_rules = True
    evaluations = _relevant_evaluations(user, closet_item_ids)
    worn_events = _relevant_worn_events(user, closet_item_ids)
    feedback_count = len(evaluations) + len(worn_events)

    model_payload = _load_model()
    has_ml_model = model_payload is not None
    rule_weight, ml_weight, feedback_weight = get_weights(feedback_count, has_ml_model)

    if not combinations:
        return {
            'weather': weather,
            'season': season,
            'tpo': tpo,
            'preferred_style': preferred_style,
            'preferred_colors': preferred_colors,
            'fixed_item_id': fixed_item_id,
            'items': [],
            'features': {},
            'reasons': ['옷장에 추천할 수 있는 조합이 부족해요. 상의/하의 또는 원피스를 더 등록해주세요.'],
            'scoring': {
                'style_colors': sorted(_style_profile(preferred_style)['colors']),
                'weights': {},
                'blend_weights': {'rule': rule_weight, 'ml': ml_weight, 'feedback': feedback_weight},
            },
            'model': {
                'type': 'RandomForestClassifier' if has_ml_model else 'rule-based fallback',
                'ml_probability': None,
                'trained_at': model_payload.get('trained_at') if model_payload else None,
                'sample_count': model_payload.get('sample_count') if model_payload else 0,
            },
        }

    # Stage 1: cheap rule_score for every candidate combination (no DB calls, no ML).
    rule_scored = sorted(
        (
            (
                items,
                sum(_item_score(item, season, colors, tpo, preferred_style, weather) for item in items)
                + _color_combo_score(items)
                + soft_rule_score(items, rule_context),
            )
            for items in combinations
        ),
        key=lambda pair: -pair[1],
    )
    top_candidates = rule_scored[:MAX_SCORED_CANDIDATES]
    rule_score_range = (
        top_candidates[0][1] - top_candidates[-1][1] if len(top_candidates) > 1 else 1
    )

    # Stage 2: only the top candidates get the expensive ML + feedback scoring.
    candidates = []
    for items, rule_score in top_candidates:
        raw_bonus = _raw_feedback_bonus(items, evaluations, worn_events, weather, tpo)
        feedback_bonus = _normalized_feedback_bonus(raw_bonus, rule_score_range)
        candidates.append({
            'items': items,
            'features': build_features(
                items, weather, tpo, preferred_style, preferred_colors
            ),
            'rule_score': rule_score,
            'feedback_bonus': feedback_bonus,
            'ml_probability': None,
        })

    if has_ml_model:
        pipeline = model_payload['pipeline']
        probabilities = pipeline.predict_proba([candidate['features'] for candidate in candidates])
        positive_index = list(pipeline.named_steps['model'].classes_).index(1)
        for candidate, probability in zip(candidates, probabilities):
            candidate['ml_probability'] = float(probability[positive_index])
            candidate['final_score'] = (
                candidate['rule_score'] * rule_weight
                + candidate['ml_probability'] * 20 * ml_weight
                + candidate['feedback_bonus'] * feedback_weight
            )
    else:
        for candidate in candidates:
            candidate['final_score'] = (
                candidate['rule_score'] * rule_weight
                + candidate['feedback_bonus'] * feedback_weight
            )

    candidates.sort(key=lambda candidate: (-candidate['final_score'], [item.id for item in candidate['items']]))
    recent_signatures = {
        tuple(sorted(event.item_ids))
        for event in RecommendationEvent.objects.filter(user=user, is_synthetic=False)[:5]
        if event.item_ids
    }
    selectable_candidates = [
        candidate
        for candidate in candidates
        if _candidate_signature(candidate) not in excluded_signatures
        and _candidate_signature(candidate) not in recent_signatures
    ] or candidates
    for index, candidate in enumerate(selectable_candidates):
        candidate['candidate_id'] = f'cand_{index + 1}'

    llm_meta = {'llm_reranked': False, 'llm_fallback': False}
    if settings.USE_LLM_RERANKER:
        top_k = max(1, int(settings.LLM_RERANK_TOP_K))
        request_context = {
            'tpo': tpo,
            'preferred_style': preferred_style,
            'preferred_colors': preferred_colors,
            'weather': weather,
        }
        selected, llm_meta = rerank_outfit_candidates(
            user=user,
            candidates=selectable_candidates[:top_k],
            request_context=request_context,
        )
    else:
        selected = selectable_candidates[0]

    ml_used = selected.get('ml_probability') is not None
    color_combo_bonus = _color_combo_score(selected['items'])
    reasons = [
        f'{weather["temperature"]}°C 날씨를 {primary_season} 시즌으로 반영했습니다.',
        f'{preferred_style} 스타일의 색상과 소재에 가까운 옷을 우선했습니다.',
        f'{fixed_item.name}을 포함한 조합으로 추천했습니다.' if fixed_item else '옷장 조합 중 가장 잘 맞는 구성을 골랐습니다.',
        'Random Forest 선호 확률과 조건 점수를 함께 반영했습니다.' if ml_used else '학습 모델이 없어 조건 점수로 추천했습니다.',
        '최근 보여준 조합은 건너뛰어 반복을 줄였습니다.',
    ]
    if used_relaxed_rules:
        reasons.append('조건에 딱 맞는 조합이 부족해 가장 가까운 코디로 추천했습니다.')
    if color_combo_bonus >= 5:
        reasons.append('아이템 간 색상 조합이 잘 어울리는 구성으로 골랐습니다.')
    elif color_combo_bonus < 0:
        reasons.append('옷장 한계로 색상 조합이 살짝 부딪힐 수 있어요.')
    if any(_tpo_score(item, tpo) > 0 for item in selected['items']):
        reasons.append(f'{tpo} 상황에 어울리는 색상/아이템을 우선했습니다.')
    llm_reasons = llm_meta.get('llm_reasons') or []
    if llm_reasons:
        reasons = llm_reasons[:3]
    return {
        'weather': weather,
        'season': season,
        'tpo': tpo,
        'preferred_style': preferred_style,
        'preferred_colors': preferred_colors,
        'fixed_item_id': fixed_item_id,
        'items': selected['items'],
        'features': selected['features'],
        'reasons': reasons,
        'llm_reranked': bool(llm_meta.get('llm_reranked')),
        'llm_reason_summary': llm_meta.get('llm_reason_summary', ''),
        'llm_reasons': llm_reasons[:3],
        'style_keywords': llm_meta.get('style_keywords', []),
        'scoring': {
            'style_colors': sorted(_style_profile(preferred_style)['colors']),
            'weights': {
                'season_match': 5,
                'aihub_style_match': 8,
                'style_color_match': 4,
                'style_material_match': 2,
                'optional_color_match': 3,
                'tpo_color_match': 2,
                'tpo_keyword_match': 3,
                'tpo_avoid_keyword_penalty': -4,
                'formal_tpo_match': 2,
                'color_combo_bonus': color_combo_bonus,
                'user_evaluation_feedback': selected.get('feedback_bonus', 0),
            },
            'blend_weights': {'rule': rule_weight, 'ml': ml_weight, 'feedback': feedback_weight},
        },
        'model': {
            'type': 'RandomForestClassifier' if ml_used else 'rule-based fallback',
            'ml_probability': selected.get('ml_probability'),
            'trained_at': model_payload.get('trained_at') if model_payload else None,
            'sample_count': model_payload.get('sample_count') if model_payload else 0,
        },
    }


AVAILABLE_PREFERRED_STYLES = set(AIHUB_PARENT_STYLE_VALUES) | set(LEGACY_STYLE_FAMILIES)
