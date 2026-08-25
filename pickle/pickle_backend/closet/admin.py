from django.contrib import admin

from .models import ClothingItem


@admin.register(ClothingItem)
class ClothingItemAdmin(admin.ModelAdmin):
    list_display = (
        'id',
        'owner',
        'name',
        'category',
        'color',
        'season',
        'created_at',
    )
    list_filter = ('category', 'season')
    search_fields = ('owner__username', 'name', 'color', 'material')
