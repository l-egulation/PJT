<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import client from '@/api/client'
import OutfitFlatlay from '@/components/OutfitFlatlay.vue'
import PhoneFrame from '@/components/PhoneFrame.vue'
import PickleBuddy from '@/components/PickleBuddy.vue'
import { describeWeather, getCurrentCoordinates } from '@/utils/weather'

const router = useRouter()
const dashboard = ref(null)
const clothes = ref([])
const outfits = ref([])
const weather = ref(null)
const locationLabel = ref('위치 확인 중')
const storedRecommendation = ref(null)

const today = new Intl.DateTimeFormat('ko-KR', {
  month: 'long',
  day: 'numeric',
  weekday: 'long',
}).format(new Date())

const weatherDescription = computed(() => describeWeather(weather.value?.weather_code))
const closetPreview = computed(() => clothes.value.slice(0, 5))
const outfitPreview = computed(() => outfits.value.slice(0, 5))
const closetCount = computed(() => {
  const totalItems = dashboard.value?.stats?.total_items
  return totalItems === 0 || totalItems ? Number(totalItems) : clothes.value.length
})
const isClosetEmpty = computed(() => Boolean(dashboard.value) && closetCount.value === 0)
const weatherLabel = computed(() => (weather.value ? `${Math.round(weather.value.temperature)}°` : '--°'))
const dustLabel = computed(() => (weather.value ? '미세먼지 좋음' : '날씨 확인 중'))
const walkLabel = computed(() => {
  if (!weather.value) return '확인 중'

  const code = Number(weather.value.weather_code)
  const temp = Number(weather.value.temperature)
  const rainCodes = [51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99]
  const snowCodes = [71, 73, 75, 77, 85, 86]

  if (rainCodes.includes(code) || snowCodes.includes(code)) return '우산 챙기기'
  if (temp <= 5) return '따뜻하게 입기'
  if (temp <= 17) return '가벼운 겉옷'
  if (temp >= 28) return '더위 조심'
  return ''
})

function seasonsForTemperature(temperature) {
  if (temperature >= 23) return ['summer', 'all']
  if (temperature >= 17) return ['spring', 'summer', 'all']
  if (temperature >= 12) return ['spring', 'fall', 'all']
  if (temperature >= 5) return ['fall', 'winter', 'all']
  return ['winter', 'all']
}

function itemSeasons(item) {
  return String(item.season || 'all')
    .split(',')
    .map((season) => season.trim())
    .filter(Boolean)
}

function matchesSeason(item, seasons) {
  const values = itemSeasons(item)
  return values.includes('all') || values.some((season) => seasons.includes(season))
}

function temperatureOnlyOutfit(items, currentWeather) {
  if (!currentWeather || !items.length) return []

  const temperature = Number(currentWeather.temperature)
  const seasons = seasonsForTemperature(temperature)
  const sorted = [...items].sort((a, b) => {
    const seasonDiff = Number(matchesSeason(b, seasons)) - Number(matchesSeason(a, seasons))
    if (seasonDiff) return seasonDiff
    return Number(a.id || 0) - Number(b.id || 0)
  })
  const byCategory = (category) => sorted.find((item) => item.category === category && matchesSeason(item, seasons))
    || sorted.find((item) => item.category === category)

  const top = byCategory('top')
  const bottom = byCategory('bottom')
  const dress = byCategory('dress')
  const shoes = byCategory('shoes')
  const outer = temperature < 20 ? byCategory('outer') : null
  const base = dress ? [outer, dress, shoes] : [outer, top, bottom, shoes]

  return base.filter(Boolean)
}

const homeOutfit = computed(() => {
  const stored = storedRecommendation.value
  if (stored) {
    return {
      items: stored.items || [],
      title: '오늘의 추천 코디',
      description: `${stored.weather?.temperature ?? '-'}° · ${describeWeather(stored.weather?.weather_code).label}에 어울리는 가벼운 조합이에요.`,
    }
  }

  return {
    items: temperatureOnlyOutfit(clothes.value, weather.value),
    title: '오늘의 데일리룩',
    description: '추천 조건을 설정하면 내 옷장에 맞는 코디가 더 정확해져요.',
  }
})

const pickDescription = computed(() => {
  if (isClosetEmpty.value) return '옷장을 채우면 내 옷으로 코디를 추천해드릴게요.'
  if (storedRecommendation.value) return homeOutfit.value.description
  if (!weather.value) return homeOutfit.value.description
  return `${Math.round(weather.value.temperature)}° · ${weatherDescription.value.label}에 어울리는 가벼운 조합이에요.`
})

function loadStoredRecommendation() {
  const stored = sessionStorage.getItem('pickle_recommendation') || localStorage.getItem('pickle_recommendation_fallback')
  storedRecommendation.value = stored ? JSON.parse(stored) : null
}

function goPickDetail() {
  router.push(storedRecommendation.value ? '/recommend/result' : '/recommend')
}

onMounted(async () => {
  loadStoredRecommendation()
  const coordinates = await getCurrentCoordinates()
  locationLabel.value = coordinates.locationLabel

  const [home, closet, savedOutfits] = await Promise.all([
    client.get('/home/'),
    client.get('/closet/'),
    client.get('/outfits/'),
  ])
  dashboard.value = home.data
  clothes.value = closet.data.results || closet.data
  outfits.value = savedOutfits.data.results || savedOutfits.data
  if (closetCount.value === 0) {
    storedRecommendation.value = null
    sessionStorage.removeItem('pickle_recommendation')
    localStorage.removeItem('pickle_recommendation_fallback')
  }

  try {
    const { data } = await client.get('/outfits/weather/', {
      params: { latitude: coordinates.latitude, longitude: coordinates.longitude },
    })
    weather.value = data
  } catch {
    locationLabel.value = '날씨 조회 실패'
  }
})
</script>

<template>
  <PhoneFrame show-nav>
    <section class="home-top">
      <header class="home-topbar">
        <img class="home-logo-image" src="/images/pickle-logo.png" alt="pickle pick your style" />
        <div class="home-top-actions">
          <button class="home-round-button" type="button" aria-label="알림">
            <span class="ui-icon" style="--icon: url('/icons/ui/bell.svg')" aria-hidden="true"></span>
            <i aria-hidden="true"></i>
          </button>
          <button class="home-profile-button" type="button" aria-label="마이페이지" @click="$router.push('/my')">
            <img src="/images/pickle-buddy-profile.png" alt="" />
          </button>
        </div>
      </header>

      <p class="home-date">{{ today }}</p>
      <h1>오늘 뭐 입지?</h1>

      <article class="home-weather-pill">
        <span class="home-weather-icon">{{ weatherDescription.icon }}</span>
        <strong>{{ weatherLabel }}</strong>
        <i></i>
        <span>{{ dustLabel }}</span>
        <i></i>
        <span>{{ locationLabel.replace(' 기준', '') }}</span>
        <span class="home-weather-status">{{ walkLabel }}</span>
      </article>
    </section>

    <section class="home-pick-hero">
      <article class="home-pick-card">
        <div class="home-pick-card-heading">
          <h2 class="home-pick-title">
            <span class="home-pick-title-text">오늘의</span>
            <img
              class="home-pick-logo"
              src="/images/pick-logo-cropped.png"
              alt="Pick"
            />
          </h2>
          <p>{{ pickDescription }}</p>
        </div>

        <div v-if="isClosetEmpty" class="home-pick-empty">
          <PickleBuddy size="small" variant="clothes" />
          <strong>옷장을 채워주세요</strong>
          <p>옷을 등록하면 날씨와 취향에 맞춰 코디를 추천해드릴게요.</p>
        </div>
        <OutfitFlatlay v-else :items="homeOutfit.items" />

        <div class="home-pick-action-row">
          <button
            class="primary-button home-pick-inner-button"
            @click="isClosetEmpty ? $router.push('/clothes/new') : $router.push('/planner')"
          >
            {{ isClosetEmpty ? '옷 등록하기' : '오늘 코디 입기' }}
          </button>
          <button v-if="!isClosetEmpty" class="home-pick-detail-button" type="button" @click="goPickDetail">
            자세히 ›
          </button>
        </div>
      </article>
    </section>

    <div class="section-title-row">
      <h2>내 옷장</h2>
      <button class="text-link" type="button" @click="$router.push('/closet')">전체 보기</button>
    </div>
    <section class="closet-preview-row">
      <button
        v-for="item in closetPreview"
        :key="item.id"
        type="button"
        class="closet-preview-item"
        @click="$router.push('/closet')"
      >
        <img v-if="item.processed_image || item.image" :src="item.processed_image || item.image" :alt="item.name" />
        <span v-else>{{ item.name?.slice(0, 1) || 'p' }}</span>
      </button>
    </section>

    <div class="section-title-row home-outfit-title-row">
      <h2>내 코디</h2>
      <button class="text-link" type="button" @click="$router.push('/my/outfits')">전체 보기</button>
    </div>
    <section v-if="outfitPreview.length" class="home-outfit-strip">
      <button
        v-for="outfit in outfitPreview"
        :key="outfit.id"
        type="button"
        class="home-outfit-card"
        @click="$router.push('/my/outfits')"
      >
        <OutfitFlatlay :items="outfit.items" />
      </button>
    </section>
    <button v-else class="home-outfit-empty" type="button" @click="$router.push('/recommend')">
      저장한 코디가 없어요
    </button>
  </PhoneFrame>
</template>
