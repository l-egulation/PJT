from django.conf import settings
from django.db import models


class StyleRequest(models.Model):
    STATUS_CHOICES = [
        ('open', 'Open'),
        ('closed', 'Closed'),
    ]

    requester = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='style_requests',
    )
    title = models.CharField(max_length=120)
    body = models.TextField()
    target_item = models.ForeignKey(
        'closet.ClothingItem',
        on_delete=models.SET_NULL,
        null=True,
        blank=True,
        related_name='style_requests',
    )
    tpo = models.CharField(max_length=40, blank=True)
    status = models.CharField(max_length=20, choices=STATUS_CHOICES, default='open')
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = 'style_requests'
        ordering = ['-created_at']

    def __str__(self):
        return self.title


class StyleSuggestion(models.Model):
    request = models.ForeignKey(
        StyleRequest,
        on_delete=models.CASCADE,
        related_name='suggestions',
    )
    stylist = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='style_suggestions',
    )
    title = models.CharField(max_length=120)
    comment = models.TextField()
    items = models.ManyToManyField(
        'closet.ClothingItem',
        related_name='style_suggestions',
        blank=True,
    )
    shopping_links = models.JSONField(default=list, blank=True)
    is_accepted = models.BooleanField(default=False)
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'style_suggestions'
        ordering = ['-created_at']

    def __str__(self):
        return self.title
