from django.conf import settings
from django.db import migrations, models
import django.db.models.deletion


class Migration(migrations.Migration):
    initial = True

    dependencies = [
        migrations.swappable_dependency(settings.AUTH_USER_MODEL),
    ]

    operations = [
        migrations.CreateModel(
            name='ClothingItem',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('name', models.CharField(max_length=100)),
                ('category', models.CharField(choices=[('top', 'Top'), ('bottom', 'Bottom'), ('outer', 'Outer'), ('shoes', 'Shoes'), ('bag', 'Bag'), ('accessory', 'Accessory')], max_length=20)),
                ('color', models.CharField(blank=True, max_length=30)),
                ('material', models.CharField(blank=True, max_length=50)),
                ('season', models.CharField(choices=[('spring', 'Spring'), ('summer', 'Summer'), ('fall', 'Fall'), ('winter', 'Winter'), ('all', 'All season')], default='all', max_length=20)),
                ('image', models.ImageField(blank=True, upload_to='clothes/originals/')),
                ('processed_image', models.ImageField(blank=True, upload_to='clothes/processed/')),
                ('shopping_url', models.URLField(blank=True)),
                ('memo', models.TextField(blank=True)),
                ('is_favorite', models.BooleanField(default=False)),
                ('created_at', models.DateTimeField(auto_now_add=True)),
                ('updated_at', models.DateTimeField(auto_now=True)),
                ('owner', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name='clothes', to=settings.AUTH_USER_MODEL)),
            ],
            options={
                'db_table': 'clothing_items',
                'ordering': ['-created_at'],
            },
        ),
    ]
