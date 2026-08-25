from django.contrib.auth import get_user_model
from django.core.files.uploadedfile import SimpleUploadedFile
from django.test import override_settings
from rest_framework.test import APITestCase

from closet.models import ClothingItem
from .models import SnapComment, SnapFollow, SnapPost


TEST_STORAGES = {
    'default': {
        'BACKEND': 'django.core.files.storage.FileSystemStorage',
    },
    'staticfiles': {
        'BACKEND': 'django.contrib.staticfiles.storage.StaticFilesStorage',
    },
}


class SnapPostApiTests(APITestCase):
    def setUp(self):
        user_model = get_user_model()
        self.user = user_model.objects.create_user('snap-user', password='test-pass-123')
        self.other = user_model.objects.create_user('snap-other', password='test-pass-123')
        self.item = ClothingItem.objects.create(
            owner=self.user,
            name='Oxford shirt',
            category='top',
            season='spring,fall',
        )
        self.other_item = ClothingItem.objects.create(
            owner=self.other,
            name='Other pants',
            category='bottom',
            season='summer',
        )
        self.client.force_authenticate(self.user)

    def _payload(self, **overrides):
        payload = {
            'body': '#오늘의스냅 출근룩',
            'photos': ['https://example.com/snap-1.jpg'],
            'gender': '남성',
            'seasons': ['봄', '가을'],
            'styles': ['캐주얼'],
            'tpos': ['출근'],
            'height': 175,
            'weight': 75,
            'skin_tone': '여름 쿨톤',
            'clothing_item_ids': [self.item.id],
        }
        payload.update(overrides)
        return payload

    def test_create_snap_with_closet_items(self):
        response = self.client.post('/api/v1/snaps/', self._payload(), format='json')

        self.assertEqual(response.status_code, 201)
        self.assertEqual(response.data['gender'], 'male')
        self.assertEqual(response.data['gender_label'], '남성')
        self.assertEqual(response.data['photos'], ['https://example.com/snap-1.jpg'])
        self.assertEqual(response.data['clothing_items'][0]['id'], self.item.id)
        self.assertEqual(response.data['like_count'], 0)

    def test_create_snap_requires_photo(self):
        response = self.client.post('/api/v1/snaps/', self._payload(photos=[]), format='json')

        self.assertEqual(response.status_code, 400)
        self.assertIn('photos', response.data)

    @override_settings(MEDIA_ROOT='/tmp/pickle-snap-test-media', STORAGES=TEST_STORAGES)
    def test_upload_snap_photo_returns_media_url(self):
        image = SimpleUploadedFile(
            'snap.jpg',
            b'test-image-content',
            content_type='image/jpeg',
        )

        response = self.client.post('/api/v1/snaps/upload-photo/', {'image': image}, format='multipart')

        self.assertEqual(response.status_code, 201)
        self.assertIn('/media/snaps/photos/', response.data['url'])
        self.assertTrue(response.data['name'].endswith('.jpg'))

    def test_create_snap_rejects_other_users_closet_item(self):
        response = self.client.post(
            '/api/v1/snaps/',
            self._payload(clothing_item_ids=[self.other_item.id]),
            format='json',
        )

        self.assertEqual(response.status_code, 400)
        self.assertIn('clothing_item_ids', response.data)

    def test_filter_by_gender_and_season(self):
        SnapPost.objects.create(
            owner=self.user,
            body='summer',
            photos=['https://example.com/summer.jpg'],
            gender='female',
            seasons=['여름'],
        )
        SnapPost.objects.create(
            owner=self.user,
            body='fall',
            photos=['https://example.com/fall.jpg'],
            gender='male',
            seasons=['가을'],
        )

        response = self.client.get('/api/v1/snaps/?gender=남&season=가을')

        self.assertEqual(response.status_code, 200)
        self.assertEqual(len(response.data), 1)
        self.assertEqual(response.data[0]['body'], 'fall')

    def test_like_scrap_comment_and_follow_actions(self):
        snap = SnapPost.objects.create(
            owner=self.other,
            body='daily',
            photos=['https://example.com/daily.jpg'],
            gender='female',
        )

        like_response = self.client.post(f'/api/v1/snaps/{snap.id}/like/')
        scrap_response = self.client.post(f'/api/v1/snaps/{snap.id}/scrap/')
        comment_response = self.client.post(
            f'/api/v1/snaps/{snap.id}/comments/',
            {'body': '좋아요'},
            format='json',
        )
        follow_response = self.client.post(f'/api/v1/snaps/profiles/{self.other.username}/follow/')

        self.assertEqual(like_response.status_code, 200)
        self.assertTrue(like_response.data['is_liked'])
        self.assertEqual(like_response.data['like_count'], 1)
        self.assertEqual(scrap_response.status_code, 200)
        self.assertTrue(scrap_response.data['is_scrapped'])
        self.assertEqual(scrap_response.data['scrap_count'], 1)
        self.assertEqual(comment_response.status_code, 201)
        self.assertEqual(comment_response.data['body'], '좋아요')
        self.assertTrue(comment_response.data['can_edit'])
        self.assertEqual(follow_response.status_code, 200)
        self.assertTrue(follow_response.data['is_following'])
        self.assertTrue(SnapFollow.objects.filter(follower=self.user, following=self.other).exists())

        comment_id = comment_response.data['id']
        update_response = self.client.patch(
            f'/api/v1/snaps/{snap.id}/comments/{comment_id}/',
            {'body': '수정한 댓글'},
            format='json',
        )
        delete_response = self.client.delete(f'/api/v1/snaps/{snap.id}/comments/{comment_id}/')

        self.assertEqual(update_response.status_code, 200)
        self.assertEqual(update_response.data['body'], '수정한 댓글')
        self.assertEqual(delete_response.status_code, 204)
        self.assertFalse(SnapComment.objects.filter(id=comment_id).exists())

    def test_hide_excludes_snap_from_feed(self):
        snap = SnapPost.objects.create(
            owner=self.other,
            body='hidden',
            photos=['https://example.com/hidden.jpg'],
            gender='female',
        )

        hide_response = self.client.post(f'/api/v1/snaps/{snap.id}/hide/')
        list_response = self.client.get('/api/v1/snaps/')

        self.assertEqual(hide_response.status_code, 200)
        self.assertTrue(hide_response.data['is_hidden'])
        self.assertEqual(list_response.status_code, 200)
        self.assertEqual(list_response.data, [])

    def test_member_ranking_includes_snap_previews(self):
        SnapPost.objects.create(
            owner=self.other,
            body='ranking',
            photos=['https://example.com/ranking.jpg'],
            gender='female',
        )
        SnapFollow.objects.create(follower=self.user, following=self.other)

        response = self.client.get('/api/v1/snaps/members/ranking/')

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data[0]['username'], self.other.username)
        self.assertEqual(response.data[0]['follower_count'], 1)
        self.assertEqual(response.data[0]['snaps'][0]['photos'], ['https://example.com/ranking.jpg'])
