<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const userStore = useUserStore()
const currentStep = ref(0)

const steps = [
  {
    no: '01',
    title: '내 옷장으로\n오늘의 스타일을 추천해요',
    body: '보유한 아이템을 분석해\n나에게 어울리는 코디를 제안해드려요.',
    image: '/images/pickle-buddy-clothes.png?v=transparent-2',
    mood: 'clothes',
  },
  {
    no: '02',
    title: '날씨와 TPO에 딱 맞는\n스타일을 제안해요',
    body: '실시간 날씨와 약속, 장소에 맞춰\n가장 센스 있는 코디를 골라드려요.',
    image: '/images/pickle-buddy-umbrella.png?v=transparent-2',
    mood: 'weather',
  },
  {
    no: '03',
    title: '나를 알아갈수록\n더 나다운 스타일로',
    body: '좋아하는 스타일과 평가를 학습해\n매일 더 잘 맞는 추천을 만들어요.',
    image: '/images/pickle-buddy-heart.png?v=transparent-2',
    mood: 'heart',
  },
]

const step = computed(() => steps[currentStep.value])
const isLastStep = computed(() => currentStep.value === steps.length - 1)

function next() {
  if (!isLastStep.value) {
    currentStep.value += 1
    return
  }
  const userId = userStore.profile?.id
  if (userId) localStorage.setItem(`pickle_onboarded_${userId}`, 'true')
  router.push('/closet-start')
}
</script>

<template>
  <PhoneFrame>
    <section class="onboarding-shell sprout-onboarding">
      <article class="onboarding-story-card" :class="`onboarding-story-card--${step.mood}`">
        <strong class="onboarding-step">{{ step.no }}</strong>
        <h1>{{ step.title }}</h1>
        <p>{{ step.body }}</p>

        <div class="onboarding-visual" aria-hidden="true">
          <span class="onboarding-visual-glow"></span>
          <span v-if="step.mood === 'weather'" class="weather-sticker sun"></span>
          <span v-if="step.mood === 'weather'" class="weather-sticker rain rain-one"></span>
          <span v-if="step.mood === 'weather'" class="weather-sticker rain rain-two"></span>
          <span v-if="step.mood === 'clothes'" class="heart-sticker"></span>
          <img class="onboarding-buddy-image" :src="step.image" alt="" />
        </div>

        <div class="onboarding-dots">
          <i v-for="(_, index) in steps" :key="index" :class="{ active: index === currentStep }"></i>
        </div>
      </article>

      <button class="primary-button fixed-bottom onboarding-next-button" type="button" @click="next">
        {{ isLastStep ? '시작하기' : '다음' }}
      </button>
    </section>
  </PhoneFrame>
</template>
