from django.contrib.auth import get_user_model
from rest_framework.test import APITestCase

from .models import StyleRequest


class StyleRequestPermissionTests(APITestCase):
    def setUp(self):
        user_model = get_user_model()
        self.owner = user_model.objects.create_user('owner', password='test-pass-123')
        self.other = user_model.objects.create_user('other', password='test-pass-123')
        self.style_request = StyleRequest.objects.create(
            requester=self.owner,
            title='Help me style this',
            body='Looking for a daily outfit.',
        )

    def test_another_user_cannot_update_request(self):
        self.client.force_authenticate(self.other)
        response = self.client.patch(
            f'/api/v1/style-requests/{self.style_request.id}/',
            {'title': 'Hijacked'},
            format='json',
        )
        self.assertEqual(response.status_code, 403)

    def test_another_user_can_suggest(self):
        self.client.force_authenticate(self.other)
        response = self.client.post(
            f'/api/v1/style-requests/{self.style_request.id}/suggest/',
            {'title': 'Try neutral colors', 'comment': 'Beige works well.'},
            format='json',
        )
        self.assertEqual(response.status_code, 201)
