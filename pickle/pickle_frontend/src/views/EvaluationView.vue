<script setup>
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import PickleBuddy from '@/components/PickleBuddy.vue'

const route = useRoute()
const router = useRouter()
const rating = ref(4)
const weatherFit = ref(3)
const rewear = ref(5)
const feedback = ref('')
const saving = ref(false)
const hasOutfit = computed(() => Boolean(route.query.outfit))

const weatherOptions = [
  { label: '추웠어요', value: 2 },
  { label: '딱 맞아요', value: 3 },
  { label: '더웠어요', value: 5 },
]
const rewearOptions = [
  { label: '아니요', value: 2 },
  { label: '보통', value: 3 },
  { label: '네!', value: 5 },
]

async function save() {
  if (!hasOutfit.value) return
  saving.value = true
  try {
    await client.put(`/outfits/${route.query.outfit}/evaluate/`, {
      rating: rating.value,
      fit_score: weatherFit.value,
      activity_score: rewear.value,
      satisfaction_score: rating.value,
      feedback: feedback.value,
    })
    router.push('/planner')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <PhoneFrame title="코디 평가" show-back>
    <section v-if="!hasOutfit" class="evaluation-empty">
      <div class="evaluation-mascot"><PickleBuddy size="small" variant="shy" /></div>
      <h1>평가할 코디가 없어요</h1>
      <p>저장된 코디를 선택해야 평가를 남길 수 있어요.</p>
      <button class="secondary-button wide" type="button" @click="$router.push('/planner')">캘린더로 돌아가기</button>
    </section>

    <template v-else>
      <section class="evaluation-loop">
        <div class="evaluation-mascot"><PickleBuddy size="small" variant="heart" /></div>
        <h1>오늘 코디 어땠어요?</h1>
        <p>평가할수록 추천이 더 똑똑해져요</p>

        <div class="stars evaluation-stars">
          <button
            v-for="score in 5"
            :key="score"
            type="button"
            :aria-label="`${score}점`"
            @click="rating = score"
          >
            <span
              class="ui-icon"
              :style="{ '--icon': `url(${score <= rating ? '/icons/ui/filled_star.svg' : '/icons/ui/star.svg'})` }"
              aria-hidden="true"
            ></span>
          </button>
        </div>
        <strong class="rating-score">{{ rating }}점</strong>
      </section>

      <section class="evaluation-group">
        <h2>날씨 적합도</h2>
        <div class="evaluation-chip-row">
          <button
            v-for="item in weatherOptions"
            :key="item.label"
            type="button"
            :class="{ selected: weatherFit === item.value }"
            @click="weatherFit = item.value"
          >
            {{ item.label }}
          </button>
        </div>
      </section>

      <section class="evaluation-group">
        <h2>또 입고 싶나요?</h2>
        <div class="evaluation-chip-row">
          <button
            v-for="item in rewearOptions"
            :key="item.label"
            type="button"
            :class="{ selected: rewear === item.value }"
            @click="rewear = item.value"
          >
            {{ item.label }}
          </button>
        </div>
      </section>

      <label class="evaluation-note">
        한 줄 메모
        <textarea v-model="feedback" placeholder="좋았던 점이나 아쉬웠던 점을 남겨주세요."></textarea>
      </label>

      <button class="primary-button fixed-bottom" type="button" :disabled="saving" @click="save">
        {{ saving ? '저장 중...' : '평가 완료' }}
      </button>
    </template>
  </PhoneFrame>
</template>
