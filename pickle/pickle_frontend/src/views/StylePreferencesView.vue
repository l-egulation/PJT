<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import TagChip from '@/components/TagChip.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const selected = ref([])
const saving = ref(false)
const error = ref('')
const labels = { minimal: '미니멀', casual: '꾸안꾸', lovely: '러블리', street: '스트릿', classic: '시크' }

onMounted(async () => {
  await user.loadStyles()
  const { data } = await client.get('/auth/style-preferences/')
  selected.value = data.style_tags?.map((tag) => tag.id) || []
})

function toggle(id) {
  selected.value = selected.value.includes(id)
    ? selected.value.filter((value) => value !== id)
    : [...selected.value, id]
}

async function save() {
  saving.value = true
  error.value = ''

  try {
    await user.updateStylePreferences(selected.value)
    router.push('/my')
  } catch {
    error.value = '스타일 저장에 실패했습니다. 잠시 후 다시 시도해 주세요.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <PhoneFrame title="나의 스타일" show-back>
    <section class="page-heading"><h1>선호 스타일 선택</h1><span>마이페이지에 표시할 스타일만 가볍게 수정해요.</span></section>
    <div class="tag-grid large"><TagChip v-for="tag in user.availableTags" :key="tag.id" :label="labels[tag.slug] || tag.name" :selected="selected.includes(tag.id)" @click="toggle(tag.id)" /></div>
    <p v-if="error" class="error-message">{{ error }}</p>
    <button class="peach-button fixed-bottom" type="button" :disabled="saving" @click="save">{{ saving ? '저장 중...' : '저장하기' }}</button>
  </PhoneFrame>
</template>
