from rest_framework import status, viewsets
from rest_framework.decorators import action
from rest_framework.permissions import AllowAny
from rest_framework.response import Response

from .models import Outfit, OutfitEvaluation, RecommendationEvent
from .serializers import OutfitEvaluationSerializer, OutfitSerializer
from .services import AVAILABLE_PREFERRED_STYLES, fetch_weather, recommend_items
from closet.serializers import ClothingItemSerializer


class OutfitViewSet(viewsets.ModelViewSet):
    serializer_class = OutfitSerializer

    def get_queryset(self):
        queryset = Outfit.objects.filter(owner=self.request.user).prefetch_related(
            'items',
            'style_tags',
        )

        tpo = self.request.query_params.get('tpo')
        public = self.request.query_params.get('public')

        if tpo:
            queryset = queryset.filter(tpo=tpo)
        if public in {'true', '1'}:
            queryset = queryset.filter(is_public=True)

        return queryset

    @action(detail=False, methods=['get'], permission_classes=[AllowAny])
    def weather(self, request):
        try:
            latitude = float(request.query_params.get('latitude', 36.3504))
            longitude = float(request.query_params.get('longitude', 127.3845))
            if not -90 <= latitude <= 90 or not -180 <= longitude <= 180:
                raise ValueError
            return Response(fetch_weather(latitude, longitude))
        except (ValueError, TypeError):
            return Response(
                {'detail': 'Latitude and longitude must be numbers.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        except Exception:
            return Response(
                {'detail': 'The weather service is temporarily unavailable.'},
                status=status.HTTP_503_SERVICE_UNAVAILABLE,
            )

    @action(detail=False, methods=['post'])
    def recommend(self, request):
        tpo = request.data.get('tpo')
        preferred_style = request.data.get('preferred_style')
        preferred_colors = request.data.get('preferred_colors', [])
        fixed_item_id = request.data.get('fixed_item_id')
        excluded_item_signatures = request.data.get('excluded_item_signatures', [])
        allowed_tpos = {value for value, _ in Outfit.TPO_CHOICES}
        if tpo not in allowed_tpos:
            return Response({'tpo': 'TPO is required.'}, status=status.HTTP_400_BAD_REQUEST)
        if preferred_style not in AVAILABLE_PREFERRED_STYLES:
            return Response(
                {'preferred_style': 'Choose a valid preferred style.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        if not isinstance(preferred_colors, list):
            return Response(
                {'preferred_colors': 'Expected a list of color names.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        if not isinstance(excluded_item_signatures, list):
            return Response(
                {'excluded_item_signatures': 'Expected a list of item id lists.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        try:
            excluded_item_signatures = [
                [int(item_id) for item_id in signature]
                for signature in excluded_item_signatures
                if isinstance(signature, list)
            ]
        except (TypeError, ValueError):
            return Response(
                {'excluded_item_signatures': 'Expected a list of item id lists.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        if fixed_item_id in {'', None}:
            fixed_item_id = None
        else:
            try:
                fixed_item_id = int(fixed_item_id)
            except (TypeError, ValueError):
                return Response(
                    {'fixed_item_id': 'Choose a valid closet item.'},
                    status=status.HTTP_400_BAD_REQUEST,
                )
        manual_weather = request.data.get('weather')
        if manual_weather:
            try:
                temperature = float(manual_weather.get('temperature'))
                weather_code = int(manual_weather.get('weather_code', 0))
                if not -30 <= temperature <= 45:
                    raise ValueError
                weather = {
                    'temperature': temperature,
                    'weather_code': weather_code,
                    'source': 'Manual',
                }
            except (AttributeError, ValueError, TypeError):
                return Response(
                    {'weather': 'Temperature must be between -30 and 45.'},
                    status=status.HTTP_400_BAD_REQUEST,
                )
        else:
            try:
                latitude = float(request.data.get('latitude', 36.3504))
                longitude = float(request.data.get('longitude', 127.3845))
                if not -90 <= latitude <= 90 or not -180 <= longitude <= 180:
                    raise ValueError
                weather = fetch_weather(latitude, longitude)
            except (ValueError, TypeError):
                return Response(
                    {'detail': 'Latitude and longitude must be numbers.'},
                    status=status.HTTP_400_BAD_REQUEST,
                )
            except Exception:
                return Response(
                    {'detail': 'The weather service is temporarily unavailable.'},
                    status=status.HTTP_503_SERVICE_UNAVAILABLE,
                )

        try:
            result = recommend_items(
                request.user,
                tpo=tpo,
                preferred_style=preferred_style,
                preferred_colors=preferred_colors,
                weather=weather,
                fixed_item_id=fixed_item_id,
                excluded_item_signatures=excluded_item_signatures,
            )
        except ValueError as error:
            if str(error) == 'fixed_item_not_recommendable':
                return Response(
                    {'fixed_item_id': 'This closet item cannot be used in outfit recommendations.'},
                    status=status.HTTP_400_BAD_REQUEST,
                )
            return Response(
                {'fixed_item_id': 'Choose one of your own closet items.'},
                status=status.HTTP_400_BAD_REQUEST,
            )
        event = RecommendationEvent.objects.create(
            user=request.user,
            tpo=tpo,
            temperature=weather['temperature'],
            weather_code=weather['weather_code'],
            item_ids=[item.id for item in result['items']],
            features=result.pop('features'),
        )
        result['items'] = ClothingItemSerializer(
            result['items'], many=True, context={'request': request}
        ).data
        result['recommendation_id'] = event.id
        return Response(result)

    @action(detail=True, methods=['put', 'patch'])
    def evaluate(self, request, pk=None):
        outfit = self.get_object()
        evaluation = getattr(outfit, 'evaluation', None)
        serializer = OutfitEvaluationSerializer(
            evaluation,
            data=request.data,
            partial=evaluation is not None and request.method == 'PATCH',
        )
        serializer.is_valid(raise_exception=True)

        if evaluation:
            serializer.save()
        else:
            serializer.save(outfit=outfit)

        try:
            event = outfit.recommendation_event
        except RecommendationEvent.DoesNotExist:
            event = None
        if event:
            event.rating = serializer.validated_data['rating']
            event.save(update_fields=['rating'])

        return Response(serializer.data, status=status.HTTP_200_OK)
