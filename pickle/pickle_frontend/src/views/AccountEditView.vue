<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const saving = ref(false)
const error = ref('')
const draft = ref({
  username: '',
  email: '',
  password: '',
})

const canSave = computed(() => {
  const username = draft.value.username.trim()
  const email = draft.value.email.trim()
  return (
    (username && username !== (user.profile?.username || '')) ||
    (email && email !== (user.profile?.email || '')) ||
    draft.value.password.length > 0
  )
})

function syncDraft() {
  draft.value = {
    username: user.profile?.username || '',
    email: user.profile?.email || '',
    password: '',
  }
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

async function saveAccount() {
  if (!canSave.value || saving.value) return
  saving.value = true
  error.value = ''

  const payload = {}
  const username = draft.value.username.trim()
  const email = draft.value.email.trim()
  if (username && username !== user.profile?.username) payload.username = username
  if (email && email !== user.profile?.email) payload.email = email
  if (draft.value.password) payload.password = draft.value.password

  try {
    await user.updateProfile(payload)
    router.push('/my')
  } catch (requestError) {
    error.value = formatRequestError(requestError)
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  if (!user.profile) await user.loadProfile()
  syncDraft()
})
</script>

<template>
  <PhoneFrame>
    <section class="profile-edit-page">
      <header class="profile-edit-top">
        <button type="button" aria-label="뒤로 가기" @click="router.back()">
          <span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span>
        </button>
        <h1>회원정보 수정</h1>
        <span></span>
      </header>

      <form class="profile-edit-content profile-edit-form-page account-edit-form" @submit.prevent="saveAccount">
        <label>
          <strong>아이디</strong>
          <input v-model="draft.username" type="text" autocomplete="username" placeholder="아이디를 입력해 주세요" />
        </label>
        <label>
          <strong>이메일</strong>
          <input v-model="draft.email" type="email" autocomplete="email" placeholder="이메일을 입력해 주세요" />
        </label>
        <label>
          <strong>비밀번호 변경</strong>
          <input v-model="draft.password" type="password" autocomplete="new-password" placeholder="새 비밀번호" />
        </label>

        <p v-if="error" class="profile-edit-error">{{ error }}</p>
        <button class="profile-edit-save" type="submit" :disabled="!canSave || saving">
          {{ saving ? '저장 중' : '저장하기' }}
        </button>
      </form>
    </section>
  </PhoneFrame>
</template>
