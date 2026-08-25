from django.conf import settings
from django.db import models

from .style_options import AIHUB_PARENT_STYLE_CHOICES


class ClothingItem(models.Model):
    CATEGORY_CHOICES = [
        ('top', 'Top'),
        ('bottom', 'Bottom'),
        ('outer', 'Outer'),
        ('dress', 'Dress'),
        ('shoes', 'Shoes'),
        ('bag', 'Bag'),
        ('accessory', 'Accessory'),
    ]

    SEASON_CHOICES = [
        ('spring', 'Spring'),
        ('summer', 'Summer'),
        ('fall', 'Fall'),
        ('winter', 'Winter'),
        ('all', 'All season'),
    ]

    STYLE_CHOICES = AIHUB_PARENT_STYLE_CHOICES

    owner = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='clothes',
    )
    name = models.CharField(max_length=100)
    category = models.CharField(max_length=20, choices=CATEGORY_CHOICES)
    color = models.CharField(max_length=30, blank=True)
    material = models.CharField(max_length=50, blank=True)
    style = models.CharField(max_length=30, choices=STYLE_CHOICES, blank=True)
    aihub_style = models.CharField(max_length=30, blank=True)
    style_confidence = models.FloatField(null=True, blank=True)
    season = models.CharField(
        max_length=60,
        default='all',
    )
    image = models.ImageField(upload_to='clothes/originals/', blank=True)
    processed_image = models.ImageField(
        upload_to='clothes/processed/',
        blank=True,
    )
    flatlay_image = models.ImageField(
        upload_to='clothes/flatlay/',
        blank=True,
    )
    shopping_url = models.URLField(blank=True)
    memo = models.TextField(blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = 'clothing_items'
        ordering = ['-created_at']

    def __str__(self):
        return f'{self.owner.username} - {self.name}'
