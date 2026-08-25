from django.conf import settings
from django.db import migrations, models
import django.db.models.deletion


class Migration(migrations.Migration):
    dependencies = [
        migrations.swappable_dependency(settings.AUTH_USER_MODEL),
        ('outfits', '0001_initial'),
    ]

    operations = [
        migrations.CreateModel(
            name='RecommendationEvent',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('tpo', models.CharField(choices=[('daily', 'Daily'), ('date', 'Date'), ('work', 'Work'), ('travel', 'Travel'), ('formal', 'Formal')], max_length=20)),
                ('temperature', models.FloatField()),
                ('weather_code', models.IntegerField(default=0)),
                ('item_ids', models.JSONField(default=list)),
                ('features', models.JSONField(default=dict)),
                ('saved', models.BooleanField(default=False)),
                ('rating', models.PositiveSmallIntegerField(blank=True, null=True)),
                ('is_synthetic', models.BooleanField(default=False)),
                ('created_at', models.DateTimeField(auto_now_add=True)),
                ('outfit', models.OneToOneField(blank=True, null=True, on_delete=django.db.models.deletion.SET_NULL, related_name='recommendation_event', to='outfits.outfit')),
                ('user', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name='recommendation_events', to=settings.AUTH_USER_MODEL)),
            ],
            options={
                'db_table': 'recommendation_events',
                'ordering': ['-created_at'],
            },
        ),
    ]
