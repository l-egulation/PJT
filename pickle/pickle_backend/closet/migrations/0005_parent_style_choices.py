from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ('closet', '0004_clothingitem_style'),
    ]

    operations = [
        migrations.AlterField(
            model_name='clothingitem',
            name='style',
            field=models.CharField(
                blank=True,
                choices=[
                    ('미니멀', '미니멀'),
                    ('꾸안꾸', '꾸안꾸'),
                    ('러블리', '러블리'),
                    ('스트릿', '스트릿'),
                    ('포멀', '포멀'),
                    ('빈티지', '빈티지'),
                    ('올드머니', '올드머니'),
                    ('시크', '시크'),
                    ('스포티', '스포티'),
                    ('캐주얼', '캐주얼'),
                ],
                max_length=30,
            ),
        ),
    ]
