from django.contrib.auth import authenticate, get_user_model
from django.conf import settings
from django.db import transaction
import requests
from rest_framework import status
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny, IsAuthenticated
from rest_framework.response import Response
from rest_framework_simplejwt.tokens import RefreshToken

from .models import Streak, UserSettings
from .serializers import (
    ProfileEditSerializer,
    StreakSerializer,
    StylePreferenceSerializer,
    UserSerializer,
    UserSettingsSerializer,
)

User = get_user_model()


def _unique_social_username(base):
    candidate = ''.join(ch for ch in base if ch.isalnum() or ch in '._-')[:24]
    if not candidate:
        candidate = 'kakao_user'

    username = candidate
    suffix = 1
    while User.objects.filter(username=username).exists():
        suffix += 1
        username = f'{candidate[:20]}{suffix}'

    return username


def _kakao_error_detail(response, fallback):
    try:
        payload = response.json()
    except ValueError:
        payload = {}

    message = (
        payload.get('error_description')
        or payload.get('msg')
        or payload.get('error')
    )
    if message:
        return f'{fallback}: {message}'
    return fallback


@api_view(['POST'])
@permission_classes([AllowAny])
def signup(request):
    """
    POST /api/v1/auth/signup/
    """
    serializer = UserSerializer(data=request.data)

    if not serializer.is_valid():
        return Response(
            serializer.errors,
            status=status.HTTP_400_BAD_REQUEST,
        )

    user = serializer.save()
    refresh = RefreshToken.for_user(user)

    return Response({
        'user': UserSerializer(user).data,
        'tokens': {
            'access': str(refresh.access_token),
            'refresh': str(refresh),
        },
    }, status=status.HTTP_201_CREATED)


@api_view(['POST'])
@permission_classes([AllowAny])
def login(request):
    """
    POST /api/v1/auth/login/
    """
    identifier = str(
        request.data.get('email') or request.data.get('username') or ''
    ).strip()
    password = str(request.data.get('password') or '')

    if not identifier or not password:
        return Response(
            {'detail': 'Email and password are required.'},
            status=status.HTTP_400_BAD_REQUEST,
        )

    username = identifier
    if '@' in identifier:
        user = User.objects.filter(email__iexact=identifier).first()
        if user:
            username = user.get_username()

    user = authenticate(request, username=username, password=password)
    if user is None:
        return Response(
            {'detail': 'No active account found with the given credentials'},
            status=status.HTTP_401_UNAUTHORIZED,
        )

    refresh = RefreshToken.for_user(user)
    return Response({
        'access': str(refresh.access_token),
        'refresh': str(refresh),
    })


@api_view(['GET'])
@permission_classes([AllowAny])
def check_username(request):
    """
    GET /api/v1/auth/check-username/?username=...
    """
    username = str(request.query_params.get('username', '')).strip()
    if not username:
        return Response(
            {'available': False, 'detail': 'Username is required.'},
            status=status.HTTP_400_BAD_REQUEST,
        )

    return Response({
        'username': username,
        'available': not User.objects.filter(username=username).exists(),
    })


@api_view(['POST'])
@permission_classes([AllowAny])
def kakao_login(request):
    """
    POST /api/v1/auth/kakao/
    """
    code = str(request.data.get('code', '')).strip()
    redirect_uri = str(request.data.get('redirect_uri', '')).strip()
    kakao_rest_api_key = getattr(settings, 'KAKAO_REST_API_KEY', '')
    kakao_client_secret = getattr(settings, 'KAKAO_CLIENT_SECRET', '')

    if not code or not redirect_uri:
        return Response(
            {'detail': 'Kakao authorization code and redirect_uri are required.'},
            status=status.HTTP_400_BAD_REQUEST,
        )

    if not kakao_rest_api_key:
        return Response(
            {'detail': 'Kakao login is not configured.'},
            status=status.HTTP_503_SERVICE_UNAVAILABLE,
        )

    token_payload = {
        'grant_type': 'authorization_code',
        'client_id': kakao_rest_api_key,
        'redirect_uri': redirect_uri,
        'code': code,
    }
    if kakao_client_secret:
        token_payload['client_secret'] = kakao_client_secret

    try:
        token_response = requests.post(
            'https://kauth.kakao.com/oauth/token',
            data=token_payload,
            timeout=10,
        )
    except requests.RequestException:
        return Response(
            {'detail': 'Could not connect to Kakao token server.'},
            status=status.HTTP_503_SERVICE_UNAVAILABLE,
        )

    if token_response.status_code >= 400:
        return Response(
            {'detail': _kakao_error_detail(token_response, 'Failed to get Kakao access token')},
            status=status.HTTP_400_BAD_REQUEST,
        )

    access_token = token_response.json().get('access_token')
    if not access_token:
        return Response(
            {'detail': 'Kakao access token was missing.'},
            status=status.HTTP_400_BAD_REQUEST,
        )

    try:
        profile_response = requests.get(
            'https://kapi.kakao.com/v2/user/me',
            headers={'Authorization': f'Bearer {access_token}'},
            timeout=10,
        )
    except requests.RequestException:
        return Response(
            {'detail': 'Could not connect to Kakao profile server.'},
            status=status.HTTP_503_SERVICE_UNAVAILABLE,
        )

    if profile_response.status_code >= 400:
        return Response(
            {'detail': _kakao_error_detail(profile_response, 'Failed to get Kakao profile')},
            status=status.HTTP_400_BAD_REQUEST,
        )

    kakao_profile = profile_response.json()
    kakao_account = kakao_profile.get('kakao_account') or {}
    profile = kakao_account.get('profile') or {}
    kakao_id = str(kakao_profile.get('id') or '').strip()
    email = kakao_account.get('email') or f'kakao_{kakao_id}@kakao.local'
    nickname = profile.get('nickname') or f'kakao_{kakao_id}'

    with transaction.atomic():
        user = User.objects.filter(email=email).first()
        if user is None:
            user = User(
                username=_unique_social_username(nickname),
                email=email,
            )
            user.set_unusable_password()
            user.save()
            Streak.objects.create(user=user)
            UserSettings.objects.create(user=user)

    refresh = RefreshToken.for_user(user)

    return Response({
        'user': UserSerializer(user).data,
        'tokens': {
            'access': str(refresh.access_token),
            'refresh': str(refresh),
        },
    })


@api_view(['GET', 'PATCH'])
@permission_classes([IsAuthenticated])
def profile(request):
    """
    GET /api/v1/auth/profile/
    PATCH /api/v1/auth/profile/
    """
    if request.method == 'GET':
        serializer = UserSerializer(request.user)
        return Response(serializer.data)

    serializer = UserSerializer(
        request.user,
        data=request.data,
        partial=True,
    )

    if not serializer.is_valid():
        return Response(
            serializer.errors,
            status=status.HTTP_400_BAD_REQUEST,
        )

    user = serializer.save()

    return Response(UserSerializer(user).data)


@api_view(['GET', 'PATCH'])
@permission_classes([IsAuthenticated])
def profile_edit(request):
    """
    GET /api/v1/auth/profile/edit/
    PATCH /api/v1/auth/profile/edit/
    """
    if request.method == 'GET':
        serializer = ProfileEditSerializer(request.user)
        return Response(serializer.data)

    serializer = ProfileEditSerializer(
        request.user,
        data=request.data,
        partial=True,
    )
    serializer.is_valid(raise_exception=True)
    user = serializer.save()

    return Response(ProfileEditSerializer(user).data)


@api_view(['GET', 'PATCH'])
@permission_classes([IsAuthenticated])
def style_preferences(request):
    """
    GET /api/v1/auth/style-preferences/
    PATCH /api/v1/auth/style-preferences/
    """
    if request.method == 'GET':
        serializer = StylePreferenceSerializer(request.user)
        return Response(serializer.data)

    serializer = StylePreferenceSerializer(
        request.user,
        data=request.data,
        partial=True,
    )
    serializer.is_valid(raise_exception=True)
    user = serializer.save()

    return Response(StylePreferenceSerializer(user).data)


@api_view(['GET', 'PATCH'])
@permission_classes([IsAuthenticated])
def account_settings(request):
    """
    GET /api/v1/auth/settings/
    PATCH /api/v1/auth/settings/
    """
    settings, _ = UserSettings.objects.get_or_create(user=request.user)

    if request.method == 'GET':
        serializer = UserSettingsSerializer(settings)
        return Response(serializer.data)

    serializer = UserSettingsSerializer(
        settings,
        data=request.data,
        partial=True,
    )
    serializer.is_valid(raise_exception=True)
    serializer.save()

    return Response(serializer.data)


@api_view(['DELETE'])
@permission_classes([IsAuthenticated])
def delete_account(request):
    """
    DELETE /api/v1/auth/account/
    """
    request.user.delete()
    return Response(status=status.HTTP_204_NO_CONTENT)


@api_view(['GET'])
@permission_classes([IsAuthenticated])
def home_dashboard(request):
    """
    GET /api/v1/home/
    """
    streak, _ = Streak.objects.get_or_create(user=request.user)

    total_items = request.user.clothes.count()
    total_outfits = request.user.outfits.count()
    evaluated_outfits = request.user.outfits.filter(
        evaluation__isnull=False,
    ).count()

    return Response({
        'user': {
            'id': request.user.id,
            'username': request.user.username,
            'email': request.user.email,
            'gender': request.user.gender,
            'preferred_colors': request.user.preferred_colors,
            'style_tags': [
                {
                    'id': tag.id,
                    'name': tag.name,
                }
                for tag in request.user.style_tags.all()
            ],
        },
        'streak': StreakSerializer(streak).data,
        'stats': {
            'total_items': total_items,
            'total_outfits': total_outfits,
            'evaluated_outfits': evaluated_outfits,
        },
    })
