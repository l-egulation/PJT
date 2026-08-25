import httpx
from django.core.management.base import BaseCommand

from closet.ml.image_classifier import predict_style
from closet.models import ClothingItem


class Command(BaseCommand):
    help = 'Classify existing closet item styles with the AIHub style model.'

    def add_arguments(self, parser):
        parser.add_argument(
            '--force',
            action='store_true',
            help='Reclassify items even when style is already set.',
        )

    def handle(self, *args, **options):
        queryset = ClothingItem.objects.exclude(image='')
        if not options['force']:
            queryset = queryset.filter(style='')

        total = queryset.count()
        classified = 0
        skipped = 0
        for item in queryset.iterator():
            try:
                try:
                    with item.image.open('rb') as image_file:
                        prediction = predict_style(image_file)
                except NotImplementedError:
                    response = httpx.get(item.image.url, follow_redirects=True, timeout=30)
                    response.raise_for_status()
                    prediction = predict_style(response.content)

                if not prediction:
                    skipped += 1
                    self.stderr.write(f'Skipped #{item.id} {item.name}: style model unavailable')
                    continue

                item.style = prediction['style']
                item.aihub_style = prediction.get('aihub_style', '')
                item.style_confidence = prediction['confidence']
                item.save(update_fields=['style', 'aihub_style', 'style_confidence'])
                classified += 1
                self.stdout.write(
                    f'Classified #{item.id} {item.name}: '
                    f'{item.style} ({item.aihub_style}, {item.style_confidence:.3f})'
                )
            except Exception as exc:
                skipped += 1
                self.stderr.write(f'Skipped #{item.id} {item.name}: {exc}')

        self.stdout.write(self.style.SUCCESS(
            f'Done. classified={classified}, skipped={skipped}, total={total}'
        ))
