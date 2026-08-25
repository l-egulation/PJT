from rest_framework.routers import DefaultRouter

from .views import SnapPostViewSet


router = DefaultRouter()
router.register('', SnapPostViewSet, basename='snaps')

urlpatterns = router.urls
