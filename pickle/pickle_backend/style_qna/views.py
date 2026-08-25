from rest_framework import status, viewsets
from rest_framework.decorators import action
from rest_framework.response import Response
from django.shortcuts import get_object_or_404

from .models import StyleRequest, StyleSuggestion
from .serializers import StyleRequestSerializer, StyleSuggestionSerializer
from .permissions import IsRequesterOrReadOnly


class StyleRequestViewSet(viewsets.ModelViewSet):
    serializer_class = StyleRequestSerializer
    permission_classes = [IsRequesterOrReadOnly]

    def get_queryset(self):
        queryset = StyleRequest.objects.select_related(
            'requester',
            'target_item',
        ).prefetch_related('suggestions')

        mine = self.request.query_params.get('mine')
        status_value = self.request.query_params.get('status')

        if mine in {'true', '1'}:
            queryset = queryset.filter(requester=self.request.user)
        if status_value:
            queryset = queryset.filter(status=status_value)

        return queryset

    def perform_create(self, serializer):
        serializer.save(requester=self.request.user)

    @action(detail=True, methods=['post'])
    def suggest(self, request, pk=None):
        style_request = self.get_object()
        if style_request.status == 'closed':
            return Response(
                {'detail': 'This style request is already closed.'},
                status=status.HTTP_409_CONFLICT,
            )
        serializer = StyleSuggestionSerializer(
            data=request.data,
            context={
                'request': request,
                'style_request': style_request,
            },
        )
        serializer.is_valid(raise_exception=True)
        suggestion = serializer.save()
        return Response(
            StyleSuggestionSerializer(
                suggestion,
                context={
                    'request': request,
                    'style_request': style_request,
                },
            ).data,
            status=status.HTTP_201_CREATED,
        )

    @action(detail=True, methods=['post'], url_path='accept-suggestion')
    def accept_suggestion(self, request, pk=None):
        style_request = self.get_object()
        suggestion_id = request.data.get('suggestion_id')
        suggestion = get_object_or_404(
            StyleSuggestion,
            id=suggestion_id,
            request=style_request,
        )

        StyleSuggestion.objects.filter(request=style_request).update(is_accepted=False)
        suggestion.is_accepted = True
        suggestion.save(update_fields=['is_accepted'])
        style_request.status = 'closed'
        style_request.save(update_fields=['status', 'updated_at'])

        return Response({'accepted_suggestion_id': suggestion.id})
