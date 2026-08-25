from django.contrib.auth import get_user_model
from django.contrib.auth.password_validation import validate_password
from django.db import transaction
from rest_framework import serializers

from styles.models import StyleTag
from styles.serializers import StyleTagSerializer
from .models import Streak, UserSettings

User = get_user_model()


class StreakSerializer(serializers.ModelSerializer):
    progress_dots = serializers.SerializerMethodField()

    def get_progress_dots(self, obj):
        return [i < obj.current_count for i in range(10)]

    class Meta:
        model = Streak
        fields = [
            'current_count',
            'longest_count',
            'last_recorded_date',
            'progress_dots',
        ]


class UserSettingsSerializer(serializers.ModelSerializer):
    class Meta:
        model = UserSettings
        fields = [
            'push_notifications_enabled',
            'weather_notifications_enabled',
            'community_notifications_enabled',
            'marketing_notifications_enabled',
            'updated_at',
        ]
        read_only_fields = ['updated_at']


class ProfileEditSerializer(serializers.ModelSerializer):
    password = serializers.CharField(
        write_only=True,
        min_length=8,
        required=False,
        allow_blank=True,
    )

    class Meta:
        model = User
        fields = [
            'id',
            'username',
            'email',
            'password',
            'gender',
            'preferred_colors',
        ]
        read_only_fields = ['id']

    def validate_username(self, value):
        user = self.instance
        qs = User.objects.filter(username=value)

        if user:
            qs = qs.exclude(pk=user.pk)

        if qs.exists():
            raise serializers.ValidationError('This username is already in use.')

        return value

    def validate_email(self, value):
        if not value:
            return value

        user = self.instance
        qs = User.objects.filter(email=value)

        if user:
            qs = qs.exclude(pk=user.pk)

        if qs.exists():
            raise serializers.ValidationError('This email is already in use.')

        return value

    def validate_password(self, value):
        if not value:
            return value
        validate_password(value, self.instance)
        return value

    def update(self, instance, validated_data):
        password = validated_data.pop('password', None)

        for attr, value in validated_data.items():
            setattr(instance, attr, value)

        if password:
            instance.set_password(password)

        instance.save()
        return instance


class StylePreferenceSerializer(serializers.Serializer):
    style_tag_ids = serializers.ListField(
        child=serializers.IntegerField(),
        required=False,
    )
    style_tags = StyleTagSerializer(many=True, read_only=True)

    def validate_style_tag_ids(self, value):
        unique_ids = set(value)
        tags_count = StyleTag.objects.filter(id__in=unique_ids).count()

        if tags_count != len(unique_ids):
            raise serializers.ValidationError(
                'One or more style tags do not exist.'
            )

        return value

    def to_representation(self, instance):
        return {
            'style_tags': StyleTagSerializer(instance.style_tags.all(), many=True).data,
        }

    def update(self, instance, validated_data):
        style_tag_ids = validated_data.get('style_tag_ids')

        if style_tag_ids is not None:
            instance.style_tags.set(style_tag_ids)

        return instance


class UserSerializer(serializers.ModelSerializer):
    password = serializers.CharField(
        write_only=True,
        min_length=8,
        required=False,
    )
    style_tag_ids = serializers.ListField(
        child=serializers.IntegerField(),
        write_only=True,
        required=False,
    )
    style_tags = StyleTagSerializer(many=True, read_only=True)
    streak = StreakSerializer(read_only=True)

    class Meta:
        model = User
        fields = [
            'id',
            'username',
            'email',
            'password',
            'gender',
            'preferred_colors',
            'style_tag_ids',
            'style_tags',
            'streak',
        ]
        read_only_fields = ['id', 'style_tags', 'streak']

    def validate_password(self, value):
        validate_password(value)
        return value

    def validate_email(self, value):
        if not value:
            return value

        user = self.instance
        qs = User.objects.filter(email=value)

        if user:
            qs = qs.exclude(pk=user.pk)

        if qs.exists():
            raise serializers.ValidationError('This email is already in use.')

        return value

    def validate_style_tag_ids(self, value):
        unique_ids = set(value)
        tags_count = StyleTag.objects.filter(id__in=unique_ids).count()

        if tags_count != len(unique_ids):
            raise serializers.ValidationError(
                'One or more style tags do not exist.'
            )

        return value

    def create(self, validated_data):
        password = validated_data.pop('password', None)
        style_tag_ids = validated_data.pop('style_tag_ids', [])

        if not password:
            raise serializers.ValidationError({
                'password': 'Password is required.'
            })

        with transaction.atomic():
            user = User(**validated_data)
            user.set_password(password)
            user.save()

            if style_tag_ids:
                user.style_tags.set(style_tag_ids)

            Streak.objects.create(user=user)
            UserSettings.objects.create(user=user)

        return user

    def update(self, instance, validated_data):
        password = validated_data.pop('password', None)
        style_tag_ids = validated_data.pop('style_tag_ids', None)

        with transaction.atomic():
            for attr, value in validated_data.items():
                setattr(instance, attr, value)

            if password:
                instance.set_password(password)

            instance.save()

            if style_tag_ids is not None:
                instance.style_tags.set(style_tag_ids)

        return instance
