from rest_framework import serializers

from .models import StyleTag


class StyleTagSerializer(serializers.ModelSerializer):
    class Meta:
        model = StyleTag
        fields = ['id', 'name', 'slug']
