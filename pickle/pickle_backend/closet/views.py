from PIL import UnidentifiedImageError
from cloudinary.exceptions import Error as CloudinaryError
from django.db.models import Q
from rest_framework import status, viewsets
from rest_framework.decorators import action
from rest_framework.parsers import FormParser, MultiPartParser
from rest_framework.response import Response

from .models import ClothingItem
from .ml.image_classifier import predict_image
from .serializers import ClothingItemSerializer


class ClothingItemViewSet(viewsets.ModelViewSet):
    serializer_class = ClothingItemSerializer

    def create(self, request, *args, **kwargs):
        try:
            return super().create(request, *args, **kwargs)
        except CloudinaryError:
            return Response(
                {
                    'detail': (
                        '이미지 저장소 연결에 실패했습니다. '
                        '서버의 CLOUDINARY_URL 설정을 확인해 주세요.'
                    )
                },
                status=status.HTTP_503_SERVICE_UNAVAILABLE,
            )

    def get_queryset(self):
        queryset = ClothingItem.objects.filter(owner=self.request.user)

        category = self.request.query_params.get('category')
        season = self.request.query_params.get('season')

        if category:
            queryset = queryset.filter(category=category)
        if season:
            queryset = queryset.filter(Q(season='all') | Q(season__contains=season))
        return queryset

    def perform_create(self, serializer):
        serializer.save(owner=self.request.user)

    @action(
        detail=False,
        methods=['post'],
        url_path='analyze-image',
        parser_classes=[MultiPartParser, FormParser],
    )
    def analyze_image(self, request):
        image = request.FILES.get('image')
        if image is None:
            return Response(
                {'image': 'An image file is required.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        try:
            prediction = predict_image(image)
        except FileNotFoundError:
            return Response(
                {'detail': 'The fashion image model has not been trained yet.'},
                status=status.HTTP_503_SERVICE_UNAVAILABLE,
            )
        except (UnidentifiedImageError, OSError, ValueError):
            return Response(
                {'image': 'Upload a valid image file.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        return Response(prediction)
