from django.contrib import admin

from .models import StyleTag


@admin.register(StyleTag)
class StyleTagAdmin(admin.ModelAdmin):
    list_display = ('id', 'name', 'slug')
    search_fields = ('name', 'slug')
    prepopulated_fields = {'slug': ('name',)}
