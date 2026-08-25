from pathlib import PurePosixPath

import cloudinary
import cloudinary.uploader
from django.core.files.storage import Storage
from django.utils.deconstruct import deconstructible


@deconstructible
class CloudinaryMediaStorage(Storage):
    """Store uploaded media in the Cloudinary account from CLOUDINARY_URL."""

    def __init__(self):
        cloudinary.config(secure=True)

    def _open(self, name, mode='rb'):
        raise NotImplementedError('Cloudinary media is accessed through its URL.')

    def _save(self, name, content):
        path = PurePosixPath(name)
        folder = '/'.join(part for part in ('pickle', *path.parts[:-1]) if part)
        result = cloudinary.uploader.upload(
            content,
            resource_type='image',
            folder=folder,
            use_filename=True,
            unique_filename=True,
            overwrite=False,
        )
        return result['public_id']

    def delete(self, name):
        if name:
            cloudinary.uploader.destroy(name, resource_type='image', invalidate=True)

    def exists(self, name):
        return False

    def url(self, name):
        return cloudinary.CloudinaryImage(name).build_url(secure=True)

