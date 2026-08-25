from django.contrib.auth.models import AbstractUser
from django.db import models


class User(AbstractUser):
    GENDER_CHOICES = [
        ('male', 'Male'),
        ('female', 'Female'),
        ('unisex', 'Unisex'),
    ]

    gender = models.CharField(
        max_length=10,
        choices=GENDER_CHOICES,
        default='unisex',
    )
    preferred_colors = models.CharField(max_length=100, blank=True)
    style_tags = models.ManyToManyField(
        'styles.StyleTag',
        through='UserStyleTag',
        related_name='users',
        blank=True,
    )

    class Meta:
        db_table = 'users'

    def __str__(self):
        return self.username


class UserStyleTag(models.Model):
    user = models.ForeignKey(User, on_delete=models.CASCADE)
    tag = models.ForeignKey('styles.StyleTag', on_delete=models.CASCADE)

    class Meta:
        db_table = 'user_style_tags'
        unique_together = ('user', 'tag')

    def __str__(self):
        return f'{self.user.username} - {self.tag.name}'


class Streak(models.Model):
    user = models.OneToOneField(
        User,
        on_delete=models.CASCADE,
        related_name='streak',
    )
    current_count = models.IntegerField(default=0)
    longest_count = models.IntegerField(default=0)
    last_recorded_date = models.DateField(null=True, blank=True)

    class Meta:
        db_table = 'streaks'

    def __str__(self):
        return f'{self.user.username} - {self.current_count} days'


class UserSettings(models.Model):
    user = models.OneToOneField(
        User,
        on_delete=models.CASCADE,
        related_name='settings',
    )
    push_notifications_enabled = models.BooleanField(default=True)
    weather_notifications_enabled = models.BooleanField(default=True)
    community_notifications_enabled = models.BooleanField(default=True)
    marketing_notifications_enabled = models.BooleanField(default=False)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = 'user_settings'

    def __str__(self):
        return f'{self.user.username} settings'
