from django.contrib import admin

from .models import Outfit, OutfitEvaluation, OutfitItem, RecommendationEvent


class OutfitItemInline(admin.TabularInline):
    model = OutfitItem
    extra = 1


@admin.register(Outfit)
class OutfitAdmin(admin.ModelAdmin):
    list_display = ('id', 'owner', 'title', 'tpo', 'is_public', 'worn_on', 'created_at')
    list_filter = ('tpo', 'is_public', 'worn_on')
    search_fields = ('owner__username', 'title', 'description')
    inlines = [OutfitItemInline]


@admin.register(OutfitEvaluation)
class OutfitEvaluationAdmin(admin.ModelAdmin):
    list_display = ('id', 'outfit', 'rating', 'fit_score', 'activity_score', 'satisfaction_score')


@admin.register(RecommendationEvent)
class RecommendationEventAdmin(admin.ModelAdmin):
    list_display = ('id', 'user', 'tpo', 'temperature', 'saved', 'rating', 'is_synthetic', 'created_at')
    list_filter = ('tpo', 'saved', 'is_synthetic')
