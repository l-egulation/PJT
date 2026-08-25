<script setup>
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'

const router = useRouter()

function openAuth(nextMode = 'signup') {
  if (nextMode === 'signup') {
    router.push('/signup')
    return
  }
  router.push('/login')
}
</script>

<template>
  <PhoneFrame>
    <div class="start-view sprout-start welcome-start">
      <section class="welcome-hero">
        <img class="welcome-logo-image" src="/images/pickle-logo.png?v=transparent-1" alt="pickle pick your style" />

        <div class="welcome-buddy-orbit" aria-hidden="true">
          <span class="welcome-glow"></span>
          <img class="welcome-buddy-image" src="/images/pickle-buddy-wave.png?v=transparent-2" alt="" />
        </div>

        <div class="welcome-copy">
          <h1>나에게 꼭 맞는 스타일,<br />피클이 골라줄게요</h1>
          <p>날씨, 상황, 옷장, 취향까지 모두 고려해<br />매일 더 나다운 코디를 추천해요.</p>
        </div>
      </section>

      <div class="start-actions welcome-actions">
        <button class="primary-button wide welcome-primary" type="button" @click="openAuth('signup')">시작하기</button>
        <button class="text-button welcome-login" type="button" @click="openAuth('login')">이미 계정이 있어요 로그인</button>
      </div>
    </div>
  </PhoneFrame>
</template>
