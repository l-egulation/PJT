from rest_framework.permissions import SAFE_METHODS, BasePermission


class IsRequesterOrReadOnly(BasePermission):
    def has_object_permission(self, request, view, obj):
        if request.method in SAFE_METHODS or view.action == 'suggest':
            return True
        return obj.requester == request.user
