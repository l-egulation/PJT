from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ('closet', '0003_add_dress_and_multi_season'),
    ]

    operations = [
        migrations.AddField(
            model_name='clothingitem',
            name='aihub_style',
            field=models.CharField(blank=True, max_length=30),
        ),
        migrations.AddField(
            model_name='clothingitem',
            name='style',
            field=models.CharField(
                blank=True,
                choices=[
                    ('minimal', 'Minimal'),
                    ('casual', 'Casual'),
                    ('street', 'Street'),
                    ('lovely', 'Lovely'),
                    ('formal', 'Formal'),
                    ('chic', 'Chic'),
                ],
                max_length=20,
            ),
        ),
        migrations.AddField(
            model_name='clothingitem',
            name='style_confidence',
            field=models.FloatField(blank=True, null=True),
        ),
    ]
