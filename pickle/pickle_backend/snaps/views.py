from pathlib import Path
from uuid import uuid4

from cloudinary.exceptions import Error as CloudinaryError
from django.contrib.auth import get_user_model
from django.core.files.storage import default_storage
from django.db.models import Count, Prefetch, Q
from rest_framework import mixins, permissions, status, viewsets
from rest_framework.decorators import action
from rest_framework.parsers import FormParser, MultiPartParser
from rest_framework.response import Response

from .models import SnapComment, SnapFollow, SnapHidden, SnapLike, SnapPost, SnapReport, SnapScrap
from .serializers import (
    GENDER_INPUT,
    SnapCommentSerializer,
    SnapPostSerializer,
    SnapPreviewSerializer,
    SnapUserSerializer,
)


User = get_user_model()


class IsOwnerOrReadOnly(permissions.BasePermission):
    interactive_actions = {'like', 'scrap', 'comments', 'comment_detail', 'hide', 'report'}

    def has_object_permission(self, request, view, obj):
        if request.method in permissions.SAFE_METHODS:
            return True
        if getattr(view, 'action', None) in self.interactive_actions:
            return request.user.is_authenticated
        return obj.owner_id == request.user.id


class SnapPostViewSet(viewsets.ModelViewSet):
    serializer_class = SnapPostSerializer
    permission_classes = [permissions.IsAuthenticated, IsOwnerOrReadOnly]

    def get_queryset(self):
        queryset = (
            SnapPost.objects
            .filter(is_hidden=False)
            .exclude(hidden_by__user=self.request.user)
            .select_related('owner')
            .prefetch_related(
                'clothing_items',
                'likes',
                'scraps',
                Prefetch(
                    'comments',
                    queryset=SnapComment.objects.select_related('user').order_by('-created_at'),
                    to_attr='prefetched_comments',
                ),
            )
            .annotate(
                like_count=Count('likes', distinct=True),
                scrap_count=Count('scraps', distinct=True),
                comment_count=Count('comments', distinct=True),
            )
        )
        params = self.request.query_params

        owner = params.get('owner')
        if owner:
            queryset = queryset.filter(owner__username=owner)

        gender = params.get('gender')
        if gender:
            normalized_gender = GENDER_INPUT.get(gender.strip(), gender.strip())
            if normalized_gender in {'male', 'female'}:
                queryset = queryset.filter(gender=normalized_gender)

        if params.get('scrapped') == 'true':
            queryset = queryset.filter(scraps__user=self.request.user)

        if params.get('following') == 'true':
            following_ids = SnapFollow.objects.filter(
                follower=self.request.user,
            ).values_list('following_id', flat=True)
            queryset = queryset.filter(owner_id__in=following_ids)

        queryset = self._filter_json_list(queryset, 'season', 'seasons')
        queryset = self._filter_json_list(queryset, 'style', 'styles')
        queryset = self._filter_json_list(queryset, 'tpo', 'tpos')

        sort = params.get('sort', 'popular')
        if sort == 'latest':
            return queryset.order_by('-created_at')
        if sort == 'recommend':
            return queryset.order_by('-scrap_count', '-like_count', '-created_at')
        return queryset.order_by('-like_count', '-created_at')

    def get_serializer_context(self):
        context = super().get_serializer_context()
        user = self.request.user
        if user.is_authenticated:
            context.update({
                'liked_snap_ids': set(
                    SnapLike.objects.filter(user=user).values_list('snap_id', flat=True),
                ),
                'scrapped_snap_ids': set(
                    SnapScrap.objects.filter(user=user).values_list('snap_id', flat=True),
                ),
                'following_user_ids': set(
                    SnapFollow.objects.filter(follower=user).values_list('following_id', flat=True),
                ),
            })
        return context

    def _filter_json_list(self, queryset, param_name, field_name):
        raw_value = self.request.query_params.get(param_name)
        if not raw_value:
            return queryset
        values = {value.strip() for value in raw_value.split(',') if value.strip()}
        if not values:
            return queryset
        ids = [
            snap.id
            for snap in queryset
            if values & {str(item) for item in getattr(snap, field_name, [])}
        ]
        return queryset.filter(id__in=ids)

    @action(
        detail=False,
        methods=['post'],
        url_path='upload-photo',
        parser_classes=[MultiPartParser, FormParser],
    )
    def upload_photo(self, request):
        image = request.FILES.get('image')
        if image is None:
            return Response(
                {'image': 'An image file is required.'},
                status=status.HTTP_400_BAD_REQUEST,
            )

        suffix = Path(image.name).suffix.lower() or '.jpg'
        name = f'snaps/photos/{request.user.id}/{uuid4().hex}{suffix}'
        try:
            saved_name = default_storage.save(name, image)
        except CloudinaryError:
            return Response(
                {
                    'detail': (
                        '이미지 저장소 연결에 실패했습니다. '
                        '서버의 CLOUDINARY_URL 설정을 확인해 주세요.'
                    )
                },
                status=status.HTTP_503_SERVICE_UNAVAILABLE,
            )
        return Response(
            {'url': default_storage.url(saved_name), 'name': saved_name},
            status=status.HTTP_201_CREATED,
        )

    @action(detail=True, methods=['post'])
    def like(self, request, pk=None):
        snap = self.get_object()
        like, created = SnapLike.objects.get_or_create(snap=snap, user=request.user)
        if not created:
            like.delete()
        return Response({
            'is_liked': created,
            'like_count': SnapLike.objects.filter(snap=snap).count(),
        })

    @action(detail=True, methods=['post'])
    def scrap(self, request, pk=None):
        snap = self.get_object()
        scrap, created = SnapScrap.objects.get_or_create(snap=snap, user=request.user)
        if not created:
            scrap.delete()
        return Response({
            'is_scrapped': created,
            'scrap_count': SnapScrap.objects.filter(snap=snap).count(),
        })

    @action(detail=True, methods=['post'])
    def hide(self, request, pk=None):
        snap = self.get_object()
        SnapHidden.objects.get_or_create(snap=snap, user=request.user)
        return Response({'is_hidden': True})

    @action(detail=True, methods=['post'])
    def report(self, request, pk=None):
        snap = self.get_object()
        report, created = SnapReport.objects.get_or_create(
            snap=snap,
            user=request.user,
            defaults={'reason': request.data.get('reason', '')},
        )
        if not created and request.data.get('reason') is not None:
            report.reason = request.data.get('reason', '')
            report.save(update_fields=['reason'])
        return Response({'is_reported': True})

    @action(detail=True, methods=['get', 'post'])
    def comments(self, request, pk=None):
        snap = self.get_object()
        if request.method == 'GET':
            serializer = SnapCommentSerializer(
                snap.comments.select_related('user'),
                many=True,
                context={'request': request},
            )
            return Response(serializer.data)

        serializer = SnapCommentSerializer(data=request.data, context={'request': request})
        serializer.is_valid(raise_exception=True)
        serializer.save(snap=snap, user=request.user)
        return Response(serializer.data, status=status.HTTP_201_CREATED)

    @action(
        detail=True,
        methods=['patch', 'delete'],
        url_path='comments/(?P<comment_id>[^/.]+)',
    )
    def comment_detail(self, request, pk=None, comment_id=None):
        snap = self.get_object()
        comment = SnapComment.objects.filter(
            snap=snap,
            id=comment_id,
            user=request.user,
        ).first()
        if comment is None:
            return Response({'detail': 'Comment not found.'}, status=status.HTTP_404_NOT_FOUND)

        if request.method == 'DELETE':
            comment.delete()
            return Response(status=status.HTTP_204_NO_CONTENT)

        serializer = SnapCommentSerializer(
            comment,
            data=request.data,
            partial=True,
            context={'request': request},
        )
        serializer.is_valid(raise_exception=True)
        serializer.save()
        return Response(serializer.data)

    @action(detail=False, methods=['get'])
    def ranking(self, request):
        queryset = self.filter_queryset(self.get_queryset()).order_by('-like_count', '-created_at')
        page = self.paginate_queryset(queryset)
        if page is not None:
            serializer = self.get_serializer(page, many=True)
            return self.get_paginated_response(serializer.data)
        serializer = self.get_serializer(queryset, many=True)
        return Response(serializer.data)

    @action(detail=False, methods=['get'], url_path='profiles/(?P<username>[^/.]+)')
    def profile(self, request, username=None):
        user = self._profile_queryset().filter(username=username).first()
        if user is None:
            return Response({'detail': 'User not found.'}, status=status.HTTP_404_NOT_FOUND)
        serializer = SnapUserSerializer(user, context={'request': request})
        return Response(serializer.data)

    @action(detail=False, methods=['post'], url_path='profiles/(?P<username>[^/.]+)/follow')
    def follow(self, request, username=None):
        user = User.objects.filter(username=username).first()
        if user is None:
            return Response({'detail': 'User not found.'}, status=status.HTTP_404_NOT_FOUND)
        if user.id == request.user.id:
            return Response({'detail': 'You cannot follow yourself.'}, status=status.HTTP_400_BAD_REQUEST)
        follow, created = SnapFollow.objects.get_or_create(follower=request.user, following=user)
        if not created:
            follow.delete()
        return Response({
            'is_following': created,
            'follower_count': user.snap_followers.count(),
        })

    @action(detail=False, methods=['get'], url_path='profiles/(?P<username>[^/.]+)/followers')
    def followers(self, request, username=None):
        user = User.objects.filter(username=username).first()
        if user is None:
            return Response({'detail': 'User not found.'}, status=status.HTTP_404_NOT_FOUND)
        follower_ids = SnapFollow.objects.filter(following=user).values_list('follower_id', flat=True)
        queryset = self._profile_queryset().filter(id__in=follower_ids).order_by('username')
        serializer = SnapUserSerializer(queryset, many=True, context={'request': request})
        return Response(serializer.data)

    @action(detail=False, methods=['get'], url_path='profiles/(?P<username>[^/.]+)/following')
    def following(self, request, username=None):
        user = User.objects.filter(username=username).first()
        if user is None:
            return Response({'detail': 'User not found.'}, status=status.HTTP_404_NOT_FOUND)
        following_ids = SnapFollow.objects.filter(follower=user).values_list('following_id', flat=True)
        queryset = self._profile_queryset().filter(id__in=following_ids).order_by('username')
        serializer = SnapUserSerializer(queryset, many=True, context={'request': request})
        return Response(serializer.data)

    @action(detail=False, methods=['get'], url_path='members/ranking')
    def member_ranking(self, request):
        queryset = self._profile_queryset().filter(post_count__gt=0).order_by('-follower_count', '-post_count', 'username')
        data = []
        for user in queryset:
            user_data = SnapUserSerializer(user, context={'request': request}).data
            snaps = (
                SnapPost.objects
                .filter(owner=user, is_hidden=False)
                .exclude(hidden_by__user=request.user)
                .annotate(like_count=Count('likes', distinct=True))
                .order_by('-created_at')[:6]
            )
            user_data['snaps'] = SnapPreviewSerializer(snaps, many=True).data
            data.append(user_data)
        return Response(data)

    def _profile_queryset(self):
        return User.objects.annotate(
            post_count=Count('snap_posts', filter=Q(snap_posts__is_hidden=False), distinct=True),
            follower_count=Count('snap_followers', distinct=True),
            following_count=Count('following_snap_users', distinct=True),
        )
