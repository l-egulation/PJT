from django.contrib.auth import get_user_model
from django.core.management.base import BaseCommand, CommandError

from outfits.models import RecommendationEvent


class Command(BaseCommand):
    help = 'Create transparent synthetic recommendation events for the demo ML model.'

    def add_arguments(self, parser):
        parser.add_argument(
            '--reset',
            action='store_true',
            help='Replace existing synthetic events with the current feature schema.',
        )

    def handle(self, *args, **options):
        if options['reset']:
            RecommendationEvent.objects.filter(is_synthetic=True).delete()
        if RecommendationEvent.objects.filter(is_synthetic=True).exists():
            self.stdout.write('Synthetic ML data already exists.')
            return

        users = list(get_user_model().objects.order_by('id')[:2])
        if len(users) < 2:
            raise CommandError('Load demo_data before seeding ML data.')

        styles = ['minimal', 'casual', 'lovely', 'street', 'formal', 'chic']
        colors = [('ivory', 'beige', 'white'), ('blue', 'black', 'black'), ('pink', 'white', 'white'), ('black', 'blue', 'black')]
        tpos = ['daily', 'work', 'date', 'formal', 'travel']
        events = []
        for index in range(80):
            top_color, bottom_color, shoes_color = colors[index % len(colors)]
            temperature = [5, 13, 19, 27][index % 4]
            tpo = tpos[index % len(tpos)]
            preferred_style = styles[index % len(styles)]
            style_match_ratio = 1.0 if (
                (preferred_style in {'minimal', 'casual'} and top_color in {'ivory', 'blue'})
                or (preferred_style in {'street', 'formal', 'chic'} and top_color == 'black')
                or (preferred_style == 'lovely' and top_color == 'pink')
            ) else 0.2
            liked = style_match_ratio > 0.5 and (tpo != 'formal' or top_color == 'black')
            features = {
                'temperature': float(temperature),
                'weather_code': float(index % 4),
                'tpo': tpo,
                'preferred_style': preferred_style,
                'item_count': 3.0,
                'season_match_ratio': 1.0 if temperature > 16 else 0.67,
                'style_match_ratio': style_match_ratio,
                'style_profile_score_ratio': style_match_ratio,
                'preferred_color_ratio': 0.67 if liked else 0.0,
                'top_color': top_color,
                'top_material': 'cotton',
                'top_season': 'all',
                'top_style': preferred_style if style_match_ratio > 0.5 else 'casual',
                'bottom_color': bottom_color,
                'bottom_material': 'cotton',
                'bottom_season': 'all',
                'bottom_style': preferred_style if style_match_ratio > 0.5 else 'casual',
                'outer_color': 'none',
                'outer_material': 'none',
                'outer_season': 'none',
                'outer_style': 'none',
                'dress_color': 'none',
                'dress_material': 'none',
                'dress_season': 'none',
                'dress_style': 'none',
                'shoes_color': shoes_color,
                'shoes_material': 'leather',
                'shoes_season': 'all',
                'shoes_style': preferred_style if style_match_ratio > 0.5 else 'casual',
            }
            events.append(RecommendationEvent(
                user=users[index % 2],
                tpo=tpo,
                temperature=temperature,
                weather_code=index % 4,
                item_ids=[],
                features=features,
                saved=liked,
                rating=5 if liked else 2,
                is_synthetic=True,
            ))

        RecommendationEvent.objects.bulk_create(events)
        self.stdout.write(self.style.SUCCESS(f'Created {len(events)} synthetic ML events.'))
