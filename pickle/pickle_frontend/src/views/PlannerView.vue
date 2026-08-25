<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import OutfitCard from '@/components/OutfitCard.vue'
import PhoneFrame from '@/components/PhoneFrame.vue'

const router = useRouter()
const route = useRoute()
const outfits = ref([])
const currentDate = new Date()
const initialDateMatch = typeof route.query.date === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(route.query.date)
  ? route.query.date.match(/^(\d{4})-(\d{2})-(\d{2})$/)
  : null
const initialDate = initialDateMatch
  ? new Date(Number(initialDateMatch[1]), Number(initialDateMatch[2]) - 1, Number(initialDateMatch[3]))
  : currentDate
const currentMonthDate = ref(new Date(initialDate.getFullYear(), initialDate.getMonth(), 1))
const selectedDay = ref(initialDate.getDate())
const currentYear = computed(() => currentMonthDate.value.getFullYear())
const currentMonth = computed(() => currentMonthDate.value.getMonth() + 1)
const isCurrentMonth = computed(() => currentYear.value === currentDate.getFullYear() && currentMonth.value === currentDate.getMonth() + 1)
const today = currentDate.getDate()
const days = computed(() => Array.from({ length: new Date(currentYear.value, currentMonth.value, 0).getDate() }, (_, index) => index + 1))
const firstWeekday = computed(() => new Date(currentYear.value, currentMonth.value - 1, 1).getDay())
const weekDays = ['일', '월', '화', '수', '목', '금', '토']
const tpoLabels = { work: '출근', date: '데이트', daily: '일상', travel: '여행', formal: '격식' }

const schedule = {}

const selectedDate = computed(() => (
  `${currentYear.value}-${String(currentMonth.value).padStart(2, '0')}-${String(selectedDay.value).padStart(2, '0')}`
))
const selectedDateObject = computed(() => new Date(currentYear.value, currentMonth.value - 1, selectedDay.value))
const todayDateObject = new Date(currentDate.getFullYear(), currentDate.getMonth(), currentDate.getDate())
const selectedTime = computed(() => selectedDateObject.value.getTime())
const todayTime = todayDateObject.getTime()

const selectedOutfit = computed(() => outfits.value.find((outfit) => outfit.worn_on === selectedDate.value) || null)
const hasSavedOutfit = computed(() => Boolean(selectedOutfit.value))
const hasEvaluation = computed(() => Boolean(selectedOutfit.value?.evaluation))
const showPastEmptyStatus = computed(() => selectedPlan.value.type === 'past' && !hasSavedOutfit.value)
const primaryActionLabel = computed(() => {
  if (selectedPlan.value.type === 'future') {
    return hasSavedOutfit.value ? '추천 코디 수정하기' : '이 날 코디 구성하기'
  }
  return '저장된 코디 평가하기'
})

const selectedPlan = computed(() => (isCurrentMonth.value ? schedule[selectedDay.value] : null) || {
  type: selectedTime.value < todayTime ? 'past' : selectedTime.value === todayTime ? 'today' : 'future',
  label: selectedTime.value < todayTime ? '기록' : selectedTime.value === todayTime ? '오늘' : '예정',
  weather: selectedTime.value < todayTime ? '기록 확인' : selectedTime.value === todayTime ? '오늘 날씨' : '직접 입력',
  title: selectedTime.value < todayTime ? '지난 코디 기록' : selectedTime.value === todayTime ? '오늘의 코디' : '코디 계획',
  note: selectedTime.value < todayTime
    ? '저장된 코디가 있으면 평가할 수 있어요.'
    : selectedTime.value === todayTime
      ? '추천 결과에서 오늘 입기를 누르면 이 날짜에 저장돼요.'
      : '여행이나 약속을 위한 코디를 미리 준비하고 캘린더에 저장해요.',
})

const previewOutfit = computed(() => selectedOutfit.value || {
  items: [],
  title: selectedPlan.value.type === 'future' ? '미리 구성할 코디' : '저장한 코디 없음',
  description: selectedPlan.value.note,
})

const planTypeLabel = computed(() => {
  if (selectedPlan.value.type === 'future') return '미리 준비'
  if (selectedOutfit.value) return '평가 가능'
  return selectedPlan.value.type === 'today' ? '오늘 기록' : '기록 없음'
})

function getDateByDay(day) {
  return `${currentYear.value}-${String(currentMonth.value).padStart(2, '0')}-${String(day).padStart(2, '0')}`
}

function getOutfitOn(day) {
  return outfits.value.find((outfit) => outfit.worn_on === getDateByDay(day)) || null
}

function hasOutfitOn(day) {
  return Boolean(getOutfitOn(day))
}

function dayLabel(day) {
  const outfit = getOutfitOn(day)
  if (outfit) return tpoLabels[outfit.tpo] || outfit.tpo || '코디'
  const plan = isCurrentMonth.value ? schedule[day] : null
  return plan?.label || ''
}

function dayClass(day) {
  const plan = isCurrentMonth.value ? schedule[day] : null
  const outfit = getOutfitOn(day)
  return {
    selected: day === selectedDay.value,
    today: isCurrentMonth.value && day === today,
    planned: plan?.type === 'future',
    past: plan?.type === 'past',
    saved: Boolean(outfit),
    [`tpo-${outfit?.tpo}`]: Boolean(outfit?.tpo),
  }
}

function moveMonth(offset) {
  currentMonthDate.value = new Date(currentYear.value, currentMonth.value - 1 + offset, 1)
  const lastDay = new Date(currentYear.value, currentMonth.value, 0).getDate()
  selectedDay.value = Math.min(selectedDay.value, lastDay)
}

function goPrimary() {
  if (selectedPlan.value.type === 'future') {
    router.push({
      path: '/recommend',
      query: {
        planDay: selectedDay.value,
        planDate: selectedDate.value,
        planOutfit: selectedOutfit.value?.id,
        tpo: selectedOutfit.value?.tpo || selectedPlan.value.tpo || 'daily',
      },
    })
    return
  }
  const targetOutfit = selectedOutfit.value
  if (!targetOutfit) return
  router.push({ path: '/evaluation', query: { outfit: targetOutfit.id, day: selectedDay.value } })
}

onMounted(async () => {
  const { data } = await client.get('/outfits/')
  outfits.value = data.results || data
})
</script>

<template>
  <PhoneFrame show-nav>
    <template #title>
      <img class="header-title-logo header-title-logo--calendar" src="/images/calendar_logo.png" alt="캘린더" />
    </template>

    <section class="calendar-card planner-calendar">
      <header>
        <button class="calendar-month-button" type="button" aria-label="이전 달" @click="moveMonth(-1)">‹</button>
        <h2>{{ currentYear }}년 {{ currentMonth }}월</h2>
        <button class="calendar-month-button" type="button" aria-label="다음 달" @click="moveMonth(1)">›</button>
      </header>
      <div class="week-row">
        <span v-for="day in weekDays" :key="day">{{ day }}</span>
      </div>
      <div class="calendar-grid planner-calendar-grid">
        <span v-for="blank in firstWeekday" :key="`b${blank}`"></span>
        <button
          v-for="day in days"
          :key="day"
          type="button"
          :class="dayClass(day)"
          @click="selectedDay = day"
        >
          <b>{{ day }}</b>
          <i v-if="dayLabel(day)">{{ dayLabel(day) }}</i>
        </button>
      </div>
    </section>

    <section v-if="showPastEmptyStatus" class="planner-past-status">
      <span>{{ planTypeLabel }}</span>
    </section>

    <section v-if="!hasSavedOutfit && !showPastEmptyStatus" class="planner-day-card">
      <header>
        <div>
          <strong>{{ currentMonth }}.{{ selectedDay }} 일정</strong>
          <p>{{ selectedPlan.weather }}</p>
        </div>
        <span>{{ planTypeLabel }}</span>
      </header>
      <h2>{{ selectedPlan.title }}</h2>
      <p>{{ selectedPlan.note }}</p>
    </section>

    <OutfitCard v-if="selectedOutfit" :outfit="previewOutfit" compact />

    <section v-if="selectedPlan.type === 'future' && !hasSavedOutfit" class="planner-prep-card">
      <h2>미리 정해둘 것</h2>
      <div class="planner-prep-grid">
        <span>TPO</span><strong>{{ selectedPlan.label }}</strong>
        <span>날씨</span><strong>예상 기온 직접 입력</strong>
        <span>기준</span><strong>내 옷장 + 선호 스타일</strong>
      </div>
    </section>

    <p v-if="selectedPlan.type !== 'future' && !hasSavedOutfit" class="planner-empty-guide">
      저장된 코디가 있을 때만 평가할 수 있어요. 추천 결과에서 먼저 오늘 입기 또는 캘린더에 저장을 눌러주세요.
    </p>
    <button
      v-if="selectedPlan.type === 'future' || (selectedPlan.type !== 'future' && hasSavedOutfit && !hasEvaluation)"
      class="primary-button wide planner-primary-action"
      type="button"
      @click="goPrimary"
    >
      {{ primaryActionLabel }}
    </button>
  </PhoneFrame>
</template>
