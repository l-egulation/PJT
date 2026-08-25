<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const saving = ref(false)
const error = ref('')
const form = ref({
  push_notifications_enabled: true,
  weather_notifications_enabled: true,
  community_notifications_enabled: true,
  marketing_notifications_enabled: false,
})

const options = [
  { key: 'push_notifications_enabled', title: '푸시 알림', desc: '중요한 서비스 알림을 받아요.' },
  { key: 'weather_notifications_enabled', title: '날씨 알림', desc: '날씨에 맞는 코디 힌트를 받아요.' },
  { key: 'community_notifications_enabled', title: '스냅 알림', desc: '좋아요, 댓글, 팔로우 소식을 받아요.' },
  { key: 'marketing_notifications_enabled', title: '이벤트 알림', desc: '새 기능과 이벤트 소식을 받아요.' },
]

onMounted(async () => {
  const data = await user.loadSettings()
  form.value = {
    push_notifications_enabled: data.push_notifications_enabled,
    weather_notifications_enabled: data.weather_notifications_enabled,
    community_notifications_enabled: data.community_notifications_enabled,
    marketing_notifications_enabled: data.marketing_notifications_enabled,
  }
})

async function save() {
  saving.value = true
  error.value = ''

  try {
    await user.updateSettings(form.value)
    router.push('/my')
  } catch {
    error.value = '알림 설정 저장에 실패했습니다. 잠시 후 다시 시도해 주세요.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <PhoneFrame title="알림 설정" show-back>
    <section class="settings-list">
      <label v-for="option in options" :key="option.key" class="settings-row">
        <span><strong>{{ option.title }}</strong><small>{{ option.desc }}</small></span>
        <input v-model="form[option.key]" class="settings-checkbox" type="checkbox" />
      </label>
    </section>

    <p v-if="error" class="error-message">{{ error }}</p>
    <button class="primary-button fixed-bottom" type="button" :disabled="saving" @click="save">{{ saving ? '저장 중...' : '저장하기' }}</button>
  </PhoneFrame>
</template>
