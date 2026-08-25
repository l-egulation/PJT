from django.conf import settings
from django.db import migrations, models
import django.db.models.deletion


class Migration(migrations.Migration):
    initial = True

    dependencies = [
        migrations.swappable_dependency(settings.AUTH_USER_MODEL),
        ('closet', '0001_initial'),
        ('styles', '0001_initial'),
    ]

    operations = [
        migrations.CreateModel(
            name='Outfit',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('title', models.CharField(max_length=100)),
                ('tpo', models.CharField(choices=[('daily', 'Daily'), ('date', 'Date'), ('work', 'Work'), ('travel', 'Travel'), ('formal', 'Formal')], default='daily', max_length=20)),
                ('description', models.TextField(blank=True)),
                ('weather_note', models.CharField(blank=True, max_length=100)),
                ('is_public', models.BooleanField(default=False)),
                ('worn_on', models.DateField(blank=True, null=True)),
                ('created_at', models.DateTimeField(auto_now_add=True)),
                ('updated_at', models.DateTimeField(auto_now=True)),
                ('owner', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name='outfits', to=settings.AUTH_USER_MODEL)),
                ('style_tags', models.ManyToManyField(blank=True, related_name='outfits', to='styles.styletag')),
            ],
            options={
                'db_table': 'outfits',
                'ordering': ['-created_at'],
            },
        ),
        migrations.CreateModel(
            name='OutfitEvaluation',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('rating', models.PositiveSmallIntegerField()),
                ('fit_score', models.PositiveSmallIntegerField(default=3)),
                ('activity_score', models.PositiveSmallIntegerField(default=3)),
                ('satisfaction_score', models.PositiveSmallIntegerField(default=3)),
                ('feedback', models.TextField(blank=True)),
                ('created_at', models.DateTimeField(auto_now_add=True)),
                ('updated_at', models.DateTimeField(auto_now=True)),
                ('outfit', models.OneToOneField(on_delete=django.db.models.deletion.CASCADE, related_name='evaluation', to='outfits.outfit')),
            ],
            options={
                'db_table': 'outfit_evaluations',
            },
        ),
        migrations.CreateModel(
            name='OutfitItem',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('sort_order', models.PositiveSmallIntegerField(default=0)),
                ('clothing_item', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, to='closet.clothingitem')),
                ('outfit', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, to='outfits.outfit')),
            ],
            options={
                'db_table': 'outfit_items',
                'ordering': ['sort_order', 'id'],
                'unique_together': {('outfit', 'clothing_item')},
            },
        ),
        migrations.AddField(
            model_name='outfit',
            name='items',
            field=models.ManyToManyField(blank=True, related_name='outfits', through='outfits.OutfitItem', to='closet.clothingitem'),
        ),
    ]
