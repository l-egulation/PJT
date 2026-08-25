from unittest.mock import MagicMock

from django.test import SimpleTestCase

from outfits.rules import (
    check_category_duplicate,
    check_color_clash,
    check_dress_or_skirt_with_casual_sandals,
    check_material_weather,
    check_rain_shoes,
    check_required_structure,
    check_snow_shoes,
    check_tpo_forbidden,
    color_clash_soft_penalty,
    color_harmony_score,
    season_conflict_soft_penalty,
    is_hard_violated,
    length_balance_score,
    material_layering_score,
    soft_rule_score,
    tpo_material_score,
)


def make_item(**kwargs):
    item = MagicMock()
    item.category = kwargs.get('category', 'top')
    item.material = kwargs.get('material', '')
    item.color = kwargs.get('color', '')
    item.season = kwargs.get('season', 'all')
    item.style = kwargs.get('style', '')
    item.aihub_style = kwargs.get('aihub_style', '')
    item.name = kwargs.get('name', '')
    item.memo = kwargs.get('memo', '')
    return item


class TpoForbiddenTests(SimpleTestCase):
    def test_formal_rejects_sneakers(self):
        combo = [
            make_item(category='top', material='울'),
            make_item(category='bottom', material='슬랙스'),
            make_item(category='shoes', name='운동화'),
        ]
        self.assertTrue(check_tpo_forbidden(combo, 'formal'))

    def test_formal_allows_loafers(self):
        combo = [
            make_item(category='top', material='울'),
            make_item(category='bottom', material='슬랙스'),
            make_item(category='shoes', name='로퍼'),
        ]
        self.assertFalse(check_tpo_forbidden(combo, 'formal'))

    def test_work_rejects_street_style(self):
        combo = [make_item(category='top', style='스트릿')]
        self.assertTrue(check_tpo_forbidden(combo, 'work'))

    def test_work_allows_casual_style(self):
        combo = [make_item(category='top', style='캐주얼')]
        self.assertFalse(check_tpo_forbidden(combo, 'work'))

    def test_travel_rejects_suit_keyword(self):
        combo = [make_item(category='top', name='수트 자켓')]
        self.assertTrue(check_tpo_forbidden(combo, 'travel'))

    def test_travel_allows_tshirt(self):
        combo = [make_item(category='top', name='반팔 티셔츠')]
        self.assertFalse(check_tpo_forbidden(combo, 'travel'))

    def test_date_has_no_forbidden_rules(self):
        combo = [make_item(category='shoes', name='운동화', style='스트릿')]
        self.assertFalse(check_tpo_forbidden(combo, 'date'))

    def test_formal_rejects_flip_flops(self):
        combo = [make_item(category='shoes', name='쪼리')]
        self.assertTrue(check_tpo_forbidden(combo, 'formal'))

    def test_formal_rejects_rain_boots(self):
        combo = [make_item(category='shoes', name='레인부츠')]
        self.assertTrue(check_tpo_forbidden(combo, 'formal'))

    def test_work_rejects_crocs(self):
        combo = [make_item(category='shoes', name='크록스')]
        self.assertTrue(check_tpo_forbidden(combo, 'work'))

    def test_work_rejects_flip_flops(self):
        combo = [make_item(category='shoes', name='쪼리 샌들')]
        self.assertTrue(check_tpo_forbidden(combo, 'work'))


class DressOrSkirtWithCasualSandalsTests(SimpleTestCase):
    def test_formal_dress_with_flip_flops_rejected(self):
        combo = [
            make_item(category='dress', name='원피스'),
            make_item(category='shoes', name='쪼리'),
        ]
        self.assertTrue(check_dress_or_skirt_with_casual_sandals(combo, 'formal'))

    def test_work_skirt_bottom_with_slippers_rejected(self):
        combo = [
            make_item(category='top', name='블라우스'),
            make_item(category='bottom', name='롱 스커트'),
            make_item(category='shoes', name='슬리퍼'),
        ]
        self.assertTrue(check_dress_or_skirt_with_casual_sandals(combo, 'work'))

    def test_daily_skirt_bottom_with_crocs_is_not_hard_rejected(self):
        combo = [
            make_item(category='bottom', name='치마'),
            make_item(category='shoes', name='크록스'),
        ]
        self.assertFalse(check_dress_or_skirt_with_casual_sandals(combo, 'daily'))

    def test_skirt_bottom_with_sneakers_passes(self):
        combo = [
            make_item(category='bottom', name='치마'),
            make_item(category='shoes', name='운동화'),
        ]
        self.assertFalse(check_dress_or_skirt_with_casual_sandals(combo, 'formal'))

    def test_pants_bottom_with_flip_flops_passes(self):
        combo = [
            make_item(category='bottom', name='바지'),
            make_item(category='shoes', name='쪼리'),
        ]
        self.assertFalse(check_dress_or_skirt_with_casual_sandals(combo, 'formal'))


class MaterialWeatherTests(SimpleTestCase):
    def test_hot_weather_rejects_padding_outer(self):
        combo = [make_item(category='outer', material='패딩')]
        self.assertTrue(check_material_weather(combo, temperature=28.0, weather_code=0))

    def test_warm_weather_rejects_suede_outer(self):
        combo = [make_item(category='outer', name='스웨이드 자켓', material='스웨이드')]
        self.assertTrue(check_material_weather(combo, temperature=21.0, weather_code=0))

    def test_warm_weather_allows_suede_shoes(self):
        combo = [make_item(category='shoes', name='스웨이드 로퍼', material='스웨이드')]
        self.assertFalse(check_material_weather(combo, temperature=21.0, weather_code=0))

    def test_hot_weather_allows_cotton_top(self):
        combo = [make_item(category='top', material='면')]
        self.assertFalse(check_material_weather(combo, temperature=27.0, weather_code=0))

    def test_rain_rejects_silk(self):
        combo = [make_item(category='top', material='실크')]
        self.assertTrue(check_material_weather(combo, temperature=18.0, weather_code=61))

    def test_no_rain_allows_silk(self):
        combo = [make_item(category='top', material='실크')]
        self.assertFalse(check_material_weather(combo, temperature=18.0, weather_code=0))

    def test_very_cold_rejects_linen(self):
        combo = [make_item(category='top', material='린넨')]
        self.assertTrue(check_material_weather(combo, temperature=-8.0, weather_code=0))

    def test_cold_requires_outer(self):
        combo = [make_item(category='top', material='면')]
        self.assertTrue(check_material_weather(combo, temperature=2.0, weather_code=0))

    def test_cold_with_outer_passes(self):
        combo = [
            make_item(category='top', material='면'),
            make_item(category='outer', material='울'),
        ]
        self.assertFalse(check_material_weather(combo, temperature=2.0, weather_code=0))


class ColorClashTests(SimpleTestCase):
    def test_neon_clash_rejected(self):
        combo = [make_item(color='형광 노랑'), make_item(color='형광 핑크')]
        self.assertTrue(check_color_clash(combo))

    def test_single_neon_item_passes(self):
        combo = [make_item(color='형광 노랑'), make_item(color='black')]
        self.assertFalse(check_color_clash(combo))

    def test_red_green_is_soft_not_hard(self):
        combo = [make_item(color='red'), make_item(color='green')]
        self.assertFalse(check_color_clash(combo))
        self.assertLess(color_clash_soft_penalty(combo), 0)

    def test_unrelated_colors_pass(self):
        combo = [make_item(color='white'), make_item(color='navy')]
        self.assertFalse(check_color_clash(combo))

    def test_red_pink_is_soft_not_hard(self):
        combo = [make_item(color='red'), make_item(color='pink')]
        self.assertFalse(check_color_clash(combo))
        self.assertLess(color_clash_soft_penalty(combo), 0)


class CategoryDuplicateTests(SimpleTestCase):
    def test_two_tops_rejected(self):
        combo = [make_item(category='top'), make_item(category='top')]
        self.assertTrue(check_category_duplicate(combo))

    def test_one_of_each_category_passes(self):
        combo = [make_item(category='top'), make_item(category='bottom'), make_item(category='shoes')]
        self.assertFalse(check_category_duplicate(combo))


class SeasonConflictTests(SimpleTestCase):
    def test_summer_and_winter_conflict_gets_soft_penalty(self):
        combo = [make_item(season='summer'), make_item(season='winter')]
        self.assertLess(season_conflict_soft_penalty(combo), 0)

    def test_multi_value_season_conflict_gets_soft_penalty(self):
        combo = [make_item(season='summer,all'), make_item(season='winter,all')]
        self.assertLess(season_conflict_soft_penalty(combo), 0)

    def test_adjacent_seasons_pass(self):
        combo = [make_item(season='spring'), make_item(season='fall')]
        self.assertEqual(season_conflict_soft_penalty(combo), 0.0)


class RequiredStructureTests(SimpleTestCase):
    def test_top_bottom_shoes_passes(self):
        combo = [
            make_item(category='top'),
            make_item(category='bottom'),
            make_item(category='shoes'),
        ]
        self.assertFalse(check_required_structure(combo))

    def test_dress_shoes_passes(self):
        combo = [make_item(category='dress'), make_item(category='shoes')]
        self.assertFalse(check_required_structure(combo))

    def test_combo_without_shoes_rejected(self):
        combo = [make_item(category='top'), make_item(category='bottom')]
        self.assertTrue(check_required_structure(combo))


class WeatherShoeTests(SimpleTestCase):
    def test_rain_rejects_suede_shoes(self):
        combo = [make_item(category='shoes', name='스웨이드 로퍼')]
        self.assertTrue(check_rain_shoes(combo, weather_code=61))

    def test_snow_rejects_heels(self):
        combo = [make_item(category='shoes', name='힐')]
        self.assertTrue(check_snow_shoes(combo, weather_code=71))


class IsHardViolatedTests(SimpleTestCase):
    def test_any_single_violation_triggers_master_check(self):
        combo = [make_item(category='shoes', name='운동화')]
        context = {'tpo': 'formal', 'temperature': 20.0, 'weather_code': 0}
        self.assertTrue(is_hard_violated(combo, context))

    def test_clean_combo_passes_master_check(self):
        combo = [
            make_item(category='top', color='white', material='면'),
            make_item(category='bottom', color='navy', material='면'),
            make_item(category='shoes', color='black', material='가죽', name='로퍼'),
        ]
        context = {'tpo': 'daily', 'temperature': 20.0, 'weather_code': 0}
        self.assertFalse(is_hard_violated(combo, context))


class ColorHarmonyTests(SimpleTestCase):
    def test_white_top_gives_harmony_bonus(self):
        combo = [make_item(color='white'), make_item(color='navy')]
        self.assertGreater(color_harmony_score(combo), 0)

    def test_red_pink_gives_penalty(self):
        combo = [make_item(color='red'), make_item(color='pink')]
        self.assertLess(color_harmony_score(combo), 0)


class MaterialLayeringTests(SimpleTestCase):
    def test_knit_under_coat_gives_bonus(self):
        combo = [
            make_item(category='top', name='니트 스웨터'),
            make_item(category='outer', name='울 코트'),
        ]
        self.assertGreater(material_layering_score(combo), 0)

    def test_padding_under_padding_gives_penalty(self):
        combo = [
            make_item(category='top', name='패딩 조끼'),
            make_item(category='outer', name='패딩 자켓'),
        ]
        self.assertLess(material_layering_score(combo), 0)

    def test_no_outer_gives_zero(self):
        combo = [make_item(category='top', name='니트 스웨터')]
        self.assertEqual(material_layering_score(combo), 0.0)


class TpoMaterialScoreTests(SimpleTestCase):
    def test_formal_wool_gives_bonus(self):
        combo = [make_item(category='top', material='울')]
        self.assertGreater(tpo_material_score(combo, 'formal'), 0)

    def test_unknown_tpo_gives_zero(self):
        combo = [make_item(category='top', material='울')]
        self.assertEqual(tpo_material_score(combo, 'unknown'), 0.0)


class LengthBalanceTests(SimpleTestCase):
    def test_crop_top_with_long_skirt_gives_bonus(self):
        combo = [
            make_item(category='top', name='크롭 니트'),
            make_item(category='bottom', name='롱치마'),
        ]
        self.assertGreater(length_balance_score(combo), 0)

    def test_long_top_with_long_bottom_gives_penalty(self):
        combo = [
            make_item(category='top', name='롱 가디건'),
            make_item(category='bottom', name='긴바지'),
        ]
        self.assertLess(length_balance_score(combo), 0)

    def test_unlabeled_items_are_skipped(self):
        combo = [
            make_item(category='top', name='기본 티셔츠'),
            make_item(category='bottom', name='기본 팬츠'),
        ]
        self.assertEqual(length_balance_score(combo), 0.0)


class SoftRuleScoreTests(SimpleTestCase):
    def test_combines_all_soft_layers(self):
        combo = [
            make_item(category='top', color='white', material='울', name='크롭 니트'),
            make_item(category='bottom', color='navy', material='면', name='롱치마'),
        ]
        context = {'tpo': 'formal'}
        self.assertGreater(soft_rule_score(combo, context), 0)
