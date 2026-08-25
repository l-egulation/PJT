from django.conf import settings
from django.db import models


class Outfit(models.Model):
    TPO_CHOICES = [
        ('daily', 'Daily'),
        ('date', 'Date'),
        ('work', 'Work'),
        ('travel', 'Travel'),
        ('formal', 'Formal'),
    ]

    owner = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='outfits',
    )
    title = models.CharField(max_length=100)
    tpo = models.CharField(max_length=20, choices=TPO_CHOICES, default='daily')
    description = models.TextField(blank=True)
    weather_note = models.CharField(max_length=100, blank=True)
    style_tags = models.ManyToManyField(
        'styles.StyleTag',
        related_name='outfits',
        blank=True,
    )
    items = models.ManyToManyField(
        'closet.ClothingItem',
        through='OutfitItem',
        related_name='outfits',
        blank=True,
    )
    is_public = models.BooleanField(default=False)
    worn_on = models.DateField(null=True, blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = 'outfits'
        ordering = ['-created_at']

    def __str__(self):
        return self.title


class OutfitItem(models.Model):
    outfit = models.ForeignKey(Outfit, on_delete=models.CASCADE)
    clothing_item = models.ForeignKey('closet.ClothingItem', on_delete=models.CASCADE)
    sort_order = models.PositiveSmallIntegerField(default=0)

    class Meta:
        db_table = 'outfit_items'
        ordering = ['sort_order', 'id']
        unique_together = ('outfit', 'clothing_item')


class OutfitEvaluation(models.Model):
    outfit = models.OneToOneField(
        Outfit,
        on_delete=models.CASCADE,
        related_name='evaluation',
    )
    rating = models.PositiveSmallIntegerField()
    fit_score = models.PositiveSmallIntegerField(default=3)
    activity_score = models.PositiveSmallIntegerField(default=3)
    satisfaction_score = models.PositiveSmallIntegerField(default=3)
    feedback = models.TextField(blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = 'outfit_evaluations'

    def __str__(self):
        return f'{self.outfit.title} - {self.rating}'


class RecommendationEvent(models.Model):
    user = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name='recommendation_events',
    )
    outfit = models.OneToOneField(
        Outfit,
        on_delete=models.SET_NULL,
        related_name='recommendation_event',
        null=True,
        blank=True,
    )
    tpo = models.CharField(max_length=20, choices=Outfit.TPO_CHOICES)
    temperature = models.FloatField()
    weather_code = models.IntegerField(default=0)
    item_ids = models.JSONField(default=list)
    features = models.JSONField(default=dict)
    saved = models.BooleanField(default=False)
    rating = models.PositiveSmallIntegerField(null=True, blank=True)
    is_synthetic = models.BooleanField(default=False)
    was_worn = models.BooleanField(null=True, blank=True)
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        db_table = 'recommendation_events'
        ordering = ['-created_at']

    @property
    def liked(self):
        return self.saved or (self.rating is not None and self.rating >= 4)

    def feedback_signal(self):
        """Weak positive signal for outfits worn but never explicitly rated."""
        if self.was_worn is None:
            return None
        if self.was_worn and self.rating is None:
            return 1.0
        return None
