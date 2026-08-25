from django.db import models


class StyleTag(models.Model):
    name = models.CharField(max_length=50, unique=True)
    slug = models.SlugField(max_length=60, unique=True)

    class Meta:
        db_table = 'style_tags'
        ordering = ['name']

    def __str__(self):
        return self.name
