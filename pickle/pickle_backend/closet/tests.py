from io import BytesIO
from unittest.mock import patch

from cloudinary.exceptions import Error as CloudinaryError
from django.contrib.auth import get_user_model
from django.core.files.base import ContentFile
from django.test import SimpleTestCase
from PIL import Image
from rest_framework.test import APITestCase

from config.storage import CloudinaryMediaStorage
from .models import ClothingItem


class CloudinaryMediaStorageTests(SimpleTestCase):
    @patch('config.storage.cloudinary.uploader.upload')
    def test_save_returns_cloudinary_public_id(self, upload):
        upload.return_value = {
            'public_id': 'pickle/clothes/originals/shirt_123',
        }
        storage = CloudinaryMediaStorage()

        name = storage.save(
            'clothes/originals/shirt.jpg',
            ContentFile(b'image-bytes'),
        )

        self.assertEqual(name, 'pickle/clothes/originals/shirt_123')
        upload.assert_called_once()

    @patch('config.storage.cloudinary.uploader.destroy')
    def test_delete_invalidates_cloudinary_asset(self, destroy):
        storage = CloudinaryMediaStorage()

        storage.delete('pickle/clothes/originals/shirt_123')

        destroy.assert_called_once_with(
            'pickle/clothes/originals/shirt_123',
            resource_type='image',
            invalidate=True,
        )


class ClothingImageAnalysisTests(APITestCase):
    def setUp(self):
        self.user = get_user_model().objects.create_user('closet-user', password='test-pass-123')
        self.client.force_authenticate(self.user)

    @patch('closet.views.predict_image')
    def test_analyze_image_returns_model_prediction(self, predict):
        predict.return_value = {
            'category': 'top',
            'season': 'summer',
            'color': 'blue',
            'confidence': {'category': 0.8, 'season': 0.6},
            'model': {'type': 'HOG + SGDClassifier'},
        }
        image = BytesIO()
        Image.new('RGB', (32, 32), 'blue').save(image, format='JPEG')
        image.seek(0)
        image.name = 'shirt.jpg'
        response = self.client.post('/api/v1/closet/analyze-image/', {'image': image}, format='multipart')
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data['category'], 'top')
        self.assertEqual(response.data['color'], 'blue')

    def test_analyze_image_requires_file(self):
        response = self.client.post('/api/v1/closet/analyze-image/', {}, format='multipart')
        self.assertEqual(response.status_code, 400)


class ClothingItemCrudTests(APITestCase):
    def setUp(self):
        user_model = get_user_model()
        self.owner = user_model.objects.create_user('closet-owner', password='test-pass-123')
        self.other_user = user_model.objects.create_user('closet-other', password='test-pass-123')
        self.item = ClothingItem.objects.create(
            owner=self.owner,
            name='Linen shirt',
            category='top',
            color='ivory',
            material='linen',
            season='summer',
        )

    def test_owner_can_update_and_delete_item(self):
        self.client.force_authenticate(self.owner)

        update_response = self.client.patch(
            f'/api/v1/closet/{self.item.id}/',
            {'name': 'Updated linen shirt', 'color': 'skyblue'},
            format='json',
        )

        self.assertEqual(update_response.status_code, 200)
        self.item.refresh_from_db()
        self.assertEqual(self.item.name, 'Updated linen shirt')
        self.assertEqual(self.item.color, 'skyblue')

        delete_response = self.client.delete(f'/api/v1/closet/{self.item.id}/')
        self.assertEqual(delete_response.status_code, 204)
        self.assertFalse(ClothingItem.objects.filter(id=self.item.id).exists())

    def test_other_user_cannot_read_update_or_delete_item(self):
        self.client.force_authenticate(self.other_user)
        url = f'/api/v1/closet/{self.item.id}/'

        self.assertEqual(self.client.get(url).status_code, 404)
        self.assertEqual(self.client.patch(url, {'name': 'Stolen'}, format='json').status_code, 404)
        self.assertEqual(self.client.delete(url).status_code, 404)

    @patch(
        'rest_framework.mixins.CreateModelMixin.create',
        side_effect=CloudinaryError('invalid credentials'),
    )
    def test_cloudinary_upload_failure_returns_service_unavailable(self, create):
        self.client.force_authenticate(self.owner)

        response = self.client.post(
            '/api/v1/closet/',
            {'name': 'Cloud item', 'category': 'top'},
            format='json',
        )

        self.assertEqual(response.status_code, 503)
        self.assertIn('CLOUDINARY_URL', response.data['detail'])
