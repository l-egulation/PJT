from django.conf import settings
from django.db import migrations, models
import django.db.models.deletion


class Migration(migrations.Migration):
    initial = True

    dependencies = [
        migrations.swappable_dependency(settings.AUTH_USER_MODEL),
        ('closet', '0001_initial'),
    ]

    operations = [
        migrations.CreateModel(
            name='StyleRequest',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('title', models.CharField(max_length=120)),
                ('body', models.TextField()),
                ('tpo', models.CharField(blank=True, max_length=40)),
                ('status', models.CharField(choices=[('open', 'Open'), ('closed', 'Closed')], default='open', max_length=20)),
                ('created_at', models.DateTimeField(auto_now_add=True)),
                ('updated_at', models.DateTimeField(auto_now=True)),
                ('requester', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name='style_requests', to=settings.AUTH_USER_MODEL)),
                ('target_item', models.ForeignKey(blank=True, null=True, on_delete=django.db.models.deletion.SET_NULL, related_name='style_requests', to='closet.clothingitem')),
            ],
            options={
                'db_table': 'style_requests',
                'ordering': ['-created_at'],
            },
        ),
        migrations.CreateModel(
            name='StyleSuggestion',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('title', models.CharField(max_length=120)),
                ('comment', models.TextField()),
                ('shopping_links', models.JSONField(blank=True, default=list)),
                ('is_accepted', models.BooleanField(default=False)),
                ('created_at', models.DateTimeField(auto_now_add=True)),
                ('items', models.ManyToManyField(blank=True, related_name='style_suggestions', to='closet.clothingitem')),
                ('request', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name='suggestions', to='style_qna.stylerequest')),
                ('stylist', models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name='style_suggestions', to=settings.AUTH_USER_MODEL)),
            ],
            options={
                'db_table': 'style_suggestions',
                'ordering': ['-created_at'],
            },
        ),
    ]
