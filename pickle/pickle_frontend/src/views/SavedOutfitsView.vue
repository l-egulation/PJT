<script setup>
import { computed, onMounted, ref } from 'vue'
import client from '@/api/client'
import OutfitFlatlay from '@/components/OutfitFlatlay.vue'
import PhoneFrame from '@/components/PhoneFrame.vue'

const outfits = ref([])
const activeTpo = ref('all')
const loading = ref(true)
const deletingIds = ref(new Set())

const tpoFilters = [
  { value: 'all', label: '전체' },
  { value: 'daily', label: '데일리' },
  { value: 'work', label: '출근' },
  { value: 'date', label: '데이트' },
  { value: 'travel', label: '여행' },
]
const tpoLabels = Object.fromEntries(tpoFilters.map((item) => [item.value, item.label]))

const filteredOutfits = computed(() => (
  activeTpo.value === 'all'
    ? outfits.value
    : outfits.value.filter((outfit) => outfit.tpo === activeTpo.value)
))

function formatDate(value) {
  const date = value ? new Date(value) : null
  if (!date || Number.isNaN(date.getTime())) return ''
  return `${date.getMonth() + 1}월 ${date.getDate()}일`
}

async function loadOutfits() {
  loading.value = true
  try {
    const { data } = await client.get('/outfits/')
    outfits.value = data.results || data
  } finally {
    loading.value = false
  }
}

async function removeOutfit(outfit) {
  if (!outfit?.id || deletingIds.value.has(outfit.id)) return
  deletingIds.value = new Set([...deletingIds.value, outfit.id])
  try {
    await client.delete(`/outfits/${outfit.id}/`)
    outfits.value = outfits.value.filter((item) => item.id !== outfit.id)
  } finally {
    const next = new Set(deletingIds.value)
    next.delete(outfit.id)
    deletingIds.value = next
  }
}

onMounted(loadOutfits)
</script>

<template>
  <PhoneFrame title="내 코디" show-back show-nav>
    <template #action>
      <button class="plain-action icon-action" type="button" aria-label="검색">
        <span class="ui-icon" style="--icon: url('/icons/ui/search.svg')" aria-hidden="true"></span>
      </button>
    </template>

    <section class="saved-outfits-page">
      <p class="saved-outfits-count">저장한 코디 {{ outfits.length }}개</p>

      <nav class="saved-outfits-tabs" aria-label="코디 분류">
        <button
          v-for="item in tpoFilters"
          :key="item.value"
          type="button"
          :class="{ active: activeTpo === item.value }"
          @click="activeTpo = item.value"
        >
          {{ item.label }}
        </button>
      </nav>

      <section v-if="loading" class="saved-outfits-empty">
        저장한 코디를 불러오고 있어요.
      </section>
      <section v-else-if="!filteredOutfits.length" class="saved-outfits-empty">
        <span class="ui-icon" style="--icon: url('/icons/ui/scrap.svg')" aria-hidden="true"></span>
        <strong>저장한 코디가 없어요</strong>
        <p>추천 코디에서 스크랩하면 여기에서 모아볼 수 있어요.</p>
      </section>
      <section v-else class="saved-outfits-grid">
        <article v-for="outfit in filteredOutfits" :key="outfit.id" class="saved-outfit-card">
          <div class="saved-outfit-visual">
            <OutfitFlatlay :items="outfit.items" />
            <button
              type="button"
              :disabled="deletingIds.has(outfit.id)"
              aria-label="저장 코디 삭제"
              @click="removeOutfit(outfit)"
            >
              <span class="ui-icon" style="--icon: url('/icons/ui/scrap_filled.svg')" aria-hidden="true"></span>
            </button>
          </div>
          <h2>{{ outfit.title }}</h2>
          <div class="saved-outfit-meta">
            <span>{{ tpoLabels[outfit.tpo] || outfit.tpo }}</span>
            <time>{{ formatDate(outfit.created_at) }}</time>
          </div>
        </article>
      </section>
    </section>
  </PhoneFrame>
</template>
