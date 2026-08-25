from django.contrib import admin
from django.contrib.auth.admin import UserAdmin

from .models import Streak, User, UserSettings, UserStyleTag


class UserStyleTagInline(admin.TabularInline):
    model = UserStyleTag
    extra = 1


@admin.register(User)
class CustomUserAdmin(UserAdmin):
    fieldsets = UserAdmin.fieldsets + (
        ('Pickle Profile', {
            'fields': (
                'gender',
                'preferred_colors',
            )
        }),
    )
    inlines = [UserStyleTagInline]
    list_display = (
        'id',
        'username',
        'email',
        'gender',
        'is_staff',
        'is_active',
    )


@admin.register(UserStyleTag)
class UserStyleTagAdmin(admin.ModelAdmin):
    list_display = ('id', 'user', 'tag')
    search_fields = ('user__username', 'tag__name')


@admin.register(Streak)
class StreakAdmin(admin.ModelAdmin):
    list_display = (
        'id',
        'user',
        'current_count',
        'longest_count',
        'last_recorded_date',
    )


@admin.register(UserSettings)
class UserSettingsAdmin(admin.ModelAdmin):
    list_display = (
        'id',
        'user',
        'push_notifications_enabled',
        'weather_notifications_enabled',
        'community_notifications_enabled',
        'marketing_notifications_enabled',
        'updated_at',
    )
    search_fields = ('user__username',)
