from rest_framework.routers import DefaultRouter

from .views import StyleRequestViewSet

router = DefaultRouter()
router.register('', StyleRequestViewSet, basename='style-requests')

urlpatterns = router.urls
