from django.conf import settings
from django.db import models


class SnapPost(models.Model):
    GENDER_CHOICES = [
        ('male', 'Male'),
        ('female', 'Female'),
    ]

    owner = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='snap_posts',
    )
    body = models.TextField(blank=True)
    photos = models.JSONField(default=list)
    gender = models.CharField(max_length=10, choices=GENDER_CHOICES)
    seasons = models.JSONField(default=list, blank=True)
    styles = models.JSONField(default=list, blank=True)
    tpos = models.JSONField(default=list, blank=True)
    height = models.PositiveSmallIntegerField(null=True, blank=True)
    weight = models.PositiveSmallIntegerField(null=True, blank=True)
    skin_tone = models.CharField(max_length=30, blank=True)
    clothing_items = models.ManyToManyField(
        'closet.ClothingItem',
        through='SnapClothingItem',
        related_name='snap_posts',
        blank=True,
    )
    is_hidden = models.BooleanField(default=False)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = 'snap_posts'
        ordering = ['-created_at']

    def __str__(self):
        return f'{self.owner.username} snap #{self.pk}'


class SnapClothingItem(models.Model):
    snap = models.ForeignKey(SnapPost, on_delete=models.CASCADE)
    clothing_item = models.ForeignKey('closet.ClothingItem', on_delete=models.CASCADE)
    sort_order = models.PositiveSmallIntegerField(default=0)

    class Meta:
        db_table = 'snap_clothing_items'
        ordering = ['sort_order', 'id']
        unique_together = ('snap', 'clothing_item')


class SnapLike(models.Model):
    snap = models.ForeignKey(SnapPost, on_delete=models.CASCADE, related_name='likes')
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE, related_name='snap_likes')
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'snap_likes'
        unique_together = ('snap', 'user')


class SnapScrap(models.Model):
    snap = models.ForeignKey(SnapPost, on_delete=models.CASCADE, related_name='scraps')
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE, related_name='snap_scraps')
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'snap_scraps'
        unique_together = ('snap', 'user')


class SnapComment(models.Model):
    snap = models.ForeignKey(SnapPost, on_delete=models.CASCADE, related_name='comments')
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE, related_name='snap_comments')
    body = models.TextField()
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'snap_comments'
        ordering = ['created_at']


class SnapHidden(models.Model):
    snap = models.ForeignKey(SnapPost, on_delete=models.CASCADE, related_name='hidden_by')
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE, related_name='hidden_snaps')
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'snap_hidden'
        unique_together = ('snap', 'user')


class SnapReport(models.Model):
    snap = models.ForeignKey(SnapPost, on_delete=models.CASCADE, related_name='reports')
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE, related_name='snap_reports')
    reason = models.CharField(max_length=100, blank=True)
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'snap_reports'
        unique_together = ('snap', 'user')


class SnapFollow(models.Model):
    follower = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='following_snap_users',
    )
    following = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='snap_followers',
    )
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'snap_follows'
        unique_together = ('follower', 'following')

    def clean(self):
        if self.follower_id == self.following_id:
            from django.core.exceptions import ValidationError

            raise ValidationError('Users cannot follow themselves.')
