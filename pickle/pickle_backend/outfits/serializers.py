from django.db import transaction
from rest_framework import serializers

from closet.models import ClothingItem
from closet.serializers import ClothingItemSerializer
from styles.models import StyleTag
from styles.serializers import StyleTagSerializer
from .models import Outfit, OutfitEvaluation, OutfitItem, RecommendationEvent


class OutfitEvaluationSerializer(serializers.ModelSerializer):
    class Meta:
        model = OutfitEvaluation
        fields = [
            'rating',
            'fit_score',
            'activity_score',
            'satisfaction_score',
            'feedback',
            'created_at',
            'updated_at',
        ]
        read_only_fields = ['created_at', 'updated_at']

    def validate_rating(self, value):
        if not 1 <= value <= 5:
            raise serializers.ValidationError('Rating must be between 1 and 5.')
        return value

    def validate(self, attrs):
        for field in ('fit_score', 'activity_score', 'satisfaction_score'):
            value = attrs.get(field)
            if value is not None and not 1 <= value <= 5:
                raise serializers.ValidationError({field: 'Score must be between 1 and 5.'})
        return attrs


class OutfitSerializer(serializers.ModelSerializer):
    recommendation_id = serializers.IntegerField(write_only=True, required=False)
    item_ids = serializers.ListField(
        child=serializers.IntegerField(),
        write_only=True,
        required=False,
    )
    style_tag_ids = serializers.ListField(
        child=serializers.IntegerField(),
        write_only=True,
        required=False,
    )
    items = ClothingItemSerializer(many=True, read_only=True)
    style_tags = StyleTagSerializer(many=True, read_only=True)
    evaluation = OutfitEvaluationSerializer(read_only=True)

    class Meta:
        model = Outfit
        fields = [
            'id',
            'title',
            'tpo',
            'description',
            'weather_note',
            'worn_on',
            'is_public',
            'item_ids',
            'recommendation_id',
            'items',
            'style_tag_ids',
            'style_tags',
            'evaluation',
            'created_at',
            'updated_at',
        ]
        read_only_fields = ['id', 'items', 'style_tags', 'evaluation', 'created_at', 'updated_at']

    def validate_item_ids(self, value):
        user = self.context['request'].user
        existing_count = ClothingItem.objects.filter(
            owner=user,
            id__in=set(value),
        ).count()

        if existing_count != len(set(value)):
            raise serializers.ValidationError('Only your own closet items can be used.')

        return value

    def validate_style_tag_ids(self, value):
        existing_count = StyleTag.objects.filter(id__in=set(value)).count()

        if existing_count != len(set(value)):
            raise serializers.ValidationError('One or more style tags do not exist.')

        return value

    def _set_items(self, outfit, item_ids):
        OutfitItem.objects.filter(outfit=outfit).delete()
        OutfitItem.objects.bulk_create([
            OutfitItem(outfit=outfit, clothing_item_id=item_id, sort_order=index)
            for index, item_id in enumerate(item_ids)
        ])

    def create(self, validated_data):
        item_ids = validated_data.pop('item_ids', [])
        style_tag_ids = validated_data.pop('style_tag_ids', [])
        recommendation_id = validated_data.pop('recommendation_id', None)

        recommendation = None
        if recommendation_id is not None:
            recommendation = RecommendationEvent.objects.filter(
                id=recommendation_id,
                user=self.context['request'].user,
                outfit__isnull=True,
            ).first()
            if recommendation is None:
                raise serializers.ValidationError({
                    'recommendation_id': 'Recommendation does not exist or was already saved.'
                })

        with transaction.atomic():
            outfit = Outfit.objects.create(
                owner=self.context['request'].user,
                **validated_data,
            )
            if item_ids:
                self._set_items(outfit, item_ids)
            if style_tag_ids:
                outfit.style_tags.set(style_tag_ids)
            if recommendation:
                recommendation.outfit = outfit
                recommendation.saved = True
                recommendation.was_worn = bool(outfit.worn_on)
                recommendation.save(update_fields=['outfit', 'saved', 'was_worn'])

        return outfit

    def update(self, instance, validated_data):
        item_ids = validated_data.pop('item_ids', None)
        style_tag_ids = validated_data.pop('style_tag_ids', None)

        with transaction.atomic():
            for attr, value in validated_data.items():
                setattr(instance, attr, value)
            instance.save()

            if item_ids is not None:
                self._set_items(instance, item_ids)
            if style_tag_ids is not None:
                instance.style_tags.set(style_tag_ids)
            try:
                recommendation = instance.recommendation_event
            except RecommendationEvent.DoesNotExist:
                recommendation = None
            if recommendation:
                recommendation.was_worn = bool(instance.worn_on)
                recommendation.save(update_fields=['was_worn'])

        return instance
