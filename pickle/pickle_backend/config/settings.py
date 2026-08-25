import os
from pathlib import Path
from datetime import timedelta
from urllib.parse import parse_qs, unquote, urlparse
from dotenv import load_dotenv

load_dotenv()

BASE_DIR = Path(__file__).resolve().parent.parent

SECRET_KEY = os.getenv('SECRET_KEY')
DEBUG = os.getenv('DEBUG', 'True') == 'True'
KAKAO_REST_API_KEY = os.getenv('KAKAO_REST_API_KEY') or os.getenv('VITE_KAKAO_REST_API_KEY', '')
KAKAO_CLIENT_SECRET = os.getenv('KAKAO_CLIENT_SECRET', '')

GMS_API_KEY = os.getenv('GMS_API_KEY', '')
GMS_OPENAI_BASE_URL = os.getenv('GMS_OPENAI_BASE_URL', 'https://gms.ssafy.io/gmsapi/api.openai.com/v1')
GMS_VISION_MODEL = os.getenv('GMS_VISION_MODEL', 'gpt-4o-mini')
USE_LLM_RERANKER = os.getenv('USE_LLM_RERANKER', 'True') == 'True'
LLM_RERANK_TOP_K = int(os.getenv('LLM_RERANK_TOP_K', 8))
LLM_RERANK_TIMEOUT = int(os.getenv('LLM_RERANK_TIMEOUT', 8))

ALLOWED_HOSTS = ['*']

INSTALLED_APPS = [
    # django
    'django.contrib.admin',
    'django.contrib.auth',
    'django.contrib.contenttypes',
    'django.contrib.sessions',
    'django.contrib.messages',
    'django.contrib.staticfiles',

    # third party
    'rest_framework',
    'rest_framework_simplejwt',
    'corsheaders',

    # local apps
    'styles',
    'accounts',
    'closet',
    'outfits',
    'style_qna',
    'snaps',
]

MIDDLEWARE = [
    'corsheaders.middleware.CorsMiddleware',
    'django.middleware.security.SecurityMiddleware',
    'django.contrib.sessions.middleware.SessionMiddleware',
    'django.middleware.common.CommonMiddleware',
    'django.middleware.csrf.CsrfViewMiddleware',
    'django.contrib.auth.middleware.AuthenticationMiddleware',
    'django.contrib.messages.middleware.MessageMiddleware',
    'django.middleware.clickjacking.XFrameOptionsMiddleware',
]

ROOT_URLCONF = 'config.urls'

AUTH_USER_MODEL = 'accounts.User'

TEMPLATES = [
    {
        'BACKEND': 'django.template.backends.django.DjangoTemplates',
        'DIRS': [],
        'APP_DIRS': True,
        'OPTIONS': {
            'context_processors': [
                'django.template.context_processors.request',
                'django.contrib.auth.context_processors.auth',
                'django.contrib.messages.context_processors.messages',
            ],
        },
    },
]

WSGI_APPLICATION = 'config.wsgi.application'

DATABASE_URL = os.getenv('DATABASE_URL', '').strip()

if DATABASE_URL:
    database = urlparse(DATABASE_URL)
    query = parse_qs(database.query)
    DATABASES = {
        'default': {
            'ENGINE': 'django.db.backends.postgresql',
            'NAME': unquote(database.path.lstrip('/')),
            'USER': unquote(database.username or ''),
            'PASSWORD': unquote(database.password or ''),
            'HOST': database.hostname or '',
            'PORT': database.port or 5432,
            'CONN_MAX_AGE': 600,
            'OPTIONS': {
                'sslmode': query.get('sslmode', ['require'])[0],
            },
        }
    }
else:
    DATABASES = {
        'default': {
            'ENGINE': 'django.db.backends.sqlite3',
            'NAME': BASE_DIR / 'db.sqlite3',
        }
    }

REST_FRAMEWORK = {
    'DEFAULT_AUTHENTICATION_CLASSES': [
        'rest_framework_simplejwt.authentication.JWTAuthentication',
    ],
    'DEFAULT_PERMISSION_CLASSES': [
        'rest_framework.permissions.IsAuthenticated',
    ],
}

SIMPLE_JWT = {
    'ACCESS_TOKEN_LIFETIME': timedelta(days=1),
    'REFRESH_TOKEN_LIFETIME': timedelta(days=30),
    'ROTATE_REFRESH_TOKENS': True,
}

CORS_ALLOWED_ORIGINS = [
    'http://localhost:5173',
    'http://127.0.0.1:5173',
]

CORS_ALLOW_CREDENTIALS = True

LANGUAGE_CODE = 'ko-kr'
TIME_ZONE = 'Asia/Seoul'
USE_I18N = True
USE_TZ = True

STATIC_URL = 'static/'
MEDIA_URL = 'media/'
MEDIA_ROOT = BASE_DIR / 'media'

if os.getenv('CLOUDINARY_URL', '').strip():
    STORAGES = {
        'default': {
            'BACKEND': 'config.storage.CloudinaryMediaStorage',
        },
        'staticfiles': {
            'BACKEND': 'django.contrib.staticfiles.storage.StaticFilesStorage',
        },
    }

FIXTURE_DIRS = [BASE_DIR / 'fixtures']
DEFAULT_AUTO_FIELD = 'django.db.models.BigAutoField'
