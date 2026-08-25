<script setup>
import { computed, nextTick, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import ClothesCard from '@/components/ClothesCard.vue'
import PhoneFrame from '@/components/PhoneFrame.vue'

const categories = [
  { key: 'all', label: '전체' },
  { key: 'top', label: '상의' },
  { key: 'bottom', label: '하의' },
  { key: 'outer', label: '아우터' },
  { key: 'dress', label: '원피스' },
  { key: 'shoes', label: '신발' },
  { key: 'bag', label: '가방' },
  { key: 'accessory', label: '액세서리' },
]

const route = useRoute()
const router = useRouter()
const selectedCategory = ref(route.query.category || 'all')
const clothes = ref([])
const loading = ref(true)
const searchOpen = ref(false)
const searchQuery = ref('')
const searchInput = ref(null)
const isReplaceMode = computed(() => route.query.from === 'recommend' && route.query.replace)
const filteredClothes = computed(() => {
  const categoryFiltered = selectedCategory.value === 'all'
    ? clothes.value
    : clothes.value.filter((item) => item.category === selectedCategory.value)
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return categoryFiltered
  return categoryFiltered.filter((item) => searchableText(item).includes(query))
})

onMounted(async () => {
  const { data } = await client.get('/closet/')
  clothes.value = data.results || data
  loading.value = false
})

function selectReplacement(item) {
  if (!isReplaceMode.value) return
  const stored = JSON.parse(sessionStorage.getItem('pickle_recommendation') || 'null')
  if (!stored?.items?.length) return router.push('/recommend/result')
  const replaceId = Number(route.query.replace)
  stored.items = stored.items.map((current) => current.id === replaceId ? item : current)
  sessionStorage.setItem('pickle_recommendation', JSON.stringify(stored))
  router.push('/recommend/result')
}

function searchableText(item) {
  const category = categories.find((entry) => entry.key === item.category)?.label
  return [
    item.name,
    category,
    item.category,
    item.color,
    item.season,
    item.style,
    item.memo,
    item.description,
  ].filter(Boolean).join(' ').toLowerCase()
}

function toggleSearch() {
  searchOpen.value = !searchOpen.value
  if (searchOpen.value) {
    nextTick(() => searchInput.value?.focus())
  } else {
    searchQuery.value = ''
  }
}
</script>

<template>
  <PhoneFrame title="내 옷장" show-back show-nav>
    <template #action>
      <button class="plain-action icon-action" type="button" aria-label="검색" @click="toggleSearch">
        <span class="ui-icon" style="--icon: url('/icons/ui/search.svg')" aria-hidden="true"></span>
      </button>
    </template>
    <p v-if="isReplaceMode" class="replace-helper">교체할 옷을 선택해 주세요.</p>
    <div class="category-scroll padded">
      <button v-for="category in categories" :key="category.key" class="category-tab" :class="{ active: selectedCategory === category.key }" @click="selectedCategory = category.key">{{ category.label }}</button>
    </div>
    <div v-if="searchOpen" class="closet-search-panel">
      <span class="ui-icon" style="--icon: url('/icons/ui/search.svg')" aria-hidden="true"></span>
      <input ref="searchInput" v-model="searchQuery" type="search" placeholder="찾고 싶은 옷을 검색해보세요" />
      <button v-if="searchQuery" type="button" aria-label="검색어 지우기" @click="searchQuery = ''">×</button>
    </div>
    <p v-if="loading" class="empty-state">옷장을 불러오는 중...</p>
    <p v-else-if="!filteredClothes.length" class="empty-state">{{ searchQuery ? '검색 결과가 없습니다.' : '등록된 옷이 없습니다.' }}</p>
    <div v-else class="clothes-grid">
      <ClothesCard v-for="item in filteredClothes" :key="item.id" :item="item" :selectable="isReplaceMode" @select="selectReplacement" />
    </div>
    <button class="floating-add" type="button" aria-label="옷 등록" @click="$router.push('/clothes/new')">
      <span class="ui-icon" style="--icon: url('/icons/ui/plus.svg')" aria-hidden="true"></span>
    </button>
  </PhoneFrame>
</template>
