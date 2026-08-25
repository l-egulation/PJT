from django.db import migrations, models


class Migration(migrations.Migration):
    dependencies = [
        ('outfits', '0002_recommendationevent'),
    ]

    operations = [
        migrations.AlterField(
            model_name='outfitevaluation',
            name='rating',
            field=models.FloatField(),
        ),
        migrations.AlterField(
            model_name='outfitevaluation',
            name='satisfaction_score',
            field=models.FloatField(default=3),
        ),
        migrations.AlterField(
            model_name='recommendationevent',
            name='rating',
            field=models.FloatField(blank=True, null=True),
        ),
    ]
