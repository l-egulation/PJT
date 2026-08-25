from rest_framework import viewsets
from rest_framework.permissions import AllowAny

from .models import StyleTag
from .serializers import StyleTagSerializer


class StyleTagViewSet(viewsets.ReadOnlyModelViewSet):
    queryset = StyleTag.objects.all()
    serializer_class = StyleTagSerializer
    permission_classes = [AllowAny]
