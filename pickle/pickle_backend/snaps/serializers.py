from django.contrib.auth import get_user_model
from django.db import transaction
from rest_framework import serializers

from closet.models import ClothingItem
from closet.serializers import ClothingItemSerializer
from .models import SnapClothingItem, SnapComment, SnapFollow, SnapPost


User = get_user_model()

GENDER_INPUT = {
    'male': 'male',
    'female': 'female',
    '남성': 'male',
    '여성': 'female',
    '남': 'male',
    '여': 'female',
}


class SnapUserSerializer(serializers.ModelSerializer):
    post_count = serializers.IntegerField(read_only=True, default=0)
    follower_count = serializers.IntegerField(read_only=True, default=0)
    following_count = serializers.IntegerField(read_only=True, default=0)
    is_following = serializers.SerializerMethodField()

    class Meta:
        model = User
        fields = [
            'id',
            'username',
            'gender',
            'preferred_colors',
            'post_count',
            'follower_count',
            'following_count',
            'is_following',
        ]

    def get_is_following(self, obj):
        request = self.context.get('request')
        if not request or not request.user.is_authenticated:
            return False
        following_user_ids = self.context.get('following_user_ids')
        if following_user_ids is not None:
            return obj.id in following_user_ids
        return SnapFollow.objects.filter(follower=request.user, following=obj).exists()


class SnapCommentSerializer(serializers.ModelSerializer):
    username = serializers.CharField(source='user.username', read_only=True)
    can_edit = serializers.SerializerMethodField()

    class Meta:
        model = SnapComment
        fields = ['id', 'username', 'body', 'created_at', 'can_edit']
        read_only_fields = ['id', 'username', 'created_at', 'can_edit']

    def get_can_edit(self, obj):
        request = self.context.get('request')
        return bool(request and request.user.is_authenticated and obj.user_id == request.user.id)


class SnapPreviewSerializer(serializers.ModelSerializer):
    like_count = serializers.SerializerMethodField()

    class Meta:
        model = SnapPost
        fields = ['id', 'photos', 'like_count', 'created_at']

    def get_like_count(self, obj):
        value = getattr(obj, 'like_count', None)
        return value if value is not None else obj.likes.count()


class SnapPostSerializer(serializers.ModelSerializer):
    owner = SnapUserSerializer(read_only=True)
    gender = serializers.CharField()
    clothing_item_ids = serializers.ListField(
        child=serializers.IntegerField(),
        write_only=True,
        required=False,
    )
    clothing_items = ClothingItemSerializer(many=True, read_only=True)
    gender_label = serializers.SerializerMethodField()
    like_count = serializers.SerializerMethodField()
    scrap_count = serializers.SerializerMethodField()
    comment_count = serializers.SerializerMethodField()
    is_liked = serializers.SerializerMethodField()
    is_scrapped = serializers.SerializerMethodField()
    can_edit = serializers.SerializerMethodField()
    latest_comment = serializers.SerializerMethodField()

    class Meta:
        model = SnapPost
        fields = [
            'id',
            'owner',
            'body',
            'photos',
            'gender',
            'gender_label',
            'seasons',
            'styles',
            'tpos',
            'height',
            'weight',
            'skin_tone',
            'clothing_item_ids',
            'clothing_items',
            'like_count',
            'scrap_count',
            'comment_count',
            'latest_comment',
            'is_liked',
            'is_scrapped',
            'can_edit',
            'created_at',
            'updated_at',
        ]
        read_only_fields = [
            'id',
            'owner',
            'clothing_items',
            'like_count',
            'scrap_count',
            'comment_count',
            'latest_comment',
            'is_liked',
            'is_scrapped',
            'can_edit',
            'created_at',
            'updated_at',
        ]

    def get_gender_label(self, obj):
        return {'male': '남성', 'female': '여성'}.get(obj.gender, obj.gender)

    def get_like_count(self, obj):
        value = getattr(obj, 'like_count', None)
        return value if value is not None else obj.likes.count()

    def get_scrap_count(self, obj):
        value = getattr(obj, 'scrap_count', None)
        return value if value is not None else obj.scraps.count()

    def get_comment_count(self, obj):
        value = getattr(obj, 'comment_count', None)
        return value if value is not None else obj.comments.count()

    def get_latest_comment(self, obj):
        prefetched_comments = getattr(obj, 'prefetched_comments', None)
        comment = prefetched_comments[0] if prefetched_comments else None
        if prefetched_comments is None:
            comment = obj.comments.select_related('user').order_by('-created_at').first()
        if comment is None:
            return None
        return SnapCommentSerializer(comment, context=self.context).data

    def get_is_liked(self, obj):
        request = self.context.get('request')
        if not request or not request.user.is_authenticated:
            return False
        liked_snap_ids = self.context.get('liked_snap_ids')
        if liked_snap_ids is not None:
            return obj.id in liked_snap_ids
        return obj.likes.filter(user=request.user).exists()

    def get_is_scrapped(self, obj):
        request = self.context.get('request')
        if not request or not request.user.is_authenticated:
            return False
        scrapped_snap_ids = self.context.get('scrapped_snap_ids')
        if scrapped_snap_ids is not None:
            return obj.id in scrapped_snap_ids
        return obj.scraps.filter(user=request.user).exists()

    def get_can_edit(self, obj):
        request = self.context.get('request')
        return bool(request and request.user.is_authenticated and obj.owner_id == request.user.id)

    def validate_gender(self, value):
        normalized = GENDER_INPUT.get(str(value).strip())
        if not normalized:
            raise serializers.ValidationError('Gender must be male or female.')
        return normalized

    def validate_photos(self, value):
        if not isinstance(value, list):
            raise serializers.ValidationError('Photos must be a list.')
        cleaned = [str(item).strip() for item in value if str(item).strip()]
        if not cleaned:
            raise serializers.ValidationError('At least one photo is required.')
        if len(cleaned) > 10:
            raise serializers.ValidationError('Up to 10 photos can be uploaded.')
        return cleaned

    def validate_clothing_item_ids(self, value):
        user = self.context['request'].user
        unique_ids = list(dict.fromkeys(value))
        existing_count = ClothingItem.objects.filter(owner=user, id__in=unique_ids).count()
        if existing_count != len(unique_ids):
            raise serializers.ValidationError('Only your own closet items can be tagged.')
        return unique_ids

    def validate(self, attrs):
        for key in ('seasons', 'styles', 'tpos'):
            values = attrs.get(key)
            if values is None:
                continue
            if not isinstance(values, list):
                raise serializers.ValidationError({key: 'This field must be a list.'})
            cleaned = [str(value).strip() for value in values if str(value).strip()]
            if len(cleaned) > 2:
                raise serializers.ValidationError({key: 'Choose up to 2 items.'})
            attrs[key] = list(dict.fromkeys(cleaned))
        return attrs

    def _set_clothing_items(self, snap, item_ids):
        SnapClothingItem.objects.filter(snap=snap).delete()
        SnapClothingItem.objects.bulk_create([
            SnapClothingItem(snap=snap, clothing_item_id=item_id, sort_order=index)
            for index, item_id in enumerate(item_ids)
        ])

    def create(self, validated_data):
        item_ids = validated_data.pop('clothing_item_ids', [])
        with transaction.atomic():
            snap = SnapPost.objects.create(
                owner=self.context['request'].user,
                **validated_data,
            )
            if item_ids:
                self._set_clothing_items(snap, item_ids)
        return snap

    def update(self, instance, validated_data):
        item_ids = validated_data.pop('clothing_item_ids', None)
        with transaction.atomic():
            for attr, value in validated_data.items():
                setattr(instance, attr, value)
            instance.save()
            if item_ids is not None:
                self._set_clothing_items(instance, item_ids)
        return instance
