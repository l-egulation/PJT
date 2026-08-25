from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ('closet', '0002_remove_clothingitem_is_favorite'),
    ]

    operations = [
        migrations.AlterField(
            model_name='clothingitem',
            name='category',
            field=models.CharField(
                choices=[
                    ('top', 'Top'),
                    ('bottom', 'Bottom'),
                    ('outer', 'Outer'),
                    ('dress', 'Dress'),
                    ('shoes', 'Shoes'),
                    ('bag', 'Bag'),
                    ('accessory', 'Accessory'),
                ],
                max_length=20,
            ),
        ),
        migrations.AlterField(
            model_name='clothingitem',
            name='season',
            field=models.CharField(default='all', max_length=60),
        ),
    ]
