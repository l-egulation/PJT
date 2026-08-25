from django.contrib import admin

from .models import (
    SnapClothingItem,
    SnapComment,
    SnapFollow,
    SnapHidden,
    SnapLike,
    SnapPost,
    SnapReport,
    SnapScrap,
)


admin.site.register(SnapPost)
admin.site.register(SnapClothingItem)
admin.site.register(SnapLike)
admin.site.register(SnapScrap)
admin.site.register(SnapComment)
admin.site.register(SnapHidden)
admin.site.register(SnapReport)
admin.site.register(SnapFollow)
