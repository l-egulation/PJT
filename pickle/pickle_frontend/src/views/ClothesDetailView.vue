<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import OutfitFlatlay from '@/components/OutfitFlatlay.vue'
import PhoneFrame from '@/components/PhoneFrame.vue'
import PickleBuddy from '@/components/PickleBuddy.vue'
import { colorHex } from '@/utils/colors'

const route = useRoute()
const router = useRouter()
const item = ref(null)
const loading = ref(true)
const deleting = ref(false)
const error = ref('')
const showMoreSheet = ref(false)

const categoryLabels = {
  top: '상의',
  bottom: '하의',
  outer: '아우터',
  dress: '원피스',
  shoes: '신발',
  bag: '가방',
  accessory: '액세서리',
}
const seasonLabels = { spring: '봄', summer: '여름', fall: '가을', winter: '겨울', all: '사계절' }
const styleLabels = {
  minimal: '미니멀',
  casual: '캐주얼',
  lovely: '러블리',
  street: '스트릿',
  classic: '클래식',
  office: '오피스룩',
  daily: '데일리룩',
  date: '데이트룩',
}

const imageUrl = computed(() => item.value?.flatlay_image || item.value?.processed_image || item.value?.image || '')
const tone = computed(() => colorHex(item.value?.color, '#eadfce'))
const categoryLabel = computed(() => categoryLabels[item.value?.category] || item.value?.category || '미정')
const brandLabel = computed(() => {
  if (!item.value?.shopping_url) return 'PICKLE'
  try {
    return new URL(item.value.shopping_url).hostname.replace(/^www\./, '').toUpperCase()
  } catch {
    return 'PICKLE'
  }
})
const materialLabel = computed(() => item.value?.material || '소재 미정')
const colorLabel = computed(() => item.value?.color || '색상 미정')
const styleLabel = computed(() => styleLabels[item.value?.style] || item.value?.aihub_style || item.value?.style || '스타일 미정')
const seasonText = computed(() =>
  String(item.value?.season || 'all')
    .split(',')
    .map((season) => seasonLabels[season.trim()] || season.trim())
    .join(', ')
)
const wearCount = computed(() => Number(item.value?.wear_count || 0))
const lastWorn = computed(() => formatDate(item.value?.last_worn) || '-')
const registeredDate = computed(() => formatDate(item.value?.created_at) || '-')
const chips = computed(() => [materialLabel.value, seasonText.value, styleLabel.value].filter(Boolean).slice(0, 3))
const pairedOutfits = computed(() => item.value?.paired_outfits || [])

function formatDate(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return `${date.getMonth() + 1}.${String(date.getDate()).padStart(2, '0')}`
}

function recommendWithItem() {
  if (!item.value) return
  router.push({ path: '/recommend', query: { fixedItem: item.value.id } })
}

function goEdit() {
  if (!item.value) return
  showMoreSheet.value = false
  router.push(`/clothes/${item.value.id}/edit`)
}

async function removeItem() {
  if (!item.value || deleting.value) return
  const confirmed = window.confirm(`'${item.value.name}'을 옷장에서 삭제할까요?`)
  if (!confirmed) return
  deleting.value = true
  error.value = ''
  try {
    await client.delete(`/closet/${item.value.id}/`)
    router.push('/closet')
  } catch (requestError) {
    error.value = requestError.response?.data?.detail || '옷을 삭제하지 못했습니다.'
    showMoreSheet.value = false
  } finally {
    deleting.value = false
  }
}

async function loadItem() {
  loading.value = true
  error.value = ''
  try {
    const { data } = await client.get(`/closet/${route.params.id}/`)
    item.value = data
  } catch (requestError) {
    error.value = requestError.response?.status === 404 ? '옷 정보를 찾을 수 없습니다.' : '옷 정보를 불러오지 못했습니다.'
  } finally {
    loading.value = false
  }
}

onMounted(loadItem)
</script>

<template>
  <PhoneFrame title="옷 상세" show-back>
    <template #action>
      <div v-if="item" class="clothes-more-wrap">
        <button class="plain-action icon-action" type="button" aria-label="더보기" @click="showMoreSheet = !showMoreSheet">
          <span class="ui-icon" style="--icon: url('/icons/ui/menu-dots.svg')" aria-hidden="true"></span>
        </button>
        <aside v-if="showMoreSheet" class="clothes-more-menu" aria-label="옷 메뉴" @click.stop>
          <button type="button" @click="goEdit">옷 수정</button>
          <button class="danger" type="button" :disabled="deleting" @click="removeItem">{{ deleting ? '삭제 중...' : '옷 삭제' }}</button>
        </aside>
      </div>
    </template>

    <p v-if="loading" class="empty-state">옷 정보를 불러오는 중...</p>
    <p v-else-if="error && !item" class="empty-state">{{ error }}</p>

    <section v-else class="clothes-detail-page">
      <div class="clothes-detail-hero" :style="{ '--tone': tone }">
        <img v-if="imageUrl" :src="imageUrl" :alt="item.name" />
        <span v-else class="detail-shirt-shape"></span>
        <span class="detail-color-pill"><i :style="{ background: tone }"></i>{{ colorLabel }}</span>
      </div>

      <section class="clothes-detail-title">
        <div>
          <span>{{ brandLabel }}</span>
          <h2>{{ item.name }}</h2>
        </div>
        <strong>{{ categoryLabel }}</strong>
      </section>

      <div class="detail-chip-row">
        <span v-for="chip in chips" :key="chip">{{ chip }}</span>
      </div>

      <section class="wear-insight-grid" aria-label="착용 정보">
        <article><strong>{{ wearCount }}회</strong><span>착용</span></article>
        <article><strong>{{ lastWorn }}</strong><span>마지막 착용</span></article>
        <article><strong>{{ registeredDate }}</strong><span>등록일</span></article>
      </section>

      <section class="paired-outfits">
        <header>
          <h3>함께 입은 코디</h3>
          <span>{{ pairedOutfits.length }}개</span>
        </header>
        <div v-if="pairedOutfits.length" class="paired-outfit-list">
          <article v-for="outfit in pairedOutfits" :key="outfit.id">
            <div class="paired-outfit-visual">
              <OutfitFlatlay :items="outfit.items" />
            </div>
            <strong>{{ outfit.title }}</strong>
          </article>
        </div>
      </section>

      <p v-if="error" class="error-message">{{ error }}</p>

      <button class="detail-recommend-banner" type="button" @click="recommendWithItem">
        <PickleBuddy mood="clothes" size="tiny" />
        <span><strong>이 옷으로 잘 어울리는 코디를 찾아볼까요?</strong>추천 코디 보기</span>
      </button>
    </section>

    <div v-if="showMoreSheet" class="clothes-more-backdrop" @click="showMoreSheet = false"></div>
  </PhoneFrame>
</template>
