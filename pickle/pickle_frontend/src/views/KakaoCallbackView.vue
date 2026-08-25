<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import PickleBuddy from '@/components/PickleBuddy.vue'
import { useUserStore } from '@/stores/user'

const route = useRoute()
const router = useRouter()
const user = useUserStore()
const error = ref('')

onMounted(async () => {
  const code = String(route.query.code || '')
  const kakaoError = String(route.query.error_description || route.query.error || '')
  const redirectUri = import.meta.env.VITE_KAKAO_REDIRECT_URI || `${window.location.origin}/oauth/kakao/callback`

  if (kakaoError) {
    error.value = `카카오 인증 실패: ${kakaoError}`
    return
  }

  if (!code) {
    error.value = '카카오 인증 코드를 찾지 못했습니다.'
    return
  }

  try {
    await user.kakaoLogin(code, redirectUri)
    router.replace('/onboarding')
  } catch (requestError) {
    error.value = requestError.response?.data?.detail || '카카오 로그인에 실패했습니다.'
  }
})
</script>

<template>
  <PhoneFrame>
    <section class="oauth-callback-page">
      <PickleBuddy size="small" variant="wink" />
      <template v-if="error">
        <h1>카카오 가입을 완료하지 못했어요</h1>
        <p>{{ error }}</p>
        <button class="secondary-button" type="button" @click="$router.replace('/signup')">다시 시도하기</button>
      </template>
      <template v-else>
        <h1>카카오 계정으로 시작하는 중</h1>
        <p>잠시만 기다려 주세요.</p>
      </template>
    </section>
  </PhoneFrame>
</template>
