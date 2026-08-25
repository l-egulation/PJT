USER_STYLE_CHOICES = [
    ('미니멀', '미니멀'),
    ('꾸안꾸', '꾸안꾸'),
    ('러블리', '러블리'),
    ('스트릿', '스트릿'),
    ('포멀', '포멀'),
    ('빈티지', '빈티지'),
    ('올드머니', '올드머니'),
    ('시크', '시크'),
    ('스포티', '스포티'),
    ('캐주얼', '캐주얼'),
]

USER_STYLE_VALUES = {value for value, _ in USER_STYLE_CHOICES}

# Backward-compatible names used by models/serializers/views.
AIHUB_PARENT_STYLE_CHOICES = USER_STYLE_CHOICES
AIHUB_PARENT_STYLE_VALUES = USER_STYLE_VALUES

AIHUB_SUBSTYLE_TO_PARENT = {
    '놈코어': '미니멀',
    '모던': '미니멀',
    '소피스트케이티드': '미니멀',
    '캐주얼': '캐주얼',
    '스포티': '스포티',
    '리조트': '캐주얼',
    '컨트리': '빈티지',
    '히피': '빈티지',
    '스트리트': '스트릿',
    '힙합': '스트릿',
    '펑크': '스트릿',
    '펑크/로커': '스트릿',
    '키치': '빈티지',
    '키치/키덜트': '빈티지',
    '밀리터리': '스트릿',
    '뉴트로': '빈티지',
    '레트로': '빈티지',
    '아방가르드': '시크',
    '페미닌': '러블리',
    '로맨틱': '러블리',
    '클래식': '올드머니',
    '프레피': '올드머니',
    '매니시': '시크',
    '톰보이': '시크',
    '섹시': '시크',
    '오리엔탈': '빈티지',
    '젠더리스': '시크',
    '웨스턴': '빈티지',
}

AIHUB_SUBSTYLE_VALUES = set(AIHUB_SUBSTYLE_TO_PARENT)

AIHUB_MODEL_LABELS = {
    'normcore': '놈코어',
    'modern': '모던',
    'sophisticated': '소피스트케이티드',
    'casual': '캐주얼',
    'sporty': '스포티',
    'resort': '리조트',
    'country': '컨트리',
    'hippie': '히피',
    'street': '스트리트',
    'hiphop': '힙합',
    'hip-hop': '힙합',
    'punk': '펑크',
    'punk/rocker': '펑크/로커',
    'kitsch': '키치',
    'kitsch/kidult': '키치/키덜트',
    'military': '밀리터리',
    'newtro': '뉴트로',
    'retro': '레트로',
    'avant-garde': '아방가르드',
    'avantgarde': '아방가르드',
    'feminine': '페미닌',
    'romantic': '로맨틱',
    'classic': '클래식',
    'preppy': '프레피',
    'manish': '매니시',
    'mannish': '매니시',
    'tomboy': '톰보이',
    'sexy': '섹시',
    'oriental': '오리엔탈',
    'genderless': '젠더리스',
    'western': '웨스턴',
    'traditional': '클래식',
    'ethnic': '히피',
    'contemporary': '모던',
    'natural': '리조트',
    'subculture': '스트리트',
}

USER_STYLE_FAMILIES = {
    '미니멀': 'minimal',
    '꾸안꾸': 'casual',
    '러블리': 'lovely',
    '스트릿': 'street',
    '포멀': 'formal',
    '빈티지': 'casual',
    '올드머니': 'formal',
    '시크': 'chic',
    '스포티': 'street',
    '캐주얼': 'casual',
}

LEGACY_STYLE_FAMILIES = {
    'minimal': 'minimal',
    'casual': 'casual',
    'street': 'street',
    'lovely': 'lovely',
    'formal': 'formal',
    'chic': 'chic',
}


def normalize_aihub_substyle(label):
    label = str(label or '').strip()
    return AIHUB_MODEL_LABELS.get(label.lower(), label)


def parent_style_for_substyle(substyle):
    return AIHUB_SUBSTYLE_TO_PARENT.get(normalize_aihub_substyle(substyle), '')


def style_family(style):
    return (
        USER_STYLE_FAMILIES.get(style)
        or USER_STYLE_FAMILIES.get(parent_style_for_substyle(style))
        or LEGACY_STYLE_FAMILIES.get(style)
    )
