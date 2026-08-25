from unittest.mock import patch

from django.contrib.auth import get_user_model
from rest_framework.test import APITestCase

from closet.models import ClothingItem


class RecommendationTests(APITestCase):
    def setUp(self):
        self.user = get_user_model().objects.create_user('picker', password='test-pass-123')
        ClothingItem.objects.create(
            owner=self.user,
            name='Favorite summer shirt',
            category='top',
            color='blue',
            season='summer',
        )
        ClothingItem.objects.create(
            owner=self.user,
            name='Winter shirt',
            category='top',
            color='black',
            season='winter',
        )
        ClothingItem.objects.create(
            owner=self.user,
            name='All-season pants',
            category='bottom',
            color='beige',
            season='all',
        )
        self.client.force_authenticate(self.user)

    @patch('outfits.views.fetch_weather')
    def test_weather_uses_requested_coordinates(self, weather):
        weather.return_value = {
            'temperature': 25.5,
            'weather_code': 1,
            'source': 'Open-Meteo',
        }

        response = self.client.get(
            '/api/v1/outfits/weather/',
            {'latitude': 35.1796, 'longitude': 129.0756},
        )

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data['temperature'], 25.5)
        weather.assert_called_once_with(35.1796, 129.0756)

    @patch('outfits.views.fetch_weather')
    def test_weather_can_be_checked_without_login(self, weather):
        self.client.force_authenticate(user=None)
        weather.return_value = {
            'temperature': 22,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }

        response = self.client.get('/api/v1/outfits/weather/')

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data['temperature'], 22)

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_recommendation_uses_weather_and_preferences(self, weather, _model):
        weather.return_value = {
            'temperature': 27,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {
                'tpo': 'daily',
                'preferred_style': 'casual',
                'preferred_colors': ['blue'],
            },
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertIn('summer', response.data['season'])
        self.assertEqual(response.data['items'][0]['name'], 'Favorite summer shirt')
        self.assertEqual(len(response.data['items']), 2)
        self.assertIn('recommendation_id', response.data)

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_recommendation_can_use_manual_future_weather(self, weather, _model):
        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {
                'tpo': 'travel',
                'preferred_style': 'casual',
                'weather': {'temperature': 27, 'weather_code': 1},
            },
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data['weather']['temperature'], 27)
        self.assertEqual(response.data['weather']['source'], 'Manual')
        weather.assert_not_called()

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_preferred_style_changes_item_ranking(self, weather, _model):
        weather.return_value = {
            'temperature': 27,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }
        black_top = ClothingItem.objects.create(
            owner=self.user,
            name='Black linen shirt',
            category='top',
            color='black',
            material='linen',
            season='summer',
        )

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'chic'},
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data['preferred_style'], 'chic')
        self.assertEqual(response.data['items'][0]['id'], black_top.id)
        self.assertEqual(response.data['scoring']['weights']['style_color_match'], 4)

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_very_hot_weather_excludes_regular_outerwear(self, weather, _model):
        weather.return_value = {
            'temperature': 29,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }
        ClothingItem.objects.create(
            owner=self.user,
            name='Suede jacket',
            category='outer',
            color='brown',
            material='suede',
            season='all',
            style='casual',
        )

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'casual'},
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertNotIn('outer', {item['category'] for item in response.data['items']})

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_mild_weather_does_not_force_light_outerwear(self, weather, _model):
        weather.return_value = {
            'temperature': 22,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }
        ClothingItem.objects.create(
            owner=self.user,
            name='Spring windbreaker',
            category='outer',
            color='blue',
            season='spring',
            style='casual',
        )

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'casual', 'preferred_colors': ['blue']},
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertNotIn('outer', {item['category'] for item in response.data['items']})

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_twenty_degrees_excludes_regular_outerwear(self, weather, _model):
        weather.return_value = {
            'temperature': 20,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }
        ClothingItem.objects.create(
            owner=self.user,
            name='Navy cardigan',
            category='outer',
            color='navy',
            season='spring',
            style='casual',
        )

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'casual', 'preferred_colors': ['navy']},
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertNotIn('outer', {item['category'] for item in response.data['items']})

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_formal_recommendation_rejects_rain_boots(self, weather, _model):
        weather.return_value = {
            'temperature': 15,
            'weather_code': 61,
            'source': 'Open-Meteo',
        }
        ClothingItem.objects.create(
            owner=self.user,
            name='Black loafers',
            category='shoes',
            color='black',
            material='leather',
            season='all',
            style='올드머니',
        )
        ClothingItem.objects.create(
            owner=self.user,
            name='Rain boots',
            category='shoes',
            color='black',
            material='rubber',
            season='all',
            style='올드머니',
        )

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'formal', 'preferred_style': '올드머니'},
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertNotIn('Rain boots', {item['name'] for item in response.data['items']})

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_recommendation_excludes_bag_from_outfit_items(self, weather, _model):
        weather.return_value = {
            'temperature': 22,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }
        ClothingItem.objects.create(
            owner=self.user,
            name='Ivory shoulder bag',
            category='bag',
            color='ivory',
            season='all',
            style='casual',
        )

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'casual', 'preferred_colors': ['ivory']},
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertNotIn('bag', {item['category'] for item in response.data['items']})

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_recommendation_can_pin_detail_item(self, weather, _model):
        weather.return_value = {
            'temperature': 27,
            'weather_code': 0,
            'source': 'Open-Meteo',
        }
        fixed_item = ClothingItem.objects.get(owner=self.user, name='Winter shirt')

        response = self.client.post(
            '/api/v1/outfits/recommend/',
            {
                'tpo': 'daily',
                'preferred_style': 'casual',
                'fixed_item_id': fixed_item.id,
            },
            format='json',
        )

        self.assertEqual(response.status_code, 200)
        self.assertIn(fixed_item.id, {item['id'] for item in response.data['items']})
        self.assertEqual(response.data['fixed_item_id'], fixed_item.id)

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_recent_combination_is_not_repeated(self, weather, _model):
        weather.return_value = {'temperature': 27, 'weather_code': 0, 'source': 'Open-Meteo'}
        payload = {'tpo': 'daily', 'preferred_style': 'casual'}
        first = self.client.post('/api/v1/outfits/recommend/', payload, format='json')
        second = self.client.post('/api/v1/outfits/recommend/', payload, format='json')
        self.assertNotEqual(
            [item['id'] for item in first.data['items']],
            [item['id'] for item in second.data['items']],
        )

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_saving_outfit_marks_recommendation_positive(self, weather, _model):
        weather.return_value = {'temperature': 27, 'weather_code': 0, 'source': 'Open-Meteo'}
        recommendation = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'casual'},
            format='json',
        )
        response = self.client.post('/api/v1/outfits/', {
            'title': 'Saved recommendation',
            'tpo': 'daily',
            'item_ids': [item['id'] for item in recommendation.data['items']],
            'recommendation_id': recommendation.data['recommendation_id'],
        }, format='json')
        self.assertEqual(response.status_code, 201)
        event = self.user.recommendation_events.get(id=recommendation.data['recommendation_id'])
        self.assertTrue(event.saved)
        self.assertEqual(event.outfit_id, response.data['id'])

    @patch('outfits.services._load_model', return_value=None)
    @patch('outfits.views.fetch_weather')
    def test_evaluation_updates_recommendation_event(self, weather, _model):
        weather.return_value = {'temperature': 27, 'weather_code': 0, 'source': 'Open-Meteo'}
        recommendation = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily', 'preferred_style': 'casual'},
            format='json',
        )
        outfit = self.client.post('/api/v1/outfits/', {
            'title': 'Saved recommendation',
            'tpo': 'daily',
            'item_ids': [item['id'] for item in recommendation.data['items']],
            'recommendation_id': recommendation.data['recommendation_id'],
        }, format='json')

        response = self.client.put(f'/api/v1/outfits/{outfit.data["id"]}/evaluate/', {
            'rating': 4,
            'fit_score': 3,
            'activity_score': 5,
            'satisfaction_score': 4,
            'feedback': 'Good fit.',
        }, format='json')

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data['rating'], 4)
        event = self.user.recommendation_events.get(id=recommendation.data['recommendation_id'])
        self.assertEqual(event.rating, 4)
        self.assertTrue(event.liked)

    @patch('outfits.views.fetch_weather')
    def test_recommendation_requires_tpo_and_style(self, weather):
        weather.return_value = {'temperature': 20, 'weather_code': 0, 'source': 'Open-Meteo'}

        missing_tpo = self.client.post(
            '/api/v1/outfits/recommend/',
            {'preferred_style': 'minimal'},
            format='json',
        )
        missing_style = self.client.post(
            '/api/v1/outfits/recommend/',
            {'tpo': 'daily'},
            format='json',
        )

        self.assertEqual(missing_tpo.status_code, 400)
        self.assertIn('tpo', missing_tpo.data)
        self.assertEqual(missing_style.status_code, 400)
        self.assertIn('preferred_style', missing_style.data)
