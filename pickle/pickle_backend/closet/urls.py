from rest_framework.routers import DefaultRouter

from .views import ClothingItemViewSet

router = DefaultRouter()
router.register('', ClothingItemViewSet, basename='clothes')

urlpatterns = router.urls
