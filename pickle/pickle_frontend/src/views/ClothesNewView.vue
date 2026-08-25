<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { COLOR_OPTIONS } from '@/utils/colors'
import { USER_STYLE_OPTIONS } from '@/utils/styles'

const router = useRouter()
const saving = ref(false)
const analyzing = ref(false)
const analysis = ref(null)
const error = ref('')
const image = ref(null)
const preview = ref('')
const form = ref({ name: '', category: 'top', color: 'ivory', material: '', style: '', season: ['all'], shopping_url: '', memo: '' })
const categories = [
  { key: 'top', label: '상의' },
  { key: 'bottom', label: '하의' },
  { key: 'outer', label: '아우터' },
  { key: 'dress', label: '원피스' },
  { key: 'shoes', label: '신발' },
  { key: 'bag', label: '가방' },
  { key: 'accessory', label: '액세서리' },
]
const styleOptions = USER_STYLE_OPTIONS
const seasonOptions = [['spring', '봄'], ['summer', '여름'], ['fall', '가을'], ['winter', '겨울'], ['all', '사계절']]

function parseSeasons(value) {
  const seasons = Array.isArray(value) ? value : String(value || 'all').split(',')
  const cleaned = seasons.map((season) => season.trim()).filter(Boolean)
  return cleaned.length ? cleaned : ['all']
}

function toggleSeason(key) {
  if (key === 'all') {
    form.value.season = ['all']
    return
  }
  const next = form.value.season.filter((season) => season !== 'all')
  const index = next.indexOf(key)
  if (index >= 0) next.splice(index, 1)
  else next.push(key)
  form.value.season = next.length ? next : ['all']
}

function appendForm(body) {
  Object.entries(form.value).forEach(([key, value]) => {
    body.append(key, key === 'season' ? parseSeasons(value).join(',') : value)
  })
}

function formatRequestError(requestError) {
  const data = requestError.response?.data
  if (!data) return '서버 연결을 확인해 주세요.'
  if (typeof data === 'string') return data
  if (data.detail) return data.detail

  return Object.entries(data)
    .flatMap(([field, messages]) => {
      const values = Array.isArray(messages) ? messages : [messages]
      return values.map((message) => `${field}: ${message}`)
    })
    .join('\n')
}

async function onFileChange(event) {
  image.value = event.target.files?.[0] || null
  if (!image.value) return
  preview.value = URL.createObjectURL(image.value)
  analyzing.value = true
  error.value = ''
  const body = new FormData()
  body.append('image', image.value)
  try {
    const { data } = await client.post('/closet/analyze-image/', body, { timeout: 30000 })
    analysis.value = data
    form.value.category = data.category
    form.value.color = data.color
    form.value.season = parseSeasons(data.season)
    if (data.style) form.value.style = data.style
    if (!form.value.name) form.value.name = `나의 ${categories.find((item) => item.key === data.category)?.label || '옷'}`
  } catch (requestError) {
    error.value = requestError.response?.data?.detail || '이미지 자동 분석에 실패했습니다. 직접 입력해도 저장할 수 있어요.'
  } finally {
    analyzing.value = false
  }
}

async function save() {
  if (saving.value || analyzing.value || !form.value.name.trim()) return
  saving.value = true
  error.value = ''
  const body = new FormData()
  appendForm(body)
  if (image.value) body.append('image', image.value)
  try {
    await client.post('/closet/', body, { timeout: 30000 })
    router.push('/closet')
  } catch (requestError) {
    error.value = formatRequestError(requestError)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <PhoneFrame title="옷 등록" show-back>
    <template #action>
      <button class="plain-action icon-action" type="button" aria-label="저장" :disabled="saving || analyzing" @click="save">
        <span class="ui-icon" style="--icon: url('/icons/ui/check.svg')" aria-hidden="true"></span>
      </button>
    </template>
    <label class="upload-card">
      <input type="file" accept="image/*" @change="onFileChange" />
      <img v-if="preview" :src="preview" alt="옷 미리보기" />
      <template v-else>
        <span class="upload-plus ui-icon" style="--icon: url('/icons/ui/camera.svg')" aria-hidden="true"></span>
        <strong>사진 추가</strong>
        <small>사진을 선택하면 AI가 자동 분석해요</small>
      </template>
    </label>
    <div v-if="analyzing" class="analysis-status">이미지 특징을 분석하고 있어요...</div>
    <div v-else-if="analysis" class="analysis-status success">
      <strong>AI 자동 입력 완료</strong>
      <span>
        카테고리 {{ Math.round(analysis.confidence.category * 100) }}% ·
        계절 {{ Math.round(analysis.confidence.season * 100) }}%
        <template v-if="analysis.confidence.style"> · 스타일 {{ Math.round(analysis.confidence.style * 100) }}%</template>
        <template v-if="analysis.aihub_style"> · 세부 {{ analysis.aihub_style }}</template>
      </span>
    </div>
    <form class="modern-form" @submit.prevent="save">
      <label>쇼핑몰 URL <small>(선택)</small><input v-model="form.shopping_url" type="url" placeholder="https://" /></label>
      <label>옷 이름<input v-model="form.name" required placeholder="예) 그린 린넨 셔츠" /></label>
      <label>카테고리<select v-model="form.category"><option v-for="item in categories" :key="item.key" :value="item.key">{{ item.label }}</option></select></label>
      <label>스타일<select v-model="form.style"><option value="">선택 안 함</option><option v-for="item in styleOptions" :key="item.id" :value="item.value">{{ item.label }}</option></select></label>
      <div class="form-group">
        <p>색상</p>
        <div class="color-row expanded-colors">
          <button v-for="color in COLOR_OPTIONS" :key="color.name" class="color-dot" :class="{ selected: form.color === color.name }" :style="{ '--color': color.hex }" :title="color.label" type="button" @click="form.color = color.name" />
        </div>
        <small>{{ COLOR_OPTIONS.find((color) => color.name === form.color)?.label }}</small>
      </div>
      <div class="form-group"><p>시즌</p><div class="tag-grid"><button v-for="season in seasonOptions" :key="season[0]" class="tag-chip" :class="{ selected: form.season.includes(season[0]) }" type="button" @click="toggleSeason(season[0])">{{ season[1] }}</button></div></div>
      <label>소재<input v-model="form.material" placeholder="예) 코튼, 데님, 울" /></label>
      <p v-if="error" class="error-message">{{ error }}</p>
      <button class="primary-button wide" :disabled="saving || analyzing">{{ saving ? '저장 중...' : '저장하기' }}</button>
    </form>
  </PhoneFrame>
</template>
