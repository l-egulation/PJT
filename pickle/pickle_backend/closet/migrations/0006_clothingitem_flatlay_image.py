from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ('closet', '0005_parent_style_choices'),
    ]

    operations = [
        migrations.AddField(
            model_name='clothingitem',
            name='flatlay_image',
            field=models.ImageField(blank=True, upload_to='clothes/flatlay/'),
        ),
    ]
