from django.db import migrations


class Migration(migrations.Migration):
    dependencies = [
        ('closet', '0001_initial'),
    ]

    operations = [
        migrations.RemoveField(
            model_name='clothingitem',
            name='is_favorite',
        ),
    ]
