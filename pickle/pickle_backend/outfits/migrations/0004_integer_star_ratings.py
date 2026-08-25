from django.db import migrations, models


class Migration(migrations.Migration):
    dependencies = [
        ('outfits', '0003_half_star_ratings'),
    ]

    operations = [
        migrations.AlterField(
            model_name='outfitevaluation',
            name='rating',
            field=models.PositiveSmallIntegerField(),
        ),
        migrations.AlterField(
            model_name='outfitevaluation',
            name='satisfaction_score',
            field=models.PositiveSmallIntegerField(default=3),
        ),
        migrations.AlterField(
            model_name='recommendationevent',
            name='rating',
            field=models.PositiveSmallIntegerField(blank=True, null=True),
        ),
    ]
