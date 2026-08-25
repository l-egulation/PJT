<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import PickleBuddy from '@/components/PickleBuddy.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const email = ref('')
const password = ref('')
const autoLogin = ref(true)
const showPassword = ref(false)
const loading = ref(false)
const error = ref('')

const errorTranslations = {
  'No active account found with the given credentials': '이메일과 비밀번호를 다시 확인해 주세요.',
}

function startKakao() {
  const kakaoRestApiKey = import.meta.env.VITE_KAKAO_REST_API_KEY
  const redirectUri = import.meta.env.VITE_KAKAO_REDIRECT_URI || `${window.location.origin}/oauth/kakao/callback`

  if (!kakaoRestApiKey) {
    window.alert('카카오 로그인 설정이 아직 없습니다. 이메일 로그인을 이용해 주세요.')
    return
  }

  const params = new URLSearchParams({
    client_id: kakaoRestApiKey,
    redirect_uri: redirectUri,
    response_type: 'code',
  })
  window.location.href = `https://kauth.kakao.com/oauth/authorize?${params.toString()}`
}

function socialUnavailable(provider) {
  window.alert(`${provider} 로그인은 지원하지 않습니다. 카카오 또는 이메일 로그인을 이용해 주세요.`)
}

function passwordHelp() {
  window.alert('비밀번호 찾기는 준비 중입니다.')
}

async function submit() {
  if (loading.value) return
  loading.value = true
  error.value = ''

  try {
    await user.login(email.value.trim(), password.value)
    const userId = user.profile?.id
    const onboarded = userId && localStorage.getItem(`pickle_onboarded_${userId}`)
    router.push(onboarded ? '/home' : '/onboarding')
  } catch (requestError) {
    const detail = requestError.response?.data?.detail
    error.value = errorTranslations[detail] || detail || '로그인에 실패했습니다.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <PhoneFrame show-back>
    <form class="login-page" @submit.prevent="submit">
      <section class="login-brand">
        <div class="login-buddy-glow">
          <PickleBuddy size="small" variant="wave" />
        </div>
        <img class="login-logo" src="/images/pickle-logo.png?v=transparent-1" alt="pickle pick your style" />
        <p>다시 만나서 반가워요!<br />오늘의 스타일을 골라볼까요?</p>
      </section>

      <label class="login-field">
        이메일
        <input v-model.trim="email" type="text" autocomplete="off" required />
      </label>

      <label class="login-field">
        비밀번호
        <span class="login-password-wrap">
          <input v-model="password" :type="showPassword ? 'text' : 'password'" autocomplete="off" required />
          <button type="button" aria-label="비밀번호 보기" @click="showPassword = !showPassword">
            <span class="password-eye" aria-hidden="true"></span>
          </button>
        </span>
      </label>

      <div class="login-options">
        <button class="login-check" type="button" @click="autoLogin = !autoLogin">
          <span :class="{ active: autoLogin }">✓</span>
          자동 로그인
        </button>
        <button type="button" @click="passwordHelp">비밀번호 찾기</button>
      </div>

      <p v-if="error" class="error-message">{{ error }}</p>

      <button class="primary-button login-submit" type="submit" :disabled="loading">
        {{ loading ? '로그인 중...' : '로그인' }}
      </button>

      <div class="login-social-divider"><span>SNS 계정으로 로그인</span></div>

      <div class="login-social-row">
        <button class="login-social login-social--kakao" type="button" aria-label="카카오 로그인" @click="startKakao">
            <span class="social-mark social-mark--kakao">
              <img src="/icons/social/kakao.svg" alt="" />
            </span>
        </button>
        <button class="login-social login-social--naver" type="button" aria-label="네이버 로그인" @click="socialUnavailable('네이버')">N</button>
        <button class="login-social login-social--apple" type="button" aria-label="Apple 로그인" @click="socialUnavailable('Apple')">
          <span class="social-mark social-mark--apple">
            <img src="/icons/social/apple.svg" alt="" />
          </span>
        </button>
      </div>

      <p class="login-signup-link">
        아직 계정이 없으신가요?
        <button type="button" @click="$router.push('/signup')">회원가입</button>
      </p>
    </form>
  </PhoneFrame>
</template>
