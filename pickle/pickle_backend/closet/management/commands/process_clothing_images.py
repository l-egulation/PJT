from django.core.management.base import BaseCommand
from django.db.models import Q
import httpx

from closet.ml.image_processing import process_clothing_image, process_flatlay_image
from closet.models import ClothingItem


class Command(BaseCommand):
    help = 'Create processed clothing images for existing closet items.'

    def add_arguments(self, parser):
        parser.add_argument(
            '--force',
            action='store_true',
            help='Regenerate processed images even when one already exists.',
        )

    def handle(self, *args, **options):
        queryset = ClothingItem.objects.exclude(image='')
        if not options['force']:
            queryset = queryset.filter(Q(processed_image='') | Q(flatlay_image=''))

        total = queryset.count()
        processed = 0
        skipped = 0
        for item in queryset.iterator():
            try:
                try:
                    with item.image.open('rb') as image_file:
                        image_bytes = image_file.read()
                except NotImplementedError:
                    response = httpx.get(item.image.url, follow_redirects=True, timeout=30)
                    response.raise_for_status()
                    image_bytes = response.content

                from io import BytesIO
                processed_file = process_clothing_image(BytesIO(image_bytes), item.image.name)
                flatlay_file = process_flatlay_image(BytesIO(image_bytes), item.image.name)

                item.processed_image.save(processed_file.name, processed_file, save=False)
                item.flatlay_image.save(flatlay_file.name, flatlay_file, save=False)
                item.save(update_fields=['processed_image', 'flatlay_image'])

                processed += 1
                self.stdout.write(f'Processed #{item.id} {item.name}')
            except Exception as exc:
                skipped += 1
                self.stderr.write(f'Skipped #{item.id} {item.name}: {exc}')

        self.stdout.write(self.style.SUCCESS(
            f'Done. processed={processed}, skipped={skipped}, total={total}'
        ))
