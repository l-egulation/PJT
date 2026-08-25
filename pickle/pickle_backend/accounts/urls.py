from django.urls import path
from rest_framework_simplejwt.views import (
    TokenRefreshView,
)

from . import views

urlpatterns = [
    path('signup/', views.signup, name='signup'),
    path('kakao/', views.kakao_login, name='kakao-login'),
    path('check-username/', views.check_username, name='check-username'),
    path('login/', views.login, name='login'),
    path('refresh/', TokenRefreshView.as_view(), name='token-refresh'),
    path('profile/', views.profile, name='profile'),
    path('profile/edit/', views.profile_edit, name='profile-edit'),
    path('style-preferences/', views.style_preferences, name='style-preferences'),
    path('settings/', views.account_settings, name='account-settings'),
    path('account/', views.delete_account, name='delete-account'),
]
