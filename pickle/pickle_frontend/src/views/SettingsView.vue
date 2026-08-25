<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const saving = ref(false)
const deleting = ref(false)
const error = ref('')
const SETTINGS_KEY = 'pickle_general_settings'
const form = ref({
  compact_cards_enabled: false,
  image_data_saver_enabled: true,
  style_feedback_enabled: true,
})

const options = [
  { key: 'compact_cards_enabled', title: '간결한 카드 보기', desc: '홈과 마이페이지의 카드 간격을 조금 더 촘촘하게 봅니다.' },
  { key: 'image_data_saver_enabled', title: '이미지 데이터 절약', desc: '이동 중에는 이미지를 조금 더 가볍게 불러옵니다.' },
  { key: 'style_feedback_enabled', title: '스타일 개선 참여', desc: '취향 선택과 사용 흐름을 서비스 개선에 활용합니다.' },
]

onMounted(() => {
  try {
    form.value = { ...form.value, ...JSON.parse(localStorage.getItem(SETTINGS_KEY) || '{}') }
  } catch {
    localStorage.removeItem(SETTINGS_KEY)
  }
})

function save() {
  saving.value = true
  error.value = ''

  try {
    localStorage.setItem(SETTINGS_KEY, JSON.stringify(form.value))
    router.push('/my')
  } catch {
    error.value = '설정 저장에 실패했습니다. 잠시 후 다시 시도해 주세요.'
  } finally {
    saving.value = false
  }
}

async function deleteAccount() {
  if (saving.value || deleting.value) return
  const confirmed = window.confirm('회원 탈퇴 시 계정과 옷장, 코디 기록이 삭제됩니다. 정말 탈퇴할까요?')
  if (!confirmed) return

  deleting.value = true
  error.value = ''

  try {
    await user.deleteAccount()
    router.push('/')
  } catch {
    error.value = '회원 탈퇴에 실패했습니다. 잠시 후 다시 시도해 주세요.'
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <PhoneFrame title="설정" show-back>
    <section class="settings-list">
      <label v-for="option in options" :key="option.key" class="settings-row">
        <span><strong>{{ option.title }}</strong><small>{{ option.desc }}</small></span>
        <input v-model="form[option.key]" class="settings-checkbox" type="checkbox" />
      </label>
    </section>

    <section class="account-danger-zone">
      <div>
        <strong>회원 탈퇴</strong>
        <p>계정과 저장된 옷장, 코디 기록이 삭제됩니다.</p>
      </div>
      <button type="button" :disabled="saving || deleting" @click="deleteAccount">
        {{ deleting ? '탈퇴 중...' : '탈퇴하기' }}
      </button>
    </section>

    <p v-if="error" class="error-message">{{ error }}</p>
    <button class="primary-button fixed-bottom" type="button" :disabled="saving || deleting" @click="save">{{ saving ? '저장 중...' : '저장하기' }}</button>
  </PhoneFrame>
</template>
