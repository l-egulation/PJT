from django.contrib.auth import get_user_model
from rest_framework.test import APITestCase
from unittest.mock import Mock, patch

from styles.models import StyleTag


class MyPageAccountApiTests(APITestCase):
    def setUp(self):
        user_model = get_user_model()
        self.user = user_model.objects.create_user(
            'picker',
            email='picker@example.com',
            password='test-pass-123',
            gender='unisex',
        )
        self.other = user_model.objects.create_user(
            'other',
            email='other@example.com',
            password='test-pass-123',
        )
        self.minimal = StyleTag.objects.create(name='Minimal', slug='minimal')
        self.casual = StyleTag.objects.create(name='Casual', slug='casual')
        self.user.style_tags.add(self.minimal)
        self.client.force_authenticate(self.user)

    def test_profile_edit_updates_profile_fields_only(self):
        response = self.client.patch('/api/v1/auth/profile/edit/', {
            'username': 'new-picker',
            'email': 'new-picker@example.com',
            'gender': 'female',
            'preferred_colors': 'ivory,blue',
        }, format='json')

        self.assertEqual(response.status_code, 200)
        self.user.refresh_from_db()
        self.assertEqual(self.user.username, 'new-picker')
        self.assertEqual(self.user.email, 'new-picker@example.com')
        self.assertEqual(self.user.gender, 'female')
        self.assertEqual(self.user.preferred_colors, 'ivory,blue')
        self.assertEqual(list(self.user.style_tags.values_list('id', flat=True)), [self.minimal.id])

    def test_profile_edit_rejects_duplicate_username_and_email(self):
        username_response = self.client.patch(
            '/api/v1/auth/profile/edit/',
            {'username': self.other.username},
            format='json',
        )
        email_response = self.client.patch(
            '/api/v1/auth/profile/edit/',
            {'email': self.other.email},
            format='json',
        )

        self.assertEqual(username_response.status_code, 400)
        self.assertEqual(email_response.status_code, 400)

    def test_check_username_returns_availability(self):
        taken_response = self.client.get('/api/v1/auth/check-username/', {
            'username': self.user.username,
        })
        available_response = self.client.get('/api/v1/auth/check-username/', {
            'username': 'new-picker',
        })

        self.assertEqual(taken_response.status_code, 200)
        self.assertFalse(taken_response.data['available'])
        self.assertEqual(available_response.status_code, 200)
        self.assertTrue(available_response.data['available'])

    def test_login_accepts_email_or_username(self):
        self.client.force_authenticate(user=None)

        email_response = self.client.post('/api/v1/auth/login/', {
            'email': self.user.email,
            'password': 'test-pass-123',
        }, format='json')
        username_response = self.client.post('/api/v1/auth/login/', {
            'username': self.user.username,
            'password': 'test-pass-123',
        }, format='json')

        self.assertEqual(email_response.status_code, 200)
        self.assertIn('access', email_response.data)
        self.assertEqual(username_response.status_code, 200)
        self.assertIn('access', username_response.data)

    @patch('accounts.views.requests.get')
    @patch('accounts.views.requests.post')
    def test_kakao_login_creates_user_and_tokens(self, post, get):
        self.client.force_authenticate(user=None)
        post.return_value = Mock(
            status_code=200,
            json=lambda: {'access_token': 'kakao-access-token'},
        )
        get.return_value = Mock(
            status_code=200,
            json=lambda: {
                'id': 12345,
                'kakao_account': {
                    'email': 'kakao@example.com',
                    'profile': {'nickname': 'kakao-picker'},
                },
            },
        )

        with patch('accounts.views.settings.KAKAO_REST_API_KEY', 'test-key'):
            response = self.client.post('/api/v1/auth/kakao/', {
                'code': 'auth-code',
                'redirect_uri': 'http://localhost:5173/oauth/kakao/callback',
            }, format='json')

        self.assertEqual(response.status_code, 200)
        self.assertIn('tokens', response.data)
        self.assertTrue(get_user_model().objects.filter(email='kakao@example.com').exists())

    def test_profile_edit_updates_password_when_provided(self):
        response = self.client.patch('/api/v1/auth/profile/edit/', {
            'password': 'new-pass-1234',
        }, format='json')

        self.assertEqual(response.status_code, 200)
        self.user.refresh_from_db()
        self.assertTrue(self.user.check_password('new-pass-1234'))

    def test_style_preferences_updates_style_tags_only(self):
        response = self.client.patch('/api/v1/auth/style-preferences/', {
            'style_tag_ids': [self.casual.id],
        }, format='json')

        self.assertEqual(response.status_code, 200)
        self.assertEqual(
            list(self.user.style_tags.values_list('id', flat=True)),
            [self.casual.id],
        )
        self.assertEqual(response.data['style_tags'][0]['slug'], 'casual')

    def test_account_settings_are_created_and_updated(self):
        get_response = self.client.get('/api/v1/auth/settings/')
        patch_response = self.client.patch('/api/v1/auth/settings/', {
            'push_notifications_enabled': False,
            'community_notifications_enabled': False,
        }, format='json')

        self.assertEqual(get_response.status_code, 200)
        self.assertTrue(get_response.data['weather_notifications_enabled'])
        self.assertEqual(patch_response.status_code, 200)
        self.assertFalse(patch_response.data['push_notifications_enabled'])
        self.assertFalse(patch_response.data['community_notifications_enabled'])

    def test_authenticated_user_can_delete_own_account(self):
        response = self.client.delete('/api/v1/auth/account/')

        self.assertEqual(response.status_code, 204)
        self.assertFalse(get_user_model().objects.filter(id=self.user.id).exists())

    def test_anonymous_user_cannot_delete_account(self):
        self.client.force_authenticate(user=None)

        response = self.client.delete('/api/v1/auth/account/')

        self.assertEqual(response.status_code, 401)
        self.assertTrue(get_user_model().objects.filter(id=self.user.id).exists())
