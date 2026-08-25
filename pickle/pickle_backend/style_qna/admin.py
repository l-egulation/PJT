from django.contrib import admin

from .models import StyleRequest, StyleSuggestion


@admin.register(StyleRequest)
class StyleRequestAdmin(admin.ModelAdmin):
    list_display = ('id', 'requester', 'title', 'tpo', 'status', 'created_at')
    list_filter = ('status', 'tpo')
    search_fields = ('requester__username', 'title', 'body')


@admin.register(StyleSuggestion)
class StyleSuggestionAdmin(admin.ModelAdmin):
    list_display = ('id', 'request', 'stylist', 'title', 'is_accepted', 'created_at')
    list_filter = ('is_accepted',)
    search_fields = ('request__title', 'stylist__username', 'title', 'comment')
