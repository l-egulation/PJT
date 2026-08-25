"""Hard constraint + soft penalty/bonus rule engine for outfit recommendations.

Hard constraints (the `_check_*` functions and `is_hard_violated`) remove a
combination from the candidate pool before any rule/ML scoring happens, so a
violation can never be "out-scored" by a high ML probability. Soft scores
(the `_*_score` functions and `soft_rule_score`) are plain additive terms that
flow into the existing rule_score alongside `_item_score`/`_color_combo_score`
in services.py, so they can still be outweighed by other signals.

All thresholds/keyword lists are module-level constants so they can be tuned
without touching the matching logic below them.

NOTE ON DATA: ClothingItem.color is filled either by the AI detector (a fixed
18-color English palette, see closet/ml/image_classifier.py) or by free-text
user input, so color values are usually English ("navy", "brown") but are not
guaranteed to be. ClothingItem.material/name/memo are always free text, often
mixed Korean/English. Keyword checks below therefore use case-insensitive
substring matching against the combined item text (name + material + memo),
the same approach already used by HEAVY_WINTER_KEYWORDS/LIGHT_OUTER_KEYWORDS
in services.py, rather than assuming a closed vocabulary.
"""

from collections import Counter

RAIN_WEATHER_CODES = {51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99}
SNOW_WEATHER_CODES = {71, 73, 75, 77, 85, 86}

HOT_TEMPERATURE_HARD = 28
COLD_TEMPERATURE_HARD = 5
WARM_TEMPERATURE_HARD = 20
VERY_COLD_TEMPERATURE_HARD = -5


def _item_text(item):
    return ' '.join([
        str(getattr(item, 'name', '') or ''),
        str(getattr(item, 'material', '') or ''),
        str(getattr(item, 'memo', '') or ''),
    ]).lower()


def _has_any_keyword(item, keywords):
    if not keywords:
        return False
    text = _item_text(item)
    return any(keyword.lower() in text for keyword in keywords)


def _item_seasons(item):
    return {season.strip() for season in str(getattr(item, 'season', '') or '').split(',') if season.strip()}


# ---------------------------------------------------------------------------
# HC-0. Required outfit structure
# ---------------------------------------------------------------------------

def check_required_structure(combo):
    """Hard-disqualify a combo that does not have a valid basic outfit structure.

    Returns True if violated.
    A valid MVP outfit must be either:
    - top + bottom + shoes
    - dress + shoes
    Outer/bag/accessory are optional.
    Example violation: a combo with only a top and shoes, or dress + bottom together.
    """
    categories = {item.category for item in combo}

    has_shoes = 'shoes' in categories
    has_top_bottom = 'top' in categories and 'bottom' in categories
    has_dress = 'dress' in categories

    if not has_shoes:
        return True

    if has_dress and ('top' in categories or 'bottom' in categories):
        return True

    if not has_dress and not has_top_bottom:
        return True

    return False


# ---------------------------------------------------------------------------
# HC-1. TPO forbidden items
# ---------------------------------------------------------------------------

# Casual sandal-type footwear that's never appropriate for work/formal.
CASUAL_SANDAL_KEYWORDS = {'쪼리', '슬리퍼', '크록스', 'flip flop', 'flip-flop', 'crocs', 'slipper'}
RAIN_BOOT_KEYWORDS = {'레인부츠', '장화', 'rain boots', 'rain boot', 'wellington', 'wellies'}

TPO_FORBIDDEN = {
    'formal': {
        'keywords': (
            {'운동화', '스니커즈', 'sneakers', '샌들', '데님', '청바지', '후드'}
            | CASUAL_SANDAL_KEYWORDS
            | RAIN_BOOT_KEYWORDS
        ),
        'styles': {'스트릿', '힙합'},
    },
    'work': {
        'keywords': {'샌들'} | CASUAL_SANDAL_KEYWORDS,
        'styles': {'스트릿', '힙합'},
    },
    'date': {
        'keywords': set(),
        'styles': set(),
    },
    'daily': {
        'keywords': set(),
        'styles': set(),
    },
    'travel': {
        'keywords': {'수트', '정장'},
        'styles': set(),
    },
}


def check_tpo_forbidden(combo, tpo):
    """Hard-disqualify a combo that contains an item explicitly banned for the given TPO.

    Checks each item's name/material/memo text against `TPO_FORBIDDEN[tpo]['keywords']`
    and the item's style/aihub_style against `TPO_FORBIDDEN[tpo]['styles']`.

    Returns True if violated (combo must be dropped).
    Example violation: tpo='formal' with a pair of sneakers in the combo.
    """
    rules = TPO_FORBIDDEN.get(tpo)
    if not rules:
        return False
    for item in combo:
        if _has_any_keyword(item, rules['keywords']):
            return True
        style = str(getattr(item, 'style', '') or '')
        aihub_style = str(getattr(item, 'aihub_style', '') or '')
        if style in rules['styles'] or aihub_style in rules['styles']:
            return True
    return False


# ---------------------------------------------------------------------------
# HC-2. Material vs. weather
# ---------------------------------------------------------------------------

MATERIAL_WEATHER_FORBIDDEN = {
    'warm': {
        'forbidden_keywords': {'스웨이드', 'suede'},
        'forbidden_categories': {'outer'},
    },
    'hot': {
        'forbidden_keywords': {
            '패딩', '무스탕', '퍼', '기모', '두꺼운 니트',
            '플리스', '쉐르파', '코듀로이', 'fleece', 'puffer', 'fur',
        },
        'forbidden_categories': set(),
    },
    'rain': {
        'forbidden_keywords': {'스웨이드', '벨벳', '새틴', '실크', 'suede', 'velvet', 'satin', 'silk'},
        'forbidden_categories': set(),
    },
    'very_cold': {
        'forbidden_keywords': {'린넨', '시폰', '쉬폰', '레이스', 'linen', 'chiffon', 'lace'},
        'forbidden_categories': set(),
    },
}

RAIN_FORBIDDEN_SHOE_KEYWORDS = {
    '쪼리', '슬리퍼', '샌들', '크록스',
    'flip flop', 'flip-flop', 'slipper', 'sandal', 'crocs',
    '스웨이드', 'suede',
}

SNOW_FORBIDDEN_SHOE_KEYWORDS = {
    '쪼리', '슬리퍼', '샌들', '크록스', '힐',
    'flip flop', 'flip-flop', 'slipper', 'sandal', 'crocs', 'heel',
    '스웨이드', 'suede',
}


def check_material_weather(combo, temperature, weather_code):
    """Hard-disqualify a combo whose materials/categories cannot survive the weather.

    `temperature` is in Celsius, `weather_code` is an Open-Meteo WMO code.
    Returns True if violated.
    Example violation: a puffer ('패딩') outer at 28 degrees, or a linen item at -8 degrees.
    Also disqualifies any combo with zero outer items when temperature <= 5C.
    """
    is_hot = temperature >= HOT_TEMPERATURE_HARD
    is_warm = temperature > WARM_TEMPERATURE_HARD
    is_rain = int(weather_code or 0) in RAIN_WEATHER_CODES
    is_very_cold = temperature <= VERY_COLD_TEMPERATURE_HARD
    is_cold = temperature <= COLD_TEMPERATURE_HARD

    for item in combo:
        if is_warm:
            rules = MATERIAL_WEATHER_FORBIDDEN['warm']
            if item.category in rules['forbidden_categories'] and _has_any_keyword(item, rules['forbidden_keywords']):
                return True

        if is_hot:
            rules = MATERIAL_WEATHER_FORBIDDEN['hot']
            if _has_any_keyword(item, rules['forbidden_keywords']):
                return True
            if item.category in rules['forbidden_categories']:
                return True

        if is_rain:
            rules = MATERIAL_WEATHER_FORBIDDEN['rain']
            if _has_any_keyword(item, rules['forbidden_keywords']):
                return True

        if is_very_cold:
            rules = MATERIAL_WEATHER_FORBIDDEN['very_cold']
            if _has_any_keyword(item, rules['forbidden_keywords']):
                return True

    if is_cold and not any(item.category == 'outer' for item in combo):
        return True

    return False


def check_rain_shoes(combo, weather_code):
    """Hard-disqualify shoes that are unsafe or impractical for rainy weather.

    Returns True if violated.
    Example violation: rain weather with suede shoes or flip-flops.
    """
    is_rain = int(weather_code or 0) in RAIN_WEATHER_CODES
    if not is_rain:
        return False

    shoes = [item for item in combo if item.category == 'shoes']
    return any(_has_any_keyword(shoe, RAIN_FORBIDDEN_SHOE_KEYWORDS) for shoe in shoes)


def check_snow_shoes(combo, weather_code):
    """Hard-disqualify shoes that are unsafe or impractical for snowy weather.

    Returns True if violated.
    Example violation: snow weather with sandals, crocs, suede shoes, or heels.
    """
    is_snow = int(weather_code or 0) in SNOW_WEATHER_CODES
    if not is_snow:
        return False

    shoes = [item for item in combo if item.category == 'shoes']
    return any(_has_any_keyword(shoe, SNOW_FORBIDDEN_SHOE_KEYWORDS) for shoe in shoes)


# ---------------------------------------------------------------------------
# HC-3. Color clashes
# ---------------------------------------------------------------------------

NEON_COLOR_KEYWORDS = ('형광', '네온', 'neon')

# Pairs that are unwearable together regardless of any other styling.
# Kept distinct from services.CLASHING_COLOR_PAIRS, which only softly
# penalizes vivid-on-vivid pairings; these are genuine hard vetoes.
COLOR_CLASH_HARD_PAIRS = set()

# Pairs that look off but are a matter of taste, not a hard rule.
COLOR_CLASH_SOFT_PAIRS = {
    frozenset({'red', 'green'}),
    frozenset({'red', 'pink'}),
    frozenset({'orange', 'purple'}),
    frozenset({'yellow', 'purple'}),
}

COLOR_CLASH_SOFT_PENALTY = -4


def check_color_clash(combo):
    """Hard-disqualify a combo with two or more neon-ish colors, or a hard color veto pair.

    Returns True if violated.
    Example violation: one item colored '형광노랑' and another '형광핑크'.
    """
    colors = [str(getattr(item, 'color', '') or '').lower() for item in combo]

    neon_count = sum(1 for color in colors if any(keyword in color for keyword in NEON_COLOR_KEYWORDS))
    if neon_count >= 2:
        return True

    color_set = set(colors)
    for pair in COLOR_CLASH_HARD_PAIRS:
        if pair <= color_set:
            return True

    return False


def color_clash_soft_penalty(combo):
    """Soft penalty for color pairs that clash by convention but aren't unwearable.

    Returns a negative float (0 if no soft clash found).
    Example: red + pink together.
    """
    color_set = {str(getattr(item, 'color', '') or '').lower() for item in combo}
    for pair in COLOR_CLASH_SOFT_PAIRS:
        if pair <= color_set:
            return COLOR_CLASH_SOFT_PENALTY
    return 0.0


# ---------------------------------------------------------------------------
# HC-4. Category duplicates
# ---------------------------------------------------------------------------

CATEGORY_DUPLICATE_FORBIDDEN = {
    'top': 1,
    'bottom': 1,
    'outer': 1,
    'shoes': 1,
    'dress': 1,
}


def check_category_duplicate(combo):
    """Hard-disqualify a combo that has more than the allowed count of any category.

    Returns True if violated.
    Example violation: two 'top' items in the same combo.
    """
    counts = Counter(item.category for item in combo)
    for category, max_count in CATEGORY_DUPLICATE_FORBIDDEN.items():
        if counts.get(category, 0) > max_count:
            return True
    return False


# ---------------------------------------------------------------------------
# SP-0. Extreme season conflicts
# ---------------------------------------------------------------------------

SEASON_CONFLICT_PAIRS = {
    frozenset({'summer', 'winter'}),
}

SEASON_CONFLICT_SOFT_PENALTY = -8


def season_conflict_soft_penalty(combo):
    """Soft penalty for mixing items from opposite extreme seasons.

    Uses each item's full season set (an item can be tagged with several
    seasons, e.g. "spring,fall"), not just one value.
    Returns a negative float (0 if no soft conflict found).
    Example: a 'summer'-only top combined with a 'winter'-only coat earns -8.
    """
    seasons_in_combo = set()
    for item in combo:
        seasons_in_combo |= _item_seasons(item)

    for pair in SEASON_CONFLICT_PAIRS:
        if pair <= seasons_in_combo:
            return SEASON_CONFLICT_SOFT_PENALTY

    return 0.0


# ---------------------------------------------------------------------------
# HC-5. Dress/skirt vs. casual sandals
# ---------------------------------------------------------------------------

SKIRT_KEYWORDS = {'치마', '스커트', 'skirt'}


def check_dress_or_skirt_with_casual_sandals(combo, tpo):
    """Hard-disqualify a combo that pairs a dress/skirt with casual sandals in strict TPOs.

    This hard rule applies only to formal/work contexts. In daily/date/travel,
    the same combination is handled as a soft penalty because it can be a matter
    of taste or situation.
    Returns True if violated.
    Example violation: tpo='work' with a 'dress' item combined with '쪼리' shoes.
    """
    if tpo not in {'formal', 'work'}:
        return False

    has_dress_or_skirt = any(
        item.category == 'dress' or (item.category == 'bottom' and _has_any_keyword(item, SKIRT_KEYWORDS))
        for item in combo
    )
    if not has_dress_or_skirt:
        return False

    return any(_has_any_keyword(item, CASUAL_SANDAL_KEYWORDS) for item in combo)


DRESS_SKIRT_CASUAL_SANDAL_SOFT_PENALTY = -5


def dress_or_skirt_with_casual_sandals_soft_penalty(combo, tpo):
    """Soft penalty for pairing a dress/skirt with casual sandals in flexible TPOs.

    Returns a negative float (0 if no soft conflict found).
    Example: tpo='daily' with a dress and crocs earns -5 instead of hard removal.
    """
    if tpo in {'formal', 'work'}:
        return 0.0

    has_dress_or_skirt = any(
        item.category == 'dress' or (item.category == 'bottom' and _has_any_keyword(item, SKIRT_KEYWORDS))
        for item in combo
    )
    if not has_dress_or_skirt:
        return 0.0

    if any(_has_any_keyword(item, CASUAL_SANDAL_KEYWORDS) for item in combo):
        return DRESS_SKIRT_CASUAL_SANDAL_SOFT_PENALTY

    return 0.0


# ---------------------------------------------------------------------------
# Master hard constraint checker
# ---------------------------------------------------------------------------

def is_hard_violated(combo, context):
    """Returns True if `combo` must be excluded entirely before any scoring.

    `context` is a dict with keys 'tpo', 'temperature', 'weather_code'.
    None of these checks can be overridden by rule_score or ML probability.
    """
    return (
        check_required_structure(combo)
        or check_tpo_forbidden(combo, context['tpo'])
        or check_material_weather(combo, context['temperature'], context['weather_code'])
        or check_rain_shoes(combo, context['weather_code'])
        or check_snow_shoes(combo, context['weather_code'])
        or check_color_clash(combo)
        or check_category_duplicate(combo)
        or check_dress_or_skirt_with_casual_sandals(combo, context['tpo'])
    )


# ---------------------------------------------------------------------------
# SP-1. Color harmony
# ---------------------------------------------------------------------------

NEUTRAL_COLORS = {'white', 'ivory', 'beige', 'gray', 'black', 'navy', 'brown', 'khaki'}
POINT_COLORS = {'red', 'pink', 'green', 'yellow', 'orange', 'purple', 'mint', 'skyblue'}

COLOR_HARMONY_BONUS = (
    ({'white', 'ivory', 'beige'}, {'navy', 'brown', 'khaki', 'gray', 'black'}, 4),
    ({'black'}, {'white', 'gray', 'beige', 'ivory'}, 4),
    ({'navy'}, {'white', 'beige', 'gray', 'ivory', 'brown'}, 4),
    ({'gray'}, {'white', 'black', 'navy'}, 3),
)

COLOR_HARMONY_PENALTY = (
    ({'red'}, {'pink'}, -4),
)


def color_harmony_score(combo):
    """Soft bonus/penalty from pairwise color harmony rules.

    Returns a float, positive for harmonious combos, negative for clashing ones.
    Example: a navy item with a white item earns +4.
    """
    colors = {str(getattr(item, 'color', '') or '').lower() for item in combo if getattr(item, 'color', None)}
    score = 0.0

    if colors and colors <= NEUTRAL_COLORS:
        score += 5

    for group_a, group_b, delta in COLOR_HARMONY_BONUS:
        if colors & group_a and colors & group_b:
            score += delta

    for group_a, group_b, delta in COLOR_HARMONY_PENALTY:
        if colors & group_a and colors & group_b:
            score += delta

    return score


def point_color_score(combo):
    """Soft bonus/penalty based on how many point colors appear in the combo.

    One point color can make an outfit lively, while too many point colors can
    make it visually noisy. Returns a float.
    Example: one red item in a mostly neutral combo earns +4.
    """
    colors = {str(getattr(item, 'color', '') or '').lower() for item in combo if getattr(item, 'color', None)}
    point_count = len(colors & POINT_COLORS)

    if point_count == 1:
        return 4
    if point_count == 2:
        return -2
    if point_count >= 3:
        return -6

    return 0.0


# ---------------------------------------------------------------------------
# SP-2. Material layering (top + outer)
# ---------------------------------------------------------------------------

MATERIAL_LAYERING_GOOD = (
    ('면', '데님', 3),
    ('니트', '코트', 4),
    ('셔츠', '코트', 4),
    ('후드', '나일론', 3),
    ('린넨', '가디건', 3),
)

MATERIAL_LAYERING_BAD = (
    ('패딩', '패딩', -5),
    ('후드', '수트', -4),
    ('두꺼운 니트', '타이트 코트', -3),
)


def material_layering_score(combo):
    """Soft bonus/penalty for how a top's material works under an outer's material.

    Both lists check substrings of the combined top text vs. the combined outer
    text. Returns a float.
    Example: a '니트' top under a '코트' outer earns +4.
    """
    tops = [item for item in combo if item.category == 'top']
    outers = [item for item in combo if item.category == 'outer']
    if not tops or not outers:
        return 0.0

    score = 0.0
    for top in tops:
        top_text = _item_text(top)
        for outer in outers:
            outer_text = _item_text(outer)
            for inner_keyword, outer_keyword, bonus in MATERIAL_LAYERING_GOOD:
                if inner_keyword in top_text and outer_keyword in outer_text:
                    score += bonus
            for inner_keyword, outer_keyword, penalty in MATERIAL_LAYERING_BAD:
                if inner_keyword in top_text and outer_keyword in outer_text:
                    score += penalty
    return score


# ---------------------------------------------------------------------------
# SP-3. TPO material bonus
# ---------------------------------------------------------------------------

TPO_MATERIAL_BONUS = {
    'formal': {'keywords': {'울', '캐시미어', '실크', '트위드', '수트'}, 'bonus': 6},
    'work': {'keywords': {'면', '울', '폴리', '슬랙스'}, 'bonus': 4},
    'date': {'keywords': {'실크', '새틴', '쉬폰', '레이스', '니트'}, 'bonus': 4},
    'travel': {'keywords': {'면', '나일론', '스판', '저지'}, 'bonus': 3},
    'daily': {'keywords': {'면', '데님', '후드'}, 'bonus': 2},
}


def tpo_material_score(combo, tpo):
    """Soft bonus for each item whose material/name/memo text matches the TPO's preferred materials.

    Returns a float (0 if `tpo` has no profile).
    Example: tpo='formal' with a '울' (wool) item earns +6.
    """
    rules = TPO_MATERIAL_BONUS.get(tpo)
    if not rules:
        return 0.0

    score = 0.0
    for item in combo:
        item_text = _item_text(item)
        if any(keyword.lower() in item_text for keyword in rules['keywords']):
            score += rules['bonus']

    return min(score, rules['bonus'] * 2)


# ---------------------------------------------------------------------------
# SP-4. Length balance (top vs. bottom), inferred from item name keywords
# ---------------------------------------------------------------------------

LENGTH_SHORT_KEYWORDS = {'반바지', '숏치마', '크롭', '미니', '숏'}
LENGTH_LONG_KEYWORDS = {'긴바지', '롱치마', '롱스커트', '롱', '맥시', '와이드'}

LENGTH_CONTRAST_BONUS = 4
LENGTH_BOTH_LONG_PENALTY = -3


def _infer_length(item):
    """Infer 'short'/'long' from the item's name/material/memo text, or None if unspecified.

    There is no dedicated length field on ClothingItem, so this is a best-effort
    keyword guess (e.g. a name containing '크롭' or '반바지' implies 'short').
    Items with no matching keyword return None and are excluded from scoring.
    """
    text = _item_text(item)
    if any(keyword in text for keyword in LENGTH_LONG_KEYWORDS):
        return 'long'
    if any(keyword in text for keyword in LENGTH_SHORT_KEYWORDS):
        return 'short'
    return None


def length_balance_score(combo):
    """Soft bonus/penalty for the top/bottom length silhouette balance.

    A short top with a long bottom (or vice versa) earns a contrast bonus;
    a long top with a long bottom earns a penalty for looking bulky. Items
    with no inferrable length (see `_infer_length`) are skipped entirely.
    Returns a float.
    Example: a '크롭' top with a '롱치마' bottom earns +4.
    """
    tops = [item for item in combo if item.category == 'top']
    bottoms = [item for item in combo if item.category == 'bottom']
    if not tops or not bottoms:
        return 0.0

    score = 0.0
    for top in tops:
        top_length = _infer_length(top)
        if top_length is None:
            continue
        for bottom in bottoms:
            bottom_length = _infer_length(bottom)
            if bottom_length is None:
                continue
            if top_length != bottom_length:
                score += LENGTH_CONTRAST_BONUS
            elif top_length == 'long':
                score += LENGTH_BOTH_LONG_PENALTY
    return score


# ---------------------------------------------------------------------------
# SP-5. TPO shoe bonus/penalty
# ---------------------------------------------------------------------------

TPO_SHOE_SCORE = {
    'formal': {
        'good': {'로퍼', '구두', '힐', 'loafer', 'heel', 'oxford'},
        'bad': {'스니커즈', '운동화', '크록스', '슬리퍼', '쪼리'},
        'good_bonus': 5,
        'bad_penalty': -8,
    },
    'work': {
        'good': {'로퍼', '플랫', '구두', '스니커즈', '운동화', 'loafer', 'flat', 'sneakers'},
        'bad': {'크록스', '쪼리', '슬리퍼'},
        'good_bonus': 4,
        'bad_penalty': -6,
    },
    'date': {
        'good': {'부츠', '로퍼', '플랫', '힐', 'boots', 'loafer', 'flat', 'heel'},
        'bad': {'크록스', '쪼리'},
        'good_bonus': 4,
        'bad_penalty': -4,
    },
    'travel': {
        'good': {'운동화', '스니커즈', 'sneakers', 'running'},
        'bad': {'힐', '구두', 'heel', 'oxford'},
        'good_bonus': 4,
        'bad_penalty': -4,
    },
    'daily': {
        'good': {'운동화', '스니커즈', '로퍼', 'sneakers', 'loafer'},
        'bad': set(),
        'good_bonus': 2,
        'bad_penalty': 0,
    },
}


def tpo_shoe_score(combo, tpo):
    """Soft bonus/penalty for shoes that fit or conflict with the selected TPO.

    Returns a float.
    Example: travel with sneakers earns +4, while travel with heels earns -4.
    """
    rules = TPO_SHOE_SCORE.get(tpo)
    if not rules:
        return 0.0

    shoes = [item for item in combo if item.category == 'shoes']
    if not shoes:
        return 0.0

    score = 0.0
    for shoe in shoes:
        if _has_any_keyword(shoe, rules['good']):
            score += rules['good_bonus']
        if _has_any_keyword(shoe, rules['bad']):
            score += rules['bad_penalty']

    return score


# ---------------------------------------------------------------------------
# Master soft score calculator
# ---------------------------------------------------------------------------

def soft_rule_score(combo, context):
    """Returns the total soft score adjustment for `combo` to add into rule_score.

    `context` is a dict with key 'tpo' (the others in `is_hard_violated`'s
    context are unused here since most soft scores in this module don't need
    weather, only HC-2/HC rain-snow checks do).
    """
    return (
        color_harmony_score(combo)
        + point_color_score(combo)
        + color_clash_soft_penalty(combo)
        + season_conflict_soft_penalty(combo)
        + dress_or_skirt_with_casual_sandals_soft_penalty(combo, context['tpo'])
        + material_layering_score(combo)
        + tpo_material_score(combo, context['tpo'])
        + length_balance_score(combo)
        + tpo_shoe_score(combo, context['tpo'])
    )
