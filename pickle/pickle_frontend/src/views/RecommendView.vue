<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import TagChip from '@/components/TagChip.vue'
import { COLOR_OPTIONS } from '@/utils/colors'
import { AIHUB_PARENT_STYLE_OPTIONS } from '@/utils/styles'
import { describeWeather, getCurrentCoordinates } from '@/utils/weather'

const route = useRoute()
const router = useRouter()
const tpo = ref('')
const preferredStyle = ref('')
const selectedStyleId = ref('')
const colors = ref([])
const recommendMode = ref('style')
const referenceAnalysis = ref(null)
const referenceImagePreview = ref('')
const referenceLoading = ref(false)
const referenceFileName = ref('')
const loading = ref(false)
const weatherLoading = ref(true)
const error = ref('')
const weather = ref(null)
const coordinates = ref(null)
const manualTemperature = ref(23)
const manualWeatherCode = ref(1)

const tpos = [
  { value: 'work', label: '출근' },
  { value: 'date', label: '데이트' },
  { value: 'daily', label: '약속' },
  { value: 'travel', label: '여행' },
  { value: 'formal', label: '격식' },
]

const weatherOptions = [
  { value: 0, label: '맑음' },
  { value: 1, label: '구름 조금' },
  { value: 3, label: '흐림' },
  { value: 61, label: '비' },
]

const styles = AIHUB_PARENT_STYLE_OPTIONS
const isFuturePlan = computed(() => Boolean(route.query.planDay))
const planDate = computed(() => {
  const value = route.query.planDate
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null
})
const planOutfitId = computed(() => {
  const value = Number(route.query.planOutfit)
  return Number.isInteger(value) && value > 0 ? value : null
})
const fixedItemId = computed(() => {
  const value = Number(route.query.fixedItem)
  return Number.isInteger(value) && value > 0 ? value : null
})
const effectiveWeather = computed(() => isFuturePlan.value
  ? { temperature: Number(manualTemperature.value), weather_code: Number(manualWeatherCode.value), source: 'Manual' }
  : weather.value)
const weatherDescription = computed(() => describeWeather(effectiveWeather.value?.weather_code))
const referenceStyleLabel = computed(() => {
  const style = referenceAnalysis.value?.style || preferredStyle.value
  return styles.find((item) => item.value === style)?.label || style || ''
})
const referenceColorLabel = computed(() => {
  const color = referenceAnalysis.value?.color
  return COLOR_OPTIONS.find((item) => item.name === color)?.label || color || ''
})
const canRecommend = computed(() => {
  const hasStyleSource = recommendMode.value === 'style'
    ? Boolean(preferredStyle.value)
    : Boolean(referenceAnalysis.value?.style && preferredStyle.value && !referenceLoading.value)
  return Boolean(tpo.value && hasStyleSource && effectiveWeather.value && !loading.value)
})

function toggleColor(color) {
  colors.value = colors.value.includes(color)
    ? colors.value.filter((item) => item !== color)
    : [...colors.value, color]
}

function selectStyle(style) {
  selectedStyleId.value = style.id
  preferredStyle.value = style.value
}

function selectRecommendMode(mode) {
  recommendMode.value = mode
  error.value = ''
}

function clearStoredRecommendation() {
  try {
    sessionStorage.removeItem('pickle_recommendation')
    sessionStorage.removeItem('pickle_weather_location')
  } catch {
    // Ignore blocked storage; the next write attempt will surface the issue.
  }
  try {
    localStorage.removeItem('pickle_recommendation_fallback')
    localStorage.removeItem('pickle_weather_location_fallback')
  } catch {
    // Ignore blocked storage; the next write attempt will surface the issue.
  }
}

function persistRecommendation(recommendation, locationText) {
  const recommendationText = JSON.stringify(recommendation)
  clearStoredRecommendation()
  try {
    sessionStorage.setItem('pickle_recommendation', recommendationText)
    sessionStorage.setItem('pickle_weather_location', locationText)
    return true
  } catch {
    try {
      localStorage.setItem('pickle_recommendation_fallback', recommendationText)
      localStorage.setItem('pickle_weather_location_fallback', locationText)
      return true
    } catch {
      return false
    }
  }
}

function formatApiError(error) {
  if (error.code === 'ECONNABORTED') return '추천 시간이 오래 걸리고 있어요. 잠시 후 다시 시도해 주세요.'
  const data = error.response?.data
  if (!data) return '추천을 만들지 못했습니다.'
  if (typeof data === 'string') return data
  if (data.detail) return data.detail
  const firstField = Object.keys(data)[0]
  const message = firstField ? data[firstField] : null
  if (Array.isArray(message)) return message[0]
  return message || '추천을 만들지 못했습니다.'
}

async function analyzeReferenceImage(event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file) return

  referenceLoading.value = true
  referenceAnalysis.value = null
  referenceFileName.value = file.name
  error.value = ''

  if (referenceImagePreview.value) URL.revokeObjectURL(referenceImagePreview.value)
  referenceImagePreview.value = URL.createObjectURL(file)

  const body = new FormData()
  body.append('image', file)
  try {
    const { data } = await client.post('/closet/analyze-image/', body, { timeout: 30000 })
    referenceAnalysis.value = data
    if (data.style) {
      preferredStyle.value = data.style
      selectedStyleId.value = styles.find((item) => item.value === data.style)?.id || ''
    }
    if (data.color) colors.value = [data.color]
    if (!data.style) {
      error.value = '이미지에서 스타일을 찾지 못했어요. 선호 스타일을 직접 선택해 주세요.'
      recommendMode.value = 'style'
    }
  } catch (e) {
    error.value = e.response?.data?.detail || e.response?.data?.image || '추구미 이미지를 분석하지 못했어요.'
  } finally {
    referenceLoading.value = false
  }
}

async function loadWeather() {
  weatherLoading.value = true
  coordinates.value = await getCurrentCoordinates()
  try {
    const { data } = await client.get('/outfits/weather/', {
      params: {
        latitude: coordinates.value.latitude,
        longitude: coordinates.value.longitude,
      },
    })
    weather.value = data
    error.value = ''
  } catch (e) {
    error.value = e.response?.data?.detail || '날씨를 불러오지 못했습니다.'
  } finally {
    weatherLoading.value = false
  }
}

async function recommend() {
  if (!canRecommend.value) {
    error.value = 'TPO, 날씨, 스타일 기준을 모두 선택해 주세요.'
    return
  }
  loading.value = true
  error.value = ''
  let recommendation = null
  try {
    const { data } = await client.post('/outfits/recommend/', {
      tpo: tpo.value,
      preferred_style: preferredStyle.value,
      preferred_colors: colors.value,
      fixed_item_id: fixedItemId.value,
      recommendation_mode: recommendMode.value,
      reference_analysis: referenceAnalysis.value,
      ...(isFuturePlan.value
        ? { weather: effectiveWeather.value }
        : { latitude: coordinates.value.latitude, longitude: coordinates.value.longitude }),
    }, {
      timeout: 30000,
    })
    recommendation = {
      ...data,
      plan_date: planDate.value,
      plan_outfit_id: planOutfitId.value,
    }
  } catch (e) {
    error.value = formatApiError(e)
    loading.value = false
    return
  }

  if (!recommendation.items?.length) {
    error.value = recommendation.reasons?.[0] || '추천할 수 있는 코디 조합을 찾지 못했어요. 조건을 조금 바꿔주세요.'
    loading.value = false
    return
  }

  error.value = ''
  const locationText = isFuturePlan.value ? `${route.query.planDay}일 예정` : (coordinates.value?.locationLabel || '현재 위치')
  if (!persistRecommendation(recommendation, locationText)) {
    error.value = '추천 결과를 저장하지 못했어요. 브라우저 저장소 권한을 확인한 뒤 다시 시도해 주세요.'
    loading.value = false
    return
  }
  router.replace('/recommend/result')
}

onMounted(() => {
  if (route.query.tpo) tpo.value = String(route.query.tpo)
  if (isFuturePlan.value) {
    weatherLoading.value = false
    return
  }
  loadWeather()
})
</script>

<template>
  <PhoneFrame title="추천 조건" show-back show-nav>
    <section class="filter-section">
      <h2>TPO</h2>
      <div class="tag-grid">
        <button v-for="item in tpos" :key="item.value" class="tag-chip" :class="{ selected: tpo === item.value }" type="button" @click="tpo = item.value">{{ item.label }}</button>
      </div>
    </section>

    <section v-if="isFuturePlan" class="weather-slider future-weather-card">
      <h2>예상 날씨 <small>{{ route.query.planDay }}일 일정</small></h2>
      <label>예상 기온
        <input v-model.number="manualTemperature" type="number" min="-30" max="45" />
      </label>
      <div class="tag-grid">
        <button
          v-for="item in weatherOptions"
          :key="item.value"
          class="tag-chip"
          :class="{ selected: manualWeatherCode === item.value }"
          type="button"
          @click="manualWeatherCode = item.value"
        >
          {{ item.label }}
        </button>
      </div>
      <p>{{ Math.round(effectiveWeather.temperature) }}°C / {{ weatherDescription.label }} 기준으로 추천해요.</p>
    </section>

    <section v-else class="weather-slider">
      <h2>현재 날씨 <small>{{ coordinates?.locationLabel || '서울 기준' }}</small></h2>
      <div v-if="weather" class="weather-inline">
        <span>{{ weatherDescription.icon }}</span>
        <strong>{{ Math.round(weather.temperature) }}°C / {{ weatherDescription.label }}</strong>
        <span>현재</span>
      </div>
      <p v-else>{{ weatherLoading ? '현재 날씨를 확인하고 있어요.' : '날씨 정보 없음' }}</p>
    </section>

    <section class="filter-section">
      <h2>추천 기준</h2>
      <div class="tag-grid">
        <button class="tag-chip" :class="{ selected: recommendMode === 'style' }" type="button" @click="selectRecommendMode('style')">선호 스타일</button>
        <button class="tag-chip" :class="{ selected: recommendMode === 'reference' }" type="button" @click="selectRecommendMode('reference')">추구미 이미지</button>
      </div>
    </section>

    <section v-if="recommendMode === 'style'" class="filter-section">
      <h2>선호 스타일</h2>
      <div class="tag-grid">
        <TagChip
          v-for="item in styles"
          :key="item.id"
          :label="item.label"
          :selected="selectedStyleId === item.id"
          @click="selectStyle(item)"
        />
      </div>
    </section>

    <section v-else class="filter-section reference-style-section">
      <h2>추구미 등록 <small>이미지로 추천</small></h2>
      <label class="upload-card reference-upload-card">
        <input type="file" accept="image/*" @change="analyzeReferenceImage" />
        <img v-if="referenceImagePreview" :src="referenceImagePreview" alt="추구미 이미지 미리보기" />
        <template v-else>
          <span class="upload-plus">+</span>
          <strong>원하는 스타일 이미지 등록</strong>
          <small>이미지를 분석해서 내 옷장에서 비슷하게 골라요.</small>
        </template>
      </label>
      <div v-if="referenceLoading" class="reference-analysis-card">추구미 이미지를 분석 중이에요...</div>
      <div v-else-if="referenceAnalysis" class="reference-analysis-card">
        <strong>{{ referenceStyleLabel || '스타일 분석 완료' }}</strong>
        <span v-if="referenceAnalysis.aihub_style">{{ referenceAnalysis.aihub_style }}</span>
        <span v-if="referenceColorLabel">대표 색상 {{ referenceColorLabel }}</span>
        <small>{{ referenceFileName }}</small>
      </div>
    </section>

    <section class="filter-section">
      <h2>선호 색상 <small>(선택)</small></h2>
      <div class="color-row recommendation-colors">
        <button v-for="color in COLOR_OPTIONS" :key="color.name" class="color-dot" :class="{ selected: colors.includes(color.name) }" :style="{ '--color': color.hex }" :title="color.label" type="button" @click="toggleColor(color.name)" />
      </div>
    </section>

    <p v-if="error" class="error-message">{{ error }}</p>
    <button class="primary-button fixed-bottom with-nav-space" :disabled="!canRecommend" @click="recommend">{{ loading ? '날씨와 옷장을 분석 중...' : '추천받기' }}</button>
  </PhoneFrame>
</template>
