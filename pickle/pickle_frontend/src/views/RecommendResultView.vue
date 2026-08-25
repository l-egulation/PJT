<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { colorHex } from '@/utils/colors'
import { STYLE_LABELS } from '@/utils/styles'

const router = useRouter()
const storedRecommendation = sessionStorage.getItem('pickle_recommendation') || localStorage.getItem('pickle_recommendation_fallback')
const result = ref(JSON.parse(storedRecommendation || '{"items":[],"reasons":[]}'))
const saved = ref(false)
const savedOutfitId = ref(null)
const saving = ref(false)
const rerolling = ref(false)
const error = ref('')
const itemShapes = { top: 'top', outer: 'top', dress: 'top', bottom: 'pants', shoes: 'shoes', bag: 'bag', accessory: 'bag' }
const categoryLabels = { top: '상의', outer: '아우터', dress: '원피스', bottom: '하의', shoes: '신발', bag: '가방', accessory: '액세서리' }

const outfit = computed(() => ({
  items: result.value.items || [],
  title: `${STYLE_LABELS[result.value.preferred_style] || result.value.preferred_style || '오늘'} 추천 코디`,
  description: `${result.value.weather?.temperature ?? '-'}°C 날씨와 ${result.value.tpo || 'daily'} 상황에 어울려요.`,
}))
const hasResult = computed(() => outfit.value.items.length > 0)
const previewItems = computed(() => {
  const order = ['top', 'outer', 'dress', 'bottom', 'shoes']
  return [...outfit.value.items]
    .sort((a, b) => order.indexOf(a.category) - order.indexOf(b.category))
    .slice(0, 4)
})
const listItems = computed(() => outfit.value.items.slice(0, 4))
const previewSwatches = computed(() => {
  const colors = previewItems.value.map((item) => item.color).filter(Boolean)
  return [...new Set(colors)].slice(0, 3)
})
const styleTitle = computed(() => STYLE_LABELS[result.value.preferred_style] || result.value.preferred_style || '스타일')
const tpoLabel = computed(() => {
  const labels = { work: '출근', date: '데이트', daily: '일상', travel: '여행', formal: '격식' }
  return labels[result.value.tpo] || '오늘 일정'
})

function conciseReason(text) {
  const cleaned = String(text || '')
    .replace(/^["']|["']$/g, '')
    .replace(/\s+/g, ' ')
    .trim()
  if (!cleaned) return ''
  return cleaned.length > 54 ? `${cleaned.slice(0, 54).trim()}...` : cleaned
}

const reasonQuote = computed(() => {
  const llmReason = conciseReason(
    result.value.llm_reason_summary
    || result.value.llm_reasons?.[0]
    || result.value.reasons?.[0],
  )
  if (llmReason) return `피클 생각엔 ${llmReason}`
  return '피클 생각엔 오늘 조건에 가장 잘 맞는 조합이에요!'
})
const isCalendarPlan = computed(() => Boolean(result.value.plan_date))
const targetDate = computed(() => result.value.plan_date || formatDate(new Date()))
const primaryButtonLabel = computed(() => (isCalendarPlan.value ? '캘린더에 저장' : '오늘 입기'))
const editingOutfitId = computed(() => {
  const value = Number(result.value.plan_outfit_id)
  return Number.isInteger(value) && value > 0 ? value : null
})
const rerollStorageKey = computed(() => `pickle_reroll_signatures_${targetDate.value}`)

function formatDate(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function itemSignature(items = result.value.items || []) {
  return items
    .map((item) => Number(item.id))
    .filter((id) => Number.isInteger(id) && id > 0)
    .sort((a, b) => a - b)
}

function readRerollSignatures() {
  try {
    const stored = JSON.parse(sessionStorage.getItem(rerollStorageKey.value) || '[]')
    return Array.isArray(stored) ? stored.filter((signature) => Array.isArray(signature)) : []
  } catch {
    return []
  }
}

function rememberRerollSignature(items = result.value.items || []) {
  const signature = itemSignature(items)
  if (!signature.length) return
  const key = signature.join(',')
  const signatures = readRerollSignatures()
  if (signatures.some((entry) => entry.join(',') === key)) return
  try {
    sessionStorage.setItem(rerollStorageKey.value, JSON.stringify([...signatures, signature]))
  } catch {
    // Reroll history is a nice-to-have; recommendation still works without it.
  }
}

function persistRecommendation(recommendation) {
  const recommendationText = JSON.stringify(recommendation)
  try {
    sessionStorage.setItem('pickle_recommendation', recommendationText)
    localStorage.removeItem('pickle_recommendation_fallback')
  } catch {
    localStorage.setItem('pickle_recommendation_fallback', recommendationText)
  }
}

async function save(wornOn = null) {
  if (!result.value.items.length) return null
  if (saved.value) {
    if (wornOn && savedOutfitId.value) {
      saving.value = true
      try {
        await client.patch(`/outfits/${savedOutfitId.value}/`, { worn_on: wornOn })
      } finally {
        saving.value = false
      }
    }
    return savedOutfitId.value
  }

  saving.value = true
  try {
    if (editingOutfitId.value) {
      const { data } = await client.patch(`/outfits/${editingOutfitId.value}/`, {
        title: outfit.value.title,
        tpo: result.value.tpo,
        weather_note: outfit.value.description,
        worn_on: wornOn,
        item_ids: result.value.items.map((item) => item.id),
      })
      saved.value = true
      savedOutfitId.value = data.id
      return data.id
    }

    const { data } = await client.post('/outfits/', {
      title: outfit.value.title,
      tpo: result.value.tpo,
      weather_note: outfit.value.description,
      worn_on: wornOn,
      item_ids: result.value.items.map((item) => item.id),
      recommendation_id: result.value.recommendation_id,
    })
    saved.value = true
    savedOutfitId.value = data.id
    return data.id
  } finally {
    saving.value = false
  }
}

async function wearToday() {
  await save(targetDate.value)
  router.push({ path: '/planner', query: { date: targetDate.value } })
}

async function saveAndOpenOutfits() {
  await save()
  router.push('/my/outfits')
}

async function rerollOutfit() {
  if (!result.value.items?.length || rerolling.value) return
  rememberRerollSignature()
  rerolling.value = true
  error.value = ''
  try {
    const { data } = await client.post('/outfits/recommend/', {
      tpo: result.value.tpo,
      preferred_style: result.value.preferred_style,
      preferred_colors: result.value.preferred_colors || [],
      weather: result.value.weather,
      excluded_item_signatures: readRerollSignatures(),
    }, {
      timeout: 30000,
    })
    if (!data.items?.length) {
      error.value = data.reasons?.[0] || '다른 추천 조합을 찾지 못했어요.'
      return
    }
    const seenSignatures = readRerollSignatures().map((signature) => signature.join(','))
    if (seenSignatures.includes(itemSignature(data.items).join(','))) {
      error.value = '같은 날에 보여줄 다른 추천 조합이 없어요.'
      return
    }
    const nextRecommendation = {
      ...data,
      plan_date: result.value.plan_date,
      plan_outfit_id: result.value.plan_outfit_id,
    }
    result.value = nextRecommendation
    saved.value = false
    savedOutfitId.value = null
    rememberRerollSignature(nextRecommendation.items)
    persistRecommendation(nextRecommendation)
  } catch (e) {
    error.value = e.response?.data?.detail || '다시 추천을 만들지 못했어요.'
  } finally {
    rerolling.value = false
  }
}

function replaceItem(item) {
  router.push({ path: '/closet', query: { category: item.category || 'all', replace: item.id, from: 'recommend' } })
}

onMounted(() => {
  rememberRerollSignature()
})
</script>

<template>
  <PhoneFrame title="추천 코디" show-back>
    <template #action>
      <button class="plain-action icon-action" type="button" aria-label="홈으로 돌아가기" @click="$router.push('/home')">
        <span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span>
      </button>
    </template>

    <section v-if="!hasResult" class="empty-state">
      추천 결과를 불러오지 못했어요. 다시 추천을 받아주세요.
      <button class="secondary-button wide" type="button" @click="$router.push('/recommend')">추천 조건으로 돌아가기</button>
    </section>

    <template v-else>
      <article class="recommend-result-card">
        <div class="recommend-preview-grid">
          <div v-for="item in previewItems" :key="item.id" class="recommend-preview-piece" :class="`recommend-preview-piece--${itemShapes[item.category] || 'top'}`">
            <img v-if="item.processed_image || item.image" :src="item.processed_image || item.image" :alt="item.name" />
            <i v-else class="recommend-preview-visual" :style="{ '--tone': colorHex(item.color, '#eadfce') }"></i>
            <span>{{ item.name }}</span>
          </div>
        </div>
        <div class="recommend-preview-summary">
          <div class="recommend-preview-swatches">
            <i v-for="color in previewSwatches" :key="color" :style="{ background: colorHex(color, '#eadfce') }"></i>
          </div>
          <h2>{{ styleTitle }} 추천 코디</h2>
        </div>
      </article>

      <article class="recommend-buddy-reason">
        <img src="/images/pickle-buddy-clothes.png" alt="" />
        <p>"{{ reasonQuote }}"</p>
      </article>

      <section class="recommend-items-section">
        <h2>구성 아이템</h2>
        <div class="recommend-item-row" v-for="item in listItems" :key="item.id">
          <div class="recommend-item-thumb">
            <img v-if="item.processed_image || item.image" :src="item.processed_image || item.image" :alt="item.name" />
            <i v-else :style="{ '--tone': colorHex(item.color, '#eadfce') }"></i>
          </div>
          <div><strong>{{ item.name }}</strong><span>{{ categoryLabels[item.category] || item.category || '아이템' }} · {{ tpoLabel }}</span></div>
          <button type="button" @click="replaceItem(item)">교체</button>
        </div>
      </section>

      <p v-if="error" class="error-message">{{ error }}</p>

      <div class="recommend-bottom-actions">
        <button class="primary-button" :disabled="saving || rerolling" @click="wearToday">{{ saving ? '저장 중...' : primaryButtonLabel }}</button>
        <button class="secondary-button icon-only" type="button" :disabled="saving || rerolling" aria-label="다시 추천" @click="rerollOutfit">
          {{ rerolling ? '...' : '↻' }}
        </button>
        <button class="secondary-button icon-only" type="button" :disabled="saving" aria-label="코디 저장" @click="saveAndOpenOutfits">
          <span class="ui-icon" style="--icon: url('/icons/ui/scrap.svg')" aria-hidden="true"></span>
        </button>
      </div>
    </template>
  </PhoneFrame>
</template>
