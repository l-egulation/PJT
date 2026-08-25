from rest_framework import serializers
from django.db.models import Max

from .ml.image_classifier import predict_style
from .ml.image_processing import process_clothing_image, process_flatlay_image
from .models import ClothingItem
from .style_options import AIHUB_PARENT_STYLE_VALUES, LEGACY_STYLE_FAMILIES


VALID_SEASONS = {'spring', 'summer', 'fall', 'winter', 'all'}
VALID_STYLES = AIHUB_PARENT_STYLE_VALUES | set(LEGACY_STYLE_FAMILIES)
LEGACY_STYLE_LABELS = {
    'minimal': '미니멀',
    'casual': '캐주얼',
    'lovely': '러블리',
    'street': '스트릿',
    'formal': '포멀',
    'chic': '시크',
}


class ClothingItemSerializer(serializers.ModelSerializer):
    style = serializers.CharField(required=False, allow_blank=True)
    wear_count = serializers.SerializerMethodField()
    last_worn = serializers.SerializerMethodField()
    paired_outfits = serializers.SerializerMethodField()

    class Meta:
        model = ClothingItem
        fields = [
            'id',
            'name',
            'category',
            'color',
            'material',
            'style',
            'aihub_style',
            'style_confidence',
            'season',
            'image',
            'processed_image',
            'flatlay_image',
            'shopping_url',
            'memo',
            'wear_count',
            'last_worn',
            'paired_outfits',
            'created_at',
            'updated_at',
        ]
        read_only_fields = [
            'id',
            'processed_image',
            'flatlay_image',
            'style_confidence',
            'wear_count',
            'last_worn',
            'paired_outfits',
            'created_at',
            'updated_at',
        ]

    def get_wear_count(self, obj):
        if not self._include_detail_metrics():
            return 0
        return obj.outfits.filter(owner=obj.owner, worn_on__isnull=False).count()

    def get_last_worn(self, obj):
        if not self._include_detail_metrics():
            return None
        worn = obj.outfits.filter(owner=obj.owner).aggregate(last=Max('worn_on'))['last']
        return worn.isoformat() if worn else None

    def get_paired_outfits(self, obj):
        if not self._include_detail_metrics():
            return []
        outfits = (
            obj.outfits
            .filter(owner=obj.owner)
            .prefetch_related('items')
            .order_by('-worn_on', '-created_at')[:3]
        )
        return [self._serialize_paired_outfit(outfit) for outfit in outfits]

    def _include_detail_metrics(self):
        view = self.context.get('view')
        return getattr(view, 'action', None) == 'retrieve'

    def _serialize_paired_outfit(self, outfit):
        return {
            'id': outfit.id,
            'title': outfit.title,
            'tpo': outfit.tpo,
            'worn_on': outfit.worn_on.isoformat() if outfit.worn_on else None,
            'created_at': outfit.created_at.isoformat() if outfit.created_at else None,
            'items': [self._serialize_outfit_item(item) for item in outfit.items.all()],
        }

    def _serialize_outfit_item(self, item):
        return {
            'id': item.id,
            'name': item.name,
            'category': item.category,
            'color': item.color,
            'image': item.image.url if item.image else '',
            'processed_image': item.processed_image.url if item.processed_image else '',
            'flatlay_image': item.flatlay_image.url if item.flatlay_image else '',
        }

    def validate_season(self, value):
        if isinstance(value, list):
            seasons = value
        else:
            seasons = str(value or '').split(',')
        cleaned = []
        for season in seasons:
            season = str(season).strip()
            if season and season not in cleaned:
                cleaned.append(season)
        if not cleaned:
            return 'all'
        invalid = [season for season in cleaned if season not in VALID_SEASONS]
        if invalid:
            raise serializers.ValidationError(f'Invalid season: {", ".join(invalid)}')
        if 'all' in cleaned:
            return 'all'
        return ','.join(cleaned)

    def validate_style(self, value):
        value = str(value or '').strip()
        if value in LEGACY_STYLE_LABELS:
            return LEGACY_STYLE_LABELS[value]
        if value and value not in VALID_STYLES:
            raise serializers.ValidationError(f'Invalid style: {value}')
        return value

    def _attach_processed_image(self, validated_data):
        image = validated_data.get('image')
        if image:
            needs_style = not validated_data.get('style')
            needs_material = not validated_data.get('material')
            if needs_style or needs_material:
                style_prediction = predict_style(image)
                if style_prediction:
                    if needs_style:
                        validated_data['style'] = style_prediction['style']
                        validated_data['aihub_style'] = style_prediction.get('aihub_style', '')
                        validated_data['style_confidence'] = style_prediction['confidence']
                    if needs_material and style_prediction.get('material'):
                        validated_data['material'] = style_prediction['material']
            image.seek(0)
            name = getattr(image, 'name', 'clothing.png')
            validated_data['processed_image'] = process_clothing_image(image, name)
            image.seek(0)
            validated_data['flatlay_image'] = process_flatlay_image(image, name)
        return validated_data

    def create(self, validated_data):
        return super().create(self._attach_processed_image(validated_data))

    def update(self, instance, validated_data):
        return super().update(instance, self._attach_processed_image(validated_data))

    def to_representation(self, instance):
        data = super().to_representation(instance)
        if data.get('processed_image'):
            data['original_image'] = data.get('image')
            data['image'] = data['processed_image']
        return data
