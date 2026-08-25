from rest_framework import serializers

from closet.models import ClothingItem
from closet.serializers import ClothingItemSerializer
from .models import StyleRequest, StyleSuggestion


class StyleSuggestionSerializer(serializers.ModelSerializer):
    item_ids = serializers.ListField(
        child=serializers.IntegerField(),
        write_only=True,
        required=False,
    )
    items = ClothingItemSerializer(many=True, read_only=True)
    stylist_username = serializers.CharField(source='stylist.username', read_only=True)

    class Meta:
        model = StyleSuggestion
        fields = [
            'id',
            'stylist',
            'stylist_username',
            'title',
            'comment',
            'item_ids',
            'items',
            'shopping_links',
            'is_accepted',
            'created_at',
        ]
        read_only_fields = ['id', 'stylist', 'stylist_username', 'items', 'is_accepted', 'created_at']

    def validate_item_ids(self, value):
        style_request = self.context['style_request']
        owned_count = ClothingItem.objects.filter(
            owner=style_request.requester,
            id__in=set(value),
        ).count()

        if owned_count != len(set(value)):
            raise serializers.ValidationError('Suggestions must use the requester closet items.')

        return value

    def create(self, validated_data):
        item_ids = validated_data.pop('item_ids', [])
        suggestion = StyleSuggestion.objects.create(
            request=self.context['style_request'],
            stylist=self.context['request'].user,
            **validated_data,
        )

        if item_ids:
            suggestion.items.set(item_ids)

        return suggestion


class StyleRequestSerializer(serializers.ModelSerializer):
    requester_username = serializers.CharField(source='requester.username', read_only=True)
    target_item_detail = ClothingItemSerializer(source='target_item', read_only=True)
    suggestions = StyleSuggestionSerializer(many=True, read_only=True)

    class Meta:
        model = StyleRequest
        fields = [
            'id',
            'requester',
            'requester_username',
            'title',
            'body',
            'target_item',
            'target_item_detail',
            'tpo',
            'status',
            'suggestions',
            'created_at',
            'updated_at',
        ]
        read_only_fields = [
            'id',
            'requester',
            'requester_username',
            'target_item_detail',
            'suggestions',
            'created_at',
            'updated_at',
        ]

    def validate_target_item(self, value):
        if value and value.owner != self.context['request'].user:
            raise serializers.ValidationError('Only your own closet item can be selected.')
        return value
