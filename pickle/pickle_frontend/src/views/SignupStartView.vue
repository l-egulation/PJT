<script setup>
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import PickleBuddy from '@/components/PickleBuddy.vue'

const router = useRouter()

function startKakao() {
  const kakaoRestApiKey = import.meta.env.VITE_KAKAO_REST_API_KEY
  const redirectUri = import.meta.env.VITE_KAKAO_REDIRECT_URI || `${window.location.origin}/oauth/kakao/callback`

  if (!kakaoRestApiKey) {
    window.alert('카카오 가입 설정이 아직 없습니다. 이메일 가입을 이용해 주세요.')
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
  window.alert(`${provider} 가입은 지원하지 않습니다. 카카오 또는 이메일 가입을 이용해 주세요.`)
}
</script>

<template>
  <PhoneFrame show-back>
    <section class="signup-start-page">
      <div class="signup-buddy-hero">
        <PickleBuddy size="large" variant="wink" />
      </div>

      <div class="signup-start-copy">
        <h1>3초 만에 가입하고<br />스타일링 시작해요</h1>
        <p>소셜 계정으로 간편하게 시작하거나<br />이메일로 가입할 수 있어요.</p>
      </div>

      <div class="signup-social-actions">
        <button class="social-button social-button--kakao" type="button" @click="startKakao">
          <span class="social-mark social-mark--kakao">
            <img src="/icons/social/kakao.svg" alt="" />
          </span>
          카카오로 시작하기
        </button>
        <button class="social-button social-button--naver" type="button" @click="socialUnavailable('네이버')">
          <span class="social-mark social-mark--naver">N</span>
          네이버로 시작하기
        </button>
        <button class="social-button social-button--apple" type="button" @click="socialUnavailable('Apple')">
          <span class="social-mark social-mark--apple">
            <img src="/icons/social/apple.svg" alt="" />
          </span>
          Apple로 시작하기
        </button>
      </div>

      <div class="signup-divider"><span>또는</span></div>

      <button class="signup-email-button" type="button" @click="router.push('/signup/email')">
        이메일로 가입하기
      </button>

      <p class="signup-terms-copy">
        가입 시 <strong>이용약관</strong> 및 <strong>개인정보처리방침</strong>에<br />
        동의하는 것으로 간주됩니다.
      </p>
    </section>
  </PhoneFrame>
</template>
