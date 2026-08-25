from rest_framework.routers import DefaultRouter

from .views import OutfitViewSet

router = DefaultRouter()
router.register('', OutfitViewSet, basename='outfits')

urlpatterns = router.urls
