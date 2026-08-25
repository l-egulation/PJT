from rest_framework.routers import DefaultRouter

from .views import StyleTagViewSet

router = DefaultRouter()
router.register('', StyleTagViewSet, basename='style-tags')

urlpatterns = router.urls
